/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { HexCoordinates } from './hex-coordinates';
import { HarborType } from './harbor-type';
import { HexSide } from './hex-side';

export interface HarborKey {
    hexCoordinates: HexCoordinates;
    harborType: HarborType;
    side: HexSide;
}
