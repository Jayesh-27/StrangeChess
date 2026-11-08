// Two small explainers: an alpha-beta search animation and a magic-bitboard lookup you can poke.

const SVG = 'http://www.w3.org/2000/svg';
const el = <K extends keyof SVGElementTagNameMap>(tag: K, attrs: Record<string, string | number>) => {
  const e = document.createElementNS(SVG, tag);
  for (const k in attrs) e.setAttribute(k, String(attrs[k]));
  return e;
};

// ---------------------------------------------------------------- alpha-beta

type Node = { id: number; max: boolean; kids: Node[]; leaf?: number; x: number; y: number; parent?: Node };
type Step = { kind: 'visit' | 'value' | 'prune'; node: Node; value?: number; alpha?: number; beta?: number };

// Root (max) -> 3 min nodes -> 3 max nodes each -> 2 leaves each.
const LEAVES = [3, 5, 6, 9, 1, 2, 0, -1, 4, 8, 5, 7, 7, 4, 8, 2, 3, 6];

function buildTree(): { root: Node; all: Node[] } {
  const all: Node[] = [];
  let id = 0, leafI = 0;
  const make = (depth: number, max: boolean, parent?: Node): Node => {
    const n: Node = { id: id++, max, kids: [], x: 0, y: 34 + depth * 78, parent };
    all.push(n);
    if (depth === 3) n.leaf = LEAVES[leafI++];
    else for (let i = 0; i < (depth === 2 ? 2 : 3); i++) n.kids.push(make(depth + 1, !max, n));
    return n;
  };
  const root = make(0, true);
  // lay out leaves evenly, parents centered over children
  const leaves = all.filter((n) => n.leaf !== undefined);
  leaves.forEach((n, i) => (n.x = 30 + i * (660 / (leaves.length - 1))));
  const place = (n: Node): number => (n.kids.length ? (n.x = n.kids.map(place).reduce((a, b) => a + b) / n.kids.length) : n.x);
  place(root);
  return { root, all };
}

function alphaBeta(n: Node, alpha: number, beta: number, steps: Step[]): number {
  steps.push({ kind: 'visit', node: n, alpha, beta });
  if (n.leaf !== undefined) { steps.push({ kind: 'value', node: n, value: n.leaf }); return n.leaf; }
  let best = n.max ? -Infinity : Infinity;
  for (let i = 0; i < n.kids.length; i++) {
    const v = alphaBeta(n.kids[i], alpha, beta, steps);
    if (n.max) { best = Math.max(best, v); alpha = Math.max(alpha, best); }
    else { best = Math.min(best, v); beta = Math.min(beta, best); }
    if (alpha >= beta) {
      for (const rest of n.kids.slice(i + 1)) steps.push({ kind: 'prune', node: rest });
      break;
    }
  }
  steps.push({ kind: 'value', node: n, value: best, alpha, beta });
  return best;
}

export function setupAlphaBeta(host: HTMLElement, caption: HTMLElement) {
  const { root, all } = buildTree();
  const steps: Step[] = [];
  alphaBeta(root, -Infinity, Infinity, steps);

  const svg = el('svg', { viewBox: '0 0 720 300', class: 'ab-svg', role: 'img', 'aria-label': 'Animated alpha-beta search tree' });
  const edges = el('g', {}), nodes = el('g', {});
  svg.append(edges, nodes);
  const edgeOf = new Map<number, SVGLineElement>();
  const shapeOf = new Map<number, SVGGElement>();
  for (const n of all) {
    if (n.parent) {
      const line = el('line', { x1: n.parent.x, y1: n.parent.y, x2: n.x, y2: n.y, class: 'ab-edge' });
      edges.append(line);
      edgeOf.set(n.id, line);
    }
    const g = el('g', { class: 'ab-node', transform: `translate(${n.x} ${n.y})` });
    const r = n.leaf !== undefined ? 13 : 17;
    g.append(n.max ? el('rect', { x: -r, y: -r, width: r * 2, height: r * 2, rx: 4 }) : el('circle', { r }));
    const t = el('text', { 'text-anchor': 'middle', dy: 5 });
    t.textContent = n.leaf !== undefined ? String(n.leaf) : '';
    g.append(t);
    nodes.append(g);
    shapeOf.set(n.id, g);
  }
  host.append(svg);

  const leafCount = all.filter((n) => n.leaf !== undefined).length;
  const subtree = (n: Node): Node[] => [n, ...n.kids.flatMap(subtree)];
  const fmt = (v?: number) => (v === undefined ? '' : v === Infinity ? '+∞' : v === -Infinity ? '−∞' : String(v));

  let i = 0, evaluated = 0, playing = false;
  function reset() {
    i = 0; evaluated = 0;
    for (const n of all) {
      shapeOf.get(n.id)!.setAttribute('class', 'ab-node');
      delete shapeOf.get(n.id)!.dataset.v;
      if (n.leaf === undefined) shapeOf.get(n.id)!.querySelector('text')!.textContent = '';
      edgeOf.get(n.id)?.setAttribute('class', 'ab-edge');
    }
  }
  function step() {
    if (i >= steps.length) {
      // highlight the principal variation
      let n: Node | undefined = root;
      const val = steps[steps.length - 1].value;
      while (n) {
        shapeOf.get(n.id)!.classList.add('ab-pv');
        edgeOf.get(n.id)?.classList.add('ab-pv');
        n = n.kids.find((k) => shapeOf.get(k.id)!.dataset.v === String(val) && !shapeOf.get(k.id)!.classList.contains('ab-pruned'));
      }
      caption.textContent = `Best score ${val}. Evaluated ${evaluated} of ${leafCount} leaves; ${leafCount - evaluated} were never needed.`;
      return false;
    }
    const s = steps[i++];
    const g = shapeOf.get(s.node.id)!;
    if (s.kind === 'visit') {
      g.classList.add('ab-visit');
      edgeOf.get(s.node.id)?.classList.add('ab-visit');
      caption.textContent = `${s.node.leaf !== undefined ? 'Evaluate leaf' : s.node.max ? 'Max node' : 'Min node'}  α = ${fmt(s.alpha)}  β = ${fmt(s.beta)}`;
    } else if (s.kind === 'value') {
      if (s.node.leaf !== undefined) evaluated++;
      g.dataset.v = String(s.value);
      g.querySelector('text')!.textContent = fmt(s.value);
      g.classList.add('ab-done');
    } else {
      for (const n of subtree(s.node)) {
        shapeOf.get(n.id)!.classList.add('ab-pruned');
        edgeOf.get(n.id)?.classList.add('ab-pruned');
      }
      caption.textContent = "α ≥ β: this branch can't change the result, so it is skipped.";
    }
    return true;
  }

  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduced) { while (step()); return; }

  let timer = 0;
  const run = () => {
    if (!playing) return;
    if (step()) timer = window.setTimeout(run, 420);
    else timer = window.setTimeout(() => { reset(); run(); }, 3500);
  };
  new IntersectionObserver(([e]) => {
    playing = e.isIntersecting;
    clearTimeout(timer);
    if (playing) run();
  }, { threshold: 0.4 }).observe(svg);
}

// ---------------------------------------------------------------- magic bitboards

const M64 = (1n << 64n) - 1n;
const ROOK_SQ = 27; // d4
const ROOK_MAGIC = 0x20c100180080280n; // Board.rookMagics[27] in the engine
const ROOK_BITS = 10; // Board.rookBlockerBitCounts[27]
const DIRS = [[1, 0], [-1, 0], [0, 1], [0, -1]];

function rookMask(sq: number): bigint {
  let m = 0n;
  const f0 = sq % 8, r0 = Math.floor(sq / 8);
  for (const [df, dr] of DIRS) {
    let f = f0 + df, r = r0 + dr;
    while (f + df >= 0 && f + df <= 7 && r + dr >= 0 && r + dr <= 7) { m |= 1n << BigInt(r * 8 + f); f += df; r += dr; }
  }
  return m;
}

function rookAttacks(sq: number, occ: bigint): bigint {
  let a = 0n;
  const f0 = sq % 8, r0 = Math.floor(sq / 8);
  for (const [df, dr] of DIRS) {
    let f = f0 + df, r = r0 + dr;
    while (f >= 0 && f <= 7 && r >= 0 && r <= 7) {
      const b = 1n << BigInt(r * 8 + f);
      a |= b;
      if (occ & b) break;
      f += df; r += dr;
    }
  }
  return a;
}

const hex = (b: bigint) => '0x' + b.toString(16).toUpperCase().padStart(16, '0');

export function setupBitboard(host: HTMLElement, out: HTMLElement) {
  const mask = rookMask(ROOK_SQ);
  let occ = (1n << 51n) | (1n << 25n) | (1n << 13n) | (1n << 30n); // d7, b4, f2, g4
  const grid = document.createElement('div');
  grid.className = 'bb-grid';
  const cells: HTMLButtonElement[] = [];
  for (let cell = 0; cell < 64; cell++) {
    const idx = (7 - Math.floor(cell / 8)) * 8 + (cell % 8);
    const b = document.createElement('button');
    b.type = 'button';
    b.dataset.idx = String(idx);
    b.setAttribute('aria-label', 'abcdefgh'[idx % 8] + (Math.floor(idx / 8) + 1));
    b.onclick = () => {
      if (idx === ROOK_SQ) return;
      occ ^= 1n << BigInt(idx);
      draw();
    };
    cells.push(b);
    grid.append(b);
  }
  host.append(grid);

  function draw() {
    const attacks = rookAttacks(ROOK_SQ, occ);
    const relevant = occ & mask;
    const index = Number((((relevant * ROOK_MAGIC) & M64) >> BigInt(64 - ROOK_BITS)));
    for (const b of cells) {
      const idx = Number(b.dataset.idx);
      const bit = 1n << BigInt(idx);
      b.className = 'bb-cell';
      if (idx === ROOK_SQ) { b.classList.add('bb-rook'); b.textContent = 'R'; continue; }
      b.textContent = occ & bit ? '●' : attacks & bit ? '1' : '0';
      if (attacks & bit) b.classList.add('bb-att');
      if (occ & bit) b.classList.add('bb-occ');
      if (mask & bit) b.classList.add('bb-mask');
    }
    out.innerHTML =
      `<div><span>blockers &amp; mask</span><code>${hex(relevant)}</code></div>` +
      `<div><span>× magic</span><code>${hex(ROOK_MAGIC)}</code></div>` +
      `<div><span>&gt;&gt; ${64 - ROOK_BITS} → table index</span><code>${index}</code></div>` +
      `<div class="bb-res"><span>attacks (one lookup)</span><code>${hex(attacks)}</code></div>`;
  }
  draw();
}
