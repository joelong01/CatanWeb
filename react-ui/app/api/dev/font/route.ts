import { readFile, stat } from 'fs/promises';
import { join } from 'path';
import { NextResponse } from 'next/server';

/**
 * Dev-only API route that serves Catan.ttf directly from disk with no-cache
 * headers. This allows font hot-reload during development: rebuild the font,
 * then hard-refresh (Ctrl+Shift+R) to pick up changes without restarting
 * the Next.js dev server.
 *
 * In production builds, this route is unused — layout.tsx uses next/font/local
 * which bakes the font into the build with content-hash caching.
 */
export async function GET() {
  if (process.env.NODE_ENV !== 'development') {
    return NextResponse.json({ error: 'dev only' }, { status: 404 });
  }

  const fontPath = join(process.cwd(), 'public/fonts/Catan.ttf');

  try {
    const [buffer, fileStat] = await Promise.all([
      readFile(fontPath),
      stat(fontPath),
    ]);

    return new NextResponse(buffer, {
      headers: {
        'Content-Type': 'font/ttf',
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        'Pragma': 'no-cache',
        'Expires': '0',
        'ETag': `"${fileStat.mtimeMs.toString(36)}"`,
        'Last-Modified': fileStat.mtime.toUTCString(),
      },
    });
  } catch {
    return NextResponse.json({ error: 'Font file not found' }, { status: 404 });
  }
}
