/**
 * Loads .catan_test JSON files (C#-serialized game state).
 *
 * These files are the authoritative wire format — they represent exactly
 * what the GameService produces. Loading them with the generated TypeScript
 * types verifies the serialization contract.
 *
 * @module load-catan-test
 */

import { readFileSync } from 'fs';
import { resolve } from 'path';
import type { GameModel } from '@/types/generated/models/game-model';

/** Top-level structure of a .catan_test JSON file. */
export interface CatanTestData {
  gameModel: GameModel;
  actionStack: unknown[];
}

/**
 * Loads a .catan_test file from the Tests/Data directory.
 *
 * @param filename - Name of the file (e.g. 'Expansion.catan_test')
 * @returns Parsed CatanTestData with typed GameModel
 */
export function loadCatanTest(filename: string): CatanTestData {
  const filePath = resolve(__dirname, '..', '..', '..', 'Tests', 'Data', filename);
  const json = readFileSync(filePath, 'utf-8');
  return JSON.parse(json) as CatanTestData;
}
