var builder = WebApplication.CreateBuilder(args);

// Port: Render sets PORT env var; fallback to 5080 for local dev
var port = Environment.GetEnvironmentVariable("PORT") ?? "5080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<EngineFacade>();

var app = builder.Build();

// Static files for production (built client in wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => "ok");
GameEndpoints.Map(app);

// SPA fallback: only when wwwroot/index.html exists (production build)
var wwwroot = app.Environment.WebRootPath;
if (!string.IsNullOrEmpty(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html")))
{
    app.MapFallbackToFile("index.html");
}

app.Run();
