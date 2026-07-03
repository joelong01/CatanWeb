import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';
import { HarborCaptureArcs } from '../GameBoard';

/**
 * Harbor capture hint geometry (#201).
 *
 * For an unowned harbor we draw two arcs from the marker circle (center
 * (50, 43.3), r 26) to the two settlement corners of the harbor's edge, each
 * ending in a target dot. Every hex corner is distance 50 from center, so an
 * arc starts at lerp(center, vertex, 26/50 = 0.52) and ends at the vertex.
 */

afterEach(cleanup);

function renderArcs(side: 'Top' | 'TopRight') {
  const { container } = render(
    <svg>
      <HarborCaptureArcs side={side} />
    </svg>
  );
  const paths = Array.from(container.querySelectorAll('path'));
  const dots = Array.from(container.querySelectorAll('circle'));
  return { paths, dots };
}

function startOf(d: string): [number, number] {
  const m = d.match(/^M\s+([\d.-]+)\s+([\d.-]+)/);
  if (!m) throw new Error(`no move command in "${d}"`);
  return [Number(m[1]), Number(m[2])];
}

describe('HarborCaptureArcs (#201)', () => {
  it('draws two arcs and two target dots', () => {
    const { paths, dots } = renderArcs('Top');
    expect(paths.length).toBe(2);
    expect(dots.length).toBe(2);
  });

  it('places target dots on the two edge vertices (Top → (25,86.6),(75,86.6))', () => {
    const { dots } = renderArcs('Top');
    const centers = dots.map((c) => [Number(c.getAttribute('cx')), Number(c.getAttribute('cy'))]);
    expect(centers).toEqual([
      [25, 86.6],
      [75, 86.6],
    ]);
  });

  it('starts each arc on the circle perimeter and ends at the vertex', () => {
    const { paths } = renderArcs('Top');

    // Arc 0 → vertex (25, 86.6): start = lerp((50,43.3),(25,86.6),0.52) = (37, 65.8)
    const [s0x, s0y] = startOf(paths[0].getAttribute('d')!);
    expect(s0x).toBeCloseTo(37, 1);
    expect(s0y).toBeCloseTo(65.8, 1);
    expect(paths[0].getAttribute('d')).toMatch(/25 86\.6$/);

    // Arc 1 → vertex (75, 86.6): start = (63, 65.8)
    const [s1x, s1y] = startOf(paths[1].getAttribute('d')!);
    expect(s1x).toBeCloseTo(63, 1);
    expect(s1y).toBeCloseTo(65.8, 1);
    expect(paths[1].getAttribute('d')).toMatch(/75 86\.6$/);
  });

  it('handles a non-symmetric (diagonal) side', () => {
    const { paths, dots } = renderArcs('TopRight');
    expect(paths.length).toBe(2);
    const centers = dots.map((c) => [Number(c.getAttribute('cx')), Number(c.getAttribute('cy'))]);
    // SIDE_TO_VERTICES.TopRight = [[0,43.3],[25,86.6]]
    expect(centers).toEqual([
      [0, 43.3],
      [25, 86.6],
    ]);
  });
});
