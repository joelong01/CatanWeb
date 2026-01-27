'use client';

/**
 * GameResourcesHeader - Total game resources with flippable cards.
 *
 * Shows the aggregate resources collected by all players across the game.
 * Each resource is displayed as a flippable card that auto-flips when
 * count > 0. Right-click to manually flip.
 *
 * Matches Blazor: WebUI/Components/Players/GameResourcesHeader.razor
 */

import { memo, useState, useRef, useEffect } from 'react';
import type { ResourcesModel } from '@/types/generated/models/resources-model';

// ============================================================================
// Types
// ============================================================================

export interface GameResourcesHeaderProps {
  /** Game resources model containing totals */
  resources: ResourcesModel | null;
}

/** Resource types to track (matching Blazor implementation) */
type TrackedResourceType = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'goldMine' | 'robber';

// ============================================================================
// Constants
// ============================================================================

/** Resource card configuration with images */
const RESOURCE_CARD_CONFIG: { type: TrackedResourceType; image: string; label: string }[] = [
  { type: 'wheat', image: '/themes/base/resources/wheat.png', label: 'Wheat' },
  { type: 'wood', image: '/themes/base/resources/wood.png', label: 'Wood' },
  { type: 'sheep', image: '/themes/base/resources/sheep.png', label: 'Sheep' },
  { type: 'brick', image: '/themes/base/resources/brick.png', label: 'Brick' },
  { type: 'ore', image: '/themes/base/resources/ore.png', label: 'Ore' },
  { type: 'goldMine', image: '/themes/base/resources/goldmine.png', label: 'Gold' },
  { type: 'robber', image: '/themes/base/resources/robber.png', label: 'Robber' },
];

// ============================================================================
// ResourceCard - Flippable card component
// ============================================================================

interface ResourceCardProps {
  resourceType: TrackedResourceType;
  count: number;
  /** Auto flip: show front when count > 0, back when count = 0 */
  autoFlip?: boolean;
}

const ResourceCard = memo(function ResourceCard({
  resourceType,
  count,
  autoFlip = true,
}: ResourceCardProps) {
  const [manualFlip, setManualFlip] = useState<boolean | null>(null);

  // Find the config for this resource
  const config = RESOURCE_CARD_CONFIG.find(c => c.type === resourceType);
  if (!config) return null;

  // Determine if showing front
  const isShowingFront = manualFlip !== null ? manualFlip : (autoFlip ? count > 0 : true);

  // Right-click to manually flip
  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setManualFlip(!isShowingFront);
  };

  // Smaller cards for header: 40px × 60px (scaled from Blazor's 71px × 100px)
  return (
    <div
      className="relative cursor-pointer"
      style={{ width: '40px', height: '60px', perspective: '500px' }}
      onContextMenu={handleContextMenu}
      title={`${config.label}: ${count} (right-click to flip)`}
    >
      {/* Flip container */}
      <div
        className="relative w-full h-full transition-transform duration-500"
        style={{
          transformStyle: 'preserve-3d',
          transform: isShowingFront ? 'rotateY(0deg)' : 'rotateY(180deg)',
        }}
      >
        {/* Front face - resource image with count */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{ backfaceVisibility: 'hidden' }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: `url(${config.image})` }}
          />
          {/* Count badge at bottom */}
          <div className="absolute bottom-0.5 left-0 right-0 flex justify-center">
            <span
              className="px-1.5 py-0.5 rounded text-white font-bold text-sm leading-none"
              style={{ background: 'rgba(0, 0, 0, 0.85)' }}
            >
              {count}
            </span>
          </div>
        </div>

        {/* Back face - card back */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: 'url(/themes/base/resources/back.png)' }}
          />
        </div>
      </div>
    </div>
  );
});

// ============================================================================
// ScaledResourcesList - Scales content to fit container
// ============================================================================

interface ScaledResourcesListProps {
  resources: ResourcesModel;
}

function ScaledResourcesList({ resources }: ScaledResourcesListProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  const [naturalSize, setNaturalSize] = useState({ width: 0, height: 0 });

  // Measure natural content size once on mount
  useEffect(() => {
    if (contentRef.current) {
      contentRef.current.style.transform = 'scale(1)';
      const width = contentRef.current.scrollWidth;
      const height = contentRef.current.scrollHeight;
      setNaturalSize({ width, height });
    }
  }, []);

  // Update scale when container resizes
  useEffect(() => {
    if (!naturalSize.width || !naturalSize.height) return;

    const updateScale = () => {
      if (containerRef.current) {
        const containerWidth = containerRef.current.clientWidth;
        const containerHeight = containerRef.current.clientHeight;

        const widthScale = containerWidth / naturalSize.width;
        const heightScale = containerHeight / naturalSize.height;

        const newScale = Math.min(widthScale, heightScale);
        setScale(newScale > 0 ? newScale : 1);
      }
    };

    updateScale();

    const resizeObserver = new ResizeObserver(updateScale);
    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }

    return () => resizeObserver.disconnect();
  }, [naturalSize]);

  return (
    <div
      ref={containerRef}
      className="w-full h-full overflow-hidden"
    >
      <div
        ref={contentRef}
        className="p-2 inline-block"
        style={{
          transform: `scale(${scale})`,
          transformOrigin: 'top left',
        }}
      >
        <div className="flex flex-row gap-1 justify-center">
          {RESOURCE_CARD_CONFIG.map(({ type }) => (
            <ResourceCard
              key={type}
              resourceType={type}
              count={resources[type] ?? 0}
              autoFlip={true}
            />
          ))}
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// GameResourcesHeader - Main component
// ============================================================================

export const GameResourcesHeader = memo(function GameResourcesHeader({
  resources,
}: GameResourcesHeaderProps): React.ReactElement {
  if (!resources) {
    return (
      <div className="p-2 text-gray-400 text-center text-sm">
        Loading resources...
      </div>
    );
  }

  return <ScaledResourcesList resources={resources} />;
});

export default GameResourcesHeader;
