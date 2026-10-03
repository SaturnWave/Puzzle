using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Recording;

namespace Puzzle.Web.Api;

/// <summary>
/// Endpoints of the lab's recordings panel: a scope's recorded attempts, and how they compare with the bots on the same
/// games — the logic of <c>puzzle humans</c>, run on the lab's own log and levels.
/// </summary>
internal static class RecordingsApi
{
    /// <summary>Level versions one listing shows at most, the most recently played.</summary>
    private const int MaxListedLevels = 50;

    /// <summary>
    /// Comparable attempts one comparison takes at most, those that ended last. With <see cref="MaxBotRuns"/> games for
    /// each of the four bot profiles, that bounds a comparison at 4,800 greedy games: about 90 thread-seconds at the 15–20
    /// ms a game <c>bench</c> measures, some seconds of wall time on a multi-core machine.
    /// </summary>
    private const int MaxComparedAttempts = 40;

    private const int DefaultBotRuns = 20;
    private const int MaxBotRuns = 30;

    private const string LoggerCategory = "Puzzle.Web.Recordings";

    public static void MapRecordingsApi(this WebApplication app)
    {
        RouteGroupBuilder recordings = app.MapGroup("/api/recordings");
        recordings.MapGet(string.Empty, ListRecordings);
        recordings.MapPost("/compare", CompareWithBotsAsync);
    }

    private static IResult ListRecordings(
        string? scope,
        [FromHeader(Name = PuzzleApi.DeviceHeader)] string? deviceHeader,
        AttemptLog log,
        ILoggerFactory loggers)
    {
        if (!DeviceId.TryCreate(deviceHeader, out DeviceId? device))
        {
            return PuzzleApi.DeviceRequired();
        }

        if (!RecordingsMapper.TryParseScope(scope, out RecordScope recordScope))
        {
            return ScopeRequired();
        }

        if (!TryRead(log, loggers.CreateLogger(LoggerCategory), out AttemptLogContents? contents))
        {
            return RecordsUnreadable();
        }

        AttemptRecord[] inScope = LabRecords.InScope(contents.Records, recordScope, device).ToArray();
        AttemptSummary summary = AttemptSummary.Of(inScope);
        return Results.Ok(RecordingsMapper.ToRecordings(recordScope, inScope.Length, contents.Problems.Count, summary, MaxListedLevels));
    }

    /// <summary>
    /// Plays the bots on the games of the scope's latest comparable attempts. One comparison runs at a time, off the
    /// request thread; a second is refused until the first ends.
    /// </summary>
    private static async Task<IResult> CompareWithBotsAsync(
        CompareRequest request,
        [FromHeader(Name = PuzzleApi.DeviceHeader)] string? deviceHeader,
        AttemptLog log,
        LevelLibrary library,
        ComparisonGate gate,
        ILoggerFactory loggers)
    {
        if (!DeviceId.TryCreate(deviceHeader, out DeviceId? device))
        {
            return PuzzleApi.DeviceRequired();
        }

        if (!RecordingsMapper.TryParseScope(request.Scope, out RecordScope scope))
        {
            return ScopeRequired();
        }

        if (!gate.TryEnter())
        {
            return Results.Json("Another comparison is running; try again when it ends.", statusCode: StatusCodes.Status429TooManyRequests);
        }

        try
        {
            ILogger logger = loggers.CreateLogger(LoggerCategory);
            if (!TryRead(log, logger, out AttemptLogContents? contents))
            {
                return RecordsUnreadable();
            }

            int runs = Math.Clamp(request.Runs ?? DefaultBotRuns, 1, MaxBotRuns);
            AttemptRecord[] inScope = LabRecords.InScope(contents.Records, scope, device).ToArray();
            IReadOnlyList<AttemptRecord> taken = LabRecords.ForComparison(inScope, MaxComparedAttempts);
            LibraryLevel[] levels = LevelsNamedBy(taken, library);
            var clock = Stopwatch.StartNew();
            HumanBotReport report = await Task.Run(() => HumanBotComparison.Compare(taken, levels.Select(entry => entry.Level).ToArray(), runs));
            Log.BotsCompared(logger, report.Records.Compared, runs, clock.ElapsedMilliseconds);
            int comparable = inScope.Count(record => AttemptCounting.Standing(record) == AttemptStanding.Comparable);
            return Results.Ok(RecordingsMapper.ToComparison(scope, runs, comparable, MaxComparedAttempts, report, levels, clock.ElapsedMilliseconds));
        }
        catch (LevelInitializationException ex)
        {
            return Results.UnprocessableEntity(ex.Message);
        }
        finally
        {
            gate.Exit();
        }
    }

    private static IResult ScopeRequired() => Results.BadRequest("Scope must be device or all.");

    private static IResult RecordsUnreadable() =>
        Results.Json("The attempt records could not be read; the lab's log says why.", statusCode: StatusCodes.Status500InternalServerError);

    /// <summary>Reads the whole log; a failure is logged here and reported to the caller as false.</summary>
    private static bool TryRead(AttemptLog log, ILogger logger, [NotNullWhen(true)] out AttemptLogContents? contents)
    {
        try
        {
            contents = log.ReadAll();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.RecordsNotRead(logger, log.Path, ex);
            contents = null;
            return false;
        }
    }

    /// <summary>The library levels whose ids <paramref name="records"/> name: the only ones the comparison can match.</summary>
    private static LibraryLevel[] LevelsNamedBy(IEnumerable<AttemptRecord> records, LevelLibrary library)
    {
        HashSet<LevelId> named = records.Select(record => record.Start.Level).ToHashSet();
        return library.Levels.Where(entry => named.Contains(entry.Level.Id)).ToArray();
    }
}
