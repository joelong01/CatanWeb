/**
 * Converts SVG files from fill-rule:evenodd to nonzero-compatible winding.
 *
 * svgicons2svgfont ignores fill-rule:evenodd (known issue #62), causing compound
 * paths to render as solid blobs in the font. This script uses svg-reorient to fix
 * subpath winding directions so the visual result is identical under nonzero.
 *
 * Usage: node reorient-evenodd.mjs <file1.svg> [file2.svg ...]
 *
 * Handles SVGs with <g transform="translate(...)"> wrappers (from viewBox origin
 * normalization) by stripping them before reorientation and re-applying after.
 */
import { readFileSync, writeFileSync } from 'fs';
import { createRequire } from 'module';
import { join, dirname } from 'path';

// Resolve svg-reorient from .iconfont-build/node_modules (where npm installs it)
const projectRoot = join(dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '../..');
const require = createRequire(join(projectRoot, '.iconfont-build', 'package.json'));
const { reorient } = require('svg-reorient');

const files = process.argv.slice(2);
if (files.length === 0) {
  console.error('Usage: node reorient-evenodd.mjs <file1.svg> [file2.svg ...]');
  process.exit(1);
}

for (const file of files) {
  let svg = readFileSync(file, 'utf-8');

  // Check if this SVG uses evenodd
  if (!svg.includes('evenodd')) {
    console.log(`SKIP ${file} (no evenodd)`);
    continue;
  }

  // Strip translate wrapper group if present (svg-reorient can't handle it)
  const translateRe = /<g transform="translate\(([^)]+)\)">\s*/;
  const translateMatch = svg.match(translateRe);
  let hadTranslate = false;
  let translateValue = '';

  if (translateMatch) {
    hadTranslate = true;
    translateValue = translateMatch[1];
    svg = svg.replace(translateRe, '');
    svg = svg.replace(/\s*<\/g>\s*(<\/svg>)/, '$1');
  }

  // Reorient paths for nonzero winding
  let result = reorient(svg);

  // Change fill-rule from evenodd to nonzero
  result = result.replace(/fill-rule:\s*evenodd/g, 'fill-rule:nonzero');
  result = result.replace(/fill-rule="evenodd"/g, 'fill-rule="nonzero"');

  // Re-apply translate wrapper if it was stripped.
  // Use the original translate value — the viewBox was already zeroed by fix-svg,
  // so checking the viewBox origin would always find (0,0) and skip re-applying.
  if (hadTranslate) {
    result = result.replace(/(<svg[^>]*>)/, `$1\n<g transform="translate(${translateValue})">`);
    result = result.replace(/<\/svg>/, '</g></svg>');
  }

  writeFileSync(file, result, 'utf-8');
  console.log(`OK   ${file}`);
}
