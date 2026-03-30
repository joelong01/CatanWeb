/**
 * @module api/azure/doctor
 *
 * Next.js API route for the Azure Doctor — runs infrastructure checks
 * server-side using the Azure ARM SDKs, streaming results to the browser
 * via Server-Sent Events (SSE).
 *
 * The browser obtains an Azure token via MSAL, passes it in the
 * Authorization header. This route wraps it in a TokenCredential and
 * calls the same `runAzureDoctor()` function used by the CLI.
 *
 * Security:
 *   - The Authorization header is NEVER logged
 *   - The token is held in memory only for the request duration
 *   - JWT exp claim is parsed for real expiration
 */

import { NextRequest } from 'next/server';
import { runAzureDoctor } from '@/lib/azure/azureDoctor';
import { createTokenCredential } from '@/lib/azure/tokenCredential';
import { resolveAzureConfig, discoverSubscriptionId } from '@/lib/azure/resolveConfig';
import type { CheckResult } from '@/lib/azure/types';

/**
 * POST /api/azure/doctor
 *
 * Streams Azure doctor check results as Server-Sent Events.
 * Expects Authorization: Bearer <token> header.
 * Optional query param: ?staging=true
 * Optional query param: ?fix=true (enables auto-fix, default off)
 */
export async function POST(req: NextRequest): Promise<Response> {
  // Extract token from Authorization header — do NOT log it
  const authHeader = req.headers.get('authorization');
  const token = authHeader?.startsWith('Bearer ') ? authHeader.slice(7) : null;

  if (!token) {
    return new Response(
      JSON.stringify({ error: 'Missing Authorization: Bearer <token> header' }),
      { status: 401, headers: { 'Content-Type': 'application/json' } }
    );
  }

  const staging = req.nextUrl.searchParams.get('staging') === 'true';
  const autoFix = req.nextUrl.searchParams.get('fix') === 'true';

  // Derive config from baseName (from env, injected by next.config.ts from .azure/catan-azure.json)
  const baseName = process.env.NEXT_PUBLIC_AZURE_BASE_NAME;
  if (!baseName) {
    return new Response(
      JSON.stringify({ error: 'baseName not configured. Add "baseName" to .azure/catan-azure.json.' }),
      { status: 500, headers: { 'Content-Type': 'application/json' } }
    );
  }

  const credential = createTokenCredential(token);
  const config = resolveAzureConfig(baseName, staging);

  // Discover subscription ID from the forwarded token
  try {
    config.subscriptionId = await discoverSubscriptionId(credential, config.resourceGroup);
  } catch (err) {
    return new Response(
      JSON.stringify({ error: `Could not discover subscription: ${err instanceof Error ? err.message : err}` }),
      { status: 500, headers: { 'Content-Type': 'application/json' } }
    );
  }

  // Create SSE stream
  const encoder = new TextEncoder();
  const stream = new ReadableStream({
    async start(controller) {
      /** Sends a check result as an SSE event. */
      const report = (result: CheckResult): void => {
        const data = JSON.stringify(result);
        controller.enqueue(encoder.encode(`event: check\ndata: ${data}\n\n`));
      };

      try {
        // Skip GitHub domain in web path — the forwarded MSAL token is scoped
        // to ARM only, not Graph API. GitHub OIDC checks don't break overnight anyway.
        const doctorResult = await runAzureDoctor(credential, config, report, autoFix, ['github']);

        // Send final summary event
        controller.enqueue(
          encoder.encode(`event: done\ndata: ${JSON.stringify(doctorResult)}\n\n`)
        );
      } catch (err) {
        const error = err instanceof Error ? err.message : String(err);
        controller.enqueue(
          encoder.encode(`event: error\ndata: ${JSON.stringify({ error })}\n\n`)
        );
      } finally {
        controller.close();
      }
    },
  });

  return new Response(stream, {
    headers: {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive',
    },
  });
}
