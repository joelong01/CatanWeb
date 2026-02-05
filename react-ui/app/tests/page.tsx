'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import Link from 'next/link';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faArrowLeft,
  faPlay,
  faStepForward,
  faRotateLeft,
  faXmark,
  faPen,
  faTrash,
  faSync,
  faCheck,
  faTimes,
} from '@fortawesome/free-solid-svg-icons';
import { MainLayout } from '@/components/layout';
import {
  gameApi,
  type RecordingSummary,
  type ActionSummary,
  type StepResult,
} from '@/lib/api/gameApi';

/** Local test result for the "Run All" results display. */
interface TestResult {
  name: string;
  success: boolean;
  message: string;
}

/** Truncate a hash to 8 chars + "..." */
function truncateHash(hash?: string | null): string {
  if (!hash) return '';
  return hash.length > 8 ? hash.slice(0, 8) + '...' : hash;
}

/** Format ISO date string to readable form. */
function formatDate(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString(undefined, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return dateStr;
  }
}

/** Return Tailwind class for action row based on replay state. */
function getRowClass(
  index: number,
  result: StepResult | undefined,
  isCurrent: boolean,
): string {
  const classes: string[] = [];
  if (isCurrent) classes.push('bg-blue-900/30 border-l-2 border-blue-400');
  if (result) {
    classes.push(result.hashMatch ? 'bg-green-900/10' : 'bg-red-900/15');
  }
  return classes.join(' ');
}

export default function TestRecordingsPage(): React.ReactElement {
  // ── Recordings state ──
  const [recordings, setRecordings] = useState<RecordingSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [runningRecordingId, setRunningRecordingId] = useState<string | null>(null);

  // ── Test results ──
  const [testResults, setTestResults] = useState<TestResult[]>([]);

  // ── Selection + actions ──
  const [selectedRecordingId, setSelectedRecordingId] = useState<string | null>(null);
  const [selectedRecordingName, setSelectedRecordingName] = useState<string | null>(null);
  const [actions, setActions] = useState<ActionSummary[]>([]);
  const [isLoadingActions, setIsLoadingActions] = useState(false);

  // ── Step-by-step replay ──
  const [replaySessionId, setReplaySessionId] = useState<string | null>(null);
  const [currentActionIndex, setCurrentActionIndex] = useState(0);
  const [actionResults, setActionResults] = useState<Map<number, StepResult>>(new Map());

  // ── Delete modal ──
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [recordingToDelete, setRecordingToDelete] = useState<RecordingSummary | null>(null);

  // ── Rename modal ──
  const [showRename, setShowRename] = useState(false);
  const [recordingToRename, setRecordingToRename] = useState<RecordingSummary | null>(null);
  const [newRecordingName, setNewRecordingName] = useState('');
  const renameInputRef = useRef<HTMLInputElement>(null);

  // ── Load recordings on mount ──
  const loadRecordings = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);
    const result = await gameApi.getRecordings();
    if (result.success && result.data) {
      setRecordings(result.data);
    } else {
      setErrorMessage(result.error ?? 'Failed to load recordings');
    }
    setIsLoading(false);
  }, []);

  useEffect(() => {
    loadRecordings();
  }, [loadRecordings]);

  // ── End replay session (fire-and-forget) ──
  const endReplaySession = useCallback(async () => {
    if (!replaySessionId) return;
    const sid = replaySessionId;
    setReplaySessionId(null);
    setCurrentActionIndex(0);
    setActionResults(new Map());
    try {
      await gameApi.endReplaySession(sid);
    } catch {
      // Ignore errors when ending session
    }
  }, [replaySessionId]);

  // ── Select recording ──
  const selectRecording = useCallback(
    async (recording: RecordingSummary) => {
      if (replaySessionId) await endReplaySession();

      setSelectedRecordingId(recording.id);
      setSelectedRecordingName(recording.name);
      setActions([]);
      setActionResults(new Map());
      setCurrentActionIndex(0);
      setIsLoadingActions(true);

      const result = await gameApi.getRecordingActions(recording.id);
      if (result.success && result.data) {
        setActions(result.data);
      } else {
        setErrorMessage(result.error ?? 'Failed to load actions');
      }
      setIsLoadingActions(false);
    },
    [replaySessionId, endReplaySession],
  );

  // ── Clear selection ──
  const clearSelection = useCallback(() => {
    if (replaySessionId) endReplaySession();
    setSelectedRecordingId(null);
    setSelectedRecordingName(null);
    setActions([]);
    setActionResults(new Map());
    setCurrentActionIndex(0);
  }, [replaySessionId, endReplaySession]);

  // ── Start replay session ──
  const startReplaySession = useCallback(async () => {
    if (!selectedRecordingId) return;
    const result = await gameApi.startReplaySession(selectedRecordingId);
    if (result.success && result.data) {
      setReplaySessionId(result.data.sessionId);
      setCurrentActionIndex(0);
      setActionResults(new Map());
    } else {
      setErrorMessage(result.error ?? 'Failed to start replay session');
    }
  }, [selectedRecordingId]);

  // ── Step replay ──
  const stepReplay = useCallback(async () => {
    if (!replaySessionId || currentActionIndex >= actions.length) return;
    const result = await gameApi.stepReplay(replaySessionId, false);
    if (result.success && result.data) {
      const stepResult = result.data;
      setActionResults((prev) => new Map(prev).set(stepResult.actionIndex, stepResult));
      setCurrentActionIndex(stepResult.actionIndex + 1);

      // Auto-scroll to current row after render
      setTimeout(() => {
        document
          .getElementById(`action-row-${stepResult.actionIndex + 1}`)
          ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }, 50);
    } else {
      setErrorMessage(result.error ?? 'Failed to execute step');
    }
  }, [replaySessionId, currentActionIndex, actions.length]);

  // ── Run single recording (full replay) ──
  const runRecording = useCallback(
    async (recordingId: string) => {
      setIsRunning(true);
      setRunningRecordingId(recordingId);
      const recording = recordings.find((r) => r.id === recordingId);
      const name = recording?.name ?? recordingId;

      try {
        const result = await gameApi.replayRecording(recordingId);
        if (result.success && result.data) {
          const data = result.data;
          if (data.success) {
            setTestResults((prev) => [
              ...prev,
              { name, success: true, message: `Replay successful (${data.actionsReplayed} actions)` },
            ]);
          } else {
            let message = data.errorMessage ?? 'Unknown error';
            if (data.failedAtAction != null) {
              message = `Failed at action ${data.failedAtAction}/${data.totalActions}: ${message}`;
              if (data.expectedHash && data.actualHash) {
                message += ` (expected: ${data.expectedHash}, got: ${data.actualHash})`;
              }
            }
            setTestResults((prev) => [...prev, { name, success: false, message }]);
          }
        } else {
          setTestResults((prev) => [
            ...prev,
            { name, success: false, message: result.error ?? 'Request failed' },
          ]);
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : 'Unknown error';
        setTestResults((prev) => [...prev, { name, success: false, message: msg }]);
      } finally {
        setRunningRecordingId(null);
        setIsRunning(false);
      }
    },
    [recordings],
  );

  // ── Run all recordings ──
  const runAllRecordings = useCallback(async () => {
    setTestResults([]);
    for (const recording of recordings) {
      await runRecording(recording.id);
    }
  }, [recordings, runRecording]);

  // ── Delete flow ──
  const confirmDelete = (recording: RecordingSummary) => {
    setRecordingToDelete(recording);
    setShowDeleteConfirm(true);
  };

  const cancelDelete = () => {
    setRecordingToDelete(null);
    setShowDeleteConfirm(false);
  };

  const deleteRecording = async () => {
    if (!recordingToDelete) return;
    const result = await gameApi.deleteRecording(recordingToDelete.id);
    if (result.success) {
      setRecordings((prev) => prev.filter((r) => r.id !== recordingToDelete.id));
      if (selectedRecordingId === recordingToDelete.id) {
        clearSelection();
      }
    } else {
      setErrorMessage(result.error ?? 'Failed to delete recording');
    }
    setRecordingToDelete(null);
    setShowDeleteConfirm(false);
  };

  // ── Rename flow ──
  const openRename = (recording: RecordingSummary) => {
    setRecordingToRename(recording);
    setNewRecordingName(recording.name);
    setShowRename(true);
    setTimeout(() => {
      renameInputRef.current?.focus();
      renameInputRef.current?.select();
    }, 50);
  };

  const cancelRename = () => {
    setRecordingToRename(null);
    setNewRecordingName('');
    setShowRename(false);
  };

  const renameRecording = async () => {
    if (!recordingToRename || !newRecordingName.trim()) return;
    const result = await gameApi.renameRecording(recordingToRename.id, newRecordingName.trim());
    if (result.success) {
      await loadRecordings();
    } else {
      setErrorMessage(result.error ?? 'Failed to rename recording');
    }
    cancelRename();
  };

  const onRenameKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && newRecordingName.trim()) {
      renameRecording();
    } else if (e.key === 'Escape') {
      cancelRename();
    }
  };

  return (
    <MainLayout className="overflow-y-auto">
      <div className="pt-[60px] px-6 pb-8 md:px-10 md:pb-10 max-w-[1400px] mx-auto">
        {/* Header */}
        <div className="flex items-center gap-4 mb-2">
          <Link href="/" className="text-gray-400 hover:text-white transition-colors">
            <FontAwesomeIcon icon={faArrowLeft} className="w-5 h-5" />
          </Link>
          <h1 className="text-2xl font-bold text-amber-400">Test Recordings</h1>
        </div>
        <p className="text-sm text-gray-500 mb-6 ml-9">
          Recorded gameplay for testing GameService consistency
        </p>

        {/* Error banner */}
        {errorMessage && (
          <div className="mb-4 px-4 py-3 rounded bg-red-900/40 border border-red-700 text-red-300 text-sm flex items-center justify-between">
            <span>{errorMessage}</span>
            <button onClick={() => setErrorMessage(null)} className="text-red-400 hover:text-white ml-4">
              <FontAwesomeIcon icon={faXmark} />
            </button>
          </div>
        )}

        {/* Loading */}
        {isLoading && (
          <div className="text-gray-400 text-center py-12">Loading recordings...</div>
        )}

        {/* Empty state */}
        {!isLoading && !errorMessage && recordings.length === 0 && (
          <div className="text-center py-12">
            <p className="text-gray-400 mb-2">No recordings found.</p>
            <p className="text-gray-500 text-sm">
              Start a game and use the recording controls to capture gameplay.
            </p>
          </div>
        )}

        {/* Recordings section */}
        {!isLoading && recordings.length > 0 && (
          <>
            {/* Section header */}
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-lg font-semibold text-gray-200">Recordings</h2>
              <div className="flex gap-2">
                <button
                  onClick={runAllRecordings}
                  disabled={isRunning}
                  className="px-3 py-1.5 rounded text-sm bg-green-700 text-white hover:bg-green-600 transition-colors disabled:opacity-50"
                >
                  <FontAwesomeIcon icon={faPlay} className="mr-1.5" />
                  Run All
                </button>
                <button
                  onClick={loadRecordings}
                  disabled={isRunning}
                  className="px-3 py-1.5 rounded text-sm bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors disabled:opacity-50"
                >
                  <FontAwesomeIcon icon={faSync} className="mr-1.5" />
                  Refresh
                </button>
              </div>
            </div>

            {/* Recordings table */}
            <div className="max-h-[300px] overflow-y-auto overflow-x-auto border border-gray-700 rounded-lg mb-6">
              <table className="w-full text-sm">
                <thead className="sticky top-0 z-10 bg-gray-700">
                  <tr>
                    <th className="px-4 py-2.5 text-left text-gray-200 font-semibold">Name</th>
                    <th className="px-4 py-2.5 text-left text-gray-200 font-semibold">Date</th>
                    <th className="px-4 py-2.5 text-left text-gray-200 font-semibold">Type</th>
                    <th className="px-4 py-2.5 text-center text-gray-200 font-semibold">Players</th>
                    <th className="px-4 py-2.5 text-center text-gray-200 font-semibold">Actions</th>
                    <th className="px-4 py-2.5 text-right text-gray-200 font-semibold" />
                  </tr>
                </thead>
                <tbody>
                  {recordings.map((rec) => (
                    <tr
                      key={rec.id}
                      className={`border-t border-gray-700/50 transition-colors ${
                        rec.id === selectedRecordingId
                          ? 'bg-blue-900/20 border-l-2 border-l-blue-400'
                          : 'hover:bg-gray-800/50'
                      }`}
                    >
                      <td className="px-4 py-2 text-gray-100 font-medium">{rec.name}</td>
                      <td className="px-4 py-2 text-gray-400 text-xs">{formatDate(rec.createdAt)}</td>
                      <td className="px-4 py-2 text-gray-400">{rec.gameType}</td>
                      <td className="px-4 py-2 text-gray-400 text-center">{rec.playerCount}</td>
                      <td className="px-4 py-2 text-gray-400 text-center">{rec.actionCount}</td>
                      <td className="px-4 py-2 text-right">
                        {runningRecordingId === rec.id ? (
                          <span className="text-yellow-400 text-xs animate-pulse">Running...</span>
                        ) : (
                          <div className="flex gap-1.5 justify-end">
                            <button
                              onClick={() => selectRecording(rec)}
                              disabled={isRunning}
                              className="px-2 py-1 rounded text-xs bg-blue-700 text-white hover:bg-blue-600 disabled:opacity-50"
                            >
                              Select
                            </button>
                            <button
                              onClick={() => runRecording(rec.id)}
                              disabled={isRunning}
                              className="px-2 py-1 rounded text-xs bg-green-700 text-white hover:bg-green-600 disabled:opacity-50"
                            >
                              Run
                            </button>
                            <button
                              onClick={() => openRename(rec)}
                              disabled={isRunning}
                              className="px-2 py-1 rounded text-xs bg-gray-600 text-gray-200 hover:bg-gray-500 disabled:opacity-50"
                            >
                              <FontAwesomeIcon icon={faPen} />
                            </button>
                            <button
                              onClick={() => confirmDelete(rec)}
                              disabled={isRunning}
                              className="px-2 py-1 rounded text-xs bg-red-800 text-red-200 hover:bg-red-700 disabled:opacity-50"
                            >
                              <FontAwesomeIcon icon={faTrash} />
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Test results */}
            {testResults.length > 0 && (
              <div className="mb-6 space-y-1.5">
                <h3 className="text-sm font-semibold text-gray-300 mb-2">Results</h3>
                {testResults.map((tr, i) => (
                  <div
                    key={i}
                    className={`flex items-center gap-3 px-4 py-2 rounded text-sm ${
                      tr.success
                        ? 'bg-green-900/20 border-l-4 border-green-500 text-green-300'
                        : 'bg-red-900/20 border-l-4 border-red-500 text-red-300'
                    }`}
                  >
                    <FontAwesomeIcon
                      icon={tr.success ? faCheck : faTimes}
                      className={`w-3.5 h-3.5 ${tr.success ? 'text-green-400' : 'text-red-400'}`}
                    />
                    <span className="font-medium text-gray-200">{tr.name}</span>
                    <span className="text-xs">{tr.message}</span>
                  </div>
                ))}
              </div>
            )}

            {/* Actions detail (when recording selected) */}
            {selectedRecordingId && (
              <>
                {/* Actions section header */}
                <div className="flex items-center justify-between mb-3">
                  <h2 className="text-lg font-semibold text-gray-200">
                    Actions: <span className="text-amber-400">{selectedRecordingName}</span>
                  </h2>
                  <div className="flex gap-2">
                    {replaySessionId == null ? (
                      <button
                        onClick={startReplaySession}
                        disabled={isLoadingActions}
                        className="px-3 py-1.5 rounded text-sm bg-blue-700 text-white hover:bg-blue-600 transition-colors disabled:opacity-50"
                      >
                        <FontAwesomeIcon icon={faPlay} className="mr-1.5" />
                        Start Replay
                      </button>
                    ) : (
                      <>
                        <button
                          onClick={stepReplay}
                          disabled={currentActionIndex >= actions.length}
                          className="px-3 py-1.5 rounded text-sm bg-blue-700 text-white hover:bg-blue-600 transition-colors disabled:opacity-50"
                        >
                          <FontAwesomeIcon icon={faStepForward} className="mr-1.5" />
                          Run Step
                        </button>
                        <button
                          onClick={endReplaySession}
                          className="px-3 py-1.5 rounded text-sm bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors"
                        >
                          <FontAwesomeIcon icon={faRotateLeft} className="mr-1.5" />
                          Reset
                        </button>
                      </>
                    )}
                    <button
                      onClick={clearSelection}
                      className="px-3 py-1.5 rounded text-sm bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors"
                    >
                      <FontAwesomeIcon icon={faXmark} className="mr-1.5" />
                      Close
                    </button>
                  </div>
                </div>

                {/* Actions table */}
                {isLoadingActions ? (
                  <div className="text-gray-400 text-center py-8">Loading actions...</div>
                ) : (
                  <div className="max-h-[400px] overflow-y-auto overflow-x-auto border border-gray-700 rounded-lg">
                    <table className="w-full text-sm">
                      <thead className="sticky top-0 z-10 bg-gray-700">
                        <tr>
                          <th className="px-3 py-2.5 text-center text-gray-200 font-semibold w-12">#</th>
                          <th className="px-3 py-2.5 text-left text-gray-200 font-semibold">Action</th>
                          <th className="px-3 py-2.5 text-left text-gray-200 font-semibold">State</th>
                          <th className="px-3 py-2.5 text-left text-gray-200 font-semibold">Details</th>
                          <th className="px-3 py-2.5 text-left text-gray-200 font-semibold font-mono text-xs">Expected</th>
                          <th className="px-3 py-2.5 text-left text-gray-200 font-semibold font-mono text-xs">Actual</th>
                          <th className="px-3 py-2.5 text-center text-gray-200 font-semibold w-12">OK</th>
                        </tr>
                      </thead>
                      <tbody>
                        {actions.map((action) => {
                          const result = actionResults.get(action.index);
                          const isCurrent =
                            replaySessionId != null && action.index === currentActionIndex;
                          return (
                            <tr
                              key={action.index}
                              id={`action-row-${action.index}`}
                              className={`border-t border-gray-700/30 transition-colors ${getRowClass(
                                action.index,
                                result,
                                isCurrent,
                              )}`}
                            >
                              <td className="px-3 py-1.5 text-center text-gray-500 font-mono text-xs">
                                {action.index}
                              </td>
                              <td className="px-3 py-1.5 text-gray-200">{action.actionType}</td>
                              <td className="px-3 py-1.5 text-gray-400">{action.gameState}</td>
                              <td className="px-3 py-1.5 text-gray-400 text-xs">{action.details}</td>
                              <td className="px-3 py-1.5 font-mono text-gray-500 text-xs">
                                {truncateHash(action.expectedHash)}
                              </td>
                              <td className="px-3 py-1.5 font-mono text-gray-500 text-xs">
                                {result ? truncateHash(result.actualHash) : ''}
                              </td>
                              <td className="px-3 py-1.5 text-center">
                                {result && (
                                  <FontAwesomeIcon
                                    icon={result.hashMatch ? faCheck : faTimes}
                                    className={`w-3.5 h-3.5 ${
                                      result.hashMatch ? 'text-green-400' : 'text-red-400'
                                    }`}
                                  />
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </>
            )}
          </>
        )}

        {/* Delete confirmation modal */}
        {showDeleteConfirm && recordingToDelete && (
          <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-[1000]">
            <div className="bg-gray-800 p-6 rounded-lg max-w-sm w-full mx-4 text-center">
              <h3 className="text-lg font-semibold text-white mb-3">Delete Recording?</h3>
              <p className="text-gray-300 text-sm mb-1">
                Are you sure you want to delete &quot;{recordingToDelete.name}&quot;?
              </p>
              <p className="text-gray-500 text-xs mb-6">This action cannot be undone.</p>
              <div className="flex gap-3 justify-center">
                <button
                  onClick={cancelDelete}
                  className="px-4 py-2 rounded text-sm bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors"
                >
                  Cancel
                </button>
                <button
                  onClick={deleteRecording}
                  className="px-4 py-2 rounded text-sm bg-red-700 text-white hover:bg-red-600 transition-colors"
                >
                  Delete
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Rename modal */}
        {showRename && recordingToRename && (
          <div className="fixed inset-0 bg-black/70 flex items-center justify-center z-[1000]">
            <div className="bg-gray-800 p-6 rounded-lg max-w-sm w-full mx-4">
              <h3 className="text-lg font-semibold text-white mb-3">Rename Recording</h3>
              <p className="text-gray-400 text-sm mb-3">
                Enter a new name for &quot;{recordingToRename.name}&quot;:
              </p>
              <input
                ref={renameInputRef}
                type="text"
                value={newRecordingName}
                onChange={(e) => setNewRecordingName(e.target.value)}
                onKeyDown={onRenameKeyDown}
                className="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm focus:outline-none focus:border-blue-500 mb-4"
              />
              <div className="flex gap-3 justify-end">
                <button
                  onClick={cancelRename}
                  className="px-4 py-2 rounded text-sm bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors"
                >
                  Cancel
                </button>
                <button
                  onClick={renameRecording}
                  disabled={!newRecordingName.trim()}
                  className="px-4 py-2 rounded text-sm bg-blue-700 text-white hover:bg-blue-600 transition-colors disabled:opacity-50"
                >
                  Rename
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </MainLayout>
  );
}
