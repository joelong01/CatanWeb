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

const nextConfig: NextConfig = {
  output: 'standalone',
  images: {
    unoptimized: true,
  },
  env: {
    ...azureEnv,
  },
  async headers() {
    return [
      {
        // Theme assets: cache but always revalidate (ETag handles 304s efficiently)
        source: '/themes/:path*',
        headers: [{ key: 'Cache-Control', value: 'public, no-cache' }],
      },
    ];
  },
};

export default nextConfig;
