import { NextRequest, NextResponse } from 'next/server';

const MAX_SIZE = 5 * 1024 * 1024; // 5MB
const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
const BROWSER_UA =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36';

/**
 * Extract og:image URL from HTML content.
 * Works for Facebook, Twitter, LinkedIn, and most social media sites.
 */
function extractOgImage(html: string): string | null {
  // Try og:image first
  const ogMatch =
    html.match(/<meta[^>]+property=["']og:image["'][^>]+content=["']([^"']+)["']/i) ??
    html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+property=["']og:image["']/i);
  if (ogMatch) return ogMatch[1];

  // Try twitter:image
  const twMatch =
    html.match(/<meta[^>]+name=["']twitter:image["'][^>]+content=["']([^"']+)["']/i) ??
    html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+name=["']twitter:image["']/i);
  if (twMatch) return twMatch[1];

  return null;
}

/**
 * Fetch an image, returning its bytes and content type.
 * Follows redirects. Validates size and content type.
 */
async function fetchImage(
  url: string
): Promise<{ data: ArrayBuffer; contentType: string } | { error: string }> {
  const response = await fetch(url, {
    headers: { 'User-Agent': BROWSER_UA },
    redirect: 'follow',
  });

  if (!response.ok) {
    return { error: `Remote server returned ${response.status}` };
  }

  const contentType = response.headers.get('content-type') ?? '';

  // If it's an image, return it directly
  if (IMAGE_TYPES.some((t) => contentType.startsWith(t))) {
    const data = await response.arrayBuffer();
    if (data.byteLength > MAX_SIZE) {
      return { error: 'Image exceeds 5MB limit' };
    }
    return { data, contentType: contentType.split(';')[0] };
  }

  // If it's HTML, try to extract og:image
  if (contentType.includes('text/html')) {
    const html = await response.text();
    const ogUrl = extractOgImage(html);
    if (!ogUrl) {
      return {
        error:
          'Page does not contain a direct image. Try right-clicking the image and copying the image address.',
      };
    }

    // Fetch the og:image URL
    const imgResponse = await fetch(ogUrl, {
      headers: { 'User-Agent': BROWSER_UA },
      redirect: 'follow',
    });

    if (!imgResponse.ok) {
      return { error: `Could not fetch image from page (${imgResponse.status})` };
    }

    const imgType = imgResponse.headers.get('content-type') ?? '';
    if (!IMAGE_TYPES.some((t) => imgType.startsWith(t))) {
      return { error: 'The extracted image URL did not return an image' };
    }

    const data = await imgResponse.arrayBuffer();
    if (data.byteLength > MAX_SIZE) {
      return { error: 'Image exceeds 5MB limit' };
    }
    return { data, contentType: imgType.split(';')[0] };
  }

  return { error: `URL returned ${contentType}, not an image` };
}

/**
 * GET /api/proxy-image?url=...
 *
 * Server-side image proxy that bypasses CORS restrictions.
 * If the URL points to an HTML page (e.g., Facebook photo page),
 * extracts the og:image meta tag and fetches that instead.
 */
export async function GET(request: NextRequest): Promise<NextResponse> {
  const url = request.nextUrl.searchParams.get('url');

  if (!url) {
    return NextResponse.json({ error: 'Missing url parameter' }, { status: 400 });
  }

  // Basic URL validation
  try {
    new URL(url);
  } catch {
    return NextResponse.json({ error: 'Invalid URL' }, { status: 400 });
  }

  try {
    const result = await fetchImage(url);

    if ('error' in result) {
      return NextResponse.json({ error: result.error }, { status: 422 });
    }

    return new NextResponse(result.data, {
      headers: {
        'Content-Type': result.contentType,
        'Cache-Control': 'public, max-age=3600',
      },
    });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to fetch image';
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
