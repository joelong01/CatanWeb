/**
 * @module azure/types
 *
 * Shared type definitions for the Azure Doctor infrastructure checker.
 * Used by the CLI entry point, the Next.js API route, and the individual
 * check modules under `./checks/`.
 */

// ── Check lifecycle ──────────────────────────────────────────────────────────

/** Progression of an individual doctor check through its lifecycle. */
export type CheckStatus = 'pending' | 'running' | 'ok' | 'fixing' | 'error' | 'warning';

/**
 * Result of a single doctor check.
 *
 * Emitted by the reporter callback as each check progresses through its
 * lifecycle: pending → running → (fixing →) ok | error | warning.
 */
export interface CheckResult {
  /** Identifier for this check (e.g., 'cosmosAccount', 'gameServiceHealth'). */
  check: string;

  /** Current lifecycle state. */
  status: CheckStatus;

  /** Human-readable detail shown alongside the status. */
  detail?: string;

  /** Wall-clock time for the check, set on terminal states. */
  durationMs?: number;

  /** Error message, present when status is 'error'. */
  error?: string;
}

/**
 * Callback for reporting check progress.
 *
 * Called multiple times per check as it transitions through states.
 * The CLI uses this to print live console output; the web API route
 * uses it to emit SSE events.
 */
export type CheckReporter = (result: CheckResult) => void;

// ── Configuration ────────────────────────────────────────────────────────────

/**
 * Azure resource identifiers needed to run all doctor checks.
 *
 * Populated from `.azure/catan-azure.json` (CLI) or from environment
 * variables / forwarded config (web).
 */
export interface AzureConfig {
  /** Base name used to derive other resource names (e.g., 'catan'). */
  baseName: string;

  /** Azure subscription ID (not in catan-azure.json — comes from env or arg). */
  subscriptionId: string;

  /** Resource group containing all Catan Azure resources (e.g., 'rg-catan'). */
  resourceGroup: string;

  /** CosmosDB account name (e.g., 'cosmos-catan'). */
  cosmosAccountName: string;

  /** CosmosDB database name within the account (e.g., 'catan'). */
  cosmosDatabaseName: string;

  /** App Service name for the GameService backend (e.g., 'catan-api'). */
  gameServiceAppName: string;

  /** App Service name for the React UI (e.g., 'catan'). */
  uiAppName: string;

  /** GameService URL for health checks (e.g., 'https://catan-api.azurewebsites.net'). */
  gameServiceUrl: string;

  /** UI URL for site-responding checks (e.g., 'https://catan.azurewebsites.net'). */
  uiUrl: string;

  /** Whether to check the staging slot instead of production. */
  staging: boolean;
}

// ── Aggregate results ────────────────────────────────────────────────────────

/** Result from a single doctor domain (database, game-service, ui, github). */
export interface DomainResult {
  /** Domain identifier. */
  domain: 'database' | 'game-service' | 'ui' | 'github';

  /** Display name (e.g., 'cosmos-catan', 'catan-api (staging)'). */
  name: string;

  /** Individual check results in execution order. */
  checks: CheckResult[];

  /** True only if every check reached 'ok' or 'warning' status. */
  healthy: boolean;
}

/** Aggregate result from a full doctor run across all domains. */
export interface DoctorResult {
  /** Results per domain. */
  domains: DomainResult[];

  /** True only if every domain is healthy. */
  allPassed: boolean;
}

// ── Constants ────────────────────────────────────────────────────────────────

/**
 * Required Cosmos containers and their partition key paths.
 *
 * Must stay in sync with `.scripts/database.ps1 $Containers` and
 * `CosmosCatanDb.InitializeAsync()`.
 */
export const REQUIRED_CONTAINERS: Record<string, string> = {
  players: '/id',
  games: '/id',
  'completed-games': '/id',
  templates: '/id',
  recordings: '/id',
};

/**
 * Cosmos DB Built-in Data Contributor role definition ID.
 *
 * This is a Cosmos-level SQL role (NOT an Azure RBAC role). Managed via
 * `CosmosDBManagementClient.sqlResources`, not `AuthorizationManagementClient`.
 *
 * @see https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-setup-rbac
 */
export const COSMOS_DATA_CONTRIBUTOR_ROLE_ID = '00000000-0000-0000-0000-000000000002';

// ── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Extracts a human-readable message from an unknown error value.
 * Azure SDK errors typically extend `Error` with a `message` property.
 */
export function formatError(err: unknown): string {
  if (err instanceof Error) return err.message;
  return String(err);
}

/**
 * Creates a timed check helper that handles the common pattern:
 * report running → do work → report ok/error with duration.
 */
export function timedCheck(
  checkName: string,
  report: CheckReporter
): {
  start: number;
  ok: (detail: string) => CheckResult;
  fail: (error: string, detail?: string) => CheckResult;
  warn: (detail: string) => CheckResult;
} {
  const start = Date.now();
  return {
    start,
    ok(detail: string): CheckResult {
      const r: CheckResult = {
        check: checkName,
        status: 'ok',
        detail,
        durationMs: Date.now() - start,
      };
      report(r);
      return r;
    },
    fail(error: string, detail?: string): CheckResult {
      const r: CheckResult = {
        check: checkName,
        status: 'error',
        error,
        detail,
        durationMs: Date.now() - start,
      };
      report(r);
      return r;
    },
    warn(detail: string): CheckResult {
      const r: CheckResult = {
        check: checkName,
        status: 'warning',
        detail,
        durationMs: Date.now() - start,
      };
      report(r);
      return r;
    },
  };
}
