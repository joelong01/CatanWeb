'use client';

import Link from 'next/link';
import { MainLayout } from '@/components/layout';

/**
 * Settings page - allows users to configure game and app preferences.
 * Currently a placeholder while the full implementation is built.
 */
export default function Settings(): React.ReactElement {
  return (
    <MainLayout>
      <div className="placeholder-page">
        <h1>Settings</h1>
        <div className="todo-badge">To Do</div>
        <p>Configure house rules, sound settings, and app preferences.</p>
        <Link href="/" className="back-link">
          ← Back to Home
        </Link>
      </div>
    </MainLayout>
  );
}
