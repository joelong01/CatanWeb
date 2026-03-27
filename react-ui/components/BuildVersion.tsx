/**
 * @module BuildVersion
 *
 * Subtle build version display — shows branch, date, and commit hash
 * so users (and developers) can verify what's actually running.
 *
 * Helps diagnose browser caching issues (issue #68) — if the version
 * string doesn't match the latest deployment, the browser has stale code.
 *
 * Format: "staging-Mar27-9:22-f94c7d4"
 */

'use client';

import React from 'react';

/**
 * Formats the build version string from environment variables.
 * Returns null if no build info is available.
 */
function getBuildVersion(): string | null {
  const branch = process.env.NEXT_PUBLIC_BUILD_BRANCH;
  const time = process.env.NEXT_PUBLIC_BUILD_TIME;
  const commit = process.env.NEXT_PUBLIC_BUILD_COMMIT;

  if (!commit || commit === 'unknown') return null;

  const parts: string[] = [];
  if (branch && branch !== 'unknown') parts.push(branch);
  if (time) parts.push(time);
  parts.push(commit);

  return parts.join('-');
}

/**
 * Renders a subtle version string in the bottom-right corner of the viewport.
 * Fixed position, low opacity, small text — visible but not distracting.
 */
export default function BuildVersion(): React.ReactElement | null {
  const version = getBuildVersion();
  if (!version) return null;

  return (
    <div
      className="fixed bottom-1 right-2 text-[10px] text-gray-600 font-mono opacity-50 hover:opacity-100 transition-opacity select-all z-40"
      title={`Build: ${version}`}
    >
      {version}
    </div>
  );
}
