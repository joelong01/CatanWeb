'use client';

import { useState, useCallback, useEffect } from 'react';
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
  faVial,
  faMap,
  faCrown,
  faShip,
} from '@fortawesome/free-solid-svg-icons';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { MainLayout } from '@/components/layout';
import { getServiceUrl } from '@/lib/config';
import {
  HexGrid,
  CenterHex,
  MenuHex,
  HEX_CONTENT_SCALE,
  getSpiralCoordinates,
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

/** Disabled hex with a diagonal "Coming Soon" banner. */
function ComingSoonHex({
  icon,
  title,
  accentColor,
}: {
  icon: IconDefinition;
  title: string;
  accentColor: string;
}): React.ReactElement {
  return (
    <div className="w-full h-full opacity-60 cursor-not-allowed">
      {/* Outer hex - border */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'var(--hex-border-idle)' }}
      />
      {/* Inner hex - content */}
      <div
        className="absolute inset-0 flex items-center justify-center hex-clip-flat"
        style={{
          background: 'var(--hex-content-gradient)',
          transform: `scale(${HEX_CONTENT_SCALE})`,
        }}
      >
        <div className="text-center px-4">
          <FontAwesomeIcon icon={icon} className={`${accentColor} text-5xl mb-2`} />
          <h3 className={`text-xl font-bold ${accentColor} tracking-wide leading-tight`}>
            {title}
          </h3>
        </div>
      </div>
      {/* Coming Soon banner */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none z-10 hex-clip-flat">
        <span
          className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 -rotate-[35deg]
            font-bold text-xs uppercase tracking-widest py-2 px-12 whitespace-nowrap
            shadow-lg text-black bg-amber-500"
        >
          Coming Soon
        </span>
      </div>
    </div>
  );
}

/**
 * Home page - main entry point for the Catan application.
 *
 * Uses two hex clusters:
 *   Top cluster (Game): Catan branding + New Game, Open Game, Edit Players, Stats, Coming Soon ×2
 *   Bottom cluster (Dev): Dev center + Hex Test, Troubleshoot, Controls Test, Font Viewer
 */
export default function Home(): React.ReactElement {
  // TODO: Get active game ID from connection service/store
  const activeGameId: string | null = null;

  // Responsive scale: shrink hex grids on narrow viewports so they fit.
  // Game grid at hexSize=140 is ~700px wide; with p-8 wrapper padding (64px) = 764px total.
  const [hexScale, setHexScale] = useState(1);
  useEffect(() => {
    const update = (): void => {
      const available = window.innerWidth - 32; // 16px margin each side
      setHexScale(Math.min(1, available / 764));
    };
    update();
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

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

  // ── Game cluster (top): center + 6 surrounding (full ring) ──
  // Spiral order: center, top, top-right, bottom-right, bottom, bottom-left, top-left
  const gameContent = [
    <CenterHex key="c" icon={faDice} title="Catan" accentColor="text-amber-400" />,
    <MenuHex
      key="new"
      icon={faGamepad}
      title="New Game"
      href="/new-game"
      accentColor="text-amber-400"
    />,
    <MenuHex
      key="open"
      icon={faFolderOpen}
      title="Open Game"
      href="/load-game"
      accentColor="text-blue-400"
    />,
    activeGameId ? (
      <MenuHex
        key="active"
        icon={faPlay}
        title="Return to"
        subtitle="Game"
        href={`/game/${activeGameId}`}
        accentColor="text-green-400"
      />
    ) : (
      <MenuHex
        key="players"
        icon={faUsers}
        title="Edit Players"
        href="/edit-players"
        accentColor="text-green-400"
      />
    ),
    <MenuHex
      key="stats"
      icon={faChartBar}
      title="Stats"
      href="/stats"
      accentColor="text-purple-400"
    />,
    <ComingSoonHex key="seafarers" icon={faShip} title="Seafarers" accentColor="text-sky-400" />,
    <ComingSoonHex
      key="cities"
      icon={faCrown}
      title="Cities &amp;"
      accentColor="text-violet-400"
    />,
  ];

  // ── Dev cluster (bottom): center + 6 surrounding ──
  // Spiral order: center, top, top-right, right, bottom, bottom-left, left
  const devContent = [
    <CenterHex key="c" icon={faCode} title="Dev" accentColor="text-cyan-400" />,
    <MenuHex
      key="trouble"
      icon={isTroubleshooting ? faSpinner : faWrench}
      title={isTroubleshooting ? 'Running...' : 'Troubleshoot'}
      onClick={isTroubleshooting ? undefined : runTroubleshoot}
      accentColor="text-gray-400"
    />,
    <MenuHex
      key="controls"
      icon={faSlidersH}
      title="Controls"
      subtitle="Test"
      href="/controls-test"
      accentColor="text-orange-400"
    />,
    <MenuHex
      key="editor"
      icon={faMap}
      title="Board Editor"
      href="/templates"
      accentColor="text-teal-400"
    />,
    <MenuHex
      key="font"
      icon={faFont}
      title="Font Viewer"
      href="/font-viewer"
      accentColor="text-emerald-400"
    />,
    <MenuHex key="tests" icon={faVial} title="Tests" href="/tests" accentColor="text-red-400" />,
    <MenuHex
      key="hex"
      icon={faFlask}
      title="Hex Test"
      href="/hex-test"
      accentColor="text-cyan-400"
    />,
  ];

  return (
    <MainLayout activeGameId={activeGameId} hideHeader>
      <div className="flex flex-col items-center justify-center min-h-full pb-8">
        {/* Game Cluster */}
        <div className="bg-white/5 rounded-xl p-8 border border-white/10">
          <HexGrid
            hexSize={140}
            coordinates={getSpiralCoordinates(gameContent.length)}
            renderItem={(_coord, i) => gameContent[i]}
            gap={4}
            scale={hexScale}
          />
        </div>

        {/* Separator */}
        <div className="w-48 my-6 border-t border-white/10" />

        {/* Dev Cluster (20% smaller) */}
        <div className="bg-white/5 rounded-xl p-6 border border-white/10">
          <HexGrid
            hexSize={112}
            coordinates={getSpiralCoordinates(devContent.length)}
            renderItem={(_coord, i) => devContent[i]}
            gap={3}
            scale={hexScale}
          />
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
