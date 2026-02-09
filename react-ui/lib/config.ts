/**
 * Application configuration.
 *
 * Supports both local development and deployed environments with runtime injection.
 */

/** Default GameService port */
const GAME_SERVICE_PORT = 8080;

/**
 * Gets the base URL for the GameService.
 * Priority:
 * 1. Window injection (window.__CATAN_SERVICE_URL__) - for runtime config
 * 2. Environment variable (NEXT_PUBLIC_GAME_SERVICE_URL)
 * 3. Same hostname as current page with GameService port
 *    (ensures iPhones/other devices on the network reach the Mac, not localhost)
 */
export function getServiceUrl(): string {
  // Check for window injection (runtime config in production)
  if (typeof window !== 'undefined') {
    const configuredUrl = (window as { __CATAN_SERVICE_URL__?: string }).__CATAN_SERVICE_URL__;
    if (configuredUrl) {
      return configuredUrl;
    }
  }

  // Check Next.js public environment variable
  if (process.env.NEXT_PUBLIC_GAME_SERVICE_URL) {
    return process.env.NEXT_PUBLIC_GAME_SERVICE_URL;
  }

  // Derive from current page hostname so network devices (iPhone, etc.)
  // reach the GameService on the same machine as the Next.js dev server.
  if (typeof window !== 'undefined') {
    return `http://${window.location.hostname}:${GAME_SERVICE_PORT}`;
  }

  return `http://localhost:${GAME_SERVICE_PORT}`;
}

/**
 * Gets the SignalR hub URL.
 */
export function getHubUrl(): string {
  return `${getServiceUrl()}/gameHub`;
}

/**
 * Consolidated application configuration.
 */
export const config = {
  /** GameService base URL for REST API and SignalR connections */
  get gameServiceUrl() {
    return getServiceUrl();
  },
  /** SignalR Hub URL */
  get hubUrl() {
    return getHubUrl();
  },
  /** Timeout for REST API calls in milliseconds */
  apiTimeout: 30000,
  /** Timeout for SignalR reconnection attempts in milliseconds */
  reconnectTimeout: 60000,
} as const;

export type AppConfig = typeof config;

// Export for backward compatibility with lib/services usage
export const serviceConfig = {
  get serviceUrl() {
    return getServiceUrl();
  },
  get hubUrl() {
    return getHubUrl();
  },
  apiTimeout: config.apiTimeout,
  reconnectTimeout: config.reconnectTimeout,
} as const;
