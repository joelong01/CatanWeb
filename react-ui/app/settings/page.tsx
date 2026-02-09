'use client';

import { useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import { MainLayout } from '@/components/layout';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faArrowLeft,
  faGear,
  faRotateLeft,
  faFloppyDisk,
  faCircleQuestion,
  faCheck,
} from '@fortawesome/free-solid-svg-icons';
import { useSettingsStore } from '@/lib/stores/settingsStore';
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
 * Settings page - allows users to configure game and app preferences.
 */
export default function Settings(): React.ReactElement {
  const store = useSettingsStore();
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(true);
  const [activeGameId] = useState<string | null>(() => localStorage.getItem('current_gameId'));

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

  const handleSave = useCallback(() => {
    try {
      store.saveToStorage();
      setSaveMessage('Settings saved');
      setSaveSuccess(true);
    } catch {
      setSaveMessage('Error saving settings');
      setSaveSuccess(false);
    }
  }, [store]);

  const handleReset = useCallback(() => {
    store.resetToDefaults();
    setSaveMessage('Settings reset to defaults');
    setSaveSuccess(true);
  }, [store]);

  // Determine back navigation destination
  const backHref = activeGameId ? `/game/${activeGameId}` : '/';
  const backLabel = activeGameId ? 'Back to Game' : 'Back';

  return (
    <MainLayout activeGameId={activeGameId} className="overflow-y-auto">
      <div className="min-h-screen h-full py-5 pt-[60px] pb-[60px] px-5 max-w-[600px] mx-auto">
        {/* Header */}
        <header className="flex items-center gap-4 mb-6">
          <Link
            href={backHref}
            className="
              flex items-center gap-2 px-4 py-2.5
              bg-white/5 rounded-lg
              text-gray-400 text-sm font-medium
              transition-all duration-200
              hover:bg-white/10 hover:text-white
            "
          >
            <FontAwesomeIcon icon={faArrowLeft} />
            <span>{backLabel}</span>
          </Link>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-gray-500 to-gray-700 flex items-center justify-center">
              <FontAwesomeIcon icon={faGear} className="text-white text-xl" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-white">Settings</h1>
              <p className="text-gray-400 text-sm">Configure game preferences</p>
            </div>
          </div>
        </header>

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
