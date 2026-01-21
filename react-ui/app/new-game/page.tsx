'use client';

import Link from 'next/link';
import { MainLayout } from '@/components/layout';

/**
 * New Game page - allows users to configure and start a new game.
 * Currently a placeholder while the full implementation is built.
 */
export default function NewGame(): React.ReactElement {
  return (
    <MainLayout>
      <div className="placeholder-page">
        <h1>New Game</h1>
        <div className="todo-badge">To Do</div>
        <p>Configure game type, select players, and start a new game.</p>
        <Link href="/" className="back-link">
          ← Back to Home
        </Link>
      </div>
    </MainLayout>
  );
}
