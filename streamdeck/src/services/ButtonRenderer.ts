import type { PlayerColor } from "../models/types";

const SIZE = 144;

/** Encode an SVG string as a data URL for setImage(). */
function svgDataUrl(svg: string): string {
  return `data:image/svg+xml,${encodeURIComponent(svg)}`;
}

/** Wrap content in a 144×144 SVG root. */
function svgRoot(content: string): string {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}" viewBox="0 0 ${SIZE} ${SIZE}">${content}</svg>`;
}

/** A linear gradient definition from primary → secondary going top-down. */
function gradientDef(
  id: string,
  primary: string,
  secondary: string,
): string {
  return `<defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="${primary}"/><stop offset="100%" stop-color="${secondary}"/></linearGradient></defs>`;
}

/** Rounded rect background filling the entire key. */
function background(fill: string, opacity = 1): string {
  return `<rect width="${SIZE}" height="${SIZE}" rx="12" fill="${fill}" opacity="${opacity}"/>`;
}

// ── Roll Button ──────────────────────────────────────────────────────

export function renderRollButton(
  roll: number,
  count: number,
  percentage: number,
  colors: PlayerColor | undefined,
  isEnabled: boolean,
): string {
  const primary = colors?.primary ?? "#2d5a27";
  const secondary = colors?.secondary ?? "#1a3518";
  const fg = colors?.foreground ?? "#ffffff";
  const opacity = isEnabled ? 1 : 0.35;

  // 6 and 8 use red text (matching board number tokens)
  const numColor = roll === 6 || roll === 8 ? "#e53935" : fg;
  const statsText =
    count > 0 ? `${count} / ${Math.round(percentage)}%` : "";

  const gId = `rg${roll}`;
  const svg = svgRoot(
    gradientDef(gId, primary, secondary) +
      `<g opacity="${opacity}">` +
      background(`url(#${gId})`) +
      `<text x="72" y="75" text-anchor="middle" font-family="Arial,sans-serif" font-size="48" font-weight="bold" fill="${numColor}">${roll}</text>` +
      `<text x="72" y="110" text-anchor="middle" font-family="Arial,sans-serif" font-size="16" fill="${fg}" opacity="0.8">${statsText}</text>` +
      `</g>`,
  );
  return svgDataUrl(svg);
}

// ── Soldier Button ───────────────────────────────────────────────────

export function renderSoldierButton(
  unspentCount: number,
  colors: PlayerColor | undefined,
  isEnabled: boolean,
): string {
  const primary = colors?.primary ?? "#5d4037";
  const secondary = colors?.secondary ?? "#3e2723";
  const fg = colors?.foreground ?? "#ffffff";
  const opacity = isEnabled ? 1 : 0.35;
  const gId = "sg";

  // Knight icon (simplified shield shape)
  const knight =
    `<path d="M72 25 L100 50 L100 90 Q100 110 72 125 Q44 110 44 90 L44 50 Z" ` +
    `fill="none" stroke="${fg}" stroke-width="3"/>` +
    `<text x="72" y="88" text-anchor="middle" font-family="Arial,sans-serif" font-size="28" fill="${fg}">⚔</text>`;

  const badge =
    unspentCount > 0
      ? `<circle cx="112" cy="28" r="16" fill="#e53935"/>` +
        `<text x="112" y="34" text-anchor="middle" font-family="Arial,sans-serif" font-size="18" font-weight="bold" fill="#fff">${unspentCount}</text>`
      : "";

  const svg = svgRoot(
    gradientDef(gId, primary, secondary) +
      `<g opacity="${opacity}">` +
      background(`url(#${gId})`) +
      knight +
      badge +
      `</g>`,
  );
  return svgDataUrl(svg);
}

// ── Undo / Redo Buttons ──────────────────────────────────────────────

export function renderUndoButton(isEnabled: boolean): string {
  const opacity = isEnabled ? 1 : 0.35;
  const svg = svgRoot(
    `<g opacity="${opacity}">` +
      background("#333") +
      `<path d="M95 72 A30 30 0 1 0 72 102" fill="none" stroke="#fff" stroke-width="5" stroke-linecap="round"/>` +
      `<polygon points="50,65 50,85 65,75" fill="#fff"/>` +
      `</g>`,
  );
  return svgDataUrl(svg);
}

export function renderRedoButton(isEnabled: boolean): string {
  const opacity = isEnabled ? 1 : 0.35;
  const svg = svgRoot(
    `<g opacity="${opacity}">` +
      background("#333") +
      `<path d="M49 72 A30 30 0 1 1 72 102" fill="none" stroke="#fff" stroke-width="5" stroke-linecap="round"/>` +
      `<polygon points="94,65 94,85 79,75" fill="#fff"/>` +
      `</g>`,
  );
  return svgDataUrl(svg);
}

// ── Server Toggle ────────────────────────────────────────────────────

export function renderServerToggle(isLocal: boolean): string {
  if (isLocal) {
    // House icon
    const svg = svgRoot(
      background("#1a5276") +
        `<polygon points="72,30 115,70 100,70 100,115 44,115 44,70 29,70" fill="none" stroke="#fff" stroke-width="4"/>` +
        `<rect x="60" y="85" width="24" height="30" fill="#fff" opacity="0.8"/>` +
        `<text x="72" y="138" text-anchor="middle" font-family="Arial,sans-serif" font-size="11" fill="#fff">Local</text>`,
    );
    return svgDataUrl(svg);
  }
  // Cloud icon
  const svg = svgRoot(
    background("#6c3483") +
      `<ellipse cx="72" cy="68" rx="40" ry="25" fill="none" stroke="#fff" stroke-width="4"/>` +
      `<ellipse cx="50" cy="75" rx="20" ry="15" fill="#6c3483" stroke="#fff" stroke-width="3"/>` +
      `<ellipse cx="94" cy="75" rx="20" ry="15" fill="#6c3483" stroke="#fff" stroke-width="3"/>` +
      `<text x="72" y="138" text-anchor="middle" font-family="Arial,sans-serif" font-size="11" fill="#fff">Azure</text>`,
  );
  return svgDataUrl(svg);
}

// ── Auto-Navigate Toggle ─────────────────────────────────────────────

export function renderAutoToggle(isOn: boolean): string {
  const color = isOn ? "#27ae60" : "#555";
  const label = isOn ? "Auto" : "Manual";
  const svg = svgRoot(
    background(color) +
      `<circle cx="72" cy="60" r="25" fill="none" stroke="#fff" stroke-width="3"/>` +
      `<polygon points="65,48 90,60 65,72" fill="#fff"/>` +
      `<text x="72" y="120" text-anchor="middle" font-family="Arial,sans-serif" font-size="14" fill="#fff">${label}</text>`,
  );
  return svgDataUrl(svg);
}

// ── Game State Display ───────────────────────────────────────────────

export function renderGameState(
  message: string,
  colors: PlayerColor | undefined,
): string {
  const primary = colors?.primary ?? "#444";
  const secondary = colors?.secondary ?? "#222";
  const fg = colors?.foreground ?? "#ffffff";
  const gId = "gsg";

  // Word-wrap message into lines of ~12 chars
  const words = message.split(" ");
  const lines: string[] = [];
  let current = "";
  for (const w of words) {
    if (current.length + w.length + 1 > 12 && current.length > 0) {
      lines.push(current);
      current = w;
    } else {
      current = current ? `${current} ${w}` : w;
    }
  }
  if (current) lines.push(current);

  const startY = 72 - (lines.length - 1) * 10;
  const textEls = lines
    .map(
      (line, i) =>
        `<text x="72" y="${startY + i * 22}" text-anchor="middle" font-family="Arial,sans-serif" font-size="16" fill="${fg}">${line}</text>`,
    )
    .join("");

  const svg = svgRoot(
    gradientDef(gId, primary, secondary) + background(`url(#${gId})`) + textEls,
  );
  return svgDataUrl(svg);
}

// ── Navigation Button ────────────────────────────────────────────────

export function renderNavigate(
  label: string,
  isHighlighted: boolean,
): string {
  const bg = isHighlighted ? "#d4ac0d" : "#444";
  const fg = isHighlighted ? "#000" : "#fff";
  const svg = svgRoot(
    background(bg) +
      `<text x="72" y="80" text-anchor="middle" font-family="Arial,sans-serif" font-size="22" font-weight="bold" fill="${fg}">${label}</text>`,
  );
  return svgDataUrl(svg);
}

// ── Back Button ──────────────────────────────────────────────────────

export function renderBackButton(): string {
  const svg = svgRoot(
    background("#444") +
      `<polygon points="40,72 85,42 85,62 110,62 110,82 85,82 85,102" fill="#fff"/>`,
  );
  return svgDataUrl(svg);
}
