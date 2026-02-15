'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { MainLayout } from '@/components/layout';
import { gameApi, type ApiResponse } from '@/lib/api/gameApi';
import type { GameTemplateSummary } from '@/types/generated/models';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faPlus,
  faLock,
  faPen,
  faCopy,
  faTrash,
  faSpinner,
} from '@fortawesome/free-solid-svg-icons';

export default function TemplateBrowser(): React.ReactElement {
  const router = useRouter();
  const [templates, setTemplates] = useState<GameTemplateSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const loadTemplates = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    const result = await gameApi.getTemplates();
    if (result.success && result.data) {
      setTemplates(result.data);
    } else {
      setError(result.error ?? 'Failed to load templates');
    }
    setIsLoading(false);
  }, []);

  useEffect(() => {
    loadTemplates();
  }, [loadTemplates]);

  const categories = useMemo(() => {
    const cats = new Set(templates.map((t) => t.category));
    return Array.from(cats).sort();
  }, [templates]);

  const filteredTemplates = useMemo(() => {
    return templates.filter((t) => {
      if (categoryFilter && t.category !== categoryFilter) return false;
      if (searchQuery && !t.name.toLowerCase().includes(searchQuery.toLowerCase())) return false;
      return true;
    });
  }, [templates, categoryFilter, searchQuery]);

  const handleEdit = useCallback(
    (id: string) => {
      router.push(`/templates/${id}`);
    },
    [router]
  );

  const handleClone = useCallback(
    async (template: GameTemplateSummary) => {
      const fullResult = await gameApi.getTemplate(template.id);
      if (!fullResult.success || !fullResult.data) {
        setError(fullResult.error ?? 'Failed to load template for cloning');
        return;
      }
      const clone = {
        ...fullResult.data,
        id: `${template.id}-copy-${Date.now()}`,
        name: `${template.name} (Copy)`,
      };
      const createResult = await gameApi.createTemplate(clone);
      if (createResult.success && createResult.data) {
        router.push(`/templates/${createResult.data.id}`);
      } else {
        setError(createResult.error ?? 'Failed to clone template');
      }
    },
    [router]
  );

  const handleDelete = useCallback(async (id: string) => {
    if (!confirm(`Delete template "${id}"? This cannot be undone.`)) return;
    setDeletingId(id);
    const result = await gameApi.deleteTemplate(id);
    if (result.success) {
      setTemplates((prev) => prev.filter((t) => t.id !== id));
    } else {
      setError(result.error ?? 'Failed to delete template');
    }
    setDeletingId(null);
  }, []);

  return (
    <MainLayout title="Game Templates" onBack={() => router.push('/')}>
      <div className="max-w-4xl mx-auto p-4 space-y-4">
        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            className="bg-white/5 border border-white/10 rounded-lg px-3 py-2 text-sm text-gray-300"
          >
            <option value="">All Categories</option>
            {categories.map((cat) => (
              <option key={cat} value={cat}>
                {cat}
              </option>
            ))}
          </select>
          <input
            type="text"
            placeholder="Search templates..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="flex-1 min-w-[200px] bg-white/5 border border-white/10 rounded-lg px-3 py-2 text-sm text-gray-300 placeholder-gray-500"
          />
          <button
            onClick={() => router.push('/templates/new')}
            className="flex items-center gap-2 bg-green-600 hover:bg-green-700 text-white rounded-lg px-4 py-2 text-sm font-medium transition-colors"
          >
            <FontAwesomeIcon icon={faPlus} />
            New Template
          </button>
        </div>

        {/* Error */}
        {error && (
          <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3 text-red-400 text-sm">
            {error}
          </div>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="flex items-center justify-center py-12 text-gray-400">
            <FontAwesomeIcon icon={faSpinner} spin className="mr-2" />
            Loading templates...
          </div>
        )}

        {/* Template grid */}
        {!isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {filteredTemplates.map((template) => (
              <div
                key={template.id}
                className="bg-white/5 border border-white/10 rounded-xl p-4 space-y-3 hover:border-white/20 transition-colors"
              >
                <div className="flex items-start justify-between">
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-white font-semibold">{template.name}</h3>
                      {template.isSystemTemplate && (
                        <FontAwesomeIcon
                          icon={faLock}
                          className="text-amber-500/60 text-xs"
                          title="System template"
                        />
                      )}
                    </div>
                    <p className="text-gray-400 text-sm mt-1">{template.description}</p>
                  </div>
                  <span className="text-xs px-2 py-1 rounded-full bg-white/10 text-gray-400 whitespace-nowrap">
                    {template.category}
                  </span>
                </div>

                <div className="flex items-center gap-4 text-xs text-gray-500">
                  <span>
                    {template.minPlayers}-{template.maxPlayers} players
                  </span>
                </div>

                <div className="flex items-center gap-2 pt-1">
                  <button
                    onClick={() => handleEdit(template.id)}
                    className="flex items-center gap-1.5 text-sm text-blue-400 hover:text-blue-300 transition-colors"
                  >
                    <FontAwesomeIcon icon={faPen} className="text-xs" />
                    Edit
                  </button>
                  <button
                    onClick={() => handleClone(template)}
                    className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-300 transition-colors"
                  >
                    <FontAwesomeIcon icon={faCopy} className="text-xs" />
                    Clone
                  </button>
                  {!template.isSystemTemplate && (
                    <button
                      onClick={() => handleDelete(template.id)}
                      disabled={deletingId === template.id}
                      className="flex items-center gap-1.5 text-sm text-red-400 hover:text-red-300 transition-colors disabled:opacity-50"
                    >
                      <FontAwesomeIcon
                        icon={deletingId === template.id ? faSpinner : faTrash}
                        className="text-xs"
                        spin={deletingId === template.id}
                      />
                      Delete
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}

        {!isLoading && filteredTemplates.length === 0 && !error && (
          <div className="text-center py-12 text-gray-500">No templates found.</div>
        )}
      </div>
    </MainLayout>
  );
}
