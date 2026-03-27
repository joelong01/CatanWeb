import type { Metadata, Viewport } from 'next';
import localFont from 'next/font/local';

/**
 * Force all pages to be dynamically rendered (no static prerendering).
 * Without this, Next.js caches pages with s-maxage=31536000 and serves
 * stale HTML after deployments. The health check, build version, and
 * splash screen all need fresh renders.
 */
export const dynamic = 'force-dynamic';
import { FontReloader } from '@/components/dev/FontReloader';
import { StartupLogger } from '@/components/StartupLogger';
import { ThemeInitializer } from '@/components/providers/ThemeInitializer';
import BuildVersion from '@/components/BuildVersion';
import './globals.css';

const isDev = process.env.NODE_ENV === 'development';

/**
 * Catan custom font for game icons and special characters.
 *
 * Production: next/font/local bakes the font into the build with content-hash
 * caching for optimal performance.
 *
 * Development: We ALSO declare a runtime @font-face that loads via an API route
 * with no-cache headers. This lets you rebuild the font and hard-refresh
 * (Ctrl+Shift+R) without restarting the dev server.
 */
const catanFont = localFont({
  src: '../public/fonts/Catan.ttf',
  variable: '--font-catan',
  display: 'swap',
});

/**
 * In dev, override the font with a runtime @font-face that reads from disk
 * on every request (via API route with no-cache headers). The CSS variable
 * --font-catan is set to this family name so all components use it seamlessly.
 *
 * The localFont-generated family (e.g. __catanFont_abc123) is still loaded but
 * this override takes priority because it's declared later in the cascade and
 * the CSS variable points directly to it.
 */
const devFontStyle = `
  @font-face {
    font-family: 'CatanDev';
    src: url('/api/dev/font') format('truetype');
    font-display: swap;
  }
  :root {
    --font-catan: 'CatanDev';
  }
`;

/** Application metadata for SEO and browser display. */
export const metadata: Metadata = {
  title: 'Catan',
  description: 'Settlers of Catan game tracker',
};

/** Viewport configuration — viewportFit: cover handles iPhone notch. */
export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  viewportFit: 'cover',
};

/**
 * Root layout component that wraps all pages.
 * Sets up global fonts, dark theme, and base styling.
 */
export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>): React.ReactElement {
  return (
    <html lang="en" className="dark" suppressHydrationWarning>
      <head>
        {isDev ? (
          // Dev: runtime @font-face via API route (no-cache, always reads from disk)
          <style dangerouslySetInnerHTML={{ __html: devFontStyle }} />
        ) : (
          // Prod: preload the static font asset
          <link
            rel="preload"
            href="/fonts/Catan.ttf"
            as="font"
            type="font/ttf"
            crossOrigin="anonymous"
          />
        )}
      </head>
      <body className={`${catanFont.variable} antialiased`}>
        <ThemeInitializer />
        {children}
        <StartupLogger />
        <BuildVersion />
        {isDev && <FontReloader />}
      </body>
    </html>
  );
}
