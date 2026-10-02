using System.Collections.Generic;
using System.Linq;

public static class VariantRules
{
    private static readonly Dictionary<string, string> PieceNames = new()
    {
        { "P", "pawn" }, { "N", "knight" }, { "B", "bishop" }, { "R", "rook" }, { "Q", "queen" },
        { "p", "pawn" }, { "n", "knight" }, { "b", "bishop" }, { "r", "rook" }, { "q", "queen" }
    };

    public static string PieceName(string key) => PieceNames.GetValueOrDefault(key, key);

    public static void InitializeGame(GameState state)
    {
        state.Points["white"] = state.Config.StartingPoints;
        state.Points["black"] = state.Config.StartingPoints;

        for (int i = 0; i < 64; i++)
        {
            var p = state.Board[i];
            if (p != null && p.P.ToUpperInvariant() != "K")
            {
                string type = PieceNames[p.P];
                if (state.Config.Lifetimes.TryGetValue(type, out int lifetime))
                {
                    state.Board[i] = p with { Left = lifetime };
                }
            }
        }
    }

    /// <summary>
    /// Applies a move (already assumed to be pseudo-legal/standard-legal).
    /// </summary>
    public static GameState SimulateMove(GameState state, string uci, EngineFacade facade)
    {
        string fen = FenHelper.ToFen(state);
        string newFen = facade.ApplyMove(fen, uci);
        
        var nextState = FenHelper.FromFen(newFen, state);
        
        // Transfer lifetimes for moved piece and handle promotion
        int from = ParseSquareIndex(uci[..2]);
        int to = ParseSquareIndex(uci.Substring(2, 2));

        // Start with a copy of the old board's lifetimes
        for (int i = 0; i < 64; i++)
        {
            if (nextState.Board[i] != null && nextState.Board[i].P.ToUpperInvariant() != "K")
            {
                // If it's the destination square, it's the piece that just moved
                if (i == to)
                {
                    var oldPiece = state.Board[from];
                    bool isPromotion = uci.Length == 5;
                    
                    if (isPromotion && state.Config.PromotionResetsLifetime)
                    {
                        string promoType = PieceNames[nextState.Board[i].P];
                        nextState.Board[i] = nextState.Board[i] with { Left = state.Config.Lifetimes.GetValueOrDefault(promoType, 2) };
                    }
                    else
                    {
                        nextState.Board[i] = nextState.Board[i] with { Left = oldPiece?.Left };
                    }
                }
                else
                {
                    // For other pieces, just copy the old lifetime if it was the same piece type (ignoring captures)
                    var oldP = state.Board[i];
                    if (oldP != null && oldP.P == nextState.Board[i].P)
                    {
                        nextState.Board[i] = nextState.Board[i] with { Left = oldP.Left };
                    }
                }
            }
        }

        // Apply variant turn end (points, expiry)
        EndTurn(nextState, state.Turn, null);
        return nextState;
    }

    public static GameState SimulateDrop(GameState state, string pieceChar, string square)
    {
        var nextState = CloneState(state);
        
        int to = ParseSquareIndex(square);
        string type = PieceNames[pieceChar];
        int lifetime = state.Config.Lifetimes.GetValueOrDefault(type, 5);

        string actualPieceChar = state.Turn == "white" ? pieceChar.ToUpperInvariant() : pieceChar.ToLowerInvariant();
        nextState.Board[to] = new PieceInfo(actualPieceChar, lifetime);
        nextState.Points[state.Turn] -= state.Config.Costs[type];

        // Toggle turn (like engine does for a move)
        nextState.Turn = state.Turn == "white" ? "black" : "white";

        // If drop consumes turn, do sweep
        if (state.Config.DropConsumesTurn)
        {
            EndTurn(nextState, state.Turn, to);
        }

        return nextState;
    }

    private static void EndTurn(GameState nextState, string turnOwner, int? droppedSquare)
    {
        nextState.Points[turnOwner] += nextState.Config.PointsPerTurn;

        var events = new List<GameEvent>();
        nextState.Events = events;
        bool isWhite = turnOwner == "white";

        for (int i = 0; i < 64; i++)
        {
            var p = nextState.Board[i];
            if (p == null || p.P.ToUpperInvariant() == "K") continue;

            bool belongsToOwner = isWhite ? char.IsUpper(p.P, 0) : char.IsLower(p.P, 0);
            if (!belongsToOwner) continue;

            // Do not decrement piece dropped this turn
            if (droppedSquare.HasValue && i == droppedSquare.Value) continue;

            if (p.Left.HasValue)
            {
                int newLeft = p.Left.Value - 1;
                if (newLeft <= 0)
                {
                    events.Add(new GameEvent("expired", SquareName(i), p.P, turnOwner));
                    nextState.Board[i] = null;
                    ClearCastlingOrEPIfExpired(nextState, i, p.P);
                }
                else
                {
                    nextState.Board[i] = p with { Left = newLeft };
                }
            }
        }

        if (droppedSquare.HasValue)
        {
            // Clear EP target if we did a drop (drops reset EP)
            nextState.EnPassant = null;
        }

        nextState.Events = events;
    }

    private static void ClearCastlingOrEPIfExpired(GameState state, int sq, string piece)
    {
        // If rook expired on home square, remove castling right
        if (piece == "R" && sq == 0) state.Castling = state.Castling.Replace("Q", "");
        if (piece == "R" && sq == 7) state.Castling = state.Castling.Replace("K", "");
        if (piece == "r" && sq == 56) state.Castling = state.Castling.Replace("q", "");
        if (piece == "r" && sq == 63) state.Castling = state.Castling.Replace("k", "");

        if (string.IsNullOrEmpty(state.Castling)) state.Castling = "-";

        // If pawn expired on the EP file/rank that is currently targeted, well, we just clear EP if it matches
        if (piece.ToUpperInvariant() == "P" && state.EnPassant != null)
        {
            int epSq = ParseSquareIndex(state.EnPassant);
            // If white pawn expired on rank 4 (index 24-31) and EP is on rank 3 (16-23)
            // Or black pawn expired on rank 5 and EP is on rank 6
            int file = sq % 8;
            int epFile = epSq % 8;
            if (file == epFile)
            {
                if (piece == "P" && sq / 8 == 3 && epSq / 8 == 2) state.EnPassant = null;
                if (piece == "p" && sq / 8 == 4 && epSq / 8 == 5) state.EnPassant = null;
            }
        }
    }

    public static void ComputeDerivedFields(GameState state, EngineFacade facade)
    {
        bool isWhite = state.Turn == "white";
        string fen = FenHelper.ToFen(state);

        var standardMoves = facade.LegalMoves(fen);
        state.LegalMoves = new List<string>();

        foreach (var uci in standardMoves)
        {
            var sim = SimulateMove(state, uci, facade);
            // After SimulateMove, Turn is flipped. But we want to check if the *original* player's king is attacked.
            // Wait, IsKingAttacked expects the FEN after the move.
            string simFen = FenHelper.ToFen(sim);
            if (!facade.IsKingAttacked(simFen, isWhite))
            {
                state.LegalMoves.Add(uci);
            }
        }

        state.LegalDrops = new Dictionary<string, List<string>>();
        int points = state.Points[state.Turn];

        string[] types = isWhite ? new[] { "P", "N", "B", "R", "Q" } : new[] { "p", "n", "b", "r", "q" };
        var dropRanks = state.Config.DropRanks[state.Turn];
        var pawnDropRanks = state.Config.PawnDropRanks[state.Turn];

        foreach (var pType in types)
        {
            string name = PieceNames[pType];
            int cost = state.Config.Costs.GetValueOrDefault(name, 9999);
            if (points < cost) continue;

            var validRanks = name == "pawn" ? pawnDropRanks : dropRanks;
            var legalSquares = new List<string>();

            foreach (int rank in validRanks)
            {
                for (int file = 0; file < 8; file++)
                {
                    int sq = (rank - 1) * 8 + file;
                    if (state.Board[sq] == null)
                    {
                        string sqName = SquareName(sq);
                        var sim = SimulateDrop(state, pType, sqName);
                        string simFen = FenHelper.ToFen(sim);
                        if (!facade.IsKingAttacked(simFen, isWhite))
                        {
                            legalSquares.Add(sqName);
                        }
                    }
                }
            }

            if (legalSquares.Count > 0)
            {
                state.LegalDrops[pType.ToUpperInvariant()] = legalSquares;
            }
        }

        state.InCheck = facade.IsKingAttacked(fen, isWhite);

        if (state.LegalMoves.Count == 0 && state.LegalDrops.Count == 0)
        {
            if (state.InCheck)
            {
                state.Status = "checkmate";
                state.Winner = isWhite ? "black" : "white";
            }
            else
            {
                state.Status = "stalemate";
                state.Winner = null;
            }
        }
        else if (state.TurnNumber >= state.Config.MaxTurns)
        {
            state.Status = "draw";
            state.Winner = null;
        }
        else
        {
            state.Status = "active";
            state.Winner = null;
        }
    }

    private static GameState CloneState(GameState s)
    {
        return new GameState
        {
            Mode = s.Mode,
            Config = s.Config,
            Board = s.Board.ToArray(),
            Turn = s.Turn,
            Castling = s.Castling,
            EnPassant = s.EnPassant,
            HalfmoveClock = s.HalfmoveClock,
            Fullmove = s.Fullmove,
            TurnNumber = s.TurnNumber,
            Points = new Dictionary<string, int>(s.Points),
            History = new List<HistoryEntry>(s.History),
            Events = new List<GameEvent>(s.Events)
        };
    }

    public static int ParseSquareIndex(string sq) =>
        sq.Length == 2 && sq[0] is >= 'a' and <= 'h' && sq[1] is >= '1' and <= '8'
            ? (sq[1] - '1') * 8 + (sq[0] - 'a')
            : -1;

    public static string SquareName(int idx) =>
        $"{(char)('a' + (idx % 8))}{(idx / 8) + 1}";
}
