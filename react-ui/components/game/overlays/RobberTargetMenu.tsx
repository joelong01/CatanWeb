'use client';

/**
 * RobberTargetMenu - Context menu for selecting robber target.
 *
 * Matches Blazor Game.razor:351-374:
 * - Positioned at click location
 * - Player targets: "Target {name}"
 * - "Nobody. Hatred Deferred." option (when no targets)
 * - Cancel option
 * - Dark theme styling
 */

import { memo } from 'react';

export interface RobberTarget {
  id: string;
  name: string;
}

export interface RobberTargetMenuProps {
  /** Players that can be targeted (have buildings adjacent to robber tile) */
  targetPlayers: RobberTarget[];
  /** Menu position (client coordinates from click event) */
  position: { x: number; y: number };
  /** Callback when a target is selected (undefined = "Nobody") */
  onSelectTarget: (playerId: string | undefined) => void;
  /** Callback to cancel/close menu */
  onCancel: () => void;
}

/**
 * RobberTargetMenu - matches Blazor implementation.
 */
export const RobberTargetMenu = memo(function RobberTargetMenu({
  targetPlayers,
  position,
  onSelectTarget,
  onCancel,
}: RobberTargetMenuProps): React.ReactElement {
  return (
    <>
      {/* Transparent backdrop for click-away dismissal */}
      <div className="fixed inset-0 z-[999]" onClick={onCancel} />

      {/* Menu positioned at click location */}
      <div
        className="fixed z-[1000] min-w-[200px] rounded-lg overflow-hidden"
        style={{
          left: position.x,
          top: position.y,
          backgroundColor: '#2a2a2a',
          border: '1px solid #444',
          boxShadow: '0 4px 20px rgba(0, 0, 0, 0.5)',
        }}
      >
        {/* Target players */}
        {targetPlayers.map((target) => (
          <div
            key={target.id}
            className="px-4 py-2.5 text-white cursor-pointer transition-colors hover:bg-[#3a3a3a]"
            onClick={() => onSelectTarget(target.id)}
          >
            Target {target.name}
          </div>
        ))}

        {/* Separator (only if there are targets) */}
        {targetPlayers.length > 0 && (
          <div className="h-px my-1.5" style={{ backgroundColor: '#444' }} />
        )}

        {/* Nobody option */}
        <div
          className="px-4 py-2.5 italic cursor-pointer transition-colors hover:bg-[#3a3a3a]"
          style={{ color: '#aaa' }}
          onClick={() => onSelectTarget(undefined)}
        >
          Nobody. Hatred Deferred.
        </div>

        {/* Separator */}
        <div className="h-px my-1.5" style={{ backgroundColor: '#444' }} />

        {/* Cancel option */}
        <div
          className="px-4 py-2.5 cursor-pointer transition-colors hover:bg-[#3a3a3a]"
          style={{ color: '#888' }}
          onClick={onCancel}
        >
          Cancel
        </div>
      </div>
    </>
  );
});

export default RobberTargetMenu;
