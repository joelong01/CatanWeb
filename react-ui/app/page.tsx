'use client';

import { useState, useCallback } from 'react';
import {
  faGamepad,
  faFolderOpen,
  faUsers,
  faChartBar,
  faPlay,
  faDice,
  faFlask,
  faWrench,
  faSpinner,
  faSlidersH,
  faCode,
  faFont,
} from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import { getServiceUrl } from '@/lib/config';
import {
  HexGrid,
  HexGridItem,
  CenterHex,
  MenuHex,
  cubicCoord,
} from '@/components/hex-grid';

interface TroubleshootResult {
  timestamp: string;
  isAzure: boolean;
  serverFqdn: string | null;
  databaseName: string | null;
  message: string | null;
  connectionSuccessful: boolean;
  checks: string[];
  issues: string[];
  fixed: string[];
  cannotFix: string[];
}

/**
 * Home page - main entry point for the Catan application.
 *
 * Uses two hex clusters:
 *   Top cluster (Game): Catan branding + New Game, Open Game, Edit Players, Stats
 *   Bottom cluster (Dev): Dev center + Hex Test, Troubleshoot, Controls Test, Font Viewer
 */
export default function Home(): React.ReactElement {
  // TODO: Get active game ID from connection service/store
  const activeGameId: string | null = null;

  // Troubleshoot state
  const [isTroubleshooting, setIsTroubleshooting] = useState(false);
  const [troubleshootResult, setTroubleshootResult] = useState<TroubleshootResult | null>(null);
  const [troubleshootError, setTroubleshootError] = useState<string | null>(null);

  const runTroubleshoot = useCallback(async () => {
    setIsTroubleshooting(true);
    setTroubleshootResult(null);
    setTroubleshootError(null);

    try {
      const response = await fetch(`${getServiceUrl()}/api/troubleshoot`, {
        method: 'POST',
      });

      if (response.ok) {
        const result = await response.json();
        setTroubleshootResult(result);
      } else {
        setTroubleshootError(`HTTP ${response.status}: ${response.statusText}`);
      }
    } catch (err) {
      setTroubleshootError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsTroubleshooting(false);
    }
  }, []);

  const dismissTroubleshoot = useCallback(() => {
    setTroubleshootResult(null);
    setTroubleshootError(null);
  }, []);

  // ── Game cluster (top): center + 4 surrounding ──
  // Diamond shape: NW, N, Center, NE, S
  const gameItems: HexGridItem[] = [
    {
      id: 'game-center',
      coord: cubicCoord(0, 0),
      content: (
        <CenterHex
          icon={faDice}
          title="Catan"
          accentColor="text-amber-400"
        />
      ),
      disabled: true,
    },
    {
      id: 'nw-slot',
      coord: cubicCoord(-1, 0),
      content: activeGameId ? (
        <MenuHex
          icon={faPlay}
          title="Return to"
          subtitle="Game"
          href={`/game/${activeGameId}`}
          accentColor="text-green-400"
        />
      ) : (
        <MenuHex
          icon={faUsers}
          title="Edit Players"
          href="/edit-players"
          accentColor="text-green-400"
        />
      ),
    },
    {
      id: 'new-game',
      coord: cubicCoord(0, -1),
      content: (
        <MenuHex
          icon={faGamepad}
          title="New Game"
          href="/new-game"
          accentColor="text-amber-400"
        />
      ),
    },
    {
      id: 'open-game',
      coord: cubicCoord(1, -1),
      content: (
        <MenuHex
          icon={faFolderOpen}
          title="Open Game"
          href="/load-game"
          accentColor="text-blue-400"
        />
      ),
    },
    {
      id: 'stats',
      coord: cubicCoord(0, 1),
      content: (
        <MenuHex
          icon={faChartBar}
          title="Stats"
          href="/stats"
          accentColor="text-purple-400"
        />
      ),
    },
  ];

  // ── Dev cluster (bottom): same diamond shape, own coordinate space ──
  const devItems: HexGridItem[] = [
    {
      id: 'dev-center',
      coord: cubicCoord(0, 0),
      content: (
        <CenterHex
          icon={faCode}
          title="Dev"
          accentColor="text-cyan-400"
        />
      ),
      disabled: true,
    },
    {
      id: 'hex-test',
      coord: cubicCoord(-1, 0),
      content: (
        <MenuHex
          icon={faFlask}
          title="Hex Test"
          href="/hex-test"
          accentColor="text-cyan-400"
        />
      ),
    },
    {
      id: 'troubleshoot',
      coord: cubicCoord(0, -1),
      content: (
        <MenuHex
          icon={isTroubleshooting ? faSpinner : faWrench}
          title={isTroubleshooting ? 'Running...' : 'Troubleshoot'}
          onClick={isTroubleshooting ? undefined : runTroubleshoot}
          accentColor="text-gray-400"
        />
      ),
      disabled: isTroubleshooting,
    },
    {
      id: 'controls-test',
      coord: cubicCoord(1, -1),
      content: (
        <MenuHex
          icon={faSlidersH}
          title="Controls"
          subtitle="Test"
          href="/controls-test"
          accentColor="text-orange-400"
        />
      ),
    },
    {
      id: 'font-viewer',
      coord: cubicCoord(0, 1),
      content: (
        <MenuHex
          icon={faFont}
          title="Font Viewer"
          href="/font-viewer"
          accentColor="text-emerald-400"
        />
      ),
    },
  ];

  return (
    <MainLayout activeGameId={activeGameId}>
      <div className="flex flex-col items-center justify-center min-h-[calc(100vh-120px)] pt-[60px] pb-8">
        {/* Game Cluster */}
        <div className="bg-white/5 rounded-xl p-8 border border-white/10">
          <HexGrid hexSize={140} items={gameItems} gap={4} />
        </div>

        {/* Separator */}
        <div className="w-48 my-6 border-t border-white/10" />

        {/* Dev Cluster (20% smaller) */}
        <div className="bg-white/5 rounded-xl p-6 border border-white/10">
          <HexGrid hexSize={112} items={devItems} gap={3} />
        </div>

        {/* Troubleshoot Results */}
        {troubleshootResult && (
          <div
            className={`mt-6 p-5 rounded-lg text-left max-w-md ${
              troubleshootResult.connectionSuccessful
                ? 'bg-green-900/50 border border-green-700'
                : 'bg-yellow-900/50 border border-yellow-700'
            }`}
          >
            <h3 className="font-bold text-lg mb-3 text-white">{troubleshootResult.message}</h3>

            {troubleshootResult.checks.length > 0 && (
              <div className="mb-3">
                <strong className="text-gray-300">Checks:</strong>
                <ul className="list-disc list-inside text-sm text-gray-400 mt-1">
                  {troubleshootResult.checks.map((check, i) => (
                    <li key={i}>{check}</li>
                  ))}
                </ul>
              </div>
            )}

            {troubleshootResult.fixed.length > 0 && (
              <div className="mb-3">
                <strong className="text-green-400">Fixed:</strong>
                <ul className="list-disc list-inside text-sm text-green-300 mt-1">
                  {troubleshootResult.fixed.map((fix, i) => (
                    <li key={i}>{fix}</li>
                  ))}
                </ul>
              </div>
            )}

            {troubleshootResult.issues.length > 0 && (
              <div className="mb-3">
                <strong className="text-yellow-400">Issues:</strong>
                <ul className="list-disc list-inside text-sm text-yellow-300 mt-1">
                  {troubleshootResult.issues.map((issue, i) => (
                    <li key={i}>{issue}</li>
                  ))}
                </ul>
              </div>
            )}

            {troubleshootResult.cannotFix.length > 0 && (
              <div className="mb-3">
                <strong className="text-blue-400">Cannot Auto-Fix:</strong>
                <ul className="list-disc list-inside text-sm text-blue-300 mt-1">
                  {troubleshootResult.cannotFix.map((item, i) => (
                    <li key={i}>{item}</li>
                  ))}
                </ul>
              </div>
            )}

            <button
              onClick={dismissTroubleshoot}
              className="mt-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-sm text-white transition-colors"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Troubleshoot Error */}
        {troubleshootError && (
          <div className="mt-6 p-5 rounded-lg text-left max-w-md bg-red-900/50 border border-red-700">
            <h3 className="font-bold text-lg mb-2 text-white">Troubleshoot Failed</h3>
            <p className="text-sm text-red-300">{troubleshootError}</p>
            <button
              onClick={dismissTroubleshoot}
              className="mt-3 px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-sm text-white transition-colors"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Service Info */}
        <div className="mt-8 px-4 py-3 bg-gray-800/50 rounded-lg text-center">
          <p className="text-sm text-gray-400">
            GameService: <code className="text-blue-400">{getServiceUrl()}</code>
          </p>
        </div>
      </div>
    </MainLayout>
  );
}
