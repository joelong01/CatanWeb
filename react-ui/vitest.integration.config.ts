import { defineConfig } from 'vitest/config';
import path from 'path';

/**
 * Vitest config for integration tests.
 * These tests require a running GameService at localhost:8080.
 *
 * Run with: npm run test:integration
 * After starting: pwsh ./catan.ps1 run
 */
export default defineConfig({
  test: {
    environment: 'node', // Integration tests don't need jsdom
    include: ['**/*.integration.test.ts'],
    testTimeout: 30000, // 30 second timeout for network operations
    hookTimeout: 30000,
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './'),
    },
  },
});
