import { GameConnection } from "./GameConnection";
import { DEFAULT_SETTINGS } from "../models/types";

/** Singleton GameConnection instance shared across all actions. */
export const gameConnection = new GameConnection(DEFAULT_SETTINGS.localUrl);
