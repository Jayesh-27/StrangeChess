using System.Text.Json;

public static class GameEndpoints
{
    private static readonly VariantConfig _defaultConfig;

    static GameEndpoints()
    {
        string path = "variant.config.json";
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            _defaultConfig = JsonSerializer.Deserialize<VariantConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new VariantConfig();
        }
        else
        {
            _defaultConfig = new VariantConfig();
        }
    }

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/config", () => Results.Ok(_defaultConfig));
        app.MapPost("/api/new",  HandleNewGame);
        app.MapPost("/api/move", HandleMove);
        app.MapPost("/api/drop", HandleDrop);
        app.MapPost("/api/bot",  HandleBot);
    }

    private static IResult HandleNewGame(NewGameRequest req, EngineFacade facade)
    {
        string mode = req.Mode ?? "classic";
        if (mode is not ("classic" or "variant"))
            return Results.BadRequest(new ErrorResponse("Mode must be 'classic' or 'variant'."));

        var state = new GameState
        {
            Mode     = mode,
            Config   = req.Config ?? _defaultConfig,
            Board    = FenHelper.StartingBoard(),
            Turn     = "white",
            Castling = "KQkq",
            Fullmove = 1,
        };

        if (mode == "variant")
        {
            VariantRules.InitializeGame(state);
        }

        facade.ResetSearchState();
        ComputeDerived(state, facade);

        return Results.Ok(state);
    }

    private static IResult HandleMove(MoveRequest req, EngineFacade facade)
    {
        var err = StateValidator.Validate(req.State);
        if (err != null) return Results.BadRequest(new ErrorResponse(err));

        string uci = req.Uci?.Trim().ToLowerInvariant() ?? "";
        if (uci.Length < 4 || uci.Length > 5)
            return Results.BadRequest(new ErrorResponse("Provide a UCI move such as 'e2e4'."));

        var state = req.State;
        if (state.Status != "active")
            return Results.BadRequest(new ErrorResponse("The game is already over."));

        if (!state.LegalMoves.Contains(uci) && !state.LegalMoves.Contains(uci + "q"))
            return Results.BadRequest(new ErrorResponse("Illegal move."));

        GameState newState;
        try
        {
            if (state.Mode == "variant")
            {
                newState = VariantRules.SimulateMove(state, uci, facade);
            }
            else
            {
                string fen = FenHelper.ToFen(state);
                string newFen = facade.ApplyMove(fen, uci);
                newState = FenHelper.FromFen(newFen, state);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }

        newState.TurnNumber = state.TurnNumber + 1;
        var history = new List<HistoryEntry>(state.History) { new HistoryEntry(state.Turn, "move", uci) };
        newState.History = history;

        ComputeDerived(newState, facade);
        return Results.Ok(newState);
    }

    public record DropRequest(GameState State, string Piece, string Square);

    private static IResult HandleDrop(DropRequest req, EngineFacade facade)
    {
        var err = StateValidator.Validate(req.State);
        if (err != null) return Results.BadRequest(new ErrorResponse(err));

        var state = req.State;
        if (state.Status != "active" || state.Mode != "variant")
            return Results.BadRequest(new ErrorResponse("Drops are only allowed in active variant games."));

        string pType = req.Piece?.ToUpperInvariant() ?? "";
        if (!state.LegalDrops.TryGetValue(pType, out var validSquares) || !validSquares.Contains(req.Square))
            return Results.BadRequest(new ErrorResponse("Illegal drop."));

        var newState = VariantRules.SimulateDrop(state, pType, req.Square);
        newState.TurnNumber = state.TurnNumber + 1;
        
        var history = new List<HistoryEntry>(state.History) { new HistoryEntry(state.Turn, "drop", null, pType, req.Square) };
        newState.History = history;

        ComputeDerived(newState, facade);
        return Results.Ok(newState);
    }

    private static IResult HandleBot(BotRequest req, EngineFacade facade)
    {
        var err = StateValidator.Validate(req.State);
        if (err != null) return Results.BadRequest(new ErrorResponse(err));

        var state = req.State;
        if (state.Status != "active")
            return Results.BadRequest(new ErrorResponse("The game is already over."));

        int depth  = Math.Clamp(req.Depth, 1, 10);
        long timeMs = Math.Clamp(req.TimeMs, 50, 3000);

        try
        {
            var newState = ComputeBotAction(state, facade, depth, timeMs);
            return Results.Ok(new BotResponse(newState, "ok")); // We don't strictly need the action string returned in BotResponse for now since frontend just uses newState
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }

    public static GameState ComputeBotAction(GameState state, EngineFacade facade, int depth, long timeMs)
    {
        string bestAction = null;
        bool isDrop = false;
        string dropType = null;

        if (state.Mode == "variant")
        {
            var random = new Random();
            if (state.LegalDrops.Count > 0 && (state.LegalMoves.Count == 0 || random.NextDouble() < 0.4))
            {
                var type = state.LegalDrops.Keys
                    .OrderByDescending(k => state.Config.Costs.GetValueOrDefault(VariantRules.PieceName(k), 0))
                    .First();
                
                var squares = state.LegalDrops[type];
                var safeSquares = new List<string>();
                bool isWhite = state.Turn == "white";

                foreach (var sq in squares)
                {
                    var tempState = VariantRules.SimulateDrop(state, type, sq);
                    int kIdx = Array.FindIndex(tempState.Board, p => p?.P == (isWhite ? "K" : "k"));
                    int dIdx = VariantRules.ParseSquareIndex(sq);
                    
                    var tempK = tempState.Board[kIdx];
                    tempState.Board[kIdx] = tempState.Board[dIdx];
                    tempState.Board[dIdx] = tempK;
                    
                    string fenForTest = FenHelper.ToFen(tempState);
                    if (!facade.IsKingAttacked(fenForTest, isWhite))
                    {
                        safeSquares.Add(sq);
                    }
                }

                if (safeSquares.Count > 0)
                {
                    bestAction = safeSquares[random.Next(safeSquares.Count)];
                }
                else
                {
                    bestAction = squares[random.Next(squares.Count)];
                }
                dropType = type;
                isDrop = true;
            }
        }

        if (bestAction == null)
        {
            string fen = FenHelper.ToFen(state);
            var engineMove = facade.BestMove(fen, depth, timeMs);
            
            if (engineMove != null && state.LegalMoves.Contains(engineMove.Uci))
            {
                bestAction = engineMove.Uci;
            }
            else if (state.LegalMoves.Count > 0)
            {
                bestAction = state.LegalMoves[new Random().Next(state.LegalMoves.Count)];
            }
        }

        if (bestAction == null)
            throw new InvalidOperationException("No legal action available.");

        GameState newState;
        HistoryEntry histEntry;

        if (isDrop)
        {
            newState = VariantRules.SimulateDrop(state, dropType, bestAction);
            histEntry = new HistoryEntry(state.Turn, "drop", null, dropType, bestAction);
        }
        else
        {
            if (state.Mode == "variant")
            {
                newState = VariantRules.SimulateMove(state, bestAction, facade);
            }
            else
            {
                string fen = FenHelper.ToFen(state);
                string newFen = facade.ApplyMove(fen, bestAction);
                newState = FenHelper.FromFen(newFen, state);
            }
            histEntry = new HistoryEntry(state.Turn, "move", bestAction);
        }

        newState.TurnNumber = state.TurnNumber + 1;
        var history = new List<HistoryEntry>(state.History) { histEntry };
        newState.History = history;

        ComputeDerived(newState, facade);
        return newState;
    }

    private static void ComputeDerived(GameState state, EngineFacade facade)
    {
        if (state.Mode == "variant")
        {
            VariantRules.ComputeDerivedFields(state, facade);
        }
        else
        {
            string fen = FenHelper.ToFen(state);
            state.LegalMoves = facade.LegalMoves(fen).ToList();
            bool isWhiteTurn = state.Turn == "white";
            state.InCheck = facade.IsKingAttacked(fen, isWhiteTurn);

            if (state.LegalMoves.Count == 0)
            {
                if (state.InCheck)
                {
                    state.Status = "checkmate";
                    state.Winner = isWhiteTurn ? "black" : "white";
                }
                else
                {
                    state.Status = "stalemate";
                    state.Winner = null;
                }
            }
            else
            {
                state.Status = "active";
                state.Winner = null;
            }
            state.LegalDrops = new Dictionary<string, List<string>>();
        }
    }
}
