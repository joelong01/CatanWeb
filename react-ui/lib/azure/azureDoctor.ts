/**
 * @module azure/azureDoctor
 *
 * Azure Doctor — orchestrates all infrastructure health checks for Catan.
 *
 * This is the single source of truth for Azure health checks, replacing the
 * PowerShell implementations in `catan-azure.ps1` and `database.ps1`.
 * It is consumed by three callers:
 *
 *   1. **CLI** — `azureDoctor.cli.ts` (local dev, uses DefaultAzureCredential)
 *   2. **Web** — Next.js API route (browser auth, forwarded MSAL token)
 *   3. **PowerShell** — `catan.ps1 azure doctor` delegates to the CLI
 *
 * Runs four doctor domains in sequence:
 *   - **game-service** — App Service health, deployment, Always On, SKU
 *   - **database** — Cosmos account, firewall, containers, RBAC
 *   - **ui** — UI App Service health, GameService URL config, deployment
 *   - **github** — Azure AD app registration, federated credentials, RBAC
 */

import { CosmosDBManagementClient } from '@azure/arm-cosmosdb';
import { WebSiteManagementClient } from '@azure/arm-appservice';
import { ResourceManagementClient } from '@azure/arm-resources';
import { AuthorizationManagementClient } from '@azure/arm-authorization';
import type { TokenCredential } from '@azure/core-auth';
import type { AzureConfig, CheckReporter, DoctorResult } from './types';
import { runCosmosDoctor } from './checks/cosmosDoctor';
import { runGameServiceDoctor } from './checks/gameServiceDoctor';
import { runUIDoctor } from './checks/uiDoctor';
import { runGitHubDoctor } from './checks/githubDoctor';

/**
 * Runs all Azure doctor checks across all four domains.
 *
 * @param credential  Azure credential (DefaultAzureCredential for CLI,
 *                    forwarded MSAL token for web)
 * @param config      Azure resource identifiers
 * @param report      Callback invoked as each check progresses
 * @param autoFix     When true, automatically repairs fixable issues
 *                    (Cosmos firewall, containers, RBAC). Default: true.
 * @returns           Aggregate result across all domains
 */
export async function runAzureDoctor(
  credential: TokenCredential,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean = true
): Promise<DoctorResult> {
  // Initialize ARM SDK clients (shared across domains)
  const cosmosClient = new CosmosDBManagementClient(credential, config.subscriptionId);
  const webClient = new WebSiteManagementClient(credential, config.subscriptionId);
  const resourceClient = new ResourceManagementClient(credential, config.subscriptionId);
  const authClient = new AuthorizationManagementClient(credential, config.subscriptionId);

  // Run each domain in sequence (dependencies between domains mean
  // parallel execution isn't safe — e.g., Cosmos must be healthy
  // before GameService health check can succeed)

  report({ check: '── game-service ──', status: 'running', detail: '' });
  const gsResult = await runGameServiceDoctor(webClient, resourceClient, config, report);

  report({ check: '── database ──', status: 'running', detail: '' });
  const dbResult = await runCosmosDoctor(cosmosClient, webClient, config, report, autoFix);

  report({ check: '── ui ──', status: 'running', detail: '' });
  const uiResult = await runUIDoctor(webClient, config, report);

  report({ check: '── github ──', status: 'running', detail: '' });
  const ghResult = await runGitHubDoctor(credential, authClient, config, report);

  const domains = [gsResult, dbResult, uiResult, ghResult];

  return {
    domains,
    allPassed: domains.every((d) => d.healthy),
  };
}
