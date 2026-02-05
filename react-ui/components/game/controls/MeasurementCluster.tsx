'use client';

/**
 * MeasurementCluster - Board measurement controls with nested hex design.
 *
 * Features:
 * - Outer ring: 5 resource types with star counts + variance hex
 * - Inner cluster: Star filter values (8-13) + reset button
 * - Multi-select resources (up to 3), single-select stars
 * - Resource images with count overlays
 * - Variance indicator with balance scale icon
 */

import { memo, useState, useCallback } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowsRotate, faCheck, faScaleBalanced } from '@fortawesome/free-solid-svg-icons';
import { HexGrid, type HexGridItem, type HexCoordinate } from '@/components/hex-grid';
import { useAssetPath } from '@/lib/theme';
import type { AssetName } from '@/lib/theme/types';
import type { PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import type { TileModel } from '@/types/generated/models/tile-model';

// ============================================================================
// Types
// ============================================================================

/** Counts of building spots for each star value */
export interface StarCounts {
  [stars: number]: number;
}

export interface MeasurementClusterProps {
  /** Tiles for resource counting */
  tiles: TileModel[];
  /** Player colors for styling */
  colors?: PlayerColorsWithGradient;
  /** Counts of building spots for each star value (8-13) */
  starCounts?: StarCounts;
  /** Callback when resource selection changes */
  onResourceSelectionChange?: (resources: string[]) => void;
  /** Callback when star filter changes */
  onStarFilterChange?: (stars: number | null) => void;
  /** Callback when reset/shuffle is clicked */
  onReset?: () => void;
  /** Whether shuffle is enabled (only during PickingBoard state) */
  shuffleEnabled?: boolean;
}

// ============================================================================
// Constants
// ============================================================================

/** Standard 7-hex cluster: center + 6 neighbors */
const CLUSTER_7: Record<string, HexCoordinate> = {
  center: { q: 0, r: 0, s: 0 },
  north: { q: 0, r: -1, s: 1 },
  northEast: { q: 1, r: -1, s: 0 },
  southEast: { q: 1, r: 0, s: -1 },
  south: { q: 0, r: 1, s: -1 },
  southWest: { q: -1, r: 1, s: 0 },
  northWest: { q: -1, r: 0, s: 1 },
};

/** Resource configuration for outer ring */
const RESOURCE_CONFIG = [
  { key: 'Sheep', label: 'Sheep', position: 'north', assetName: 'CardSheep' as AssetName },
  { key: 'Wood', label: 'Wood', position: 'northEast', assetName: 'CardWood' as AssetName },
  { key: 'Wheat', label: 'Wheat', position: 'southEast', assetName: 'CardWheat' as AssetName },
  { key: 'Brick', label: 'Brick', position: 'southWest', assetName: 'CardBrick' as AssetName },
  { key: 'Ore', label: 'Ore', position: 'northWest', assetName: 'CardOre' as AssetName },
] as const;

/** Star filter values for inner cluster. 0 = "show all" (no star threshold). */
const STAR_VALUES_CONFIG = [
  { value: 13, position: 'north' },
  { value: 12, position: 'northEast' },
  { value: 11, position: 'southEast' },
  { value: 10, position: 'south' },
  { value: 9, position: 'southWest' },
  { value: 0, position: 'northWest' },
] as const;

/** Max resources that can be selected at once */
const MAX_RESOURCE_SELECTION = 3;

// ============================================================================
// Helper Functions
// ============================================================================

/** Stars (probability dots) for each dice number */
function getStarsForNumber(num: number): number {
  if (num === 7) return 0;
  return 6 - Math.abs(7 - num);
}

/** Calculate star count and tile count for a resource type from tiles */
function calculateResourceStats(tiles: TileModel[], resourceType: string): { stars: number; tiles: number } {
  if (!tiles || tiles.length === 0) return { stars: 0, tiles: 0 };

  const resourceTiles = tiles.filter((tile) => tile.resourceTileType === resourceType);
  const stars = resourceTiles.reduce((sum, tile) => {
    return sum + (tile.number ? getStarsForNumber(tile.number) : 0);
  }, 0);

  return { stars, tiles: resourceTiles.length };
}

/**
 * Calculate variance (how unbalanced the resources are).
 * Matches C# CalculateVariance: max(avg) - min(avg) where avg = stars/tiles for each resource.
 */
function calculateVariance(stats: Record<string, { stars: number; tiles: number }>): number {
  const averages = Object.values(stats)
    .filter(s => s.tiles > 0)
    .map(s => s.stars / s.tiles);

  if (averages.length === 0) return 0;

  const max = Math.max(...averages);
  const min = Math.min(...averages);
  return max - min;
}

// ============================================================================
// ResourceHexContent - Resource image with count overlay
// ============================================================================

interface ResourceHexContentProps {
  assetName: AssetName;
  count: number;
  isSelected: boolean;
}

const ResourceHexContent = memo(function ResourceHexContent({
  assetName,
  count,
  isSelected,
}: ResourceHexContentProps) {
  const image = useAssetPath(assetName);
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Scale based on interaction state
  const innerScale = isPressed ? 0.86 : isHovered ? 0.89 : 0.92;
  const borderColor = isSelected ? '#3b82f6' : isHovered ? '#60a5fa' : 'rgba(255,255,255,0.3)';

  return (
    <div
      className="absolute inset-0"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Outer border - blue when selected */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content - resource image background */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-end justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          backgroundImage: image ? `url(${image})` : undefined,
          backgroundSize: 'cover',
          backgroundPosition: 'center',
        }}
      >
        {/* Count overlay - white text on black background, flush to bottom */}
        <div
          className="px-2 py-0.5 rounded-t transition-transform duration-150"
          style={{
            background: 'rgba(0, 0, 0, 0.75)',
            transform: isPressed ? 'scale(0.9)' : 'scale(1)',
          }}
        >
          <span className="font-bold text-xl text-white leading-none">
            {count}
          </span>
        </div>
      </div>
      {/* Selection checkmark - upper-right area */}
      {isSelected && (
        <div
          className="absolute z-10 w-6 h-6 rounded-full flex items-center justify-center bg-black/50 border border-white/50 -translate-x-1/2 -translate-y-1/2"
          style={{ top: '16%', left: '68%' }}
        >
          <FontAwesomeIcon
            icon={faCheck}
            className="text-xs text-white"
          />
        </div>
      )}
    </div>
  );
});

// ============================================================================
// VarianceHexContent - Scale icon with variance value
// ============================================================================

/** Threshold for considering the board "balanced" - matches C# ValidateBalance default */
const BALANCE_VARIANCE_THRESHOLD = 0.5;

interface VarianceHexContentProps {
  variance: number;
  colors?: PlayerColorsWithGradient;
}

const VarianceHexContent = memo(function VarianceHexContent({
  variance,
  colors,
}: VarianceHexContentProps) {
  const gradient = colors?.cssGradient || 'var(--hex-content-gradient)';
  const foreground = colors?.foreground || '#ffffff';

  // Only show balance scale icon when board is balanced (variance below threshold)
  // Matches Blazor: IsBalanced => GameModel.ValidateBalance() && GameModel.ValidateNoClumps()
  // We can only check variance here; NoClumps validation would require tile adjacency analysis
  const isBalanced = variance <= BALANCE_VARIANCE_THRESHOLD;

  return (
    <>
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-200"
        style={{ background: 'var(--hex-border-idle)' }}
      />
      {/* Inner content - player gradient background */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center"
        style={{
          transform: 'scale(0.92)',
          background: gradient,
        }}
      >
        {/* Scale icon - only shown when board is balanced */}
        {isBalanced && (
          <FontAwesomeIcon
            icon={faScaleBalanced}
            className="text-xl"
            style={{ color: foreground }}
          />
        )}
        {/* Variance value - always shown */}
        <span
          className={`font-bold ${isBalanced ? 'text-lg' : 'text-2xl'}`}
          style={{ color: foreground }}
        >
          {variance.toFixed(1)}
        </span>
      </div>
    </>
  );
});

// ============================================================================
// StarHexContent - Star filter button (purple/violet theme)
// ============================================================================

interface StarHexContentProps {
  value: number;
  /** Count of building spots with this star value */
  count?: number;
  isSelected: boolean;
  colors?: PlayerColorsWithGradient;
}

const StarHexContent = memo(function StarHexContent({
  value,
  count,
  isSelected,
  colors,
}: StarHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Purple/lavender color scheme
  const purpleBg = '#a5b4fc'; // indigo-300
  const purpleBgSelected = '#6366f1'; // indigo-500

  // Scale based on interaction state
  const innerScale = isPressed ? 0.80 : isHovered ? 0.84 : 0.88;
  const borderColor = isSelected ? (colors?.primary || purpleBgSelected) : isHovered ? '#a5b4fc' : '#818cf8';
  const textColor = isSelected ? (colors?.foreground || '#ffffff') : '#1e1b4b';

  return (
    <div
      className="absolute inset-0"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
    >
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content - purple/lavender, split into star value and count */}
      <div
        className="absolute inset-0 hex-clip-flat flex flex-col items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: isSelected ? (colors?.cssGradient || purpleBgSelected) : purpleBg,
        }}
      >
        {/* Star value (top) — 0 means "show all" */}
        <span
          className="font-bold leading-none transition-transform duration-150"
          style={{
            color: textColor,
            transform: isPressed ? 'scale(0.85)' : 'scale(1)',
            fontSize: value === 0 ? '6px' : '8px',
          }}
        >
          {value === 0 ? 'All' : value}
        </span>
        {/* Divider line */}
        {count !== undefined && (
          <div
            className="w-3/5 h-px my-0.5"
            style={{ background: textColor, opacity: 0.5 }}
          />
        )}
        {/* Count (bottom) */}
        {count !== undefined && (
          <span
            className="font-semibold text-[7px] leading-none transition-transform duration-150"
            style={{
              color: textColor,
              transform: isPressed ? 'scale(0.85)' : 'scale(1)',
              opacity: 0.9,
            }}
          >
            {count}
          </span>
        )}
      </div>
    </div>
  );
});

// ============================================================================
// ResetHexContent - Reset/shuffle button
// ============================================================================

interface ResetHexContentProps {
  colors?: PlayerColorsWithGradient;
  disabled?: boolean;
}

const ResetHexContent = memo(function ResetHexContent({ colors, disabled }: ResetHexContentProps) {
  const [isHovered, setIsHovered] = useState(false);
  const [isPressed, setIsPressed] = useState(false);

  // Same purple theme as star buttons
  const purpleBg = '#a5b4fc'; // indigo-300
  const disabledBg = '#6b7280'; // gray-500

  // Scale based on interaction state (no interaction when disabled)
  const innerScale = disabled ? 0.88 : isPressed ? 0.80 : isHovered ? 0.84 : 0.88;
  const borderColor = disabled ? '#4b5563' : isHovered ? '#a5b4fc' : '#818cf8';

  return (
    <div
      className="absolute inset-0"
      onMouseEnter={() => !disabled && setIsHovered(true)}
      onMouseLeave={() => { setIsHovered(false); setIsPressed(false); }}
      onMouseDown={() => !disabled && setIsPressed(true)}
      onMouseUp={() => setIsPressed(false)}
      onTouchStart={() => !disabled && setIsPressed(true)}
      onTouchEnd={() => setIsPressed(false)}
      style={{ cursor: disabled ? 'not-allowed' : 'pointer' }}
    >
      {/* Outer border */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-150"
        style={{ background: borderColor }}
      />
      {/* Inner content - purple with shuffle/refresh icon (gray when disabled) */}
      <div
        className="absolute inset-0 hex-clip-flat flex items-center justify-center transition-all duration-150"
        style={{
          transform: `scale(${innerScale})`,
          background: disabled ? disabledBg : (colors?.cssGradient || purpleBg),
          opacity: disabled ? 0.6 : 1,
        }}
      >
        <FontAwesomeIcon
          icon={faArrowsRotate}
          className="text-[9px] transition-transform duration-150"
          style={{
            color: disabled ? '#9ca3af' : (colors?.foreground || '#1e1b4b'),
            transform: isPressed ? 'scale(0.85)' : 'scale(1)',
          }}
        />
      </div>
    </div>
  );
});

// ============================================================================
// MeasurementCluster - Main component with nested hex design
// ============================================================================

export const MeasurementCluster = memo(function MeasurementCluster({
  tiles,
  colors,
  starCounts,
  onResourceSelectionChange,
  onStarFilterChange,
  onReset,
  shuffleEnabled = true,
}: MeasurementClusterProps): React.ReactElement {
  // Multi-select for resources (up to 3), single-select for stars
  const [selectedResources, setSelectedResources] = useState<string[]>([]);
  const [selectedStars, setSelectedStars] = useState<number | null>(null);

  // Calculate resource star counts and stats for variance
  const resourceStats = RESOURCE_CONFIG.reduce((acc, { key }) => {
    acc[key] = calculateResourceStats(tiles, key);
    return acc;
  }, {} as Record<string, { stars: number; tiles: number }>);

  // Extract just the star totals for display
  const resourceCounts = Object.entries(resourceStats).reduce((acc, [key, stats]) => {
    acc[key] = stats.stars;
    return acc;
  }, {} as Record<string, number>);

  // Calculate variance (max-min of averages, matching C# CalculateVariance)
  const variance = calculateVariance(resourceStats);

  // Toggle resource selection (multi-select up to MAX_RESOURCE_SELECTION)
  const handleResourceClick = useCallback((key: string) => {
    setSelectedResources(prev => {
      let newSelection: string[];
      if (prev.includes(key)) {
        // Deselect if already selected
        newSelection = prev.filter(r => r !== key);
      } else if (prev.length < MAX_RESOURCE_SELECTION) {
        // Add to selection if under limit
        newSelection = [...prev, key];
      } else {
        // At max: remove oldest, add new (circular selection)
        newSelection = [...prev.slice(1), key];
      }
      onResourceSelectionChange?.(newSelection);
      return newSelection;
    });
  }, [onResourceSelectionChange]);

  // Single-select for star filter
  const handleStarClick = useCallback((value: number) => {
    setSelectedStars(prev => {
      const newValue = prev === value ? null : value;
      onStarFilterChange?.(newValue);
      return newValue;
    });
  }, [onStarFilterChange]);

  // Shuffle only — preserve current filter selections
  const handleShuffle = useCallback(() => {
    onReset?.();
  }, [onReset]);

  // Build outer ring items (5 resources + variance)
  const outerItems: HexGridItem[] = [
    // Resources in outer ring positions
    ...RESOURCE_CONFIG.map(({ key, position, assetName }) => ({
      id: `resource-${key}`,
      coord: CLUSTER_7[position],
      content: (
        <ResourceHexContent
          assetName={assetName}
          count={resourceCounts[key] || 0}
          isSelected={selectedResources.includes(key)}
        />
      ),
      onClick: () => handleResourceClick(key),
    })),
    // Variance in south position
    {
      id: 'variance',
      coord: CLUSTER_7.south,
      content: <VarianceHexContent variance={variance} colors={colors} />,
    },
  ];

  // Build inner cluster items (7 hexes for star filter)
  const innerItems: HexGridItem[] = [
    // Center: Shuffle button (disabled after board is approved)
    {
      id: 'reset',
      coord: CLUSTER_7.center,
      content: <ResetHexContent colors={colors} disabled={!shuffleEnabled} />,
      onClick: shuffleEnabled ? handleShuffle : undefined,
    },
    // Star values around the outside
    ...STAR_VALUES_CONFIG.map(({ value, position }) => ({
      id: `star-${value}`,
      coord: CLUSTER_7[position],
      content: (
        <StarHexContent
          value={value}
          count={starCounts?.[value]}
          isSelected={selectedStars === value}
          colors={colors}
        />
      ),
      onClick: () => handleStarClick(value),
    })),
  ];

  return (
    <div className="w-full h-full flex flex-col">
      {/* Nested hex clusters container */}
      <div className="flex-1 min-h-0 relative">
        {/* Outer ring - larger hexes */}
        <div className="absolute inset-0">
          <HexGrid
            items={outerItems}
            hexSize={50}
            borderColor="transparent"
            fitToParent={true}
            fitPadding={5}
          />
        </div>
        {/* Inner cluster - smaller hexes centered within the outer ring's empty center */}
        <div
          className="absolute"
          style={{
            left: '50%',
            top: '50%',
            transform: 'translate(-50%, -50%)',
            width: '32%',
            height: '32%',
          }}
        >
          <HexGrid
            items={innerItems}
            hexSize={12}
            gap={1}
            borderColor="transparent"
            fitToParent={true}
            fitPadding={0}
          />
        </div>
      </div>
    </div>
  );
});

export default MeasurementCluster;
