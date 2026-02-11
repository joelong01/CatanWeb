/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { PlayerColors } from './player-colors';
import { LifetimeStats } from './lifetime-stats';

export interface PlayerProfile {
    id: string;
    name: string;
    colors: PlayerColors;
    imageUri?: string;
    lifetimeStats?: LifetimeStats;
}
