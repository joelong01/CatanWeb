#!/usr/bin/env node
// Quick script to inspect recording actions and test what gets sent to API
//
// Usage: node inspect-recording.mjs [recordingFile]
//   recordingFile: Path to recording JSON file (default: uses checked-in test recording)
//
// Examples:
//   node inspect-recording.mjs
//   node inspect-recording.mjs "../Catan3.GameService/Default Data/Recordings/VP-test.json"

import { readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Default to the most comprehensive checked-in recording
const defaultRecording = join(
  __dirname,
  '..',
  'Catan3.GameService',
  'Default Data',
  'Recordings',
  'full-simulated-game-with-VPs.json'
);
const recordingFile = process.argv[2] || defaultRecording;

console.log(`Loading recording from: ${recordingFile}`);
console.log();

let entity;
try {
  entity = JSON.parse(readFileSync(recordingFile, 'utf-8'));
} catch (err) {
  console.error(`Error loading recording: ${err.message}`);
  console.error('Available recordings in Default Data/Recordings/:');
  console.error('  - full-simulated-game-with-VPs.json (default)');
  console.error('  - full-simulated-game.json');
  console.error('  - Simulated-Regular-Game.json');
  console.error('  - VP-test.json');
  console.error('  - Balanced---Winner.json');
  process.exit(1);
}

const data = JSON.parse(entity.data);

console.log('=== Recording Info ===');
console.log(`Name: ${entity.name}`);
console.log(`Actions: ${data.actions.length}`);
console.log(`Players: ${entity.playerCount}`);
console.log();

console.log('=== Recording Data Format ===');
// Find first buildingUpgrade action for inspection (field is "type" not "actionType")
const buildingUpgradeIdx = data.actions.findIndex((a) => a.type === 'buildingUpgrade');
if (buildingUpgradeIdx >= 0) {
  const action = data.actions[buildingUpgradeIdx];
  console.log(`Action ${buildingUpgradeIdx} (buildingUpgrade from recording):`);
  console.log(JSON.stringify(action, null, 2));

  // 2. Show what the proxy would send
  console.log('\n=== What Proxy Would Send ===');
  const proxyPayload = {
    gameId: 'test-game',
    playerId: 'test-player',
    messageType: 'BuildingUpgradeMessage',
    messageData: {
      buildingKey: action.buildingKey,
    },
  };
  console.log('Proxy command body:');
  console.log(JSON.stringify(proxyPayload, null, 2));

  // Check if property names match
  console.log('\n=== Property Name Check ===');
  const bk = action.buildingKey;
  console.log('Has hexCoordinates?', 'hexCoordinates' in bk);
  console.log('Has position?', 'position' in bk);
  if (bk.hexCoordinates) {
    console.log('hexCoordinates has q?', 'q' in bk.hexCoordinates);
    console.log('hexCoordinates has r?', 'r' in bk.hexCoordinates);
    console.log('hexCoordinates has s?', 's' in bk.hexCoordinates);
  }
} else {
  console.log('No buildingUpgrade actions found in this recording.');
  console.log('First 5 action types:');
  data.actions.slice(0, 5).forEach((action, i) => {
    console.log(`  ${i}: ${action.type}`);
  });
}

// 3. Show what C# AsyncCommandProcessor expects
console.log('\n=== C# AsyncCommandProcessor Expects ===');
console.log('messageData.GetProperty("buildingKey")');
console.log('buildingKey.GetProperty("hexCoordinates")');
console.log('buildingKey.GetProperty("position")');
console.log('hexCoordinates.GetProperty("q"), ("r"), ("s")');
