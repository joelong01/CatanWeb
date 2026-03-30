/**
 * @module azure/tokenCredential
 *
 * Wraps a forwarded MSAL access token in the Azure SDK's `TokenCredential`
 * interface so it can be used with ARM SDK clients server-side.
 *
 * The token arrives via the Authorization header from the browser (MSAL login).
 * We parse the JWT `exp` claim for the real expiration instead of hardcoding.
 *
 * Security: The token is held in memory only for the request duration.
 * The Authorization header MUST NOT be logged.
 */

import type { TokenCredential, AccessToken } from '@azure/core-auth';

/**
 * Creates a `TokenCredential` from a raw JWT access token string.
 *
 * The Azure ARM SDKs accept any object implementing `TokenCredential`.
 * This implementation returns the same token for every `getToken()` call
 * since the token was already obtained by the browser via MSAL.
 *
 * @param token  Raw JWT access token (from Authorization: Bearer header)
 * @returns      A `TokenCredential` usable with Azure SDK clients
 */
export function createTokenCredential(token: string): TokenCredential {
  // Parse the JWT payload to get the actual expiration timestamp.
  // JWT structure: header.payload.signature (all base64url-encoded).
  let expiresOnTimestamp: number;
  try {
    const payloadBase64 = token.split('.')[1];
    const payload = JSON.parse(Buffer.from(payloadBase64, 'base64url').toString());
    expiresOnTimestamp = (payload.exp as number) * 1000; // JWT exp is in seconds
  } catch {
    // If parsing fails, assume 1 hour from now (safe fallback)
    expiresOnTimestamp = Date.now() + 3600_000;
  }

  return {
    getToken: async (): Promise<AccessToken> => ({
      token,
      expiresOnTimestamp,
    }),
  };
}
