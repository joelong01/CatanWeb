'use client';

/**
 * VictoryPointsOverlay - FloatingPanel with flat-top hex grid for VP adjustment.
 *
 * Features:
 * - Uses FloatingPanel component
 * - Flat-top hexagons (rotated 30°)
 * - Avatar images instead of names
 * - Face-up flip animation
 * - Ring formation
 */

import { memo, useState, useCallback } from 'react';
import { motion } from 'framer-motion';
import type { PlayerColors } from '@/types/player-profile';

export interface PlayerVPInfo {
  id: string;
  name: string;
  colors: PlayerColors;
  victoryPoints: number;
  avatarUrl?: string;
}

export interface VictoryPointsOverlayProps {
  /** All players with their current VP counts */
  players: PlayerVPInfo[];
  /** Callback when done - provides updated VP values */
  onDone: (victoryPoints: Record<string, number>) => void;
}

/**
 * Flat-top hexagon polygon points (rotated 30° from pointy-top)
 * Points start at top-left and go clockwise
 */
const FLAT_TOP_HEX_POINTS = "25,5 75,5 100,50 75,95 25,95 0,50";

/**
 * Flat-top hexagonal player VP adjuster with avatar
 */
function PlayerVPHex({
  player,
  victoryPoints,
  onChange,
  delay = 0,
}: {
  player: PlayerVPInfo;
  victoryPoints: number;
  onChange: (delta: number) => void;
  delay?: number;
}) {
  return (
    <motion.div
      className="relative flex flex-col items-center"
      initial={{ rotateY: 180, scale: 0 }}
      animate={{ rotateY: 0, scale: 1 }}
      transition={{
        delay,
        duration: 0.6,
        type: 'spring',
        damping: 15,
        stiffness: 200,
      }}
      style={{ transformStyle: 'preserve-3d' }}
    >
      {/* Flat-top hexagon background */}
      <div className="relative flex items-center justify-center" style={{ width: 140, height: 161 }}>
        <svg viewBox="0 0 100 100" className="absolute inset-0 w-full h-full">
          <defs>
            <linearGradient id={`vp-grad-${player.id}`} x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor={player.colors.primary} />
              <stop offset="100%" stopColor={player.colors.secondary} />
            </linearGradient>
            <clipPath id={`vp-clip-${player.id}`}>
              <polygon points={FLAT_TOP_HEX_POINTS} />
            </clipPath>
          </defs>

          {/* Hex background */}
          <polygon
            points={FLAT_TOP_HEX_POINTS}
            fill={`url(#vp-grad-${player.id})`}
            stroke={player.colors.foreground}
            strokeWidth="2"
          />

          {/* Avatar image */}
          {player.avatarUrl && (
            <image
              href={player.avatarUrl}
              x="25"
              y="15"
              width="50"
              height="50"
              clipPath={`url(#vp-clip-${player.id})`}
              preserveAspectRatio="xMidYMid slice"
            />
          )}
        </svg>

        {/* Content overlay */}
        <div className="relative z-10 text-center px-2 pointer-events-none">
          {/* Player name */}
          <div
            className="font-bold text-xs mb-1 mt-12"
            style={{
              color: player.colors.foreground,
              textShadow: '0 1px 3px rgba(0,0,0,0.8)',
            }}
          >
            {player.name}
          </div>

          {/* VP counter */}
          <div className="flex items-center justify-center gap-2 pointer-events-auto">
            <motion.button
              className="w-7 h-7 rounded-full bg-black/40 text-white font-bold flex items-center justify-center"
              whileHover={{ scale: 1.1, backgroundColor: 'rgba(0,0,0,0.6)' }}
              whileTap={{ scale: 0.9 }}
              onClick={() => onChange(-1)}
            >
              −
            </motion.button>

            <div
              className="font-black text-2xl min-w-[2ch] text-center"
              style={{
                color: player.colors.foreground,
                textShadow: '0 2px 4px rgba(0,0,0,0.8)',
              }}
            >
              {victoryPoints}
            </div>

            <motion.button
              className="w-7 h-7 rounded-full bg-black/40 text-white font-bold flex items-center justify-center"
              whileHover={{ scale: 1.1, backgroundColor: 'rgba(0,0,0,0.6)' }}
              whileTap={{ scale: 0.9 }}
              onClick={() => onChange(1)}
            >
              +
            </motion.button>
          </div>

          <div
            className="text-xs mt-1"
            style={{
              color: player.colors.foreground,
              opacity: 0.8,
            }}
          >
            VP
          </div>
        </div>
      </div>
    </motion.div>
  );
}

/**
 * Victory Points adjustment overlay with flat-top hexagonal layout
 */
export const VictoryPointsOverlay = memo(function VictoryPointsOverlay({
  players,
  onDone,
}: VictoryPointsOverlayProps): React.ReactElement {
  // Local state for VP adjustments
  const [vpValues, setVpValues] = useState<Record<string, number>>(
    Object.fromEntries(players.map(p => [p.id, p.victoryPoints]))
  );

  const handleVPChange = useCallback((playerId: string, delta: number) => {
    setVpValues(prev => ({
      ...prev,
      [playerId]: Math.max(0, (prev[playerId] || 0) + delta),
    }));
  }, []);

  const handleDone = useCallback(() => {
    onDone(vpValues);
  }, [vpValues, onDone]);

  // Arrange players in hexagonal ring
  const radius = 200;
  const anglePerPlayer = 360 / players.length;

  return (
    <motion.div
      className="fixed inset-0 z-[1000] pointer-events-none"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/70 pointer-events-auto" />

      {/* Content container */}
      <div className="absolute inset-0 flex items-center justify-center">
        <motion.div
          className="relative pointer-events-none"
          style={{
            width: radius * 2.8,
            height: radius * 2.8,
          }}
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
          exit={{ scale: 0 }}
          transition={{ type: 'spring', damping: 20, stiffness: 300 }}
        >
          {/* Central flat-top hex with title and done button */}
          <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 pointer-events-auto">
            <motion.div
              className="relative flex flex-col items-center justify-center"
              style={{ width: 180, height: 207 }}
              initial={{ rotateY: 180, scale: 0 }}
              animate={{ rotateY: 0, scale: 1 }}
              transition={{
                delay: 0.2,
                duration: 0.6,
                type: 'spring',
                damping: 15,
                stiffness: 200,
              }}
            >
              <svg viewBox="0 0 100 100" className="absolute inset-0 w-full h-full">
                <defs>
                  <linearGradient id="vp-center-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stopColor="#FFD700" />
                    <stop offset="100%" stopColor="#FFA500" />
                  </linearGradient>
                </defs>
                <polygon
                  points={FLAT_TOP_HEX_POINTS}
                  fill="url(#vp-center-grad)"
                  stroke="#FFD700"
                  strokeWidth="3"
                />
              </svg>

              <div className="relative z-10 text-center px-4">
                <div className="text-3xl mb-2">📊</div>
                <div className="font-black text-base text-black mb-3">Victory Points</div>

                <motion.button
                  className="px-6 py-2 rounded-lg font-semibold text-sm text-white"
                  style={{
                    background: 'linear-gradient(135deg, #4ade80 0%, #22c55e 100%)',
                  }}
                  whileHover={{ scale: 1.05, boxShadow: '0 0 20px rgba(74, 222, 128, 0.5)' }}
                  whileTap={{ scale: 0.95 }}
                  onClick={handleDone}
                >
                  Done
                </motion.button>
              </div>
            </motion.div>
          </div>

          {/* Player ring */}
          <div className="absolute inset-0 pointer-events-auto">
            {players.map((player, index) => {
              const angle = ((index * anglePerPlayer - 90) * Math.PI) / 180; // -90 to start at top
              const x = radius + Math.cos(angle) * radius;
              const y = radius + Math.sin(angle) * radius;

              return (
                <div
                  key={player.id}
                  className="absolute"
                  style={{
                    left: x - 70,
                    top: y - 80.5,
                  }}
                >
                  <PlayerVPHex
                    player={player}
                    victoryPoints={vpValues[player.id] || 0}
                    onChange={(delta) => handleVPChange(player.id, delta)}
                    delay={0.3 + index * 0.1}
                  />
                </div>
              );
            })}
          </div>
        </motion.div>
      </div>
    </motion.div>
  );
});

export default VictoryPointsOverlay;
