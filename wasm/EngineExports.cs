using System.Runtime.InteropServices.JavaScript;
using System.Text;

// Thin JavaScript bridge around the engine. The worker calls these exports;
// all chess logic stays in ../Engine.
public static partial class EngineExports
{
    const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static void Main() { }

    [JSExport]
    public static void Init()
    {
        Board.Instance = new Board();
        Chess.Instance = new Chess();
        AI.Instance = new AI();
        Board.Instance.Initialize();
    }

    [JSExport]
    public static void NewGame()
    {
        Board.Instance.LoadFEN(StartFen);
        Array.Clear(AI.Instance.transpositionTable, 0, AI.Instance.transpositionTable.Length);
        Array.Clear(AI.Instance.killerMoves, 0, AI.Instance.killerMoves.Length);
    }

    // Same as the UCI "position startpos moves ..." handler in Program.cs.
    [JSExport]
    public static void SetPosition(string moves)
    {
        Board.Instance.LoadFEN(StartFen);
        foreach (string m in moves.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int from = (m[1] - '1') * 8 + (m[0] - 'a');
            int to = (m[3] - '1') * 8 + (m[2] - 'a');
            Chess.Instance.movePiece(1UL << from, 1UL << to);
            Board.Instance.isWhiteTurn = !Board.Instance.isWhiteTurn;
        }
    }

    // Legal moves as "e2e4 g1f3 ...", using the engine's own generator + legality check.
    [JSExport]
    public static string LegalMoves()
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>();
        bool isWhite = Board.Instance.isWhiteTurn;
        Chess.Instance.GenerateAllMoves(0);
        int count = Chess.Instance.moveCount[0];
        for (int i = 0; i < count; i++)
        {
            ushort move = Chess.Instance.moveList[0][i];
            if (!IsLegal(move, isWhite)) continue;
            string uci = Chess.Instance.FormatUciMove(move).Substring(0, 4); // engine always promotes to a queen
            if (seen.Add(uci)) sb.Append(uci).Append(' ');
        }
        return sb.ToString().Trim();
    }

    [JSExport]
    public static bool InCheck()
    {
        bool isWhite = Board.Instance.isWhiteTurn;
        ulong king = Board.Instance.pieceBitboards[(int)(isWhite ? pieceType.whiteKing : pieceType.blackKing)];
        return !Chess.Instance.isSquareSafe(king);
    }

    // 64 chars, a1..h8, "PNBRQKpnbrqk" or '.'.
    [JSExport]
    public static string BoardString()
    {
        const string symbols = ".PNBRQKpnbrqk";
        var chars = new char[64];
        for (int i = 0; i < 64; i++) chars[i] = symbols[(int)Board.Instance.boardSquares[i]];
        return new string(chars);
    }

    // Runs the engine's iterative-deepening search to exactly `depth` within `ms`.
    // Returns "bestmove depth score nodes elapsedMs".
    [JSExport]
    public static string Search(int depth, double ms)
    {
        AI.Instance.timeLimitMs = Math.Max(1, (long)ms);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ushort best = AI.Instance.GetBestMove(depth);
        sw.Stop();
        string uci = best == 0 ? "0000" : Chess.Instance.FormatUciMove(best);
        return $"{uci} {AI.Instance.completedDepth} {AI.Instance.completedScore} {AI.Instance.nodesSearched} {sw.Elapsed.TotalMilliseconds:F1}";
    }

    static bool IsLegal(ushort move, bool isWhite)
    {
        int from = move & 0x3F, to = (move >> 6) & 0x3F;
        ulong fromSq = 1UL << from, toSq = 1UL << to;
        int savedCastling = Chess.Instance.castlingRights;
        int savedEP = Chess.Instance.enPassantTarget;
        ulong savedHash = Chess.Instance.currentZobristKey;
        pieceType original = Board.Instance.boardSquares[from];
        pieceType captured = Board.Instance.boardSquares[to];

        Chess.Instance.movePiece(fromSq, toSq);
        ulong king = Board.Instance.pieceBitboards[(int)(isWhite ? pieceType.whiteKing : pieceType.blackKing)];
        bool legal = Chess.Instance.isSquareSafe(king);

        Chess.Instance.unmakeMove(fromSq, toSq, captured, original);
        Chess.Instance.castlingRights = savedCastling;
        Chess.Instance.enPassantTarget = savedEP;
        Chess.Instance.currentZobristKey = savedHash;
        return legal;
    }
}
