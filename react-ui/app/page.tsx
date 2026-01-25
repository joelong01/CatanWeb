'use client';

import {
  faGamepad,
  faFolderOpen,
  faUsers,
  faChartBar,
  faPlay,
  faDice,
} from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import { getServiceUrl } from '@/lib/config';
import {
  HexGrid,
  HexGridItem,
  HEX_LAYOUTS,
  CenterHex,
  MenuHex,
  WaterHex,
} from '@/components/hex-grid';

/**
 * Home page - main entry point for the Catan application.
 *
 * Uses hex grid layout with center branding and surrounding menu items.
 * Layout follows CLUSTER_7 pattern (center + 6 surrounding hexes).
 */
export default function Home(): React.ReactElement {
  // TODO: Get active game ID from connection service/store
  const activeGameId: string | null = null;

  // Build hex grid items
  const items: HexGridItem[] = [
    // Center: Catan branding
    {
      id: 'center',
      coord: HEX_LAYOUTS.CLUSTER_7[0], // (0, 0)
      content: (
        <CenterHex
          icon={faDice}
          title="Catan"
          accentColor="text-amber-400"
        />
      ),
      disabled: true,
    },

    // North: New Game
    {
      id: 'new-game',
      coord: HEX_LAYOUTS.CLUSTER_7[1], // (0, -1)
      content: (
        <MenuHex
          icon={faGamepad}
          title="New Game"
          href="/new-game"
          accentColor="text-amber-400"
        />
      ),
    },

    // NorthEast: Open Game
    {
      id: 'open-game',
      coord: HEX_LAYOUTS.CLUSTER_7[2], // (1, -1)
      content: (
        <MenuHex
          icon={faFolderOpen}
          title="Open Game"
          href="/load-game"
          accentColor="text-blue-400"
        />
      ),
    },

    // SouthEast: Edit Players
    {
      id: 'edit-players',
      coord: HEX_LAYOUTS.CLUSTER_7[3], // (1, 0)
      content: (
        <MenuHex
          icon={faUsers}
          title="Edit Players"
          href="/edit-players"
          accentColor="text-green-400"
        />
      ),
    },

    // South: Stats
    {
      id: 'stats',
      coord: HEX_LAYOUTS.CLUSTER_7[4], // (0, 1)
      content: (
        <MenuHex
          icon={faChartBar}
          title="Stats"
          href="/stats"
          accentColor="text-purple-400"
        />
      ),
    },

    // SouthWest: Water placeholder
    {
      id: 'water-sw',
      coord: HEX_LAYOUTS.CLUSTER_7[5], // (-1, 1)
      content: <WaterHex imageUrl="/water.png" showBorder opacity={0.6} />,
      disabled: true,
    },

    // NorthWest: Return to Game (if active) or Water
    {
      id: 'nw-slot',
      coord: HEX_LAYOUTS.CLUSTER_7[6], // (-1, 0)
      content: activeGameId ? (
        <MenuHex
          icon={faPlay}
          title="Return to"
          subtitle="Game"
          href={`/game/${activeGameId}`}
          accentColor="text-green-400"
        />
      ) : (
        <WaterHex imageUrl="/water.png" showBorder opacity={0.6} />
      ),
      disabled: !activeGameId,
    },
  ];

  return (
    <MainLayout activeGameId={activeGameId}>
      <div className="flex flex-col items-center justify-center min-h-[calc(100vh-120px)] py-8">
        {/* Hex Grid Menu - card background matching New Game page */}
        <div className="bg-white/5 rounded-xl p-8 border border-white/10">
          <HexGrid hexSize={140} items={items} gap={4} />
        </div>

        {/* Troubleshooting Section */}
        <div className="mt-8 px-4 py-3 bg-gray-800/50 rounded-lg text-center">
          <p className="text-sm text-gray-400">
            GameService: <code className="text-blue-400">{getServiceUrl()}</code>
          </p>
        </div>
      </div>
    </MainLayout>
  );
}
