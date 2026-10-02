/// <summary>
/// The complete game state exchanged between client and server.
/// Fields marked "derived" are recomputed by the server and ignored on input.
/// </summary>
public class GameState
{
    public string Mode { get; set; } = "classic";
    public VariantConfig Config { get; set; } = new();
    public PieceInfo?[] Board { get; set; } = new PieceInfo?[64];
    public string Turn { get; set; } = "white";
    public string Castling { get; set; } = "KQkq";
    public string? EnPassant { get; set; }
    public int HalfmoveClock { get; set; }
    public int Fullmove { get; set; } = 1;
    public int TurnNumber { get; set; }
    public Dictionary<string, int> Points { get; set; } = new() { ["white"] = 0, ["black"] = 0 };
    public List<HistoryEntry> History { get; set; } = new();
    public List<GameEvent> Events { get; set; } = new();
    // derived
    public string Status { get; set; } = "active";
    public string? Winner { get; set; }
    public bool InCheck { get; set; }
    public List<string> LegalMoves { get; set; } = new();
    public Dictionary<string, List<string>> LegalDrops { get; set; } = new();
}

public record PieceInfo(string P, int? Left);

public record HistoryEntry(string Side, string Type, string? Uci = null, string? Piece = null, string? Square = null);

public record GameEvent(string Type, string? Square = null, string? Piece = null, string? Side = null);

/// <summary>
/// Variant-mode configuration. Always present in GameState (unused in classic mode).
/// Defaults match plan section 9.1.
/// </summary>
public class VariantConfig
{
    public int PointsPerTurn { get; set; } = 30;
    public int StartingPoints { get; set; } = 0;
    public Dictionary<string, int> Lifetimes { get; set; } = new()
    {
        ["pawn"] = 15, ["knight"] = 10, ["bishop"] = 10, ["rook"] = 8, ["queen"] = 5
    };
    public Dictionary<string, int> Costs { get; set; } = new()
    {
        ["pawn"] = 10, ["knight"] = 30, ["bishop"] = 30, ["rook"] = 50, ["queen"] = 80
    };
    public Dictionary<string, int[]> DropRanks { get; set; } = new()
    {
        ["white"] = [1, 2], ["black"] = [7, 8]
    };
    public Dictionary<string, int[]> PawnDropRanks { get; set; } = new()
    {
        ["white"] = [2], ["black"] = [7]
    };
    public bool DropConsumesTurn { get; set; } = true;
    public bool PromotionResetsLifetime { get; set; } = true;
    public int MaxTurns { get; set; } = 300;
}
