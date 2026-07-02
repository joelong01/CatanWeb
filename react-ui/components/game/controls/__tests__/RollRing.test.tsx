import { describe, it, expect, vi, afterEach, beforeAll } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/react';
import { RollRing } from '../RollRing';
import type { RollStats } from '@/lib/extensions';

// HexGrid (fitToParent) observes its parent; jsdom has no ResizeObserver.
beforeAll(() => {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});

/**
 * RollRing disabled-state behavior (#199).
 *
 * When rolling is not enabled the tiles must be non-interactive (no roll fires,
 * cursor is not-allowed) while the stats stay rendered. Enabled tiles roll on
 * click. HexTile marks an active click target with data-has-click="true".
 */

// Full 2-12 stats map so every tile renders.
const rollStats: Record<number, RollStats> = Object.fromEntries(
  Array.from({ length: 11 }, (_, i) => [i + 2, { count: i, percentage: i * 2 }])
) as Record<number, RollStats>;

afterEach(cleanup);

describe('RollRing roll gating (#199)', () => {
  it('fires onRollClick when enabled (first tile is roll 2)', () => {
    const onRollClick = vi.fn();
    const { container } = render(
      <RollRing rollStats={rollStats} onRollClick={onRollClick} rollsEnabled={true} />
    );

    const clickable = container.querySelectorAll('[data-has-click="true"]');
    expect(clickable.length).toBe(11);

    fireEvent.click(clickable[0]);
    expect(onRollClick).toHaveBeenCalledWith(2);
  });

  it('defaults to enabled when rollsEnabled is omitted', () => {
    const { container } = render(<RollRing rollStats={rollStats} onRollClick={vi.fn()} />);
    expect(container.querySelectorAll('[data-has-click="true"]').length).toBe(11);
  });

  it('does not fire onRollClick when disabled', () => {
    const onRollClick = vi.fn();
    const { container } = render(
      <RollRing rollStats={rollStats} onRollClick={onRollClick} rollsEnabled={false} />
    );

    // No tile is an active click target when disabled.
    expect(container.querySelectorAll('[data-has-click="true"]').length).toBe(0);

    // Clicking a disabled tile is a no-op.
    const disabledTiles = container.querySelectorAll('.cursor-not-allowed');
    expect(disabledTiles.length).toBeGreaterThan(0);
    fireEvent.click(disabledTiles[0]);
    expect(onRollClick).not.toHaveBeenCalled();
  });
});
