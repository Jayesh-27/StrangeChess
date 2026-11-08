import { BoardView, squareIndex } from './boardview';
import type { FromWorker, ToWorker, PositionState } from './engine.worker';

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
const fmtInt = (n: number) => Math.round(n).toLocaleString('en-US');
export const fmtNps = (n: number) =>
  n >= 1e6 ? (n / 1e6).toFixed(2) + 'M' : n >= 1e3 ? (n / 1e3).toFixed(0) + 'k' : Math.round(n).toString();

const MATE = 9000000;
function fmtScore(whiteScore: number): string {
  if (Math.abs(whiteScore) > MATE) {
    const plies = 9999999 - Math.abs(whiteScore);
    return (whiteScore > 0 ? '#' : '-#') + Math.max(1, Math.ceil(plies / 2));
  }
  const p = whiteScore / 100;
  return (p > 0 ? '+' : '') + p.toFixed(2);
}

export function setupPlay(onNps: (nps: number) => void) {
  const worker = new Worker(new URL('./engine.worker.ts', import.meta.url), { type: 'module' });
  const send = (m: ToWorker) => worker.postMessage(m);
  const view = new BoardView($('play-board'), true);

  const status = $('play-status');
  const log = $('console');
  const moveList = $('moves');
  const thinkSel = $<HTMLSelectElement>('think-ms');
  const btnNew = $<HTMLButtonElement>('btn-new');
  const btnUndo = $<HTMLButtonElement>('btn-undo');
  const btnSide = $<HTMLButtonElement>('btn-side');

  let ready = false;
  let thinking = false;
  let moves: string[] = [];
  let human: 'w' | 'b' = 'w';
  let pos: PositionState = { board: '', legal: [], inCheck: false };
  let selected: string | null = null;
  let gameOver = false;

  const sideToMove = () => (moves.length % 2 === 0 ? 'w' : 'b');
  const engineWhite = () => human === 'b';

  function line(text: string, cls = '') {
    const div = document.createElement('div');
    if (cls) div.className = cls;
    div.textContent = text;
    log.appendChild(div);
    while (log.childElementCount > 80) log.firstElementChild!.remove();
    log.scrollTop = log.scrollHeight;
  }

  function draw() {
    const last = moves.length ? moves[moves.length - 1] : null;
    let check: string | null = null;
    if (pos.inCheck) {
      const king = sideToMove() === 'w' ? 'K' : 'k';
      const i = pos.board.indexOf(king);
      check = 'abcdefgh'[i % 8] + (Math.floor(i / 8) + 1);
    }
    view.render(pos.board, {
      flipped: human === 'b',
      selected,
      targets: selected ? pos.legal.filter((m) => m.startsWith(selected!)).map((m) => m.slice(2, 4)) : [],
      lastMove: last ? [last.slice(0, 2), last.slice(2, 4)] : null,
      check,
    });
    moveList.innerHTML = '';
    for (let i = 0; i < moves.length; i += 2) {
      const li = document.createElement('li');
      li.textContent = `${moves[i]}${moves[i + 1] ? '  ' + moves[i + 1] : ''}`;
      moveList.appendChild(li);
    }
    moveList.scrollTop = moveList.scrollHeight;
    btnUndo.disabled = thinking || moves.length === 0;
    btnSide.disabled = thinking;
  }

  function setStatus(text: string) { status.textContent = text; }

  function afterState() {
    selected = null;
    if (pos.legal.length === 0) {
      gameOver = true;
      const loser = sideToMove();
      const msg = pos.inCheck
        ? (loser === human ? 'Checkmate. StrangeChess wins.' : 'Checkmate. You beat StrangeChess.')
        : 'Stalemate. Draw.';
      setStatus(msg);
      line(`# ${msg}`, 'ln-end');
      draw();
      return;
    }
    gameOver = false;
    draw();
    if (sideToMove() !== human) startThinking();
    else setStatus(pos.inCheck ? 'Your move. You are in check.' : 'Your move.');
  }

  function startThinking() {
    thinking = true;
    const ms = Number(thinkSel.value);
    setStatus(`StrangeChess is thinking (${ms / 1000} s)…`);
    line(`> go movetime ${ms}`, 'ln-cmd');
    draw();
    send({ type: 'think', moves, ms });
  }

  function evalBar(whiteScore: number) {
    const clamp = Math.abs(whiteScore) > MATE ? Math.sign(whiteScore) * 1000 : Math.max(-1000, Math.min(1000, whiteScore));
    $('eval-fill').style.height = `${50 + clamp / 20}%`;
    $('eval-live').textContent = fmtScore(whiteScore);
  }

  worker.onmessage = (e: MessageEvent<FromWorker>) => {
    const m = e.data;
    if (m.type === 'ready') {
      ready = true;
      $('engine-badge').textContent = `Engine loaded in ${(m.loadMs / 1000).toFixed(1)} s · WebAssembly (AOT)`;
      $('engine-badge').classList.add('ok');
      line(`> uci`, 'ln-cmd');
      line(`id name StrangeChess`);
      line(`id author Jayesh`);
      line(`uciok`);
      send({ type: 'state', moves });
    } else if (m.type === 'error') {
      setStatus('The engine hit an error. Reload the page to try again.');
      line(`error: ${m.message}`, 'ln-err');
      thinking = false;
    } else if (m.type === 'state') {
      pos = { board: m.board, legal: m.legal, inCheck: m.inCheck };
      afterState();
    } else if (m.type === 'info') {
      const white = engineWhite() ? m.score : -m.score;
      $('depth-live').textContent = String(m.depth);
      $('nodes-live').textContent = fmtNps(m.nodes);
      $('nps-live').textContent = fmtNps(m.nps);
      evalBar(white);
      onNps(m.nps);
      line(`info depth ${m.depth} score ${fmtScore(white)} nodes ${fmtInt(m.nodes)} nps ${fmtInt(m.nps)} pv ${m.best}`);
    } else if (m.type === 'bestmove') {
      thinking = false;
      line(`bestmove ${m.best}`, 'ln-best');
      if (m.best === '0000') { send({ type: 'state', moves }); return; }
      moves.push(m.best.slice(0, 4));
      send({ type: 'state', moves });
    }
  };

  view.onSquare = (sq) => {
    if (!ready || thinking || gameOver || sideToMove() !== human) return;
    const piece = pos.board[squareIndex(sq)];
    const own = piece !== '.' && (piece === piece.toUpperCase()) === (human === 'w');
    if (selected) {
      const move = pos.legal.find((m) => m.startsWith(selected!) && m.slice(2, 4) === sq);
      if (move) {
        moves.push(move);
        selected = null;
        send({ type: 'state', moves });
        return;
      }
    }
    selected = own && selected !== sq ? sq : null;
    draw();
  };

  function newGame() {
    if (thinking) return;
    moves = [];
    gameOver = false;
    send({ type: 'newgame' });
    line('> ucinewgame', 'ln-cmd');
    send({ type: 'state', moves });
  }

  btnNew.onclick = newGame;
  btnSide.onclick = () => {
    human = human === 'w' ? 'b' : 'w';
    btnSide.textContent = human === 'w' ? 'Play as black' : 'Play as white';
    newGame();
  };
  btnUndo.onclick = () => {
    if (thinking || moves.length === 0) return;
    moves.pop();
    if (sideToMove() !== human && moves.length) moves.pop();
    send({ type: 'state', moves });
  };

  setStatus('Loading the engine…');
  send({ type: 'init' });
}
