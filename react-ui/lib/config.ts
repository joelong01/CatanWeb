/**
 * Application configuration loaded from environment variables.
 * All NEXT_PUBLIC_ variables are available client-side.
 */
export const config = {
  /** GameService base URL for REST API and SignalR connections */
  gameServiceUrl: process.env.NEXT_PUBLIC_GAME_SERVICE_URL || 'http://localhost:8080',
} as const;

/** Type-safe environment variable access */
export type AppConfig = typeof config;
