# StrangeChessWeb — Project Plan

You are an AI coding agent. Read this entire file before doing anything. It is the single source of truth.

## 1. Goal

Build a chess website (chess.com-like but small) powered by the owner's own C# chess engine, **StrangeChess** (~1900 Elo, bitboards, magic bitboards, Zobrist hashing, UCI-capable).

The site is for a personal project and a **game jam**. For the jam it has a custom **variant mode** (section 9) on top of normal chess ("classic mode").

**Scope right now:** single-player vs bot, and hot-seat (two humans on one screen). Deployed publicly on a free host.
**Not now:** database, accounts, multiplayer, Android, VR. Leave clean hooks for them (section 14) but do not build them.

## 2. HARD RULES (read twice)

1. **Only create/edit/delete files inside `C:\Users\jayes\Desktop\StrangeChessWeb`.**
2. `C:\Users\jayes\Desktop\StrangeChess` is the owner's original repo. You may **read** it and **copy files out of it**. You must **never** modify, delete, move, create files in, format, build in, or run `git` write commands in that folder. Do not run `dotnet build/run/test` with that folder as the working directory or as a project target (build outputs would be written there). Only read.
3. Do not add dependencies that are not listed here without asking the owner first.
4. Do not rewrite the engine's algorithms (`AI.cs`, `Board.cs`, `Chess.cs`, `Zobrist.cs`). The engine already works. Our variant needs **zero engine changes** (see section 6).
5. Work in the phases of section 13. **Stop at the end of each phase**, summarize what you did, list how the owner can verify it, and wait for "continue".
6. The owner uses **Windows + PowerShell**. Give commands in PowerShell syntax.
7. If something here is ambiguous, pick the stated default, note it in `README.md` under "Decisions", and keep going. Only ask a question if you are truly blocked.

## 3. Reference material (READ-ONLY): `C:\Users\jayes\Desktop\StrangeChess`

| Path | What it is | Use |
|---|---|---|
| `Engine\Engine\Board.cs, Chess.cs, AI.cs, Zobrist.cs, Perft.cs, Engine.csproj` | The engine source (namespace-less, global classes) | **Copy** into our repo (section 7) |
| `Engine\Engine\Program.cs` | UCI console loop with `Main` | Read for how the engine is started. **Do not copy** (it has its own entry point) |
| `backend\EngineGame.cs` | Owner's earlier working wrapper | **Read carefully.** It shows the correct way to call the engine: legality filtering, make/unmake with manual restoration, search call |
| `backend\Program.cs`, `backend\wwwroot\*` | Owner's earlier minimal API + vanilla JS lab UI | Read for ideas only. We are building a cleaner version |
| `Engine\StockFish\`, any `bin\`, `obj\` | Binaries / build output | Ignore completely |

## 4. Engine facts (verified from source — trust these, but re-check in code if a test fails)

- Engine classes are **global singletons**: `Board.Instance`, `Chess.Instance`, `AI.Instance`. **They are not thread-safe and there is only one game state per process.**
- Startup: create the three instances (`Board.Instance = new Board(); Chess.Instance = new Chess(); AI.Instance = new AI();`) then `Board.Instance.Initialize()` once (see `Program.cs` and `EngineGame` constructor).
- `Board.Instance.LoadFEN(fen)` fully resets the position: board, side to move (`isWhiteTurn`), `Chess.Instance.castlingRights`, `enPassantTarget`, halfmove/fullmove, Zobrist key, and repetition history. Standard 6-field FEN works; the FEN parser uses `pieceFromSymbol` and will throw on unknown characters.
- **Square index:** `a1 = 0`, `h1 = 7`, `a8 = 56`, `h8 = 63` (`index = rank0 * 8 + file0`).
- Moves are `ushort` packed (`from | to << 6 | flag << 12`). `Chess.Instance.FormatUciMove(move)` returns UCI text such as `e2e4`.
- `Chess.Instance.GenerateAllMoves(0)` fills `moveList[0]` / `moveCount[0]` with **pseudo-legal** moves. You must filter for legality: `movePiece` → check own king with `isSquareSafe(kingBitboard)` → `unmakeMove`, then **manually restore** `castlingRights`, `enPassantTarget`, `currentZobristKey` (exactly the pattern in `EngineGame.TryApplyLegalMove` / `GetLegalMoveStrings`).
- `movePiece(from, to)` takes **bitboards** (`1UL << index`). It does **not** flip `isWhiteTurn` — the caller does.
- `isSquareSafe(kingBitboard)` uses `Board.Instance.isWhiteTurn` as "the colour of the king being tested". To test the other side's king, temporarily set `isWhiteTurn`, then restore it.
- **Promotion is always to a queen** (auto-promote inside `movePiece`). Underpromotion is not supported. v1 accepts this: no promotion-choice UI.
- Search: set `AI.Instance.timeLimitMs`, call `AI.Instance.GetBestMove(depth)` → packed `ushort` (0 = none). Transposition table (`AI.Instance.transpositionTable`) and `killerMoves` persist between calls; clear them on "new game".
- Repetition/50-move info exists inside the engine search; we do not expose draws by repetition or 50-move in v1.

## 5. Decisions: flow, stack, hosting

### 5.1 Architecture decision: **stateless server, state lives in the browser**

Because the engine is a process-wide singleton, the server must not keep per-game state (two visitors would overwrite each other). So:

- The **browser holds the complete game state** (a JSON object).
- Every request sends that state plus an action; the server takes a **global lock**, loads the position into the engine with `LoadFEN`, computes the result, releases the lock, and returns the **new full state**.
- This also fits free hosting that sleeps/restarts (nothing important lives in server memory).

### 5.2 The engine is only a tool, never modified

All variant logic (expiry, points, drops) is **plain C# on our own state object** (`board` array + counters). The engine is used only for:

1. generating/validating standard chess moves,
2. applying a standard move,
3. testing "is this king attacked",
4. searching for a best move.

Drops and expiry change the board array in our state; then we regenerate a FEN and `LoadFEN` it. This avoids touching engine internals (bitboards, Zobrist) at all.

### 5.3 Stack

| Layer | Choice | Why |
|---|---|---|
| Server | **ASP.NET Core minimal API, .NET 10, C#** | The engine is C#. Same process, no IPC, no UCI parsing |
| Engine integration | In-process **class library** + an `EngineFacade` class with a single lock | One place touches the singletons |
| Client | **React 18 + TypeScript + Vite**, plain CSS, no UI library, no state library (`useReducer`) | Small, well-known, easy to extend with shop/badges UI, later reusable for Android via Capacitor |
| Pieces | Unicode chess glyphs (no asset licensing) | Zero setup. Can be swapped for SVG later |
| Tests | **xUnit** (server), manual checklist (client) | Perft protects the engine integration; unit tests protect variant rules |
| Packaging | **Docker** (multi-stage) — one container serves API **and** the built client | One URL, no CORS, simplest deploy |
| Hosting | **Render** — free Web Service (Docker runtime) | Free tier supports Docker web services. Caveats: sleeps after 15 min idle (~1 min cold start), 750 free instance hours/month, may restart any time (fine: stateless), free CPU is small so bot searches are shallower |
| Not used | Vercel (cannot run a persistent .NET server; could host only a static client later), Railway (credit-based; check its current free terms before relying on it — fallback only) | |

### 5.4 Request flow

```
Browser (React)                              ASP.NET Core (Render, Docker)
 holds GameState  --- POST /api/... --->     lock(engine)
 renders board/shop                           state -> FEN -> LoadFEN
                                              VariantRules + EngineFacade
 <--------- new GameState (JSON) ---------   unlock
```

## 6. Why no engine changes are needed

The owner already proved that add/remove piece works from outside the engine (wrapper sets `boardSquares` / rebuilds). We go one step safer: we never poke engine internals; we just build a FEN from our state. Keep the engine files byte-identical to the originals so they can be re-synced.

`scripts\sync-engine.ps1` must copy `Board.cs, Chess.cs, AI.cs, Zobrist.cs, Perft.cs` from `C:\Users\jayes\Desktop\StrangeChess\Engine\Engine\` into `engine\StrangeChess.Engine\` (read from reference, write to ours; never the reverse).

## 7. Folder layout (create exactly this)

```
C:\Users\jayes\Desktop\StrangeChessWeb\
  plan.md                      (this file, already present)
  README.md                    (how to run, decisions, rules summary)
  .gitignore                   (bin/ obj/ node_modules/ dist/ .vs/ *.user)
  StrangeChessWeb.sln
  Dockerfile
  .dockerignore
  render.yaml
  scripts\
    sync-engine.ps1
  engine\
    StrangeChess.Engine\       (copied .cs files + our own .csproj: net10.0, OutputType=Library, no Program.cs)
  server\
    StrangeChess.Server\
      Program.cs
      Api\                     (request/response records, endpoints)
      Domain\                  (GameState, Piece types, VariantConfig, VariantRules, Fen, BotPolicy)
      Engine\EngineFacade.cs   (ONLY file that references Board/Chess/AI)
      variant.config.json
    StrangeChess.Tests\        (xUnit)
  client\                      (Vite + React + TS)
  docs\
    engine-notes.md            (your notes from reading the reference, Phase 0)
```

Rule: **nothing outside `EngineFacade.cs` may reference `Board`, `Chess`, `AI`, or `Zobrist`.** This keeps a future refactor (removing singletons) to one file.

### EngineFacade API (suggested)

```csharp
public sealed class EngineFacade
{
    // every method takes the global lock internally
    IReadOnlyList<string> LegalMoves(string fen);                 // UCI strings, fully legal (king-safe)
    string ApplyMove(string fen, string uci);                     // returns new FEN, side to move flipped
    bool IsKingAttacked(string fen, bool whiteKing);              // regardless of side to move
    BotMove? BestMove(string fen, int depth, long timeMs);        // uci or null
    void ResetSearchState();                                      // clear TT/killers
}
```

## 8. API contract (all JSON; every endpoint is stateless)

`GameState` is defined in section 10. Fields marked *derived* are computed by the server and ignored on input.

| Method & path | Request | Response |
|---|---|---|
| `GET /health` | – | `200 ok` (Render health check) |
| `GET /api/config` | – | current default `VariantConfig` |
| `POST /api/new` | `{ mode: "classic"\|"variant", config?: VariantConfig }` | `GameState` |
| `POST /api/move` | `{ state, uci }` | `GameState` or `400 { error }` |
| `POST /api/drop` | `{ state, piece: "N", square: "b1" }` | `GameState` or `400` (variant only) |
| `POST /api/bot` | `{ state, depth, timeMs }` | `{ state, action }` where action is the move/drop the bot played |

Server rules: treat the incoming `state` as **untrusted** — validate shape (64 squares, exactly one king per colour, valid piece letters, `left` within config bounds, points ≥ 0). Return `400` with a clear message on invalid input. Clamp `depth` to 1–10 and `timeMs` to 50–3000. Never trust client-sent `legalMoves/legalDrops/status`; recompute.

## 9. Variant mode — rules spec (defaults; ALL numbers live in `variant.config.json`)

Classic mode = ordinary chess; none of the below applies and the UI hides badges/shop.

### 9.1 Config (defaults)

```json
{
  "pointsPerTurn": 10,
  "startingPoints": 0,
  "lifetimes": { "pawn": 10, "knight": 7, "bishop": 7, "rook": 5, "queen": 2 },
  "costs":     { "pawn": 10, "knight": 30, "bishop": 30, "rook": 50, "queen": 80 },
  "dropRanks": { "white": [1, 2], "black": [7, 8] },
  "pawnDropRanks": { "white": [2], "black": [7] },
  "dropConsumesTurn": true,
  "promotionResetsLifetime": true,
  "maxTurns": 300
}
```

The king never expires, is never bought, never dropped. **These costs/lifetimes are guesses the owner will tune** — the config must be the only place they exist, and the UI should expose them (a small "Rules" panel that can edit them before starting a new game; the edited config is sent in `/api/new` and then carried inside `GameState.config`).

### 9.2 A turn

A player's turn is exactly one **action**: a normal chess move **or** a drop (if `dropConsumesTurn`). Then, in this order:

1. **Action** is applied.
2. **Award points:** the player gains `pointsPerTurn`.
3. **Expiry sweep (owner's pieces only):** every non-king piece of the player that **existed at the start of this turn** has `left` decreased by 1; pieces reaching 0 are removed from the board. A piece **dropped this turn is not decremented this turn**. (So lifetime *L* means the piece can act on *L* of its owner's turns. The initial pieces start with full lifetime and the initial queen therefore vanishes at the end of White's 2nd turn unless it is captured first.)
4. Update castling rights / en-passant target: if an expired piece stood on a rook/king home square, clear the matching castling right; clear the en-passant target if the pawn it refers to is gone, or after any drop.

`left` travels with the piece when it moves (moving does not reset it). A captured piece disappears with its counter. A promoted pawn becomes a queen; if `promotionResetsLifetime`, its `left` becomes the queen lifetime.

### 9.3 Drops

- Buy and drop in one action: pick an affordable piece type, then pick an empty square in the player's drop zone. Cost is deducted. Dropped piece gets full lifetime.
- Drop zone = `dropRanks[side]`, empty squares only; pawns only on `pawnDropRanks[side]`; no kings.
- A drop is legal only if the resulting turn is legal (see 9.4). Dropping may give check/checkmate. Dropping can block a check.

### 9.4 Legality of a whole turn (important)

Expiry can remove the player's own blockers. Therefore a turn (action + points + expiry sweep) is legal **only if, after the whole sequence, the player's own king is not attacked.** Implement by simulating the sweep on a copy of the board and testing with `EngineFacade.IsKingAttacked`. The legal-move list shown to the UI must already be filtered by this rule (for both moves and drops).

### 9.5 End of game

At the start of a player's turn, if they have **no legal turn**: king attacked → **checkmate** (other side wins); otherwise **stalemate** (draw). Also draw when `maxTurns` is reached. In variant mode there is no 50-move rule, repetition rule, or insufficient-material rule. Classic mode uses checkmate/stalemate from legal-move count only (no repetition/50-move in v1).

### 9.6 Open balance questions (do NOT block on these; defaults above; list in README)

- Starting army evaporates fast with these lifetimes (everything but the king is gone by turn ~10). Is that intended?
- Should drops give points? (default: yes, every turn gives points)
- Should costs scale with lifetime?

## 10. GameState schema (JSON)

```jsonc
{
  "mode": "classic" | "variant",
  "config": { /* VariantConfig snapshot, always present */ },
  "board": [ /* 64 entries, index 0 = a1 ... 63 = h8 */
    null | { "p": "P", "left": 10 }   // p: PNBRQK white, pnbrqk black; left = null for kings and in classic mode
  ],
  "turn": "white" | "black",
  "castling": "KQkq" | "-",
  "enPassant": "e3" | null,
  "halfmoveClock": 0,
  "fullmove": 1,
  "turnNumber": 0,                       // total actions played, for maxTurns
  "points": { "white": 0, "black": 0 },
  "history": [                           // append-only action log (enables replays later)
    { "side": "white", "type": "move", "uci": "e2e4" },
    { "side": "white", "type": "drop", "piece": "N", "square": "b1" }
  ],
  "events": [ { "type": "expired", "square": "a2", "piece": "P", "side": "white" } ],  // from the LAST action only
  "status": "active" | "checkmate" | "stalemate" | "draw",
  "winner": null | "white" | "black",
  "inCheck": false,                      // derived: side to move is attacked right now
  "legalMoves": ["e2e4", "..."],         // derived
  "legalDrops": { "N": ["b1", "g1"] }    // derived; only affordable piece types; empty in classic mode
}
```

## 11. Bot

- **Classic mode:** `EngineFacade.BestMove`. Difficulty presets: Easy (depth 2, 200 ms), Medium (depth 4, 500 ms), Hard (depth 6, 1500 ms). Free-tier CPU is small; time limit dominates, so the bot may be weaker online than locally. That is acceptable.
- **Variant mode (v1, heuristic, honest about being weak):**
  1. Ask the engine for its best standard move on the current board.
  2. If that move is a legal turn under 9.4, play it. Otherwise pick a random legal turn.
  3. Drop heuristic: if the bot can afford a piece and a legal drop exists, with probability ~40% (config) prefer a drop: most expensive affordable piece, on a drop square that is not attacked by the opponent (use facade attack test); if none is safe, skip.
- Document that variant-aware search is future work.

## 12. Client spec

Single page, responsive, usable on phone.

- Board: CSS grid 8×8, white at bottom by default, **flip** button, coordinates, last-move highlight, check highlight on the king, legal-target dots when a piece is selected, click-select then click-target (drag is optional/nice-to-have).
- Variant UI: each piece shows a small **lifetime badge** (`left`); badge turns warning colour at ≤ 2 and flashes when it will expire at the end of the owner's turn. A **points panel** for both players. A **shop** panel: buttons for affordable piece types with cost; clicking one highlights legal drop squares; click a square to drop; Esc cancels.
- Game controls: New game (mode: Classic/Variant; opponent: Bot/Hot-seat; your colour vs bot; bot difficulty), Resume (from `localStorage`), Rules panel (edit config before a new game).
- Move list (history) with drops labelled (e.g. `N@b1`), event log ("White's pawn on a2 expired").
- Game-over banner (checkmate/stalemate/draw + winner).
- "Waking up the server…" notice if a request takes > 3 s (Render cold start), with automatic retry.
- Autosave `GameState` to `localStorage` after every action; offer to resume on load.
- No promotion picker (engine auto-queens). Show a note in README.
- Dev: Vite dev server on `5173` proxying `/api` and `/health` to the .NET server on `5080`. Prod: built files are served by ASP.NET Core from `wwwroot` (`UseDefaultFiles`, `UseStaticFiles`, SPA fallback to `index.html`).

## 13. Phases (stop after each one)

### Phase 0 — Recon & scaffold
- Read everything listed in section 3. Write `docs/engine-notes.md` (≤ 1 page): how to boot the engine, how legality filtering works, any surprises.
- Create the layout from section 7, `.gitignore`, solution, empty projects, `sync-engine.ps1`, run it once.
- `dotnet build` succeeds for `engine` + `server` + `tests` (run from `StrangeChessWeb` only).
- **Done when:** build passes and a trivial test that boots the engine and loads the start FEN passes.

### Phase 1 — Classic mode playable
- Implement `EngineFacade`, `Fen` (state ⇄ FEN), `/health`, `/api/new`, `/api/move`, `/api/bot` (classic only).
- **Perft tests via the facade** (these guard the integration): start position depth 1–4 → 20, 400, 8902, 197281; "Kiwipete" `r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1` depth 1–3 → 48, 2039, 97862. If numbers differ, **investigate and report; do not "fix" by editing engine algorithm files** — tell the owner.
- Client: board, click-to-move, bot/hot-seat, move list, game over, flip.
- **Done when:** the owner can play a full classic game vs the bot locally.

### Phase 2 — Variant rules
- `VariantConfig` + `variant.config.json`, expiry sweep, points, shop/drops, whole-turn legality (9.4), end-of-game (9.5), `/api/drop`, events.
- xUnit tests (minimum): lifetime countdown and removal timing; dropped piece not decremented on its drop turn; `left` travels with a moved piece; queen expires at end of owner's 2nd turn; expiry exposing own king makes a move illegal; a drop can block a check; castling right cleared when a home-square rook expires; en-passant cleared after a drop; pawn drop only on allowed rank; cannot afford → no drop; points accumulate; stalemate/checkmate detection with expiry; promotion lifetime reset; `maxTurns` draw.
- Client: badges, points, shop, rules panel, event log.
- **Done when:** a hot-seat variant game is fully playable and the tests pass.

### Phase 3 — Variant bot
- Implement section 11 variant bot. Add a test that the bot never returns an illegal turn over 200 random self-play games (cap turns, low depth).
- **Done when:** bot vs human works in variant mode without illegal moves.

### Phase 4 — Polish & deploy
- Dockerfile (multi-stage: `node:22` build client → `mcr.microsoft.com/dotnet/sdk:10.0` publish server and copy `client/dist` into `wwwroot` → `mcr.microsoft.com/dotnet/aspnet:10.0` runtime). Server must listen on `0.0.0.0:$PORT` (Render sets `PORT`; fall back to 10000).
- `render.yaml`: one `web` service, `runtime: docker`, `plan: free`, `healthCheckPath: /health`.
- README: run locally (two terminals: `dotnet run` and `npm run dev`), run tests, how to deploy to Render step by step (push to GitHub → Render "New Web Service"/Blueprint → pick repo), how to re-sync the engine, rules summary, list of decisions and open balance questions, known limits (auto-queen promotion, cold start, weak free CPU).
- **Done when:** `docker build` + `docker run -p 10000:10000` works locally and the owner can deploy.

## 14. Future hooks (design for, DO NOT build)

- **Multiplayer:** server-side `GameSession` per room replaces "state in browser"; SignalR hub; server authoritative. The append-only `history` and the `/api/move|drop` action shapes should map 1:1 onto hub messages. Engine singleton must first be refactored to instances (only `EngineFacade` should need to change).
- **Database:** store `history` (+ result, players, config) as JSONB in PostgreSQL; replay by re-simulating.
- **Android:** wrap the React app (Capacitor/PWA). **VR:** Unity client calling the same JSON API.

## 15. Working agreements

- Small, readable files; comments only for non-obvious engine quirks.
- No secrets, no analytics, no auth.
- Do not create git commits or push unless the owner asks.
- Every phase end: update `README.md`, then report: what changed, how to run, how to test, what is next.
- If a test reveals an engine bug, report it with a minimal reproducing FEN and stop that thread; do not patch the engine silently.
