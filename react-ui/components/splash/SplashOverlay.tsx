/**
 * @module splash/SplashOverlay
 *
 * Full-screen overlay for the three-phase startup flow:
 *   Phase 1: Health check results (from useHealthCheck)
 *   Phase 2: Azure sign-in (when health fails and Azure is configured)
 *   Phase 3: Infrastructure repair via SSE (after sign-in)
 *
 * On healthy systems, this component is never rendered — the home page
 * appears immediately with no delay.
 */

'use client';

import React, { useState, useCallback } from 'react';
import type { HealthResult } from '@/lib/hooks/useHealthCheck';
import { isAzureConfigured } from '@/lib/azure/msalConfig';
import type { CheckResult } from '@/lib/azure/types';
import CheckRow from './CheckRow';
import type { CheckRowStatus } from './CheckRow';
import AzureSignIn from './AzureSignIn';

// ── Phase type ───────────────────────────────────────────────────────────────

type Phase = 'health' | 'azure-signin' | 'azure-doctor' | 'done';

// ── Props ────────────────────────────────────────────────────────────────────

interface SplashOverlayProps {
  /** Health check result from useHealthCheck(). */
  health: HealthResult;
  /** Callback to re-run the health check. */
  onRetry: () => void;
  /** Callback to dismiss the overlay (shown when health is ok). */
  onDismiss?: () => void;
}

// ── Health check row mapping ─────────────────────────────────────────────────

/**
 * Maps the health check result into individual check rows for display.
 * Phase 1 is a single /health call, but we break it down for the user.
 */
function getHealthCheckRows(health: HealthResult): Array<{
  label: string;
  status: CheckRowStatus;
  detail?: string;
  error?: string;
}> {
  const { status, data, error } = health;

  if (status === 'checking' || status === 'retrying') {
    const detail = status === 'retrying'
      ? `Attempt ${health.retryCount + 1}... ${error ?? ''}`
      : 'Connecting...';
    return [
      { label: 'Game Service', status: 'running', detail },
      { label: 'Database', status: 'pending' },
      { label: 'Players', status: 'pending' },
      { label: 'Templates', status: 'pending' },
    ];
  }

  if (status === 'healthy' && data) {
    const db = data.databaseDiagnostics;
    return [
      { label: 'Game Service', status: 'ok', detail: `${data.version.environment} (${data.version.commit})` },
      { label: 'Database', status: db.connected ? 'ok' : 'error', detail: db.connected ? 'Connected' : 'Disconnected', error: db.error },
      { label: 'Players', status: db.playerCount > 0 ? 'ok' : 'warning', detail: `${db.playerCount} players` },
      { label: 'Templates', status: db.templateCount > 0 ? 'ok' : 'warning', detail: `${db.templateCount} templates` },
      { label: 'Games', status: 'ok', detail: `${db.gameCount} saved` },
      { label: 'Recordings', status: 'ok', detail: `${db.recordingCount} saved` },
    ];
  }

  if (data) {
    const db = data.databaseDiagnostics;
    return [
      { label: 'Game Service', status: 'ok', detail: `${data.version.environment} (${data.version.commit})` },
      { label: 'Database', status: 'error', detail: 'Connection failed', error: db?.error ?? error },
      { label: 'Players', status: 'pending' },
      { label: 'Templates', status: 'pending' },
    ];
  }

  return [
    { label: 'Game Service', status: 'error', detail: 'Unreachable', error },
    { label: 'Database', status: 'pending' },
    { label: 'Players', status: 'pending' },
    { label: 'Templates', status: 'pending' },
  ];
}

// ── Azure doctor check name → display label ──────────────────────────────────

const AZURE_CHECK_LABELS: Record<string, string> = {
  cosmosAccount: 'Cosmos Account',
  cosmosFirewall: 'Cosmos Firewall',
  cosmosContainers: 'Cosmos Containers',
  appServiceConfig: 'App Service Config',
  cosmosRbac: 'Cosmos RBAC',
  resourceGroup: 'Resource Group',
  appServicePlan: 'App Service Plan',
  webApp: 'Web App',
  appConfig: 'App Config',
  healthEndpoint: 'Health Endpoint',
  deployment: 'Deployment',
  uiWebApp: 'UI Web App',
  gameServiceUrl: 'GameService URL',
  siteResponding: 'Site Responding',
  uiDeployment: 'UI Deployment',
  appRegistration: 'App Registration',
  servicePrincipal: 'Service Principal',
  federatedCredentials: 'Federated Creds',
  contributorRole: 'Contributor Role',
};

// ── Component ────────────────────────────────────────────────────────────────

export default function SplashOverlay({ health, onRetry, onDismiss }: SplashOverlayProps) {
  const [phase, setPhase] = useState<Phase>('health');
  const [azureChecks, setAzureChecks] = useState<CheckResult[]>([]);
  const [azureError, setAzureError] = useState<string | null>(null);

  const healthRows = getHealthCheckRows(health);
  const isChecking = health.status === 'checking' || health.status === 'retrying';
  const showAzureOption = health.status === 'failed' && isAzureConfigured();

  /**
   * Phase 3: Stream Azure doctor results from the Next.js API route.
   * Each SSE event updates the check list in real time.
   */
  const runAzureDoctor = useCallback(async (token: string) => {
    setPhase('azure-doctor');
    setAzureChecks([]);
    setAzureError(null);

    try {
      const response = await fetch('/api/azure/doctor?fix=true', {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}` },
      });

      if (!response.ok) {
        const body = await response.text();
        setAzureError(`Azure doctor failed: HTTP ${response.status} — ${body}`);
        setPhase('done');
        return;
      }

      // Read SSE stream
      const reader = response.body?.getReader();
      if (!reader) {
        setAzureError('No response stream');
        setPhase('done');
        return;
      }

      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });

        // Parse SSE events from buffer
        const lines = buffer.split('\n');
        buffer = lines.pop() ?? ''; // Keep incomplete line in buffer

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            try {
              const data = JSON.parse(line.slice(6));
              // Update or add check result
              setAzureChecks((prev) => {
                const idx = prev.findIndex((c) => c.check === data.check);
                if (idx >= 0) {
                  const updated = [...prev];
                  updated[idx] = data;
                  return updated;
                }
                // Skip domain header events (they start with ──)
                if (data.check?.startsWith('──')) return prev;
                return [...prev, data];
              });
            } catch {
              // Ignore unparseable lines
            }
          }
        }
      }

      // After stream completes, re-run Phase 1 to verify everything works
      setPhase('done');
      onRetry();
    } catch (err) {
      setAzureError(`Stream error: ${err instanceof Error ? err.message : String(err)}`);
      setPhase('done');
    }
  }, [onRetry]);

  /** Phase 2 → Phase 3 transition: token acquired, start doctor. */
  const handleTokenAcquired = useCallback((token: string) => {
    runAzureDoctor(token);
  }, [runAzureDoctor]);

  // ── Subtitle ─────────────────────────────────────────────────────────────

  let subtitle = 'Checking services...';
  if (health.status === 'healthy') subtitle = 'All services healthy';
  else if (health.status === 'failed') subtitle = 'Service issue detected';
  else if (phase === 'azure-signin') subtitle = 'Azure sign-in required';
  else if (phase === 'azure-doctor') subtitle = 'Diagnosing infrastructure...';
  else if (phase === 'done' && azureError) subtitle = 'Repair failed';
  else if (phase === 'done') subtitle = 'Repair complete — verifying...';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 backdrop-blur-sm">
      <div className="w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        {/* Catan branding */}
        <div className="text-center mb-8">
          <h1
            className="text-5xl text-amber-400 tracking-wide"
            style={{ fontFamily: 'var(--font-catan)' }}
          >
            Catan
          </h1>
          <p className="text-gray-400 text-sm mt-2">{subtitle}</p>
        </div>

        {/* Phase 1: Health check results */}
        <div className="bg-gray-900/80 rounded-lg border border-gray-700 px-4 py-3 mb-4">
          <p className="text-xs text-gray-500 mb-2 uppercase tracking-wider">Service Health</p>
          {healthRows.map((row) => (
            <CheckRow key={row.label} {...row} />
          ))}
        </div>

        {/* Step log — shows exactly what's happening for troubleshooting */}
        {health.steps.length > 0 && (
          <div className="bg-gray-900/80 rounded-lg border border-gray-700 px-4 py-3 mb-4">
            <p className="text-xs text-gray-500 mb-2 uppercase tracking-wider">Activity Log</p>
            <div className="font-mono text-[11px] text-gray-400 space-y-0.5 max-h-40 overflow-y-auto">
              {health.steps.map((step, i) => (
                <div key={i} className={step.startsWith('Error') ? 'text-red-400' : step.startsWith('All checks') ? 'text-green-400' : ''}>
                  {step}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Phase 3: Azure doctor results (when running) */}
        {(phase === 'azure-doctor' || (phase === 'done' && azureChecks.length > 0)) && (
          <div className="bg-gray-900/80 rounded-lg border border-gray-700 px-4 py-3 mb-4">
            <p className="text-xs text-gray-500 mb-2 uppercase tracking-wider">Azure Infrastructure</p>
            {azureChecks.map((check) => (
              <CheckRow
                key={check.check}
                label={AZURE_CHECK_LABELS[check.check] ?? check.check}
                status={check.status === 'warning' ? 'warning' : check.status as CheckRowStatus}
                durationMs={check.durationMs}
                detail={check.detail}
                error={check.error}
              />
            ))}
          </div>
        )}

        {/* Azure error */}
        {azureError && (
          <div className="bg-red-900/30 border border-red-700 rounded-lg px-4 py-3 mb-4">
            <p className="text-red-400 text-sm">{azureError}</p>
          </div>
        )}

        {/* Actions — context-dependent */}
        <div className="flex flex-col items-center gap-3 mt-4">
          {/* Healthy: dismiss button */}
          {health.status === 'healthy' && onDismiss && phase !== 'azure-doctor' && (
            <button
              onClick={onDismiss}
              className="px-6 py-2 bg-green-600 hover:bg-green-500 text-white rounded-md font-medium transition-colors"
            >
              All Good — Continue
            </button>
          )}

          {/* Failed + Azure configured: show sign-in (Phase 2) */}
          {showAzureOption && phase === 'health' && (
            <>
              <AzureSignIn onTokenAcquired={handleTokenAcquired} />
              <div className="w-full border-t border-gray-700 my-1" />
            </>
          )}

          {/* Failed: retry button */}
          {health.status === 'failed' && phase !== 'azure-doctor' && (
            <>
              <button
                onClick={onRetry}
                className="px-6 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-md font-medium transition-colors"
              >
                Retry
              </button>
              {!showAzureOption && (
                <p className="text-gray-500 text-xs text-center">
                  If this persists, run <code className="text-gray-400">./catan.ps1 doctor</code> for help.
                </p>
              )}
            </>
          )}

          {/* Retrying indicator */}
          {health.status === 'retrying' && (
            <p className="text-gray-400 text-sm">
              Retry {health.retryCount} of 3...
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
