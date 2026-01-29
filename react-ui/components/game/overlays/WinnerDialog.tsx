'use client';

/**
 * WinnerDialog - Confirmation dialog for declaring a winner.
 *
 * Matches Blazor Game.razor lines 409-424 with enhanced styling.
 */

import { memo } from 'react';
import { motion } from 'framer-motion';

export interface WinnerDialogProps {
  /** Name of the player to be declared winner */
  playerName: string;
  /** Callback when user confirms */
  onConfirm: () => void;
  /** Callback when user cancels */
  onCancel: () => void;
}

/**
 * Winner confirmation dialog - prevents accidental winner declaration
 */
export const WinnerDialog = memo(function WinnerDialog({
  playerName,
  onConfirm,
  onCancel,
}: WinnerDialogProps): React.ReactElement {
  return (
    <>
      {/* Backdrop */}
      <motion.div
        className="fixed inset-0 z-[999] flex items-center justify-center"
        style={{
          backgroundColor: 'rgba(0, 0, 0, 0.7)',
        }}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        onClick={onCancel}
      />

      {/* Dialog */}
      <motion.div
        className="fixed z-[1000] rounded-xl overflow-hidden shadow-2xl"
        style={{
          left: '50%',
          top: '50%',
          transform: 'translate(-50%, -50%)',
          backgroundColor: '#1a1a1a',
          border: '3px solid #FFD700',
          minWidth: '400px',
          maxWidth: '500px',
        }}
        initial={{ scale: 0.8, opacity: 0, y: 20 }}
        animate={{ scale: 1, opacity: 1, y: 0 }}
        exit={{ scale: 0.8, opacity: 0, y: 20 }}
        transition={{ type: 'spring', damping: 20, stiffness: 300 }}
      >
        {/* Header */}
        <div
          className="px-6 py-4 text-center"
          style={{
            background: 'linear-gradient(135deg, #FFD700 0%, #FFA500 100%)',
          }}
        >
          <div className="text-3xl mb-2">🏆</div>
          <h2 className="text-2xl font-bold text-black">Declare Winner</h2>
        </div>

        {/* Content */}
        <div className="px-6 py-8 text-center">
          <p className="text-xl text-white mb-2">
            Declare <span className="font-bold text-yellow-400">{playerName}</span> as the winner
          </p>
          <p className="text-lg text-gray-300">
            and end the game?
          </p>
        </div>

        {/* Buttons */}
        <div className="px-6 pb-6 flex gap-4">
          <motion.button
            className="flex-1 px-6 py-3 rounded-lg font-semibold text-lg"
            style={{
              background: 'linear-gradient(135deg, #4ade80 0%, #22c55e 100%)',
              color: 'white',
            }}
            whileHover={{ scale: 1.05, boxShadow: '0 0 20px rgba(74, 222, 128, 0.5)' }}
            whileTap={{ scale: 0.95 }}
            onClick={onConfirm}
          >
            Yes
          </motion.button>
          <motion.button
            className="flex-1 px-6 py-3 rounded-lg font-semibold text-lg"
            style={{
              background: 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)',
              color: 'white',
            }}
            whileHover={{ scale: 1.05, boxShadow: '0 0 20px rgba(239, 68, 68, 0.5)' }}
            whileTap={{ scale: 0.95 }}
            onClick={onCancel}
          >
            No
          </motion.button>
        </div>
      </motion.div>
    </>
  );
});

export default WinnerDialog;
