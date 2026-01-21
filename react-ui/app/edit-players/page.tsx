'use client';

import Link from 'next/link';
import { MainLayout } from '@/components/layout';

/**
 * Edit Players page - allows users to manage player profiles.
 * Currently a placeholder while the full implementation is built.
 */
export default function EditPlayers(): React.ReactElement {
  return (
    <MainLayout>
      <div className="placeholder-page">
        <h1>Edit Players</h1>
        <div className="todo-badge">To Do</div>
        <p>Add, edit, or remove player profiles and customize their colors.</p>
        <Link href="/" className="back-link">
          ← Back to Home
        </Link>
      </div>
    </MainLayout>
  );
}
