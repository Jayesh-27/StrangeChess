using System.Text;

/// <summary>
/// Converts between GameState and FEN strings.
/// </summary>
public static class FenHelper
{
    public const string StartFen = "1k6/ppp5/8/8/8/8/4P1PP/6K1 w - - 0 1";

    /// <summary>Builds a standard 6-field FEN from the GameState board + metadata.</summary>
    public static string ToFen(GameState state)
    {
        var sb = new StringBuilder();

        // Piece placement: rank 8 (index 56-63) down to rank 1 (index 0-7)
        for (int rank = 7; rank >= 0; rank--)
        {
            int empty = 0;
            for (int file = 0; file < 8; file++)
            {
                var piece = state.Board[rank * 8 + file];
                if (piece == null)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(piece.P);
                }
            }
            if (empty > 0) sb.Append(empty);
            if (rank > 0) sb.Append('/');
        }

        sb.Append(state.Turn == "white" ? " w " : " b ");
        sb.Append(string.IsNullOrEmpty(state.Castling) || state.Castling == "-" ? "-" : state.Castling);
        sb.Append(' ');
        sb.Append(state.EnPassant ?? "-");
        sb.Append(' ');
        sb.Append(state.HalfmoveClock);
        sb.Append(' ');
        sb.Append(state.Fullmove);

        return sb.ToString();
    }

    /// <summary>
    /// Creates a new GameState from a FEN string.
    /// Non-FEN fields (mode, config, points, history, etc.) are copied from the optional template.
    /// </summary>
    public static GameState FromFen(string fen, GameState? template = null)
    {
        var parts = fen.Split(' ');
        return new GameState
        {
            Mode      = template?.Mode ?? "classic",
            Config    = template?.Config ?? new VariantConfig(),
            Board     = ParseBoard(parts[0]),
            Turn      = parts[1] == "w" ? "white" : "black",
            Castling  = parts[2],
            EnPassant = parts[3] == "-" ? null : parts[3],
            HalfmoveClock = int.Parse(parts[4]),
            Fullmove  = int.Parse(parts[5]),
            TurnNumber = template?.TurnNumber ?? 0,
            Points    = template != null ? new Dictionary<string, int>(template.Points) : new Dictionary<string, int> { ["white"] = 0, ["black"] = 0 },
            History   = template != null ? new List<HistoryEntry>(template.History) : new List<HistoryEntry>(),
            Events    = template != null ? new List<GameEvent>(template.Events) : new List<GameEvent>(),
        };
    }

    /// <summary>Creates the starting board for a new game.</summary>
    public static PieceInfo?[] StartingBoard()
    {
        return ParseBoard("1k6/ppp5/8/8/8/8/4P1PP/6K1");
    }

    private static PieceInfo?[] ParseBoard(string placement)
    {
        var board = new PieceInfo?[64];
        int rank = 7, file = 0;

        foreach (char c in placement)
        {
            if (c == '/')
            {
                rank--;
                file = 0;
            }
            else if (char.IsDigit(c))
            {
                file += c - '0';
            }
            else
            {
                board[rank * 8 + file] = new PieceInfo(c.ToString(), null);
                file++;
            }
        }

        return board;
    }
}

/// <summary>
/// Validates an incoming GameState. Returns null if valid, or an error message.
/// </summary>
public static class StateValidator
{
    private static readonly HashSet<string> ValidPieces = new()
    {
        "P", "N", "B", "R", "Q", "K",
        "p", "n", "b", "r", "q", "k"
    };

    public static string? Validate(GameState? state)
    {
        if (state == null) return "State is required.";
        if (state.Board == null || state.Board.Length != 64)
            return "Board must have exactly 64 squares.";

        int whiteKings = 0, blackKings = 0;
        foreach (var piece in state.Board)
        {
            if (piece == null) continue;
            if (string.IsNullOrEmpty(piece.P) || !ValidPieces.Contains(piece.P))
                return $"Invalid piece: '{piece.P}'.";
            if (piece.P == "K") whiteKings++;
            if (piece.P == "k") blackKings++;
        }

        if (whiteKings != 1) return "Must have exactly one white king.";
        if (blackKings != 1) return "Must have exactly one black king.";

        if (state.Turn is not ("white" or "black"))
            return "Turn must be 'white' or 'black'.";

        return null;
    }
}
