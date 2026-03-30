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
 * Always returns something — "dev" if no build info is available.
 */
function getBuildVersion(): string {
  const branch = process.env.NEXT_PUBLIC_BUILD_BRANCH;
  const time = process.env.NEXT_PUBLIC_BUILD_TIME;
  const commit = process.env.NEXT_PUBLIC_BUILD_COMMIT;

  if (!commit || commit === 'unknown') return 'dev';

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
export default function BuildVersion(): React.ReactElement {
  const version = getBuildVersion();

  return (
    <div
      className="fixed bottom-2 right-2 text-sm text-yellow-400 font-mono bg-black/80 px-2 py-1 rounded select-all z-[9999] pointer-events-auto"
      title={`Build: ${version}`}
    >
      {version}
    </div>
  );
}
