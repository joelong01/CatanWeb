using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using Xunit;

namespace Tests.DesktopApp.UI.ScriptedTestData
{
    /// <summary>
    /// Provides robust UI automation helper methods for interacting with the Catan Desktop application.
    /// 
    /// Key Responsibilities:
    /// - Low-level UI element interaction (clicking, text input, etc.)
    /// - AutomationElement caching for performance optimization
    /// - Game state verification through GameModel deserialization
    /// - Robust element finding with retry logic and error handling
    /// 
    /// Architecture:
    /// - Wraps FlaUI automation framework for WinUI3 application testing
    /// - Maintains a cache of AutomationElements for efficient repeated access
    /// - Provides typed methods for different UI interaction patterns
    /// - Handles timing issues with configurable wait periods
    /// 
    /// Usage Pattern:
    /// 1. Create instance with main window and automation references
    /// 2. Call LoadAutomationObjects() to populate element cache
    /// 3. Use typed methods (ClickButton, ClickBuilding, etc.) for interactions
    /// 4. Use VerifyGameState() for state assertions
    /// </summary>
    public class UIAutomationHelper : IDisposable
    {
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private readonly Dictionary<string, AutomationElement> _uiControlsCache;
        private readonly ConditionFactory _cf;
        private const int SHORT_WAIT = 750;

        public UIAutomationHelper(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
            _uiControlsCache = new Dictionary<string, AutomationElement>();
            _cf = new ConditionFactory(new UIA3PropertyLibrary());
        }

        /// <summary>
        /// Populates the AutomationElement cache by scanning all UI elements in the main window.
        /// 
        /// Process:
        /// 1. Clears any existing cache entries
        /// 2. Recursively finds all descendant elements in the main window
        /// 3. Extracts AutomationId from each element and stores in cache
        /// 4. Logs summary statistics by element type (Roads, Buildings, Tiles, Rolls)
        /// 5. Provides sample AutomationIds for debugging
        /// 
        /// Performance Impact: This is a one-time expensive operation that significantly speeds up
        /// subsequent element lookups throughout the test execution.
        /// 
        /// Must be called after the game board is fully loaded but before any UI interactions.
        /// </summary>
        public void LoadAutomationObjects()
        {
            TraceMessage("=== Loading Automation Objects ===");
            _uiControlsCache.Clear();

            var allElements = _mainWindow.FindAllDescendants();
            foreach (var element in allElements)
            {
                try
                {
                    var automationId = element.Properties.AutomationId.ValueOrDefault;
                    if (!string.IsNullOrEmpty(automationId))
                    {
                        _uiControlsCache[automationId] = element;
                    }
                }
                catch (Exception ex)
                {
                    TraceMessage($"  Skipped element due to error: {ex.Message}");
                }
            }

            TraceMessage($"✅ Loaded {_uiControlsCache.Count} automation objects into cache");

            var roadElements = _uiControlsCache.Values.Count(obj => obj.AutomationId.StartsWith("Road"));
            var buildingElements = _uiControlsCache.Values.Count(obj => obj.AutomationId.StartsWith("Building"));
            var tileElements = _uiControlsCache.Values.Count(obj => obj.AutomationId.StartsWith("Tile"));
            var rollElements = _uiControlsCache.Values.Count(obj => obj.AutomationId.StartsWith("Roll"));
            
            // Also count coordinate-based building elements (format like (-3,3,0)-Right)
            var coordinateBuildingElements = _uiControlsCache.Values.Count(obj => 
                obj.AutomationId.Contains("(") && obj.AutomationId.Contains(")") && obj.AutomationId.Contains("-"));

            TraceMessage($"  Roads: {roadElements}, Buildings: {buildingElements}, CoordinateBuildings: {coordinateBuildingElements}, Tiles: {tileElements}, Rolls: {rollElements}");
            
            // Show sample AutomationIds for debugging
            var sampleIds = _uiControlsCache.Keys.Take(10).ToArray();
            TraceMessage($"  Sample AutomationIds: {string.Join(", ", sampleIds)}");
            
            // Show coordinate building samples specifically
            var coordinateSamples = _uiControlsCache.Keys
                .Where(id => id.Contains("(") && id.Contains(")") && id.Contains("-"))
                .Take(5)
                .ToArray();
            if (coordinateSamples.Any())
            {
                TraceMessage($"  Sample Coordinate Buildings: {string.Join(", ", coordinateSamples)}");
            }
        }

        /// <summary>
        /// Clicks a button with the specified automation ID robustly.
        /// </summary>
        public void ClickButton(string automationId)
        {
            TraceMessage($"Clicking button: {automationId}");
            
            var button = FindByAutomationId(automationId).AsButton();
            Assert.NotNull(button);
            
            if (!button.IsEnabled)
            {
                throw new InvalidOperationException($"Button '{automationId}' is not enabled");
            }
            
            button.Invoke();
            Thread.Sleep(SHORT_WAIT);
        }

        /// <summary>
        /// Clicks the Next button to advance game state.
        /// </summary>
        public void ClickNext()
        {
            ClickButton("NextButton");
        }

        /// <summary>
        /// Clicks a roll card with the specified number.
        /// </summary>
        public void ClickRoll(int rollNumber)
        {
            var id = $"Roll - {rollNumber}";
            TraceMessage($"Clicking roll: {id}");

            // Activate the main window first
            var win = _mainWindow.AsWindow();
            try { win.Focus(); } catch { /* best effort */ }

            // Locate the roll card by AutomationId
            var card = FindByAutomationId(id);
            
            // Prefer the inner Button under the card
            var btn = card.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton()
                     ?? card.AsButton();

            Assert.NotNull(btn);

            // If virtualized/offscreen, scroll it into view
            btn.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();

            // Wait until interactable
            Retry.WhileTrue(
                () => !btn.IsEnabled || btn.IsOffscreen,
                timeout: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100));

            // Snapshot pre-action state
            var preHash = GetCurrentGameModel().GameHash;

            // Give focus
            try { btn.Focus(); } catch { /* ignore */ }

            // Prefer Invoke; fall back to Click
            var inv = btn.Patterns.Invoke.PatternOrDefault;
            if (inv != null) 
                inv.Invoke();
            else 
                btn.Click();

            // Wait for the model to change
            var changed = Retry.WhileTrue(
                () => string.Equals(GetCurrentGameModel().GameHash, preHash, StringComparison.Ordinal),
                timeout: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100)).Success;

            if (!changed)
            {
                throw new Exception($"Roll '{id}' did not change the GameModel within timeout.");
            }

            Thread.Sleep(SHORT_WAIT);
        }

        /// <summary>
        /// Clicks on a building element.
        /// </summary>
        public void ClickBuilding(string buildingKey)
        {
            TraceMessage($"Clicking building: {buildingKey}");
            
            var buildingElement = _uiControlsCache.ContainsKey(buildingKey) 
                ? _uiControlsCache[buildingKey] 
                : FindByAutomationId(buildingKey);
                
            Assert.NotNull(buildingElement);
            buildingElement.Click();
            Thread.Sleep(SHORT_WAIT);
        }

        /// <summary>
        /// Clicks on a road element.
        /// </summary>
        public void ClickRoad(string roadKey)
        {
            TraceMessage($"Clicking road: {roadKey}");
            
            var roadElement = _uiControlsCache.ContainsKey(roadKey) 
                ? _uiControlsCache[roadKey] 
                : FindByAutomationId(roadKey);
                
            Assert.NotNull(roadElement);
            roadElement.Click();
            Thread.Sleep(SHORT_WAIT);
        }


        /// <summary>
        /// Retrieves the current GameModel from the UI.
        /// </summary>
        public GameModel GetCurrentGameModel()
        {
            var nextButton = FindByAutomationId("NextButton");
            Assert.NotNull(nextButton);
            
            if (nextButton.Properties.ItemStatus.TryGetValue(out var buttonGameModelValue))
            {
                var buttonGameModelJson = buttonGameModelValue as string;
                if (!string.IsNullOrEmpty(buttonGameModelJson))
                {
                    var buttonGameModel = JsonSerializer.Deserialize<GameModel>(buttonGameModelJson, JsonHelper.StandardOptions);
                    Assert.NotNull(buttonGameModel);
                    return buttonGameModel;
                }
            }

            throw new Exception("Unable to retrieve GameModel from UI");
        }

        /// <summary>
        /// Verifies that the current game state matches the expected state.
        /// </summary>
        public void VerifyGameState(GameState expectedState)
        {
            TraceMessage($"Verifying expected GameState: {expectedState}");
            
            var currentGameState = GetCurrentGameModel().GameState;
            TraceMessage($"Current GameState: {currentGameState}, Expected: {expectedState}");
            
            Assert.Equal(expectedState, currentGameState);
        }

        /// <summary>
        /// Waits for the game state to transition to the expected state.
        /// </summary>
        public bool WaitForGameState(GameState expectedState, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            TraceMessage($"WaitForGameState: Looking for '{expectedState}' state");

            while (sw.Elapsed < timeout)
            {
                var currentGameState = GetCurrentGameModel().GameState;

                if (currentGameState == expectedState)
                {
                    TraceMessage($"WaitForGameState: Found expected state '{expectedState}'!");
                    return true;
                }

                Thread.Sleep(SHORT_WAIT);
            }

            TraceMessage($"WaitForGameState: Timed out waiting for '{expectedState}' after {timeout.TotalSeconds}s");
            return false;
        }

        /// <summary>
        /// Finds a UI element by its automation ID.
        /// </summary>
        private AutomationElement FindByAutomationId(string automationId)
        {
            if (_uiControlsCache.ContainsKey(automationId))
            {
                return _uiControlsCache[automationId];
            }

            var res = Retry.WhileNull(
                () => _mainWindow.FindFirstDescendant(_cf.ByAutomationId(automationId)),
                timeout: TimeSpan.FromMilliseconds(SHORT_WAIT),
                interval: TimeSpan.FromMilliseconds(100),
                throwOnTimeout: false);

            return res.Result ?? throw new TimeoutException($"AutomationId '{automationId}' not found under main window in {SHORT_WAIT} ms.");
        }

        /// <summary>
        /// Finds UI elements by text content.
        /// </summary>
        public AutomationElement[] FindAllByText(string text)
        {
            return _mainWindow.FindAllDescendants(cf => cf.ByText(text));
        }

        private void TraceMessage(string message, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0)
        {
            var output = $"UIAutomationHelper({cln}): {message} [Caller={cmb}]";
            System.Diagnostics.Debug.WriteLine(output);
        }

        public void Dispose()
        {
            _uiControlsCache.Clear();
        }
    }
}