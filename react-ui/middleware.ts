/**
 * @module middleware
 *
 * Next.js middleware — runs on every request before the page renders.
 *
 * Primary purpose: override Next.js's aggressive caching headers on HTML pages.
 * Without this, Next.js sets `s-maxage=31536000` (1 year) and `x-nextjs-prerender`,
 * causing browsers and CDN proxies to serve stale HTML after redeployments.
 *
 * Static assets (_next/static/*) use content-hashed filenames and are safe
 * to cache forever — this middleware skips them.
 */

import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest): NextResponse {
  const response = NextResponse.next();

  // HTML pages: always revalidate — prevents stale deployments
  response.headers.set('Cache-Control', 'no-cache, must-revalidate');
  response.headers.set('Pragma', 'no-cache');

  // Remove Next.js prerender headers that tell CDNs to cache for a year
  response.headers.delete('x-nextjs-cache');

  return response;
}

/**
 * Only run middleware on page routes — skip static assets, API routes,
 * and Next.js internals.
 */
export const config = {
  matcher: [
    // Match all pages except static files and API routes
    '/((?!_next/static|_next/image|api|favicon.ico|fonts|themes).*)',
  ],
};
