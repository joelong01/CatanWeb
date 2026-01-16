/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { HexCoordinates } from './hex-coordinates';
import { ResourceType } from './resource-type';

export interface TileModel {
    tileKey: HexCoordinates;
    number: number;
    resourceTileType: ResourceType;
    highlighted: boolean;
    temporarilyGold: boolean;
    default: TileModel;
    stars: number;
}
