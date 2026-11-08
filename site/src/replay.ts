import { BoardView } from './boardview';
import match from './data/match.json';

// Auto-plays a real StrangeChess win from the cutechess run (src/data/match.json).
export function setupReplay(host: HTMLElement, caption: HTMLElement, controls: { prev: HTMLButtonElement; play: HTMLButtonElement; next: HTMLButtonElement }) {
  const view = new BoardView(host, false);
  const plies = match.replay.plies;
  const flipped = match.replay.black === 'StrangeChess';
  let i = 0, playing = false, inView = false, timer = 0;

  function draw() {
    const p = plies[i];
    view.render(p.board, { flipped, lastMove: p.from ? [p.from, p.to] : null });
    const moveNo = Math.ceil(i / 2);
    caption.textContent = i === 0 ? 'Start position' : `${moveNo}${i % 2 ? '.' : '…'} ${p.san}`;
    controls.play.textContent = playing ? 'Pause' : i === plies.length - 1 ? 'Replay' : 'Play';
  }
  function tick() {
    clearTimeout(timer);
    if (!playing || !inView) return;
    if (i < plies.length - 1) { i++; draw(); timer = window.setTimeout(tick, i === plies.length - 1 ? 0 : 650); }
    else { playing = false; draw(); }
  }
  controls.play.onclick = () => {
    if (i === plies.length - 1) i = 0;
    playing = !playing;
    draw();
    tick();
  };
  controls.prev.onclick = () => { playing = false; i = Math.max(0, i - 1); draw(); };
  controls.next.onclick = () => { playing = false; i = Math.min(plies.length - 1, i + 1); draw(); };

  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  new IntersectionObserver(([e]) => {
    inView = e.isIntersecting;
    if (inView && !reduced && i === 0) playing = true;
    draw();
    tick();
  }, { threshold: 0.35 }).observe(host);
  draw();
}

export { match };
