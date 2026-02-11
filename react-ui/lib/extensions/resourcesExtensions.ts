/**
 * ResourcesModel extension functions - TypeScript equivalents of C# ResourcesModelExtensions.
 *
 * These functions provide utility operations for ResourcesModel.
 *
 * @module resourcesExtensions
 */

import type { ResourcesModel } from '@/types/generated/models/resources-model';
import type { ResourceType } from '@/types/generated/models/resource-type';

/**
 * Creates a new empty ResourcesModel with all values set to 0.
 *
 * @returns A new ResourcesModel with all resource counts at 0
 */
export function createEmptyResourcesModel(): ResourcesModel {
  return {
    brick: 0,
    goldMine: 0,
    ore: 0,
    sheep: 0,
    wheat: 0,
    wood: 0,
    paper: 0,
    cloth: 0,
    coin: 0,
    politics: 0,
    trade: 0,
    science: 0,
    victoryPoint: 0,
    anyDevCard: 0,
    robber: 0,
  };
}

/**
 * Computes the total count of all resources in a ResourcesModel.
 * This is the client-side equivalent of C#'s [JsonIgnore] Count property.
 */
export function resourceCount(model: ResourcesModel): number {
  return (
    model.wheat +
    model.wood +
    model.brick +
    model.ore +
    model.sheep +
    model.goldMine +
    model.cloth +
    model.coin +
    model.paper +
    model.victoryPoint +
    model.politics +
    model.science +
    model.trade +
    model.anyDevCard +
    model.robber
  );
}

/**
 * Adds a resource amount to a ResourcesModel.
 * Mutates the model in place (matches C# behavior).
 *
 * **WARNING:** This function mutates the model. Only use on:
 * - Freshly created objects from `createEmptyResourcesModel()`
 * - Objects inside an Immer producer (Zustand `set()` callback)
 * - Local working copies, never directly on React state or store objects
 *
 * @param model - The ResourcesModel to modify
 * @param resourceType - The type of resource to add
 * @param toAdd - The amount to add
 *
 * @example
 * // CORRECT: Use on a fresh object
 * const resources = createEmptyResourcesModel();
 * addResource(resources, 'Wheat', 2);
 *
 * @example
 * // CORRECT: Use inside Immer producer
 * useGameStore.setState((state) => {
 *   addResource(state.player.resources, 'Ore', 1);
 * });
 *
 * @example
 * // WRONG: Never mutate store state directly
 * // addResource(gameStore.getState().player.resources, 'Ore', 1); // DON'T DO THIS
 */
export function addResource(
  model: ResourcesModel,
  resourceType: ResourceType,
  toAdd: number
): void {
  switch (resourceType) {
    case 'Sheep':
      model.sheep += toAdd;
      break;
    case 'Wood':
      model.wood += toAdd;
      break;
    case 'Ore':
      model.ore += toAdd;
      break;
    case 'Wheat':
      model.wheat += toAdd;
      break;
    case 'Brick':
      model.brick += toAdd;
      break;
    case 'GoldMine':
      model.goldMine += toAdd;
      break;
    case 'Robber':
      model.robber += toAdd;
      break;
    case 'Cloth':
      model.cloth += toAdd;
      break;
    case 'Coin':
      model.coin += toAdd;
      break;
    case 'Paper':
      model.paper += toAdd;
      break;
    case 'Politics':
      model.politics += toAdd;
      break;
    case 'Science':
      model.science += toAdd;
      break;
    case 'Trade':
      model.trade += toAdd;
      break;
    case 'VictoryPoint':
      model.victoryPoint += toAdd;
      break;
    case 'AnyDevCard':
      model.anyDevCard += toAdd;
      break;
    case 'Desert':
    case 'Back':
    case 'None':
    case 'Sea':
    default:
      // No-op for these resource types
      break;
  }
}
