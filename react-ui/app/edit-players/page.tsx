'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { MainLayout } from '@/components/layout';
import { gameApi } from '@/lib/api/gameApi';
import { useGameStore } from '@/lib/stores/gameStore';
import { buildCssGradient } from '@/lib/utils/playerColors';
import { serviceConfig } from '@/lib/services/config';
import { ImageCropDialog } from '@/components/ui/ImageCropDialog';
import {
  type PlayerProfile,
  type PlayerColors,
  DEFAULT_PLAYER_COLORS,
  PRESET_PLAYER_COLORS,
} from '@/types/player-profile';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPlus, faTrash, faLink, faUpload } from '@fortawesome/free-solid-svg-icons';

/** Max image upload size: 5MB */
const MAX_IMAGE_SIZE = 5 * 1024 * 1024;
const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif'];

/** Get initials from player name for avatar fallback. */
function getInitials(name: string): string {
  const parts = name.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }
  return name.length >= 2 ? name.substring(0, 2).toUpperCase() : name.toUpperCase();
}

/** Build full image URL from relative imageUri. */
function buildImageUrl(imageUri: string, version: number): string {
  const base = serviceConfig.serviceUrl;
  return `${base}${imageUri}?v=${version}`;
}

/**
 * Color picker row: label + color input + hex text input.
 */
function ColorPickerRow({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (hex: string) => void;
}) {
  return (
    <div className="flex items-center gap-3">
      <label className="text-sm text-gray-400 w-24 shrink-0">{label}</label>
      <input
        type="color"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-10 h-10 rounded cursor-pointer border border-gray-600 bg-transparent"
      />
      <input
        type="text"
        value={value}
        onChange={(e) => {
          const v = e.target.value;
          if (/^#[0-9a-fA-F]{0,6}$/.test(v)) onChange(v);
        }}
        maxLength={7}
        className="w-24 px-2 py-1.5 bg-gray-800 border border-gray-600 rounded text-sm text-white font-mono"
      />
    </div>
  );
}

/**
 * Edit Players page - manage player profiles.
 */
export default function EditPlayers(): React.ReactElement {
  // Player data
  const [players, setPlayers] = useState<PlayerProfile[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Editor form
  const [editName, setEditName] = useState('');
  const [editPrimary, setEditPrimary] = useState(DEFAULT_PLAYER_COLORS.primary);
  const [editSecondary, setEditSecondary] = useState(DEFAULT_PLAYER_COLORS.secondary);
  const [editForeground, setEditForeground] = useState(DEFAULT_PLAYER_COLORS.foreground);
  const [imageVersion, setImageVersion] = useState(0);
  const [saving, setSaving] = useState(false);

  // Original values for change detection
  const [origName, setOrigName] = useState('');
  const [origPrimary, setOrigPrimary] = useState('');
  const [origSecondary, setOrigSecondary] = useState('');
  const [origForeground, setOrigForeground] = useState('');

  // Image upload state
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [imageUrl, setImageUrl] = useState('');
  const [showUrlInput, setShowUrlInput] = useState(false);
  const [cropImageSrc, setCropImageSrc] = useState<string | null>(null);
  const [imageChanged, setImageChanged] = useState(false);

  const selectedPlayer = players.find((p) => p.id === selectedId) ?? null;

  // Derived: current edit colors
  const editColors: PlayerColors = {
    primary: editPrimary,
    secondary: editSecondary,
    foreground: editForeground,
  };
  const editGradient = buildCssGradient(editColors);

  const hasChanges =
    selectedPlayer !== null &&
    (editName !== origName ||
      editPrimary !== origPrimary ||
      editSecondary !== origSecondary ||
      editForeground !== origForeground ||
      imageChanged);

  /** Refresh players from API and update both local state and game store. */
  const refreshPlayers = useCallback(async (): Promise<PlayerProfile[]> => {
    const result = await gameApi.getPlayers();
    if (result.success && result.data) {
      setPlayers(result.data);
      useGameStore.getState().setPlayerProfiles(result.data);
      return result.data;
    }
    return [];
  }, []);

  /** Populate editor form from a player profile. */
  const populateEditor = useCallback((player: PlayerProfile) => {
    setEditName(player.name);
    setEditPrimary(player.colors.primary);
    setEditSecondary(player.colors.secondary);
    setEditForeground(player.colors.foreground);
    setOrigName(player.name);
    setOrigPrimary(player.colors.primary);
    setOrigSecondary(player.colors.secondary);
    setOrigForeground(player.colors.foreground);
    setImageChanged(false);
  }, []);

  /** Select a player for editing. */
  const selectPlayer = useCallback(
    (id: string) => {
      setSelectedId(id);
      setShowUrlInput(false);
      setImageUrl('');
      const player = players.find((p) => p.id === id);
      if (player) populateEditor(player);
    },
    [players, populateEditor]
  );

  /** Load players on mount. */
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      const result = await gameApi.getPlayers();
      if (cancelled) return;
      if (result.success && result.data) {
        setPlayers(result.data);
        useGameStore.getState().setPlayerProfiles(result.data);
        setError(null);
      } else {
        setError(result.error ?? 'Failed to load players');
      }
      setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  /** Add a new player. */
  const handleAdd = async () => {
    setSaving(true);
    const newPlayer: PlayerProfile = {
      id: crypto.randomUUID(),
      name: 'New Player',
      colors: { ...DEFAULT_PLAYER_COLORS },
    };
    const result = await gameApi.createPlayer(newPlayer);
    if (result.success && result.data) {
      const refreshed = await refreshPlayers();
      const created = refreshed.find((p) => p.name === newPlayer.name) ?? result.data;
      setSelectedId(created.id);
      populateEditor(created);
    } else {
      setError(result.error ?? 'Failed to create player');
    }
    setSaving(false);
  };

  /** Save current edits. */
  const handleSave = async () => {
    if (!selectedId || !hasChanges) return;
    setSaving(true);
    const result = await gameApi.updatePlayer(selectedId, {
      ...selectedPlayer,
      name: editName,
      colors: editColors,
    });
    if (result.success) {
      const refreshed = await refreshPlayers();
      const updated = refreshed.find((p) => p.id === selectedId);
      if (updated) populateEditor(updated);
    } else {
      setError(result.error ?? 'Failed to save player');
    }
    setSaving(false);
  };

  /** Cancel edits: revert to originals. */
  const handleCancel = () => {
    if (selectedPlayer) populateEditor(selectedPlayer);
  };

  /** Delete selected player. */
  const handleDelete = async () => {
    if (!selectedId) return;
    const player = players.find((p) => p.id === selectedId);
    if (!player) return;
    const confirmed = window.confirm(`Delete player "${player.name}"?`);
    if (!confirmed) return;
    setSaving(true);
    const result = await gameApi.deletePlayer(selectedId);
    if (result.success) {
      setSelectedId(null);
      await refreshPlayers();
    } else {
      setError(result.error ?? 'Failed to delete player');
    }
    setSaving(false);
  };

  /** Upload a cropped image blob to the server. */
  const uploadCroppedImage = async (blob: Blob) => {
    if (!selectedId) return;
    setCropImageSrc(null);
    setSaving(true);
    setError(null);
    const file = new File([blob], 'avatar.jpg', { type: 'image/jpeg' });
    const result = await gameApi.uploadPlayerImage(selectedId, file);
    if (result.success) {
      setImageVersion((v) => v + 1);
      setImageChanged(true);
      await refreshPlayers();
    } else {
      setError(result.error ?? 'Failed to upload image');
    }
    setSaving(false);
  };

  /** Handle local file selection -- open in crop dialog. */
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!ACCEPTED_IMAGE_TYPES.includes(file.type)) {
      setError('Image must be JPEG, PNG, or GIF');
      return;
    }
    if (file.size > MAX_IMAGE_SIZE) {
      setError('Image must be under 5MB');
      return;
    }

    setError(null);
    const reader = new FileReader();
    reader.onload = () => {
      setCropImageSrc(reader.result as string);
    };
    reader.readAsDataURL(file);

    // Reset file input so same file can be re-selected
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  /** Load image from URL via server proxy and open in crop dialog as a data URI. */
  const handleLoadUrl = async () => {
    const url = imageUrl.trim();
    if (!url) return;
    setError(null);
    setSaving(true);

    try {
      const proxyUrl = `/api/proxy-image?url=${encodeURIComponent(url)}`;
      const response = await fetch(proxyUrl);

      if (!response.ok) {
        const data = await response.json().catch(() => ({ error: 'Failed to load image' }));
        setError(data.error ?? `Failed to load image (${response.status})`);
        setSaving(false);
        return;
      }

      // Convert to data URI (same as file uploads) so canvas crop works reliably
      const blob = await response.blob();
      const reader = new FileReader();
      reader.onload = () => {
        setCropImageSrc(reader.result as string);
        setShowUrlInput(false);
        setImageUrl('');
        setSaving(false);
      };
      reader.onerror = () => {
        setError('Failed to read image data');
        setSaving(false);
      };
      reader.readAsDataURL(blob);
      return; // setSaving(false) handled in reader callbacks
    } catch {
      setError('Failed to fetch image from URL');
    }
    setSaving(false);
  };

  /** Apply a preset color scheme. */
  const applyPreset = (preset: PlayerColors) => {
    setEditPrimary(preset.primary);
    setEditSecondary(preset.secondary);
    setEditForeground(preset.foreground);
  };

  // ── Render ───────────────────────────────────────────────

  if (loading) {
    return (
      <MainLayout>
        <div className="flex items-center justify-center h-full text-gray-400">
          Loading players...
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <div className="flex flex-col sm:flex-row h-full overflow-hidden pt-[60px] pl-4 sm:pl-6">
        {/* ── Player List (left) ─────────────────────────── */}
        <div className="w-full sm:w-60 shrink-0 border-b sm:border-b-0 sm:border-r border-gray-700 flex flex-col">
          <div className="flex items-center justify-between px-3 py-2 border-b border-gray-700">
            <h2 className="text-sm font-semibold text-gray-300">Players</h2>
            <button
              onClick={handleAdd}
              disabled={saving}
              className="p-1.5 rounded hover:bg-gray-700 text-gray-400 hover:text-white transition-colors disabled:opacity-50"
              title="Add player"
            >
              <FontAwesomeIcon icon={faPlus} className="text-sm" />
            </button>
          </div>

          <div className="flex-1 overflow-y-auto">
            {players.length === 0 ? (
              <div className="px-3 py-6 text-center text-sm text-gray-500">
                No players yet. Click + to add one.
              </div>
            ) : (
              players.map((player) => {
                const isSelected = player.id === selectedId;
                const gradient = buildCssGradient(player.colors);
                return (
                  <button
                    key={player.id}
                    onClick={() => selectPlayer(player.id)}
                    className={`w-full flex items-center gap-3 px-3 py-2.5 text-left transition-colors ${
                      isSelected
                        ? 'bg-blue-900/40 border-l-2 border-blue-400'
                        : 'hover:bg-gray-800/60 border-l-2 border-transparent'
                    }`}
                  >
                    {/* Avatar */}
                    <div
                      className="w-9 h-9 rounded-full shrink-0 flex items-center justify-center text-xs font-bold overflow-hidden"
                      style={{ background: gradient, color: player.colors.foreground }}
                    >
                      {player.imageUri ? (
                        <img
                          src={buildImageUrl(player.imageUri, imageVersion)}
                          alt={player.name}
                          className="w-full h-full object-cover"
                        />
                      ) : (
                        getInitials(player.name)
                      )}
                    </div>
                    <span className="text-sm text-white truncate">{player.name}</span>
                  </button>
                );
              })
            )}
          </div>
        </div>

        {/* ── Editor Panel (right) ──────────────────────── */}
        <div className="flex-1 overflow-y-auto p-4 sm:p-6">
          {!selectedPlayer ? (
            <div className="flex items-center justify-center h-full text-gray-500 text-sm">
              Select a player to edit, or click + to add one.
            </div>
          ) : (
            <div className="max-w-lg space-y-6">
              {/* Error banner */}
              {error && (
                <div className="px-3 py-2 rounded bg-red-900/50 border border-red-700 text-red-300 text-sm">
                  {error}
                  <button
                    onClick={() => setError(null)}
                    className="ml-2 text-red-400 hover:text-red-200"
                  >
                    dismiss
                  </button>
                </div>
              )}

              {/* Name input */}
              <div>
                <label className="block text-xs text-gray-400 mb-1">Name</label>
                <input
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  maxLength={50}
                  className="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded text-white text-sm focus:outline-none focus:border-blue-500"
                />
              </div>

              {/* Avatar section */}
              <div>
                <h3 className="text-sm font-semibold text-gray-300 mb-3">Avatar</h3>

                {/* Inline crop dialog - replaces avatar preview when active */}
                {cropImageSrc ? (
                  <ImageCropDialog
                    imageSrc={cropImageSrc}
                    onCrop={uploadCroppedImage}
                    onCancel={() => setCropImageSrc(null)}
                  />
                ) : (
                  <div className="flex items-start gap-4">
                    {/* Current avatar preview */}
                    <div
                      className="w-20 h-20 rounded-full shrink-0 flex items-center justify-center text-xl font-bold overflow-hidden"
                      style={{ background: editGradient, color: editForeground }}
                    >
                      {selectedPlayer.imageUri ? (
                        <img
                          src={buildImageUrl(selectedPlayer.imageUri, imageVersion)}
                          alt={editName}
                          className="w-full h-full object-cover"
                        />
                      ) : (
                        getInitials(editName)
                      )}
                    </div>

                    {/* Upload options */}
                    <div className="flex-1 space-y-2">
                      {/* File upload button */}
                      <button
                        onClick={() => fileInputRef.current?.click()}
                        disabled={saving}
                        className="flex items-center gap-2 w-full px-3 py-2 rounded text-sm text-gray-300 bg-gray-800 border border-gray-600 hover:border-gray-400 transition-colors disabled:opacity-50"
                      >
                        <FontAwesomeIcon icon={faUpload} className="text-xs text-gray-400" />
                        Upload from file
                      </button>
                      <input
                        ref={fileInputRef}
                        type="file"
                        accept={ACCEPTED_IMAGE_TYPES.join(',')}
                        onChange={handleFileSelect}
                        className="hidden"
                      />

                      {/* URL input toggle */}
                      {!showUrlInput ? (
                        <button
                          onClick={() => setShowUrlInput(true)}
                          disabled={saving}
                          className="flex items-center gap-2 w-full px-3 py-2 rounded text-sm text-gray-300 bg-gray-800 border border-gray-600 hover:border-gray-400 transition-colors disabled:opacity-50"
                        >
                          <FontAwesomeIcon icon={faLink} className="text-xs text-gray-400" />
                          Load from URL
                        </button>
                      ) : (
                        <div className="flex gap-2">
                          <input
                            type="url"
                            value={imageUrl}
                            onChange={(e) => setImageUrl(e.target.value)}
                            placeholder="https://..."
                            onKeyDown={(e) => { if (e.key === 'Enter') handleLoadUrl(); }}
                            className="flex-1 px-3 py-2 bg-gray-800 border border-gray-600 rounded text-sm text-white focus:outline-none focus:border-blue-500"
                            autoFocus
                          />
                          <button
                            onClick={handleLoadUrl}
                            disabled={!imageUrl.trim()}
                            className="px-3 py-2 rounded text-sm bg-blue-600 text-white hover:bg-blue-500 transition-colors disabled:opacity-50"
                          >
                            Load
                          </button>
                          <button
                            onClick={() => { setShowUrlInput(false); setImageUrl(''); }}
                            className="px-2 py-2 rounded text-sm text-gray-400 hover:text-white transition-colors"
                          >
                            Cancel
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </div>

              {/* Color pickers */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold text-gray-300">Colors</h3>
                <ColorPickerRow label="Primary" value={editPrimary} onChange={setEditPrimary} />
                <ColorPickerRow label="Secondary" value={editSecondary} onChange={setEditSecondary} />
                <ColorPickerRow label="Foreground" value={editForeground} onChange={setEditForeground} />
              </div>

              {/* Preset swatches */}
              <div>
                <h3 className="text-sm font-semibold text-gray-300 mb-2">Presets</h3>
                <div className="flex gap-2 flex-wrap">
                  {PRESET_PLAYER_COLORS.map((preset, i) => {
                    const presetGradient = buildCssGradient(preset);
                    const isActive =
                      editPrimary === preset.primary &&
                      editSecondary === preset.secondary &&
                      editForeground === preset.foreground;
                    return (
                      <button
                        key={i}
                        onClick={() => applyPreset(preset)}
                        className={`w-10 h-10 rounded-full border-2 transition-all ${
                          isActive ? 'border-white scale-110' : 'border-gray-600 hover:border-gray-400'
                        }`}
                        style={{ background: presetGradient }}
                        title={`Preset ${i + 1}`}
                      />
                    );
                  })}
                </div>
              </div>

              {/* Live preview */}
              <div>
                <h3 className="text-sm font-semibold text-gray-300 mb-2">Preview</h3>
                <div
                  className="px-4 py-3 rounded-lg text-sm font-semibold"
                  style={{ background: editGradient, color: editForeground }}
                >
                  {editName || 'Player Name'}
                </div>
              </div>

              {/* Action buttons */}
              <div className="flex items-center justify-between pt-2">
                <button
                  onClick={handleDelete}
                  disabled={saving}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded text-sm text-red-400 hover:bg-red-900/30 transition-colors disabled:opacity-50"
                >
                  <FontAwesomeIcon icon={faTrash} className="text-xs" />
                  Delete
                </button>

                <div className="flex gap-2">
                  <button
                    onClick={handleCancel}
                    disabled={!hasChanges || saving}
                    className="px-4 py-1.5 rounded text-sm text-gray-300 hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-default"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleSave}
                    disabled={!hasChanges || saving || !editName.trim()}
                    className="px-4 py-1.5 rounded text-sm bg-blue-600 text-white hover:bg-blue-500 transition-colors disabled:opacity-50 disabled:cursor-default"
                  >
                    {saving ? 'Saving...' : 'Save'}
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

    </MainLayout>
  );
}
