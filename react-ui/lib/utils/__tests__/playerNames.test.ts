/**
 * Regression tests for player display-name resolution (issue #208).
 *
 * The bug: display names were produced by splitting the player ID on `-` and taking the
 * first segment. That silently worked for seeded `Joe-001` IDs and produced a GUID
 * fragment (`1ffb33af`) for anyone whose ID was a bare GUID.
 */

import { describe, it, expect } from 'vitest';
import {
  resolvePlayerName,
  resolveHistoricalName,
  PLAYER_NAME_LOADING,
  PLAYER_NAME_MISSING,
} from '../playerNames';
import { DEFAULT_PLAYER_COLORS, type PlayerProfile } from '@/types/player-profile';

const TY_GUID_ID = '1ffb33af-9316-4870-b7db-32346965ed8b';
const TY_NAMED_ID = 'Ty-1ffb33af-9316-4870-b7db-32346965ed8b';
const JOE_LEGACY_ID = 'Joe-001';

function profile(id: string, name: string): PlayerProfile {
  return { id, name, colors: { ...DEFAULT_PLAYER_COLORS } };
}

function profiles(...entries: PlayerProfile[]): Map<string, PlayerProfile> {
  return new Map(entries.map((p) => [p.id, p]));
}

describe('resolvePlayerName', () => {
  it('returns the profile name for a bare-GUID ID — the reported bug', () => {
    const map = profiles(profile(TY_GUID_ID, 'Ty'));
    expect(resolvePlayerName(map, TY_GUID_ID)).toBe('Ty');
  });

  it('never returns a segment of the ID', () => {
    const map = profiles(profile(TY_GUID_ID, 'Ty'));
    expect(resolvePlayerName(map, TY_GUID_ID)).not.toBe('1ffb33af');
  });

  it('returns the profile name for a legacy Name-NNN ID', () => {
    const map = profiles(profile(JOE_LEGACY_ID, 'Joe'));
    expect(resolvePlayerName(map, JOE_LEGACY_ID)).toBe('Joe');
  });

  it('prefers the profile over the ID prefix when they disagree', () => {
    // Renamed player: the ID still says "Joe", the profile says "Joseph".
    const map = profiles(profile(JOE_LEGACY_ID, 'Joseph'));
    expect(resolvePlayerName(map, JOE_LEGACY_ID)).toBe('Joseph');
  });

  it('shows Loading while profiles have not been fetched', () => {
    expect(resolvePlayerName(new Map(), TY_GUID_ID)).toBe(PLAYER_NAME_LOADING);
  });

  it('shows Profile Error when profiles are loaded but the ID is absent', () => {
    const map = profiles(profile(JOE_LEGACY_ID, 'Joe'));
    expect(resolvePlayerName(map, TY_GUID_ID)).toBe(PLAYER_NAME_MISSING);
  });
});

describe('resolveHistoricalName', () => {
  it('keeps the stored name — a completed game is a point-in-time record', () => {
    // Ty was called "Ty" when the game ended, and has since been renamed.
    const map = profiles(profile(TY_NAMED_ID, 'Tyler'));
    expect(resolveHistoricalName(map, TY_NAMED_ID, 'Ty')).toBe('Ty');
  });

  it('keeps the stored name for a renamed legacy player', () => {
    const map = profiles(profile(JOE_LEGACY_ID, 'Joseph'));
    expect(resolveHistoricalName(map, JOE_LEGACY_ID, 'Joe')).toBe('Joe');
  });

  it('repairs a stored name that is a bare-GUID ID fragment', () => {
    const map = profiles(profile(TY_GUID_ID, 'Ty'));
    expect(resolveHistoricalName(map, TY_GUID_ID, '1ffb33af')).toBe('Ty');
  });

  it('keeps a genuine stored name even when the ID is a bare GUID', () => {
    const map = profiles(profile(TY_GUID_ID, 'Tyler'));
    expect(resolveHistoricalName(map, TY_GUID_ID, 'Ty')).toBe('Ty');
  });

  it('resolves from the profile when nothing was stored', () => {
    const map = profiles(profile(TY_GUID_ID, 'Ty'));
    expect(resolveHistoricalName(map, TY_GUID_ID, undefined)).toBe('Ty');
  });

  /**
   * An earlier version of the repair rule compared the stored name against the current
   * profile name. Because new IDs are minted as `<Name>-<uuid>`, that fired on every
   * rename and replaced the correct historical name with the current one.
   */
  it('does not repair a name-prefixed ID whose owner was renamed', () => {
    const map = profiles(profile(TY_NAMED_ID, 'Tyler'));
    const stored = 'Ty';
    expect(stored).toBe(TY_NAMED_ID.split('-')[0]); // would have tripped the old rule
    expect(resolveHistoricalName(map, TY_NAMED_ID, stored)).toBe('Ty');
  });
});
