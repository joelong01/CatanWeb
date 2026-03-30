/**
 * @module azure/checks/gameServiceDoctor
 *
 * GameService App Service checks — the TypeScript port of
 * `.scripts/catan-azure.ps1 Get-GameServiceDoctor`.
 *
 * Checks:
 *   1. Resource group exists
 *   2. App Service Plan exists with acceptable SKU
 *   3. Web App exists with managed identity
 *   4. Always On enabled + adequate startup timeout
 *   5. Health endpoint responds (with cold-start retry)
 *   6. Deployed commit matches current git HEAD
 */

import type { WebSiteManagementClient } from '@azure/arm-appservice';
import type { ResourceManagementClient } from '@azure/arm-resources';
import type { AzureConfig, CheckReporter, CheckResult, DomainResult } from '../types';
import { formatError, timedCheck } from '../types';
import { execSync } from 'child_process';

/** Validates a string looks like a short or full git commit hash (hex only). */
const COMMIT_HASH_RE = /^[0-9a-f]{7,40}$/i;

/**
 * Runs all GameService doctor checks in sequence.
 */
export async function runGameServiceDoctor(
  webClient: WebSiteManagementClient,
  resourceClient: ResourceManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<DomainResult> {
  const checks: CheckResult[] = [];
  const slotSuffix = config.staging ? ' (staging)' : '';
  const appName = config.gameServiceAppName;
  const targetUrl = config.staging
    ? `https://${appName}-staging.azurewebsites.net`
    : config.gameServiceUrl;

  // 1. Resource group
  const rgResult = await checkResourceGroup(resourceClient, config, report);
  checks.push(rgResult);
  if (rgResult.status === 'error') {
    return { domain: 'game-service', name: `${appName}${slotSuffix}`, checks, healthy: false };
  }

  // 2. App Service Plan
  const planResult = await checkAppServicePlan(webClient, config, report);
  checks.push(planResult);

  // 3. Web App + managed identity
  const appResult = await checkWebApp(webClient, config, report);
  checks.push(appResult);
  if (appResult.status === 'error') {
    return { domain: 'game-service', name: `${appName}${slotSuffix}`, checks, healthy: false };
  }

  // 4. Always On + startup timeout
  const configResult = await checkAppConfig(webClient, config, report);
  checks.push(configResult);

  // 5. Health endpoint (with retry for cold starts)
  const healthResult = await checkHealthEndpoint(targetUrl, report);
  checks.push(healthResult);

  // 6. Deployment status
  const deployResult = await checkDeployment(targetUrl, report);
  checks.push(deployResult);

  return {
    domain: 'game-service',
    name: `${appName}${slotSuffix}`,
    checks,
    healthy: checks.every((c) => c.status === 'ok' || c.status === 'warning'),
  };
}

// ── Individual checks ────────────────────────────────────────────────────────

/** Verifies the resource group exists. */
async function checkResourceGroup(
  client: ResourceManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('resourceGroup', report);
  report({ check: 'resourceGroup', status: 'running', detail: 'Checking resource group...' });

  try {
    const exists = await client.resourceGroups.checkExistence(config.resourceGroup);
    if (exists) {
      return t.ok(config.resourceGroup);
    }
    return t.fail(`Resource group '${config.resourceGroup}' not found`);
  } catch (err) {
    return t.fail(`Failed to check resource group: ${formatError(err)}`);
  }
}

/** Verifies the App Service Plan exists and checks the SKU tier. */
async function checkAppServicePlan(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('appServicePlan', report);
  report({ check: 'appServicePlan', status: 'running', detail: 'Checking App Service Plan...' });

  try {
    const planName = `asp-${config.baseName}`;
    const plan = await client.appServicePlans.get(config.resourceGroup, planName);
    const sku = plan.sku?.name ?? 'unknown';
    const weakSkus = ['F1', 'D1'];

    if (weakSkus.includes(sku)) {
      return t.warn(`SKU=${sku} (cold start penalties, consider B1+)`);
    }
    return t.ok(`SKU=${sku}`);
  } catch (err) {
    return t.fail(`App Service Plan not found: ${formatError(err)}`);
  }
}

/** Verifies the Web App exists and has a managed identity. */
async function checkWebApp(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('webApp', report);
  report({ check: 'webApp', status: 'running', detail: 'Checking Web App...' });

  try {
    const site = await client.webApps.get(config.resourceGroup, config.gameServiceAppName);
    const hasIdentity = !!site.identity?.principalId;
    const state = site.state ?? 'unknown';

    if (!hasIdentity) {
      return t.warn(`state=${state}, managed identity NOT enabled`);
    }
    return t.ok(`state=${state}, managed identity enabled`);
  } catch (err) {
    return t.fail(`Web App '${config.gameServiceAppName}' not found: ${formatError(err)}`);
  }
}

/** Checks Always On setting and container startup timeout. */
async function checkAppConfig(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('appConfig', report);
  report({ check: 'appConfig', status: 'running', detail: 'Checking Always On + startup timeout...' });

  try {
    const siteConfig = await client.webApps.getConfiguration(config.resourceGroup, config.gameServiceAppName);
    const alwaysOn = siteConfig.alwaysOn ?? false;

    const settings = await client.webApps.listApplicationSettings(config.resourceGroup, config.gameServiceAppName);
    const timeoutStr = settings.properties?.['WEBSITES_CONTAINER_START_TIME_LIMIT'];
    const timeout = timeoutStr ? parseInt(timeoutStr, 10) : 230;
    const timeoutOk = timeout >= 600;

    const warnings: string[] = [];
    if (!alwaysOn) warnings.push('Always On disabled (cold start delays)');
    if (!timeoutOk) warnings.push(`Startup timeout=${timeout}s (need 600+)`);

    if (warnings.length > 0) {
      return t.warn(warnings.join(', '));
    }
    return t.ok(`Always On=true, startup timeout=${timeout}s`);
  } catch (err) {
    return t.fail(`Failed to check app config: ${formatError(err)}`);
  }
}

/**
 * Checks the GameService /health endpoint with retry logic for cold starts.
 * Retries: 2 attempts with 15s then 60s timeouts.
 */
async function checkHealthEndpoint(
  targetUrl: string,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('healthEndpoint', report);
  report({ check: 'healthEndpoint', status: 'running', detail: `Checking ${targetUrl}/health...` });

  const timeouts = [15_000, 60_000];

  for (let attempt = 0; attempt <= timeouts.length; attempt++) {
    try {
      const timeout = attempt === 0 ? 15_000 : timeouts[Math.min(attempt - 1, timeouts.length - 1)];
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), timeout);

      const response = await fetch(`${targetUrl}/health`, { signal: controller.signal });
      clearTimeout(timer);

      if (response.ok) {
        const data = await response.json();
        const status = data.status ?? 'unknown';
        const dbConnected = data.databaseDiagnostics?.connected ?? false;

        if (status === 'healthy' && dbConnected) {
          return t.ok(`status=${status}, db=connected`);
        }
        return t.warn(`status=${status}, db=${dbConnected ? 'connected' : 'disconnected'}`);
      }

      // Non-200 response — retry if attempts remain
      if (attempt < timeouts.length) {
        report({ check: 'healthEndpoint', status: 'running', detail: `HTTP ${response.status}, retrying (attempt ${attempt + 2})...` });
        continue;
      }
      return t.fail(`Health endpoint returned HTTP ${response.status}`);
    } catch (err) {
      if (attempt < timeouts.length) {
        report({ check: 'healthEndpoint', status: 'running', detail: `Unreachable, retrying (attempt ${attempt + 2})...` });
        continue;
      }
      return t.fail(`Health endpoint unreachable: ${formatError(err)}`);
    }
  }

  return t.fail('Health endpoint unreachable after all retries');
}

/**
 * Checks deployment status by comparing deployed commit (from /health)
 * to the current git commit on origin/main.
 */
async function checkDeployment(
  targetUrl: string,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('deployment', report);
  report({ check: 'deployment', status: 'running', detail: 'Checking deployment status...' });

  try {
    // Get current git commit
    let currentCommit: string;
    try {
      currentCommit = execSync('git rev-parse --short origin/main', { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] }).trim();
    } catch {
      currentCommit = 'unknown';
    }

    // Get deployed commit from health endpoint
    let deployedCommit = 'unknown';
    let buildTime = 'unknown';
    try {
      const controller = new AbortController();
      const timer = setTimeout(() => controller.abort(), 10_000);
      const response = await fetch(`${targetUrl}/health`, { signal: controller.signal });
      clearTimeout(timer);

      if (response.ok) {
        const data = await response.json();
        deployedCommit = data.version?.commit ?? 'unknown';
        buildTime = data.version?.buildTime ?? 'unknown';
      }
    } catch {
      // Health endpoint already checked separately — just report unknown
    }

    if (deployedCommit === 'unknown' || deployedCommit === 'local') {
      return t.warn(`deployed=local/unknown, current=${currentCommit}`);
    }

    // Validate commit hashes before passing to shell (prevent command injection)
    if (!COMMIT_HASH_RE.test(deployedCommit)) {
      return t.warn(`deployed commit has invalid format: ${deployedCommit}`);
    }
    if (!COMMIT_HASH_RE.test(currentCommit)) {
      return t.warn(`current commit has invalid format: ${currentCommit}`);
    }

    if (deployedCommit === currentCommit) {
      return t.ok(`commit=${deployedCommit}, built=${buildTime}`);
    }

    // Commits differ — first verify both commits exist locally
    try {
      execSync(`git cat-file -t ${deployedCommit}`, { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] });
    } catch {
      // Deployed commit not in local git history (e.g., on a feature branch)
      return t.ok(`deployed=${deployedCommit}, current=${currentCommit} (deployed commit not in local history — run 'git fetch' to compare)`);
    }

    // Both commits exist — check if any deployable files changed
    try {
      const diff = execSync(
        `git diff --name-only ${deployedCommit}..${currentCommit} -- Catan3.GameService/ Catan3.Shared/`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] }
      ).trim();

      if (diff.length === 0) {
        return t.ok(`commit=${deployedCommit} (current=${currentCommit}, no deployable changes)`);
      }
      return t.warn(`needs deploy: ${diff.split('\n').length} files changed (deployed=${deployedCommit}, current=${currentCommit})`);
    } catch {
      return t.warn(`deployed=${deployedCommit}, current=${currentCommit} (diff failed)`);
    }
  } catch (err) {
    return t.fail(`Failed to check deployment: ${formatError(err)}`);
  }
}
