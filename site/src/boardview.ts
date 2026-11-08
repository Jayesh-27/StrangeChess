import { pieceSvg, PIECE_NAMES } from './pieces';

export type BoardMarks = {
  flipped?: boolean;
  selected?: string | null;
  targets?: string[];
  lastMove?: [string, string] | null;
  check?: string | null;
};

const FILES = 'abcdefgh';
export const squareName = (i: number) => FILES[i % 8] + (Math.floor(i / 8) + 1);
export const squareIndex = (s: string) => (s.charCodeAt(1) - 49) * 8 + (s.charCodeAt(0) - 97);

// A 2D board. `board` is 64 chars a1..h8 ("PNBRQKpnbrqk" or ".") straight from the engine.
export class BoardView {
  readonly el: HTMLElement;
  private squares: HTMLButtonElement[] = [];
  onSquare: (sq: string) => void = () => {};

  constructor(host: HTMLElement, interactive: boolean) {
    this.el = document.createElement('div');
    this.el.className = 'board' + (interactive ? ' board-live' : '');
    this.el.setAttribute('role', 'grid');
    for (let i = 0; i < 64; i++) {
      const b = document.createElement('button');
      b.type = 'button';
      b.tabIndex = interactive ? 0 : -1;
      if (!interactive) b.disabled = true;
      b.addEventListener('click', () => this.onSquare(b.dataset.sq!));
      this.squares.push(b);
      this.el.appendChild(b);
    }
    host.appendChild(this.el);
  }

  render(board: string, marks: BoardMarks = {}) {
    const targets = new Set(marks.targets ?? []);
    for (let cell = 0; cell < 64; cell++) {
      const row = Math.floor(cell / 8), col = cell % 8;
      const idx = marks.flipped ? row * 8 + (7 - col) : (7 - row) * 8 + col;
      const sq = squareName(idx);
      const b = this.squares[cell];
      const piece = board[idx];
      const light = (Math.floor(idx / 8) + (idx % 8)) % 2 === 1;
      b.dataset.sq = sq;
      b.className = 'sq ' + (light ? 'sq-l' : 'sq-d');
      if (marks.selected === sq) b.classList.add('sq-sel');
      if (marks.lastMove && (marks.lastMove[0] === sq || marks.lastMove[1] === sq)) b.classList.add('sq-last');
      if (marks.check === sq) b.classList.add('sq-check');
      if (targets.has(sq)) b.classList.add(piece === '.' ? 'sq-dot' : 'sq-cap');
      const label = piece === '.' ? sq : `${sq}, ${piece === piece.toUpperCase() ? 'white' : 'black'} ${PIECE_NAMES[piece.toLowerCase()]}`;
      b.setAttribute('aria-label', label);
      const coords =
        (col === 0 ? `<span class="co co-r">${sq[1]}</span>` : '') +
        (row === 7 ? `<span class="co co-f">${sq[0]}</span>` : '');
      b.innerHTML = (piece === '.' ? '' : pieceSvg(piece)) + coords;
    }
  }
}
