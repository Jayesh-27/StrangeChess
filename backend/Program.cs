using StrangeChess.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EngineGame>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/game", (EngineGame game) => game.GetState());
app.MapPost("/api/game/reset", (EngineGame game, ResetRequest? request) =>
{
    try
    {
        return Results.Ok(game.Reset(request?.Fen));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
app.MapPost("/api/game/move", (EngineGame game, MoveRequest request) =>
{
    var result = game.PlayHumanMove(request.Uci);
    return result.Error is null ? Results.Ok(result.State) : Results.BadRequest(new { error = result.Error });
});
app.MapPost("/api/game/engine", (EngineGame game, EngineRequest? request) =>
{
    var result = game.PlayEngineMove(request?.Depth ?? 5, request?.TimeMs ?? 1000);
    return result.Error is null ? Results.Ok(result.State) : Results.BadRequest(new { error = result.Error });
});
app.MapPost("/api/game/edit", (EngineGame game, EditRequest request) =>
{
    try
    {
        return Results.Ok(game.EditSquare(request.Square, request.Piece));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapFallbackToFile("index.html");
app.Run();

public record MoveRequest(string Uci);
public record ResetRequest(string? Fen);
public record EngineRequest(int? Depth, long? TimeMs);
public record EditRequest(int Square, string? Piece);
