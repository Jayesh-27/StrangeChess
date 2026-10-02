using System.Text;

/// <summary>
/// The ONLY class that references Board, Chess, AI, or Zobrist.
/// Every public method acquires a global lock internally.
/// </summary>
public sealed class EngineFacade
{
    private static readonly object _lock = new();
    private static bool _initialized;

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();
        Board.Instance.Initialize();
        _initialized = true;
    }

    /// <summary>
    /// Returns fully-legal UCI move strings for the position.
    /// Promotions are deduplicated to queen only (engine always auto-queens).
    /// </summary>
    public IReadOnlyList<string> LegalMoves(string fen)
    {
        lock (_lock)
        {
            EnsureInitialized();
            Board.Instance.LoadFEN(fen);
            return GetLegalMovesInternal();
        }
    }

    /// <summary>
    /// Applies a legal move and returns the resulting FEN string.
    /// Throws InvalidOperationException if the move is not legal.
    /// </summary>
    public string ApplyMove(string fen, string uci)
    {
        lock (_lock)
        {
            EnsureInitialized();
            Board.Instance.LoadFEN(fen);

            int halfmove = Chess.Instance.halfmoveClock;
            int fullmove = Chess.Instance.fullmoveNumber;
            bool wasWhite = Board.Instance.isWhiteTurn;

            int from = ParseSquareIndex(uci[..2]);
            int to = ParseSquareIndex(uci.Substring(2, 2));
            if (from < 0 || to < 0)
                throw new InvalidOperationException($"Invalid square in move '{uci}'.");

            // Auto-append queen promotion if a pawn reaches the back rank
            if (uci.Length == 4)
            {
                var piece = Board.Instance.boardSquares[from];
                bool isPromotion = (piece == pieceType.whitePawn && to >= 56) ||
                                   (piece == pieceType.blackPawn && to <= 7);
                if (isPromotion) uci += "q";
            }

            var movingPiece = Board.Instance.boardSquares[from];
            bool isPawn = movingPiece == pieceType.whitePawn || movingPiece == pieceType.blackPawn;
            bool isCapture = Board.Instance.boardSquares[to] != pieceType.none;

            Chess.Instance.GenerateAllMoves(0);
            for (int i = 0; i < Chess.Instance.moveCount[0]; i++)
            {
                ushort move = Chess.Instance.moveList[0][i];
                if ((move & 0x3F) != from || ((move >> 6) & 0x3F) != to) continue;

                string candidateUci = Chess.Instance.FormatUciMove(move);
                // For promotions, match the full 5-char UCI; for normal moves, match 4 chars
                if (uci.Length == 5 && candidateUci != uci) continue;
                if (uci.Length == 4 && candidateUci.Length == 5) continue;

                var original = Board.Instance.boardSquares[from];
                var captured = Board.Instance.boardSquares[to];
                int savedCR = Chess.Instance.castlingRights;
                int savedEP = Chess.Instance.enPassantTarget;
                ulong savedHash = Chess.Instance.currentZobristKey;

                Chess.Instance.movePiece(1UL << from, 1UL << to);

                // movePiece does NOT flip isWhiteTurn; check the mover's king
                ulong king = Board.Instance.isWhiteTurn
                    ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing]
                    : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

                if (!Chess.Instance.isSquareSafe(king))
                {
                    // Illegal — undo and try the next candidate
                    Chess.Instance.unmakeMove(1UL << from, 1UL << to, captured, original);
                    Chess.Instance.castlingRights = savedCR;
                    Chess.Instance.enPassantTarget = savedEP;
                    Chess.Instance.currentZobristKey = savedHash;
                    continue;
                }

                // Legal move — flip turn
                Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;

                // The engine doesn't update halfmove/fullmove during movePiece; do it ourselves
                Chess.Instance.halfmoveClock = (isPawn || isCapture) ? 0 : halfmove + 1;
                Chess.Instance.fullmoveNumber = wasWhite ? fullmove : fullmove + 1;

                return GenerateFen();
            }

            throw new InvalidOperationException($"Move '{uci}' is not legal in this position.");
        }
    }

    /// <summary>
    /// Checks whether the specified king is currently attacked.
    /// </summary>
    public bool IsKingAttacked(string fen, bool whiteKing)
    {
        lock (_lock)
        {
            EnsureInitialized();
            Board.Instance.LoadFEN(fen);

            // isSquareSafe uses isWhiteTurn to pick "friendly" vs "enemy" — set it to the king's colour
            bool savedTurn = Board.Instance.isWhiteTurn;
            Board.Instance.isWhiteTurn = whiteKing;

            ulong king = whiteKing
                ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing]
                : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            bool attacked = !Chess.Instance.isSquareSafe(king);

            Board.Instance.isWhiteTurn = savedTurn;
            return attacked;
        }
    }

    /// <summary>
    /// Returns the engine's best move, or null if no legal move exists.
    /// </summary>
    public BotMove? BestMove(string fen, int depth, long timeMs)
    {
        lock (_lock)
        {
            EnsureInitialized();
            Board.Instance.LoadFEN(fen);

            AI.Instance.timeLimitMs = timeMs;
            ushort move = AI.Instance.GetBestMove(depth);

            if (move == 0) return null;

            string uci = Chess.Instance.FormatUciMove(move);
            // Normalize underpromotions to queen (engine always auto-queens anyway)
            if (uci.Length == 5 && uci[4] != 'q')
                uci = uci[..4] + "q";

            return new BotMove(uci);
        }
    }

    /// <summary>
    /// Clears the transposition table and killer moves. Call on new game.
    /// </summary>
    public void ResetSearchState()
    {
        lock (_lock)
        {
            EnsureInitialized();
            Array.Clear(AI.Instance.transpositionTable, 0, AI.Instance.transpositionTable.Length);
            Array.Clear(AI.Instance.killerMoves, 0, AI.Instance.killerMoves.Length);
        }
    }

    /// <summary>
    /// Standard perft node count. Counts all legal moves including all promotion variants.
    /// </summary>
    public long Perft(string fen, int depth)
    {
        lock (_lock)
        {
            EnsureInitialized();
            Board.Instance.LoadFEN(fen);
            return PerftRecursive(depth, 0);
        }
    }

    // ─── Internal helpers ────────────────────────────────────────────

    private static IReadOnlyList<string> GetLegalMovesInternal()
    {
        Chess.Instance.GenerateAllMoves(0);
        var legal = new List<string>();
        var seen = new HashSet<string>();

        for (int i = 0; i < Chess.Instance.moveCount[0]; i++)
        {
            ushort move = Chess.Instance.moveList[0][i];
            int from = move & 0x3F;
            int to = (move >> 6) & 0x3F;

            var original = Board.Instance.boardSquares[from];
            var captured = Board.Instance.boardSquares[to];
            int savedCR = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;

            Chess.Instance.movePiece(1UL << from, 1UL << to);

            ulong king = Board.Instance.isWhiteTurn
                ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing]
                : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(king))
            {
                string uci = Chess.Instance.FormatUciMove(move);
                // Deduplicate: only include queen promotions (engine auto-queens anyway)
                if (uci.Length == 5 && uci[4] != 'q')
                {
                    // skip underpromotion variant
                }
                else if (seen.Add(uci))
                {
                    legal.Add(uci);
                }
            }

            Chess.Instance.unmakeMove(1UL << from, 1UL << to, captured, original);
            Chess.Instance.castlingRights = savedCR;
            Chess.Instance.enPassantTarget = savedEP;
            Chess.Instance.currentZobristKey = savedHash;
        }

        return legal;
    }

    private static long PerftRecursive(int depth, int ply)
    {
        if (depth == 0) return 1;

        Chess.Instance.GenerateAllMoves(ply);
        int moveCount = Chess.Instance.moveCount[ply];
        long nodes = 0;

        for (int i = 0; i < moveCount; i++)
        {
            ushort move = Chess.Instance.moveList[ply][i];
            int from = move & 0x3F;
            int to = (move >> 6) & 0x3F;

            var original = Board.Instance.boardSquares[from];
            var captured = Board.Instance.boardSquares[to];
            int savedCR = Chess.Instance.castlingRights;
            int savedEP = Chess.Instance.enPassantTarget;
            ulong savedHash = Chess.Instance.currentZobristKey;

            Chess.Instance.movePiece(1UL << from, 1UL << to);

            ulong king = Board.Instance.isWhiteTurn
                ? Board.Instance.pieceBitboards[(int)pieceType.whiteKing]
                : Board.Instance.pieceBitboards[(int)pieceType.blackKing];

            if (Chess.Instance.isSquareSafe(king))
            {
                Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;
                nodes += PerftRecursive(depth - 1, ply + 1);
                Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;
            }

            Chess.Instance.unmakeMove(1UL << from, 1UL << to, captured, original);
            Chess.Instance.castlingRights = savedCR;
            Chess.Instance.enPassantTarget = savedEP;
            Chess.Instance.currentZobristKey = savedHash;
        }

        return nodes;
    }

    private static string GenerateFen()
    {
        var sb = new StringBuilder();

        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                var piece = Board.Instance.boardSquares[rank * 8 + file];
                if (piece == pieceType.none)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(PieceToChar(piece));
                }
            }
            if (empty > 0) sb.Append(empty);
            if (rank > 0) sb.Append('/');
        }

        sb.Append(Board.Instance.isWhiteTurn ? " w " : " b ");

        int cr = Chess.Instance.castlingRights;
        if (cr == 0)
            sb.Append('-');
        else
        {
            if ((cr & 1) != 0) sb.Append('K');
            if ((cr & 2) != 0) sb.Append('Q');
            if ((cr & 4) != 0) sb.Append('k');
            if ((cr & 8) != 0) sb.Append('q');
        }

        sb.Append(' ');
        int ep = Chess.Instance.enPassantTarget;
        if (ep < 0)
            sb.Append('-');
        else
        {
            sb.Append((char)('a' + ep % 8));
            sb.Append(ep / 8 + 1);
        }

        sb.Append(' ');
        sb.Append(Chess.Instance.halfmoveClock);
        sb.Append(' ');
        sb.Append(Chess.Instance.fullmoveNumber);

        return sb.ToString();
    }

    private static int ParseSquareIndex(string sq) =>
        sq.Length == 2 && sq[0] is >= 'a' and <= 'h' && sq[1] is >= '1' and <= '8'
            ? (sq[1] - '1') * 8 + (sq[0] - 'a')
            : -1;

    private static char PieceToChar(pieceType p) => p switch
    {
        pieceType.whitePawn   => 'P', pieceType.whiteKnight => 'N',
        pieceType.whiteBishop => 'B', pieceType.whiteRook   => 'R',
        pieceType.whiteQueen  => 'Q', pieceType.whiteKing   => 'K',
        pieceType.blackPawn   => 'p', pieceType.blackKnight => 'n',
        pieceType.blackBishop => 'b', pieceType.blackRook   => 'r',
        pieceType.blackQueen  => 'q', pieceType.blackKing   => 'k',
        _ => ' '
    };
}

public record BotMove(string Uci);
