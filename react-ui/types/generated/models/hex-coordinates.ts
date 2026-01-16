/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { Direction } from './direction';

export interface HexCoordinates {
    q: number;
    r: number;
    s: number;
    directions: { [key in Direction]?: HexCoordinates; };
    north: HexCoordinates;
    northEast: HexCoordinates;
    southEast: HexCoordinates;
    south: HexCoordinates;
    southWest: HexCoordinates;
    northWest: HexCoordinates;
    default: HexCoordinates;
}
