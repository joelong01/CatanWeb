'use client';

/**
 * WinnerOverlay - Three-phase winner declaration with HexGrid layout.
 *
 * Phase 1 (ready): HexGrid CLUSTER_7 with "Winner!" center hex and player ring.
 * Phase 2 (celebrating): Spinning hex ring with confetti particles.
 * Phase 3 (scoring): Static ring with VP adjusters and "End Game" center button.
 *
 * Uses HexGrid (not polar positioning) to avoid layout bugs from the old
 * WinnerCelebration component. Modeled after SupplementalOverlay.
 */

import { useState, useEffect, useMemo, useCallback } from 'react';
import { HexGrid, type HexGridItem } from '@/components/hex-grid/HexGrid';
import { HEX_LAYOUTS, cubicCoord } from '@/components/hex-grid/hex-geometry';
import { buildCssGradient } from '@/lib/utils/playerColors';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import type { PlayerColors } from '@/types/player-profile';
import { DEFAULT_PLAYER_COLORS } from '@/types/player-profile';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTrophy } from '@fortawesome/free-solid-svg-icons';

// ============================================================================
// Types
// ============================================================================

/** Player data needed for the winner overlay */
export interface WinnerPlayer {
  id: string;
  name: string;
  score: number;
  colors: PlayerColors;
  avatarUrl?: string;
}

/** Internal phase state */
type WinnerPhase = 'ready' | 'celebrating' | 'scoring';

export interface WinnerOverlayProps {
  /** All players in the game */
  players: WinnerPlayer[];
  /** Current player's colors with gradient (for center hex styling) */
  currentPlayerColors: PlayerColorsWithGradient;
  /** Duration of celebration spin in ms (default: 5000) */
  celebrationDurationMs?: number;
  /** Called when "End Game" is clicked with final VP scores */
  onEndGame: (vpScores: Record<string, number>) => void;
}


// ============================================================================
// WinnerCenterHex - Phase 1: "Winner!" button
// ============================================================================

function WinnerCenterHex({
  colors,
  onClick,
}: {
  colors: PlayerColorsWithGradient;
  onClick: () => void;
}): React.ReactElement {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);
  const innerScale = isPressed ? 0.88 : isHovered ? 0.90 : 0.92;

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onClick={onClick}
    >
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 255, 255, 0.3)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center text-center transition-transform duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: colors.cssGradient,
        }}
      >
        <span
          className="text-sm font-bold leading-tight"
          style={{ color: colors.foreground }}
        >
          Winner!
        </span>
        <span
          className="text-xs opacity-70"
          style={{ color: colors.foreground }}
        >
          (Click)
        </span>
      </div>
    </div>
  );
}

// ============================================================================
// CelebrationCenterHex - Phase 2: Trophy icon during spin
// ============================================================================

function CelebrationCenterHex({
  colors,
}: {
  colors: PlayerColorsWithGradient;
}): React.ReactElement {
  return (
    <div className="absolute inset-0">
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 215, 0, 0.4)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-center justify-center"
        style={{
          transform: 'scale(0.92)',
          background: colors.cssGradient,
        }}
      >
        <FontAwesomeIcon
          icon={faTrophy}
          className="text-2xl"
          style={{ color: '#FFD700' }}
        />
      </div>
    </div>
  );
}

// ============================================================================
// EndGameCenterHex - Phase 3: "End Game" button
// ============================================================================

function EndGameCenterHex({
  onClick,
}: {
  onClick: () => void;
}): React.ReactElement {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);
  const innerScale = isPressed ? 0.88 : isHovered ? 0.90 : 0.92;
  const bgColor = isPressed
    ? 'linear-gradient(135deg, #15803d, #14532d)'
    : isHovered
      ? 'linear-gradient(135deg, #4ade80, #22c55e)'
      : 'linear-gradient(135deg, #22c55e, #16a34a)';

  return (
    <div
      className="absolute inset-0 cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onClick={onClick}
    >
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 255, 255, 0.3)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center text-center transition-transform duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: bgColor,
        }}
      >
        <span className="text-white text-sm font-bold leading-tight">
          End Game
        </span>
      </div>
    </div>
  );
}

// ============================================================================
// PlayerDisplayHex - Phases 1 & 2: Avatar + name (non-interactive)
// ============================================================================

function PlayerDisplayHex({
  player,
}: {
  player: WinnerPlayer;
}): React.ReactElement {
  const [imageError, setImageError] = useState(false);
  const colors = player.colors ?? DEFAULT_PLAYER_COLORS;
  const gradient = buildCssGradient(colors);
  const showImage = player.avatarUrl && !imageError;

  return (
    <div className="absolute inset-0">
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 255, 255, 0.3)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-transform duration-150"
        style={{ transform: 'scale(0.92)', background: gradient }}
      >
        {/* Avatar circle */}
        <div
          className="w-10 h-10 rounded-full overflow-hidden border-2"
          style={{ borderColor: colors.foreground }}
        >
          {showImage ? (
            <img
              src={player.avatarUrl}
              alt={player.name}
              className="w-full h-full object-cover"
              onError={() => setImageError(true)}
            />
          ) : (
            <div
              className="w-full h-full flex items-center justify-center text-lg font-bold"
              style={{ background: gradient, color: colors.foreground }}
            >
              {player.name.charAt(0).toUpperCase()}
            </div>
          )}
        </div>
        {/* Player name */}
        <span
          className="text-xs font-medium mt-1 truncate max-w-[90%]"
          style={{ color: colors.foreground }}
        >
          {player.name}
        </span>
      </div>
    </div>
  );
}

// ============================================================================
// PlayerScoringHex - Phase 3: Avatar + VP count + "+"/"-" buttons
// ============================================================================

function PlayerScoringHex({
  player,
  vpScore,
  minScore,
  onIncrement,
  onDecrement,
}: {
  player: WinnerPlayer;
  vpScore: number;
  minScore: number;
  onIncrement: () => void;
  onDecrement: () => void;
}): React.ReactElement {
  const [imageError, setImageError] = useState(false);
  const colors = player.colors ?? DEFAULT_PLAYER_COLORS;
  const gradient = buildCssGradient(colors);
  const showImage = player.avatarUrl && !imageError;
  const canDecrement = vpScore > minScore;

  return (
    <div className="absolute inset-0">
      {/* Border layer */}
      <div
        className="absolute inset-0 hex-clip-flat"
        style={{ background: 'rgba(255, 255, 255, 0.3)' }}
      />
      {/* Content layer */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center"
        style={{ transform: 'scale(0.92)', background: gradient }}
      >
        {/* Avatar circle (smaller) */}
        <div
          className="w-8 h-8 rounded-full overflow-hidden border-2 -mt-1"
          style={{ borderColor: colors.foreground }}
        >
          {showImage ? (
            <img
              src={player.avatarUrl}
              alt={player.name}
              className="w-full h-full object-cover"
              onError={() => setImageError(true)}
            />
          ) : (
            <div
              className="w-full h-full flex items-center justify-center text-sm font-bold"
              style={{ background: gradient, color: colors.foreground }}
            >
              {player.name.charAt(0).toUpperCase()}
            </div>
          )}
        </div>
        {/* VP score + buttons */}
        <div className="flex items-center gap-1 mt-0.5">
          <button
            className="w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold transition-opacity"
            style={{
              background: canDecrement ? 'rgba(0,0,0,0.4)' : 'rgba(0,0,0,0.15)',
              color: canDecrement ? '#fff' : 'rgba(255,255,255,0.3)',
              cursor: canDecrement ? 'pointer' : 'default',
            }}
            onClick={(e) => { e.stopPropagation(); if (canDecrement) onDecrement(); }}
          >
            -
          </button>
          <span
            className="font-black text-lg min-w-[2ch] text-center leading-none"
            style={{ color: colors.foreground, textShadow: '0 1px 3px rgba(0,0,0,0.6)' }}
          >
            {vpScore}
          </span>
          <button
            className="w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold cursor-pointer"
            style={{ background: 'rgba(0,0,0,0.4)', color: '#fff' }}
            onClick={(e) => { e.stopPropagation(); onIncrement(); }}
          >
            +
          </button>
        </div>
        {/* Player name */}
        <span
          className="text-[10px] font-medium truncate max-w-[90%] leading-tight"
          style={{ color: colors.foreground }}
        >
          {player.name}
        </span>
      </div>
    </div>
  );
}

// ============================================================================
// FireworksOverlay - Rockets that shoot up and explode into radial sparks
// ============================================================================

const FIREWORK_COLORS = [
  '#FFD700', '#FF69B4', '#00CED1', '#FF4500',
  '#32CD32', '#DA70D6', '#FF6347', '#1E90FF',
];

interface FireworkData {
  id: number;
  x: number;           // % across container width
  y: number;           // % from top (explosion point)
  rocketTravel: number; // px the rocket travels upward to reach explosion point
  delay: number;        // ms before this firework launches
  rocketDuration: number;
  color: string;
  flashSize: number;    // px diameter of the explosion flash
  sparks: { x: number; y: number; size: number; duration: number }[];
}

function FireworksOverlay({
  durationMs,
}: {
  durationMs: number;
}): React.ReactElement {
  const fireworks = useMemo((): FireworkData[] => {
    const count = 15;
    return Array.from({ length: count }, (_, i) => {
      const x = 15 + Math.random() * 70;              // 15-85% across
      const y = 10 + Math.random() * 40;              // 10-50% from top (upper half)
      const rocketTravel = 100 + Math.random() * 80;   // 100-180px upward
      const delay = (i / count) * (durationMs - 1600); // stagger across duration
      const rocketDuration = 500 + Math.random() * 300;
      const color = FIREWORK_COLORS[i % FIREWORK_COLORS.length];
      const flashSize = 30 + Math.random() * 20;       // 30-50px explosion flash
      const sparkCount = 16 + Math.floor(Math.random() * 8); // 16-23 sparks

      const sparks = Array.from({ length: sparkCount }, (_, j) => {
        const angle = (j * 360 / sparkCount) + (Math.random() * 15 - 7.5);
        const rad = angle * Math.PI / 180;
        const distance = 50 + Math.random() * 80;      // 50-130px burst radius
        return {
          x: Math.cos(rad) * distance,
          y: Math.sin(rad) * distance,
          size: 3 + Math.random() * 5,                  // 3-8px sparks
          duration: 700 + Math.random() * 600,
        };
      });

      return { id: i, x, y, rocketTravel, delay, rocketDuration, color, flashSize, sparks };
    });
  }, [durationMs]);

  return (
    <div className="absolute inset-0 z-10 pointer-events-none overflow-hidden">
      {fireworks.map((fw) => (
        <div
          key={fw.id}
          className="absolute"
          style={{ left: `${fw.x}%`, top: `${fw.y}%` }}
        >
          {/* Rocket: glowing trail that shoots upward to this point */}
          <div
            className="absolute w-1.5 h-5 rounded-full animate-firework-rocket motion-reduce:hidden"
            style={{
              backgroundColor: fw.color,
              boxShadow: `0 0 6px ${fw.color}, 0 0 12px ${fw.color}`,
              marginLeft: -3,
              '--rocket-travel': `${fw.rocketTravel}px`,
              '--rocket-duration': `${fw.rocketDuration}ms`,
              '--rocket-delay': `${fw.delay}ms`,
            } as React.CSSProperties}
          />
          {/* Flash: bright expanding circle at the explosion point */}
          <div
            className="absolute rounded-full animate-firework-flash motion-reduce:hidden"
            style={{
              width: fw.flashSize,
              height: fw.flashSize,
              marginLeft: -fw.flashSize / 2,
              marginTop: -fw.flashSize / 2,
              background: `radial-gradient(circle, white 0%, ${fw.color} 40%, transparent 70%)`,
              boxShadow: `0 0 15px ${fw.color}, 0 0 30px ${fw.color}, 0 0 45px ${fw.color}80`,
              '--flash-duration': '500ms',
              '--flash-delay': `${fw.delay + fw.rocketDuration}ms`,
            } as React.CSSProperties}
          />
          {/* Sparks: burst radially from explosion point */}
          {fw.sparks.map((spark, j) => (
            <div
              key={j}
              className="absolute rounded-full animate-firework-spark motion-reduce:hidden"
              style={{
                width: spark.size,
                height: spark.size,
                backgroundColor: fw.color,
                boxShadow: `0 0 4px ${fw.color}, 0 0 10px ${fw.color}`,
                marginLeft: -spark.size / 2,
                marginTop: -spark.size / 2,
                '--spark-x': `${spark.x}px`,
                '--spark-y': `${spark.y}px`,
                '--spark-duration': `${spark.duration}ms`,
                '--spark-delay': `${fw.delay + fw.rocketDuration}ms`,
              } as React.CSSProperties}
            />
          ))}
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// WinnerOverlay - Main component
// ============================================================================

/**
 * WinnerOverlay component - three-phase winner declaration flow.
 *
 * In Catan, only the current player can win. The current player declares the
 * game over and adjusts all scores to account for hidden VP cards. The game
 * determines the winner from the final scores after submission.
 */
export function WinnerOverlay({
  players,
  currentPlayerColors,
  celebrationDurationMs = 5000,
  onEndGame,
}: WinnerOverlayProps): React.ReactElement {
  const [phase, setPhase] = useState<WinnerPhase>('ready');

  // VP scores: initialized to 0 (represents VP card count, not final score)
  const [vpScores, setVpScores] = useState<Record<string, number>>(() =>
    Object.fromEntries(players.map(p => [p.id, 0]))
  );

  // Floor for decrement is 0 (can't have negative VP cards)
  const minVpCount = 0;

  // Auto-advance from celebrating to scoring after duration
  useEffect(() => {
    if (phase !== 'celebrating') return;
    const timer = setTimeout(() => setPhase('scoring'), celebrationDurationMs);
    return () => clearTimeout(timer);
  }, [phase, celebrationDurationMs]);

  const incrementVP = useCallback((playerId: string) => {
    setVpScores(prev => ({ ...prev, [playerId]: (prev[playerId] || 0) + 1 }));
  }, []);

  const decrementVP = useCallback((playerId: string) => {
    setVpScores(prev => {
      const current = prev[playerId] || 0;
      return { ...prev, [playerId]: Math.max(minVpCount, current - 1) };
    });
  }, []);

  const handleEndGame = useCallback(() => {
    onEndGame(vpScores);
  }, [vpScores, onEndGame]);

  // Calculate spin parameters: exact N rotations for clean alignment
  const spinRotations = Math.max(1, Math.floor(celebrationDurationMs / 1500));

  // CSS custom properties for spin animations (shared by spin + counter-spin)
  const spinVars = useMemo(() => ({
    '--winner-spin-duration': `${celebrationDurationMs / spinRotations}ms`,
    '--winner-spin-count': `${spinRotations}`,
  } as React.CSSProperties), [celebrationDurationMs, spinRotations]);

  // Wrap content in counter-spin so hex faces stay upright while the ring rotates
  const wrapCounterSpin = useCallback((content: React.ReactNode) => (
    <div className="absolute inset-0 animate-winner-counter-spin motion-reduce:animate-none" style={spinVars}>
      {content}
    </div>
  ), [spinVars]);

  // Build hex grid items based on current phase
  const hexItems = useMemo((): HexGridItem[] => {
    const items: HexGridItem[] = [];
    const ringCoords = HEX_LAYOUTS.CLUSTER_7.slice(1);

    // Center hex varies by phase
    if (phase === 'ready') {
      items.push({
        id: 'center',
        coord: cubicCoord(0, 0),
        content: (
          <WinnerCenterHex
            colors={currentPlayerColors}
            onClick={() => setPhase('celebrating')}
          />
        ),
      });
    } else if (phase === 'celebrating') {
      items.push({
        id: 'center',
        coord: cubicCoord(0, 0),
        content: wrapCounterSpin(<CelebrationCenterHex colors={currentPlayerColors} />),
        disabled: true,
      });
    } else {
      items.push({
        id: 'center',
        coord: cubicCoord(0, 0),
        content: <EndGameCenterHex onClick={handleEndGame} />,
      });
    }

    // Player ring hexes
    players.forEach((player, index) => {
      if (index >= ringCoords.length) return;
      const coord = ringCoords[index];

      if (phase === 'scoring') {
        items.push({
          id: player.id,
          coord: cubicCoord(coord.q, coord.r),
          content: (
            <PlayerScoringHex
              player={player}
              vpScore={vpScores[player.id] || 0}
              minScore={minVpCount}
              onIncrement={() => incrementVP(player.id)}
              onDecrement={() => decrementVP(player.id)}
            />
          ),
        });
      } else if (phase === 'celebrating') {
        items.push({
          id: player.id,
          coord: cubicCoord(coord.q, coord.r),
          content: wrapCounterSpin(<PlayerDisplayHex player={player} />),
          disabled: true,
        });
      } else {
        items.push({
          id: player.id,
          coord: cubicCoord(coord.q, coord.r),
          content: <PlayerDisplayHex player={player} />,
          disabled: true,
        });
      }
    });

    return items;
  }, [phase, players, currentPlayerColors, vpScores, incrementVP, decrementVP, handleEndGame, wrapCounterSpin]);

  // Phase title text
  const titleText = phase === 'ready'
    ? 'Click Winner! to celebrate'
    : phase === 'celebrating'
      ? 'Celebrating!'
      : 'How many hidden Victory Point cards does each player have?';

  return (
    <div className="w-full h-full flex flex-col">
      {/* Title */}
      <div className="text-center text-white/80 text-sm mb-2">
        {titleText}
      </div>
      {/* Hex grid with optional spin wrapper */}
      <div className="flex-1 flex items-center justify-center p-2 relative">
        <div
          className={phase === 'celebrating'
            ? 'animate-winner-spin motion-reduce:animate-none w-full h-full'
            : 'w-full h-full'}
          style={phase === 'celebrating' ? spinVars : undefined}
        >
          <HexGrid
            hexSize={50}
            gap={3}
            items={hexItems}
            fitToParent
          />
        </div>
        {/* Fireworks during celebration */}
        {phase === 'celebrating' && <FireworksOverlay durationMs={celebrationDurationMs} />}
      </div>
    </div>
  );
}

export default WinnerOverlay;
