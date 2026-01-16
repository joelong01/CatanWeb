/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { SpecialDice } from './special-dice';
import { ValidCatanRoll } from './valid-catan-roll';

export interface TurnRollModel {
    redRoll: number;
    whiteRoll: number;
    specialDice: SpecialDice;
    normalRoll: ValidCatanRoll;
}
