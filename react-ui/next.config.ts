import type { NextConfig } from 'next';
import { readFileSync } from 'fs';
import { join } from 'path';

/**
 * Load minimal Azure config from .azure/catan-azure.json.
 *
 * Only two values are read from the config:
 *   - baseName: used to derive all resource names via naming conventions
 *   - auth.clientId: needed for MSAL sign-in (can't be derived or discovered)
 *
 * Everything else (subscription, resource group, cosmos account, etc.) is
 * either derived from baseName or discovered from Azure at runtime.
 */
function loadAzureConfig(): Record<string, string> {
  const env: Record<string, string> = {};

  try {
    const configPath = join(__dirname, '..', '.azure', 'catan-azure.json');
    const cfg = JSON.parse(readFileSync(configPath, 'utf-8'));

    if (cfg.baseName) env['NEXT_PUBLIC_AZURE_BASE_NAME'] = cfg.baseName;
    if (cfg.auth?.clientId) env['NEXT_PUBLIC_AZURE_CLIENT_ID'] = cfg.auth.clientId;
  } catch {
    // Config file not found — Azure features disabled
  }

  return env;
}

const azureEnv = loadAzureConfig();

/**
 * Inject build-time version info so the UI can display what's actually running.
 * Format: "staging-Mar27-9:22-f94c7d4"
 * These are baked in at build time — not runtime-discoverable.
 */
function getBuildVersionEnv(): Record<string, string> {
  const { execSync } = require('child_process');
  const env: Record<string, string> = {};

  try {
    env['NEXT_PUBLIC_BUILD_COMMIT'] = execSync('git rev-parse --short HEAD', {
      encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();
  } catch {
    env['NEXT_PUBLIC_BUILD_COMMIT'] = process.env.DEPLOY_COMMIT ?? 'unknown';
  }

  try {
    env['NEXT_PUBLIC_BUILD_BRANCH'] = execSync('git rev-parse --abbrev-ref HEAD', {
      encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'],
    }).trim();
  } catch {
    env['NEXT_PUBLIC_BUILD_BRANCH'] = 'unknown';
  }

  const now = new Date();
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
  env['NEXT_PUBLIC_BUILD_TIME'] = process.env.DEPLOY_BUILD_TIME
    ?? `${months[now.getMonth()]}${now.getDate()}-${now.getHours()}:${String(now.getMinutes()).padStart(2, '0')}`;

  return env;
}

const buildEnv = getBuildVersionEnv();

const nextConfig: NextConfig = {
  output: 'standalone',
  images: {
    unoptimized: true,
  },
  env: {
    ...azureEnv,
    ...buildEnv,
  },
  async headers() {
    return [
      {
        // HTML pages: never cache at CDN/proxy level — always revalidate.
        // This prevents stale deployments from being served after a redeploy.
        // JS/CSS chunks use content-hashed filenames and are safe to cache.
        source: '/((?!_next/static|_next/image|favicon).*)',
        headers: [
          { key: 'Cache-Control', value: 'no-cache, no-store, must-revalidate' },
          { key: 'Pragma', value: 'no-cache' },
        ],
      },
      {
        // Theme assets: cache but always revalidate (ETag handles 304s efficiently)
        source: '/themes/:path*',
        headers: [{ key: 'Cache-Control', value: 'public, no-cache' }],
      },
    ];
  },
};

export default nextConfig;
