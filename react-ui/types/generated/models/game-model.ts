/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ReplayableRandom } from './replayable-random';
import { EntitlementPurchaseModel } from './entitlement-purchase-model';
import { ActionFlags } from './action-flags';
import { GameType } from './game-type';
import { GameState } from './game-state';
import { PlayerModel } from './player-model';
import { TileModel } from './tile-model';
import { BuildingModel } from './building-model';
import { RoadModel } from './road-model';
import { HarborModel } from './harbor-model';
import { RobberModel } from './robber-model';
import { HouseRules } from './house-rules';
import { ResourceRules } from './resource-rules';
import { RollModel } from './roll-model';
import { ResourcesModel } from './resources-model';

export interface GameModel {
    gameHash: string;
    random: ReplayableRandom;
    entitlementPurchaseModel: EntitlementPurchaseModel[];
    actionFlags: ActionFlags;
    gameType: GameType;
    gameName: string;
    saveLifetimeStats: boolean;
    gameState: GameState;
    hasSupplementalBuildPhase: boolean;
    players: PlayerModel[];
    tiles: TileModel[];
    buildings: BuildingModel[];
    roads: RoadModel[];
    harbors: HarborModel[];
    robber: RobberModel;
    houseRules: HouseRules;
    resourceRules: ResourceRules;
    currentPlayerId: string;
    gameId: string;
    createdTime: Date;
    gameStateMachineVersion: number;
    rollModel: RollModel;
    nextPlayerToRollAfterSupplemental: string;
    gameResourcesModel: ResourcesModel;
    previousGameState: GameState;
}
