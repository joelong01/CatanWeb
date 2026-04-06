/**
 * replay-recording.ts
 *
 * Replays a recording through the real GameService API with SignalR confirmation.
 * Creates a new game with the same seed, connects via SignalR, and sends each
 * action via POST /api/game/action — waiting for the GameStateUpdated callback
 * before sending the next action (same flow as the browser).
 *
 * Usage:
 *   npx tsx scripts/replay-recording.ts <recording-file> [options]
 *
 * Options:
 *   --moves N        Stop after N moves (default: all)
 *   --skip N         Resume from action N (joins existing game)
 *   --game-id ID     Join this game ID directly
 *   --no-record      Disable recording (default: recording enabled)
 *   --url URL        GameService URL (default: http://localhost:8080)
 *   --delay MS       Extra delay between actions in ms (default: 0)
 *   --verbose        Print each action as it's sent
 *   --help           Show this help
 */

import { readFileSync, writeFileSync, mkdirSync } from 'fs';
import { basename } from 'path';
import * as signalR from '@microsoft/signalr';

// ─── Helpers ────────────────────────────────────────────────────────────────

/** HH:MM:SS.mmm timestamp */
function timestamp(): string {
  const d = new Date();
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}.${String(d.getMilliseconds()).padStart(3, '0')}`;
}

// ─── Types ──────────────────────────────────────────────────────────────────

interface RecordingFile {
  id: string;
  name: string;
  actionCount: number;
  gameType: string;
  playerCount: number;
  data: string;
}

interface RecordingData {
  initialGameModel: {
    gameType: string;
    gameName: string;
    players: Array<{ id: string }>;
    random: { seed: number; iterations: number };
    houseRules: Record<string, unknown>;
  };
  actions: RecordingAction[];
}

interface RecordingAction {
  type: string;
  expectedGameHash: string;
  expectedGameState: string;
  roll?: { redRoll: number; whiteRoll: number; specialDice: string; normalRoll: string };
  entitlement?: string;
  roadKey?: unknown;
  buildingKey?: unknown;
  coordinates?: { q: number; r: number; s: number };
  targetPlayerId?: string | null;
  playerId?: string;
}

interface ActionTiming {
  index: number;
  type: string;
  state: string;
  restMs: number;
  signalrMs: number;
  totalMs: number;
  timedOut: boolean;
  success: boolean;
  error?: string;
}

// ─── Action Mapping ─────────────────────────────────────────────────────────

function mapAction(action: RecordingAction): {
  messageType: string;
  messageData: Record<string, unknown>;
} {
  switch (action.type) {
    case 'roll':
      return { messageType: 'RollMessage', messageData: { roll: action.roll } };
    case 'purchase':
      return { messageType: 'PurchaseMessage', messageData: { entitlement: action.entitlement } };
    case 'roadPurchase':
      return { messageType: 'RoadPurchaseMessage', messageData: { roadKey: action.roadKey } };
    case 'buildingUpgrade':
      return {
        messageType: 'BuildingUpgradeMessage',
        messageData: { buildingKey: action.buildingKey },
      };
    case 'moveRobber':
      return {
        messageType: 'MoveRobberMessage',
        messageData: {
          coordinates: action.coordinates,
          targetPlayerId: action.targetPlayerId ?? null,
        },
      };
    case 'nextRecord':
      return { messageType: 'NextMessage', messageData: {} };
    case 'undoRecord':
      return { messageType: 'UndoMessage', messageData: {} };
    case 'redoRecord':
      return { messageType: 'RedoMessage', messageData: {} };
    case 'shuffleRecord':
      return { messageType: 'ShuffleMessage', messageData: {} };
    case 'balanceBoard':
      return { messageType: 'BalanceBoardMessage', messageData: {} };
    case 'goFirst':
      return { messageType: 'GoFirstMessage', messageData: { playerId: action.playerId } };
    case 'declareWinner':
      return { messageType: 'DeclareWinnerMessage', messageData: { winnerId: action.playerId } };
    default:
      throw new Error(`Unknown action type: ${action.type}`);
  }
}

// ─── API + SignalR Helpers ──────────────────────────────────────────────────

async function apiPost(url: string, path: string, body: unknown): Promise<unknown> {
  const response = await fetch(`${url}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${response.status} ${response.statusText}: ${text}`);
  }
  return response.json();
}

async function apiGet(url: string, path: string): Promise<unknown> {
  const response = await fetch(`${url}${path}`);
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return response.json();
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Creates a SignalR connection, joins the game, and returns a function
 * that waits for the next GameStateUpdated callback.
 *
 * Uses a buffered approach: if GameStateUpdated fires before waitForUpdate()
 * is called, the notification is queued so waitForUpdate() resolves immediately.
 */
async function connectSignalR(
  url: string,
  gameId: string,
  playerId: string,
  verbose: boolean
): Promise<{
  waitForUpdate: () => Promise<{ timedOut: boolean; error?: string }>;
  prepareForUpdate: () => void;
  disconnect: () => Promise<void>;
}> {
  const hubUrl = `${url}/gameHub`;

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.None)
    .build();

  // Buffered notification: resolve is set when we're waiting, buffered flag
  // is set when a callback fires before we start waiting.
  let pendingResolve: ((result: { timedOut: boolean; error?: string }) => void) | null = null;
  let updateBuffered: { error?: string } | null = null;
  let updateCount = 0;

  function resolveWaiter(error?: string) {
    if (pendingResolve) {
      const resolve = pendingResolve;
      pendingResolve = null;
      resolve({ timedOut: false, error });
    } else {
      updateBuffered = { error };
    }
  }

  connection.on('GameStateUpdated', () => {
    updateCount++;
    if (verbose) {
      console.log(`    [SignalR] GameStateUpdated #${updateCount} at ${timestamp()}`);
    }
    resolveWaiter();
  });

  connection.on('CommandFailed', (_commandId: string, error: string) => {
    console.log(`    [SignalR] CommandFailed at ${timestamp()}: ${error}`);
    resolveWaiter(error);
  });

  connection.on('CommandCompleted', () => {
    // Success path — GameStateUpdated should also fire, but this is a backup signal
    if (verbose) {
      console.log(`    [SignalR] CommandCompleted at ${timestamp()}`);
    }
  });

  connection.onreconnecting((error) => {
    console.log(`  [SignalR] Reconnecting... ${error?.message ?? ''}`);
  });
  connection.onreconnected((connectionId) => {
    console.log(`  [SignalR] Reconnected (${connectionId})`);
  });
  connection.onclose((error) => {
    console.log(`  [SignalR] Connection closed. ${error?.message ?? ''}`);
  });

  await connection.start();
  await connection.invoke('JoinGame', gameId, playerId);

  return {
    /** Call before sending the REST action to start listening */
    prepareForUpdate: () => {
      updateBuffered = null;
      pendingResolve = null;
    },

    /** Call after REST action returns — resolves when GameStateUpdated or CommandFailed arrives */
    waitForUpdate: () =>
      new Promise<{ timedOut: boolean; error?: string }>((resolve) => {
        // If callback already arrived while REST was in-flight, resolve immediately
        if (updateBuffered !== null) {
          const buffered = updateBuffered;
          updateBuffered = null;
          resolve({ timedOut: false, error: buffered.error });
          return;
        }
        pendingResolve = resolve;
        // Timeout safety — log and proceed
        setTimeout(() => {
          if (pendingResolve === resolve) {
            pendingResolve = null;
            console.log(`\n  [TIMEOUT] No SignalR callback after 10s — proceeding`);
            resolve({ timedOut: true });
          }
        }, 10000);
      }),

    disconnect: async () => {
      try {
        await connection.invoke('LeaveGame', gameId, playerId);
      } catch {
        // ignore
      }
      await connection.stop();
    },
  };
}

// ─── Main ───────────────────────────────────────────────────────────────────

async function main() {
  const args = process.argv.slice(2);
  if (args.length === 0 || args.includes('--help')) {
    console.log(`
Usage: npx tsx scripts/replay-recording.ts <recording-file> [options]

Options:
  --moves N        Stop after N moves (default: all)
  --skip N         Resume from action N (joins existing game)
  --game-id ID     Join this game ID directly
  --no-record      Disable recording (default: recording enabled)
  --url URL        GameService URL (default: http://localhost:8080)
  --delay MS       Extra delay between actions (default: 0)
  --verbose        Print each action
`);
    process.exit(0);
  }

  const filePath = args[0];
  const maxMoves = args.includes('--moves')
    ? parseInt(args[args.indexOf('--moves') + 1])
    : Infinity;
  const skipMoves = args.includes('--skip') ? parseInt(args[args.indexOf('--skip') + 1]) : 0;
  const existingGameId = args.includes('--game-id') ? args[args.indexOf('--game-id') + 1] : null;
  const record = !args.includes('--no-record');
  const url = args.includes('--url') ? args[args.indexOf('--url') + 1] : 'http://localhost:8080';
  const delay = args.includes('--delay') ? parseInt(args[args.indexOf('--delay') + 1]) : 0;
  const verbose = args.includes('--verbose');

  // Load recording
  console.log(`Loading recording: ${filePath}`);
  const rawFile: RecordingFile = JSON.parse(readFileSync(filePath, 'utf-8'));
  const data: RecordingData = JSON.parse(rawFile.data);
  const initialModel = data.initialGameModel;

  console.log(`  Name: ${rawFile.name}`);
  console.log(`  Actions: ${data.actions.length}`);
  console.log(`  Players: ${initialModel.players.map((p) => p.id).join(', ')}`);
  console.log(`  Seed: ${initialModel.random.seed}`);
  console.log(`  Record: ${record}`);
  console.log();

  // Check health
  try {
    const health = (await apiGet(url, '/health')) as { status: string };
    console.log(`GameService: ${health.status}`);
  } catch {
    console.error(`GameService not reachable at ${url}`);
    process.exit(1);
  }

  // Create or find game
  let gameId: string;

  if (skipMoves > 0 && existingGameId) {
    gameId = existingGameId;
    console.log(`\nJoining existing game: ${gameId}`);
  } else if (skipMoves > 0) {
    console.log(`\nSearching for "Replay: ${rawFile.name}"...`);
    const gamesResult = (await apiGet(url, '/api/games')) as {
      games: Array<{ gameId: string; gameName: string; turnCount: number }>;
    };
    const match = gamesResult.games?.find((g) => g.gameName === `Replay: ${rawFile.name}`);
    if (!match) {
      console.error(`No existing game found. Run without --skip first.`);
      process.exit(1);
    }
    gameId = match.gameId;
    console.log(`Found: ${gameId} (turn ${match.turnCount})`);
  } else {
    console.log(`\nCreating game with seed ${initialModel.random.seed}...`);
    const templateId = initialModel.gameType === 'Regular' ? 'regular' : 'expansion';
    const result = (await apiPost(url, '/api/game/new', {
      gameType: initialModel.gameType,
      playerIds: initialModel.players.map((p) => p.id),
      gameName: `Replay: ${rawFile.name}`,
      houseRules: initialModel.houseRules,
      saveLifetimeStats: false,
      templateId,
      recordGame: record,
      seed: initialModel.random.seed,
    })) as { success: boolean; gameId: string };

    if (!result.success) {
      console.error('Failed to create game:', result);
      process.exit(1);
    }
    gameId = result.gameId;
    console.log(`Game created: ${gameId}`);
  }

  const playerId = initialModel.players[0].id;
  const browserUrl = url.replace(':8080', ':3000');
  console.log(`  Browser: ${browserUrl}/game/${gameId}`);

  // Connect SignalR — same as the browser client
  console.log(`\nConnecting SignalR...`);
  const { waitForUpdate, prepareForUpdate, disconnect } = await connectSignalR(
    url,
    gameId,
    playerId,
    verbose
  );
  console.log(`Connected at ${timestamp()}.`);

  // Replay actions
  const endIndex = Math.min(data.actions.length, skipMoves + maxMoves);
  const movesToPlay = endIndex - skipMoves;
  console.log(`\nReplaying actions ${skipMoves}-${endIndex - 1} (${movesToPlay} actions)...\n`);

  const timings: ActionTiming[] = [];
  let succeeded = 0;
  let failed = 0;

  let timeoutCount = 0;

  for (let i = skipMoves; i < endIndex; i++) {
    const action = data.actions[i];
    const { messageType, messageData } = mapAction(action);

    let success = true;
    let error: string | undefined;
    let restMs = 0;
    let signalrMs = 0;
    let timedOut = false;

    // Start listening before the POST so we don't miss the callback
    prepareForUpdate();

    const t0 = performance.now();
    console.log(
      `  [${String(i).padStart(3)}/${endIndex}] ${timestamp()} >> ${messageType.padEnd(24)} ${action.expectedGameState.padEnd(25)}`
    );

    try {
      // Send action via REST
      await apiPost(url, '/api/game/action', {
        gameId,
        playerId,
        messageType,
        messageData,
      });

      const t1 = performance.now();
      restMs = Math.round((t1 - t0) * 100) / 100;

      // Wait for SignalR GameStateUpdated or CommandFailed
      const result = await waitForUpdate();
      const t2 = performance.now();
      signalrMs = Math.round((t2 - t1) * 100) / 100;
      timedOut = result.timedOut;

      if (timedOut) timeoutCount++;
      if (result.error) {
        // Server rejected the action (CommandFailed) — no state change
        error = `[REJECTED] ${result.error}`;
        success = false;
        failed++;
      } else {
        succeeded++;
      }
    } catch (e) {
      const t1 = performance.now();
      restMs = Math.round((t1 - t0) * 100) / 100;
      success = false;
      error = e instanceof Error ? e.message : String(e);
      failed++;
    }

    const totalMs = Math.round((performance.now() - t0) * 100) / 100;

    timings.push({
      index: i,
      type: action.type,
      state: action.expectedGameState,
      restMs,
      signalrMs,
      totalMs,
      timedOut,
      success,
      error,
    });

    const status = success ? (timedOut ? 'T' : '✓') : '✗';
    const errorStr = error ? ` ERR: ${error.substring(0, 60)}` : '';
    console.log(
      `           ${timestamp()} << ${status} rest=${restMs}ms signalr=${signalrMs}ms total=${totalMs}ms${errorStr}`
    );

    if (delay > 0) await sleep(delay);
  }

  // Disconnect SignalR
  await disconnect();

  // Summary
  console.log();
  console.log(`─── Results ───`);
  console.log(
    `  Actions: ${succeeded} succeeded, ${failed} failed, ${timeoutCount} timed out, ${movesToPlay} total`
  );
  console.log(`  Game ID: ${gameId}`);
  console.log(`  Recording: ${record ? 'enabled' : 'disabled'}`);
  console.log();

  // Timing by type
  const byType = new Map<string, ActionTiming[]>();
  for (const t of timings) {
    if (!byType.has(t.type)) byType.set(t.type, []);
    byType.get(t.type)!.push(t);
  }

  console.log(
    `  ${'Action'.padEnd(20)} ${'Count'.padStart(5)} ${'AvgRest'.padStart(8)} ${'AvgSR'.padStart(8)} ${'AvgTot'.padStart(8)} ${'MaxTot'.padStart(8)} ${'Tout'.padStart(5)} ${'Fail'.padStart(5)}`
  );
  console.log(
    `  ${'─'.repeat(20)} ${'─'.repeat(5)} ${'─'.repeat(8)} ${'─'.repeat(8)} ${'─'.repeat(8)} ${'─'.repeat(8)} ${'─'.repeat(5)} ${'─'.repeat(5)}`
  );

  for (const [type, actions] of [...byType.entries()].sort(
    (a, b) =>
      b[1].reduce((s, t) => s + t.totalMs, 0) / b[1].length -
      a[1].reduce((s, t) => s + t.totalMs, 0) / a[1].length
  )) {
    const avgRest =
      Math.round((actions.reduce((s, t) => s + t.restMs, 0) / actions.length) * 100) / 100;
    const avgSR =
      Math.round((actions.reduce((s, t) => s + t.signalrMs, 0) / actions.length) * 100) / 100;
    const avgTot =
      Math.round((actions.reduce((s, t) => s + t.totalMs, 0) / actions.length) * 100) / 100;
    const maxTot = Math.round(Math.max(...actions.map((t) => t.totalMs)) * 100) / 100;
    const touts = actions.filter((t) => t.timedOut).length;
    const failCount = actions.filter((t) => !t.success).length;
    console.log(
      `  ${type.padEnd(20)} ${String(actions.length).padStart(5)} ${String(avgRest).padStart(8)} ${String(avgSR).padStart(8)} ${String(avgTot).padStart(8)} ${String(maxTot).padStart(8)} ${String(touts).padStart(5)} ${String(failCount).padStart(5)}`
    );
  }

  const totalMs = Math.round(timings.reduce((s, t) => s + t.totalMs, 0) * 100) / 100;
  console.log();
  console.log(`  Total time: ${totalMs}ms (${Math.round((totalMs / 1000) * 100) / 100}s)`);
  console.log(
    `  Average per action: ${Math.round((totalMs / Math.max(movesToPlay, 1)) * 100) / 100}ms`
  );
  console.log();
  console.log(`Open in browser: ${browserUrl}/game/${gameId}`);

  // ─── Write perf log ────────────────────────────────────────────────────────

  function percentile(sorted: number[], p: number): number {
    const idx = Math.ceil((p / 100) * sorted.length) - 1;
    return sorted[Math.max(0, idx)];
  }

  const summaryByType: Record<
    string,
    {
      count: number;
      avgRest: number;
      avgSignalr: number;
      avgTotal: number;
      p50: number;
      p95: number;
      max: number;
      timedOut: number;
      failed: number;
    }
  > = {};

  for (const [type, actions] of byType.entries()) {
    const sorted = actions.map((t) => t.totalMs).sort((a, b) => a - b);
    summaryByType[type] = {
      count: actions.length,
      avgRest: Math.round((actions.reduce((s, t) => s + t.restMs, 0) / actions.length) * 100) / 100,
      avgSignalr:
        Math.round((actions.reduce((s, t) => s + t.signalrMs, 0) / actions.length) * 100) / 100,
      avgTotal: Math.round((sorted.reduce((s, v) => s + v, 0) / sorted.length) * 100) / 100,
      p50: percentile(sorted, 50),
      p95: percentile(sorted, 95),
      max: sorted[sorted.length - 1],
      timedOut: actions.filter((t) => t.timedOut).length,
      failed: actions.filter((t) => !t.success).length,
    };
  }

  const allSorted = timings.map((t) => t.totalMs).sort((a, b) => a - b);

  const perfLog = {
    timestamp: new Date().toISOString(),
    recording: basename(filePath, '.json'),
    gameId,
    url,
    seed: initialModel.random.seed,
    playerCount: initialModel.players.length,
    actionRange: { start: skipMoves, end: endIndex, total: movesToPlay },
    results: { succeeded, failed, totalMs },
    overall: {
      avg: Math.round((totalMs / Math.max(movesToPlay, 1)) * 100) / 100,
      p50: percentile(allSorted, 50),
      p95: percentile(allSorted, 95),
      max: allSorted[allSorted.length - 1],
    },
    byType: summaryByType,
    actions: timings,
  };

  const logsDir = `${__dirname}/../logs`;
  mkdirSync(logsDir, { recursive: true });
  const ts = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
  const logPath = `${logsDir}/replay-${ts}.json`;
  writeFileSync(logPath, JSON.stringify(perfLog, null, 2));
  console.log(`  Perf log: ${logPath}`);

  process.exit(failed > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error('Fatal error:', e);
  process.exit(1);
});
