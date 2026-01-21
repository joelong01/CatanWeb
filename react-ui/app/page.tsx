'use client';

import Link from 'next/link';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faGamepad,
  faFolderOpen,
  faUsers,
  faChartBar,
  faPlay,
} from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import { getServiceUrl } from '@/lib/config';

/**
 * Home page - main entry point for the Catan application.
 * Provides navigation to create new games, load saved games, edit players, and view stats.
 */
export default function Home(): React.ReactElement {
  // TODO: Get active game ID from connection service/store
  const activeGameId: string | null = null;

  return (
    <MainLayout activeGameId={activeGameId}>
      <div className="home-container">
        <h1>Catan</h1>
        <p className="home-subtitle">Settlers of Catan - Web Edition</p>

        <div className="menu-options">
          {/* Return to Game - only shown if there's an active game */}
          {activeGameId && (
            <Link href={`/game/${activeGameId}`} className="menu-button return-game">
              <span className="icon">
                <FontAwesomeIcon icon={faPlay} />
              </span>
              <span className="text">Return to Game</span>
            </Link>
          )}

          <Link href="/new-game" className="menu-button">
            <span className="icon">
              <FontAwesomeIcon icon={faGamepad} />
            </span>
            <span className="text">New Game</span>
          </Link>

          <Link href="/load-game" className="menu-button">
            <span className="icon">
              <FontAwesomeIcon icon={faFolderOpen} />
            </span>
            <span className="text">Open Game</span>
          </Link>

          <Link href="/edit-players" className="menu-button">
            <span className="icon">
              <FontAwesomeIcon icon={faUsers} />
            </span>
            <span className="text">Edit Players</span>
          </Link>

          <Link href="/stats" className="menu-button">
            <span className="icon">
              <FontAwesomeIcon icon={faChartBar} />
            </span>
            <span className="text">Stats</span>
          </Link>
        </div>

        <div className="info-section">
          <p>
            React UI connected to GameService at <code>{getServiceUrl()}</code>
          </p>
        </div>
      </div>
    </MainLayout>
  );
}
