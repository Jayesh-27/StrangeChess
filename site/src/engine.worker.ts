// Runs the StrangeChess C# engine (compiled to WebAssembly) off the main thread.
// All chess rules come from the engine itself: legal moves, check, board state and search.

type Exports = {
  EngineExports: {
    Init(): void;
    NewGame(): void;
    SetPosition(moves: string): void;
    LegalMoves(): string;
    InCheck(): boolean;
    BoardString(): string;
    Search(depth: number, ms: number): string;
  };
};

export type ToWorker =
  | { type: 'init' }
  | { type: 'newgame' }
  | { type: 'state'; moves: string[] }
  | { type: 'think'; moves: string[]; ms: number };

export type PositionState = { board: string; legal: string[]; inCheck: boolean };

export type FromWorker =
  | { type: 'ready'; loadMs: number }
  | { type: 'error'; message: string }
  | ({ type: 'state' } & PositionState)
  | { type: 'info'; depth: number; score: number; nodes: number; nps: number; best: string; ms: number }
  | { type: 'bestmove'; best: string; depth: number; score: number; nodes: number; nps: number; ms: number };

let engine: Exports['EngineExports'] | null = null;
const post = (m: FromWorker) => postMessage(m);

async function init() {
  const t0 = performance.now();
  // Tells the .NET runtime it owns this worker. Without it, a worker that has an
  // onmessage handler is treated as a thread-pool helper and waits forever for the main thread.
  (globalThis as unknown as { dotnetSidecar: boolean }).dotnetSidecar = true;
  const url = new URL('/engine/_framework/dotnet.js', self.location.origin).href;
  const { dotnet } = await import(/* @vite-ignore */ url);
  const runtime = await dotnet.create();
  const config = runtime.getConfig();
  const exports = (await runtime.getAssemblyExports(config.mainAssemblyName)) as Exports;
  engine = exports.EngineExports;
  engine.Init();
  post({ type: 'ready', loadMs: performance.now() - t0 });
}

function state(moves: string[]): PositionState {
  engine!.SetPosition(moves.join(' '));
  const legal = engine!.LegalMoves();
  return { board: engine!.BoardString(), legal: legal ? legal.split(' ') : [], inCheck: engine!.InCheck() };
}

// The engine's GetBestMove(depth) runs iterative deepening up to `depth`.
// Calling it with depth 1, 2, 3... (the transposition table carries over between calls)
// lets us report progress after every depth instead of going silent until the end.
function think(moves: string[], budgetMs: number) {
  engine!.SetPosition(moves.join(' '));
  const t0 = performance.now();
  let totalNodes = 0, best = '0000', bestDepth = 0, bestScore = 0;

  for (let depth = 1; depth <= 63; depth++) {
    const remaining = budgetMs - (performance.now() - t0);
    if (remaining < 5) break;
    const [move, doneDepth, score, nodes] = engine!.Search(depth, remaining).split(' ');
    totalNodes += Number(nodes);
    const finished = Number(doneDepth) === depth;
    if (move !== '0000' && (finished || bestDepth === 0)) {
      best = move;
      bestDepth = Number(doneDepth);
      bestScore = Number(score);
    }
    const ms = performance.now() - t0;
    post({ type: 'info', depth: bestDepth, score: bestScore, nodes: totalNodes, nps: totalNodes / (ms / 1000), best, ms });
    if (!finished) break;
    if (Math.abs(bestScore) > 9000000) break; // forced mate found
  }

  const ms = performance.now() - t0;
  post({ type: 'bestmove', best, depth: bestDepth, score: bestScore, nodes: totalNodes, nps: totalNodes / (ms / 1000), ms });
}

self.onmessage = async (e: MessageEvent<ToWorker>) => {
  const msg = e.data;
  try {
    if (msg.type === 'init') await init();
    else if (msg.type === 'newgame') engine!.NewGame();
    else if (msg.type === 'state') post({ type: 'state', ...state(msg.moves) });
    else if (msg.type === 'think') think(msg.moves, msg.ms);
  } catch (err) {
    post({ type: 'error', message: err instanceof Error ? err.message : String(err) });
  }
};
