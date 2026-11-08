// Geometric piece set drawn for this site (viewBox 0 0 100 100).
const BASE = 'M24 84h52v8H24z M30 76h40l4 8H26z';

const SHAPES: Record<string, string> = {
  p: 'M50 20a13 13 0 1 1 0 26a13 13 0 1 1 0-26z M39 48h22l7 28H32z',
  r: 'M30 22h8v7h7v-7h10v7h7v-7h8v15l-6 5v34H36V42l-6-5z',
  n: 'M34 76c0-15 5-21 14-29c-7 1-13 3-17 7c-5-1-8-6-5-11c7-7 11-15 20-21l2-9 7 7c13 5 19 17 19 32c0 10-2 17-4 24z',
  b: 'M50 8a6 6 0 1 1 0 12a6 6 0 1 1 0-12z M50 22c13 10 19 22 15 35c-2 7-8 11-15 11s-13-4-15-11c-4-13 2-25 15-35z M37 68h26l4 8H33z',
  q: 'M20 32L34 58L38 26L46 54L50 18L54 54L62 26L66 58L80 32L71 76H29Z',
  k: 'M46 6h8v8h8v8h-8v9h-8v-9h-8v-8h8z M50 33c17 0 25 9 23 19c-2 9-6 15-8 24H35c-2-9-6-15-8-24c-2-10 6-19 23-19z',
};

// Small details cut out of the silhouette (drawn in the "cut" colour).
const CUTS: Record<string, string> = {
  b: 'M55 34L45 46',
  n: 'M54 30a3 3 0 1 1 0 6a3 3 0 1 1 0-6z',
  q: 'M20 32m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0 M38 26m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0 M50 18m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0 M62 26m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0 M80 32m-4 0a4 4 0 1 0 8 0a4 4 0 1 0-8 0',
};

export function pieceSvg(symbol: string): string {
  const type = symbol.toLowerCase();
  const white = symbol !== type;
  const cls = white ? 'pc pc-w' : 'pc pc-b';
  const balls = type === 'q' ? `<path class="pc-fill" d="${CUTS.q}"/>` : '';
  const cut = CUTS[type] && type !== 'q' ? `<path class="pc-cut" d="${CUTS[type]}"/>` : '';
  return `<svg class="${cls}" viewBox="0 0 100 100" aria-hidden="true"><path class="pc-fill" d="${SHAPES[type]} ${BASE}"/>${balls}${cut}</svg>`;
}

export const PIECE_NAMES: Record<string, string> = {
  p: 'pawn', n: 'knight', b: 'bishop', r: 'rook', q: 'queen', k: 'king',
};
