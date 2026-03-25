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
  /** Number of retry attempts so far. */
  retryCount: number;
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
  const [retryCount, setRetryCount] = useState(0);

  // Use ref to track whether the component is still mounted (avoid state
  // updates after unmount during async retries).
  const mountedRef = useRef(true);
  useEffect(() => {
    return () => { mountedRef.current = false; };
  }, []);

  const runCheck = useCallback(async () => {
    if (!mountedRef.current) return;
    setStatus('checking');
    setError(undefined);
    setRetryCount(0);

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      if (!mountedRef.current) return;

      try {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT);

        const response = await fetch(`${serviceConfig.serviceUrl}/health`, {
          signal: controller.signal,
        });
        clearTimeout(timer);

        if (response.ok) {
          const healthData: HealthResponse = await response.json();

          if (!mountedRef.current) return;

          // Check if database is connected
          if (healthData.status === 'healthy' && healthData.databaseDiagnostics?.connected) {
            setData(healthData);
            setStatus('healthy');
            return;
          }

          // Service is up but database is degraded
          setData(healthData);
          setError(
            healthData.databaseDiagnostics?.error
              ?? 'GameService is running but the database is not connected'
          );
          setStatus('failed');
          return;
        }

        // Non-OK response — retry if attempts remain
        if (attempt < MAX_RETRIES) {
          if (!mountedRef.current) return;
          setStatus('retrying');
          setRetryCount(attempt + 1);
          setError(
            response.status === 503
              ? 'GameService is starting up...'
              : `GameService returned HTTP ${response.status}`
          );
          await sleep(RETRY_DELAYS[attempt]);
          continue;
        }

        // All retries exhausted
        if (!mountedRef.current) return;
        setError(`GameService returned HTTP ${response.status} after ${MAX_RETRIES} retries`);
        setStatus('failed');
        return;
      } catch (err) {
        // Network error or timeout
        if (attempt < MAX_RETRIES) {
          if (!mountedRef.current) return;
          setStatus('retrying');
          setRetryCount(attempt + 1);
          setError('GameService is unreachable — retrying...');
          await sleep(RETRY_DELAYS[attempt]);
          continue;
        }

        // All retries exhausted
        if (!mountedRef.current) return;
        setError('GameService is unreachable. Check that the service is running.');
        setStatus('failed');
        return;
      }
    }
  }, []);

  // Run on mount
  useEffect(() => {
    runCheck();
  }, [runCheck]);

  return { status, data, error, retryCount, retry: runCheck };
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
