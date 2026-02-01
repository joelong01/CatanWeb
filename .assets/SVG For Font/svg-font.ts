/**
 * build-icon-font.ts
 *
 * Deterministic SVG -> TTF icon font builder with explicit glyph mapping.
 *
 * Pipeline:
 *   svgicons2svgfont (SVG glyphs -> SVG font)
 *   svg2ttf          (SVG font -> TTF)
 *
 * Outputs:
 *   <outDir>/<fontName>.svg        (intermediate SVG font)
 *   <outDir>/<fontName>.ttf        (final TTF)
 *   <outDir>/<fontName>.map.json   (filename -> codepoint/unicode)
 *
 * Usage:
 *   npx tsx build-icon-font.ts --input ./svgs --output ./out --name Catan --map ./glyph-map.json
 */

import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import { Writable } from "node:stream";
import { pipeline } from "node:stream/promises";

import fg from "fast-glob";
import yargs from "yargs";
import { hideBin } from "yargs/helpers";

import { SVGIcons2SVGFontStream } from "svgicons2svgfont";
import svg2ttf from "svg2ttf";

type FontMapEntry = {
  file: string;
  glyphName: string;
  codepointHex: string;
  codepointDec: number;
  unicode: string;
};

type FontMap = {
  fontName: string;
  glyphCount: number;
  mappedCount: number;
  autoAssignedCount: number;
  entries: FontMapEntry[];
};

const argv = yargs(hideBin(process.argv))
  .option("input", {
    type: "string",
    demandOption: true,
    describe: "Input directory containing SVG files",
  })
  .option("output", {
    type: "string",
    demandOption: true,
    describe: "Output directory for generated fonts",
  })
  .option("name", {
    type: "string",
    demandOption: true,
    describe: "Font name (also used for output file names)",
  })
  .option("map", {
    type: "string",
    describe:
      "Path to glyph mapping JSON (filename -> hex codepoint). Unmapped files get auto-assigned after the highest mapped codepoint.",
  })
  .option("start", {
    type: "string",
    default: "E000",
    describe:
      "Fallback start codepoint (hex) when no --map is provided (default: E000)",
  })
  .option("fontHeight", {
    type: "number",
    default: 1000,
    describe: "Units-per-em (common: 1000)",
  })
  .option("descent", {
    type: "number",
    default: 200,
    describe: "Descent in font units (tune for vertical alignment)",
  })
  .option("normalize", {
    type: "boolean",
    default: true,
    describe: "Normalize glyph sizes based on viewBox",
  })
  .option("centerHorizontally", {
    type: "boolean",
    default: true,
    describe: "Center glyphs horizontally in the em box",
  })
  .strict()
  .help()
  .parseSync();

function parseHexCodepoint(hex: string): number {
  const cleaned = hex.trim().replace(/^0x/i, "");
  if (!/^[0-9a-fA-F]+$/.test(cleaned)) {
    throw new Error(`Invalid hex codepoint: "${hex}"`);
  }
  const cp = Number.parseInt(cleaned, 16);
  if (!Number.isFinite(cp) || cp <= 0 || cp > 0x10ffff) {
    throw new Error(`Codepoint out of range: "${hex}"`);
  }
  return cp;
}

function toHex(cp: number): string {
  return cp.toString(16).toUpperCase();
}

function safeGlyphName(filePath: string): string {
  return path.basename(filePath, path.extname(filePath));
}

/**
 * Load explicit glyph mapping from JSON file.
 * Keys starting with "_" are treated as comments and skipped.
 * Values must be hex codepoint strings (e.g. "E900").
 */
async function loadGlyphMap(
  mapPath: string
): Promise<Record<string, number>> {
  const raw = await fsp.readFile(mapPath, "utf8");
  const parsed = JSON.parse(raw);
  const result: Record<string, number> = {};

  for (const [key, value] of Object.entries(parsed)) {
    if (key.startsWith("_")) continue;
    if (typeof value !== "string") continue;
    result[key] = parseHexCodepoint(value);
  }

  return result;
}

class StringCollector extends Writable {
  private chunks: Buffer[] = [];

  _write(
    chunk: Buffer | string,
    _encoding: BufferEncoding,
    callback: (error?: Error | null) => void
  ): void {
    this.chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    callback();
  }

  toStringUTF8(): string {
    return Buffer.concat(this.chunks).toString("utf8");
  }
}

async function main(): Promise<void> {
  const inputDir = path.resolve(argv.input);
  const outputDir = path.resolve(argv.output);
  const fontName = argv.name;
  const fontHeight = argv.fontHeight;
  const descent = argv.descent;

  await fsp.mkdir(outputDir, { recursive: true });

  // Non-recursive glob: only *.svg in the input directory (not subdirs)
  const pattern = path.join(inputDir, "*.svg").replace(/\\/g, "/");
  const svgFiles = (await fg(pattern, { onlyFiles: true })).sort((a, b) =>
    a.localeCompare(b, "en")
  );

  if (svgFiles.length === 0) {
    throw new Error(`No SVG files found in: ${inputDir}`);
  }

  // Load explicit mapping if provided
  const explicitMap: Record<string, number> = argv.map
    ? await loadGlyphMap(path.resolve(argv.map))
    : {};

  const hasExplicitMap = Object.keys(explicitMap).length > 0;

  // Track used codepoints to prevent collisions
  const usedCodepoints = new Set<number>();
  let mappedCount = 0;

  // Find the highest explicitly-mapped codepoint for auto-assignment start
  let autoStart = parseHexCodepoint(argv.start);
  for (const cp of Object.values(explicitMap)) {
    usedCodepoints.add(cp);
    if (cp >= autoStart) autoStart = cp + 1;
    mappedCount++;
  }

  // Build entries: explicit codepoints first, then auto-assign the rest
  type PreEntry = {
    filePath: string;
    fileName: string;
    codepoint: number;
    explicit: boolean;
  };

  const preEntries: PreEntry[] = [];

  for (const filePath of svgFiles) {
    const fileName = path.basename(filePath);
    const explicitCp = explicitMap[fileName];

    if (explicitCp !== undefined) {
      preEntries.push({
        filePath,
        fileName,
        codepoint: explicitCp,
        explicit: true,
      });
    } else {
      // Auto-assign: find next unused codepoint
      while (usedCodepoints.has(autoStart)) autoStart++;
      usedCodepoints.add(autoStart);
      preEntries.push({
        filePath,
        fileName,
        codepoint: autoStart,
        explicit: false,
      });
      autoStart++;
    }
  }

  // Check for duplicate codepoints (shouldn't happen, but be safe)
  const cpCheck = new Map<number, string>();
  for (const pe of preEntries) {
    const existing = cpCheck.get(pe.codepoint);
    if (existing) {
      throw new Error(
        `Codepoint collision: U+${toHex(pe.codepoint)} assigned to both "${existing}" and "${pe.fileName}"`
      );
    }
    cpCheck.set(pe.codepoint, pe.fileName);
  }

  // Sort by codepoint for deterministic font output
  preEntries.sort((a, b) => a.codepoint - b.codepoint);

  const entries: FontMapEntry[] = preEntries.map((pe) => ({
    file: pe.fileName,
    glyphName: safeGlyphName(pe.filePath),
    codepointHex: toHex(pe.codepoint),
    codepointDec: pe.codepoint,
    unicode: String.fromCodePoint(pe.codepoint),
  }));

  const svgFontOutPath = path.join(outputDir, `${fontName}.svg`);
  const ttfOutPath = path.join(outputDir, `${fontName}.ttf`);
  const mapOutPath = path.join(outputDir, `${fontName}.map.json`);

  // Build SVG font
  const fontStream = new SVGIcons2SVGFontStream({
    fontName,
    fontHeight,
    descent,
    normalize: argv.normalize,
    centerHorizontally: argv.centerHorizontally,
  });

  const collector = new StringCollector();
  const piping = pipeline(fontStream, collector);

  for (const e of entries) {
    const absPath = path.join(inputDir, e.file);
    const glyphStream = fs.createReadStream(absPath);

    (glyphStream as any).metadata = {
      name: e.glyphName,
      unicode: [e.unicode],
    };

    fontStream.write(glyphStream);
  }

  fontStream.end();
  await piping;

  const svgFontText = collector.toStringUTF8();
  await fsp.writeFile(svgFontOutPath, svgFontText, "utf8");

  // Convert SVG font -> TTF
  const ttf = svg2ttf(svgFontText, {});
  await fsp.writeFile(ttfOutPath, Buffer.from(ttf.buffer));

  // Write mapping JSON
  const autoAssignedCount = preEntries.filter((pe) => !pe.explicit).length;
  const fontMap: FontMap = {
    fontName,
    glyphCount: entries.length,
    mappedCount: preEntries.filter((pe) => pe.explicit).length,
    autoAssignedCount,
    entries,
  };
  await fsp.writeFile(mapOutPath, JSON.stringify(fontMap, null, 2), "utf8");

  // Summary
  console.log(`Built font: ${ttfOutPath}`);
  console.log(`SVG font:   ${svgFontOutPath}`);
  console.log(`Map:        ${mapOutPath}`);
  console.log(`Glyphs:     ${entries.length} total (${fontMap.mappedCount} mapped, ${autoAssignedCount} auto-assigned)`);

  if (hasExplicitMap) {
    // Report any map entries that didn't match an SVG file
    const svgBaseNames = new Set(svgFiles.map((f: string) => path.basename(f)));
    const unmatchedMapEntries = Object.keys(explicitMap).filter(
      (k) => !svgBaseNames.has(k)
    );
    if (unmatchedMapEntries.length > 0) {
      console.warn(
        `\nWarning: ${unmatchedMapEntries.length} map entries have no matching SVG file:`
      );
      for (const name of unmatchedMapEntries) {
        console.warn(`  ${name} -> U+${toHex(explicitMap[name])}`);
      }
    }
  }

  // Print codepoint table
  console.log("\nCodepoint assignments:");
  for (const e of entries) {
    const pe = preEntries.find((p) => p.fileName === e.file)!;
    const marker = pe.explicit ? "(mapped)" : "(auto)";
    console.log(`  U+${e.codepointHex}  ${e.glyphName.padEnd(24)} ${marker}`);
  }
}

main().catch((err) => {
  console.error("ERROR:", err instanceof Error ? err.message : err);
  process.exitCode = 1;
});
