# CSS & Theming As-Built

**Status:** As-Built
**Source:** `.design/css.md` & `WebUI/wwwroot/css/app.css` & `react-ui/app/globals.css`

## 1. Architecture

The styling follows a coherent design token system across both Blazor (Legacy) and React (Active) platforms.

*   **Variables**: Defined in `:root`.
*   **Scoping**: Usage of CSS Modules (React) or Scoped CSS (Blazor) prevents leakage.
*   **Orientation**: Heavy usage of `@media (orientation: portrait)` for responsive layouts.

## 2. Design Tokens

### Colors
*   `--game-bg-primary` (#222): Main background.
*   `--game-bg-panel` (#2a2a): Panel backgrounds.
*   `--overlay-dark`: Modal backdrops.
*   `--accent-primary` (#007bff): Action buttons.

### Player Colors & Gradients
Crucial for gameplay clarity.
*   `--color-player-[color]-primary`: Main identity color.
*   `--color-player-[color]-secondary`: Gradient/border accent.
*   `--color-player-[color]-foreground`: Text contrast color.
*   **Gradients**: Hex tiles use complex linear gradients (`--hex-content-gradient`) to simulate depth/terrain.

## 3. Typography

*   **Icons**: Custom Icon Font (`Segoe MDL2 Assets` parity) + FontAwesome.
*   **Game Font**: "Catan" custom font for specialized glyphs.

## 4. Mobile & Portrait Strategy

*   **Landscape**: 3-Panel Layout (Left Controls | Board | Right Stats).
*   **Portrait**: Tabbed Layout (Board vs Controls).
*   **Scaling**: `viewBox` scaling for the SVG board ensures it fits any aspect ratio.
*   **Touch**: Expanded hit-targets (44px min) for touch devices.
