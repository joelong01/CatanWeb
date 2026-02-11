'use client';

import { useState, useMemo, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTableCells, faList } from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import { CatanGlyph, type CatanGlyphKey } from '@/lib/constants/catanGlyphs';

type ViewMode = 'grid' | 'table';

interface GlyphEntry {
  key: string;
  char: string;
  hex: string;
  decimal: number;
}

interface GlyphCategory {
  label: string;
  entries: GlyphEntry[];
}

/** Category definitions -- display order determines page order. */
const GLYPH_CATEGORIES: { label: string; keys: string[] }[] = [
  {
    label: 'Buildings & Roads',
    keys: ['City', 'Settlement', 'Road'],
  },
  {
    label: 'Resource Tiles',
    keys: [
      'BrickHex',
      'DesertHex',
      'GoldHex',
      'TempGoldHex',
      'OreHex',
      'SheepHex',
      'WheatHex',
      'WoodHex',
    ],
  },
  {
    label: 'Harbors',
    keys: [
      'ThreeToOneHarbor',
      'BrickHarbor',
      'OreHarbor',
      'SheepHarbor',
      'WheatHarbor',
      'WoodHarbor',
    ],
  },
  {
    label: 'Catan Numbers (E945–E95F)',
    keys: [
      'Number2',
      'Number3',
      'Number4',
      'Number5',
      'Number6',
      'Number7',
      'Number8',
      'Number9',
      'Number10',
      'Number11',
      'Number12',
    ],
  },
  {
    label: 'Knights & Cities',
    keys: [
      'Knight',
      'KnightAttacking',
      'KnightKneeling',
      'KnightStanding',
      'Deserter',
      'Diplomat',
      'Inventor',
      'Merchant',
      'Politics',
      'Intrigue',
      'Science',
      'Entry',
      'Metro',
      'Wagon',
    ],
  },
  {
    label: 'Stats & Scoring',
    keys: [
      'Laurel',
      'Star',
      'Sum',
      'BadRoll',
      'GoodRoll',
      'LargestArmy',
      'LongestRoad',
      'Target',
      'Check',
    ],
  },
  {
    label: 'Game Elements',
    keys: ['Ship', 'Robber', 'PirateShip', 'SolidShield', 'Skull'],
  },
];

/** Aliases to exclude from display. */
const ALIASES = new Set(['Soldier', 'Pirate', 'DevCard']);

/** Build a single GlyphEntry from a key+char pair. */
function makeEntry(key: string, char: string): GlyphEntry {
  const codepoint = char.codePointAt(0) ?? 0;
  return {
    key,
    char,
    hex: codepoint.toString(16).toUpperCase().padStart(4, '0'),
    decimal: codepoint,
  };
}

/** Build display entries grouped by category. */
function getGroupedEntries(): GlyphCategory[] {
  const glyphMap = new Map(
    (Object.entries(CatanGlyph) as [CatanGlyphKey, string][]).filter(([key]) => !ALIASES.has(key))
  );

  const usedKeys = new Set<string>();
  const categories: GlyphCategory[] = [];

  for (const cat of GLYPH_CATEGORIES) {
    const entries: GlyphEntry[] = [];
    for (const key of cat.keys) {
      const char = glyphMap.get(key as CatanGlyphKey);
      if (char) {
        entries.push(makeEntry(key, char));
        usedKeys.add(key);
      }
    }
    if (entries.length > 0) {
      categories.push({ label: cat.label, entries });
    }
  }

  // Catch any glyphs not assigned to a category
  const uncategorized: GlyphEntry[] = [];
  for (const [key, char] of glyphMap) {
    if (!usedKeys.has(key)) {
      uncategorized.push(makeEntry(key, char));
    }
  }
  if (uncategorized.length > 0) {
    uncategorized.sort((a, b) => a.decimal - b.decimal);
    categories.push({ label: 'Other', entries: uncategorized });
  }

  return categories;
}

export default function FontViewerPage(): React.ReactElement {
  const router = useRouter();
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const categories = useMemo(() => getGroupedEntries(), []);
  const totalCount = useMemo(
    () => categories.reduce((sum, cat) => sum + cat.entries.length, 0),
    [categories]
  );

  const handleCopy = useCallback(async (entry: GlyphEntry, e: React.MouseEvent) => {
    e.preventDefault();
    try {
      await navigator.clipboard.writeText(entry.char);
      setCopiedKey(entry.key);
      setTimeout(() => setCopiedKey(null), 1500);
    } catch {
      // Clipboard API may fail in insecure contexts -- silently ignore
    }
  }, []);

  return (
    <MainLayout
      title="Catan Font Viewer"
      subtitle={`${totalCount} glyphs · Right-click to copy`}
      onBack={() => router.push('/')}
    >
      <div className="pb-8 px-4 sm:px-6">
        <div className="max-w-6xl mx-auto">
          {/* View toggle */}
          <div className="flex justify-end mb-6">
            <div className="flex gap-1 bg-white/5 rounded-lg p-1">
              <button
                onClick={() => setViewMode('grid')}
                className={`px-3 py-1.5 rounded text-sm transition-colors ${
                  viewMode === 'grid'
                    ? 'bg-amber-400 text-gray-900 font-semibold'
                    : 'text-gray-400 hover:text-white'
                }`}
              >
                <FontAwesomeIcon icon={faTableCells} className="mr-1.5" />
                Grid
              </button>
              <button
                onClick={() => setViewMode('table')}
                className={`px-3 py-1.5 rounded text-sm transition-colors ${
                  viewMode === 'table'
                    ? 'bg-amber-400 text-gray-900 font-semibold'
                    : 'text-gray-400 hover:text-white'
                }`}
              >
                <FontAwesomeIcon icon={faList} className="mr-1.5" />
                Table
              </button>
            </div>
          </div>

          {/* Grid View */}
          {viewMode === 'grid' && (
            <div className="space-y-8">
              {categories.map((cat) => (
                <section key={cat.label}>
                  <h2 className="text-lg font-semibold text-amber-400/80 mb-3 border-b border-white/10 pb-2">
                    {cat.label}
                  </h2>
                  <div className="grid grid-cols-[repeat(auto-fill,minmax(160px,1fr))] gap-3">
                    {cat.entries.map((entry) => (
                      <div
                        key={entry.key}
                        className="bg-white/5 border border-white/10 rounded-lg p-4 text-center
                                   hover:border-amber-400/50 transition-colors cursor-context-menu relative"
                        onContextMenu={(e) => handleCopy(entry, e)}
                      >
                        {/* Glyph with bounding rectangle */}
                        <div className="h-20 flex items-center justify-center mb-3">
                          <span
                            className="font-catan text-6xl leading-none text-white
                                       inline-block border border-dashed border-red-500"
                          >
                            {entry.char}
                          </span>
                        </div>
                        <div className="text-sm font-semibold text-amber-400 mb-1 break-words">
                          {entry.key}
                        </div>
                        <div className="text-xs text-gray-500 font-mono">U+{entry.hex}</div>
                        <div className="text-xs text-gray-600 font-mono">{entry.decimal}</div>
                        {/* Copied feedback */}
                        {copiedKey === entry.key && (
                          <div
                            className="absolute inset-0 flex items-center justify-center
                                          bg-black/70 rounded-lg text-green-400 font-semibold text-sm
                                          animate-pulse"
                          >
                            Copied!
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          )}

          {/* Table View */}
          {viewMode === 'table' && (
            <div className="overflow-x-auto rounded-lg border border-white/10">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-white/5 text-left">
                    <th className="px-4 py-3 text-amber-400 font-semibold">Glyph</th>
                    <th className="px-4 py-3 text-amber-400 font-semibold">Name</th>
                    <th className="px-4 py-3 text-amber-400 font-semibold">Hex</th>
                    <th className="px-4 py-3 text-amber-400 font-semibold">Decimal</th>
                    <th className="px-4 py-3 text-amber-400 font-semibold">CSS</th>
                    <th className="px-4 py-3 text-amber-400 font-semibold">JS/TS</th>
                  </tr>
                </thead>
                <tbody>
                  {categories.map((cat) => (
                    <>
                      {/* Category header row */}
                      <tr key={`cat-${cat.label}`} className="bg-white/[0.03]">
                        <td
                          colSpan={6}
                          className="px-4 py-2 text-amber-400/80 font-semibold text-xs uppercase tracking-wider"
                        >
                          {cat.label}
                        </td>
                      </tr>
                      {cat.entries.map((entry) => (
                        <tr
                          key={entry.key}
                          className="border-t border-white/5 hover:bg-white/5 transition-colors
                                     cursor-context-menu relative"
                          onContextMenu={(e) => handleCopy(entry, e)}
                        >
                          <td className="px-4 py-3 text-center w-16 relative">
                            <span
                              className="font-catan text-3xl text-white
                                         inline-block border border-dashed border-red-500"
                            >
                              {entry.char}
                            </span>
                            {copiedKey === entry.key && (
                              <span
                                className="absolute inset-0 flex items-center justify-center
                                              bg-black/70 text-green-400 font-semibold text-xs
                                              animate-pulse"
                              >
                                Copied!
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-gray-200">{entry.key}</td>
                          <td className="px-4 py-3 font-mono text-gray-400">U+{entry.hex}</td>
                          <td className="px-4 py-3 font-mono text-gray-400">{entry.decimal}</td>
                          <td className="px-4 py-3 font-mono text-gray-500 text-xs">
                            {`content: '\\${entry.hex}';`}
                          </td>
                          <td className="px-4 py-3 font-mono text-gray-500 text-xs">
                            {`'\\u${entry.hex}'`}
                          </td>
                        </tr>
                      ))}
                    </>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </MainLayout>
  );
}
