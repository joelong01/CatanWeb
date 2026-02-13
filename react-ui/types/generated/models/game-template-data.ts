/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { ResourceRules } from './resource-rules';
import { HouseRules } from './house-rules';
import { TemplateTile } from './template-tile';
import { TemplateHarbor } from './template-harbor';
import { TemplateEntitlement } from './template-entitlement';

export interface GameTemplateData {
    id: string;
    name: string;
    category: string;
    version: number;
    description: string;
    engine: string;
    gameType: string;
    resourceRules: ResourceRules;
    houseRules: HouseRules;
    hasSupplemental: boolean;
    tiles: TemplateTile[];
    harbors: TemplateHarbor[];
    entitlements: TemplateEntitlement[];
}
