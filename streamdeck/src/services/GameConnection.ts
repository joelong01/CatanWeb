import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type IRetryPolicy,
  type RetryContext,
} from "@microsoft/signalr";
// @ts-expect-error - ws has no type declarations; resolved at bundle time
import WebSocket from "ws";
import { NativeFetchHttpClient } from "./NativeFetchHttpClient";
import type { PluginGameState } from "../models/types";

export type ConnectionState =
  | "disconnected"
  | "connecting"
  | "connected"
  | "reconnecting";

/** Exponential backoff capped at 30s, retries indefinitely. */
const retryPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds(ctx: RetryContext): number {
    return Math.min(1000 * Math.pow(2, ctx.previousRetryCount), 30_000);
  },
};

/**
 * Manages the SignalR connection to GameService and sends REST commands.
 * Singleton — shared across all Stream Deck actions.
 */
export class GameConnection {
  private connection: HubConnection | null = null;
  private serviceUrl: string;
  private gameId: string | null = null;
  private playerId: string = "";
  private connecting: Promise<void> | null = null;

  // Callbacks
  onGameStateUpdated: ((state: PluginGameState) => void) | null = null;
  onConnectionStateChanged: ((state: ConnectionState) => void) | null = null;

  constructor(serviceUrl: string) {
    this.serviceUrl = serviceUrl;
  }

  get isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  get currentGameId(): string | null {
    return this.gameId;
  }

  setPlayerId(id: string): void {
    this.playerId = id;
  }

  /** Connect to the SignalR hub. Retries initial connection indefinitely. */
  async connect(): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) return;
    if (this.connecting) return this.connecting;

    this.connecting = this.doConnect();
    try {
      await this.connecting;
    } finally {
      this.connecting = null;
    }
  }

  private async doConnect(): Promise<void> {
    this.notify("connecting");

    // Pass custom httpClient + WebSocket to bypass SignalR's dynamic require()
    // calls (tough-cookie, node-fetch, ws) which crash in ESM bundles.
    const options = {
      withCredentials: false,
      httpClient: new NativeFetchHttpClient(),
      WebSocket,
    };
    this.connection = new HubConnectionBuilder()
      .withUrl(
        `${this.serviceUrl}/gamehub`,
        options as Parameters<HubConnectionBuilder["withUrl"]>[1],
      )
      .withAutomaticReconnect(retryPolicy)
      .configureLogging(LogLevel.Warning)
      .build();

    this.setupHandlers();
    await this.startWithRetry();
  }

  private async startWithRetry(): Promise<void> {
    let attempt = 0;
    while (true) {
      try {
        await this.connection!.start();
        this.notify("connected");
        return;
      } catch {
        attempt++;
        const delay = Math.min(1000 * Math.pow(2, attempt), 30_000);
        await new Promise((r) => setTimeout(r, delay));
      }
    }
  }

  private setupHandlers(): void {
    const conn = this.connection!;

    conn.on("GameStateUpdated", (gameModel: PluginGameState) => {
      this.onGameStateUpdated?.(gameModel);
    });

    conn.onreconnecting(() => this.notify("reconnecting"));

    conn.onreconnected(async () => {
      this.notify("connected");
      if (this.gameId) {
        try {
          await conn.invoke("JoinGame", this.gameId, this.playerId);
        } catch {
          // Will retry on next reconnect
        }
      }
    });

    conn.onclose(() => this.notify("disconnected"));
  }

  /** Disconnect and clean up. */
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.gameId = null;
      this.notify("disconnected");
    }
  }

  /** Switch to a different GameService URL. */
  async switchServer(newUrl: string): Promise<void> {
    await this.disconnect();
    this.serviceUrl = newUrl;
    await this.connect();
    // Re-join game if we had one
    if (this.gameId) {
      await this.joinGame(this.gameId);
    }
  }

  /** Join a game to receive SignalR updates. */
  async joinGame(gameId: string): Promise<void> {
    if (!this.isConnected) return;
    await this.connection!.invoke("JoinGame", gameId, this.playerId);
    this.gameId = gameId;
  }

  /** Leave the current game. */
  async leaveGame(): Promise<void> {
    if (this.gameId && this.isConnected) {
      await this.connection!.invoke("LeaveGame", this.gameId, this.playerId);
      this.gameId = null;
    }
  }

  // ── REST Commands ──────────────────────────────────────────────────

  private async sendCommand(
    messageType: string,
    messageJson: string,
  ): Promise<void> {
    if (!this.gameId) return;
    const url = `${this.serviceUrl}/api/game/${this.gameId}/command`;
    await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        messageType,
        messageJson,
        playerId: this.playerId,
      }),
    });
  }

  async sendRoll(die1: number, die2: number): Promise<void> {
    await this.sendCommand(
      "RollMessage",
      JSON.stringify({ Roll: { Die1: die1, Die2: die2 } }),
    );
  }

  async sendNext(): Promise<void> {
    await this.sendCommand("NextMessage", "{}");
  }

  async sendUndo(): Promise<void> {
    await this.sendCommand("UndoMessage", "{}");
  }

  async sendRedo(): Promise<void> {
    await this.sendCommand("RedoMessage", "{}");
  }

  async sendPurchase(entitlement: string): Promise<void> {
    await this.sendCommand(
      "PurchaseMessage",
      JSON.stringify({ Entitlement: entitlement }),
    );
  }

  private notify(state: ConnectionState): void {
    this.onConnectionStateChanged?.(state);
  }
}
