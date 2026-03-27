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
import type { AzureConfig, CheckReporter, DoctorResult, DomainResult } from './types';
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
 * @param skipDomains Domains to skip (e.g., ['github'] for web path where
 *                    Graph API token isn't available). Default: none.
 * @returns           Aggregate result across all domains
 */
export async function runAzureDoctor(
  credential: TokenCredential,
  config: AzureConfig,
  report: CheckReporter,
  autoFix: boolean = true,
  skipDomains: string[] = []
): Promise<DoctorResult> {
  // Initialize ARM SDK clients (shared across domains)
  const cosmosClient = new CosmosDBManagementClient(credential, config.subscriptionId);
  const webClient = new WebSiteManagementClient(credential, config.subscriptionId);
  const resourceClient = new ResourceManagementClient(credential, config.subscriptionId);
  const authClient = new AuthorizationManagementClient(credential, config.subscriptionId);

  const domains: DomainResult[] = [];

  // Run each domain in sequence (dependencies between domains mean
  // parallel execution isn't safe — e.g., Cosmos must be healthy
  // before GameService health check can succeed)

  if (!skipDomains.includes('game-service')) {
    report({ check: '── game-service ──', status: 'running', detail: '' });
    domains.push(await runGameServiceDoctor(webClient, resourceClient, config, report));
  }

  if (!skipDomains.includes('database')) {
    report({ check: '── database ──', status: 'running', detail: '' });
    domains.push(await runCosmosDoctor(cosmosClient, webClient, config, report, autoFix));
  }

  if (!skipDomains.includes('ui')) {
    report({ check: '── ui ──', status: 'running', detail: '' });
    domains.push(await runUIDoctor(webClient, config, report));
  }

  if (!skipDomains.includes('github')) {
    report({ check: '── github ──', status: 'running', detail: '' });
    domains.push(await runGitHubDoctor(credential, authClient, config, report));
  }

  return {
    domains,
    allPassed: domains.every((d) => d.healthy),
  };
}
