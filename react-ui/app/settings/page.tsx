'use client';

import { useEffect, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faRotateLeft,
  faFloppyDisk,
  faCircleQuestion,
  faCheck,
  faDownload,
} from '@fortawesome/free-solid-svg-icons';
import { useSettingsStore } from '@/lib/stores/settingsStore';
import { getServiceUrl } from '@/lib/config';
import { useGameType } from '@/lib/stores/gameStoreHooks';
import { gameApi } from '@/lib/api/gameApi';
import type { HouseRules } from '@/types/generated/models';
import type { AnimationSpeed } from '@/types/settings';

/**
 * Tooltip component for setting descriptions.
 */
function Tooltip({ text }: { text: string }): React.ReactElement {
  return (
    <span className="group relative inline-flex items-center ml-2">
      <FontAwesomeIcon
        icon={faCircleQuestion}
        className="text-gray-500 hover:text-gray-300 cursor-help text-sm"
      />
      <span
        className="
          absolute left-6 top-1/2 -translate-y-1/2 z-50
          invisible group-hover:visible opacity-0 group-hover:opacity-100
          transition-opacity duration-200
          bg-gray-800 text-white text-xs rounded-lg px-3 py-2
          w-64 shadow-lg border border-gray-600
        "
      >
        {text}
      </span>
    </span>
  );
}

/**
 * Dropdown select component for settings.
 */
function SettingDropdown({
  value,
  options,
  onChange,
}: {
  value: string | number;
  options: string[];
  onChange: (value: string) => void;
}): React.ReactElement {
  return (
    <select
      value={String(value)}
      onChange={(e) => onChange(e.target.value)}
      className="
        bg-gray-700 text-white rounded-lg px-3 py-2
        border border-gray-600 focus:border-blue-500 focus:outline-none
        cursor-pointer min-w-[100px]
      "
    >
      {options.map((opt) => (
        <option key={opt} value={opt}>
          {opt}
        </option>
      ))}
    </select>
  );
}

/**
 * Individual setting row component.
 */
function SettingRow({
  label,
  tooltip,
  children,
}: {
  label: string;
  tooltip?: string;
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <div className="flex items-center justify-between py-3 px-4 bg-gray-800/50 rounded-lg">
      <div className="flex items-center">
        <span className="text-white font-medium">{label}</span>
        {tooltip && <Tooltip text={tooltip} />}
      </div>
      <div>{children}</div>
    </div>
  );
}

/**
 * Settings category section component.
 */
function SettingsCategory({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <div className="mb-6">
      <h2 className="text-lg font-semibold text-amber-400 mb-3">{title}</h2>
      <div className="space-y-2">{children}</div>
    </div>
  );
}

/**
 * Stream Deck plugin download section.
 * Fetches version metadata from the server so the download link always points
 * to the correct versioned file.  Shows a clear error state if the metadata
 * cannot be loaded (instead of silently falling back to a stale URL).
 */
function StreamDeckDownload(): React.ReactElement {
  const [meta, setMeta] = useState<{ version: string; filename: string } | null>(null);
  const [error, setError] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(`${getServiceUrl()}/downloads/streamdeck-latest.json`)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      })
      .then((data) => {
        setMeta(data);
        setError(false);
      })
      .catch(() => {
        setMeta(null);
        setError(true);
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <SettingsCategory title="Downloads">
      <div className="flex items-center justify-between py-3 px-4 bg-gray-800/50 rounded-lg">
        <div>
          <span className="text-white font-medium">Stream Deck Plugin</span>
          {meta?.version && <span className="text-gray-500 text-sm ml-2">v{meta.version}</span>}
          <p className="text-gray-400 text-sm mt-1">
            Control dice rolls, undo/redo, and navigate game states from your Elgato Stream Deck.
          </p>
          {error && (
            <p className="text-red-400 text-sm mt-1">
              Could not load plugin info from GameService.
            </p>
          )}
        </div>
        {meta ? (
          <a
            href={`${getServiceUrl()}/downloads/${meta.filename}`}
            download={meta.filename}
            className="
              flex items-center gap-2 px-4 py-2.5
              bg-purple-600 hover:bg-purple-500
              text-white rounded-lg font-medium
              transition-colors duration-200 whitespace-nowrap
            "
          >
            <FontAwesomeIcon icon={faDownload} />
            <span>Download</span>
          </a>
        ) : (
          <button
            type="button"
            disabled
            className="
              flex items-center gap-2 px-4 py-2.5
              bg-gray-600 text-gray-400 rounded-lg font-medium
              cursor-not-allowed whitespace-nowrap
            "
          >
            <FontAwesomeIcon icon={faDownload} />
            <span>{loading ? 'Loading…' : 'Unavailable'}</span>
          </button>
        )}
      </div>
    </SettingsCategory>
  );
}

/**
 * Settings page - allows users to configure game and app preferences.
 */
export default function Settings(): React.ReactElement {
  const router = useRouter();
  const store = useSettingsStore();
  const gameType = useGameType();
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(true);
  const [activeGameId] = useState<string | null>(() =>
    typeof window !== 'undefined' ? localStorage.getItem('current_gameId') : null
  );

  // Initialize store on mount
  useEffect(() => {
    store.initialize();
  }, [store]);

  // Clear save message after 3 seconds
  useEffect(() => {
    if (saveMessage) {
      const timer = setTimeout(() => setSaveMessage(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [saveMessage]);

  const handleSave = useCallback(async () => {
    try {
      store.saveToStorage();
      setSaveSuccess(true);

      // Push server-side settings to running game if one is active
      if (activeGameId && gameType) {
        const houseRules = store.getHouseRules(gameType) as HouseRules;
        const result = await gameApi.updateHouseRules(activeGameId, houseRules);
        setSaveMessage(
          result.success ? 'Settings saved & game updated' : 'Settings saved (game update failed)'
        );
      } else {
        setSaveMessage('Settings saved');
      }
    } catch {
      setSaveMessage('Error saving settings');
      setSaveSuccess(false);
    }
  }, [store, activeGameId, gameType]);

  const handleReset = useCallback(() => {
    store.resetToDefaults();
    setSaveMessage('Settings reset to defaults');
    setSaveSuccess(true);
  }, [store]);

  // Determine back navigation destination
  const backHref = activeGameId ? `/game/${activeGameId}` : '/';
  return (
    <MainLayout
      activeGameId={activeGameId}
      title="Settings"
      subtitle="Configure game preferences"
      onBack={() => router.push(backHref)}
    >
      <div className="min-h-screen h-full py-5 pb-[60px] px-5 max-w-[600px] mx-auto">
        {/* Settings content */}
        <div className="space-y-6">
          {/* House Rules */}
          <SettingsCategory title="House Rules">
            <SettingRow
              label="Gold tiles (Expansion)"
              tooltip="Gold tiles produce any resource when rolled. Expansion games support 0-4 gold tiles."
            >
              <SettingDropdown
                value={store.expansionGoldTiles}
                options={['0', '1', '2', '3', '4']}
                onChange={(v) => store.setExpansionGoldTiles(parseInt(v, 10))}
              />
            </SettingRow>

            <SettingRow
              label="Gold tiles (Regular)"
              tooltip="Gold tiles produce any resource when rolled. Regular games support 0-2 gold tiles."
            >
              <SettingDropdown
                value={store.regularGoldTiles}
                options={['0', '1', '2']}
                onChange={(v) => store.setRegularGoldTiles(parseInt(v, 10))}
              />
            </SettingRow>

            <SettingRow
              label="Supplemental build min players"
              tooltip="The minimum number of players required to enable the supplemental build phase in expansion games."
            >
              <SettingDropdown
                value={store.supplementalMinPlayers}
                options={['3', '4', '5', '6']}
                onChange={(v) => store.setSupplementalMinPlayers(parseInt(v, 10))}
              />
            </SettingRow>
          </SettingsCategory>

          {/* Game Configuration */}
          <SettingsCategory title="Game Configuration">
            <SettingRow
              label="Animation speed"
              tooltip="Controls the speed of UI animations. Normal is the base; other speeds are calculated proportionally."
            >
              <SettingDropdown
                value={store.animationSpeed}
                options={['Slow', 'Normal', 'Fast', 'None']}
                onChange={(v) => store.setAnimationSpeed(v as AnimationSpeed)}
              />
            </SettingRow>
          </SettingsCategory>

          {/* Downloads */}
          <StreamDeckDownload />

          {/* Action buttons */}
          <div className="flex gap-3 pt-4 border-t border-gray-700">
            <button
              type="button"
              onClick={handleReset}
              className="
                flex items-center gap-2 px-4 py-2.5
                bg-gray-700 hover:bg-gray-600
                text-white rounded-lg font-medium
                transition-colors duration-200
              "
            >
              <FontAwesomeIcon icon={faRotateLeft} />
              <span>Reset to Defaults</span>
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="
                flex items-center gap-2 px-6 py-2.5
                bg-blue-600 hover:bg-blue-500
                text-white rounded-lg font-medium
                transition-colors duration-200
              "
            >
              <FontAwesomeIcon icon={faFloppyDisk} />
              <span>Save</span>
            </button>
          </div>

          {/* Save message toast */}
          {saveMessage && (
            <div
              className={`
                flex items-center gap-2 px-4 py-3 rounded-lg
                ${
                  saveSuccess
                    ? 'bg-green-600/20 border border-green-500 text-green-400'
                    : 'bg-red-600/20 border border-red-500 text-red-400'
                }
              `}
            >
              {saveSuccess && <FontAwesomeIcon icon={faCheck} />}
              <span>{saveMessage}</span>
            </div>
          )}
        </div>
      </div>
    </MainLayout>
  );
}
