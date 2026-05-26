'use client';

/**
 * SaveLayoutDialog - Modal for naming a layout preset.
 *
 * Follows the RobberTargetMenu pattern: fixed overlay with backdrop.
 * Renders above the NavMenu overlay (z-[2000]).
 */

import { useState, useRef, useEffect, useCallback } from 'react';
import { useLayoutStore } from '@/lib/stores/layoutStore';

const MODAL_ID = 'save-layout-dialog';

interface SaveLayoutDialogProps {
  /** Pre-fill name (current layout name, if any) */
  defaultName: string | null;
  /** Names already in use (for overwrite warning) */
  existingNames: string[];
  /** Called with the chosen name */
  onSave: (name: string) => void;
  /** Called on cancel / dismiss */
  onCancel: () => void;
}

export function SaveLayoutDialog({
  defaultName,
  existingNames,
  onSave,
  onCancel,
}: SaveLayoutDialogProps): React.ReactElement {
  const [name, setName] = useState(defaultName ?? '');
  const inputRef = useRef<HTMLInputElement>(null);

  const trimmed = name.trim();
  const isOverwrite = trimmed.length > 0 && existingNames.includes(trimmed);
  const canSave = trimmed.length > 0;

  // Autofocus and select text on mount
  useEffect(() => {
    const el = inputRef.current;
    if (el) {
      el.focus();
      el.select();
    }
  }, []);

  // Register with the modal token-set so the global keyboard hook
  // suppresses Enter/Backspace shortcuts while this dialog owns the
  // keyboard. React guarantees cleanup runs on unmount.
  const registerModal = useLayoutStore((s) => s.registerModal);
  const unregisterModal = useLayoutStore((s) => s.unregisterModal);
  useEffect(() => {
    registerModal(MODAL_ID);
    return () => unregisterModal(MODAL_ID);
  }, [registerModal, unregisterModal]);

  // Escape key dismisses
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCancel();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [onCancel]);

  const handleSubmit = useCallback(() => {
    if (canSave) onSave(trimmed);
  }, [canSave, trimmed, onSave]);

  return (
    <>
      {/* Backdrop */}
      <div className="fixed inset-0 z-[1999] bg-black/50" onClick={onCancel} />

      {/* Dialog */}
      <div
        className="fixed z-[2000] w-[320px] rounded-lg overflow-hidden bg-gray-900/95 backdrop-blur-sm border border-gray-700/50 shadow-xl"
        style={{
          left: '50%',
          top: '50%',
          transform: 'translate(-50%, -50%)',
        }}
      >
        {/* Title */}
        <div className="px-4 pt-4 pb-2 text-white text-sm font-medium">Save Layout As</div>

        {/* Input */}
        <div className="px-4 pb-2">
          <input
            ref={inputRef}
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleSubmit();
            }}
            placeholder="Layout name..."
            className="w-full px-3 py-2 rounded bg-gray-800 border border-gray-600 text-white text-sm placeholder-gray-500 focus:outline-none focus:border-amber-500/50 focus:ring-1 focus:ring-amber-500/30"
          />
          {isOverwrite && (
            <div className="mt-1 text-xs text-amber-400">
              A layout with this name exists and will be overwritten.
            </div>
          )}
        </div>

        {/* Buttons */}
        <div className="flex justify-end gap-2 px-4 pb-4 pt-1">
          <button
            onClick={onCancel}
            className="px-3 py-1.5 rounded text-sm text-gray-400 hover:text-white hover:bg-gray-700 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={!canSave}
            className="px-3 py-1.5 rounded text-sm bg-amber-600 text-white hover:bg-amber-500 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            {isOverwrite ? 'Overwrite' : 'Save'}
          </button>
        </div>
      </div>
    </>
  );
}
