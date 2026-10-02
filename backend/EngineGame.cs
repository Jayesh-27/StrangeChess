using System.Text;

namespace StrangeChess.Web;

public sealed class EngineGame
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private readonly object sync = new();
    private readonly List<string> moves = new();

    public EngineGame()
    {
        lock (sync)
        {
            EnsureEngine();
            Board.Instance.Initialize();
        }
    }

    public GameState Reset(string? fen)
    {
        lock (sync)
        {
            EnsureEngine();
            var selectedFen = string.IsNullOrWhiteSpace(fen) ? StartFen : fen.Trim();
            Board.Instance.LoadFEN(selectedFen);
            moves.Clear();
            ClearSearchState();
            return Snapshot();
        }
    }

    public GameState GetState()
    {
        lock (sync) return Snapshot();
    }

    public GameState EditSquare(int square, string? symbol)
    {
        lock (sync)
        {
            if (square is < 0 or > 63)
                throw new ArgumentException("The selected square is outside the board.");

            var piece = ParsePiece(symbol);
            Board.Instance.boardSquares[square] = piece;
            RebuildPositionState();
            moves.Clear();
            ClearSearchState();
            return Snapshot();
        }
    }

    public MoveResult PlayHumanMove(string uci)
    {
        lock (sync)
        {
            if (!TryApplyLegalMove(uci, out var error))
                return new MoveResult(null, error);

            moves.Add(uci.ToLowerInvariant());
            return new MoveResult(Snapshot(), null);
        }
    }

    public MoveResult PlayEngineMove(int depth, long timeMs)
    {
        lock (sync)
        {
            if (depth is < 1 or > 12) return new MoveResult(null, "Depth must be between 1 and 12.");
            if (timeMs is < 50 or > 30000) return new MoveResult(null, "Time must be between 50 and 30000 milliseconds.");

            Chess.Instance.GenerateAllMoves(0);
            if (Chess.Instance.moveCount[0] == 0)
                return new MoveResult(Snapshot(), "The game is over.");

            AI.Instance.timeLimitMs = timeMs;
            var bestMove = AI.Instance.GetBestMove(depth);
            var uci = Chess.Instance.FormatUciMove(bestMove);
            string? error = null;
            if (bestMove == 0 || !TryApplyLegalMove(uci, out error))
                return new MoveResult(null, error ?? "The engine did not return a legal move.");

            moves.Add(uci);
            return new MoveResult(Snapshot(), null);
        }
    }

    private bool TryApplyLegalMove(string? requestedMove, out string? error)
    {
        error = null;
        var uci = requestedMove?.Trim().ToLowerInvariant();
        if (uci is null || (uci.Length != 4 && uci.Length != 5))
        {
            error = "Enter a UCI move such as e2e4.";
            return false;
        }

        var from = ParseSquare(uci[..2]);
        var to = ParseSquare(uci.Substring(2, 2));
        if (from < 0 || to < 0 || (uci.Length == 5 && !"qrbn".Contains(uci[4])))
        {
            error = "That is not a valid UCI move.";
            return false;
        }

        var movingPiece = Board.Instance.boardSquares[from];
        var movingWhite = IsWhite(movingPiece);
        if (movingPiece == pieceType.none || movingWhite != Board.Instance.isWhiteTurn)
        {
            error = "That piece cannot move right now.";
            return false;
        }

        Chess.Instance.GenerateAllMoves(0);
        for (var index = 0; index < Chess.Instance.moveCount[0]; index++)
        {
            var candidate = Chess.Instance.moveList[0][index];
            if ((candidate & 0x3F) != from || ((candidate >> 6) & 0x3F) != to) continue;
            var candidateUci = Chess.Instance.FormatUciMove(candidate);
            if (uci.Length == 5 && candidateUci != uci) continue;
            if (uci.Length == 4 && candidateUci.Length != 4) continue;

            var original = Board.Instance.boardSquares[from];
            var captured = Board.Instance.boardSquares[to];
            var savedRights = Chess.Instance.castlingRights;
            var savedEnPassant = Chess.Instance.enPassantTarget;
            var savedHash = Chess.Instance.currentZobristKey;
            Chess.Instance.movePiece(1UL << from, 1UL << to);
            var king = Board.Instance.pieceBitboards[(int)(movingWhite ? pieceType.whiteKing : pieceType.blackKing)];
            var legal = Chess.Instance.isSquareSafe(king);
            Chess.Instance.unmakeMove(1UL << from, 1UL << to, captured, original);
            Chess.Instance.castlingRights = savedRights;
            Chess.Instance.enPassantTarget = savedEnPassant;
            Chess.Instance.currentZobristKey = savedHash;

            if (!legal) continue;
            Chess.Instance.movePiece(1UL << from, 1UL << to);
            Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;
            return true;
        }

        error = "That move is not legal in the current position.";
        return false;
    }

    private GameState Snapshot()
    {
        var board = new string[64];
        for (var index = 0; index < 64; index++) board[index] = PieceSymbol(Board.Instance.boardSquares[index]);
        var legalMoves = GetLegalMoveStrings();
        var whiteKing = Board.Instance.pieceBitboards[(int)pieceType.whiteKing];
        var blackKing = Board.Instance.pieceBitboards[(int)pieceType.blackKing];
        var inCheck = Board.Instance.isWhiteTurn
            ? !Chess.Instance.isSquareSafe(whiteKing)
            : !Chess.Instance.isSquareSafe(blackKing);
        return new GameState(board, Board.Instance.isWhiteTurn ? "white" : "black", inCheck, moves.ToArray(), legalMoves);
    }

    private string[] GetLegalMoveStrings()
    {
        Chess.Instance.GenerateAllMoves(0);
        var legal = new List<string>();
        for (var index = 0; index < Chess.Instance.moveCount[0]; index++)
        {
            var move = Chess.Instance.moveList[0][index];
            var from = move & 0x3F;
            var to = (move >> 6) & 0x3F;
            var original = Board.Instance.boardSquares[from];
            var captured = Board.Instance.boardSquares[to];
            var rights = Chess.Instance.castlingRights;
            var enPassant = Chess.Instance.enPassantTarget;
            var hash = Chess.Instance.currentZobristKey;
            Chess.Instance.movePiece(1UL << from, 1UL << to);
            var king = Board.Instance.pieceBitboards[(int)(Board.Instance.isWhiteTurn ? pieceType.whiteKing : pieceType.blackKing)];
            var isLegal = Chess.Instance.isSquareSafe(king);
            Chess.Instance.unmakeMove(1UL << from, 1UL << to, captured, original);
            Chess.Instance.castlingRights = rights;
            Chess.Instance.enPassantTarget = enPassant;
            Chess.Instance.currentZobristKey = hash;
            if (isLegal) legal.Add(Chess.Instance.FormatUciMove(move));
        }
        return legal.ToArray();
    }

    private static void EnsureEngine()
    {
        Board.Instance ??= new Board();
        Chess.Instance ??= new Chess();
        AI.Instance ??= new AI();
    }

    private static void ClearSearchState()
    {
        Array.Clear(AI.Instance.transpositionTable, 0, AI.Instance.transpositionTable.Length);
        Array.Clear(AI.Instance.killerMoves, 0, AI.Instance.killerMoves.Length);
    }

    private static void RebuildPositionState()
    {
        Array.Clear(Board.Instance.pieceBitboards, 0, Board.Instance.pieceBitboards.Length);
        for (var square = 0; square < Board.Instance.boardSquares.Length; square++)
        {
            var piece = Board.Instance.boardSquares[square];
            if (piece != pieceType.none)
                Board.Instance.pieceBitboards[(int)piece] |= 1UL << square;
        }

        Board.Instance.CalculateExtraBitboards();
        Chess.Instance.castlingRights = 0;
        Chess.Instance.enPassantTarget = -1;
        Chess.Instance.currentZobristKey = Chess.Instance.GenerateHashFromScratch();
        Chess.Instance.positionHistory[0] = Chess.Instance.currentZobristKey;
        Chess.Instance.historyPly = 1;
    }

    private static int ParseSquare(string square) => square.Length == 2 && square[0] is >= 'a' and <= 'h' && square[1] is >= '1' and <= '8'
        ? (square[1] - '1') * 8 + square[0] - 'a'
        : -1;

    private static bool IsWhite(pieceType piece) => piece is >= pieceType.whitePawn and <= pieceType.whiteKing;

    private static pieceType ParsePiece(string? symbol) => symbol switch
    {
        null or "" => pieceType.none,
        "P" => pieceType.whitePawn, "N" => pieceType.whiteKnight, "B" => pieceType.whiteBishop,
        "R" => pieceType.whiteRook, "Q" => pieceType.whiteQueen, "K" => pieceType.whiteKing,
        "p" => pieceType.blackPawn, "n" => pieceType.blackKnight, "b" => pieceType.blackBishop,
        "r" => pieceType.blackRook, "q" => pieceType.blackQueen, "k" => pieceType.blackKing,
        _ => throw new ArgumentException("Choose a valid chess piece or remove mode.")
    };

    private static string PieceSymbol(pieceType piece) => piece switch
    {
        pieceType.whitePawn => "P", pieceType.whiteKnight => "N", pieceType.whiteBishop => "B", pieceType.whiteRook => "R", pieceType.whiteQueen => "Q", pieceType.whiteKing => "K",
        pieceType.blackPawn => "p", pieceType.blackKnight => "n", pieceType.blackBishop => "b", pieceType.blackRook => "r", pieceType.blackQueen => "q", pieceType.blackKing => "k",
        _ => ""
    };
}

public sealed record GameState(string[] Board, string Turn, bool InCheck, string[] Moves, string[] LegalMoves);
public sealed record MoveResult(GameState? State, string? Error);
