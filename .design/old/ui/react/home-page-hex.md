# Home Page Hex Layout

**Created:** 2026-01-25
**Status:** Implementation pending
**Related:** [hex-grid-component.md](hex-grid-component.md)

## Goal

Replace the current vertical menu list with a hex grid layout matching the New Game page style.

## Layout

Uses CLUSTER_7 layout (center + 6 surrounding hexes):

```text
        [North]
   [NW]  [Center]  [NE]
        [SW]  [SE]
             [South]
```

| Position          | Content                 | Component   | Notes                           |
| ----------------- | ----------------------- | ----------- | ------------------------------- |
| Center (0,0)      | "Catan" branding        | CenterHex   | Dice icon, amber accent         |
| North (0,-1)      | New Game                | MenuHex     | Gamepad icon, link to /new-game |
| NorthEast (1,-1)  | Open Game               | MenuHex     | Folder icon, link to /load-game |
| SouthEast (1,0)   | Edit Players            | MenuHex     | Users icon, link to /players    |
| South (0,1)       | Stats                   | MenuHex     | Chart icon, link to /stats      |
| SouthWest (-1,1)  | Water placeholder       | WaterHex    | Decorative                      |
| NorthWest (-1,0)  | Return to Game OR Water | Conditional | See below                       |

### NorthWest Hex Logic

```tsx
const northWestContent = activeGameId
  ? <MenuHex icon={faPlay} title="Return to Game" href={`/game/${activeGameId}`} />
  : <WaterHex />;
```

## Troubleshooting Section

Below the hex grid, display debugging info:

- Service URL from `getServiceUrl()` (shows actual endpoint being used)
- Version/build info for cache debugging

```tsx
<div className="text-center text-sm text-gray-500 mt-8">
  <p>Connected to: <code>{getServiceUrl()}</code></p>
  <p>Build: {BUILD_VERSION}</p>
</div>
```

## Content Components Needed

### CenterHex

Non-interactive branding hex for the center position.

```typescript
interface CenterHexProps {
  icon: IconDefinition;
  title: string;
  subtitle?: string;
  accentColor?: string;  // Tailwind class
  background?: string;   // Tailwind class
}
```

### MenuHex

Clickable navigation hex for menu items.

```typescript
interface MenuHexProps {
  icon: IconDefinition;
  title: string;
  subtitle?: string;
  accentColor?: string;
  background?: string;
  href?: string;         // Next.js Link navigation
  onClick?: () => void;  // Action callback
}
```

### WaterHex

Decorative placeholder using water texture.

```typescript
interface WaterHexProps {
  opacity?: number;  // Default: 0.6
}
```

## Files to Create/Modify

### New Files

1. `react-ui/components/hex-grid/content/CenterHex.tsx`
2. `react-ui/components/hex-grid/content/MenuHex.tsx`
3. `react-ui/components/hex-grid/content/WaterHex.tsx`
4. `react-ui/components/hex-grid/content/index.ts`

### Modified Files

1. `react-ui/components/hex-grid/index.ts` - Re-export content components
2. `react-ui/app/page.tsx` - Use hex layout

## Implementation

```tsx
// app/page.tsx
export default function Home() {
  const activeGameId = null; // TODO: Get from connection service

  const items: HexGridItem[] = [
    {
      id: 'center',
      coord: HEX_LAYOUTS.CLUSTER_7[0],
      content: <CenterHex icon={faDice} title="Catan" accentColor="text-amber-500" />,
      disabled: true,
    },
    {
      id: 'new-game',
      coord: HEX_LAYOUTS.CLUSTER_7[1],
      content: <MenuHex icon={faGamepad} title="New Game" href="/new-game" />,
    },
    // ... remaining hexes
  ];

  return (
    <MainLayout>
      <HexGrid hexSize={100} items={items} gap={4} borderColor="bg-gray-700" />
      <TroubleshootingSection />
    </MainLayout>
  );
}
```
