/**
 * Integration tests for RecordingPlayer.
 *
 * These tests require a running GameService instance at localhost:8080.
 * Run with: pwsh ./catan.ps1 run (in another terminal)
 * Then: npm run test:integration
 *
 * Tests verify that recordings can be loaded and played back through
 * the TypeScript GameServiceProxy, with hash verification at each step.
 *
 * Output format matches `./catan.ps1 recording replay` for consistency.
 */

import { describe, it, expect, beforeAll, afterEach } from 'vitest';
import {
  RecordingPlayer,
  type RecordingListItem,
} from './RecordingPlayer';

const SERVICE_URL = 'http://localhost:8080';
const TEST_TIMEOUT = 120000; // 120 seconds for full recording playback

// Check for debug mode via environment variable
const DEBUG = process.env.DEBUG === 'true' || process.env.DEBUG === '1';

// Smoke test mode - run only the largest recording for quick validation
const SMOKE_TEST = process.env.SMOKE === 'true' || process.env.SMOKE === '1';

/**
 * Check if GameService is running.
 */
async function isServiceRunning(): Promise<boolean> {
  try {
    const response = await fetch(`${SERVICE_URL}/api/recordings`, {
      signal: AbortSignal.timeout(5000),
    });
    return response.ok;
  } catch {
    return false;
  }
}

/**
 * Format recording name and action count like PowerShell output.
 */
function formatRecordingInfo(recording: RecordingListItem): string {
  return `${recording.name} (${recording.actionCount} actions)`;
}

describe('RecordingPlayer Integration Tests', () => {
  let serviceAvailable = false;
  let player: RecordingPlayer;
  let recordings: RecordingListItem[] = [];

  beforeAll(async () => {
    serviceAvailable = await isServiceRunning();
    if (!serviceAvailable) {
      console.log('');
      console.log('⚠️  GameService not running at localhost:8080');
      console.log('   Run: pwsh ./catan.ps1 run');
      console.log('   Skipping integration tests.');
      console.log('');
      return;
    }

    // Pre-load recordings list for all tests
    const tempPlayer = new RecordingPlayer(SERVICE_URL);
    recordings = await tempPlayer.listRecordings();
    await tempPlayer.dispose();
  }, TEST_TIMEOUT);

  afterEach(async () => {
    if (player) {
      await player.dispose();
    }
  });

  describe('Recording API', () => {
    it('should list available recordings', async () => {
      if (!serviceAvailable) return;

      player = new RecordingPlayer(SERVICE_URL);
      const list = await player.listRecordings();

      expect(Array.isArray(list)).toBe(true);

      if (DEBUG) {
        console.log(`Found ${list.length} recordings`);
        for (const rec of list) {
          console.log(`  - ${formatRecordingInfo(rec)}`);
        }
      }
    });

    it('should load a recording by ID', async () => {
      if (!serviceAvailable) return;

      player = new RecordingPlayer(SERVICE_URL);
      const list = await player.listRecordings();

      if (list.length === 0) {
        if (DEBUG) console.log('No recordings available to test');
        return;
      }

      // Find a recording with reasonable action count
      const testRecording =
        list.find((r) => r.actionCount > 0 && r.actionCount < 50) ?? list[0];

      const data = await player.loadRecording(testRecording.id);

      expect(data).toBeDefined();
      expect(data.initialGameModel).toBeDefined();
      expect(data.actions).toBeDefined();
      expect(Array.isArray(data.actions)).toBe(true);
      expect(data.actions.length).toBe(testRecording.actionCount);

      if (DEBUG) {
        console.log(
          `Loaded recording "${testRecording.name}": ` +
            `${data.actions.length} actions, ` +
            `initial state: ${data.initialGameModel.gameState}`
        );
      }
    });
  });

  describe('Playback Initialization', () => {
    it('should initialize playback from a recording', async () => {
      if (!serviceAvailable) return;

      player = new RecordingPlayer(SERVICE_URL);

      if (recordings.length === 0) {
        if (DEBUG) console.log('No recordings available to test');
        return;
      }

      const testRecording = recordings[0];
      await player.loadRecording(testRecording.id);

      const gameId = await player.initializePlayback();

      expect(gameId).toBeDefined();
      expect(typeof gameId).toBe('string');
      expect(player.gameModel).not.toBeNull();

      if (DEBUG) {
        console.log(
          `Initialized playback: gameId=${gameId}, ` +
            `state=${player.gameModel?.gameState}`
        );
      }
    }, TEST_TIMEOUT);
  });

  describe('Full Recording Playback', () => {
    // Run all recordings in parallel - models multiple concurrent games
    // Use SMOKE=1 to run only the largest recording for quick validation
    it('should play all recordings in parallel', async () => {
      if (!serviceAvailable) return;

      if (recordings.length === 0) {
        console.log('No recordings found. Create some recordings first!');
        return;
      }

      // In smoke test mode, only run the recording with the most actions
      let recordingsToRun = recordings;
      if (SMOKE_TEST) {
        const largest = recordings.reduce((max, r) =>
          r.actionCount > max.actionCount ? r : max
        );
        recordingsToRun = [largest];
      }

      console.log('');
      console.log(`Recording Replay Tests (TypeScript Proxy${SMOKE_TEST ? ' - SMOKE' : ' - Parallel'})`);
      console.log('====================================================');
      console.log(`Running ${recordingsToRun.length} recording(s)${SMOKE_TEST ? ' (smoke test)' : ' in parallel'}...`);

      const startTime = Date.now();

      // Run recordings in parallel (or single in smoke test mode)
      const results = await Promise.all(
        recordingsToRun.map(async (recording) => {
          const recPlayer = new RecordingPlayer(SERVICE_URL);
          try {
            const result = await recPlayer.playRecording(recording.id);
            return { recording, result, error: null as string | null };
          } catch (error) {
            return {
              recording,
              result: null,
              error: error instanceof Error ? error.message : String(error),
            };
          } finally {
            await recPlayer.dispose();
          }
        })
      );

      const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);

      // Collect and print results
      const passed: string[] = [];
      const failedTests: Array<{
        name: string;
        actions: number;
        error: string;
        expected?: string;
        actual?: string;
      }> = [];

      console.log('');
      for (const { recording, result, error } of results) {
        const info = `${recording.name} (${recording.actionCount} actions)`;
        if (error) {
          console.log(`  FAIL: ${info}`);
          failedTests.push({ name: recording.name, actions: recording.actionCount, error });
        } else if (result?.success) {
          console.log(`  PASS: ${info}`);
          passed.push(recording.name);
        } else if (result) {
          console.log(`  FAIL: ${info}`);
          const errorMsg = result.failedAtIndex !== undefined
            ? `Action ${result.failedAtIndex}: ${result.error}`
            : result.error ?? 'Unknown error';
          failedTests.push({
            name: recording.name,
            actions: recording.actionCount,
            error: errorMsg,
            expected: result.hashMismatch?.expected,
            actual: result.hashMismatch?.actual,
          });
        }
      }

      // Print summary
      console.log('');
      console.log(`Results: ${passed.length} passed, ${failedTests.length} failed in ${elapsed}s`);

      if (failedTests.length > 0) {
        console.log('');
        console.log('Failures:');
        for (const test of failedTests) {
          console.log(`  ${test.name}: ${test.error}`);
          if (test.expected && test.actual) {
            console.log(`    Expected: ${test.expected}, Actual: ${test.actual}`);
          }
        }
      }

      expect(failedTests.length).toBe(0);
    }, TEST_TIMEOUT * 2);
  });

  describe('Error Handling', () => {
    it('should handle non-existent recording gracefully', async () => {
      if (!serviceAvailable) return;

      player = new RecordingPlayer(SERVICE_URL);

      await expect(
        player.loadRecording('non-existent-recording-id')
      ).rejects.toThrow();
    });

    it('should handle playback without initialization', async () => {
      if (!serviceAvailable) return;

      player = new RecordingPlayer(SERVICE_URL);

      if (recordings.length === 0) return;

      await player.loadRecording(recordings[0].id);

      // Should throw because we didn't call initializePlayback
      await expect(player.executeStep()).rejects.toThrow(
        'Playback not initialized'
      );
    });
  });
});
