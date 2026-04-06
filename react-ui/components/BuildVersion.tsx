/**
 * @module BuildVersion
 *
 * Compact build marker — shows PR number as a clickable link to GitHub.
 * Designed to be minimal on mobile (just "PR xxx") and not hide UI controls.
 *
 * In dev mode (no PR number), shows commit hash instead.
 */

'use client';

import React from 'react';

const REPO_URL = 'https://github.com/joelong01/CatanWeb';

export default function BuildVersion(): React.ReactElement {
  const pr = process.env.NEXT_PUBLIC_BUILD_PR;
  const commit = process.env.NEXT_PUBLIC_BUILD_COMMIT;
  const branch = process.env.NEXT_PUBLIC_BUILD_BRANCH;
  const time = process.env.NEXT_PUBLIC_BUILD_TIME;

  // Build tooltip with full details
  const tooltip = [branch, time, commit].filter((s) => s && s !== 'unknown').join(' · ');

  if (pr) {
    return (
      <a
        href={`${REPO_URL}/pull/${pr}`}
        target="_blank"
        rel="noopener noreferrer"
        className="fixed bottom-1 right-1 text-xs text-white font-mono bg-black/80 px-2 py-1 rounded z-[9999] pointer-events-auto no-underline hover:text-blue-300"
        title={tooltip}
      >
        PR {pr}
      </a>
    );
  }

  // No PR — local dev mode
  return (
    <div
      className="fixed bottom-1 right-1 text-xs text-white font-mono bg-black/80 px-2 py-1 rounded z-[9999] pointer-events-auto"
      title={tooltip}
    >
      dev
    </div>
  );
}
