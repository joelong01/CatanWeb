import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  output: 'standalone',
  images: {
    unoptimized: true,
  },
  async headers() {
    return [
      {
        // Theme assets: cache but always revalidate (ETag handles 304s efficiently)
        source: '/themes/:path*',
        headers: [
          { key: 'Cache-Control', value: 'public, no-cache' },
        ],
      },
    ];
  },
};

export default nextConfig;
