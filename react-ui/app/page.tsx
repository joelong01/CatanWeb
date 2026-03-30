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
import { useHealthCheck } from '@/lib/hooks/useHealthCheck';
import SplashOverlay from '@/components/splash/SplashOverlay';
import {
  HexGrid,
  CenterHex,
  MenuHex,
  HEX_CONTENT_SCALE,
  getSpiralCoordinates,
} from '@/components/hex-grid';

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

  // Phase 1: health check — overlay appears only on failure
  const health = useHealthCheck();

  // Responsive scale: shrink hex grids on narrow viewports so they fit.
  // Game grid at hexSize=140 is ~700px wide; with p-8 wrapper padding (64px) = 764px total.
  const [hexScale, setHexScale] = useState(1);
  // Clear stale active-game pointer when user returns to home
  useEffect(() => {
    localStorage.removeItem('current_gameId');
  }, []);

  useEffect(() => {
    const update = (): void => {
      const available = window.innerWidth - 32; // 16px margin each side
      setHexScale(Math.min(1, available / 764));
    };
    update();
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

  // Troubleshoot: show splash overlay with current health state (or re-check)
  const [showSplash, setShowSplash] = useState(false);

  const runTroubleshoot = useCallback(() => {
    setShowSplash(true);
    // If the health check already completed, just show the result.
    // If it hasn't run yet or failed, re-run it.
    if (health.status !== 'healthy') {
      health.retry();
    }
  }, [health.status, health.retry]);

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
      icon={faWrench}
      title="Troubleshoot"
      onClick={runTroubleshoot}
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

        {/* Service Info */}
        <div className="mt-8 px-4 py-3 bg-gray-800/50 rounded-lg text-center">
          <p className="text-sm text-gray-400">
            GameService: <code className="text-blue-400">{getServiceUrl()}</code>
          </p>
        </div>
      </div>

      {/* Splash overlay — shown on health check failure OR when user clicks Troubleshoot.
          NOT shown during initial 'checking' — home page renders immediately. */}
      {(health.status === 'failed' || health.status === 'retrying' || showSplash) && (
        <SplashOverlay
          health={health}
          onRetry={health.retry}
          onDismiss={health.status === 'healthy' ? () => setShowSplash(false) : undefined}
        />
      )}
    </MainLayout>
  );
}
