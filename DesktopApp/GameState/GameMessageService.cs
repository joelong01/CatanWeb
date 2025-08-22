using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Catan3.GameState
{
    /// <summary>
    ///     This class registers for all the MVVM messages necessary to drive the GameStateMachine
    /// </summary>
    internal class GameMessageService : ObservableRecipient
    {
        private void RegisterMessages()
        {
            Debug.Assert(Messenger is not null);
            IsActive = true;
            Messenger.Register<ExecuteGameActionMessage>(this, (recipient, message) =>
            {
                try
                {
                   // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<ShuffleMessage>(this, (recipient, message) =>
            {
                try
                {
                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<BuildingUpgradeMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<SetPlayerOrderMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<RoadPurchaseMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<MoveRobberMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<Catan3.Models.NewGameMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<LoadGameMessage>(this, async (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<StartRecordingMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<StopRecordingMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<RollMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<PurchaseMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });

            Messenger.Register(this, (object recipient, ParticipatingInSupplementalMessage message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });

            Messenger.Register<BalanceBoardMessage>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });
            Messenger.Register(this, (object recipient, GoFirstMessage message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }
            });

            Messenger.Register<PersistGameMessage>(this, async (recipient, message) =>
            {
                try
                {

                    // Dispatch to the GameController
                }
                catch (GameException e)
                {
                    SendErrorMessage(e.Message, e.ErrorLevel);
                }

            });
        }

        private void SendErrorMessage(string message, ErrorLevel errorLevel, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            this.TraceMessage(errorLevel.ToString() + ": " + message, indentLevel, cmb, cln, cfp);
            Messenger.Send(new ErrorMessage(message, errorLevel, cmb, cln, cfp));
        }
    }
}
