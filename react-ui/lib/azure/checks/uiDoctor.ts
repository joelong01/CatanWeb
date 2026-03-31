/**
 * @module azure/checks/uiDoctor
 *
 * React UI App Service checks — the TypeScript port of
 * `.scripts/catan-azure.ps1 Get-UIDoctor`.
 *
 * Checks:
 *   1. Web App exists with managed identity
 *   2. GameService URL configured in app settings
 *   3. Site responding (HTTP 200)
 *   4. Deployment status (commit tracking)
 */

import type { WebSiteManagementClient } from '@azure/arm-appservice';
import type { AzureConfig, CheckReporter, CheckResult, DomainResult } from '../types';
import { formatError, timedCheck } from '../types';
import { execSync } from 'child_process';

/** Validates a string looks like a short or full git commit hash (hex only). */
const COMMIT_HASH_RE = /^[0-9a-f]{7,40}$/i;

/**
 * Runs all UI doctor checks in sequence.
 */
export async function runUIDoctor(
  webClient: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<DomainResult> {
  const checks: CheckResult[] = [];
  const slotSuffix = config.staging ? ' (staging)' : '';
  const appName = config.uiAppName;
  const targetUrl = config.staging ? `https://${appName}-staging.azurewebsites.net` : config.uiUrl;

  // 1. Web App + managed identity
  const appResult = await checkWebApp(webClient, config, report);
  checks.push(appResult);
  if (appResult.status === 'error') {
    return { domain: 'ui', name: `${appName}${slotSuffix}`, checks, healthy: false };
  }

  // 2. GameService URL in app settings
  const gsUrlResult = await checkGameServiceUrlSetting(webClient, config, report);
  checks.push(gsUrlResult);

  // 3. Site responding
  const siteResult = await checkSiteResponding(targetUrl, report);
  checks.push(siteResult);

  // 4. Deployment status
  const deployResult = await checkDeployment(webClient, config, targetUrl, report);
  checks.push(deployResult);

  return {
    domain: 'ui',
    name: `${appName}${slotSuffix}`,
    checks,
    healthy: checks.every((c) => c.status === 'ok' || c.status === 'warning'),
  };
}

// ── Individual checks ────────────────────────────────────────────────────────

/** Verifies the UI Web App exists and has a managed identity. */
async function checkWebApp(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('uiWebApp', report);
  report({ check: 'uiWebApp', status: 'running', detail: 'Checking UI Web App...' });

  try {
    const site = await client.webApps.get(config.resourceGroup, config.uiAppName);
    const hasIdentity = !!site.identity?.principalId;
    const state = site.state ?? 'unknown';
    const runtime = site.siteConfig?.linuxFxVersion ?? 'unknown';

    if (!hasIdentity) {
      return t.warn(`state=${state}, runtime=${runtime}, managed identity NOT enabled`);
    }
    return t.ok(`state=${state}, runtime=${runtime}, identity enabled`);
  } catch (err) {
    return t.fail(`UI Web App '${config.uiAppName}' not found: ${formatError(err)}`);
  }
}

/**
 * Verifies the GameService URL is correctly configured in app settings.
 *
 * Production uses `GAMESERVICE_URL`, staging uses `NEXT_PUBLIC_GAME_SERVICE_URL`.
 */
async function checkGameServiceUrlSetting(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('gameServiceUrl', report);
  report({
    check: 'gameServiceUrl',
    status: 'running',
    detail: 'Checking GameService URL setting...',
  });

  try {
    const settings = await client.webApps.listApplicationSettings(
      config.resourceGroup,
      config.uiAppName
    );

    // Staging uses NEXT_PUBLIC_GAME_SERVICE_URL, production uses GAMESERVICE_URL
    const settingName = config.staging ? 'NEXT_PUBLIC_GAME_SERVICE_URL' : 'GAMESERVICE_URL';
    const configuredUrl = settings.properties?.[settingName];

    if (!configuredUrl) {
      return t.fail(`${settingName} not set in app settings`);
    }

    // Verify it points to the expected GameService URL
    const expectedUrl = config.gameServiceUrl;
    if (configuredUrl === expectedUrl) {
      return t.ok(`${settingName}=${configuredUrl}`);
    }
    return t.warn(`${settingName}=${configuredUrl} (expected ${expectedUrl})`);
  } catch (err) {
    return t.fail(`Failed to check app settings: ${formatError(err)}`);
  }
}

/** Checks if the UI site responds to HTTP requests (15s timeout). */
async function checkSiteResponding(targetUrl: string, report: CheckReporter): Promise<CheckResult> {
  const t = timedCheck('siteResponding', report);
  report({ check: 'siteResponding', status: 'running', detail: `Checking ${targetUrl}...` });

  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15_000);
    const response = await fetch(targetUrl, { signal: controller.signal });
    clearTimeout(timer);

    if (response.ok) {
      return t.ok(`HTTP ${response.status}`);
    }
    return t.warn(`HTTP ${response.status}`);
  } catch (err) {
    return t.warn(`Site not responding (cold start?): ${formatError(err)}`);
  }
}

/**
 * Checks deployment status by comparing DEPLOY_COMMIT app setting
 * to the current git commit on origin/main.
 */
async function checkDeployment(
  client: WebSiteManagementClient,
  config: AzureConfig,
  _targetUrl: string,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('uiDeployment', report);
  report({ check: 'uiDeployment', status: 'running', detail: 'Checking UI deployment status...' });

  try {
    // Get current git commit
    let currentCommit: string;
    try {
      currentCommit = execSync('git rev-parse --short origin/main', {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'pipe'],
      }).trim();
    } catch {
      currentCommit = 'unknown';
    }

    // Get deployed commit from app settings
    const settings = await client.webApps.listApplicationSettings(
      config.resourceGroup,
      config.uiAppName
    );
    const deployedCommit = settings.properties?.['DEPLOY_COMMIT'] ?? 'unknown';

    if (!deployedCommit || deployedCommit === 'unknown') {
      return t.warn('No DEPLOY_COMMIT in app settings (not yet deployed?)');
    }

    // Validate commit hashes before passing to shell (prevent command injection)
    if (!COMMIT_HASH_RE.test(deployedCommit)) {
      return t.warn(`deployed commit has invalid format: ${deployedCommit}`);
    }
    if (!COMMIT_HASH_RE.test(currentCommit)) {
      return t.warn(`current commit has invalid format: ${currentCommit}`);
    }

    if (deployedCommit === currentCommit) {
      return t.ok(`commit=${deployedCommit}`);
    }

    // Commits differ — first verify both commits exist locally
    try {
      execSync(`git cat-file -t ${deployedCommit}`, {
        encoding: 'utf-8',
        stdio: ['pipe', 'pipe', 'pipe'],
      });
    } catch {
      return t.ok(
        `deployed=${deployedCommit}, current=${currentCommit} (deployed commit not in local history — run 'git fetch' to compare)`
      );
    }

    // Both commits exist — check if any deployable files changed
    try {
      const diff = execSync(
        `git diff --name-only ${deployedCommit}..${currentCommit} -- react-ui/`,
        { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] }
      ).trim();

      if (diff.length === 0) {
        return t.ok(`commit=${deployedCommit} (current=${currentCommit}, no deployable changes)`);
      }
      return t.warn(
        `needs deploy: ${diff.split('\n').length} files changed (deployed=${deployedCommit}, current=${currentCommit})`
      );
    } catch {
      return t.warn(`deployed=${deployedCommit}, current=${currentCommit} (diff failed)`);
    }
  } catch (err) {
    return t.fail(`Failed to check UI deployment: ${formatError(err)}`);
  }
}
