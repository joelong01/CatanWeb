/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { GameType } from './game-type';
import { HouseRules } from './house-rules';

export interface NewGameMessage {
    gameType: GameType;
    playerIds: string[];
    gameName: string;
    houseRules: HouseRules;
    saveLifetimeStats: boolean;
    templateId: string;
    recordGame: boolean;
    seed: number;
}
