/**
 * Get or create a persistent dev player ID from localStorage.
 * Used by both the game page and phone-control page to identify this browser session.
 */
export function getDevPlayerId(): string {
  if (typeof window === 'undefined') return 'dev-player';

  // Check localStorage for existing ID
  const stored = localStorage.getItem('catan-dev-player-id');
  if (stored) return stored;

  // Generate new ID
  const newId = `player-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  localStorage.setItem('catan-dev-player-id', newId);
  return newId;
}
