using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Api;
using Puzzle.Web.Recording;
using Puzzle.Web.Security;
using Puzzle.Web.Sessions;

namespace Puzzle.Web;

/// <summary>Builds the web app from its configuration: the test lab, or with <c>Puzzle:Mode=Public</c> the public mode around it.</summary>
public static class PuzzleApp
{
    /// <param name="args">The command line; its settings outrank the environment's and the settings file's.</param>
    /// <exception cref="InvalidOperationException">A setting is unusable — the file of the ready-made accounts too —, or the public level set cannot be played through; the message says which and why.</exception>
    /// <exception cref="DirectoryNotFoundException">The levels, the recordings folder or the player page cannot be found.</exception>
    /// <exception cref="Puzzle.Tooling.Players.PlayerStoreInUseException">Public mode: another process has the player store open.</exception>
    /// <exception cref="Puzzle.Tooling.Players.PlayerStoreFormatException">Public mode: the player store's file is damaged.</exception>
    /// <exception cref="IOException">Public mode: the player store could not be read, or a ready-made account could not be written to it.</exception>
    public static WebApplication Build(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        string levelsPath = builder.Configuration["Puzzle:LevelsPath"] is { Length: > 0 } configured
            ? configured
            : LevelLibrary.FindLevelsDirectory(builder.Environment.ContentRootPath)
                ?? throw new DirectoryNotFoundException("No 'levels' directory found; set Puzzle:LevelsPath.");
        int maxSessions = builder.Configuration.GetValue("Puzzle:MaxSessions", 200);
        int idleMinutes = builder.Configuration.GetValue("Puzzle:SessionIdleMinutes", 30);

        // By default in the repository root, however the lab is started, so every run feeds the one log `puzzle humans` reads.
        // A configured path is relative to the content root; an absolute one is used as is.
        string recordingsPath = builder.Configuration["Puzzle:RecordingsPath"] is { Length: > 0 } recordings
            ? Path.Combine(builder.Environment.ContentRootPath, recordings)
            : AttemptLog.FindDefaultPath(builder.Environment.ContentRootPath)
                ?? throw new DirectoryNotFoundException("No repository root (a folder with 'levels') found; set Puzzle:RecordingsPath.");

        PublicSettings? publicSettings = PublicSettings.ReadMode(builder.Configuration) == ServerMode.Public
            ? PublicSettings.Read(builder.Configuration, builder.Environment.ContentRootPath)
            : null;

        LevelLibrary library = LevelLibrary.Load(levelsPath);
        var attemptLog = new AttemptLog(recordingsPath);
        AttemptLogContents recorded = attemptLog.ReadAll();
        builder.Services.AddSingleton(library);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(attemptLog);
        builder.Services.AddSingleton(new RetryNumbers(recorded.Records));
        builder.Services.AddSingleton<AttemptRecorder>();
        builder.Services.AddSingleton<ComparisonGate>();
        builder.Services.AddSingleton(_ => new MemoryCache(new MemoryCacheOptions { SizeLimit = maxSessions }));
        builder.Services.AddSingleton(provider => new GameSessionStore(
            provider.GetRequiredService<MemoryCache>(),
            TimeSpan.FromMinutes(idleMinutes),
            provider.GetRequiredService<AttemptRecorder>(),
            provider.GetRequiredService<TimeProvider>()));
        builder.Services.AddHostedService<IdleSessionSweeper>();
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        if (publicSettings is not null)
        {
            builder.AddPublicMode(publicSettings, library);
        }

        WebApplication app = builder.Build();
        foreach (LibraryProblem problem in library.Problems)
        {
            Log.LevelSkipped(app.Logger, problem.Path, problem.Message);
        }

        Log.LevelsLoaded(app.Logger, library.Levels.Count, levelsPath);
        foreach (AttemptLogProblem problem in recorded.Problems)
        {
            Log.RecordSkipped(app.Logger, problem.Line, problem.Message);
        }

        Log.RecordingTo(app.Logger, attemptLog.Path, recorded.Records.Count);

        // The lab's sessions vanish with the process: record the attempts still being played as abandoned at their last
        // activity, then make a last try at the records the log refused before. (A player's attempt is in the player
        // store and goes on after the restart.)
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            app.Services.GetRequiredService<GameSessionStore>().RecordUnfinishedAttempts();
            app.Services.GetRequiredService<AttemptRecorder>().RetryWaitingBeforeStop();
        });

        if (publicSettings is null)
        {
            UseLabMode(app);
        }
        else
        {
            app.UsePublicMode(publicSettings);
        }

        return app;
    }

    /// <summary>
    /// The lab as it always was: its page at the site's root and its API, open to whoever reaches the server directly.
    /// What comes through a tunnel or a proxy is refused before anything else looks at it (<see cref="DirectOnly"/>).
    /// </summary>
    private static void UseLabMode(WebApplication app)
    {
        app.UseMiddleware<DirectOnly>();
        app.UseDefaultFiles();

        // The lab changes often: make browsers (and phones on the LAN) revalidate instead of serving stale files.
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });
        app.MapPuzzleApi();
        app.MapRecordingsApi();
    }
}
