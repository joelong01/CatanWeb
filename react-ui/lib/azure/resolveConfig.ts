/**
 * @module azure/resolveConfig
 *
 * Zero-config Azure resource discovery. Given just a `baseName` (e.g., "catan"),
 * derives all Azure resource names using deterministic conventions, then discovers
 * the subscription ID at runtime from the authenticated credential.
 *
 * Naming conventions (must match `.scripts/catan-azure.ps1 Initialize-ConfigFromBaseName`):
 *
 *   | Resource           | Pattern              | Example            |
 *   |--------------------|----------------------|--------------------|
 *   | Resource Group     | rg-{baseName}        | rg-catan           |
 *   | Cosmos Account     | cosmos-{baseName}    | cosmos-catan       |
 *   | GameService App    | {baseName}-api       | catan-api          |
 *   | UI App             | {baseName}           | catan              |
 *   | App Service Plan   | asp-{baseName}       | asp-catan          |
 *   | Storage Account    | st{baseName}         | stcatan            |
 *   | Cosmos Database    | catan (hardcoded)    | catan              |
 *
 * The only value that can't be derived or discovered is the MSAL `clientId`
 * (needed BEFORE authentication). Everything else is either a naming convention
 * or discoverable from Azure after auth.
 */

import type { TokenCredential } from '@azure/core-auth';
import type { AzureConfig } from './types';

/**
 * Extracts the baseName from a GameService URL.
 *
 * The React app already knows its GameService URL (from service config).
 * The baseName is the prefix before `-api.azurewebsites.net`.
 *
 * @example
 *   extractBaseName("https://catan-api.azurewebsites.net")          → "catan"
 *   extractBaseName("https://catan-api-staging.azurewebsites.net")  → "catan"
 *   extractBaseName("http://localhost:8080")                         → null (local dev)
 */
export function extractBaseName(serviceUrl: string): string | null {
  const match = serviceUrl.match(/https?:\/\/(.+)-api(?:-staging)?\.azurewebsites\.net/);
  return match?.[1] ?? null;
}

/**
 * Derives all Azure resource names from baseName using naming conventions.
 * Pure string derivation — no Azure API calls.
 *
 * `subscriptionId` is left empty — call {@link discoverSubscriptionId} after auth.
 */
export function resolveAzureConfig(baseName: string, staging: boolean = false): AzureConfig {
  return {
    baseName,
    subscriptionId: '', // Discovered after auth via discoverSubscriptionId()
    resourceGroup: `rg-${baseName}`,
    cosmosAccountName: `cosmos-${baseName}`,
    cosmosDatabaseName: 'catan',
    gameServiceAppName: `${baseName}-api`,
    uiAppName: baseName,
    gameServiceUrl: staging
      ? `https://${baseName}-api-staging.azurewebsites.net`
      : `https://${baseName}-api.azurewebsites.net`,
    uiUrl: staging
      ? `https://${baseName}-staging.azurewebsites.net`
      : `https://${baseName}.azurewebsites.net`,
    staging,
  };
}

/**
 * Discovers the Azure subscription ID from the authenticated credential.
 *
 * For CLI (DefaultAzureCredential): uses the default subscription from
 * `az login` / `az account set`. The user picks their subscription
 * before running the doctor — we just read what they chose.
 *
 * For web (MSAL token): parses the subscription from the JWT `tid` claim
 * and finds the subscription containing the resource group.
 *
 * @param credential     Azure credential
 * @param resourceGroup  Expected resource group name (e.g., "rg-catan")
 * @returns              The subscription ID, or throws if not found
 */
/**
 * Gets the subscription ID from the user's Azure CLI default subscription.
 *
 * The user controls which subscription they're working with via:
 *   `az login` → `az account set --subscription <name-or-id>`
 *
 * This respects that choice instead of trying to scan all subscriptions.
 * For the web path (MSAL token), pass the subscription ID explicitly.
 *
 * @returns The default subscription ID from `az account show`
 */
export async function getDefaultSubscriptionId(): Promise<string> {
  const { execSync } = await import('child_process');
  try {
    const subId = execSync('az account show --query id -o tsv', {
      encoding: 'utf-8',
      stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();

    if (!subId) {
      throw new Error('Empty subscription ID');
    }
    return subId;
  } catch {
    throw new Error(
      'Could not get Azure subscription. Make sure you are logged in:\n' +
        '  az login\n' +
        '  az account set --subscription <name-or-id>'
    );
  }
}

/**
 * Discovers the subscription ID from a credential token by scanning
 * subscriptions for the expected resource group. Used by the web path
 * where `az account show` isn't available.
 *
 * @param credential     Azure credential (forwarded MSAL token)
 * @param resourceGroup  Expected resource group name (e.g., "rg-catan")
 * @returns              The subscription ID, or throws if not found
 */
export async function discoverSubscriptionId(
  credential: TokenCredential,
  resourceGroup: string
): Promise<string> {
  const tokenResponse = await credential.getToken('https://management.azure.com/.default');
  if (!tokenResponse?.token) {
    throw new Error('Could not get Azure management token');
  }

  const response = await fetch(
    'https://management.azure.com/subscriptions?api-version=2022-12-01',
    {
      headers: { Authorization: `Bearer ${tokenResponse.token}` },
    }
  );

  if (!response.ok) {
    throw new Error(`Failed to list subscriptions: HTTP ${response.status}`);
  }

  const data = (await response.json()) as {
    value: Array<{ subscriptionId: string; state: string }>;
  };
  const enabled = data.value.filter((s) => s.state === 'Enabled');

  // Find the subscription containing our resource group + Cosmos account.
  // Check for the Cosmos account (not just the RG) to avoid false positives
  // from empty RGs with the same name in other subscriptions.
  const cosmosAccountName = `cosmos-${resourceGroup.replace('rg-', '')}`;
  const { CosmosDBManagementClient } = await import('@azure/arm-cosmosdb');

  // Parallelize across all subscriptions (much faster for enterprise tenants)
  const results = await Promise.allSettled(
    enabled.map(async (sub) => {
      const cosmosClient = new CosmosDBManagementClient(credential, sub.subscriptionId);
      await cosmosClient.databaseAccounts.get(resourceGroup, cosmosAccountName);
      return sub.subscriptionId;
    })
  );

  const found = results.find((r) => r.status === 'fulfilled');
  if (found?.status === 'fulfilled') {
    return found.value;
  }

  throw new Error(
    `Could not find '${cosmosAccountName}' in '${resourceGroup}' across ${enabled.length} subscription(s). ` +
      `Make sure you sign in with the correct Azure account.`
  );
}

/**
 * Reads the baseName from the config file (.azure/catan-azure.json).
 * Returns null if the file doesn't exist or has no baseName.
 *
 * This is the ONLY value read from the config file — everything else
 * is derived from baseName or discovered from Azure.
 */
export function readBaseNameFromConfig(
  configPath: string
): { baseName: string; clientId?: string } | null {
  try {
    // Dynamic import needed for server-side file access in Next.js API routes
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const { readFileSync } = require('fs') as typeof import('fs');
    const cfg = JSON.parse(readFileSync(configPath, 'utf-8'));
    const baseName = cfg.baseName;
    if (!baseName) return null;
    return {
      baseName,
      clientId: cfg.auth?.clientId,
    };
  } catch {
    return null;
  }
}
