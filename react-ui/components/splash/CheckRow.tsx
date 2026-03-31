/**
 * @module splash/CheckRow
 *
 * Displays a single health check result as a row in the splash overlay.
 * Each row shows an icon, label, timing, and detail/error message.
 */

'use client';

import React from 'react';

export type CheckRowStatus = 'pending' | 'running' | 'ok' | 'warning' | 'error' | 'fixing';

export interface CheckRowProps {
  /** Display label for this check (e.g., "Game Service", "Database"). */
  label: string;
  /** Current status of the check. */
  status: CheckRowStatus;
  /** Duration in milliseconds (shown for terminal states). */
  durationMs?: number;
  /** Detail text (e.g., "10 players, 4 templates"). */
  detail?: string;
  /** Error message (shown below the row when status is 'error'). */
  error?: string;
}

/** Status icon and color mapping. */
const STATUS_CONFIG: Record<CheckRowStatus, { icon: string; color: string }> = {
  pending: { icon: '\u25CB', color: 'text-gray-500' }, // ○
  running: { icon: '', color: 'text-blue-400' }, // spinner
  ok: { icon: '\u2705', color: 'text-green-400' }, // ✅
  warning: { icon: '\u26A0\uFE0F', color: 'text-yellow-400' }, // ⚠️
  error: { icon: '\u274C', color: 'text-red-400' }, // ❌
  fixing: { icon: '\uD83D\uDD27', color: 'text-amber-400' }, // 🔧
};

export default function CheckRow({ label, status, durationMs, detail, error }: CheckRowProps) {
  const { icon, color } = STATUS_CONFIG[status];

  return (
    <div className="py-1.5">
      <div className="flex items-center gap-3 font-mono text-sm">
        {/* Status icon */}
        <span className={`w-6 text-center ${color}`}>
          {status === 'running' || status === 'fixing' ? (
            <span className="inline-block animate-spin">{'\u25E0'}</span>
          ) : (
            icon
          )}
        </span>

        {/* Label */}
        <span className="w-36 text-gray-300">{label}</span>

        {/* Duration */}
        <span className="w-16 text-right text-gray-500 text-xs">
          {durationMs != null ? `${durationMs}ms` : ''}
        </span>

        {/* Detail */}
        <span className={`flex-1 text-xs ${color}`}>{detail ?? ''}</span>
      </div>

      {/* Error message (expanded below the row) */}
      {error && status === 'error' && (
        <div className="ml-9 mt-1 text-xs text-red-400/80 font-mono">{error}</div>
      )}
    </div>
  );
}
