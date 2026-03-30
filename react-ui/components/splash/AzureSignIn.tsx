/**
 * @module splash/AzureSignIn
 *
 * Azure sign-in button for the splash overlay (Phase 2).
 *
 * Lazy-loads @azure/msal-browser only when rendered — the ~40KB MSAL library
 * is never included in the bundle for healthy page loads where this component
 * is not mounted.
 *
 * On sign-in, acquires a token for Azure Resource Manager + Graph APIs
 * and passes it to the parent via onTokenAcquired callback.
 */

'use client';

import React, { useState, useCallback } from 'react';
import { msalConfig, azureManagementScope } from '@/lib/azure/msalConfig';

interface AzureSignInProps {
  /** Called when an Azure access token is successfully acquired. */
  onTokenAcquired: (token: string) => void;
}

export default function AzureSignIn({ onTokenAcquired }: AzureSignInProps) {
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSignIn = useCallback(async () => {
    setIsSigningIn(true);
    setError(null);

    try {
      // Lazy-load MSAL — only fetched when user clicks sign-in
      const { PublicClientApplication } = await import('@azure/msal-browser');

      const msalInstance = new PublicClientApplication(msalConfig);
      await msalInstance.initialize();

      // Try silent token acquisition first (cached session)
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0) {
        try {
          const silentResult = await msalInstance.acquireTokenSilent({
            scopes: [azureManagementScope],
            account: accounts[0],
          });
          if (silentResult.accessToken) {
            onTokenAcquired(silentResult.accessToken);
            return;
          }
        } catch {
          // Silent failed — fall through to interactive
        }
      }

      // Interactive login (popup)
      const result = await msalInstance.acquireTokenPopup({
        scopes: [azureManagementScope],
      });

      if (result.accessToken) {
        onTokenAcquired(result.accessToken);
      } else {
        setError('No access token received');
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      // Don't show "user_cancelled" as an error — they just closed the popup
      if (message.includes('user_cancelled') || message.includes('User cancelled')) {
        setError(null);
      } else {
        setError(`Sign-in failed: ${message}`);
      }
    } finally {
      setIsSigningIn(false);
    }
  }, [onTokenAcquired]);

  return (
    <div className="flex flex-col items-center gap-3">
      <p className="text-gray-400 text-sm text-center">
        Sign in to Azure to diagnose and repair infrastructure.
      </p>

      <button
        onClick={handleSignIn}
        disabled={isSigningIn}
        className="px-6 py-2.5 bg-blue-600 hover:bg-blue-500 disabled:bg-blue-800 disabled:cursor-wait text-white rounded-md font-medium transition-colors flex items-center gap-2"
      >
        {isSigningIn ? (
          <>
            <span className="inline-block animate-spin">{'\u25E0'}</span>
            Signing in...
          </>
        ) : (
          'Sign in with Microsoft'
        )}
      </button>

      {error && <p className="text-red-400 text-xs text-center max-w-sm">{error}</p>}
    </div>
  );
}
