/**
 * Human-readable display strings for each GameState value.
 * Maps from C# GameState [Description] attributes in GameEnums.cs.
 * TODO: Auto-generate this from C# during build phase.
 */

import type { GameState } from '@/types/generated/models/game-state';

export const GAME_STATE_MESSAGES: Record<GameState, string> = {
  Uninitialized: 'Uninitialized',
  WaitingForNewGame: 'New Game',
  BeginResourceAllocation: 'Start Pick Resources',
  WaitingForPlayers: 'Pick Board...',
  PickingBoard: 'Accept Board',
  WaitingForRollForOrder: 'Roll For Order...',
  FinishedRollOrder: 'Order Done',
  AllocateResourceForward: 'Next',
  AllocateResourceReverse: 'Next',
  DoneResourceAllocation: 'Start Game...',
  WaitingForRoll: 'Select Roll...',
  WaitingForNext: 'Build or click Next.',
  Supplemental: 'Supplemental',
  TooManyCards: 'Discard Cards',
  MustDestroyCity: 'Destroy City',
  PickingRandomGoldTiles: 'Picking Random Gold Cards',
  HandlePirates: 'Handling Pirate',
  DoneDestroyingCities: 'Done',
  MustMoveMerchant: 'Move Merchant',
  DestroyRoad: 'Destroy Road',
  SwapNumbers: 'Swap Numbers',
  PickDeserter: 'Pick a Deserter',
  PlaceDeserterKnight: 'Place Deserter',
  DoneWithDeserter: 'DoneWithDeserter',
  UpgradeToMetro: 'Pick City',
  TestCheckpoint: 'Test Checkpoint',
  MustMoveRobber: 'Move Robber',
  DisplaceVictimKnight: 'DnD Aggressor on Victim',
  DisplaceKnightMoveVictim: 'Move Target Knight',
  ClickOnKnight: 'Select Knight',
  PickSupplementalPlayers: 'Pick Supplemental Players',
  GameOver: 'Game Over',
};

export function getStateMessage(gameState: GameState | null | undefined): string {
  if (!gameState) return '';
  const message = GAME_STATE_MESSAGES[gameState];
  if (!message) {
    console.error(`[getStateMessage] Unknown GameState: ${gameState}`);
    return gameState;
  }
  return message;
}
