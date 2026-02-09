'use client';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faVideo, faFloppyDisk, faChartLine, faGavel } from '@fortawesome/free-solid-svg-icons';

/**
 * Game options configuration.
 */
export interface GameOptionsState {
  /** Whether to record the game for replay */
  recordGame: boolean;
  /** Whether to save the game to database */
  saveGame: boolean;
  /** Whether to update player lifetime stats */
  updateStats: boolean;
  /** Whether to use house rules (enables default house rules) */
  useHouseRules: boolean;
}

/**
 * Props for the GameOptions component.
 */
interface GameOptionsProps {
  /** Current options state */
  options: GameOptionsState;
  /** Callback when options change */
  onChange: (options: GameOptionsState) => void;
}

/**
 * Default game options.
 */
export const DEFAULT_GAME_OPTIONS: GameOptionsState = {
  recordGame: true,
  saveGame: true,
  updateStats: true,
  useHouseRules: true, // ON by default, matching Blazor behavior
};

/**
 * Compact option item with checkbox.
 */
interface CompactOptionProps {
  icon: typeof faVideo;
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

function CompactOption({ icon, label, checked, onChange }: CompactOptionProps): React.ReactElement {
  return (
    <label className="flex items-center gap-3 cursor-pointer group">
      <div
        className={`
          w-5 h-5 rounded border-2 flex items-center justify-center
          transition-all duration-200
          ${checked ? 'bg-blue-500 border-blue-500' : 'border-gray-500 group-hover:border-gray-400'}
        `}
      >
        {checked && (
          <svg className="w-3 h-3 text-white" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z"
              clipRule="evenodd"
            />
          </svg>
        )}
      </div>
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="sr-only"
      />
      <FontAwesomeIcon
        icon={icon}
        className={`text-sm ${checked ? 'text-blue-400' : 'text-gray-500'}`}
      />
      <span className={`text-sm ${checked ? 'text-white' : 'text-gray-400'}`}>{label}</span>
    </label>
  );
}

/**
 * Game options configuration section - compact 2x2 grid layout.
 * Allows toggling recording, saving, stats tracking, and house rules.
 */
export function GameOptions({ options, onChange }: GameOptionsProps): React.ReactElement {
  return (
    <div className="mb-6">
      <h2 className="text-xl font-semibold text-white mb-4">Game Options</h2>

      <div className="grid grid-cols-2 gap-4 p-4 bg-game-bg-panel rounded-xl border-2 border-white/10">
        <CompactOption
          icon={faVideo}
          label="Record Game"
          checked={options.recordGame}
          onChange={(checked) => onChange({ ...options, recordGame: checked })}
        />

        <CompactOption
          icon={faFloppyDisk}
          label="Save Game"
          checked={options.saveGame}
          onChange={(checked) => onChange({ ...options, saveGame: checked })}
        />

        <CompactOption
          icon={faChartLine}
          label="Update Stats"
          checked={options.updateStats}
          onChange={(checked) => onChange({ ...options, updateStats: checked })}
        />

        <CompactOption
          icon={faGavel}
          label="House Rules"
          checked={options.useHouseRules}
          onChange={(checked) => onChange({ ...options, useHouseRules: checked })}
        />
      </div>
    </div>
  );
}
