'use client';

/**
 * PlayersPanel - Shows player tiles with 13 stats and resource cards.
 *
 * Ported from controls-test/page.tsx implementation.
 * Displays each player's stats using Catan font glyphs and shows
 * resources gained this turn as flippable cards.
 *
 * Matches Blazor: WebUI/Components/Players/PlayerTile.razor
 */

import { memo, useState, useRef, useEffect, ReactNode } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faReceipt } from '@fortawesome/free-solid-svg-icons';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { useAssetPath } from '@/lib/theme';
import type { AssetName } from '@/lib/theme/types';
import type { PlayerModel } from '@/types/generated/models/player-model';
import { usePlayers, useCurrentTurnPlayerId, usePlayerProfiles } from '@/lib/stores/gameStoreHooks';
import { getServiceUrl } from '@/lib/config';
import {
  createPlayerColorsWithGradient,
  type PlayerColorsWithGradient,
} from '@/lib/utils/playerColors';
import { DEFAULT_PLAYER_COLORS, type PlayerProfile } from '@/types/player-profile';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';

// ============================================================================
// Types
// ============================================================================

export interface PlayersPanelProps {
  /** @deprecated - PlayersPanel now uses internal hooks. This prop is ignored. */
  gameModel?: unknown;
}

/** Resource types tracked for cards */
type TrackedResourceType = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'goldMine' | 'robber';

/** Harbors owned by player (for 2:1 trade indicator) */
type OwnedHarbor = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'generic';

// ============================================================================
// Catan Font Glyphs (from PlayerTile.razor CatanGlyph class)
// ============================================================================

// ============================================================================
// Player Colors Helper
// ============================================================================

/** Create PlayerColors with gradient from profile */
function createColorsFromProfile(profile: PlayerProfile | undefined): PlayerColorsWithGradient {
  const colors = profile?.colors ?? DEFAULT_PLAYER_COLORS;
  return createPlayerColorsWithGradient(colors);
}

// ============================================================================
// Resource Card Component (flippable cards for ResourcesThisTurn)
// ============================================================================

/** Map tracked resource types to theme asset names */
const RESOURCE_TO_ASSET: Record<TrackedResourceType, AssetName> = {
  wheat: 'CardWheat',
  wood: 'CardWood',
  sheep: 'CardSheep',
  brick: 'CardBrick',
  ore: 'CardOre',
  goldMine: 'CardGoldMine',
  robber: 'CardRobber',
};

const RESOURCE_CARD_CONFIG: { type: TrackedResourceType; label: string }[] = [
  { type: 'wheat', label: 'Wheat' },
  { type: 'wood', label: 'Wood' },
  { type: 'sheep', label: 'Sheep' },
  { type: 'brick', label: 'Brick' },
  { type: 'ore', label: 'Ore' },
  { type: 'goldMine', label: 'Gold' },
  { type: 'robber', label: 'Robber' },
];

interface ResourceCardProps {
  resourceType: TrackedResourceType;
  count: number;
  hasHarbor?: boolean;
  autoFlip?: boolean;
}

const ResourceCard = memo(function ResourceCard({
  resourceType,
  count,
  hasHarbor = false,
  autoFlip = true,
}: ResourceCardProps) {
  const [manualFlip, setManualFlip] = useState<boolean | null>(null);

  // Theme-resolved asset paths
  const imagePath = useAssetPath(RESOURCE_TO_ASSET[resourceType]);
  const cardBackPath = useAssetPath('CardBack');

  const config = RESOURCE_CARD_CONFIG.find((c) => c.type === resourceType);
  if (!config) return null;

  const isShowingFront = manualFlip !== null ? manualFlip : autoFlip ? count > 0 : true;

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();
    setManualFlip(!isShowingFront);
  };

  return (
    <div
      className="relative cursor-pointer"
      style={{ width: '71px', height: '100px', perspective: '500px' }}
      onContextMenu={handleContextMenu}
      title={`${config.label}: ${count} (right-click to flip)`}
    >
      <div
        className="relative w-full h-full transition-transform"
        style={{
          transformStyle: 'preserve-3d',
          transform: isShowingFront ? 'rotateY(0deg)' : 'rotateY(180deg)',
          transitionDuration: 'var(--animation-slow)',
        }}
      >
        {/* Front face */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{ backfaceVisibility: 'hidden' }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: imagePath ? `url(${imagePath})` : undefined }}
          />
          <div className="absolute bottom-1 left-0 right-0 flex justify-center">
            <span
              className="px-2 py-1 rounded text-white font-bold text-lg leading-none"
              style={{ background: 'rgba(0, 0, 0, 0.85)' }}
            >
              {count}
            </span>
          </div>
          {hasHarbor && (
            <div className="absolute top-1 right-1 w-6 h-6 rounded-full bg-black flex items-center justify-center">
              <span className="font-catan text-white text-base">{'\uE90D'}</span>
            </div>
          )}
        </div>

        {/* Back face */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: cardBackPath ? `url(${cardBackPath})` : undefined }}
          />
        </div>
      </div>
    </div>
  );
});

// ============================================================================
// Stat Tile Component
// ============================================================================

interface StatTileProps {
  glyph?: string;
  faIcon?: IconDefinition;
  count: number;
  isHighlighted?: boolean;
  isScore?: boolean;
  colors: PlayerColorsWithGradient;
  bold?: boolean;
}

const StatTile = memo(function StatTile({
  glyph,
  faIcon,
  count,
  isHighlighted = false,
  isScore = false,
  colors,
  bold = false,
}: StatTileProps) {
  const bgStyle = isHighlighted
    ? { background: colors.cssGradient }
    : { background: colors.primary };

  const tileSize = 'w-[35px] h-[35px]';

  const renderIcon = (className: string): ReactNode => {
    if (faIcon) {
      return (
        <FontAwesomeIcon icon={faIcon} className={className} style={{ color: colors.foreground }} />
      );
    }
    return <span className={`font-catan ${className} ${bold ? 'font-bold' : ''}`}>{glyph}</span>;
  };

  if (isScore) {
    return (
      <div
        className={`${tileSize} rounded-md flex items-center justify-center relative`}
        style={{ ...bgStyle, color: colors.foreground }}
      >
        <span className="font-catan text-[32px] absolute leading-none">{glyph}</span>
        <span className="font-bold text-lg z-10">{count}</span>
      </div>
    );
  }

  return (
    <div
      className={`${tileSize} rounded-md flex flex-col items-center justify-center`}
      style={{ ...bgStyle, color: colors.foreground }}
    >
      {renderIcon('text-base leading-none')}
      <span className="text-xs font-bold leading-none mt-0.5">{count}</span>
    </div>
  );
});

// ============================================================================
// Player Tile Component
// ============================================================================

interface PlayerTileProps {
  player: PlayerModel;
  profile: PlayerProfile | undefined;
  isCurrentPlayer: boolean;
}

const PlayerTile = memo(function PlayerTile({ player, profile, isCurrentPlayer }: PlayerTileProps) {
  const colors = createColorsFromProfile(profile);

  // Count from spentEntitlementsThisGame - these are placed items
  const spentEntitlements = player.spentEntitlementsThisGame ?? [];
  const roadCount = spentEntitlements.filter((e) => e === 'Road').length;
  const settlements = spentEntitlements.filter((e) => e === 'Settlement').length;
  const cities = spentEntitlements.filter((e) => e === 'City').length;

  // Count soldiers from spent entitlements
  const soldierCount = spentEntitlements.filter((e) => e === 'Soldier').length;

  // Count dev cards
  const devCardCount = spentEntitlements.filter((e) => e === 'DevCard').length;

  // Get totals from ResourcesModel - match Blazor: r.Wheat + r.Wood + r.Sheep + r.Brick + r.Ore
  const r = player.resourcesThisGame;
  const totalResources =
    (r?.wheat ?? 0) + (r?.wood ?? 0) + (r?.sheep ?? 0) + (r?.brick ?? 0) + (r?.ore ?? 0);
  const robberLoss = r?.robber ?? 0;

  // 13 stats in display order (matching Blazor PlayerTile.razor)
  const stats: Omit<StatTileProps, 'colors'>[] = [
    {
      glyph: CatanGlyph.Laurel,
      count: player.score,
      isHighlighted: player.highestScore,
      isScore: true,
    },
    { glyph: CatanGlyph.Road, count: roadCount, bold: true },
    { glyph: CatanGlyph.City, count: cities },
    { glyph: CatanGlyph.Settlement, count: settlements },
    { glyph: CatanGlyph.Soldier, count: soldierCount, isHighlighted: player.largestArmy },
    { faIcon: faReceipt, count: devCardCount },
    { glyph: CatanGlyph.Pirate, count: robberLoss },
    { glyph: CatanGlyph.Target, count: player.timesTargeted },
    { glyph: CatanGlyph.Sum, count: totalResources },
    {
      glyph: CatanGlyph.LongestRoad,
      count: player.longestRoad,
      isHighlighted: player.hasLongestRoad,
      bold: true,
    },
    { glyph: CatanGlyph.GoodRoll, count: player.goodRolls },
    { glyph: CatanGlyph.BadRoll, count: player.badRolls },
    { glyph: CatanGlyph.Star, count: player.stars },
  ];

  // Get owned harbors from player model - extract harbor type from HarborKey
  const ownedHarbors: OwnedHarbor[] = (player.ownedHarbors || []).map((h) => {
    // HarborKey has harborType property
    const harborType = h.harborType ?? '';
    const lower = harborType.toLowerCase();
    if (
      lower === 'wheat' ||
      lower === 'wood' ||
      lower === 'sheep' ||
      lower === 'brick' ||
      lower === 'ore' ||
      lower === 'generic'
    ) {
      return lower as OwnedHarbor;
    }
    return 'generic';
  });

  return (
    <div
      className={`p-1 rounded transition-all ${isCurrentPlayer ? 'ring-2 ring-amber-400' : 'hover:bg-white/5'}`}
      style={{
        backgroundColor: `${colors.primary}20`,
        borderLeft: `4px solid ${colors.primary}`,
      }}
    >
      {/* Row 1: Avatar + Stats Grid */}
      <div className="flex gap-0.5 items-start mb-0.5">
        {/* Avatar */}
        <div
          className="w-10 h-10 rounded-full bg-cover bg-center flex-shrink-0 flex items-center justify-center text-white font-bold"
          style={{
            backgroundImage: profile?.imageUri
              ? `url('${getServiceUrl()}${profile.imageUri}')`
              : undefined,
            border: `1px solid ${colors.foreground}`,
            backgroundColor: colors.primary,
          }}
        >
          {!profile?.imageUri && player.name.charAt(0).toUpperCase()}
        </div>

        {/* Stats Grid */}
        <div className="flex gap-px">
          {stats.map((stat, i) => (
            <StatTile
              key={i}
              glyph={stat.glyph}
              faIcon={stat.faIcon}
              count={stat.count}
              isHighlighted={stat.isHighlighted}
              isScore={stat.isScore}
              colors={colors}
              bold={stat.bold}
            />
          ))}
        </div>
      </div>

      {/* Row 2: Resources This Turn */}
      <div className="flex gap-0.5 mt-1">
        {RESOURCE_CARD_CONFIG.map(({ type }) => {
          const count = player.resourcesThisTurn?.[type] ?? 0;
          const hasHarbor =
            type !== 'goldMine' && type !== 'robber' && ownedHarbors.includes(type as OwnedHarbor);
          return (
            <ResourceCard
              key={type}
              resourceType={type}
              count={count}
              hasHarbor={hasHarbor}
              autoFlip={true}
            />
          );
        })}
      </div>
    </div>
  );
});

// ============================================================================
// Scaled Players List (scales content to fit container)
// ============================================================================

function ScaledPlayersList() {
  const containerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);

  // Get data from store via hooks (optimized for re-render performance)
  // Note: PlayerTile only needs player + profile - counts come from player.spentEntitlementsThisGame
  const players = usePlayers();
  const currentPlayerId = useCurrentTurnPlayerId();
  const playerProfiles = usePlayerProfiles();

  // Measure content and container, compute scale to fit
  useEffect(() => {
    const container = containerRef.current;
    const content = contentRef.current;
    if (!container || !content) return;

    const updateScale = () => {
      // scrollWidth/scrollHeight are layout dimensions, unaffected by CSS transforms.
      // No need to temporarily reset scale — transform: scale() never changes layout.
      const naturalWidth = content.scrollWidth;
      const naturalHeight = content.scrollHeight;
      if (!naturalWidth || !naturalHeight) return;

      const containerWidth = container.clientWidth;
      const containerHeight = container.clientHeight;
      if (containerWidth <= 0 || containerHeight <= 0) return;

      const newScale = Math.min(containerWidth / naturalWidth, containerHeight / naturalHeight);
      setScale(newScale > 0 ? newScale : 1);
    };

    // Observe both: container (panel resize) and content (player count/layout changes)
    const observer = new ResizeObserver(updateScale);
    observer.observe(container);
    observer.observe(content);

    // Defer initial measurement to ensure DOM is fully laid out
    requestAnimationFrame(updateScale);

    return () => observer.disconnect();
  }, [players.length]);

  return (
    <div ref={containerRef} className="w-full h-full overflow-hidden">
      <div
        ref={contentRef}
        className="space-y-2 p-2 inline-block"
        style={{
          transform: `scale(${scale})`,
          transformOrigin: 'top left',
        }}
      >
        {players.map((player) => (
          <PlayerTile
            key={player.id}
            player={player}
            profile={playerProfiles.get(player.id)}
            isCurrentPlayer={player.id === currentPlayerId}
          />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Main PlayersPanel Component
// ============================================================================

export function PlayersPanel(_props: PlayersPanelProps): React.ReactElement {
  // Get players from store to check if data is loaded
  const players = usePlayers();

  if (!players || players.length === 0) {
    return <div className="p-4 text-gray-400 text-center">Waiting for game data...</div>;
  }

  return <ScaledPlayersList />;
}

export default PlayersPanel;
