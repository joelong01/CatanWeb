'use client';

import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useRouter, useParams } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { gameApi } from '@/lib/api/gameApi';
import { EditorBoard, coordKey } from '@/components/templates/EditorBoard';
import { TileContextMenu } from '@/components/templates/TileContextMenu';
import { WaterContextMenu } from '@/components/templates/WaterContextMenu';
import { HarborContextMenu } from '@/components/templates/HarborContextMenu';
import type { GameTemplateData, TemplateTile, TemplateHarbor } from '@/types/generated/models';
import type { HexCoordinate } from '@/components/hex-grid/hex-geometry';
import { cubicCoord } from '@/components/hex-grid/hex-geometry';
import { getSpiralCoordinates, getSquareCoordinates } from '@/components/hex-grid';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faSave, faSpinner, faTrash, faPlus, faMinus } from '@fortawesome/free-solid-svg-icons';

type LayoutType = 'Spiral' | 'Square';

const LAYOUT_OPTIONS: { value: LayoutType; label: string }[] = [
  { value: 'Spiral', label: 'Spiral (hexagonal rings)' },
  { value: 'Square', label: 'Square (column-based)' },
];

const RESOURCE_OPTIONS = ['Desert', 'Wood', 'Brick', 'Wheat', 'Sheep', 'Ore', 'GoldMine'];
const TILE_NUMBERS = [2, 3, 4, 5, 6, 8, 9, 10, 11, 12];
const NUMBER_OPTIONS = [0, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12];
const HARBOR_SIDE_OPTIONS = ['Top', 'TopRight', 'BottomRight', 'Bottom', 'BottomLeft', 'TopLeft'];
const HARBOR_TYPE_OPTIONS = ['ThreeForOne', 'Wood', 'Brick', 'Wheat', 'Sheep', 'Ore'];

/** Flip animation duration in ms. */
const FLIP_DURATION = 300;

export default function TemplateEditor(): React.ReactElement {
  const router = useRouter();
  const params = useParams();
  const templateId = params.id as string;
  const isNew = templateId === 'new';

  const [template, setTemplate] = useState<GameTemplateData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [selectedTileIndex, setSelectedTileIndex] = useState<number | null>(null);
  const [layout, setLayout] = useState<LayoutType>('Spiral');

  // Context menu state
  const [contextMenu, setContextMenu] = useState<{
    tileIndex: number;
    position: { x: number; y: number };
  } | null>(null);

  // Flip animation state — set of coordKey strings currently animating
  const [flippingCoords, setFlippingCoords] = useState<Set<string>>(new Set());

  // Refs for tile table row scrolling
  const tileRowRefs = useRef<Map<number, HTMLDivElement>>(new Map());

  /** Trigger a flip animation at a coordinate, auto-clears after duration. */
  const triggerFlip = useCallback((coord: HexCoordinate) => {
    const key = coordKey(coord);
    setFlippingCoords((prev) => new Set(prev).add(key));
    setTimeout(() => {
      setFlippingCoords((prev) => {
        const next = new Set(prev);
        next.delete(key);
        return next;
      });
    }, FLIP_DURATION);
  }, []);

  /** Regenerate tile coordinates using the current layout algorithm, preserving resources/numbers. */
  const applyLayout = useCallback(
    (tiles: TemplateTile[], layoutType: LayoutType): TemplateTile[] => {
      if (tiles.length === 0) return tiles;
      const coords =
        layoutType === 'Spiral'
          ? getSpiralCoordinates(tiles.length)
          : getSquareCoordinates(tiles.length);
      return tiles.map((tile, i) => ({
        ...tile,
        q: coords[i].q,
        r: coords[i].r,
      }));
    },
    []
  );

  const loadTemplate = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    const result = await gameApi.getTemplate(templateId);
    if (result.success && result.data) {
      setTemplate(result.data);
    } else {
      setError(result.error ?? 'Failed to load template');
    }
    setIsLoading(false);
  }, [templateId]);

  // Load template on mount
  useEffect(() => {
    if (isNew) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- initialization
      setTemplate(createDefaultTemplate());

      setIsLoading(false);
    } else {
      loadTemplate();
    }
  }, [isNew, loadTemplate]);

  const handleSave = useCallback(async () => {
    if (!template) return;
    setIsSaving(true);
    setError(null);
    setSaveMessage(null);

    const result = isNew
      ? await gameApi.createTemplate(template)
      : await gameApi.updateTemplate(templateId, template);

    if (result.success) {
      setSaveMessage('Saved successfully');
      if (isNew && result.data) {
        router.replace(`/templates/${result.data.id}`);
      }
      setTimeout(() => setSaveMessage(null), 3000);
    } else {
      setError(result.error ?? 'Failed to save');
    }
    setIsSaving(false);
  }, [template, isNew, templateId, router]);

  // Template field updaters
  const updateField = useCallback(
    <K extends keyof GameTemplateData>(field: K, value: GameTemplateData[K]) => {
      setTemplate((prev) => (prev ? { ...prev, [field]: value } : prev));
    },
    []
  );

  const handleLayoutChange = useCallback(
    (newLayout: LayoutType) => {
      setLayout(newLayout);
      setTemplate((prev) => {
        if (!prev) return prev;
        return { ...prev, tiles: applyLayout(prev.tiles, newLayout) };
      });
      setSelectedTileIndex(null);
    },
    [applyLayout]
  );

  const addResourceTile = useCallback(
    (resource: string) => {
      setTemplate((prev) => {
        if (!prev) return prev;
        const num = resource === 'Desert' ? 0 : 0;
        const newTiles = [...prev.tiles, { q: 0, r: 0, resource, number: num }];
        return { ...prev, tiles: applyLayout(newTiles, layout) };
      });
    },
    [applyLayout, layout]
  );

  const removeResourceTile = useCallback(
    (resource: string) => {
      setTemplate((prev) => {
        if (!prev) return prev;
        const idx = prev.tiles.findLastIndex((t) => t.resource === resource);
        if (idx === -1) return prev;
        const tiles = prev.tiles.filter((_, i) => i !== idx);
        return { ...prev, tiles: applyLayout(tiles, layout) };
      });
      setSelectedTileIndex(null);
    },
    [applyLayout, layout]
  );

  // Tile CRUD for context menu and tile table
  const updateTile = useCallback((index: number, updates: Partial<TemplateTile>) => {
    setTemplate((prev) => {
      if (!prev) return prev;
      const tiles = [...prev.tiles];
      tiles[index] = { ...tiles[index], ...updates };
      return { ...prev, tiles };
    });
  }, []);

  const removeTile = useCallback(
    (index: number) => {
      setTemplate((prev) => {
        if (!prev) return prev;
        const removed = prev.tiles[index];
        if (removed) {
          triggerFlip(cubicCoord(removed.q, removed.r));
        }
        return { ...prev, tiles: prev.tiles.filter((_, i) => i !== index) };
      });
      setSelectedTileIndex(null);
    },
    [triggerFlip]
  );

  // Water hex right-click context menu state
  const [waterMenu, setWaterMenu] = useState<{
    coord: HexCoordinate;
    adjacentTiles: { q: number; r: number; side: string }[];
    position: { x: number; y: number };
  } | null>(null);

  // Harbor right-click context menu state
  const [harborMenu, setHarborMenu] = useState<{
    harborIndex: number;
    position: { x: number; y: number };
  } | null>(null);

  // Water right-click — show menu with Add Tile / Add Harbor options
  const handleWaterRightClick = useCallback(
    (
      coord: HexCoordinate,
      adjacentTiles: { q: number; r: number; side: string }[],
      event: React.MouseEvent
    ) => {
      setWaterMenu({ coord, adjacentTiles, position: { x: event.clientX, y: event.clientY } });
    },
    []
  );

  const addTileAtCoord = useCallback(
    (coord: HexCoordinate) => {
      triggerFlip(coord);
      setTemplate((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          tiles: [...prev.tiles, { q: coord.q, r: coord.r, resource: 'Desert', number: 0 }],
        };
      });
      setWaterMenu(null);
    },
    [triggerFlip]
  );

  const addHarborAtCoord = useCallback((tileQ: number, tileR: number, side: string) => {
    setTemplate((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        harbors: [...prev.harbors, { q: tileQ, r: tileR, side, type: 'ThreeForOne' }],
      };
    });
    setWaterMenu(null);
  }, []);

  // Harbor right-click — show edit menu
  const handleHarborRightClick = useCallback((index: number, event: React.MouseEvent) => {
    setHarborMenu({ harborIndex: index, position: { x: event.clientX, y: event.clientY } });
  }, []);

  const closeWaterMenu = useCallback(() => setWaterMenu(null), []);
  const closeHarborMenu = useCallback(() => setHarborMenu(null), []);

  const updateHarbor = useCallback((index: number, updates: Partial<TemplateHarbor>) => {
    setTemplate((prev) => {
      if (!prev) return prev;
      const harbors = [...prev.harbors];
      harbors[index] = { ...harbors[index], ...updates };
      return { ...prev, harbors };
    });
  }, []);

  const addHarbor = useCallback(() => {
    setTemplate((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        harbors: [...prev.harbors, { q: 0, r: 0, side: 'Top', type: 'ThreeForOne' }],
      };
    });
  }, []);

  const removeHarbor = useCallback((index: number) => {
    setTemplate((prev) => {
      if (!prev) return prev;
      return { ...prev, harbors: prev.harbors.filter((_, i) => i !== index) };
    });
  }, []);

  // Right-click on tile → immediately open context menu
  const handleTileRightClick = useCallback((index: number, event: React.MouseEvent) => {
    setSelectedTileIndex(index);
    setContextMenu({ tileIndex: index, position: { x: event.clientX, y: event.clientY } });
    // Scroll tile table row into view
    const row = tileRowRefs.current.get(index);
    if (row) {
      row.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, []);

  const closeContextMenu = useCallback(() => {
    setContextMenu(null);
  }, []);

  // Validation
  const validationErrors = useMemo(() => {
    if (!template) return [];
    const errors: string[] = [];
    if (!template.name.trim()) errors.push('Template name is required');
    if (template.tiles.length === 0) errors.push('Template has no tiles');

    // Check duplicate coordinates
    const coordSet = new Set<string>();
    for (const tile of template.tiles) {
      const key = `${tile.q},${tile.r}`;
      if (coordSet.has(key)) {
        errors.push(`Duplicate tile at (${tile.q}, ${tile.r})`);
      }
      coordSet.add(key);
    }

    return errors;
  }, [template]);

  if (isLoading) {
    return (
      <MainLayout title="Template Editor" onBack={() => router.push('/templates')}>
        <div className="flex items-center justify-center py-12 text-gray-400">
          <FontAwesomeIcon icon={faSpinner} spin className="mr-2" />
          Loading template...
        </div>
      </MainLayout>
    );
  }

  if (!template) {
    return (
      <MainLayout title="Template Editor" onBack={() => router.push('/templates')}>
        <div className="text-center py-12 text-red-400">{error ?? 'Template not found'}</div>
      </MainLayout>
    );
  }

  return (
    <MainLayout
      title={template.name || 'Untitled Template'}
      onBack={() => router.push('/templates')}
    >
      <div className="h-[calc(100vh-4rem)] flex flex-col">
        {/* Toolbar */}
        <div className="flex items-center gap-3 px-4 py-2 border-b border-white/10">
          <button
            onClick={handleSave}
            disabled={isSaving || validationErrors.length > 0}
            className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-600/50 disabled:cursor-not-allowed text-white rounded-lg px-4 py-1.5 text-sm font-medium transition-colors"
          >
            <FontAwesomeIcon icon={isSaving ? faSpinner : faSave} spin={isSaving} />
            Save
          </button>
          {saveMessage && <span className="text-green-400 text-sm">{saveMessage}</span>}
          {error && <span className="text-red-400 text-sm">{error}</span>}
        </div>

        {/* Main content: form + board */}
        <div className="flex-1 flex flex-col lg:flex-row overflow-hidden">
          {/* Form panel (left) */}
          <div className="lg:w-[400px] xl:w-[480px] overflow-y-auto border-r border-white/10 p-4 space-y-4">
            {/* Metadata */}
            <section className="space-y-3">
              <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                Metadata
              </h2>
              <div className="space-y-2">
                <label className="block">
                  <span className="text-xs text-gray-400">Name</span>
                  <input
                    type="text"
                    value={template.name}
                    onChange={(e) => updateField('name', e.target.value)}
                    className="w-full mt-1 bg-white/5 border border-white/10 rounded px-3 py-1.5 text-sm text-white"
                  />
                </label>
                <label className="block">
                  <span className="text-xs text-gray-400">Category</span>
                  <input
                    type="text"
                    value={template.category}
                    onChange={(e) => updateField('category', e.target.value)}
                    className="w-full mt-1 bg-white/5 border border-white/10 rounded px-3 py-1.5 text-sm text-white"
                  />
                </label>
                <label className="block">
                  <span className="text-xs text-gray-400">Description</span>
                  <textarea
                    value={template.description}
                    onChange={(e) => updateField('description', e.target.value)}
                    rows={2}
                    className="w-full mt-1 bg-white/5 border border-white/10 rounded px-3 py-1.5 text-sm text-white"
                  />
                </label>
                <label className="block">
                  <span className="text-xs text-gray-400">Game Type</span>
                  <select
                    value={template.gameType}
                    onChange={(e) => updateField('gameType', e.target.value)}
                    className="w-full mt-1 bg-white/5 border border-white/10 rounded px-3 py-1.5 text-sm text-white"
                  >
                    <option value="Regular">Regular</option>
                    <option value="Expansion">Expansion</option>
                  </select>
                </label>
                <label className="block">
                  <span className="text-xs text-gray-400">Board Layout</span>
                  <select
                    value={layout}
                    onChange={(e) => handleLayoutChange(e.target.value as LayoutType)}
                    className="w-full mt-1 bg-white/5 border border-white/10 rounded px-3 py-1.5 text-sm text-white"
                  >
                    {LAYOUT_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            </section>

            {/* Resource Distribution */}
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                Resources ({template.tiles.length} tiles)
              </h2>
              <div className="space-y-1">
                {RESOURCE_OPTIONS.map((resource) => {
                  const count = template.tiles.filter((t) => t.resource === resource).length;
                  return (
                    <div
                      key={resource}
                      className="flex items-center justify-between text-sm px-2 py-1 rounded bg-white/5"
                    >
                      <span className="text-gray-300">{resource}</span>
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => removeResourceTile(resource)}
                          disabled={count === 0}
                          className="w-5 h-5 flex items-center justify-center rounded bg-white/10 hover:bg-white/20 disabled:opacity-30 disabled:cursor-not-allowed text-gray-300"
                        >
                          <FontAwesomeIcon icon={faMinus} className="text-[9px]" />
                        </button>
                        <span className="text-white w-6 text-center font-mono">{count}</span>
                        <button
                          onClick={() => addResourceTile(resource)}
                          className="w-5 h-5 flex items-center justify-center rounded bg-white/10 hover:bg-white/20 text-gray-300"
                        >
                          <FontAwesomeIcon icon={faPlus} className="text-[9px]" />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            </section>

            {/* Tile Table */}
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                Tiles ({template.tiles.length})
              </h2>
              {template.tiles.length > 0 && (
                <div className="max-h-[300px] overflow-y-auto rounded border border-white/10">
                  {/* Header */}
                  <div className="flex items-center text-[10px] text-gray-500 uppercase tracking-wider px-2 py-1 bg-white/5 border-b border-white/10 sticky top-0">
                    <span className="w-14">Q, R</span>
                    <span className="flex-1">Resource</span>
                    <span className="w-16">Number</span>
                    <span className="w-6" />
                  </div>
                  {/* Rows */}
                  {template.tiles.map((tile, i) => {
                    const isSelected = selectedTileIndex === i;
                    const isDesert = tile.resource === 'Desert';
                    return (
                      <div
                        key={i}
                        ref={(el) => {
                          if (el) tileRowRefs.current.set(i, el);
                          else tileRowRefs.current.delete(i);
                        }}
                        className={`flex items-center text-sm px-2 py-0.5 cursor-pointer transition-colors ${
                          isSelected
                            ? 'bg-blue-600/20 border-l-2 border-blue-500'
                            : 'hover:bg-white/5 border-l-2 border-transparent'
                        }`}
                        onClick={() => setSelectedTileIndex(i)}
                      >
                        <span className="w-14 text-gray-500 text-xs font-mono">
                          {tile.q},{tile.r}
                        </span>
                        <select
                          value={tile.resource}
                          onChange={(e) => {
                            const resource = e.target.value;
                            const updates: Partial<TemplateTile> = { resource };
                            if (resource === 'Desert') updates.number = 0;
                            updateTile(i, updates);
                          }}
                          className="flex-1 bg-transparent border-none text-white text-xs py-0.5"
                          onClick={(e) => e.stopPropagation()}
                        >
                          {RESOURCE_OPTIONS.map((r) => (
                            <option key={r} value={r}>
                              {r}
                            </option>
                          ))}
                        </select>
                        <select
                          value={tile.number}
                          onChange={(e) => updateTile(i, { number: parseInt(e.target.value, 10) })}
                          disabled={isDesert}
                          className="w-16 bg-transparent border-none text-white text-xs py-0.5 disabled:opacity-40"
                          onClick={(e) => e.stopPropagation()}
                        >
                          {NUMBER_OPTIONS.map((n) => (
                            <option key={n} value={n}>
                              {n === 0 ? '—' : n}
                            </option>
                          ))}
                        </select>
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            removeTile(i);
                          }}
                          className="w-6 text-red-400/40 hover:text-red-400 text-xs"
                        >
                          <FontAwesomeIcon icon={faTrash} />
                        </button>
                      </div>
                    );
                  })}
                </div>
              )}
            </section>

            {/* Number Distribution */}
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                Numbers
              </h2>
              <div className="space-y-1">
                {TILE_NUMBERS.map((num) => {
                  const count = template.tiles.filter((t) => t.number === num).length;
                  return (
                    <div
                      key={num}
                      className="flex items-center justify-between text-sm px-2 py-1 rounded bg-white/5"
                    >
                      <span
                        className={`font-mono ${num === 6 || num === 8 ? 'text-red-400 font-bold' : 'text-gray-300'}`}
                      >
                        {num}
                      </span>
                      <span className="text-white w-6 text-center font-mono">{count}</span>
                    </div>
                  );
                })}
              </div>
            </section>

            {/* Harbors */}
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                  Harbors ({template.harbors.length})
                </h2>
                <button
                  onClick={addHarbor}
                  className="text-xs text-blue-400 hover:text-blue-300 flex items-center gap-1"
                >
                  <FontAwesomeIcon icon={faPlus} /> Add
                </button>
              </div>
              <div className="space-y-1 max-h-[200px] overflow-y-auto">
                {template.harbors.map((harbor, i) => (
                  <div
                    key={i}
                    className="flex items-center gap-2 text-sm px-2 py-1 rounded bg-white/5 hover:bg-white/10"
                  >
                    <span className="text-gray-500 w-12 text-xs">
                      ({harbor.q},{harbor.r})
                    </span>
                    <select
                      value={harbor.side}
                      onChange={(e) => updateHarbor(i, { side: e.target.value })}
                      className="bg-transparent border-none text-white text-xs"
                    >
                      {HARBOR_SIDE_OPTIONS.map((s) => (
                        <option key={s} value={s}>
                          {s}
                        </option>
                      ))}
                    </select>
                    <select
                      value={harbor.type}
                      onChange={(e) => updateHarbor(i, { type: e.target.value })}
                      className="bg-transparent border-none text-white text-xs"
                    >
                      {HARBOR_TYPE_OPTIONS.map((t) => (
                        <option key={t} value={t}>
                          {t}
                        </option>
                      ))}
                    </select>
                    <button
                      onClick={() => removeHarbor(i)}
                      className="text-red-400/60 hover:text-red-400 text-xs"
                    >
                      <FontAwesomeIcon icon={faTrash} />
                    </button>
                  </div>
                ))}
              </div>
            </section>

            {/* Entitlements (read-only for now) */}
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-gray-300 uppercase tracking-wider">
                Entitlements ({template.entitlements.length})
              </h2>
              <div className="space-y-1">
                {template.entitlements.map((ent, i) => (
                  <div key={i} className="text-sm px-2 py-1 rounded bg-white/5 text-gray-300">
                    {ent.entitlement}
                  </div>
                ))}
              </div>
            </section>
          </div>

          {/* Board (right) */}
          <div className="flex-1 min-h-[400px] bg-[#1a1a2e] relative">
            <EditorBoard
              tiles={template.tiles}
              harbors={template.harbors}
              selectedTileIndex={selectedTileIndex}
              onTileRightClick={handleTileRightClick}
              onHarborRightClick={handleHarborRightClick}
              onWaterRightClick={handleWaterRightClick}
              flippingCoords={flippingCoords}
            />
          </div>
        </div>

        {/* Validation bar */}
        {validationErrors.length > 0 && (
          <div className="px-4 py-2 border-t border-white/10 bg-red-500/5 text-sm">
            {validationErrors.map((err, i) => (
              <span key={i} className="text-red-400 mr-4">
                {err}
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Tile context menu (portal) */}
      {contextMenu && template.tiles[contextMenu.tileIndex] && (
        <TileContextMenu
          tile={template.tiles[contextMenu.tileIndex]}
          tileIndex={contextMenu.tileIndex}
          position={contextMenu.position}
          onUpdateTile={updateTile}
          onRemoveTile={removeTile}
          onClose={closeContextMenu}
        />
      )}

      {/* Water context menu (portal) */}
      {waterMenu && (
        <WaterContextMenu
          coord={waterMenu.coord}
          adjacentTiles={waterMenu.adjacentTiles}
          position={waterMenu.position}
          onAddTile={() => addTileAtCoord(waterMenu.coord)}
          onAddHarbor={addHarborAtCoord}
          onClose={closeWaterMenu}
        />
      )}

      {/* Harbor context menu (portal) */}
      {harborMenu && template.harbors[harborMenu.harborIndex] && (
        <HarborContextMenu
          harbor={template.harbors[harborMenu.harborIndex]}
          harborIndex={harborMenu.harborIndex}
          position={harborMenu.position}
          onUpdateHarbor={updateHarbor}
          onRemoveHarbor={(i) => {
            removeHarbor(i);
            closeHarborMenu();
          }}
          onClose={closeHarborMenu}
        />
      )}
    </MainLayout>
  );
}

function createDefaultTemplate(): GameTemplateData {
  return {
    id: '',
    name: '',
    category: 'Base',
    version: 1,
    description: '',
    engine: 'base',
    gameType: 'Regular',
    resourceRules: {
      maxCities: 4,
      maxSettlements: 5,
      maxRoads: 15,
      minPlayers: 3,
      maxPlayers: 4,
    },
    houseRules: {
      goldTiles: 0,
      wallsProtectCities: false,
      hideBaronBeforeInvasion: false,
      knightMovesBaronBeforeRoll: false,
      hideRobberBeforeInvasion: false,
      knightMovesRobberBeforeRoll: false,
      supplementalMinPlayers: 5,
      griefDodgy: false,
    },
    hasSupplemental: false,
    tiles: [],
    harbors: [],
    entitlements: [
      { entitlement: 'Road' },
      { entitlement: 'Settlement' },
      { entitlement: 'City' },
      { entitlement: 'DevelopmentCard' },
    ],
  };
}
