/**
 * @module azure/checks/cosmosDoctor
 *
 * Cosmos DB infrastructure checks — the TypeScript port of
 * `.scripts/database.ps1 Get-AzureDoctorResult`.
 *
 * Checks:
 *   1. Cosmos account exists and is provisioned
 *   2. Firewall allows public access (no IP restrictions)
 *   3. Database and all required containers exist
 *   4. App Service has COSMOS_ENDPOINT and managed identity
 *   5. Managed identity has Cosmos SQL Data Contributor role
 */

import type { CosmosDBManagementClient } from '@azure/arm-cosmosdb';
import type { WebSiteManagementClient } from '@azure/arm-appservice';
import type { AzureConfig, CheckReporter, CheckResult, DomainResult } from '../types';
import {
  REQUIRED_CONTAINERS,
  COSMOS_DATA_CONTRIBUTOR_ROLE_ID,
  formatError,
  timedCheck,
} from '../types';

/**
 * Runs all Cosmos DB doctor checks in sequence.
 *
 * Checks run in dependency order — if the account doesn't exist,
 * downstream checks are skipped.
 */
export async function runCosmosDoctor(
  cosmosClient: CosmosDBManagementClient,
  webClient: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean
): Promise<DomainResult> {
  const checks: CheckResult[] = [];

  // 1. Cosmos account existence
  const accountResult = await checkCosmosAccount(cosmosClient, config, report);
  checks.push(accountResult);
  if (accountResult.status === 'error') {
    return { domain: 'database', name: config.cosmosAccountName, checks, healthy: false };
  }

  // 2. Firewall (public access + IP rules)
  const firewallResult = await checkCosmosFirewall(cosmosClient, config, report, autoFix);
  checks.push(firewallResult);

  // 3. Database + containers
  const containerResult = await checkCosmosContainers(cosmosClient, config, report, autoFix);
  checks.push(containerResult);

  // 4. App Service config
  const appResult = await checkAppServiceConfig(webClient, config, report);
  checks.push(appResult);

  // 5. Cosmos SQL RBAC
  const rbacResult = await checkCosmosRbac(cosmosClient, webClient, config, report, autoFix);
  checks.push(rbacResult);

  return {
    domain: 'database',
    name: config.cosmosAccountName,
    checks,
    healthy: checks.every((c) => c.status === 'ok' || c.status === 'warning'),
  };
}

// ── Individual checks ────────────────────────────────────────────────────────

/**
 * Verifies the CosmosDB account exists and has finished provisioning.
 * No auto-fix — account creation is a separate install operation.
 */
async function checkCosmosAccount(
  client: CosmosDBManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('cosmosAccount', report);
  report({ check: 'cosmosAccount', status: 'running', detail: 'Checking Cosmos account...' });

  try {
    const account = await client.databaseAccounts.get(
      config.resourceGroup,
      config.cosmosAccountName
    );
    if (account.provisioningState !== 'Succeeded') {
      return t.fail(
        `Cosmos account '${config.cosmosAccountName}' is not ready (state: ${account.provisioningState}).`,
        `provisioningState=${account.provisioningState}`
      );
    }
    return t.ok('provisioningState=Succeeded');
  } catch (err) {
    return t.fail(
      `Cosmos account '${config.cosmosAccountName}' not found in '${config.resourceGroup}': ${formatError(err)}`
    );
  }
}

/**
 * Verifies Cosmos firewall allows public access with no IP restrictions.
 *
 * This is the #1 failure mode: MCAPS policy disables publicNetworkAccess overnight.
 * Auto-fix: PATCHes the account to re-enable (can take 30-60s).
 */
async function checkCosmosFirewall(
  client: CosmosDBManagementClient,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean
): Promise<CheckResult> {
  const t = timedCheck('cosmosFirewall', report);
  report({ check: 'cosmosFirewall', status: 'running', detail: 'Checking firewall config...' });

  try {
    const account = await client.databaseAccounts.get(
      config.resourceGroup,
      config.cosmosAccountName
    );
    const publicAccess = account.publicNetworkAccess === 'Enabled';
    const ipRulesEmpty = !account.ipRules || account.ipRules.length === 0;

    if (publicAccess && ipRulesEmpty) {
      return t.ok('publicNetworkAccess=Enabled, ipRules=[]');
    }

    const issues: string[] = [];
    if (!publicAccess) issues.push('publicNetworkAccess=Disabled');
    if (!ipRulesEmpty) issues.push(`ipRules=[${account.ipRules!.length} entries]`);

    if (!autoFix) {
      return t.fail(`Cosmos firewall is blocking access: ${issues.join(', ')}`, issues.join(', '));
    }

    report({
      check: 'cosmosFirewall',
      status: 'fixing',
      detail: 'Enabling public access (this may take a minute)...',
    });
    await client.databaseAccounts.beginCreateOrUpdateAndWait(
      config.resourceGroup,
      config.cosmosAccountName,
      {
        location: account.location!,
        locations: account.locations ?? [],
        databaseAccountOfferType: account.databaseAccountOfferType ?? 'Standard',
        publicNetworkAccess: 'Enabled',
        ipRules: [],
      }
    );
    return t.ok('Fixed: publicNetworkAccess=Enabled, ipRules=[]');
  } catch (err) {
    return t.fail(`Failed to check/fix Cosmos firewall: ${formatError(err)}`);
  }
}

/**
 * Verifies the Cosmos database exists and all required containers are present.
 * Auto-fix: creates missing containers with `/id` partition key.
 */
async function checkCosmosContainers(
  client: CosmosDBManagementClient,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean
): Promise<CheckResult> {
  const t = timedCheck('cosmosContainers', report);
  report({
    check: 'cosmosContainers',
    status: 'running',
    detail: 'Checking database and containers...',
  });

  try {
    // Verify database exists
    try {
      await client.sqlResources.getSqlDatabase(
        config.resourceGroup,
        config.cosmosAccountName,
        config.cosmosDatabaseName
      );
    } catch {
      return t.fail(
        `Database '${config.cosmosDatabaseName}' not found. Run: ./catan.ps1 database install -Azure`
      );
    }

    // Enumerate existing containers
    const existing = new Set<string>();
    for await (const c of client.sqlResources.listSqlContainers(
      config.resourceGroup,
      config.cosmosAccountName,
      config.cosmosDatabaseName
    )) {
      if (c.resource?.id) existing.add(c.resource.id);
    }

    const missing = Object.keys(REQUIRED_CONTAINERS).filter((name) => !existing.has(name));
    if (missing.length === 0) {
      return t.ok(`All ${Object.keys(REQUIRED_CONTAINERS).length} containers present`);
    }

    if (!autoFix) {
      return t.fail(`Missing containers: ${missing.join(', ')}`, `Missing: ${missing.join(', ')}`);
    }

    report({
      check: 'cosmosContainers',
      status: 'fixing',
      detail: `Creating: ${missing.join(', ')}...`,
    });
    for (const name of missing) {
      await client.sqlResources.beginCreateUpdateSqlContainerAndWait(
        config.resourceGroup,
        config.cosmosAccountName,
        config.cosmosDatabaseName,
        name,
        {
          resource: {
            id: name,
            partitionKey: { paths: [REQUIRED_CONTAINERS[name]], kind: 'Hash' },
          },
        }
      );
    }
    return t.ok(`Fixed: created ${missing.join(', ')}`);
  } catch (err) {
    return t.fail(`Failed to check/fix containers: ${formatError(err)}`);
  }
}

/**
 * Verifies App Service has COSMOS_ENDPOINT set and managed identity enabled.
 * Read-only — no auto-fix (requires deployment-level changes).
 */
async function checkAppServiceConfig(
  client: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter
): Promise<CheckResult> {
  const t = timedCheck('appServiceConfig', report);
  report({
    check: 'appServiceConfig',
    status: 'running',
    detail: 'Checking App Service config...',
  });

  try {
    const settings = await client.webApps.listApplicationSettings(
      config.resourceGroup,
      config.gameServiceAppName
    );
    const hasEndpoint = !!settings.properties?.['COSMOS_ENDPOINT'];

    const site = await client.webApps.get(config.resourceGroup, config.gameServiceAppName);
    const hasIdentity = site.identity?.type === 'SystemAssigned';

    const issues: string[] = [];
    if (!hasEndpoint) issues.push('COSMOS_ENDPOINT not set');
    if (!hasIdentity) issues.push('Managed identity not enabled');

    if (issues.length === 0) {
      return t.ok('COSMOS_ENDPOINT set, managed identity enabled');
    }
    return t.fail(
      `App Service config issues: ${issues.join(', ')}. Run: ./catan.ps1 azure deploy`,
      issues.join(', ')
    );
  } catch (err) {
    return t.fail(`Failed to check App Service config: ${formatError(err)}`);
  }
}

/**
 * Verifies the GameService's managed identity has the Cosmos SQL Data
 * Contributor role. Uses CosmosDBManagementClient (NOT AuthorizationManagementClient).
 * Auto-fix: creates the SQL role assignment.
 */
async function checkCosmosRbac(
  cosmosClient: CosmosDBManagementClient,
  webClient: WebSiteManagementClient,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean
): Promise<CheckResult> {
  const t = timedCheck('cosmosRbac', report);
  report({ check: 'cosmosRbac', status: 'running', detail: 'Checking Cosmos RBAC roles...' });

  try {
    const site = await webClient.webApps.get(config.resourceGroup, config.gameServiceAppName);
    const principalId = site.identity?.principalId;
    if (!principalId) {
      return t.fail(
        'Cannot check RBAC: App Service has no managed identity. Run: ./catan.ps1 azure deploy'
      );
    }

    let hasRole = false;
    for await (const a of cosmosClient.sqlResources.listSqlRoleAssignments(
      config.resourceGroup,
      config.cosmosAccountName
    )) {
      if (
        a.principalId === principalId &&
        a.roleDefinitionId?.includes(COSMOS_DATA_CONTRIBUTOR_ROLE_ID)
      ) {
        hasRole = true;
        break;
      }
    }

    if (hasRole) {
      return t.ok(`Data Contributor role assigned to ${principalId.substring(0, 8)}...`);
    }

    if (!autoFix) {
      return t.fail(`Managed identity ${principalId} lacks Cosmos Data Contributor role`);
    }

    report({
      check: 'cosmosRbac',
      status: 'fixing',
      detail: `Granting role to ${principalId.substring(0, 8)}...`,
    });
    const accountScope = `/subscriptions/${config.subscriptionId}/resourceGroups/${config.resourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/${config.cosmosAccountName}`;
    await cosmosClient.sqlResources.beginCreateUpdateSqlRoleAssignmentAndWait(
      crypto.randomUUID(),
      config.resourceGroup,
      config.cosmosAccountName,
      {
        roleDefinitionId: `${accountScope}/sqlRoleDefinitions/${COSMOS_DATA_CONTRIBUTOR_ROLE_ID}`,
        principalId,
        scope: accountScope,
      }
    );
    return t.ok(`Fixed: granted Data Contributor role to ${principalId.substring(0, 8)}...`);
  } catch (err) {
    return t.fail(`Failed to check/fix RBAC: ${formatError(err)}`);
  }
}
