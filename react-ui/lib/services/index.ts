/**
 * Services barrel export.
 */

export {
  GameServiceProxy,
  getGameServiceProxy,
  type CommandResult,
  type GameInfo,
  type PlayerProfile,
  type ConnectionState,
  type GameServiceProxyEvents,
} from './GameServiceProxy';

export { serviceConfig, getServiceUrl, getHubUrl } from './config';
