import './styles.css';
import { setupHero } from './hero';
import { setupPlay, fmtNps } from './play';
import { setupAlphaBeta, setupBitboard } from './viz';
import { setupReplay, match } from './replay';

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;

setupHero($<HTMLCanvasElement>('hero-canvas'));

// Peak browser NPS seen this visit, shown in the stats strip.
let peak = 0;
setupPlay((nps) => {
  if (nps > peak) {
    peak = nps;
    $('stat-browser-nps').textContent = fmtNps(peak);
  }
});

setupAlphaBeta($('ab-host'), $('ab-caption'));
setupBitboard($('bb-host'), $('bb-out'));
setupReplay($('replay-board'), $('replay-caption'), {
  prev: $<HTMLButtonElement>('replay-prev'),
  play: $<HTMLButtonElement>('replay-play'),
  next: $<HTMLButtonElement>('replay-next'),
});

// Match numbers come straight from the generated match.json.
const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
$('m-headline').textContent = `${count(match.wins, 'win', 'wins')}, ${count(match.draws, 'draw', 'draws')} and ${count(match.losses, 'loss', 'losses')} against Stockfish at 2000 Elo`;
$('m-games').textContent = String(match.games);
{
  const r = match.replay;
  const color = r.black === 'StrangeChess' ? 'black' : 'white';
  const last = r.plies[r.plies.length - 1].san;
  const piece = ({ K: 'king', Q: 'queen', R: 'rook', B: 'bishop', N: 'knight' } as Record<string, string>)[last[0]] ?? 'pawn';
  $('replay-note').textContent = `This replay is one of the ${match.wins} wins. StrangeChess plays ${color} and finishes with a ${piece} checkmate.`;
}
$('m-wins').textContent = String(match.wins);
$('m-draws').textContent = String(match.draws);
$('m-losses').textContent = String(match.losses);
$('m-opponent').textContent = match.opponent;
$('m-tc').textContent = match.timeControl;
for (const [k, v] of [['wins', match.wins], ['draws', match.draws], ['losses', match.losses]] as const) {
  $(`bar-${k}`).style.flexGrow = String(v);
}

// Count-up numbers and fade-in sections when they scroll into view.
const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const io = new IntersectionObserver((entries) => {
  for (const e of entries) {
    if (!e.isIntersecting) continue;
    io.unobserve(e.target);
    const t = e.target as HTMLElement;
    t.classList.add('in');
    const target = Number(t.dataset.count);
    if (!Number.isNaN(target) && t.dataset.count !== undefined) {
      const decimals = Number(t.dataset.decimals ?? 0);
      const suffix = t.dataset.suffix ?? '';
      if (reduced) { t.textContent = target.toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals }) + suffix; continue; }
      const t0 = performance.now();
      const step = (now: number) => {
        const k = Math.min(1, (now - t0) / 1400);
        const v = target * (1 - Math.pow(1 - k, 3));
        t.textContent = v.toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals }) + suffix;
        if (k < 1) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
    }
  }
}, { threshold: 0.3 });
document.querySelectorAll('[data-count], .reveal').forEach((n) => io.observe(n));
