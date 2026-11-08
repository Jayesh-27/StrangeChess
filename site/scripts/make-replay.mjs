// Builds src/data/match.json from the cutechess PGN in scripts/matches.pgn:
// the overall score plus one StrangeChess win (the shortest one ending in checkmate) as a replayable game.
// Run: npm run replay
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { Chess } from 'chess.js';

const pgn = readFileSync(new URL('./matches.pgn', import.meta.url), 'utf8');
const games = pgn.trim().split(/\r?\n(?=\[Event )/);

const tagOf = (text, key) => {
  const line = text.split(/\r?\n/).find((l) => l.startsWith(`[${key} "`));
  return line ? line.slice(key.length + 3, line.lastIndexOf('"')) : undefined;
};

let wins = 0, draws = 0, losses = 0, finished = 0, best = null;
for (const text of games) {
  const white = tagOf(text, 'White'), result = tagOf(text, 'Result');
  if (result === '*') continue; // game cut off before it ended
  finished++;
  if (result === '1/2-1/2') { draws++; continue; }
  const scWon = (white === 'StrangeChess' && result === '1-0') || (white !== 'StrangeChess' && result === '0-1');
  if (!scWon) { losses++; continue; }
  wins++;
  const chess = new Chess();
  chess.loadPgn(text);
  const length = chess.history().length;
  if (chess.isCheckmate() && (!best || length < best.length)) best = { text, white, black: tagOf(text, 'Black'), result, length };
}

// 64 chars, a1..h8, same format as the engine's BoardString()
const toBoard = (c) => {
  const out = [];
  for (let r = 1; r <= 8; r++) for (const f of 'abcdefgh') {
    const p = c.get(f + r);
    out.push(p ? (p.color === 'w' ? p.type.toUpperCase() : p.type) : '.');
  }
  return out.join('');
};

const game = new Chess();
game.loadPgn(best.text);
const replay = new Chess();
const plies = [{ san: '', from: '', to: '', board: toBoard(replay) }];
for (const m of game.history({ verbose: true })) {
  replay.move(m.san);
  plies.push({ san: m.san, from: m.from, to: m.to, board: toBoard(replay) });
}

mkdirSync(new URL('../src/data/', import.meta.url), { recursive: true });
writeFileSync(new URL('../src/data/match.json', import.meta.url), JSON.stringify({
  opponent: 'Stockfish 18 (UCI_LimitStrength on, UCI_Elo 2000, 1 thread)',
  timeControl: '60 seconds per game plus 0.6 seconds per move',
  games: finished, wins, draws, losses,
  replay: { white: best.white, black: best.black, result: best.result, plies },
}));
console.log({ games: finished, wins, draws, losses, replayPlies: plies.length - 1 });
