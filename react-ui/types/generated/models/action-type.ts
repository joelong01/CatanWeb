/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export type ActionType = 'RollDice' | 'AdvanceNext' | 'GoFirst' | 'PlaceSettlement' | 'PlaceRoad' | 'UpgradeToCity' | 'PurchaseRoad' | 'PurchaseSettlement' | 'PurchaseCity' | 'PurchaseSoldier' | 'MoveRobber' | 'SelectSupplementalPlayers' | 'ShuffleBoard' | 'PreviousBoard' | 'RedoBoard' | 'LoadGame' | 'StartNewGame' | 'VerifyGameState' | 'VerifyTurnResources' | 'VerifyGameResources' | 'Wait';

export const ActionType = {
    RollDice: 'RollDice',
    AdvanceNext: 'AdvanceNext',
    GoFirst: 'GoFirst',
    PlaceSettlement: 'PlaceSettlement',
    PlaceRoad: 'PlaceRoad',
    UpgradeToCity: 'UpgradeToCity',
    PurchaseRoad: 'PurchaseRoad',
    PurchaseSettlement: 'PurchaseSettlement',
    PurchaseCity: 'PurchaseCity',
    PurchaseSoldier: 'PurchaseSoldier',
    MoveRobber: 'MoveRobber',
    SelectSupplementalPlayers: 'SelectSupplementalPlayers',
    ShuffleBoard: 'ShuffleBoard',
    PreviousBoard: 'PreviousBoard',
    RedoBoard: 'RedoBoard',
    LoadGame: 'LoadGame',
    StartNewGame: 'StartNewGame',
    VerifyGameState: 'VerifyGameState',
    VerifyTurnResources: 'VerifyTurnResources',
    VerifyGameResources: 'VerifyGameResources',
    Wait: 'Wait',
} as const;
