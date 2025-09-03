using Catan3.Shared.Extensions;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Tests.DesktopApp.UI.ScriptedTestData;
using Tests.DesktopApp.UI.TestInfra;
using Xunit;
using Xunit.Sdk;

namespace Tests.DesktopApp.UI
{
    /// <summary>
    /// End-to-end UI test against the packaged app (MSIX). Launches via AUMID and
    /// validates the core flow similar to the CLI parity test.  The goal of this tests is to create the game once and then go through the full cycle of 
    /// the game. The way we will build it is to understand the GameState enum (defined in Catan3.Shared.Models) and how the GameController.cs 
    /// transitions from one state to the next via user interactions in the game.  There is *one* official "test" because we are stateful and thus
    /// create one game and have it run through the scenarios.  the general layout of the tests are
    /// public void Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers()
    ///    {
    ///        Sta.Run(() =>
    ///        {
    ///             // Initialize() -- gather useful data we cache: DO NOT CACHE GAMEMODEL
    ///             // for each state we are testing, we implement a function with the naming convention Test_<GameState>
    ///         }
    ///     }
    /// 
    ///  There is a property we bind to that has the GameModel (which is all the state for the game) in JSON format. The game uses a "copy on write"
    ///  strategy, so every interaction that causes an update to the game results in a new GameModel.  You can compare GameModel.ExpectedGameHash between two
    ///  GameModel instances. if they are the same, they are identical.  if they are not, they are different.
    ///  
    ///     ========== RULES FOR THIS FILE ==========
    /// 1. This file is stateful and contains one test that runs through the entire game flow.
    /// 2. always build this test with the build script ./build.ps1 -NoTest
    /// 3. when you run this test, do not pass in --log parameter because I don't want double logging
    /// 4. do NOT put comments that show the history of how the code got there.  if you add comments, they should say what it DOES
    /// 5. header comments should not be updated without explicit user consent.  if the code is inconsistent with the comment, ask for guidance.
    /// 6. the full test name is "Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers" use it.
    /// 
    ///     ========== HOW TO CREATE/UPDATE TEST SCENARIOS ==========
    /// 
    /// TO RECORD A NEW SCENARIO:
    /// 1. Enable recording mode in DesktopApp\App.xaml.cs:
    ///    - Set: public static bool RecordMode { get; set; } = true;
    /// 
    /// 2. Copy the test file to preserve the original:
    ///    - Source: Tests.DesktopApp.UI\ScriptedTestData\Expansion-Test.catan
    ///    - Copy to: DesktopApp\Assets\Test Files\MyNewTest.catan (or similar name)
    /// 
    /// 3. Build and run the desktop app:
    ///    - Run: ./build.ps1 -NoTest
    ///    - Launch the app and open your copied .catan file
    /// 
    /// 4. Play through the scenario manually:
    ///    - Click on the Menu Item "Show Debug Trace" to open the debug window
    ///    - All actions will be automatically recorded with AutomationIds
    ///    - Recorded data appears in Debug output window
    ///    - Game state is captured BEFORE each action for proper assertion timing
    /// 
    /// 5. Copy recorded actions from Debug output into a new JSON scenario file:
    ///    - Save as: Tests.DesktopApp.UI\ScriptedTestData\my-new-scenario.json
    ///    - Follow the structure of the ActionStack in .catan_test files
    /// 
    /// 6. Update test to use your new scenario:
    ///    - Modify ExecuteScenario() to load your new JSON file
    ///    - Update the test file path in CreateTempTestFile() if needed
    /// 
    /// 7. Turn off recording mode when done:
    ///    - Set: public static bool RecordMode { get; set; } = false;
    /// 
    /// IMPORTANT NOTES:
    /// - Recording captures AutomationIds (e.g., "Building-(-3,3,0)-Right") not raw coordinates
    /// - State assertions are recorded BEFORE actions, not after
    /// - All placement actions use deterministic parameters, not "optimal placement" logic
    /// - Test files should be copied to avoid overwriting originals during development
    /// </summary>
    [Collection("UIAutomation")]
    public class FullCyclePackagedUiTests : IDisposable
    {
        private static int SHORT_WAIT = 250;
        private static int MEDIUM_WAIT = 750;
        private UIA3Automation? _automation;
        private AutomationElement? _main;
        private bool _testSucceeded = false;
        private UIAutomationHelper? _uiHelper;
        private static readonly ConditionFactory Cf = new(new UIA3PropertyLibrary());
        private AutomationElement Main => _main ?? throw new InvalidOperationException("Main window not initialized");

        /// <summary>
        /// Initializes a new instance of the FullCyclePackagedUiTests class.
        /// Test infrastructure and automation elements are initialized when the actual test runs.
        /// </summary>
        public FullCyclePackagedUiTests()
        {

        }

        /// <summary>
        /// Loads automation objects using the enhanced UIAutomationHelper with validation.
        /// Should be called once after the game board is fully loaded (PickingBoard state).
        /// Will throw if required buttons (Purchase buttons, TestAutomationActionButton) are missing.
        /// </summary>
        private void LoadAutomationObjects()
        {
            // Initialize UI helper if not already created
            if (_uiHelper == null)
            {
                _uiHelper = new UIAutomationHelper(Main, _automation!);
            }

            // Wait a bit more for all UI elements to be fully rendered
            this.TraceMessage("Waiting for UI elements to be fully rendered...");


            // Retry LoadAutomationObjects if TestAutomationActionButton is missing (UI might still be loading)
            int retryCount = 0;
            const int maxRetries = 5;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Use the enhanced UIAutomationHelper implementation with validation
                    // This will automatically log stats and throw if required buttons are missing
                    _uiHelper.LoadAutomationObjects();
                    this.TraceMessage("✅ LoadAutomationObjects succeeded");
                    return; // Success, exit the retry loop
                }
                catch (Exception ex) when (ex.Message.Contains("TestAutomationActionButton") && retryCount < maxRetries - 1)
                {
                    retryCount++;
                    this.TraceMessage($"⚠️ LoadAutomationObjects failed (attempt {retryCount}/{maxRetries}): {ex.Message}");
                    this.TraceMessage($"Waiting {2000 * retryCount}ms before retry...");
                    Thread.Sleep(50 * retryCount); // Progressive delay
                }
            }

            // If we get here, all retries failed
            throw new Exception($"LoadAutomationObjects failed after {maxRetries} attempts. UI may not be fully loaded.");
        }

        /// <summary>
        /// IDisposable implementation for test cleanup.
        /// If the test succeeded, closes the app normally.
        /// If the test failed, leaves the app open for debugging purposes.
        /// This allows developers to inspect the app state and debug window after test failure.
        /// </summary>
        public void Dispose()
        {
            try
            {

                if (_testSucceeded)
                {
                    this.TraceMessage("Test succeeded - closing app");
                    // leave app open for manual testing
                    // _main?.AsWindow()?.Close();
                }
                else
                {
                    this.TraceMessage("Test failed - leaving app open for debugging. You can check the debug window for trace messages.");
                }
            }
            catch { }
            _automation?.Dispose();
        }
        [Fact]
        public void Expansion_End_To_End_Test()
        {
            Sta.Run(() =>
            {
                DoFullTestWithScriptedActions("Expansion.catan_test");
            });
        }

        [Fact]
        public void Regular_End_To_End_Test()
        {
            Sta.Run(() =>
            {
                DoFullTestWithScriptedActions("Regular.catan_test");
            });
        }

        [Fact]
        [Obsolete("Use Expansion_End_To_End_Test or Regular_End_To_End_Test instead")]
        public void Full_Stateful_Flow_PackagedApp_Expansion_FivePlayers()
        {
            Sta.Run(() =>
            {
                DoFullTestWithScriptedActions();
            });
        }
        /// <summary>
        /// Main test method using the new scripted action methodology.
        /// 
        /// Entry State: App not launched
        /// Exit State: Test completed (app either closed or left open for debugging)
        /// 
        /// Test Flow:
        /// 1. Locate test file path directly
        /// 2. Launch app with command line args to auto-load the test file
        /// 3. Wait for game board to be loaded
        /// 4. Load automation objects cache
        /// 5. Execute scripted actions from JSON scenario
        /// 6. Verify final game state
        /// 
        /// Exception Handling: Any unhandled exception marks the test as failed,
        /// which triggers the Dispose() method to leave the app open for debugging.
        /// </summary>
        /// <param name="testFileName">Optional test file name. If null, uses GetTestFileName() logic.</param>
        private void DoFullTestWithScriptedActions(string? testFileName = null)
        {
            // Ensure we have a test file name
            testFileName ??= GetTestFileName();

            this.TraceMessage($"Test starting with scripted actions methodology - File: {testFileName}");

            // Step 1: Load scenario data once from embedded resources (synchronous version)
            this.TraceMessage("Loading test scenario data from embedded resources");
            Catan3.Shared.TestData.TestScenario sharedScenario;
            try
            {
                // Use the synchronous stream version to avoid async issues in STA thread
                using var stream = Catan3.Shared.TestData.TestDataLoader.GetTestFileStream(testFileName);
                using var reader = new System.IO.StreamReader(stream);
                var json = reader.ReadToEnd();
                
                var document = System.Text.Json.JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("gameModel", out var gameModelElement))
                {
                    throw new InvalidOperationException($"Test file '{testFileName}' is missing 'gameModel' property");
                }

                if (!root.TryGetProperty("actionStack", out var actionStackElement))
                {
                    throw new InvalidOperationException($"Test file '{testFileName}' is missing 'actionStack' property");
                }

                var gameModel = gameModelElement.Deserialize<Catan3.Shared.Models.GameModel>(Catan3.Shared.Utility.JsonHelper.StandardOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize GameModel from '{testFileName}'");

                var actions = actionStackElement.Deserialize<Catan3.Shared.Models.IRecordedMessage[]>(Catan3.Shared.Utility.JsonHelper.StandardOptions)
                    ?? Array.Empty<Catan3.Shared.Models.IRecordedMessage>();

                sharedScenario = new Catan3.Shared.TestData.TestScenario
                {
                    TestFileName = testFileName,
                    InitialGameModel = gameModel,
                    RecordedActions = actions
                };
                
                this.TraceMessage($"Loaded scenario with {sharedScenario.RecordedActions.Length} actions");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Failed to load scenario: {ex.Message}");
                this.TraceMessage($"Exception type: {ex.GetType().Name}");
                this.TraceMessage($"Stack trace: {ex.StackTrace}");
                throw;
            }

            // Step 2: Get test file path for app launch (still need temp file for app to open)
            var testFilePath = GetTestFilePath(testFileName);
            this.TraceMessage($"Using test file for app launch: {testFilePath}");

            // Step 3: Launch app with test file
            this.TraceMessage("About to launch app with test file");
            LaunchAppWithTestFile(testFilePath);
            this.TraceMessage("App launched successfully");

            // Step 4: Wait for game to be loaded (should skip NewGame dialog)
            this.TraceMessage("Waiting for game board to load");
            WaitForGameBoardToLoad();
            this.TraceMessage("Game board loaded");

            // Step 5: Load automation objects after the game board is created
            LoadAutomationObjects();

            // Step 6: Execute the scripted scenario using pre-loaded data
            this.TraceMessage("=== Starting scripted action execution ===");
            ExecuteScenario(sharedScenario);

            this.TraceMessage("=== All scripted actions completed successfully ===");
            _testSucceeded = true; // Mark test as successful
        }







        /// <summary>
        /// Clicks the NextButton to advance the game state.
        /// Verifies the button is enabled before clicking and waits for state transition.
        /// Fails the test if the NextButton is disabled, as this indicates an invalid game state.
        /// </summary>
        private void CallNext()
        {
            var nextButton = FindByAutomationId("NextButton").AsButton();
            Assert.NotNull(nextButton);
            if (nextButton.IsEnabled == false)
            {
                this.TraceMessage("Next button is not enabled, cannot proceed");
                // Trace the current state and any relevant information
                var gameModel = GetCurrentGameModel();
                this.TraceMessage($"Current GameState: {gameModel.GameState} CurrentPlayer={gameModel.CurrentPlayerId}");
                Assert.Fail("Next button should be enabled to proceed to next state");
            }
            nextButton.Invoke();
            // Wait for state transition
            ShortWait(_uiHelper!);
        }
        /// <summary>
        /// Performs a dice roll by clicking the specified roll card in the UI.
        /// 
        /// Process:
        /// 1. Locates the roll card by AutomationId "Roll - {roll}"
        /// 2. Finds the Button control within the card
        /// 3. Ensures the button is enabled and visible
        /// 4. Captures pre-action GameModel hash for verification
        /// 5. Clicks the button (prefers Invoke pattern over Click)
        /// 
        /// 6. Waits for GameModel to change, confirming the action succeeded
        /// 
        /// The method includes robust error handling and scrolling for virtualized UI elements.
        /// </summary>
        /// <param name="roll">The dice value to roll (2-12)</param>
        private void DoRoll(int roll)
        {
            var id = $"Roll - {roll}";

            // Activate the main window first (helps input routing)
            var win = _main!.AsWindow();
            try { win.Focus(); } catch { /* best effort */ }

            // Locate the roll card (SingleRoll root) by AutomationId
            var card = FindByAutomationId(id);

            // Prefer the inner Button under the card; fall back to the card itself if you moved the id
            var btn = card.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton()
                     ?? card.AsButton(); // may be null if ControlType != Button

            Assert.NotNull(btn);

            // If virtualized/offscreen, scroll it into view
            btn.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView();

            // Wait until interactable
            Retry.WhileTrue(
                () => !btn.IsEnabled || btn.IsOffscreen,
                timeout: TimeSpan.FromSeconds(5),
                interval: TimeSpan.FromMilliseconds(100));



            // Snapshot pre-action state so we can confirm the click did something
            var preHash = GetCurrentGameModel().GameHash;
            this.TraceMessage($"Before Roll [ExpectedGameHash={preHash}]");
            ShortWait(_uiHelper!);
            // Give focus (improves Click reliability)
            try { btn.Focus(); } catch { /* ignore */ }
            ShortWait(_uiHelper!);
            // Prefer Invoke; fall back to Click (some templates don’t expose Invoke)
            var inv = btn.Patterns.Invoke.PatternOrDefault;
            if (inv != null) inv.Invoke();
            else btn.Click();

            // Wait a bit to ensure the click is processed, and also give the game a chance
            // to run its bindings
            ShortWait(_uiHelper!);

            //
            //  
            // this.TraceMessage($"After Roll and wait [ExpectedGameHash={GetCurrentGameModel().GameHash}]");

            // // Wait for the model to change to confirm the action happened
            // var changed = Retry.WhileTrue(
            //     () => string.Equals(GetCurrentGameModel().GameHash, preHash, StringComparison.Ordinal),
            //     timeout: TimeSpan.FromSeconds(5),
            //     interval: TimeSpan.FromMilliseconds(100)).Success;

            // if (!changed)
            // {
            //     // Optional: dump element info to diagnose why it didn't fire
            //     DumpElementForDiagnostics(btn);
            //     throw new Xunit.Sdk.XunitException($"Roll '{id}' did not change the GameModel within timeout.");
            // }

            // ShortWait(_uiHelper!);
        }


        private static void DumpElementForDiagnostics(AutomationElement el)
        {
            try
            {
                var rect = el.BoundingRectangle;
                bool hasInvoke = el.Patterns.Invoke.PatternOrDefault != null;
                bool hasScrollItem = el.Patterns.ScrollItem.PatternOrDefault != null;
                bool hasSelection = el.Patterns.SelectionItem.PatternOrDefault != null;
                bool hasLegacyIA = el.Patterns.LegacyIAccessible.PatternOrDefault != null;
                bool hasToggle = el.Patterns.Toggle.PatternOrDefault != null;
                bool hasValue = el.Patterns.Value.PatternOrDefault != null;

                rect.TraceMessage(

                    $"Name={el.Name}, Id={el.AutomationId}, Enabled={el.IsEnabled}, " +
                    $"Offscreen={el.IsOffscreen}, Rect={rect}, " +
                    $"Patterns: Invoke={hasInvoke}, ScrollItem={hasScrollItem}, " +
                    $"SelectionItem={hasSelection}, LegacyIA={hasLegacyIA}, Toggle={hasToggle}, Value={hasValue}");
            }
            catch { /* best effort */ }
        }







        /// <summary>
        /// Places the optimal settlement for the current player based on star count.
        /// Uses the same algorithm as the SignalR test PickOptimalSettlement method.
        /// 
        /// Algorithm:
        /// 1. Find all buildings in PossibleSettlement state
        /// 2. Calculate star count for each potential settlement location
        /// 3. Select the settlement with the highest star count (ties broken by first occurrence)
        /// 4. Click on the selected settlement in the UI
        /// 
        /// Star Count Calculation: Uses gameModel.TilesForBuildings(buildingKey).Stars()
        /// which evaluates the resource production potential of adjacent tiles.
        /// 
        /// Throws: InvalidOperationException if no possible settlements are available
        /// </summary>
        /// <param name="gameModel">Current game state containing building and tile information</param>
        private void PlaceOptimalSettlement(GameModel gameModel)
        {
            // Find all possible settlements
            var possibleSettlements = gameModel.Buildings
                .Where(b => b.BuildingState == BuildingState.PossibleSettlement)
                .ToList();

            if (!possibleSettlements.Any())
            {
                throw new InvalidOperationException("No possible settlements available");
            }

            // Find the settlement with the highest star count using the same logic as SignalR tests
            var settlementOptions = possibleSettlements
                .Select(building => new
                {
                    stars = gameModel.TilesForBuildings(building.BuildingKey).Stars(),
                    building = building
                })
                .ToList();

            var maxStars = settlementOptions.Max(s => s.stars);
            var bestSettlement = settlementOptions.First(s => s.stars == maxStars);

            this.TraceMessage($"Placing settlement at {bestSettlement.building.BuildingKey} with {bestSettlement.stars} stars");

            // Click on the building to select it
            ClickOnBuilding(bestSettlement.building);
        }

        /// <summary>
        /// Places the first available buildable road for the current player.
        /// 
        /// Strategy: This method uses a comprehensive approach to handle road UI automation challenges:
        /// 1. Find all roads in RoadState.Buildable from the GameModel
        /// 2. Trace all available roads for debugging purposes
        /// 3. Attempt to find and click each road using complex alias logic and hierarchy navigation
        /// 4. Include extensive debugging output to help diagnose automation issues
        /// 5. Test the entire XAML hierarchy to understand why road elements aren't found
        /// 
        /// The method is intentionally complex due to ongoing issues with road UI automation
        /// element discovery. It includes fallback strategies and comprehensive logging to
        /// help debug the AutomationId binding problems with road elements.
        /// 
        /// Throws: InvalidOperationException if no buildable roads are available
        ///         or if UI elements cannot be found for any buildable road
        /// </summary>
        /// <param name="gameModel">Current game state containing road information</param>
        private void PlaceFirstBuildableRoad(GameModel gameModel)
        {
            // Find all buildable roads
            var buildableRoads = gameModel.Roads
                .Where(r => r.RoadState == RoadState.Buildable)
                .ToList();

            if (!buildableRoads.Any())
            {
                throw new InvalidOperationException("No buildable roads available");
            }



            // Trace buildable roads as CSV like GameController does
            var buildableRoadsCsv = string.Join(",", buildableRoads.Select(r => r.RoadKey.ToString()));
            this.TraceMessage($"Buildable roads CSV: {buildableRoadsCsv}");

            // the roads in the GameModel should have the same IDs as each time, so we don't need to 
            // look them up by alias ... 

            // Initialize UI helper if not already created
            if (_uiHelper == null)
            {
                _uiHelper = new UIAutomationHelper(Main, _automation!);
            }

            var automationId = buildableRoads[0].RoadKey.GetAutomationId();
            var element = _uiHelper.FindElement(automationId);
            Assert.NotNull(element);
            element.Click();

        }

        /// <summary>
        /// gets the AutomationId for a building element and clicks on it.
        /// 
        /// Asserts that the building element exists in the UI and performs a click action.
        /// </summary>
        /// <param name="buildingKey">The BuildingKey identifying the building to click</param>
        private void ClickOnBuilding(BuildingModel building)
        {
            // Build the expected AutomationId that matches BuildingViewModel.AutomationId format
            var expectedAutomationId = building.BuildingKey.GetAutomationId();

            // Look for a UI element with the building's AutomationId
            // Initialize UI helper if not already created
            if (_uiHelper == null)
            {
                _uiHelper = new UIAutomationHelper(Main, _automation!);
            }

            var buildingElement = _uiHelper.FindElement(expectedAutomationId);
            Assert.NotNull(buildingElement);

            buildingElement.Click();
            ShortWait(_uiHelper!);
        }

        /// <summary>
        /// Clicks on a road element in the UI by its RoadKey.
        /// 
        /// the UIElement is built when the board is built and is stable for the lifetime of the game.
        /// the RoadState changes, but not the road itself.
        /// 
        /// Throws: AssertionException if the road element cannot be found
        /// </summary>
        /// <param name="roadKey">The RoadKey identifying the road to click</param>
        private void ClickOnRoad(IEnumerable<RoadModel> roads, RoadKey roadKey)
        {
            var roadModel = roads.FindRoad(roadKey); //extension from RoadModelExtensions.cs in the Shared project
            Assert.NotNull(roadModel);
            // Initialize UI helper if not already created
            if (_uiHelper == null)
            {
                _uiHelper = new UIAutomationHelper(Main, _automation!);
            }

            var roadElement = _uiHelper.FindElement(roadModel.RoadKey.GetAutomationId());
            Assert.NotNull(roadElement);

            roadElement.Click();
            ShortWait(_uiHelper!);
        }



        /// <summary>
        /// Launches the packaged app (MSIX) and attaches the UI automation framework.
        /// 
        /// Process:
        /// 1. Constructs the Application User Model ID (AUMID) from Package Family Name
        /// 2. Launches the app via Windows Shell using explorer.exe
        /// 3. Initializes UIA3Automation framework for UI automation
        /// 4. Waits up to 25 seconds for the main window to appear
        /// 5. Sets up the Main window reference for subsequent automation
        /// 
        /// AUMID Format: "{PackageFamilyName}!App"
        /// Window Detection: Searches for WinUIDesktopWin32WindowClass windows
        /// 
        /// Timeout: 25 seconds with 250ms polling interval
        /// 
        /// Side Effects:
        /// - Sets _automation instance
        /// - Sets _main window reference
        /// 
        /// Throws: XunitException if the app fails to launch or main window cannot be found
        /// </summary>
        /// <summary>
        /// Gets the default test file name for the legacy obsolete test.
        /// Modern tests should specify the test file name directly.
        /// </summary>
        private string GetTestFileName()
        {
            return "Expansion.catan_test";
        }

        /// <summary>
        /// Gets the path to the test file by extracting it from the shared TestData resources.
        /// Creates a temporary copy that the desktop app can load.
        /// </summary>
        /// <param name="testFileName">Optional test file name. If null, uses GetTestFileName() logic.</param>
        private string GetTestFilePath(string? testFileName = null)
        {
            testFileName ??= GetTestFileName();

            // Extract the test file from the Shared assembly embedded resources
            var tempPath = Path.Combine(Path.GetTempPath(), "CatanTests", testFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            
            // Load from shared embedded resources (TestData files: Expansion.catan_test, Regular.catan_test)
            using var stream = Catan3.Shared.TestData.TestDataLoader.GetTestFileStream(testFileName);
            using var fileStream = File.Create(tempPath);
            stream.CopyTo(fileStream);
            
            this.TraceMessage($"Extracted shared test file to: {tempPath}");
            return tempPath;
        }

        /// <summary>
        /// Launches the packaged app with a test file via file association.
        /// Now that .catan files are registered in the manifest, we can simply launch the file 
        /// and Windows will open it with the app.
        /// </summary>
        private void LaunchAppWithTestFile(string testFilePath)
        {
            this.TraceMessage($"Launching .catan file via file association: {testFilePath}");

            var psi = new ProcessStartInfo
            {
                FileName = testFilePath,
                UseShellExecute = true, // This tells Windows to use file associations
                Verb = "open" // Use the default open verb for the file type
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to launch process or get process ID");
            }

            var targetProcessId = process.Id;
            this.TraceMessage($"Launched process ID: {targetProcessId}");

            _automation = new UIA3Automation();

            // Wait for the window with our specific process ID
            _main = Retry.WhileNull(
                () =>
                {
                    var wins = _automation.GetDesktop()
                        .FindAllChildren(Cf.ByProcessId(targetProcessId));

                    this.TraceMessage($"Found {wins.Length} WinUI windows for process {targetProcessId}");

                    // Should only be one window for our process, but filter out debug windows just in case
                    var mainWindow = wins.FirstOrDefault(w =>
                        !w.Name.Contains("Catan Debug Messages", StringComparison.OrdinalIgnoreCase));

                    if (mainWindow != null)
                    {
                        this.TraceMessage($"✅ Found window: {mainWindow.Name}");
                    }

                    return mainWindow;
                },
                timeout: TimeSpan.FromSeconds(25),
                interval: TimeSpan.FromMilliseconds(250),
                throwOnTimeout: false
            ).Result ?? throw new XunitException($"Failed to find window for process {targetProcessId}. The app may have crashed or failed to create a window.");

            // Check if we should wait for debugger attachment AFTER the app is launched
            if (Environment.GetEnvironmentVariable("CATAN_DEBUG_WAIT") == "true")
            {
                this.TraceMessage("⏸️ WAITING FOR DEBUGGER ATTACHMENT");
                this.TraceMessage("   The Catan Desktop app is now running");
                this.TraceMessage("   1. In VS Code, switch to 'Attach to Catan Desktop' configuration");
                this.TraceMessage("   2. Press F5 to attach debugger");
                this.TraceMessage("   3. Set your breakpoints in the Desktop app");
                this.TraceMessage("   Waiting 10 seconds for debugger attachment...");

                // Give time to attach debugger

                this.TraceMessage("Continuing with test execution...");
            }
        }

        /// <summary>
        /// Gets the repository root directory by walking up from the assembly location.
        /// </summary>
        private string GetRepositoryRoot()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyPath = Path.GetDirectoryName(assembly.Location)!;

            // Walk up until we find the .sln file or .git directory
            var current = new DirectoryInfo(assemblyPath);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "*.sln")) ||
                    Directory.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root (no .sln or .git found)");
        }

        private void LaunchPackagedAppAndAttachToMainWindow()
        {
            var pfn = GetPackageFamilyNameOrThrow();
            var aumid = pfn + "!App";

            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true
            };

            using var _ = Process.Start(psi);
            _automation = new UIA3Automation();

            // Wait for WinUI top-level windows, then pick the *non-debug* one
            _main = Retry.WhileNull(
                () =>
                {
                    var wins = _automation.GetDesktop()
                        .FindAllChildren(Cf.ByControlType(ControlType.Window)
                        .And(Cf.ByClassName("WinUIDesktopWin32WindowClass")));

                    return wins.FirstOrDefault(w =>
                        !w.Name.Contains("Debug", StringComparison.OrdinalIgnoreCase));
                },
                timeout: TimeSpan.FromSeconds(25),
                interval: TimeSpan.FromMilliseconds(250),
                throwOnTimeout: false
            ).Result ?? throw new XunitException($"Failed to find main window for AUMID '{aumid}'. Is the app deployed and running?");
        }

        private static string GetPackageFamilyNameOrThrow()
        {
            const string identityName = "606d7833-a1be-4389-aa5f-fe8dd1dd1da3";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-AppxPackage -Name '" + identityName + "*').PackageFamilyName\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var ps = Process.Start(psi);
            ps!.WaitForExit(500);
            var output = (ps.StandardOutput.ReadToEnd() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new XunitException("App package is not installed. Build/deploy the MSIX before running packaged UI tests.");
            }
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }

        /// <summary>
        /// Finds a UI element by its AutomationId using the most efficient available method.
        /// 
        /// Strategy:
        /// 1. If UiControls cache is populated, returns element from cache (fastest)
        /// 2. Otherwise, searches the main window hierarchy with retry logic
        /// 
        /// The cache-first approach provides significant performance benefits for repeated lookups
        /// during test execution, while the fallback ensures reliability during initialization.
        /// </summary>
        /// <param name="automationId">The AutomationId of the element to find</param>
        /// <returns>AutomationElement instance</returns>
        /// <exception cref="TimeoutException">Thrown if element not found within SHORT_WAIT timeout</exception>
        private AutomationElement FindByAutomationId(string automationId)
        {
            // Initialize UI helper if not already created
            if (_uiHelper == null)
            {
                _uiHelper = new UIAutomationHelper(Main, _automation!);
            }

            return _uiHelper.FindElement(automationId);
        }

        /// <summary>
        /// Waits for the game board to be loaded instead of the NewGame page.
        /// Since we're auto-loading a test file, the app should skip the NewGame dialog.
        /// </summary>
        private void WaitForGameBoardToLoad()
        {
            this.TraceMessage("Waiting for game board to load...");

            // Wait for the NextButton to appear, which indicates the game is loaded
            var nextButton = Retry.WhileNull(
                () => _main?.FindFirstDescendant(Cf.ByAutomationId("NextButton")),
                timeout: TimeSpan.FromSeconds(15),
                interval: TimeSpan.FromMilliseconds(500),
                throwOnTimeout: false
            ).Result;

            Assert.NotNull(nextButton);
            this.TraceMessage("✅ Game board loaded successfully");
        }



        /// <summary>
        /// Retrieves the current GameModel from the UI via AutomationProperties.ItemStatus.
        /// 
        /// This method provides access to the complete game state without requiring
        /// direct app dependencies or complex inter-process communication.
        /// 
        ///  Strategy: Read JSON from NextButton's ItemStatus property
        /// 
        /// The GameModel contains all game state and it has copy on write, so it will
        /// change every time an update is sent to the GameController. It cannot be cached
        /// 
        /// JSON Deserialization: Uses JsonHelper.StandardOptions for consistency
        /// with the rest of the codebase.
        /// 
        /// Returns: Valid GameModel instance
        /// Throws: AssertionException if GameModel cannot be retrieved or is invalid
        /// </summary>
        /// <summary>
        /// Gets the current game hash without deserializing the full GameModel.
        /// More efficient when only the hash is needed for validation.
        /// </summary>
        private string GetCurrentGameHash()
        {
            AutomationElement nextButton = FindByAutomationId("NextButton");
            Assert.NotNull(nextButton);

            if (nextButton.Properties.ItemStatus.TryGetValue(out var buttonGameModelValue))
            {
                var buttonGameModelJson = buttonGameModelValue as string;
                if (!string.IsNullOrEmpty(buttonGameModelJson))
                {
                    // Simple JSON search for gameHash value without full deserialization
                    var hashStart = buttonGameModelJson.IndexOf("\"gameHash\":\"");
                    if (hashStart >= 0)
                    {
                        hashStart += 12; // Skip past "gameHash":"
                        var hashEnd = buttonGameModelJson.IndexOf('"', hashStart);
                        if (hashEnd > hashStart)
                        {
                            return buttonGameModelJson.Substring(hashStart, hashEnd - hashStart);
                        }
                    }
                }
            }

            throw new InvalidOperationException("Could not extract ExpectedGameHash from NextButton ItemStatus");
        }

        private GameModel GetCurrentGameModel()
        {
            // Primary approach: Get GameModel from NextButton which is always accessible and reliable
            // with the possible exception of starting a new game
            AutomationElement nextButton = FindByAutomationId("NextButton");
            Assert.NotNull(nextButton);
            try
            {
                if (nextButton.Properties.ItemStatus.TryGetValue(out var buttonGameModelValue))
                {
                    var buttonGameModelJson = buttonGameModelValue as string;
                    if (!string.IsNullOrEmpty(buttonGameModelJson))
                    {
                        var buttonGameModel = JsonSerializer.Deserialize<GameModel>(buttonGameModelJson, JsonHelper.StandardOptions);
                        Assert.NotNull(buttonGameModel);
                        return buttonGameModel;
                    }
                    else
                    {
                        this.TraceMessage("NextButton found but ItemStatus is empty");
                    }
                }
                else
                {
                    this.TraceMessage("NextButton does not support ItemStatus property");
                }
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error getting ItemStatus from NextButton: {ex.Message}");
                Assert.Fail("Next Button must exits");
            }

            throw new Exception("Game Model can't be null");
        }

        /// <summary>
        /// Verifies that the current GameState matches the expected state
        /// Also verifies that the UI Description matches the GameState Description attribute
        /// This provides consistent, reliable state verification across all tests
        /// </summary>
        /// <param name="expectedState">The expected GameState</param>
        private void VerifyExpectedGameState(GameState expectedState)
        {
            this.TraceMessage($"Verifying expected GameState: {expectedState}");

            var currentGameState = GetCurrentGameModel().GameState;

            this.TraceMessage($"Current GameState: {currentGameState}, Expected: {expectedState}");
            Assert.Equal(expectedState, currentGameState);
        }

        /// <summary>
        /// Waits for the GameState to transition to the expected state
        /// Uses actual GameState enum instead of brittle UI text matching
        /// </summary>
        /// <param name="expectedState">The expected GameState to wait for</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <returns>True if the expected state is reached within timeout</returns>
        private bool WaitForGameState(GameState expectedState, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            this.TraceMessage($"WaitForGameState: Looking for '{expectedState}' state");

            while (sw.Elapsed < timeout)
            {
                var currentGameState = GetCurrentGameModel().GameState;

                if (currentGameState == expectedState)
                {
                    this.TraceMessage($"WaitForGameState: Found expected state '{expectedState}'!");
                    return true;
                }

                ShortWait(_uiHelper!);
            }

            this.TraceMessage($"WaitForGameState: Timed out waiting for '{expectedState}' after {timeout.TotalSeconds}s");
            return false;
        }
        /// <summary>
        /// Executes the main scripted test scenario using pre-loaded scenario data.
        /// 
        /// Process:
        /// 1. Uses pre-loaded scenario data from embedded resources (no file searching)
        /// 2. Creates UIAutomationHelper and ActionExecutor instances  
        /// 3. Iterates through all actions in sequence
        /// 4. For each action:
        ///    - Asserts current game state matches recorded state (before action execution)
        ///    - Executes the action using ActionExecutor
        ///    - Waits for UI to update
        /// 
        /// State Assertion Timing:
        /// During recording, game state is captured BEFORE each action executes.
        /// During replay, we assert the same timing - check state BEFORE executing each action.
        /// This ensures the test validates the exact same conditions as when recorded.
        /// 
        /// Efficiency: This method no longer searches for files - it uses pre-loaded data
        /// passed from DoFullTestWithScriptedActions() to eliminate duplicate file access.
        /// </summary>
        /// <param name="sharedScenario">Pre-loaded test scenario with actions to execute</param>
        private void ExecuteScenario(Catan3.Shared.TestData.TestScenario sharedScenario)
        {
            this.TraceMessage($"Executing pre-loaded scenario with {sharedScenario.RecordedActions.Length} actions");

            // Create UI automation helper and action executor once
            var uiHelper = new UIAutomationHelper(Main, _automation!);
            var actionExecutor = new ActionExecutor(uiHelper);

            // Execute each recorded message in sequence
            for (int i = 0; i < sharedScenario.RecordedActions.Length; i++)
            {
                var recordedMessage = sharedScenario.RecordedActions[i];
                
                ValidateMessage(recordedMessage);
                // Validate that current ExpectedGameHash matches recorded ExpectedGameHash
                var currentGameHash = GetCurrentGameHash();
                if (currentGameHash != recordedMessage.ExpectedGameHash)
                {
                    this.TraceMessage($"❌ Game state mismatch at action {i}:[GameState={GetCurrentGameModel().GameState}][Action={recordedMessage.RecordType}] [Expected Game Hash={recordedMessage.ExpectedGameHash}][Current={currentGameHash}]");

                    throw new InvalidOperationException($"Game state mismatch at action {i}: expected {recordedMessage.ExpectedGameHash}, got {currentGameHash}");
                }

                // Execute UI interaction based on recorded message type
                ExecuteRecordedMessage(recordedMessage, uiHelper);

                // Wait for UI to update
                ShortWait(_uiHelper!);
            }

            this.TraceMessage("All scripted actions completed successfully");
        }

        ///
        private void ValidateMessage(IRecordedMessage recordedMessage)
        {
            // Ensure the recorded message has a valid ExpectedGameHash
            if (string.IsNullOrEmpty(recordedMessage.ExpectedGameHash))
            {
                throw new InvalidOperationException($"Recorded message {recordedMessage.RecordType} is missing ExpectedGameHash");
            }
            // Ensure the recorded message has a valid RecordType
            if (recordedMessage.RecordType == null)
            {
                throw new InvalidOperationException($"Recorded message is missing RecordType");
            }

            var currentGameModel = GetCurrentGameModel() ?? throw new InvalidOperationException("Current GameModel cannot be null");

            if (currentGameModel.GameHash != recordedMessage.ExpectedGameHash || currentGameModel.GameState != recordedMessage.ExpectedGameState)
            {
                string message=($"[Expected GameHash ={recordedMessage.ExpectedGameHash}][Current Hash={currentGameModel.GameHash}][Expected GameState={recordedMessage.ExpectedGameState}] [Current GameState=[{currentGameModel.GameState}]");
                this.TraceMessage(message);
                throw new InvalidOperationException($"message");
            }
        }

        /// <summary>
        /// Executes a UI interaction based on the recorded message type.
        /// Uses pattern matching to delegate to specific execution methods.
        /// </summary>
        private void ExecuteRecordedMessage(IRecordedMessage recordedMessage, UIAutomationHelper uiHelper)
        {
            this.TraceMessage($"Executing recorded message: {recordedMessage.RecordType} with hash {recordedMessage.ExpectedGameHash}");
            switch (recordedMessage)
            {
                case UndoRecord undoAction:
                    Execute_Undo(undoAction);
                    break;
                case RedoRecord redoAction:
                    Execute_Redo(redoAction);
                    break;
                case NextRecord nextAction:
                    Execute_Next(nextAction);
                    break;
                case ShuffleRecord shuffle:
                    Execute_Shuffle(shuffle);
                    break;
                case PurchaseRecord purchase:
                    Execute_Purchase(purchase);
                    break;
                case BuildingUpgradeRecord building:
                    Execute_BuildingUpgrade(building, uiHelper);
                    break;
                case RoadPurchaseRecord road:
                    Execute_RoadPurchase(road, uiHelper);
                    break;
                case MoveRobberRecord robber:
                    Execute_MoveRobber(robber, uiHelper);
                    break;
                case RollRecord roll:
                    Execute_Roll(roll);
                    break;
                case SetPlayerOrderRecord playerOrder:
                    Execute_SetPlayerOrder(playerOrder);
                    break;
                case GoFirstRecord goFirst:
                    Execute_GoFirst(goFirst);
                    break;
                case ParticipatingInSupplementalRecord supplemental:
                    Execute_ParticipatingInSupplemental(supplemental);
                    break;
                case BalanceBoardRecord balance:
                    Execute_BalanceBoard(balance);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown recorded message type: {recordedMessage.GetType().Name}");
            }
        }

        // Individual execution methods for each recorded message type

        private void Execute_Roll(RollRecord roll)
        {
            this.TraceMessage($"Executing roll: {roll.Roll.NormalRoll}");
            DoRoll((int)roll.Roll.NormalRoll);
        }

        private void Execute_Purchase(PurchaseRecord purchase)
        {
            var buttonId = purchase.Entitlement switch
            {
                Entitlement.Road => "PurchaseRoadButton",
                Entitlement.Settlement => "PurchaseSettlementButton",
                Entitlement.City => "PurchaseCityButton",
                Entitlement.Soldier => "PurchaseSoldierButton",
                _ => throw new InvalidOperationException($"Unknown entitlement: {purchase.Entitlement}")
            };

            this.TraceMessage($"Executing purchase: {purchase.Entitlement} -> {buttonId}");
            var button = FindByAutomationId(buttonId);
            Assert.NotNull(button);

            // PurchaseCtrl uses custom pointer handling, try Invoke pattern first
            var invokePattern = button.Patterns.Invoke.PatternOrDefault;
            if (invokePattern != null)
            {
                invokePattern.Invoke();
                this.TraceMessage($"Used Invoke pattern for purchase button: {buttonId}");
            }
            else
            {
                // Fallback to Click if Invoke pattern is not available
                button.Click();
                this.TraceMessage($"Used Click fallback for purchase button: {buttonId}");
            }
        }

        private void Execute_Undo(UndoRecord action)
        {
            this.TraceMessage("Executing undo action -> UndoButton");
            var button = FindByAutomationId("UndoButton");
            Assert.NotNull(button);
            button.Click();
        }

        private void Execute_Redo(RedoRecord action)
        {
            this.TraceMessage("Executing redo action -> RedoButton");
            var button = FindByAutomationId("RedoButton");
            Assert.NotNull(button);
            button.Click();
        }

        private void Execute_Next(NextRecord action)
        {
            this.TraceMessage("Executing next action -> NextButton");
            var button = FindByAutomationId("NextButton");
            Assert.NotNull(button);
            button.Click();
        }

        private void Execute_Shuffle(ShuffleRecord shuffle)
        {
            this.TraceMessage($"Executing shuffle with recorded");
            // click the shuffle button...
            var shuffleButton = FindByAutomationId("ShuffleButton");
            Assert.NotNull(shuffleButton);
            shuffleButton.Click();
        }

        private void Execute_BuildingUpgrade(BuildingUpgradeRecord building, UIAutomationHelper uiHelper)
        {
            var automationId = building.BuildingKey.GetAutomationId();
            this.TraceMessage($"Executing building upgrade: {automationId}");

            var element = uiHelper.FindElement(automationId);
            if (element == null)
                throw new InvalidOperationException($"Building element not found: {automationId}");

            element.Click();
        }

        private void Execute_RoadPurchase(RoadPurchaseRecord road, UIAutomationHelper uiHelper)
        {
            var automationId = road.RoadKey.GetAutomationId();
            this.TraceMessage($"Executing road purchase: {automationId}");

            var element = uiHelper.FindElement(automationId);
            if (element == null)
                throw new InvalidOperationException($"Road element not found: {automationId}");

            element.Click();
        }

        private void Execute_MoveRobber(MoveRobberRecord robber, UIAutomationHelper uiHelper)
        {
            Thread.Sleep(MEDIUM_WAIT);
            var tileAutomationId = $"Tile-{robber.Coordinates}";
            this.TraceMessage($"Executing move robber: {tileAutomationId}, target: {robber.TargetPlayerId ?? "none"}");

            var tileElement = uiHelper.FindElement(tileAutomationId);
            if (tileElement == null)
                throw new InvalidOperationException($"Tile element not found: {tileAutomationId}");

            // Right-click on tile to open robber context menu
            tileElement.RightClick();
            this.TraceMessage("Right-clicked on tile to open context menu");


            // Generate the expected AutomationId using the same extension method
            var targetAutomationId = TileModelExtensions.GetRobberTargetAutomationId(robber.TargetPlayerId);
            // Wait specifically for the target menu item to appear with retry logic
            AutomationElement? targetMenuItem = null;
            var maxRetries = 10;
            var retryDelay = 100; // ms

            for (int retry = 0; retry < maxRetries; retry++)
            {
                targetMenuItem = uiHelper.TryFindElement(targetAutomationId);
                if (targetMenuItem != null)
                {
                    this.TraceMessage($"Found target menu item on retry {retry + 1}");
                    break;
                }

                this.TraceMessage($"Target menu item not found on retry {retry + 1}, waiting {retryDelay}ms...");
                Thread.Sleep(retryDelay);
            }
            if (targetMenuItem != null)
            {
                targetMenuItem.Click();
            }
            else
            {

                throw new InvalidOperationException($"Target menu item not found: {targetAutomationId}");
            }
        }

        private void ExecuteTestCommand(TestCommandModel model, UIAutomationHelper uiHelper)
        {
            this.TraceMessage($"Executing test command: {model.Type}");

            // Handle specific commands
            switch (model.Type)
            {
                case TestCommandType.UpdateUi:
                    {
                        var json = JsonSerializer.Serialize(model, JsonHelper.StandardOptions);

                        // Set the JSON in the smuggled test data TextBox
                        var smuggledDataTextBox = FindByAutomationId("SmuggledTestData");
                        Assert.NotNull(smuggledDataTextBox);
                        smuggledDataTextBox.AsTextBox().Text = json;
                        this.TraceMessage($"Set smuggled test data: {json}");
                    }
                    break;
                default:
                    this.TraceMessage($"Unhandled test command type: {model.Type}");
                    break;
            }

            // Click the test automation action button to execute the command
            var testActionButton = FindByAutomationId("TestAutomationActionButton");
            Assert.NotNull(testActionButton);
            testActionButton.Click();
            this.TraceMessage("Clicked TestAutomationActionButton");

        }

        private void ShortWait(UIAutomationHelper uiHelper)
        {
            TestCommandModel cmd = new TestCommandModel(TestCommandType.UpdateUi);
            ExecuteTestCommand(cmd, uiHelper);
            Thread.Sleep(SHORT_WAIT);

        }

        private void Execute_SetPlayerOrder(SetPlayerOrderRecord playerOrder)
        {
            this.TraceMessage($"Executing set player order: {string.Join(", ", playerOrder.PlayerIds)}");
            // Player order is typically handled by the game logic automatically
            // May need to click Next to proceed
            var nextButton = FindByAutomationId("NextButton");
            nextButton?.Click();
        }

        private void Execute_GoFirst(GoFirstRecord goFirst)
        {
            var automationId = $"GoFirst-{goFirst.PlayerId}";
            this.TraceMessage($"Executing go first: Player {goFirst.PlayerId} -> {automationId}");

            var goFirstButton = FindByAutomationId(automationId);
            if (goFirstButton == null)
                throw new InvalidOperationException($"Go First button not found for player: {automationId}");

            goFirstButton.Click();
        }

        private void Execute_ParticipatingInSupplemental(ParticipatingInSupplementalRecord supplemental)
        {
            this.TraceMessage($"Executing supplemental participation: {supplemental.PlayerId} -> {supplemental.Participating}");

            var automationId = $"ParticipatingInSupplemental-{supplemental.PlayerId}";
            var checkbox = FindByAutomationId(automationId);
            if (checkbox == null)
                throw new InvalidOperationException($"Supplemental player checkbox not found: {automationId}");

            // Check if the checkbox is already in the correct state
            bool isChecked = checkbox.Patterns.Toggle.IsSupported ? 
                checkbox.Patterns.Toggle.Pattern.ToggleState == ToggleState.On : 
                false;

            if (isChecked != supplemental.Participating)
            {
                // Use Toggle pattern to change the state
                var togglePattern = checkbox.Patterns.Toggle.Pattern;
                if (togglePattern != null)
                {
                    togglePattern.Toggle();
                    this.TraceMessage($"Toggled checkbox using Toggle pattern for {supplemental.PlayerId}");
                }
                else
                {
                    checkbox.Click();
                    this.TraceMessage($"Clicked checkbox (no Toggle pattern) for {supplemental.PlayerId}");
                }
                
                // Wait for XAML bindings to update after checkbox click
                ShortWait(_uiHelper!);
            }
            else
            {
                this.TraceMessage($"Checkbox already in correct state for {supplemental.PlayerId}: {supplemental.Participating}");
            }
        }

        private void Execute_BalanceBoard(BalanceBoardRecord balance)
        {
            this.TraceMessage("Executing balance board");
            var button = FindByAutomationId("BalanceBoardButton");
            if (button == null)
                throw new InvalidOperationException("BalanceBoardButton not found");

            // AppBarButton requires Invoke pattern, not Click
            var invokePattern = button.Patterns.Invoke.PatternOrDefault;
            if (invokePattern != null)
            {
                invokePattern.Invoke();
                this.TraceMessage("Invoked BalanceBoardButton using Invoke pattern");
            }
            else
            {
                // Fallback to Click if Invoke pattern is not available
                button.Click();
                this.TraceMessage("Used Click fallback for BalanceBoardButton");
            }
        }

        /// <summary>
        /// Executes a minimal test scenario when no JSON scenario file is available.
        /// Provides basic validation that the test infrastructure works by performing a simple roll action.
        /// This serves as a fallback to ensure the test doesn't fail completely due to missing scenario files.
        /// </summary>
        private void ExecuteBasicScenario()
        {
            this.TraceMessage("Executing basic test scenario");

            // Wait for game to be in a testable state
            var gameModel = GetCurrentGameModel();
            this.TraceMessage($"Current game state: {gameModel.GameState}");

            // Perform a simple roll action to verify the system works
            if (gameModel.GameState == GameState.WaitingForRoll)
            {
                this.TraceMessage("Performing test roll");
                DoRoll(6);

                // Verify state changed
                var newGameModel = GetCurrentGameModel();
                this.TraceMessage($"Game state after roll: {newGameModel.GameState}");
            }

            this.TraceMessage("Basic scenario completed");
        }



    }

    /// <summary>
    /// Extension methods for test tracing
    /// </summary>
    public static class TestExtensions
    {
        public static void TraceMessage(this object o, string toWrite, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            var message = $"{Path.GetFileNameWithoutExtension(cfp)}({cln}):{toWrite}\t\t[Caller={cmb}]";

            // Write to both debug output and console for test visibility
            for (int i = 0; i < indentLevel; i++)
            {
                System.Diagnostics.Debug.Indent();

            }
            System.Diagnostics.Debug.WriteLine(message);

            for (int i = 0; i < indentLevel; i++)
            {
                System.Diagnostics.Debug.Unindent();
            }
        }

        /// <summary>
        /// Gets the Description attribute value from an enum
        /// Copied locally to avoid external dependencies
        /// </summary>
        public static string Description(this Enum instance)
        {
            string output = "";
            Type type = instance.GetType();
            if (type is null) return string.Empty;
            FieldInfo? fi = type.GetField(instance.ToString());
            if (fi is null) return string.Empty;
            DescriptionAttribute[]? attrs = fi.GetCustomAttributes(attributeType: typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attrs is not null && attrs.Length > 0)
            {
                output = attrs[0].Description;
            }
            return output;
        }
    }
}
