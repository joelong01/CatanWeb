/**
 * A SignalR HttpClient that uses Node 20's native fetch, bypassing
 * @microsoft/signalr's FetchHttpClient which dynamically require()s
 * tough-cookie / fetch-cookie / node-fetch — breaking ESM bundles.
 */
import {
  HttpClient,
  HttpResponse,
  type HttpRequest,
} from "@microsoft/signalr";

export class NativeFetchHttpClient extends HttpClient {
  override async send(request: HttpRequest): Promise<HttpResponse> {
    if (request.abortSignal?.aborted) {
      throw new Error("The operation was aborted.");
    }

    const controller = new AbortController();
    if (request.abortSignal) {
      request.abortSignal.onabort = () => controller.abort();
    }

    let timeoutId: ReturnType<typeof setTimeout> | null = null;
    if (request.timeout) {
      timeoutId = setTimeout(() => controller.abort(), request.timeout);
    }

    try {
      const response = await fetch(request.url!, {
        method: request.method,
        body: request.content as BodyInit | undefined,
        headers: {
          "X-Requested-With": "XMLHttpRequest",
          ...(request.headers ?? {}),
        },
        signal: controller.signal,
      });

      const content =
        request.responseType === "arraybuffer"
          ? await response.arrayBuffer()
          : await response.text();

      return new HttpResponse(response.status, response.statusText, content);
    } finally {
      if (timeoutId) clearTimeout(timeoutId);
      if (request.abortSignal) request.abortSignal.onabort = null;
    }
  }

  override getCookieString(_url: string): string {
    return "";
  }
}
