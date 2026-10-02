# Engine Notes (from reading StrangeChess source)

## Booting the engine

```csharp
Board.Instance = new Board();
Chess.Instance = new Chess();
AI.Instance = new AI();
Board.Instance.Initialize();   // precomputes knight/king/rook/bishop attack tables, loads start FEN
```

Order matters: `Board` must exist before `Chess` and `AI` (they reference `Board.Instance`).
`Initialize()` calls `LoadFEN(startpos)` at the end, so the board is ready after this single call.

## Loading a position

`Board.Instance.LoadFEN(fen)` — standard 6-field FEN. Fully resets:
`boardSquares[]`, `pieceBitboards[]`, `whitePieces/blackPieces/allPieces`,
`isWhiteTurn`, `Chess.Instance.castlingRights`, `enPassantTarget`,
`halfmoveClock`, `fullmoveNumber`, Zobrist key, and `positionHistory[0]`.

## Square indexing

LERF: `a1 = 0`, `h1 = 7`, `a8 = 56`, `h8 = 63`. `index = rank * 8 + file` (both 0-based).

## Move representation

`ushort`: `from6 | to6 << 6 | flag4 << 12`. Flags in `moveFlag` enum (0=quiet, 1=double-pawn,
2=O-O, 3=O-O-O, 4=capture, 5=EP, 8–15=promotions). `FormatUciMove(move)` → e.g. `"e2e4"`, `"e7e8q"`.

## Generating & filtering legal moves

1. `Chess.Instance.GenerateAllMoves(0)` fills `moveList[0]` / `moveCount[0]` with **pseudo-legal** moves.
2. For each candidate: save `castlingRights`, `enPassantTarget`, `currentZobristKey`.
3. `Chess.Instance.movePiece(1UL << from, 1UL << to)` — takes **bitboards**, not indices.
4. Check own king: `Chess.Instance.isSquareSafe(kingBitboard)` where `isWhiteTurn` still equals the
   mover's colour (movePiece does NOT flip `isWhiteTurn`).
5. `Chess.Instance.unmakeMove(fromBB, toBB, capturedPiece, originalPiece)` to undo.
6. Restore saved castling/EP/hash.

This exact pattern is used in `EngineGame.TryApplyLegalMove`, `GetLegalMoveStrings`, and `Perft`.

## isSquareSafe semantics

`isSquareSafe(kingBitboard)` answers "is this king safe from the **opponent** of whoever `isWhiteTurn` points to?"
It uses `isWhiteTurn` to pick enemy piece bitboards. So after `movePiece` (which doesn't flip turn),
the mover's king is correctly checked against the opponent's pieces.

## Promotion

`movePiece` **always promotes to queen** — it ignores the flag bits for underpromotion.
The move generator does emit all four promotion flags (N/B/R/Q), but they all result in a queen.
`FormatUciMove` does append the promo char (`n`, `b`, `r`, `q`), so the facade should filter
to only return the queen-promotion UCI string for each promotion move.

## Search

`AI.Instance.timeLimitMs = ms; AI.Instance.GetBestMove(depth)` → `ushort` (0 = no legal move).
Transposition table + killer moves persist across calls; clear on new game:
`Array.Clear(AI.Instance.transpositionTable, ...)` and `Array.Clear(AI.Instance.killerMoves, ...)`.

## Surprises / gotchas

- **Not thread-safe.** Global singletons, mutable static state everywhere. Must lock externally.
- **movePiece does not flip isWhiteTurn.** The caller is responsible for `Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn` after a legal move.
- **unmakeMove does not restore castling/EP/hash.** Caller must save and restore them manually.
- **Perft ply parameter:** `GenerateAllMoves(ply)` writes to `moveList[ply]`. Recursive calls must use `ply + 1` to avoid overwriting the parent's move list.
- **positionHistory grows:** `movePiece` appends to `positionHistory[historyPly++]`. After unmake, the caller should decrement `historyPly` — but for our facade (LoadFEN each time), this resets automatically.
