/**
 * This is a TypeGen auto-generated file.
 * Contains enum description mappings extracted from C# [Description] attributes.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { GameState } from './game-state';
import { Entitlement } from './entitlement';
import { ActionType } from './action-type';

/**
 * Display text for GameState enum values.
 * Maps each enum value to its user-friendly description from C# [Description] attributes.
 */
export const GameStateDescriptions: Record<GameState, string> = {
  [GameState.Uninitialized]: 'Uninitialized',
  [GameState.WaitingForNewGame]: 'New Game',
  [GameState.BeginResourceAllocation]: 'Start Pick Resources',
  [GameState.WaitingForPlayers]: 'Pick Board...',
  [GameState.PickingBoard]: 'Accept Board',
  [GameState.WaitingForRollForOrder]: 'Roll For Order...',
  [GameState.FinishedRollOrder]: 'Order Done',
  [GameState.AllocateResourceForward]: 'Next',
  [GameState.AllocateResourceReverse]: 'Next',
  [GameState.DoneResourceAllocation]: 'Start Game...',
  [GameState.WaitingForRoll]: 'Select Roll...',
  [GameState.WaitingForNext]: 'Build or click Next.',
  [GameState.Supplemental]: 'Supplemental',
  [GameState.TooManyCards]: 'Discard Cards',
  [GameState.MustDestroyCity]: 'Destroy City',
  [GameState.PickingRandomGoldTiles]: 'Picking Random Gold Cards',
  [GameState.HandlePirates]: 'Handling Pirate',
  [GameState.DoneDestroyingCities]: 'Done',
  [GameState.MustMoveMerchant]: 'Move Merchant',
  [GameState.DestroyRoad]: 'Destroy Road',
  [GameState.SwapNumbers]: 'Swap Numbers',
  [GameState.PickDeserter]: 'Pick a Deserter',
  [GameState.PlaceDeserterKnight]: 'Place Deserter',
  [GameState.DoneWithDeserter]: 'DoneWithDeserter',
  [GameState.UpgradeToMetro]: 'Pick City',
  [GameState.TestCheckpoint]: 'Test Checkpoint',
  [GameState.MustMoveRobber]: 'Move Robber',
  [GameState.DisplaceVictimKnight]: 'DnD Aggressor on Victim',
  [GameState.DisplaceKnightMoveVictim]: 'Move Target Knight',
  [GameState.ClickOnKnight]: 'Select Knight',
  [GameState.PickSupplementalPlayers]: 'Pick Supplemental Players',
  [GameState.GameOver]: 'Game Over'
};

/**
 * Display text for Entitlement enum values.
 * Maps each enum value to its user-friendly description from C# [Description] attributes.
 */
export const EntitlementDescriptions: Record<Entitlement, string> = {
  [Entitlement.Undefined]: 'Undefined',
  [Entitlement.DevCard]: 'Dev Card',
  [Entitlement.Settlement]: 'Settlement',
  [Entitlement.City]: 'City',
  [Entitlement.Road]: 'Road',
  [Entitlement.Ship]: 'Ship',
  [Entitlement.BuyKnight]: 'Buy',
  [Entitlement.UpgradeKnight]: 'Upgrade',
  [Entitlement.ActivateKnight]: 'Activate',
  [Entitlement.Soldier]: 'Play Soldier',
  [Entitlement.PoliticsUpgrade]: 'Politics',
  [Entitlement.ScienceUpgrade]: 'Science',
  [Entitlement.TradeUpgrade]: 'Trade',
  [Entitlement.Wall]: 'Wall',
  [Entitlement.DestroyCity]: 'Destroy City',
  [Entitlement.Bishop]: 'Bishop',
  [Entitlement.Deserter]: 'PickDeserter',
  [Entitlement.Inventor]: 'Inventor',
  [Entitlement.Intrigue]: 'Intrigue',
  [Entitlement.Diplomat]: 'Diplomat',
  [Entitlement.Merchant]: 'Merchant',
  [Entitlement.KnightDisplacement]: 'Displace',
  [Entitlement.UpgradeToMetro]: 'Metropolis',
  [Entitlement.KnightDisplacementMoveKnightOutOfTheWay]: 'KnightDisplacementMoveKnightOutOfTheWay',
  [Entitlement.RolledSeven]: 'Rolled Seven'
};

/**
 * Display text for ActionType enum values.
 * Maps each enum value to its user-friendly description from C# [Description] attributes.
 */
export const ActionTypeDescriptions: Record<ActionType, string> = {
  [ActionType.RollDice]: 'Roll dice with specified value (physical dice input)',
  [ActionType.AdvanceNext]: 'Click Next button to advance game state',
  [ActionType.GoFirst]: 'Select players to go first during roll order',
  [ActionType.PlaceSettlement]: 'Place settlement at specified location',
  [ActionType.PlaceRoad]: 'Place road at specified location',
  [ActionType.UpgradeToCity]: 'Upgrade settlement to city',
  [ActionType.PurchaseRoad]: 'Purchase road entitlement',
  [ActionType.PurchaseSettlement]: 'Purchase settlement entitlement',
  [ActionType.PurchaseCity]: 'Purchase city entitlement',
  [ActionType.PurchaseSoldier]: 'Purchase soldier entitlement (ability to move robber)',
  [ActionType.MoveRobber]: 'Move robber to specified tile and optionally target player',
  [ActionType.SelectSupplementalPlayers]: 'Select which players participate in supplemental building',
  [ActionType.ShuffleBoard]: 'Shuffle board layout',
  [ActionType.PreviousBoard]: 'Use Previous Board (undo)',
  [ActionType.RedoBoard]: 'Use Redo Board',
  [ActionType.LoadGame]: 'Load predefined game file',
  [ActionType.StartNewGame]: 'Start new game with specified settings',
  [ActionType.VerifyGameState]: 'Verify current game state matches expected',
  [ActionType.VerifyTurnResources]: 'Verify player turn resources match expected',
  [ActionType.VerifyGameResources]: 'Verify player game total resources match expected',
  [ActionType.Wait]: 'Wait for specified time (for UI updates)'
};

/**
 * Gets the display description for an enum value.
 *
 * @param descriptions - The description record for the enum type
 * @param value - The enum value to look up
 * @returns The description string, or the value itself if no description found
 */
export function getEnumDescription<T extends string>(
  descriptions: Record<T, string>,
  value: T
): string {
  return descriptions[value] ?? value;
}
