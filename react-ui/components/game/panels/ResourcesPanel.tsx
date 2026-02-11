'use client';

/**
 * ResourcesPanel - Shows total game resources.
 *
 * Displays the aggregate resources collected by all players.
 */

import type { ResourcesModel } from '@/types/generated/models/resources-model';
import { resourceCount } from '@/lib/extensions/resourcesExtensions';

interface ResourcesPanelProps {
  /** Game resources model */
  resources: ResourcesModel | null;
}

/** Resource display config */
const RESOURCES = [
  { key: 'wheat', label: 'Wheat', icon: '🌾', color: 'bg-yellow-600' },
  { key: 'wood', label: 'Wood', icon: '🪵', color: 'bg-green-700' },
  { key: 'sheep', label: 'Sheep', icon: '🐑', color: 'bg-lime-500' },
  { key: 'brick', label: 'Brick', icon: '🧱', color: 'bg-red-700' },
  { key: 'ore', label: 'Ore', icon: '⛰️', color: 'bg-gray-500' },
] as const;

export function ResourcesPanel({ resources }: ResourcesPanelProps): React.ReactElement {
  if (!resources) {
    return <div className="p-4 text-gray-400 text-center">No resource data</div>;
  }

  return (
    <div className="p-3">
      <div className="grid grid-cols-5 gap-2">
        {RESOURCES.map(({ key, label, icon, color }) => (
          <div key={key} className="flex flex-col items-center" title={label}>
            <div
              className={`
                w-10 h-10 rounded-lg flex items-center justify-center
                ${color} text-white text-lg shadow-md
              `}
            >
              {icon}
            </div>
            <span className="mt-1 text-sm font-bold text-gray-200">
              {resources[key as keyof ResourcesModel]}
            </span>
            <span className="text-[10px] text-gray-500">{label}</span>
          </div>
        ))}
      </div>

      {/* Total resources */}
      <div className="mt-3 pt-2 border-t border-gray-700 text-center">
        <span className="text-xs text-gray-400">Total Resources: </span>
        <span className="text-sm font-bold text-gray-200">{resourceCount(resources)}</span>
      </div>
    </div>
  );
}

export default ResourcesPanel;
