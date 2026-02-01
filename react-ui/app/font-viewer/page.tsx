'use client';

import { useState, useMemo } from 'react';
import Link from 'next/link';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faArrowLeft, faTableCells, faList } from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import { CatanGlyph, type CatanGlyphKey } from '@/lib/constants/catanGlyphs';

type ViewMode = 'grid' | 'table';

/** Build display entries from the CatanGlyph constant. */
function getGlyphEntries(): { key: string; char: string; hex: string; decimal: number }[] {
  // Skip aliases (Soldier, Pirate, DevCard) — only show primary entries
  const aliases = new Set(['Soldier', 'Pirate', 'DevCard']);

  return (Object.entries(CatanGlyph) as [CatanGlyphKey, string][])
    .filter(([key]) => !aliases.has(key))
    .map(([key, char]) => {
      const codepoint = char.codePointAt(0) ?? 0;
      return {
        key,
        char,
        hex: codepoint.toString(16).toUpperCase().padStart(4, '0'),
        decimal: codepoint,
      };
    })
    .sort((a, b) => a.decimal - b.decimal);
}

export default function FontViewerPage(): React.ReactElement {
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const entries = useMemo(() => getGlyphEntries(), []);

  return (
    <MainLayout>
      <div className="min-h-[calc(100vh-120px)] py-8 px-4">
        <div className="max-w-6xl mx-auto">
          {/* Header */}
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-4">
              <Link
                href="/"
                className="text-gray-400 hover:text-white transition-colors"
              >
                <FontAwesomeIcon icon={faArrowLeft} className="w-5 h-5" />
              </Link>
              <div>
                <h1 className="text-2xl font-bold text-amber-400">Catan Font Viewer</h1>
                <p className="text-sm text-gray-400">
                  {entries.length} glyphs from Catan.ttf (U+E900–E942)
                </p>
              </div>
            </div>

            {/* View toggle */}
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
            <div className="grid grid-cols-[repeat(auto-fill,minmax(160px,1fr))] gap-3">
              {entries.map((entry) => (
                <div
                  key={entry.key}
                  className="bg-white/5 border border-white/10 rounded-lg p-4 text-center
                             hover:border-amber-400/50 transition-colors"
                >
                  <div className="font-catan text-6xl leading-none h-20 flex items-center justify-center text-white mb-3">
                    {entry.char}
                  </div>
                  <div className="text-sm font-semibold text-amber-400 mb-1 break-words">
                    {entry.key}
                  </div>
                  <div className="text-xs text-gray-500 font-mono">
                    U+{entry.hex}
                  </div>
                  <div className="text-xs text-gray-600 font-mono">
                    {entry.decimal}
                  </div>
                </div>
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
                  {entries.map((entry) => (
                    <tr
                      key={entry.key}
                      className="border-t border-white/5 hover:bg-white/5 transition-colors"
                    >
                      <td className="px-4 py-3 font-catan text-3xl text-white text-center w-16">
                        {entry.char}
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
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </MainLayout>
  );
}
