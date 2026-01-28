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
import type { PlayerModel } from '@/types/generated/models/player-model';
import type { GameModel } from '@/types/generated/models/game-model';
import { useGameStore } from '@/lib/stores/gameStore';
import { createPlayerColorsWithGradient, type PlayerColorsWithGradient } from '@/lib/utils/playerColors';
import { DEFAULT_PLAYER_COLORS, type PlayerProfile } from '@/types/player-profile';

// ============================================================================
// Types
// ============================================================================

export interface PlayersPanelProps {
  /** Game model containing players and current player */
  gameModel: GameModel | null;
}

/** Resource types tracked for cards */
type TrackedResourceType = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'goldMine' | 'robber';

/** Harbors owned by player (for 2:1 trade indicator) */
type OwnedHarbor = 'wheat' | 'wood' | 'sheep' | 'brick' | 'ore' | 'generic';

// ============================================================================
// Catan Font Glyphs (from PlayerTile.razor CatanGlyph class)
// ============================================================================

const CatanGlyph = {
  City: '\uE900',
  Laurel: '\uE907',
  Road: '\uE909',
  Pirate: '\uE90C',
  Settlement: '\uE926',
  Soldier: '\uE90E',
  Sum: '\uE910',
  BadRoll: '\uE913',
  GoodRoll: '\uE914',
  LongestRoad: '\uE915',
  Target: '\uE916',
  Star: '\uE911',
  DevCard: '?',
  Ship: '\uE90A',
};

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

const RESOURCE_CARD_CONFIG: { type: TrackedResourceType; image: string; label: string }[] = [
  { type: 'wheat', image: '/themes/base/resources/wheat.png', label: 'Wheat' },
  { type: 'wood', image: '/themes/base/resources/wood.png', label: 'Wood' },
  { type: 'sheep', image: '/themes/base/resources/sheep.png', label: 'Sheep' },
  { type: 'brick', image: '/themes/base/resources/brick.png', label: 'Brick' },
  { type: 'ore', image: '/themes/base/resources/ore.png', label: 'Ore' },
  { type: 'goldMine', image: '/themes/base/resources/goldmine.png', label: 'Gold' },
  { type: 'robber', image: '/themes/base/resources/robber.png', label: 'Robber' },
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

  const config = RESOURCE_CARD_CONFIG.find(c => c.type === resourceType);
  if (!config) return null;

  const isShowingFront = manualFlip !== null ? manualFlip : (autoFlip ? count > 0 : true);

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
        className="relative w-full h-full transition-transform duration-500"
        style={{
          transformStyle: 'preserve-3d',
          transform: isShowingFront ? 'rotateY(0deg)' : 'rotateY(180deg)',
        }}
      >
        {/* Front face */}
        <div
          className="absolute inset-0 rounded overflow-hidden shadow-md"
          style={{ backfaceVisibility: 'hidden' }}
        >
          <div
            className="w-full h-full bg-cover bg-center"
            style={{ backgroundImage: `url(${config.image})` }}
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
            style={{ backgroundImage: 'url(/themes/base/resources/back.png)' }}
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
        <FontAwesomeIcon
          icon={faIcon}
          className={className}
          style={{ color: colors.foreground }}
        />
      );
    }
    return (
      <span className={`font-catan ${className} ${bold ? 'font-bold' : ''}`}>{glyph}</span>
    );
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
  gameModel: GameModel;
  isCurrentPlayer: boolean;
  isSelected?: boolean;
  onClick?: () => void;
}

const PlayerTile = memo(function PlayerTile({
  player,
  profile,
  gameModel,
  isCurrentPlayer,
  isSelected,
  onClick,
}: PlayerTileProps) {
  const colors = createColorsFromProfile(profile);

  // Count player's buildings and roads from game model
  const roadCount = gameModel.roads.filter(r => r.ownerId === player.id).length;
  const settlements = gameModel.buildings.filter(
    b => b.ownerId === player.id && b.buildingState === 'Settlement'
  ).length;
  const cities = gameModel.buildings.filter(
    b => b.ownerId === player.id && b.buildingState === 'City'
  ).length;

  // Count soldiers from spent entitlements
  const soldierCount = player.spentEntitlementsThisGame.filter(
    e => e === 'Soldier'
  ).length;

  // Count dev cards
  const devCardCount = player.spentEntitlementsThisGame.filter(
    e => e === 'DevCard'
  ).length;

  // Get totals from ResourcesModel - match Blazor: r.Wheat + r.Wood + r.Sheep + r.Brick + r.Ore
  const r = player.resourcesThisGame;
  const totalResources = (r?.wheat ?? 0) + (r?.wood ?? 0) + (r?.sheep ?? 0) + (r?.brick ?? 0) + (r?.ore ?? 0);
  const robberLoss = 0; // TODO: Not implemented yet (matches Blazor)

  // 13 stats in display order (matching Blazor PlayerTile.razor)
  const stats: Omit<StatTileProps, 'colors'>[] = [
    { glyph: CatanGlyph.Laurel, count: player.score, isHighlighted: player.highestScore, isScore: true },
    { glyph: CatanGlyph.Road, count: roadCount, bold: true },
    { glyph: CatanGlyph.City, count: cities },
    { glyph: CatanGlyph.Settlement, count: settlements },
    { glyph: CatanGlyph.Soldier, count: soldierCount, isHighlighted: player.largestArmy },
    { faIcon: faReceipt, count: devCardCount },
    { glyph: CatanGlyph.Pirate, count: robberLoss },
    { glyph: CatanGlyph.Target, count: player.timesTargeted },
    { glyph: CatanGlyph.Sum, count: totalResources },
    { glyph: CatanGlyph.LongestRoad, count: player.longestRoad, isHighlighted: player.hasLongestRoad, bold: true },
    { glyph: CatanGlyph.GoodRoll, count: player.goodRolls },
    { glyph: CatanGlyph.BadRoll, count: player.badRolls },
    { glyph: CatanGlyph.Star, count: player.stars },
  ];

  // Get owned harbors from player model - extract harbor type from HarborKey
  const ownedHarbors: OwnedHarbor[] = (player.ownedHarbors || []).map(h => {
    // HarborKey has harborType property
    const harborType = h.harborType ?? '';
    const lower = harborType.toLowerCase();
    if (lower === 'wheat' || lower === 'wood' || lower === 'sheep' ||
        lower === 'brick' || lower === 'ore' || lower === 'generic') {
      return lower as OwnedHarbor;
    }
    return 'generic';
  });

  return (
    <div
      className={`p-1 rounded cursor-pointer transition-all ${isSelected || isCurrentPlayer ? 'ring-2 ring-amber-400' : 'hover:bg-white/5'}`}
      style={{
        backgroundColor: `${colors.primary}20`,
        borderLeft: `4px solid ${colors.primary}`,
      }}
      onClick={onClick}
    >
      {/* Row 1: Avatar + Stats Grid */}
      <div className="flex gap-0.5 items-start mb-0.5">
        {/* Avatar */}
        <div
          className="w-10 h-10 rounded-full bg-cover bg-center flex-shrink-0 flex items-center justify-center text-white font-bold"
          style={{
            backgroundImage: profile?.imageUri ? `url('http://localhost:8080${profile.imageUri}')` : undefined,
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
          const hasHarbor = type !== 'goldMine' && type !== 'robber' &&
            ownedHarbors.includes(type as OwnedHarbor);
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

interface ScaledPlayersListProps {
  gameModel: GameModel;
}

function ScaledPlayersList({ gameModel }: ScaledPlayersListProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(1);
  const naturalSizeRef = useRef({ width: 0, height: 0 });
  const hasMeasuredRef = useRef(false);

  // Get player profiles from store (reactive - updates when profiles change)
  const playerProfiles = useGameStore((state) => state.playerProfiles);

  // Combined effect: measure natural size and set up resize observer
  useEffect(() => {
    const measureAndScale = () => {
      if (!contentRef.current || !containerRef.current) return;

      // Only measure natural size once (or when players count changes)
      const playersCount = gameModel.players.length;
      if (!hasMeasuredRef.current || naturalSizeRef.current.width === 0) {
        // Temporarily reset scale to measure natural size
        const currentTransform = contentRef.current.style.transform;
        contentRef.current.style.transform = 'scale(1)';

        // Force layout
        void contentRef.current.offsetHeight;

        naturalSizeRef.current = {
          width: contentRef.current.scrollWidth,
          height: contentRef.current.scrollHeight,
        };

        // Restore transform
        contentRef.current.style.transform = currentTransform;
        hasMeasuredRef.current = true;
      }

      const { width: naturalWidth, height: naturalHeight } = naturalSizeRef.current;
      if (!naturalWidth || !naturalHeight) return;

      const containerWidth = containerRef.current.clientWidth;
      const containerHeight = containerRef.current.clientHeight;

      if (containerWidth > 0 && containerHeight > 0) {
        const widthScale = containerWidth / naturalWidth;
        const heightScale = containerHeight / naturalHeight;
        const newScale = Math.min(widthScale, heightScale); // Scale to fit container
        setScale(newScale > 0 ? newScale : 1);
      }
    };

    // Initial measurement
    measureAndScale();

    // Set up resize observer
    const resizeObserver = new ResizeObserver(() => {
      measureAndScale();
    });

    if (containerRef.current) {
      resizeObserver.observe(containerRef.current);
    }

    return () => resizeObserver.disconnect();
  }, [gameModel.players.length]); // Only re-run if player count changes

  return (
    <div
      ref={containerRef}
      className="w-full h-full overflow-hidden"
    >
      <div
        ref={contentRef}
        className="space-y-2 p-2 inline-block"
        style={{
          transform: `scale(${scale})`,
          transformOrigin: 'top left',
        }}
      >
        {gameModel.players.map(player => (
          <PlayerTile
            key={player.id}
            player={player}
            profile={playerProfiles.get(player.id)}
            gameModel={gameModel}
            isCurrentPlayer={player.id === gameModel.currentPlayerId}
          />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Main PlayersPanel Component
// ============================================================================

export function PlayersPanel({ gameModel }: PlayersPanelProps): React.ReactElement {
  if (!gameModel) {
    return (
      <div className="p-4 text-gray-400 text-center">
        Waiting for game data...
      </div>
    );
  }

  return <ScaledPlayersList gameModel={gameModel} />;
}

export default PlayersPanel;
