'use client';

import Link from 'next/link';
import { MainLayout } from '@/components/layout';

/**
 * Stats page - displays player statistics and leaderboards.
 * Currently a placeholder while the full implementation is built.
 */
export default function Stats(): React.ReactElement {
  return (
    <MainLayout className="overflow-y-auto">
      <div className="placeholder-page">
        <h1>Stats</h1>
        <div className="todo-badge">To Do</div>
        <p>View player statistics, win rates, and game history.</p>
        <Link href="/" className="back-link">
          ← Back to Home
        </Link>
      </div>
    </MainLayout>
  );
}
