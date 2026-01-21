/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export type GameState = 'Uninitialized' | 'WaitingForNewGame' | 'BeginResourceAllocation' | 'WaitingForPlayers' | 'PickingBoard' | 'WaitingForRollForOrder' | 'FinishedRollOrder' | 'AllocateResourceForward' | 'AllocateResourceReverse' | 'DoneResourceAllocation' | 'WaitingForRoll' | 'WaitingForNext' | 'Supplemental' | 'TooManyCards' | 'MustDestroyCity' | 'PickingRandomGoldTiles' | 'HandlePirates' | 'DoneDestroyingCities' | 'MustMoveMerchant' | 'DestroyRoad' | 'SwapNumbers' | 'PickDeserter' | 'PlaceDeserterKnight' | 'DoneWithDeserter' | 'UpgradeToMetro' | 'TestCheckpoint' | 'MustMoveRobber' | 'DisplaceVictimKnight' | 'DisplaceKnightMoveVictim' | 'ClickOnKnight' | 'PickSupplementalPlayers' | 'GameOver';

export const GameState = {
    Uninitialized: 'Uninitialized',
    WaitingForNewGame: 'WaitingForNewGame',
    BeginResourceAllocation: 'BeginResourceAllocation',
    WaitingForPlayers: 'WaitingForPlayers',
    PickingBoard: 'PickingBoard',
    WaitingForRollForOrder: 'WaitingForRollForOrder',
    FinishedRollOrder: 'FinishedRollOrder',
    AllocateResourceForward: 'AllocateResourceForward',
    AllocateResourceReverse: 'AllocateResourceReverse',
    DoneResourceAllocation: 'DoneResourceAllocation',
    WaitingForRoll: 'WaitingForRoll',
    WaitingForNext: 'WaitingForNext',
    Supplemental: 'Supplemental',
    TooManyCards: 'TooManyCards',
    MustDestroyCity: 'MustDestroyCity',
    PickingRandomGoldTiles: 'PickingRandomGoldTiles',
    HandlePirates: 'HandlePirates',
    DoneDestroyingCities: 'DoneDestroyingCities',
    MustMoveMerchant: 'MustMoveMerchant',
    DestroyRoad: 'DestroyRoad',
    SwapNumbers: 'SwapNumbers',
    PickDeserter: 'PickDeserter',
    PlaceDeserterKnight: 'PlaceDeserterKnight',
    DoneWithDeserter: 'DoneWithDeserter',
    UpgradeToMetro: 'UpgradeToMetro',
    TestCheckpoint: 'TestCheckpoint',
    MustMoveRobber: 'MustMoveRobber',
    DisplaceVictimKnight: 'DisplaceVictimKnight',
    DisplaceKnightMoveVictim: 'DisplaceKnightMoveVictim',
    ClickOnKnight: 'ClickOnKnight',
    PickSupplementalPlayers: 'PickSupplementalPlayers',
    GameOver: 'GameOver',
} as const;
