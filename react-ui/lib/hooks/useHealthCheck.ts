/**
 * @module hooks/useHealthCheck
 *
 * Phase 1 health check hook for the splash screen.
 *
 * Calls the GameService `/health` endpoint on mount with exponential-backoff
 * retry logic for cold starts. Returns the health status so the home page
 * can conditionally show the splash overlay.
 *
 * Retry strategy: 3 attempts, backoff 2s → 6s → 18s, total ~30s max.
 * HTTP 503 → "GameService is starting up..."
 * Network error → "GameService is unreachable"
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { serviceConfig } from '../services/config';

// ── Types ────────────────────────────────────────────────────────────────────

export type HealthStatus = 'checking' | 'healthy' | 'failed' | 'retrying';

/** Database diagnostics returned by the /health endpoint. */
export interface DatabaseDiagnostics {
  connected: boolean;
  playerCount: number;
  gameCount: number;
  templateCount: number;
  recordingCount: number;
  error?: string;
}

/** Parsed response from the GameService /health endpoint. */
export interface HealthResponse {
  status: string;
  timestamp: string;
  version: {
    commit: string;
    buildTime: string;
    environment: string;
  };
  databaseDiagnostics: DatabaseDiagnostics;
}

/** Return value from the useHealthCheck hook. */
export interface HealthResult {
  /** Current check status. */
  status: HealthStatus;
  /** Parsed health response (only when status is 'healthy'). */
  data?: HealthResponse;
  /** Human-readable error message (when status is 'failed' or 'retrying'). */
  error?: string;
  /** Step-by-step log of what's happening (for troubleshooting display). */
  steps: string[];
  /** Number of retry attempts so far. */
  retryCount: number;
  /** The URL being checked. */
  healthUrl: string;
  /** Call to manually re-run the health check. */
  retry: () => void;
}

// ── Constants ────────────────────────────────────────────────────────────────

/** Backoff delays between retries (milliseconds). */
const RETRY_DELAYS = [2_000, 6_000, 18_000];

/** Maximum number of retry attempts before giving up. */
const MAX_RETRIES = RETRY_DELAYS.length;

/** Timeout for each individual fetch attempt (milliseconds). */
const FETCH_TIMEOUT = 15_000;

// ── Hook ─────────────────────────────────────────────────────────────────────

/**
 * Checks the GameService `/health` endpoint with retry logic for cold starts.
 *
 * On mount, immediately starts checking. Returns the current health status
 * so the caller can decide whether to show the splash overlay.
 */
export function useHealthCheck(): HealthResult {
  const [status, setStatus] = useState<HealthStatus>('checking');
  const [data, setData] = useState<HealthResponse | undefined>();
  const [error, setError] = useState<string | undefined>();
  const [steps, setSteps] = useState<string[]>([]);
  const [retryCount, setRetryCount] = useState(0);

  const healthUrl = `${serviceConfig.serviceUrl}/health`;

  /** Append a step to the log (visible in the troubleshooting UI). */
  const addStep = (step: string): void => {
    setSteps((prev) => [...prev, step]);
  };

  // Use ref to track whether the component is still mounted (avoid state
  // updates after unmount during async retries).
  const mountedRef = useRef(true);
  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const runCheck = useCallback(async () => {
    if (!mountedRef.current) return;
    setStatus('checking');
    setError(undefined);
    setRetryCount(0);
    setSteps([`URL: ${healthUrl}`]);

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      if (!mountedRef.current) return;

      try {
        addStep(`Attempt ${attempt + 1}: fetching ${healthUrl}...`);
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT);

        const response = await fetch(healthUrl, {
          signal: controller.signal,
        });
        clearTimeout(timer);

        addStep(`Response: HTTP ${response.status}`);

        if (response.ok) {
          addStep('Parsing JSON response...');
          const healthData: HealthResponse = await response.json();

          if (!mountedRef.current) return;

          addStep(`Service status: ${healthData.status}`);
          addStep(`Database connected: ${healthData.databaseDiagnostics?.connected ?? 'unknown'}`);

          if (healthData.databaseDiagnostics?.connected) {
            const db = healthData.databaseDiagnostics;
            addStep(
              `Players: ${db.playerCount}, Games: ${db.gameCount}, Templates: ${db.templateCount}, Recordings: ${db.recordingCount}`
            );
          }

          // Check if database is connected
          if (healthData.status === 'healthy' && healthData.databaseDiagnostics?.connected) {
            addStep('All checks passed');
            setData(healthData);
            setStatus('healthy');
            return;
          }

          // Service is up but database is degraded
          const dbError = healthData.databaseDiagnostics?.error;
          if (dbError) addStep(`Database error: ${dbError.substring(0, 150)}`);
          setData(healthData);
          setError(dbError ?? 'GameService is running but the database is not connected');
          setStatus('failed');
          return;
        }

        // Non-OK response — retry if attempts remain
        if (attempt < MAX_RETRIES) {
          if (!mountedRef.current) return;
          const msg =
            response.status === 503
              ? 'GameService is starting up...'
              : `GameService returned HTTP ${response.status}`;
          addStep(`${msg} — retrying in ${RETRY_DELAYS[attempt] / 1000}s...`);
          setStatus('retrying');
          setRetryCount(attempt + 1);
          setError(msg);
          await sleep(RETRY_DELAYS[attempt]);
          continue;
        }

        // All retries exhausted
        if (!mountedRef.current) return;
        addStep(`Failed after ${MAX_RETRIES} retries`);
        setError(`GameService returned HTTP ${response.status} after ${MAX_RETRIES} retries`);
        setStatus('failed');
        return;
      } catch (err) {
        const errMsg = err instanceof Error ? err.message : String(err);
        addStep(`Error: ${errMsg}`);

        // Network error or timeout
        if (attempt < MAX_RETRIES) {
          if (!mountedRef.current) return;
          addStep(`Retrying in ${RETRY_DELAYS[attempt] / 1000}s...`);
          setStatus('retrying');
          setRetryCount(attempt + 1);
          setError('GameService is unreachable — retrying...');
          await sleep(RETRY_DELAYS[attempt]);
          continue;
        }

        // All retries exhausted
        if (!mountedRef.current) return;
        addStep(`Failed after ${MAX_RETRIES} retries`);
        setError('GameService is unreachable. Check that the service is running.');
        setStatus('failed');
        return;
      }
    }
  }, [healthUrl]);

  // Run on mount — runCheck sets state (intentional, it's the initialization path)
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => {
    runCheck();
  }, [runCheck]);

  return { status, data, error, steps, retryCount, healthUrl, retry: runCheck };
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
