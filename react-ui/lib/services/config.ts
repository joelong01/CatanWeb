/**
 * Service configuration for GameService connection.
 *
 * Supports both local development and deployed environments.
 */

/** Default local development URL */
const LOCAL_SERVICE_URL = 'http://localhost:8080';

/**
 * Get the base URL for the GameService.
 * Uses environment variable if set, otherwise falls back to localhost.
 */
export function getServiceUrl(): string {
  // Check for environment variable (set in production deployment)
  if (typeof window !== 'undefined') {
    // Browser environment - check for injected config
    const configuredUrl = (window as { __CATAN_SERVICE_URL__?: string })
      .__CATAN_SERVICE_URL__;
    if (configuredUrl) {
      return configuredUrl;
    }
  }

  // Check Next.js public environment variable
  if (process.env.NEXT_PUBLIC_SERVICE_URL) {
    return process.env.NEXT_PUBLIC_SERVICE_URL;
  }

  return LOCAL_SERVICE_URL;
}

/**
 * Get the SignalR hub URL.
 */
export function getHubUrl(): string {
  return `${getServiceUrl()}/gameHub`;
}

/**
 * Service configuration object.
 */
export const serviceConfig = {
  get serviceUrl() {
    return getServiceUrl();
  },
  get hubUrl() {
    return getHubUrl();
  },
  /** Timeout for REST API calls in milliseconds */
  apiTimeout: 30000,
  /** Timeout for SignalR reconnection attempts in milliseconds */
  reconnectTimeout: 60000,
} as const;
