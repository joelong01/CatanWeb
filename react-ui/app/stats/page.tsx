'use client';

import { useState, useEffect, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { gameApi } from '@/lib/api';
import { serviceConfig } from '@/lib/services/config';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';
import { faTrophy } from '@fortawesome/free-solid-svg-icons';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import type { PlayerProfile } from '@/types/player-profile';
import type { LifetimeStats } from '@/types/player-profile';

/** Display modes for the stat columns. */
type StatMode = 'count' | 'min' | 'max' | 'average';

const STAT_MODES: { value: StatMode; label: string }[] = [
  { value: 'count', label: 'Count' },
  { value: 'min', label: 'Min' },
  { value: 'max', label: 'Max' },
  { value: 'average', label: 'Average' },
];

/**
 * Gets the display value for a mode-sensitive stat column.
 * Returns the number to display, or null for unavailable data.
 */
function getStatValue(
  stats: LifetimeStats | undefined,
  column: 'score' | 'soldiers' | 'stars' | 'targeted' | 'robber',
  mode: StatMode
): number | null {
  if (!stats || stats.gamesPlayed === 0) return null;

  const t = stats.totals;

  switch (column) {
    case 'score':
      // Only max is available (highestScoreRecord). No min/total stored.
      if (mode === 'max') return stats.highestScoreRecord;
      return null;

    case 'soldiers':
      if (mode === 'count') return t.soldiersPlayed;
      if (mode === 'min') return sanitizeMin(stats.minSoldiersRecord);
      if (mode === 'max') return stats.mostSoldiersRecord;
      return stats.averageSoldiers;

    case 'stars':
      if (mode === 'count') return t.starsEarned;
      if (mode === 'min') return sanitizeMin(stats.minStarsRecord);
      if (mode === 'max') return stats.mostStarsRecord;
      return stats.averageStars;

    case 'targeted':
      if (mode === 'count') return t.timesTargeted;
      if (mode === 'min') return sanitizeMin(stats.minTargetedRecord);
      if (mode === 'max') return stats.mostTargetedRecord;
      return stats.averageTargeted;

    case 'robber':
      if (mode === 'count') return t.resourcesLostToRobber;
      if (mode === 'min') return sanitizeMin(stats.minRobberRecord);
      if (mode === 'max') return stats.mostRobberRecord;
      return stats.averageRobber;
  }
}

/** Returns null for int.MaxValue sentinel (no games played yet). */
function sanitizeMin(value: number): number | null {
  return value >= 2147483647 ? null : value;
}

/** Format a stat value for display. */
function formatValue(value: number | null, mode: StatMode): string {
  if (value === null) return '-';
  if (mode === 'average') return value.toFixed(1);
  return String(value);
}

/** Get initials from a player name. */
function getInitials(name: string | undefined): string {
  if (!name?.trim()) return '?';
  const parts = name.split(/\s+/);
  if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/**
 * Stats page - displays player leaderboard with lifetime statistics.
 */
export default function Stats(): React.ReactElement {
  const router = useRouter();
  const [players, setPlayers] = useState<PlayerProfile[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statMode, setStatMode] = useState<StatMode>('count');

  useEffect(() => {
    async function loadPlayers(): Promise<void> {
      try {
        setIsLoading(true);
        setError(null);
        const result = await gameApi.getPlayers();
        if (result.success && result.data) {
          setPlayers(result.data);
        } else {
          setError(result.error ?? 'Failed to load players');
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load players');
      } finally {
        setIsLoading(false);
      }
    }
    loadPlayers();
  }, []);

  /** Sort players: wins desc, win rate desc, name asc. */
  const sortedPlayers = useMemo(() => {
    return [...players].sort((a, b) => {
      const aWins = a.lifetimeStats?.wins ?? 0;
      const bWins = b.lifetimeStats?.wins ?? 0;
      if (bWins !== aWins) return bWins - aWins;

      const aRate = a.lifetimeStats?.winRate ?? 0;
      const bRate = b.lifetimeStats?.winRate ?? 0;
      if (bRate !== aRate) return bRate - aRate;

      return a.name.localeCompare(b.name);
    });
  }, [players]);

  return (
    <MainLayout title="Player Statistics" onBack={() => router.push('/')}>
      <div className="px-4 pb-8 md:px-8 max-w-6xl mx-auto">
        <div className="flex items-center justify-end mb-5">
          {/* Mode selector */}
          <div className="flex rounded-lg overflow-hidden border border-white/20">
            {STAT_MODES.map(({ value, label }) => (
              <button
                type="button"
                key={value}
                onClick={() => setStatMode(value)}
                className={`px-3 py-1.5 text-sm font-medium transition-colors ${
                  statMode === value
                    ? 'bg-amber-500 text-black'
                    : 'bg-white/5 text-white/70 hover:bg-white/10 hover:text-white'
                }`}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        {isLoading && (
          <div className="text-center py-10 text-white/60 text-lg">
            Loading player statistics...
          </div>
        )}

        {error && <div className="text-center py-10 text-red-400 text-lg">{error}</div>}

        {!isLoading && !error && (
          <div className="overflow-x-auto rounded-lg shadow-lg">
            <table className="w-full border-collapse text-sm">
              <thead>
                <tr className="bg-[#333]">
                  <th className="text-left px-3 py-3 text-white text-xs whitespace-nowrap sticky left-0 bg-[#333] z-10">
                    Player
                  </th>
                  <th className="px-3 py-3 text-white text-xs whitespace-nowrap text-center">
                    Games
                  </th>
                  <th className="px-3 py-3 text-amber-400 text-xs whitespace-nowrap text-center">
                    Wins
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Longest Road Wins"
                    aria-label="Longest Road Wins"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.LongestRoad}</span>
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Largest Army Wins"
                    aria-label="Largest Army Wins"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.LargestArmy}</span>
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Score"
                    aria-label="Score"
                  >
                    <FontAwesomeIcon icon={faTrophy} className="text-amber-400" />
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Soldiers"
                    aria-label="Soldiers"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.LargestArmy}</span>
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Stars"
                    aria-label="Stars"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.Star}</span>
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Times Targeted"
                    aria-label="Times Targeted"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.Target}</span>
                  </th>
                  <th
                    className="px-3 py-3 text-white text-xs whitespace-nowrap text-center"
                    title="Robber Losses"
                    aria-label="Robber Losses"
                  >
                    <span className="font-catan text-lg">{CatanGlyph.Robber}</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {sortedPlayers.map((player) => {
                  const stats = player.lifetimeStats;
                  const fg = player.colors.foreground;
                  const gradient = `linear-gradient(135deg, ${player.colors.primary}, ${player.colors.secondary}, #000)`;

                  return (
                    <tr key={player.id} style={{ background: gradient }}>
                      {/* Player name (sticky) */}
                      <td
                        className="px-3 py-2 sticky left-0 z-[1]"
                        style={{ color: fg, background: 'inherit' }}
                      >
                        <div className="flex items-center gap-2">
                          <div
                            className="w-8 h-8 min-w-[32px] rounded-full overflow-hidden border-2 flex-shrink-0"
                            style={{ borderColor: fg }}
                          >
                            {player.imageUri ? (
                              <img
                                src={`${serviceConfig.serviceUrl}${player.imageUri}`}
                                alt={player.name}
                                className="w-full h-full object-cover"
                              />
                            ) : (
                              <span
                                className="flex items-center justify-center w-full h-full text-[11px] font-bold bg-black/20"
                                style={{ color: fg }}
                              >
                                {getInitials(player.name)}
                              </span>
                            )}
                          </div>
                          <span className="font-bold text-[13px] whitespace-nowrap">
                            {(stats?.wins ?? 0) > 0 && (
                              <FontAwesomeIcon icon={faTrophy} className="mr-1 text-amber-400" />
                            )}
                            {player.name}
                          </span>
                        </div>
                      </td>

                      {/* Always-count columns */}
                      <StatCell value={stats?.gamesPlayed ?? 0} fg={fg} />
                      <StatCell value={stats?.wins ?? 0} fg={fg} gold />
                      <StatCell value={stats?.longestRoadWins ?? 0} fg={fg} />
                      <StatCell value={stats?.largestArmyWins ?? 0} fg={fg} />

                      {/* Mode-sensitive columns */}
                      <StatCell
                        value={formatValue(getStatValue(stats, 'score', statMode), statMode)}
                        fg={fg}
                      />
                      <StatCell
                        value={formatValue(getStatValue(stats, 'soldiers', statMode), statMode)}
                        fg={fg}
                      />
                      <StatCell
                        value={formatValue(getStatValue(stats, 'stars', statMode), statMode)}
                        fg={fg}
                      />
                      <StatCell
                        value={formatValue(getStatValue(stats, 'targeted', statMode), statMode)}
                        fg={fg}
                      />
                      <StatCell
                        value={formatValue(getStatValue(stats, 'robber', statMode), statMode)}
                        fg={fg}
                      />
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </MainLayout>
  );
}

/** Simple stat cell for the table. */
function StatCell({
  value,
  fg,
  gold,
}: {
  value: number | string;
  fg: string;
  gold?: boolean;
}): React.ReactElement {
  return (
    <td
      className="px-3 py-2 text-center font-bold text-base"
      style={{ color: gold ? '#ffd700' : fg }}
    >
      {value}
    </td>
  );
}
