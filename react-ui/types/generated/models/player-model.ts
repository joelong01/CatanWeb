/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ResourcesModel } from './resources-model';
import { Entitlement } from './entitlement';
import { HarborKey } from './harbor-key';

export interface PlayerModel {
    id: string;
    goldRolls: number;
    goodRolls: number;
    badRolls: number;
    hasLongestRoad: boolean;
    islandsPlayed: number;
    largestArmy: boolean;
    longestRoad: number;
    maxNoResourceRolls: number;
    noResourceCount: number;
    stars: number;
    score: number;
    highestScore: boolean;
    timesTargeted: number;
    participatingInSupplemental: boolean;
    finishedSupplemental: boolean;
    resourcesThisTurn: ResourcesModel;
    resourcesThisGame: ResourcesModel;
    unspentEntitlements: Entitlement[];
    spentEntitlementsThisGame: Entitlement[];
    spentEntitlementsThisTurn: Entitlement[];
    ownedHarbors: HarborKey[];
    victoryPointCards: number;
}
