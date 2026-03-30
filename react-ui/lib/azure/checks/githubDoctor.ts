/**
 * @module azure/checks/githubDoctor
 *
 * GitHub OIDC / Azure AD checks — the TypeScript port of
 * `.scripts/catan-azure.ps1 Get-GitHubDoctor`.
 *
 * Verifies the Azure AD app registration and federated credentials
 * needed for GitHub Actions deployments.
 *
 * Checks:
 *   1. App registration exists (display name: github-actions-{baseName}-deploy)
 *   2. Service principal created
 *   3. Federated credentials for main and staging branches
 *   4. Contributor role assigned on the resource group
 *
 * Uses Microsoft Graph REST API (not an SDK) to check Azure AD resources.
 * Uses `@azure/arm-authorization` for RBAC role assignment checks.
 */

import type { AuthorizationManagementClient } from '@azure/arm-authorization';
import type { TokenCredential } from '@azure/core-auth';
import type { AzureConfig, CheckReporter, CheckResult, DomainResult } from '../types';
import { formatError, timedCheck } from '../types';

/** Graph API base URL for Azure AD queries. */
const GRAPH_URL = 'https://graph.microsoft.com/v1.0';

/**
 * Runs all GitHub OIDC doctor checks in sequence.
 */
export async function runGitHubDoctor(
  credential: TokenCredential,
  authClient: AuthorizationManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<DomainResult> {
  const checks: CheckResult[] = [];
  const appDisplayName = `github-actions-${config.baseName}-deploy`;

  // Get a Graph API token (different scope from ARM)
  let graphToken: string;
  try {
    const tokenResponse = await credential.getToken('https://graph.microsoft.com/.default');
    if (!tokenResponse?.token) {
      return {
        domain: 'github',
        name: appDisplayName,
        checks: [{ check: 'graphAuth', status: 'error', error: 'Could not get Graph API token' }],
        healthy: false,
      };
    }
    graphToken = tokenResponse.token;
  } catch (err) {
    return {
      domain: 'github',
      name: appDisplayName,
      checks: [{ check: 'graphAuth', status: 'error', error: `Graph API auth failed: ${formatError(err)}` }],
      healthy: false,
    };
  }

  // 1. App registration
  const { result: appResult, appObjectId, appId } = await checkAppRegistration(graphToken, appDisplayName, report);
  checks.push(appResult);
  if (appResult.status === 'error') {
    return { domain: 'github', name: appDisplayName, checks, healthy: false };
  }

  // 2. Service principal
  const { result: spResult, spObjectId } = await checkServicePrincipal(graphToken, appId!, report);
  checks.push(spResult);

  // 3. Federated credentials
  const fedResult = await checkFederatedCredentials(graphToken, appObjectId!, report);
  checks.push(fedResult);

  // 4. Contributor role
  if (spObjectId) {
    const roleResult = await checkContributorRole(authClient, config, spObjectId, report);
    checks.push(roleResult);
  }

  return {
    domain: 'github',
    name: appDisplayName,
    checks,
    healthy: checks.every((c) => c.status === 'ok' || c.status === 'warning'),
  };
}

// ── Individual checks ────────────────────────────────────────────────────────

/**
 * Checks if the Azure AD app registration exists.
 * Returns the app's object ID and client ID for downstream checks.
 */
async function checkAppRegistration(
  graphToken: string,
  displayName: string,
  report: CheckReporter
): Promise<{ result: CheckResult; appObjectId?: string; appId?: string }> {
  const t = timedCheck('appRegistration', report);
  report({ check: 'appRegistration', status: 'running', detail: `Looking for '${displayName}'...` });

  try {
    const response = await graphFetch(
      graphToken,
      `/applications?$filter=displayName eq '${displayName}'&$select=id,appId,displayName`
    );

    const apps = response.value as Array<Record<string, string>> | undefined;
    if (!apps || apps.length === 0) {
      return { result: t.fail(`App registration '${displayName}' not found`) };
    }

    const app = apps[0];
    return {
      result: t.ok(`appId=${app.appId}`),
      appObjectId: app.id,
      appId: app.appId,
    };
  } catch (err) {
    return { result: t.fail(`Failed to check app registration: ${formatError(err)}`) };
  }
}

/**
 * Checks if a service principal exists for the app registration.
 * Returns the SP's object ID for RBAC checks.
 */
async function checkServicePrincipal(
  graphToken: string,
  appId: string,
  report: CheckReporter
): Promise<{ result: CheckResult; spObjectId?: string }> {
  const t = timedCheck('servicePrincipal', report);
  report({ check: 'servicePrincipal', status: 'running', detail: 'Checking service principal...' });

  try {
    const response = await graphFetch(
      graphToken,
      `/servicePrincipals?$filter=appId eq '${appId}'&$select=id,appId`
    );

    const sps = response.value as Array<Record<string, string>> | undefined;
    if (!sps || sps.length === 0) {
      return { result: t.fail('Service principal not found') };
    }

    return {
      result: t.ok(`objectId=${sps[0].id.substring(0, 8)}...`),
      spObjectId: sps[0].id,
    };
  } catch (err) {
    return { result: t.fail(`Failed to check service principal: ${formatError(err)}`) };
  }
}

/**
 * Checks for federated credentials on the app registration.
 * Expects two: 'github-actions-main' and 'github-actions-staging'.
 */
async function checkFederatedCredentials(
  graphToken: string,
  appObjectId: string,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('federatedCredentials', report);
  report({ check: 'federatedCredentials', status: 'running', detail: 'Checking federated credentials...' });

  try {
    const response = await graphFetch(
      graphToken,
      `/applications/${appObjectId}/federatedIdentityCredentials`
    );

    const creds = (response.value as Array<{ name: string }>) ?? [];
    const names = new Set(creds.map((c) => c.name));
    const hasMain = names.has('github-actions-main');
    const hasStaging = names.has('github-actions-staging');

    const found: string[] = [];
    const missing: string[] = [];
    if (hasMain) found.push('main'); else missing.push('main');
    if (hasStaging) found.push('staging'); else missing.push('staging');

    if (missing.length === 0) {
      return t.ok(`Federated: ${found.join(', ')}`);
    }
    return t.fail(`Missing federated credentials: ${missing.join(', ')}`, `Found: ${found.join(', ') || 'none'}`);
  } catch (err) {
    return t.fail(`Failed to check federated credentials: ${formatError(err)}`);
  }
}

/**
 * Checks if the service principal has Contributor role on the resource group.
 * Uses ARM RBAC (this IS an Azure RBAC role, unlike Cosmos SQL roles).
 */
async function checkContributorRole(
  authClient: AuthorizationManagementClient,
  config: AzureConfig,
  spObjectId: string,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('contributorRole', report);
  report({ check: 'contributorRole', status: 'running', detail: 'Checking Contributor role...' });

  try {
    const scope = `/subscriptions/${config.subscriptionId}/resourceGroups/${config.resourceGroup}`;
    const assignments = authClient.roleAssignments.listForScope(scope, {
      filter: `assignedTo('${spObjectId}')`,
    });

    // Contributor role definition ID ends with this GUID
    const contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c';

    let hasContributor = false;
    for await (const a of assignments) {
      if (a.roleDefinitionId?.includes(contributorRoleId)) {
        hasContributor = true;
        break;
      }
    }

    if (hasContributor) {
      return t.ok(`Contributor role on ${config.resourceGroup}`);
    }
    return t.fail(`Service principal lacks Contributor role on ${config.resourceGroup}`);
  } catch (err) {
    return t.fail(`Failed to check Contributor role: ${formatError(err)}`);
  }
}

// ── Graph API helper ─────────────────────────────────────────────────────────

/**
 * Makes an authenticated GET request to the Microsoft Graph API.
 * Uses plain fetch instead of the Graph SDK to avoid another dependency.
 */
async function graphFetch(token: string, path: string): Promise<Record<string, unknown>> {
  const response = await fetch(`${GRAPH_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Graph API ${response.status}: ${body.substring(0, 200)}`);
  }

  return response.json() as Promise<Record<string, unknown>>;
}
