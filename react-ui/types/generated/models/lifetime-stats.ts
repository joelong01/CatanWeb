/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { GameStats } from './game-stats';

export interface LifetimeStats {
    readonly currentSchemaVersion: number;
    schemaVersion: number;
    gamesPlayed: number;
    wins: number;
    longestRoadWins: number;
    largestArmyWins: number;
    longestRoadRecord: number;
    mostSoldiersRecord: number;
    mostStarsRecord: number;
    highestScoreRecord: number;
    mostTargetedRecord: number;
    mostRobberRecord: number;
    minSoldiersRecord: number;
    minStarsRecord: number;
    minTargetedRecord: number;
    minRobberRecord: number;
    totals: GameStats;
    winRate: number;
    averageStars: number;
    averageSoldiers: number;
    averageTargeted: number;
    averageRobber: number;
}
