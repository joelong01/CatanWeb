/*
 * GameMessageServiceProxy.cs
 * 
 * OVERVIEW:
 * This partial class contains all the service handler methods for GameMessageService.
 * When ServiceGame setting is enabled, these handlers delegate all game operations
 * to the remote GameService via GameServiceProxy instead of using local GameStateMachine.
 * 
 * PURPOSE AND ARCHITECTURE:
 * - Service Delegation: Each handler method receives MVVM messages and forwards them
 *   to the GameServiceProxy, which communicates with the remote GameService.
 * - Event Translation: Subscribes to GameServiceProxy events and converts them back
 *   to MVVM UpdateGameModel messages that the UI expects.
 * - Identical Interface: Maintains the same method signatures and behavior as local
 *   handlers, ensuring the UI experience is identical regardless of execution mode.
 * 
 * KEY RESPONSIBILITIES:
 * 1. GameServiceProxy Management: Initialize and manage the proxy connection
 * 2. Message Translation: Convert MVVM messages to GameServiceProxy calls
 * 3. Event Handling: Convert GameServiceProxy events back to MVVM messages
 * 4. Error Handling: Catch and convert service errors to UI error messages
 * 
 * MESSAGE FLOW:
 * UI Action → MVVM Message → Service Handler → GameServiceProxy → GameService →
 * GameModel Response → GameServiceProxy Event → UpdateGameModel Message → UI Update
 */

using Catan3.Shared.Models;
using Catan3.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Catan3.GameState
{
    /// <summary>
    /// Partial class containing GameService proxy handlers for GameMessageService.
    /// These methods delegate to GameServiceProxy when ServiceGame setting is enabled.
    /// </summary>
    internal partial class GameMessageService
    {
        /// <summary>
        /// Initializes the GameServiceProxy when service handlers are being used.
        /// Creates the proxy connection and wires up event handlers.
        /// </summary>
        /// <param name="baseUrl">The base URL of the GameService (e.g., "http://localhost:8080")</param>
        private async Task InitializeGameServiceProxyAsync(string baseUrl)
        {
            if (_gameServiceProxy == null)
            {
                try
                {
                    // Derive all URLs from the base URL
                    string hubUrl = $"{baseUrl.TrimEnd('/')}/gameHub";
                    
                    _gameServiceProxy = new Catan3.Shared.Services.GameServiceProxy(hubUrl, baseUrl, "desktop-player", null);
                    
                    _gameServiceProxy.GameStateUpdated += OnGameServiceStateUpdated;
                    _gameServiceProxy.CommandCompleted += OnGameServiceCommandCompleted;
                    _gameServiceProxy.CommandFailed += OnGameServiceCommandFailed;
                    
                    await _gameServiceProxy.ConnectAsync();
                    
                    this.TraceMessage($"✅ GameServiceProxy initialized and connected to {baseUrl}");
                }
                catch (Exception ex)
                {
                    SendErrorMessage($"Failed to initialize GameServiceProxy: {ex.Message}", ErrorLevel.Critical);
                }
            }
        }

        /// <summary>
        /// Disposes the GameServiceProxy and cleans up event subscriptions.
        /// </summary>
        private async Task DisposeGameServiceProxyAsync()
        {
            if (_gameServiceProxy != null)
            {
                try
                {
                    _gameServiceProxy.GameStateUpdated -= OnGameServiceStateUpdated;
                    _gameServiceProxy.CommandCompleted -= OnGameServiceCommandCompleted;
                    _gameServiceProxy.CommandFailed -= OnGameServiceCommandFailed;
                    
                    await _gameServiceProxy.DisposeAsync();
                    _gameServiceProxy = null;
                    
                    this.TraceMessage("✅ GameServiceProxy disposed");
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"⚠️ Error disposing GameServiceProxy: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles GameServiceProxy GameStateUpdated events and converts them to UpdateGameModel messages.
        /// Marshals to UI thread to avoid threading issues with WinUI controls.
        /// </summary>
        private void OnGameServiceStateUpdated(GameModel gameModel)
        {
            // Marshal to UI thread since SignalR events come from background threads
            // Use the MainWindow's dispatcher since we're application-wide now
            if (((App)Microsoft.UI.Xaml.Application.Current).MainWindow?.DispatcherQueue != null)
            {
                ((App)Microsoft.UI.Xaml.Application.Current).MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    Messenger.Send(new UpdateGameModel(gameModel));
                });
            }
            else
            {
                // Fallback - send directly if no dispatcher available (shouldn't happen in normal operation)
                this.TraceMessage("⚠️ No UI dispatcher available, sending message directly");
                Messenger.Send(new UpdateGameModel(gameModel));
            }
        }

        /// <summary>
        /// Handles GameServiceProxy CommandCompleted events.
        /// </summary>
        private void OnGameServiceCommandCompleted(string commandId, bool success, string message)
        {
            if (success)
            {
                this.TraceMessage($"✅ GameService command completed: {commandId}");
            }
            else
            {
                this.TraceMessage($"❌ GameService command failed: {commandId} - {message}");
                SendErrorMessage($"GameService command failed: {message}", ErrorLevel.Critical);
            }
        }

        /// <summary>
        /// Handles GameServiceProxy CommandFailed events.
        /// </summary>
        private void OnGameServiceCommandFailed(string commandId, string errorMessage)
        {
            this.TraceMessage($"❌ GameService command failed: {commandId} - {errorMessage}");
            SendErrorMessage($"GameService error: {errorMessage}", ErrorLevel.Critical);
        }

        // ======================================================================================
        // SERVICE HANDLER METHODS
        // ======================================================================================

        private async void HandleUndoServiceAsync(object recipient, UndoMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteUndoAsync();
                // Result comes via GameStateUpdated event -> OnGameServiceStateUpdated -> UpdateGameModel message
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to undo via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleRedoServiceAsync(object recipient, RedoMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteRedoAsync();
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to redo via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleNextServiceAsync(object recipient, NextMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteNextAsync();
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to execute next via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleShuffleServiceAsync(object recipient, ShuffleMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteShuffleAsync();
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to shuffle via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleBuildingUpgradeServiceAsync(object recipient, BuildingUpgradeMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteBuildingUpgradeAsync(_gameServiceProxy.GameId!, message.BuildingKey);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to upgrade building via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleSetPlayerOrderServiceAsync(object recipient, SetPlayerOrderMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteSetPlayerOrderAsync(_gameServiceProxy.GameId!, message.PlayerIds);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to set player order via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleRoadPurchaseServiceAsync(object recipient, RoadPurchaseMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteRoadPurchaseAsync(_gameServiceProxy.GameId!, message.RoadKey);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to purchase road via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleMoveRobberServiceAsync(object recipient, MoveRobberMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteMoveRobberAsync(_gameServiceProxy.GameId!, message.Coordinates, message.TargetPlayerId);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to move robber via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleNewGameServiceAsync(object recipient, NewGameMessage message)
        {
            if (_gameServiceProxy == null)
            {
                string gameServiceUrl = _currentSettings!.GetStringValue("GameServiceUrl");
                await InitializeGameServiceProxyAsync(gameServiceUrl);
            }

            if (_gameServiceProxy == null)
            {
                SendErrorMessage("GameServiceProxy not available", ErrorLevel.Critical);
                return;
            }

            try
            {
                var playerNames = message.PlayerIds.ToList();
                var gameInfo = await _gameServiceProxy.CreateGameAsync(message.GameType, false, playerNames);
                
                // Connect to the created game
                await _gameServiceProxy.JoinGameAsync(gameInfo.GameId);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to create new game via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleLoadLocalCatanGameServiceAsync(object recipient, Catan3.Models.LoadLocalCatanGameMessage message)
        {
            if (_gameServiceProxy == null)
            {
                string gameServiceUrl = _currentSettings!.GetStringValue("GameServiceUrl");
                await InitializeGameServiceProxyAsync(gameServiceUrl);
            }

            if (_gameServiceProxy == null)
            {
                SendErrorMessage("GameServiceProxy not available", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ConnectAsync();
                
                // Load the GameModel from the local file first
                var filePath = message.LocalFile;
                if (!System.IO.File.Exists(filePath))
                {
                    SendErrorMessage($"File not found: {filePath}", ErrorLevel.Critical);
                    return;
                }

                var extension = System.IO.Path.GetExtension(filePath);
                GameModel gameModel;

                switch (extension.ToLowerInvariant())
                {
                    case ".catan":
                        gameModel = await LoadCompressedGameAsync(filePath);
                        break;
                    case ".catan_test":
                        gameModel = await LoadTestScenarioAsync(filePath);
                        break;
                    default:
                        SendErrorMessage($"Unsupported file extension: {extension}. Only .catan and .catan_test files are supported.", ErrorLevel.Critical);
                        return;
                }
                
                // Now load the GameModel via the service
                var result = await _gameServiceProxy.LoadGameModelAsync(gameModel);
                if (!result.Success)
                {
                    SendErrorMessage($"Failed to load game: {result.Message}", ErrorLevel.Critical);
                }
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to load game via service: {ex.Message}", ErrorLevel.Critical);
            }
        }


        private async void HandleRollServiceAsync(object recipient, RollMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteRollAsync(message.Roll.RedRoll, message.Roll.WhiteRoll);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to roll via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandlePurchaseServiceAsync(object recipient, PurchaseMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecutePurchaseAsync(message.Entitlement);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to purchase via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleParticipatingInSupplementalServiceAsync(object recipient, ParticipatingInSupplementalMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteParticipatingInSupplementalAsync(message.PlayerId, message.Participating);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to handle supplemental participation via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleBalanceBoardServiceAsync(object recipient, BalanceBoardMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteBalanceBoardAsync(_gameServiceProxy.GameId!);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to balance board via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleEndGameServiceAsync(object recipient, EndGame message)
        {
            if (_gameServiceProxy == null)
            {
                return; // Already disposed
            }

            try
            {
                // End the game on the server first, but only if we have a valid GameId
                if (!string.IsNullOrEmpty(_gameServiceProxy.GameId))
                {
                    await _gameServiceProxy.EndGameAsync(_gameServiceProxy.GameId);
                }
                // GameId will be cleared when we join a new game
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to end game via service: {ex.Message}", ErrorLevel.Critical);
            }
            // Note: We don't dispose the GameServiceProxy here - keep connection alive for next game
        }

        private async void HandleGoFirstServiceAsync(object recipient, GoFirstMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.ExecuteGoFirstAsync(message.PlayerId);
                // Result comes via GameStateUpdated event
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to go first via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandlePersistGameServiceAsync(object recipient, PersistGameMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                await _gameServiceProxy.PersistGameAsync(_gameServiceProxy.GameId!, message.Action.ToString(), message.Location);
                // Result comes via persistence completion
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to persist game via service: {ex.Message}", ErrorLevel.Critical);
            }
        }

        private async void HandleSaveAsRequestServiceAsync(object recipient, SaveAsRequestMessage message)
        {
            if (_gameServiceProxy == null)
            {
                SendErrorMessage("No active GameService connection", ErrorLevel.Critical);
                return;
            }

            try
            {
                // SaveAs in service mode would require UI integration for file picker
                // For now, just perform a regular save
                await _gameServiceProxy.PersistGameAsync(_gameServiceProxy.GameId!, "Save", "");
                SendErrorMessage("SaveAs performed as Save in GameService mode", ErrorLevel.Information);
            }
            catch (Exception ex)
            {
                SendErrorMessage($"Failed to save-as via service: {ex.Message}", ErrorLevel.Critical);
            }
        }
    }
}