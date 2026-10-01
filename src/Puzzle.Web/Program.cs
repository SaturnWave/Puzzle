using System.Text.Json.Serialization;
using Puzzle.Tooling;
using Puzzle.Web;
using Puzzle.Web.Api;
using Puzzle.Web.Sessions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string levelsPath = builder.Configuration["Puzzle:LevelsPath"] is { Length: > 0 } configured
    ? configured
    : LevelLibrary.FindLevelsDirectory(builder.Environment.ContentRootPath)
        ?? throw new DirectoryNotFoundException("No 'levels' directory found; set Puzzle:LevelsPath.");
int maxSessions = builder.Configuration.GetValue("Puzzle:MaxSessions", 200);
int idleMinutes = builder.Configuration.GetValue("Puzzle:SessionIdleMinutes", 30);

LevelLibrary library = LevelLibrary.Load(levelsPath);
builder.Services.AddSingleton(library);
builder.Services.AddMemoryCache(options => options.SizeLimit = maxSessions);
builder.Services.AddSingleton(provider => new GameSessionStore(
    provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    TimeSpan.FromMinutes(idleMinutes)));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

WebApplication app = builder.Build();
foreach (LibraryProblem problem in library.Problems)
{
    Log.LevelSkipped(app.Logger, problem.Path, problem.Message);
}

Log.LevelsLoaded(app.Logger, library.Levels.Count, levelsPath);
app.UseDefaultFiles();
// The lab changes often: make browsers (and phones on the LAN) revalidate instead of serving stale files.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});
app.MapPuzzleApi();
app.Run();
