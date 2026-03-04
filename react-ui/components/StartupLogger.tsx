'use client';

import { useEffect, useState } from 'react';
import { getServiceUrl } from '@/lib/config';

interface LogEntry {
  time: number;
  message: string;
  isError?: boolean;
}

/**
 * Startup connectivity logger.
 *
 * Always logs to console with [Loading Xs] prefix (matching Blazor pattern).
 * Shows a visible error overlay only when GameService connectivity fails.
 * On success the component renders nothing.
 */
export function StartupLogger() {
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    const startTime = Date.now();
    const logs: LogEntry[] = [];

    function elapsed(): number {
      return (Date.now() - startTime) / 1000;
    }

    function log(message: string, isError = false) {
      const t = elapsed();
      const prefix = `[Loading ${t.toFixed(1)}s]`;
      if (isError) {
        console.error(`${prefix} ${message}`);
      } else {
        console.log(`${prefix} ${message}`);
      }
      logs.push({ time: t, message, isError });
      // Update state with a copy so React re-renders
      setEntries([...logs]);
      if (isError) setHasError(true);
    }

    async function checkHealth() {
      const serviceUrl = getServiceUrl();
      log('Page loaded');
      log(`GameService URL: ${serviceUrl}`);
      log('Checking server health...');

      try {
        const response = await fetch(`${serviceUrl}/health`, {
          signal: AbortSignal.timeout(30000),
        });

        if (!response.ok) {
          log(`Health check failed: HTTP ${response.status}`, true);
          return;
        }

        log(`Health: HTTP ${response.status}`);

        try {
          const data = await response.json();
          if (data?.database?.canConnect !== undefined) {
            const dbStatus = data.database.canConnect ? 'connected' : 'disconnected';
            log(`Database: ${dbStatus}`, !data.database.canConnect);
          }
        } catch {
          // Health endpoint may not return JSON — that's fine
        }

        log('Ready');
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        log(`Health check failed: ${message}`, true);
      }
    }

    checkHealth();
  }, []);

  // Render nothing when healthy
  if (!hasError) return null;

  return (
    <div className="startup-log-panel">
      <div className="startup-log-header">Startup Log</div>
      <div className="startup-log-entries">
        {entries.map((entry, i) => (
          <div key={i} className={entry.isError ? 'startup-log-error' : 'startup-log-info'}>
            [{entry.time.toFixed(1)}s] {entry.message}
          </div>
        ))}
      </div>
    </div>
  );
}
