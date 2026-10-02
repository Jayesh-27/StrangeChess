// ─── Requests ────────────────────────────────────────────────────

public record NewGameRequest(string? Mode, VariantConfig? Config);

public record MoveRequest(GameState State, string Uci);

public record BotRequest(GameState State, int Depth, long TimeMs);

// ─── Responses ───────────────────────────────────────────────────

public record BotResponse(GameState State, string Action);

public record ErrorResponse(string Error);
