import type { Metadata } from 'next';
import localFont from 'next/font/local';
import './globals.css';

/**
 * Catan custom font for game icons and special characters.
 * Loaded locally from public/fonts to ensure availability.
 */
const catanFont = localFont({
  src: '../public/fonts/Catan.ttf',
  variable: '--font-catan',
  display: 'swap',
});

/** Application metadata for SEO and browser display. */
export const metadata: Metadata = {
  title: 'Catan',
  description: 'Settlers of Catan game tracker',
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
    <html lang="en" className="dark">
      <head>
        {/* Preload Catan font to prevent flash of unstyled text */}
        <link
          rel="preload"
          href="/fonts/Catan.ttf"
          as="font"
          type="font/ttf"
          crossOrigin="anonymous"
        />
      </head>
      <body className={`${catanFont.variable} antialiased`}>{children}</body>
    </html>
  );
}
