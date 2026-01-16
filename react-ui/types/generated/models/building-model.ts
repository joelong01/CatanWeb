/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { BuildingKey } from './building-key';
import { BuildingState } from './building-state';

export interface BuildingModel {
    buildingKey: BuildingKey;
    buildingState: BuildingState;
    wall: boolean;
    metropolis: boolean;
    ownerId: string;
    hasRobber: boolean;
    default: BuildingModel;
}
