/// <summary>
/// Maps all game-related API endpoints.
/// Every endpoint is stateless: the client sends the full GameState, the server recomputes derived fields.
/// </summary>
public static class GameEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/new",  HandleNewGame);
        app.MapPost("/api/move", HandleMove);
        app.MapPost("/api/bot",  HandleBot);
    }

    // ─── POST /api/new ───────────────────────────────────────────

    private static IResult HandleNewGame(NewGameRequest req, EngineFacade facade)
    {
        string mode = req.Mode ?? "classic";
        if (mode is not ("classic" or "variant"))
            return Results.BadRequest(new ErrorResponse("Mode must be 'classic' or 'variant'."));

        var state = new GameState
        {
            Mode     = mode,
            Config   = req.Config ?? new VariantConfig(),
            Board    = FenHelper.StartingBoard(),
            Turn     = "white",
            Castling = "KQkq",
            Fullmove = 1,
        };

        facade.ResetSearchState();
        ComputeDerivedFields(state, facade);

        return Results.Ok(state);
    }

    // ─── POST /api/move ──────────────────────────────────────────

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

        string fen = FenHelper.ToFen(state);

        string newFen;
        try
        {
            newFen = facade.ApplyMove(fen, uci);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }

        var newState = FenHelper.FromFen(newFen, state);
        newState.TurnNumber = state.TurnNumber + 1;

        // Append to history
        var history = new List<HistoryEntry>(state.History);
        history.Add(new HistoryEntry(state.Turn, "move", uci));
        newState.History = history;
        newState.Events = new List<GameEvent>();

        ComputeDerivedFields(newState, facade);

        return Results.Ok(newState);
    }

    // ─── POST /api/bot ───────────────────────────────────────────

    private static IResult HandleBot(BotRequest req, EngineFacade facade)
    {
        var err = StateValidator.Validate(req.State);
        if (err != null) return Results.BadRequest(new ErrorResponse(err));

        var state = req.State;
        if (state.Status != "active")
            return Results.BadRequest(new ErrorResponse("The game is already over."));

        int depth  = Math.Clamp(req.Depth, 1, 10);
        long timeMs = Math.Clamp(req.TimeMs, 50, 3000);

        string fen = FenHelper.ToFen(state);
        var bestMove = facade.BestMove(fen, depth, timeMs);

        if (bestMove == null)
            return Results.BadRequest(new ErrorResponse("No legal move available."));

        string newFen;
        try
        {
            newFen = facade.ApplyMove(fen, bestMove.Uci);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse($"Engine error: {ex.Message}"));
        }

        var newState = FenHelper.FromFen(newFen, state);
        newState.TurnNumber = state.TurnNumber + 1;

        var history = new List<HistoryEntry>(state.History);
        history.Add(new HistoryEntry(state.Turn, "move", bestMove.Uci));
        newState.History = history;
        newState.Events = new List<GameEvent>();

        ComputeDerivedFields(newState, facade);

        return Results.Ok(new BotResponse(newState, bestMove.Uci));
    }

    // ─── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Recomputes all server-derived fields: legalMoves, inCheck, status, winner, legalDrops.
    /// </summary>
    private static void ComputeDerivedFields(GameState state, EngineFacade facade)
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

        // Drops are only available in variant mode (Phase 2)
        state.LegalDrops = new Dictionary<string, List<string>>();
    }
}
