/**
 * @module splash/SplashOverlay
 *
 * Full-screen overlay shown when the Phase 1 health check fails.
 * Displays check results, retry controls, and (when Azure is configured)
 * a sign-in button to trigger Phase 2/3 infrastructure repair.
 *
 * On healthy systems, this component is never rendered — the home page
 * appears immediately with no delay.
 */

'use client';

import React from 'react';
import type { HealthResult } from '@/lib/hooks/useHealthCheck';
import CheckRow from './CheckRow';
import type { CheckRowStatus } from './CheckRow';

interface SplashOverlayProps {
  /** Health check result from useHealthCheck(). */
  health: HealthResult;
  /** Callback to re-run the health check. */
  onRetry: () => void;
  /** Callback to dismiss the overlay (shown when health is ok). */
  onDismiss?: () => void;
}

/**
 * Maps the health check result into individual check rows for display.
 * Phase 1 is a single /health call, but we break it down for the user.
 */
function getCheckRows(health: HealthResult): Array<{
  label: string;
  status: CheckRowStatus;
  detail?: string;
  durationMs?: number;
  error?: string;
}> {
  const { status, data, error } = health;

  // Still checking — show running state
  if (status === 'checking' || status === 'retrying') {
    const retryDetail = status === 'retrying'
      ? `Attempt ${health.retryCount + 1}... ${error ?? ''}`
      : 'Connecting...';

    return [
      { label: 'Game Service', status: 'running', detail: retryDetail },
      { label: 'Database', status: 'pending' },
      { label: 'Players', status: 'pending' },
      { label: 'Templates', status: 'pending' },
    ];
  }

  // Health check succeeded — show green results
  if (status === 'healthy' && data) {
    const db = data.databaseDiagnostics;
    return [
      { label: 'Game Service', status: 'ok', detail: `${data.version.environment} (${data.version.commit})` },
      { label: 'Database', status: db.connected ? 'ok' : 'error', detail: db.connected ? 'Connected' : 'Disconnected', error: db.error },
      { label: 'Players', status: db.playerCount > 0 ? 'ok' : 'warning', detail: `${db.playerCount} players` },
      { label: 'Templates', status: db.templateCount > 0 ? 'ok' : 'warning', detail: `${db.templateCount} templates` },
      { label: 'Games', status: 'ok', detail: `${db.gameCount} saved` },
      { label: 'Recordings', status: 'ok', detail: `${db.recordingCount} saved` },
    ];
  }

  // Health check failed
  if (data) {
    // Partial data (service up but DB down)
    const db = data.databaseDiagnostics;
    return [
      { label: 'Game Service', status: 'ok', detail: `${data.version.environment} (${data.version.commit})` },
      { label: 'Database', status: 'error', detail: 'Connection failed', error: db?.error ?? error },
      { label: 'Players', status: 'pending' },
      { label: 'Templates', status: 'pending' },
    ];
  }

  // No data at all (service completely unreachable)
  return [
    { label: 'Game Service', status: 'error', detail: 'Unreachable', error },
    { label: 'Database', status: 'pending' },
    { label: 'Players', status: 'pending' },
    { label: 'Templates', status: 'pending' },
  ];
}

export default function SplashOverlay({ health, onRetry, onDismiss }: SplashOverlayProps) {
  const rows = getCheckRows(health);
  const isChecking = health.status === 'checking' || health.status === 'retrying';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 backdrop-blur-sm">
      <div className="w-full max-w-lg mx-4">
        {/* Catan branding */}
        <div className="text-center mb-8">
          <h1
            className="text-5xl text-amber-400 tracking-wide"
            style={{ fontFamily: 'var(--font-catan)' }}
          >
            Catan
          </h1>
          <p className="text-gray-400 text-sm mt-2">
            {isChecking ? 'Checking services...' : 'Service issue detected'}
          </p>
        </div>

        {/* Check results */}
        <div className="bg-gray-900/80 rounded-lg border border-gray-700 px-4 py-3 mb-6">
          {rows.map((row) => (
            <CheckRow
              key={row.label}
              label={row.label}
              status={row.status}
              durationMs={row.durationMs}
              detail={row.detail}
              error={row.error}
            />
          ))}
        </div>

        {/* Actions — context-dependent */}
        {health.status === 'healthy' && onDismiss && (
          <div className="flex flex-col items-center gap-3">
            <button
              onClick={onDismiss}
              className="px-6 py-2 bg-green-600 hover:bg-green-500 text-white rounded-md font-medium transition-colors"
            >
              All Good — Continue
            </button>
          </div>
        )}

        {health.status === 'failed' && (
          <div className="flex flex-col items-center gap-3">
            <button
              onClick={onRetry}
              className="px-6 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-md font-medium transition-colors"
            >
              Retry
            </button>
            <p className="text-gray-500 text-xs text-center">
              If this persists, run <code className="text-gray-400">./catan.ps1 doctor</code> for help.
            </p>
          </div>
        )}

        {health.status === 'retrying' && (
          <div className="text-center">
            <p className="text-gray-400 text-sm">
              Retry {health.retryCount} of {3}...
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
