export interface PieceInfo {
  p: string;      // "P","N","B","R","Q","K" = white; lowercase = black
  left: number | null;
}

export interface HistoryEntry {
  side: string;
  type: string;   // "move" | "drop"
  uci?: string;
  piece?: string;
  square?: string;
}

export interface GameEvent {
  type: string;
  square?: string;
  piece?: string;
  side?: string;
}

export interface VariantConfig {
  pointsPerTurn: number;
  startingPoints: number;
  lifetimes: Record<string, number>;
  costs: Record<string, number>;
  dropRanks: Record<string, number[]>;
  pawnDropRanks: Record<string, number[]>;
  dropConsumesTurn: boolean;
  promotionResetsLifetime: boolean;
  maxTurns: number;
}

export interface GameState {
  mode: string;
  config: VariantConfig;
  board: (PieceInfo | null)[];
  turn: string;
  castling: string;
  enPassant: string | null;
  halfmoveClock: number;
  fullmove: number;
  turnNumber: number;
  points: Record<string, number>;
  history: HistoryEntry[];
  events: GameEvent[];
  status: string;
  winner: string | null;
  inCheck: boolean;
  legalMoves: string[];
  legalDrops: Record<string, string[]>;
}

export interface BotResponse {
  state: GameState;
  action: string;
}

export interface ErrorResponse {
  error: string;
}

export type Opponent = 'bot' | 'hotseat';
export type Difficulty = 'easy' | 'medium' | 'hard';
export type PlayerColor = 'white' | 'black';

export interface GameSettings {
  mode: 'classic' | 'variant';
  opponent: Opponent;
  playerColor: PlayerColor;
  difficulty: Difficulty;
}

export const DIFFICULTY_PRESETS: Record<Difficulty, { depth: number; timeMs: number }> = {
  easy:   { depth: 2, timeMs: 200 },
  medium: { depth: 4, timeMs: 500 },
  hard:   { depth: 6, timeMs: 1500 },
};

// Unicode piece glyphs
export const PIECE_GLYPHS: Record<string, string> = {
  K: '♔', Q: '♕', R: '♖', B: '♗', N: '♘', P: '♙',
  k: '♚', q: '♛', r: '♜', b: '♝', n: '♞', p: '♟',
};

// Square name ↔ index helpers
export function squareName(index: number): string {
  const file = String.fromCharCode(97 + (index % 8));
  const rank = Math.floor(index / 8) + 1;
  return `${file}${rank}`;
}

export function squareIndex(name: string): number {
  return (name.charCodeAt(1) - 49) * 8 + (name.charCodeAt(0) - 97);
}
