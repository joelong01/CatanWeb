/**
 * @module azure/msalConfig
 *
 * MSAL (Microsoft Authentication Library) configuration for the splash screen.
 * Client-side only — used by the AzureSignIn component to acquire tokens
 * for Azure Resource Manager API access.
 *
 * Configuration comes from `.azure/catan-azure.json` via `next.config.ts`:
 *   - `NEXT_PUBLIC_AZURE_CLIENT_ID` — app registration client ID (for MSAL)
 *   - `NEXT_PUBLIC_AZURE_BASE_NAME` — used to check if Azure is configured
 *
 * When these are not set (no config file), Azure sign-in is disabled.
 */

/**
 * MSAL configuration for PublicClientApplication.
 *
 * Uses `common` as the authority (works for any Azure AD tenant).
 * The user's tenant is determined at sign-in time.
 */
export const msalConfig = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_AZURE_CLIENT_ID ?? '',
    authority: 'https://login.microsoftonline.com/common',
    redirectUri: typeof window !== 'undefined' ? window.location.origin : '',
  },
  cache: {
    cacheLocation: 'sessionStorage' as const,
  },
};

/** OAuth2 scope for Azure Resource Manager API access. */
export const azureManagementScope = 'https://management.azure.com/.default';

/**
 * Returns true if Azure AD credentials are configured.
 * When false, the splash screen skips Phase 2/3 (no Azure sign-in button).
 *
 * Requires both baseName (to know which resources to check) and
 * clientId (to initiate MSAL sign-in).
 */
export function isAzureConfigured(): boolean {
  return !!process.env.NEXT_PUBLIC_AZURE_BASE_NAME && !!process.env.NEXT_PUBLIC_AZURE_CLIENT_ID;
}
