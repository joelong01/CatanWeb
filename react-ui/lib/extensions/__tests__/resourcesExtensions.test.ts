/**
 * Tests for resourcesExtensions.ts
 */

import { describe, it, expect } from 'vitest';
import { createEmptyResourcesModel, addResource } from '../resourcesExtensions';

describe('resourcesExtensions', () => {
  describe('createEmptyResourcesModel', () => {
    it('should create a model with all zero values', () => {
      const model = createEmptyResourcesModel();

      expect(model.brick).toBe(0);
      expect(model.ore).toBe(0);
      expect(model.sheep).toBe(0);
      expect(model.wheat).toBe(0);
      expect(model.wood).toBe(0);
      expect(model.goldMine).toBe(0);
      expect(model.paper).toBe(0);
      expect(model.cloth).toBe(0);
      expect(model.coin).toBe(0);
      expect(model.politics).toBe(0);
      expect(model.trade).toBe(0);
      expect(model.science).toBe(0);
      expect(model.victoryPoint).toBe(0);
      expect(model.anyDevCard).toBe(0);
      expect(model.robber).toBe(0);
    });
  });

  describe('addResource', () => {
    it('should add Sheep resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Sheep', 3);
      expect(model.sheep).toBe(3);
    });

    it('should add Wood resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Wood', 2);
      expect(model.wood).toBe(2);
    });

    it('should add Ore resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Ore', 1);
      expect(model.ore).toBe(1);
    });

    it('should add Wheat resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Wheat', 4);
      expect(model.wheat).toBe(4);
    });

    it('should add Brick resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Brick', 5);
      expect(model.brick).toBe(5);
    });

    it('should add GoldMine resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'GoldMine', 2);
      expect(model.goldMine).toBe(2);
    });

    it('should add Cloth resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Cloth', 1);
      expect(model.cloth).toBe(1);
    });

    it('should add Coin resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Coin', 3);
      expect(model.coin).toBe(3);
    });

    it('should add Paper resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Paper', 2);
      expect(model.paper).toBe(2);
    });

    it('should add Politics resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Politics', 1);
      expect(model.politics).toBe(1);
    });

    it('should add Science resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Science', 2);
      expect(model.science).toBe(2);
    });

    it('should add Trade resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Trade', 1);
      expect(model.trade).toBe(1);
    });

    it('should add VictoryPoint resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'VictoryPoint', 2);
      expect(model.victoryPoint).toBe(2);
    });

    it('should add AnyDevCard resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'AnyDevCard', 1);
      expect(model.anyDevCard).toBe(1);
    });

    it('should add Robber resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Robber', 1);
      expect(model.robber).toBe(1);
    });

    it('should handle Desert (no-op)', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Desert', 5);
      // Desert should not add anything
      expect(model.brick).toBe(0);
      expect(model.ore).toBe(0);
      expect(model.sheep).toBe(0);
      expect(model.wheat).toBe(0);
      expect(model.wood).toBe(0);
    });

    it('should handle None (no-op)', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'None', 5);
      // None should not add anything
      expect(model.brick).toBe(0);
    });

    it('should handle Sea (no-op)', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Sea', 5);
      // Sea should not add anything
      expect(model.brick).toBe(0);
    });

    it('should handle Back (no-op)', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Back', 5);
      // Back should not add anything
      expect(model.brick).toBe(0);
    });

    it('should accumulate resources on multiple adds', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Wheat', 2);
      addResource(model, 'Wheat', 3);
      expect(model.wheat).toBe(5);
    });

    it('should add multiple different resources', () => {
      const model = createEmptyResourcesModel();
      addResource(model, 'Wheat', 2);
      addResource(model, 'Ore', 1);
      addResource(model, 'Brick', 3);

      expect(model.wheat).toBe(2);
      expect(model.ore).toBe(1);
      expect(model.brick).toBe(3);
      expect(model.sheep).toBe(0);
      expect(model.wood).toBe(0);
    });
  });
});
