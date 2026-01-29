'use client';

/**
 * WinnerCelebration - Hexagonal spinner celebration with flat-top hexes and avatars.
 *
 * Features:
 * - Flat-top hexagons (rotated 30°)
 * - Avatar images
 * - Face-up flip animation
 * - Spinning ring
 * - Winner stops at top, moves to center
 */

import { memo, useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import type { PlayerColors } from '@/types/player-profile';

export interface PlayerInfo {
  id: string;
  name: string;
  colors: PlayerColors;
  avatarUrl?: string;
}

export interface WinnerCelebrationProps {
  /** All players in the game */
  players: PlayerInfo[];
  /** ID of the winning player */
  winnerId: string;
  /** Callback when animation completes */
  onComplete?: () => void;
}

/**
 * Flat-top hexagon polygon points (rotated 30° from pointy-top)
 */
const FLAT_TOP_HEX_POINTS = "25,5 75,5 100,50 75,95 25,95 0,50";

/**
 * Flat-top hexagonal player card with avatar
 */
function PlayerHex({
  player,
  isWinner,
  scale = 1,
  style,
}: {
  player: PlayerInfo;
  isWinner: boolean;
  scale?: number;
  style?: React.CSSProperties;
}) {
  return (
    <div
      className="relative flex items-center justify-center"
      style={{
        width: 120 * scale,
        height: 138 * scale,
        ...style,
      }}
    >
      {/* Flat-top hexagon background */}
      <svg
        viewBox="0 0 100 100"
        className="absolute inset-0 w-full h-full"
        style={{ filter: isWinner ? 'drop-shadow(0 0 20px rgba(255, 215, 0, 0.8))' : 'none' }}
      >
        <defs>
          <linearGradient id={`grad-${player.id}`} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor={player.colors.primary} />
            <stop offset="100%" stopColor={player.colors.secondary} />
          </linearGradient>
          <clipPath id={`clip-${player.id}`}>
            <polygon points={FLAT_TOP_HEX_POINTS} />
          </clipPath>
        </defs>

        {/* Hex background */}
        <polygon
          points={FLAT_TOP_HEX_POINTS}
          fill={`url(#grad-${player.id})`}
          stroke={isWinner ? '#FFD700' : player.colors.foreground}
          strokeWidth={isWinner ? '4' : '2'}
        />

        {/* Avatar image */}
        {player.avatarUrl && (
          <image
            href={player.avatarUrl}
            x="20"
            y="10"
            width="60"
            height="60"
            clipPath={`url(#clip-${player.id})`}
            preserveAspectRatio="xMidYMid slice"
          />
        )}
      </svg>

      {/* Player name and crown */}
      <div className="relative z-10 text-center px-2" style={{ marginTop: player.avatarUrl ? '50px' : '0' }}>
        <div
          className="font-bold text-sm break-words"
          style={{
            color: player.colors.foreground,
            textShadow: '0 1px 3px rgba(0,0,0,0.8)',
            fontSize: 12 * scale,
          }}
        >
          {player.name}
        </div>
        {isWinner && (
          <div className="text-2xl mt-1" style={{ fontSize: 24 * scale }}>
            👑
          </div>
        )}
      </div>
    </div>
  );
}

/**
 * Hexagonal winner celebration with spinning player ring
 */
export const WinnerCelebration = memo(function WinnerCelebration({
  players,
  winnerId,
  onComplete,
}: WinnerCelebrationProps): React.ReactElement {
  const [phase, setPhase] = useState<'spinning' | 'stopped' | 'centering' | 'complete'>('spinning');
  const winnerIndex = players.findIndex(p => p.id === winnerId);

  // Calculate rotation needed to put winner at top (12 o'clock position)
  const anglePerPlayer = 360 / players.length;
  const winnerAngle = winnerIndex * anglePerPlayer;
  const rotationToTop = -winnerAngle + 90; // 90 degrees is top position

  useEffect(() => {
    // Spin for 2 seconds
    const spinTimer = setTimeout(() => setPhase('stopped'), 2000);

    // Move to center after stopping
    const centerTimer = setTimeout(() => setPhase('centering'), 2500);

    // Complete after centering
    const completeTimer = setTimeout(() => {
      setPhase('complete');
      onComplete?.();
    }, 4000);

    return () => {
      clearTimeout(spinTimer);
      clearTimeout(centerTimer);
      clearTimeout(completeTimer);
    };
  }, [onComplete]);

  const radius = 250;

  return (
    <div className="fixed inset-0 z-[1001] pointer-events-none flex items-center justify-center overflow-hidden">
      {/* Dark backdrop */}
      <motion.div
        className="absolute inset-0 bg-black/80"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
      />

      {/* Central flat-top hex with "WINNER" text */}
      <AnimatePresence>
        {phase !== 'centering' && phase !== 'complete' && (
          <motion.div
            className="absolute"
            initial={{ scale: 0, opacity: 0, rotateY: 180 }}
            animate={{ scale: 1, opacity: 1, rotateY: 0 }}
            exit={{ scale: 0, opacity: 0 }}
            transition={{ duration: 0.5 }}
          >
            <div className="relative flex items-center justify-center" style={{ width: 160, height: 184 }}>
              <svg viewBox="0 0 100 100" className="absolute inset-0 w-full h-full">
                <defs>
                  <linearGradient id="center-grad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stopColor="#FFD700" />
                    <stop offset="100%" stopColor="#FFA500" />
                  </linearGradient>
                </defs>
                <polygon
                  points={FLAT_TOP_HEX_POINTS}
                  fill="url(#center-grad)"
                  stroke="#FFD700"
                  strokeWidth="3"
                />
              </svg>
              <div className="relative z-10 text-center">
                <div className="text-3xl mb-1">🏆</div>
                <div className="font-black text-lg text-black">WINNER</div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Player ring */}
      <motion.div
        className="relative"
        style={{ width: radius * 2, height: radius * 2 }}
        animate={{
          rotate: phase === 'spinning' ? [0, 720 + rotationToTop] : rotationToTop,
        }}
        transition={{
          duration: phase === 'spinning' ? 2 : 0.5,
          ease: phase === 'spinning' ? 'linear' : 'easeOut',
        }}
      >
        {players.map((player, index) => {
          const angle = (index * anglePerPlayer * Math.PI) / 180;
          const x = Math.cos(angle) * radius;
          const y = Math.sin(angle) * radius;
          const isWinner = player.id === winnerId;

          return (
            <motion.div
              key={player.id}
              className="absolute"
              style={{
                left: '50%',
                top: '50%',
                x: x - 60,
                y: y - 69,
              }}
              initial={{ rotateY: 180, scale: 0 }}
              animate={{
                // Face-up flip animation
                rotateY: phase === 'spinning' ? [-720 - rotationToTop + 180, 0] : 0,
                // Winner moves to center
                ...(isWinner && (phase === 'centering' || phase === 'complete')
                  ? { x: -60, y: -69, scale: 1.5 }
                  : { scale: 1 }),
              }}
              transition={{
                rotateY: {
                  duration: phase === 'spinning' ? 2 : 0.5,
                  ease: phase === 'spinning' ? 'linear' : 'easeOut',
                },
                scale: {
                  duration: 0.4,
                  delay: index * 0.05,
                  type: 'spring',
                  damping: 15,
                  stiffness: 200,
                },
                x: { duration: 0.8, ease: 'easeInOut', delay: 0.2 },
                y: { duration: 0.8, ease: 'easeInOut', delay: 0.2 },
              }}
              style={{ transformStyle: 'preserve-3d' }}
            >
              <motion.div
                animate={
                  isWinner && phase === 'stopped'
                    ? {
                        scale: [1, 1.1, 1],
                      }
                    : {}
                }
                transition={{
                  duration: 0.5,
                  repeat: 2,
                }}
              >
                <PlayerHex
                  player={player}
                  isWinner={isWinner}
                  scale={isWinner && (phase === 'centering' || phase === 'complete') ? 1.2 : 1}
                />
              </motion.div>
            </motion.div>
          );
        })}
      </motion.div>

      {/* Confetti burst when winner centers */}
      <AnimatePresence>
        {phase === 'centering' && (
          <>
            {Array.from({ length: 50 }, (_, i) => {
              const angle = (i * (360 / 50) * Math.PI) / 180;
              const distance = 200 + Math.random() * 300;
              return (
                <motion.div
                  key={i}
                  className="absolute left-1/2 top-1/2 w-3 h-3 rounded"
                  style={{
                    backgroundColor: ['#FFD700', '#FF69B4', '#00CED1', '#FF4500', '#32CD32'][
                      Math.floor(Math.random() * 5)
                    ],
                  }}
                  initial={{ x: 0, y: 0, scale: 0, opacity: 1 }}
                  animate={{
                    x: Math.cos(angle) * distance,
                    y: Math.sin(angle) * distance,
                    scale: [0, 1, 0],
                    opacity: [1, 1, 0],
                    rotate: Math.random() * 720,
                  }}
                  transition={{
                    duration: 1.5,
                    ease: 'easeOut',
                  }}
                />
              );
            })}
          </>
        )}
      </AnimatePresence>
    </div>
  );
});

export default WinnerCelebration;
