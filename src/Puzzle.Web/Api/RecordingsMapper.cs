using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Players;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Recording;

namespace Puzzle.Web.Api;

/// <summary>Translates the recordings' summaries and comparisons into the browser contract.</summary>
internal static class RecordingsMapper
{
    /// <summary>The scope a request names: "device" (also when it names none) or "all"; false for anything else.</summary>
    public static bool TryParseScope(string? text, out RecordScope scope)
    {
        switch (text ?? "device")
        {
            case "device":
                scope = RecordScope.Device;
                return true;
            case "all":
                scope = RecordScope.All;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    /// <param name="records">Records in the scope.</param>
    /// <param name="unreadableLines">Lines of the whole log that are not valid records.</param>
    /// <param name="maxLevels">Level versions listed at most, the most recently played.</param>
    /// <param name="players">The public mode's players by username; absent in the lab mode and for a single device's scope.</param>
    public static RecordingsDto ToRecordings(
        RecordScope scope,
        int records,
        int unreadableLines,
        AttemptSummary summary,
        int maxLevels,
        PlayerAttemptsDto[]? players) => new(
        ScopeName(scope),
        records,
        unreadableLines,
        ToPlay(summary.Overall),
        summary.Assisted,
        summary.ChosenSeeds,
        summary.Levels.Count,
        summary.Levels.Take(maxLevels).Select(ToLevel).ToArray(),
        players);

    /// <summary>
    /// Each account with its own attempts, the player who played last first. A player's attempts are the records that
    /// carry their player id; the figures count the comparable ones, as the rest of the panel does.
    /// </summary>
    public static PlayerAttemptsDto[] ToPlayers(IReadOnlyList<PlayerAccount> accounts, IReadOnlyList<AttemptRecord> records)
    {
        ILookup<DeviceId, AttemptRecord> byPlayer = records.ToLookup(record => record.Start.Device);
        return accounts
            .Select(account => ToPlayer(account, byPlayer[account.PlayerId].ToArray()))
            .OrderByDescending(player => player.LastPlayedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(player => player.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <param name="comparable">Comparable attempts in the scope, compared or not.</param>
    /// <param name="comparedLimit">Comparable attempts one comparison takes at most.</param>
    /// <param name="levels">The library levels the comparison matched records against, to name its rows.</param>
    public static ComparisonDto ToComparison(
        RecordScope scope,
        int botRuns,
        int comparable,
        int comparedLimit,
        HumanBotReport report,
        IReadOnlyList<LibraryLevel> levels,
        long elapsedMs)
    {
        RecordTally tally = report.Records;
        return new ComparisonDto(
            ScopeName(scope),
            botRuns,
            comparable,
            comparedLimit,
            new TallyDto(tally.Compared, tally.Assisted, tally.ChosenSeeds, tally.UnknownLevel),
            report.Bots.ToArray(),
            ToRow(level: null, medianWinMoves: null, report.Overall),
            report.Levels.Select(row => ToRow(new LevelRow(row.Level.Value, KeyOf(row, levels), row.MoveLimit), row.MedianWinMoves, row.Figures)).ToArray(),
            elapsedMs);
    }

    private static string ScopeName(RecordScope scope) => scope switch
    {
        RecordScope.Device => "device",
        RecordScope.All => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown record scope."),
    };

    private static PlayDto ToPlay(ComparisonFigures figures) => new(
        figures.Finished,
        figures.Abandoned,
        figures.Wins,
        figures.Devices,
        figures.HumanWinRate,
        figures.HumanWinRateLowerBound);

    private static PlayerAttemptsDto ToPlayer(PlayerAccount account, AttemptRecord[] attempts)
    {
        AttemptRecord[] comparable = attempts.Where(attempt => AttemptCounting.Standing(attempt) == AttemptStanding.Comparable).ToArray();
        return new PlayerAttemptsDto(
            account.Username.Value,
            account.Progress.LevelsWon,
            attempts.Length,
            ToPlay(ComparisonFigures.Of(comparable, [])),
            attempts.Length > 0 ? attempts.Max(attempt => attempt.Outcome.EndedUtc) : null);
    }

    private static LevelAttemptsDto ToLevel(LevelAttempts level) => new(
        level.Level.Value,
        level.LevelKey,
        level.MoveLimit,
        ToPlay(level.Play),
        level.MedianWinMoves,
        level.Assisted,
        level.ChosenSeeds,
        level.LastPlayedUtc);

    /// <param name="level">The row's level; absent on the overall row.</param>
    private static ComparisonRowDto ToRow(LevelRow? level, double? medianWinMoves, ComparisonFigures figures) => new(
        level?.Id,
        level?.Key,
        level?.MoveLimit,
        medianWinMoves,
        ToPlay(figures),
        figures.Bots.Select(bot => new BotRateDto(bot.Bot, bot.WinRateOnFinished, bot.WinRateOnAll)).ToArray(),
        figures.ClosestBot,
        figures.ClosestBotAtLowerBound,
        figures.AbandonsDecideClosest);

    /// <summary>The library's name for the level version a row compares; absent when the library no longer has it.</summary>
    private static string? KeyOf(LevelComparison row, IReadOnlyList<LibraryLevel> levels) => levels
        .Where(entry => entry.Level.Id == row.Level)
        .FirstOrDefault(entry => LevelJson.Fingerprint(entry.Level) == row.LevelFingerprint)
        ?.Key;

    private sealed record LevelRow(int Id, string? Key, int MoveLimit);
}
