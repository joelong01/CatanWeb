#!/usr/bin/env npx tsx
/**
 * @module azure/azureDoctor.cli
 *
 * CLI entry point for the Azure Doctor — runs all infrastructure health
 * checks from the command line using `DefaultAzureCredential` (picks up
 * `az login` session).
 *
 * This is the same code that powers the browser splash screen, just
 * with a different credential source and console-based reporting.
 *
 * Usage:
 *   # Check all domains (read-only):
 *   AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv) \
 *     npx tsx react-ui/lib/azure/azureDoctor.cli.ts --no-fix
 *
 *   # Check all domains with auto-fix:
 *   AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv) \
 *     npx tsx react-ui/lib/azure/azureDoctor.cli.ts
 *
 *   # Check staging slot:
 *   AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv) \
 *     npx tsx react-ui/lib/azure/azureDoctor.cli.ts --staging
 *
 *   # JSON output (for catan.ps1 integration):
 *   AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv) \
 *     npx tsx react-ui/lib/azure/azureDoctor.cli.ts --json
 *
 * Exit codes:
 *   0 — all checks passed
 *   1 — one or more checks failed
 *   2 — fatal error (auth failure, missing config, etc.)
 */

import { DefaultAzureCredential } from '@azure/identity';
import { join } from 'path';
import { runAzureDoctor } from './azureDoctor';
import {
  resolveAzureConfig,
  getDefaultSubscriptionId,
  readBaseNameFromConfig,
} from './resolveConfig';
import type { CheckResult, CheckStatus } from './types';

// ── Argument parsing ─────────────────────────────────────────────────────────

const args = process.argv.slice(2);
const jsonOutput = args.includes('--json');
const noFix = args.includes('--no-fix');
const staging = args.includes('--staging');

/**
 * Extracts a named argument value from the CLI args.
 * e.g., getArg('--subscription') returns the value after --subscription.
 */
function getArg(name: string): string | undefined {
  const idx = args.indexOf(name);
  return idx >= 0 && idx + 1 < args.length ? args[idx + 1] : undefined;
}

// ── Configuration loading ────────────────────────────────────────────────────

/**
 * Resolves the Azure config from baseName (CLI arg or config file).
 * Everything is derived from naming conventions — no hardcoded resource IDs.
 * subscriptionId is discovered at runtime from the credential.
 */
function loadBaseName(): string {
  // CLI arg takes precedence
  const baseNameArg = getArg('--base-name');
  if (baseNameArg) return baseNameArg;

  // Fall back to .azure/catan-azure.json
  const projectRoot = join(__dirname, '..', '..', '..');
  const configPath = join(projectRoot, '.azure', 'catan-azure.json');
  const cfg = readBaseNameFromConfig(configPath);

  if (cfg?.baseName) return cfg.baseName;

  console.error('Error: baseName not found.');
  console.error('Provide --base-name <name> or add "baseName" to .azure/catan-azure.json.');
  process.exit(1);
}

// ── Console reporter ─────────────────────────────────────────────────────────

/** Status icons for terminal display. */
const STATUS_ICONS: Record<CheckStatus, string> = {
  pending: '  ',
  running: '...',
  ok: ' \u2705',
  fixing: ' \uD83D\uDD27',
  warning: ' \u26A0\uFE0F',
  error: ' \u274C',
};

/**
 * Reports check progress to the console.
 *
 * Transient states (running/fixing) overwrite the current line.
 * Terminal states (ok/error/warning) print a permanent line.
 * Domain headers (check name starts with ──) print a section separator.
 */
function consoleReporter(result: CheckResult): void {
  if (jsonOutput) return;

  // Domain separator headers
  if (result.check.startsWith('──')) {
    process.stdout.write('\r\x1b[K');
    console.log(`\n  \x1b[36m${result.check}\x1b[0m`);
    return;
  }

  const icon = STATUS_ICONS[result.status] ?? '  ';
  const duration = result.durationMs != null ? `${result.durationMs}ms` : '';
  const detail = result.detail ?? '';
  const error = result.error ? `\n       ${result.error}` : '';

  if (result.status === 'running' || result.status === 'fixing') {
    process.stdout.write(`\r\x1b[K  ${icon} ${result.check.padEnd(22)} ${detail}`);
  } else {
    process.stdout.write('\r\x1b[K');
    console.log(`  ${icon} ${result.check.padEnd(22)} ${duration.padStart(8)}  ${detail}${error}`);
  }
}

// ── Main ─────────────────────────────────────────────────────────────────────

async function main() {
  const baseName = loadBaseName();
  const credential = new DefaultAzureCredential();
  const config = resolveAzureConfig(baseName, staging);

  // Discover subscription ID from credential
  if (!jsonOutput) {
    const envLabel = staging ? 'staging' : 'production';
    console.log(`\nAzure Doctor (${envLabel})`);
    console.log('='.repeat(40));
    console.log(`  Base Name:       ${baseName}`);
    console.log(`  Resource Group:  ${config.resourceGroup}`);
    console.log(`  Auto-fix:        ${noFix ? 'OFF' : 'ON'}`);
    process.stdout.write('  Subscription:    discovering...');
  }

  try {
    config.subscriptionId = await getDefaultSubscriptionId();
  } catch (err) {
    if (!jsonOutput) {
      process.stdout.write('\r\x1b[K');
      console.error('  Subscription:    FAILED');
      console.error(`\n${err instanceof Error ? err.message : err}`);
    } else {
      console.log(JSON.stringify({ error: err instanceof Error ? err.message : String(err) }));
    }
    process.exit(2);
  }

  if (!jsonOutput) {
    process.stdout.write(`\r\x1b[K  Subscription:    ${config.subscriptionId}\n`);
    console.log(`  Cosmos Account:  ${config.cosmosAccountName}`);
    console.log(`  GameService:     ${config.gameServiceAppName}`);
    console.log(`  UI:              ${config.uiAppName}`);
  }

  try {
    const result = await runAzureDoctor(credential, config, consoleReporter, !noFix);

    if (jsonOutput) {
      console.log(JSON.stringify(result, null, 2));
    } else {
      // Summary
      console.log('\n' + '='.repeat(40));
      for (const domain of result.domains) {
        const icon = domain.healthy ? '\u2705' : '\u274C';
        const failed = domain.checks.filter((c) => c.status === 'error').length;
        const warned = domain.checks.filter((c) => c.status === 'warning').length;
        const suffix =
          failed > 0 ? ` (${failed} failed)` : warned > 0 ? ` (${warned} warnings)` : '';
        console.log(`  ${icon} ${domain.domain.padEnd(16)} ${domain.name}${suffix}`);
      }
      console.log('');
      if (result.allPassed) {
        console.log('\u2705 All checks passed.');
      } else {
        const totalFailed = result.domains.reduce(
          (n, d) => n + d.checks.filter((c) => c.status === 'error').length,
          0
        );
        console.log(
          `\u274C ${totalFailed} check(s) failed across ${result.domains.filter((d) => !d.healthy).length} domain(s).`
        );
      }
    }

    process.exit(result.allPassed ? 0 : 1);
  } catch (err) {
    if (!jsonOutput) {
      console.error('\nFatal error:', err instanceof Error ? err.message : err);
      console.error('\nMake sure you are logged in: az login');
    } else {
      console.log(JSON.stringify({ error: err instanceof Error ? err.message : String(err) }));
    }
    process.exit(2);
  }
}

main();
