'use client';

/**
 * MeasurementCluster - Board measurement controls.
 *
 * Shows:
 * - 5 resource types with star counts from tiles
 * - Star filter buttons (8-13) to filter building spots
 * - Reset button to clear filters
 *
 * Always visible - helps evaluate settlement spots during allocation
 * and provides resource overview during gameplay.
 */

import { memo, useCallback } from 'react';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import type { GameModel } from '@/types/generated/models/game-model';

interface MeasurementClusterProps {
  /** Game model for tile data */
  gameModel: GameModel | null;
}

/** Resource config with colors */
const RESOURCES = [
  { key: 'Wheat', label: 'Wheat', icon: '🌾', bg: 'bg-yellow-600' },
  { key: 'Wood', label: 'Wood', icon: '🪵', bg: 'bg-green-700' },
  { key: 'Sheep', label: 'Sheep', icon: '🐑', bg: 'bg-lime-600' },
  { key: 'Brick', label: 'Brick', icon: '🧱', bg: 'bg-red-700' },
  { key: 'Ore', label: 'Ore', icon: '⛰️', bg: 'bg-gray-500' },
] as const;

/** Star filter values */
const STAR_VALUES = [13, 12, 11, 10, 9, 8];

/** Calculate star count for a resource type from tiles */
function calculateResourceStars(gameModel: GameModel | null, resourceType: string): number {
  if (!gameModel?.tiles) return 0;

  return gameModel.tiles
    .filter((tile) => tile.resourceTileType === resourceType)
    .reduce((sum, tile) => sum + (tile.stars || 0), 0);
}

/** Count building spots with at least minStars */
function countBuildingSpotsAtStars(gameModel: GameModel | null, minStars: number): number {
  if (!gameModel?.buildings) return 0;

  return gameModel.buildings.filter((b) => {
    // Buildings may have stars property
    const building = b as unknown as { stars?: number };
    return building.stars !== undefined && building.stars >= minStars;
  }).length;
}

/** Resource card component */
const ResourceCard = memo(function ResourceCard({
  resourceKey,
  label,
  icon,
  bg,
  starCount,
  isSelected,
  onClick,
}: {
  resourceKey: string;
  label: string;
  icon: string;
  bg: string;
  starCount: number;
  isSelected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={`
        flex flex-col items-center p-1.5 rounded-lg
        transition-all duration-200
        ${bg}
        ${isSelected ? 'ring-2 ring-amber-400 scale-105' : 'hover:brightness-110'}
      `}
      title={`${label}: ${starCount} stars`}
    >
      <span className="text-base">{icon}</span>
      <span className="text-white font-bold text-xs">{starCount}</span>
    </button>
  );
});

/** Star filter button */
const StarButton = memo(function StarButton({
  value,
  count,
  isSelected,
  onClick,
}: {
  value: number;
  count: number;
  isSelected: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={`
        w-8 h-8 rounded-full flex flex-col items-center justify-center
        transition-all duration-200 text-xs
        ${isSelected ? 'bg-amber-500 text-white' : 'bg-gray-700 text-gray-300 hover:bg-gray-600'}
      `}
      title={`Show ${value}+ star spots (${count})`}
    >
      <span className="font-bold leading-none">{value}</span>
      {count > 0 && <span className="text-[8px] leading-none">{count}</span>}
    </button>
  );
});

/** Main MeasurementCluster component */
export const MeasurementCluster = memo(function MeasurementCluster({
  gameModel,
}: MeasurementClusterProps): React.ReactElement {
  const starFilter = useLayoutStore((state) => state.starFilter);
  const resourceFilter = useLayoutStore((state) => state.resourceFilter);
  const setStarFilter = useLayoutStore((state) => state.setStarFilter);
  const setResourceFilter = useLayoutStore((state) => state.setResourceFilter);

  const handleResourceClick = useCallback((resourceKey: string) => {
    setResourceFilter(resourceFilter === resourceKey ? null : resourceKey);
  }, [resourceFilter, setResourceFilter]);

  const handleStarClick = useCallback((value: number) => {
    setStarFilter(starFilter === value ? null : value);
  }, [starFilter, setStarFilter]);

  const handleReset = useCallback(() => {
    setStarFilter(null);
    setResourceFilter(null);
  }, [setStarFilter, setResourceFilter]);

  // Show placeholder if no game data
  const hasData = gameModel !== null;

  return (
    <div className="p-2">
      {/* Resource row */}
      <div className="grid grid-cols-5 gap-1.5">
        {RESOURCES.map(({ key, label, icon, bg }) => (
          <ResourceCard
            key={key}
            resourceKey={key}
            label={label}
            icon={icon}
            bg={bg}
            starCount={hasData ? calculateResourceStars(gameModel, key) : 0}
            isSelected={resourceFilter === key}
            onClick={() => handleResourceClick(key)}
          />
        ))}
      </div>

      {/* Star filter row */}
      <div className="mt-2 flex items-center justify-center gap-1">
        {STAR_VALUES.map((value) => (
          <StarButton
            key={value}
            value={value}
            count={hasData ? countBuildingSpotsAtStars(gameModel, value) : 0}
            isSelected={starFilter === value}
            onClick={() => handleStarClick(value)}
          />
        ))}

        {/* Reset button */}
        <button
          onClick={handleReset}
          className="w-8 h-8 rounded-full bg-gray-700 text-gray-400 hover:bg-gray-600 hover:text-white flex items-center justify-center transition-colors ml-1"
          title="Reset filters"
        >
          ↺
        </button>
      </div>

      {/* Filter status */}
      <div className="mt-1.5 text-center text-[10px] text-gray-500">
        {!hasData && 'Waiting for game data...'}
        {hasData && starFilter && <span>{starFilter}+ stars</span>}
        {hasData && resourceFilter && starFilter && <span> • </span>}
        {hasData && resourceFilter && <span>{resourceFilter}</span>}
        {hasData && !starFilter && !resourceFilter && <span>Click to filter</span>}
      </div>
    </div>
  );
});

export default MeasurementCluster;
