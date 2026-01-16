/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { HarborKey } from './harbor-key';
import { PlayerModel } from './player-model';

export interface HarborModel {
    harborKey: HarborKey;
    owner: PlayerModel;
    default: HarborModel;
}
