#!/usr/bin/env node
/**
 * Converts simple JSON profile definitions (.json) into proper .streamDeckProfile
 * zip archives that Stream Deck can import.
 *
 * Stream Deck expects .streamDeckProfile files to be ZIP archives containing:
 *   {uuid}.sdProfile/
 *     manifest.json          — profile metadata (Device, Name, Pages)
 *     Profiles/{page-uuid}/
 *       manifest.json        — page layout with actions keyed by "col,row"
 *
 * Source files: profiles/CatanHome.json, profiles/CatanRoll.json
 * Output files: profiles/CatanHome.streamDeckProfile, profiles/CatanRoll.streamDeckProfile
 *
 * Usage: node scripts/build-profiles.mjs
 */
import { readFileSync, mkdirSync, writeFileSync, rmSync } from "node:fs";
import { join, dirname } from "node:path";
import { execSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const sdPlugin = join(__dirname, "..", "com.catan.streamdeck.sdPlugin");
const profilesDir = join(sdPlugin, "profiles");
const COLUMNS = 5;

/** Convert flat position index (0-14) to "col,row" format */
function posToColRow(pos) {
  const col = pos % COLUMNS;
  const row = Math.floor(pos / COLUMNS);
  return `${col},${row}`;
}

/** Read a .json profile definition and build a proper .streamDeckProfile zip */
function buildProfile(name) {
  const srcFile = join(profilesDir, `${name}.json`);
  const simple = JSON.parse(readFileSync(srcFile, "utf8"));

  const profileUUID = randomUUID();
  const pageUUID = randomUUID();

  // Build the page manifest — convert flat position to "col,row" format
  const actions = {};
  for (const [posStr, action] of Object.entries(simple.Pages[0].Actions)) {
    const pos = parseInt(posStr, 10);
    const colRow = posToColRow(pos);
    actions[colRow] = {
      ActionID: randomUUID(),
      LinkedTitle: true,
      Name: action.UUID.split(".").pop(),
      Plugin: {
        Name: action.UUID.split(".").pop(),
        UUID: action.UUID,
        Version: "1.0",
      },
      Settings: action.Settings || {},
      State: action.State ?? 0,
      States: [{}],
    };
  }

  const pageManifest = {
    Controllers: [
      {
        Actions: actions,
        Layout: { Columns: 5, Rows: 3 },
        Type: 0,
      },
    ],
    Name: simple.Pages[0].Name || simple.Name,
  };

  const profileManifest = {
    Device: { Model: simple.DeviceModel },
    Name: simple.Name,
    Pages: {
      Current: pageUUID,
      Default: pageUUID,
      Pages: [pageUUID],
    },
    Version: "2.0",
  };

  // Create temp directory structure
  const tmpBase = join(profilesDir, `_tmp_${name}`);
  const sdProfileDir = join(tmpBase, `${profileUUID}.sdProfile`);
  const pageDir = join(sdProfileDir, "Profiles", pageUUID);

  rmSync(tmpBase, { recursive: true, force: true });
  mkdirSync(pageDir, { recursive: true });

  writeFileSync(
    join(sdProfileDir, "manifest.json"),
    JSON.stringify(profileManifest),
  );
  writeFileSync(
    join(pageDir, "manifest.json"),
    JSON.stringify(pageManifest),
  );

  // Zip into .streamDeckProfile (overwrite any existing)
  const outFile = join(profilesDir, `${name}.streamDeckProfile`);
  rmSync(outFile, { force: true });

  execSync(`cd "${tmpBase}" && zip -r "${outFile}" "${profileUUID}.sdProfile"`, {
    stdio: "pipe",
  });

  // Cleanup temp
  rmSync(tmpBase, { recursive: true, force: true });

  console.log(`  ${name}.streamDeckProfile created`);
}

const profiles = ["CatanHome", "CatanRoll"];
for (const name of profiles) {
  buildProfile(name);
}
console.log("Profile build complete.");
