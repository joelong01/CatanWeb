/**
 * Round-trip serialization test script.
 * Reads JSON from input file, parses it, re-serializes it, writes to output file.
 * This simulates what happens when TypeScript receives data from C# and sends it back.
 */

import { readFileSync, writeFileSync } from 'fs';

const inputFile = process.argv[2];
const outputFile = process.argv[3];

if (!inputFile || !outputFile) {
    console.error('Usage: node test-roundtrip.mjs <input.json> <output.json>');
    process.exit(1);
}

try {
    // Read and parse JSON (simulates receiving from C#)
    const inputJson = readFileSync(inputFile, 'utf8');
    const parsed = JSON.parse(inputJson);

    // Re-serialize (simulates sending back to C#)
    const outputJson = JSON.stringify(parsed);

    // Write output
    writeFileSync(outputFile, outputJson);

    process.exit(0);
} catch (error) {
    console.error('Round-trip failed:', error.message);
    process.exit(1);
}
