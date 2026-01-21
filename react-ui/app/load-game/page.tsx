'use client';

import Link from 'next/link';
import { MainLayout } from '@/components/layout';

/**
 * Load Game page - allows users to browse and load saved games.
 * Currently a placeholder while the full implementation is built.
 */
export default function LoadGame(): React.ReactElement {
  return (
    <MainLayout>
      <div className="placeholder-page">
        <h1>Load Game</h1>
        <div className="todo-badge">To Do</div>
        <p>Browse saved games and continue where you left off.</p>
        <Link href="/" className="back-link">
          ← Back to Home
        </Link>
      </div>
    </MainLayout>
  );
}
