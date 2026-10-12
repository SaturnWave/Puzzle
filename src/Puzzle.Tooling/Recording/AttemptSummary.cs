using Puzzle.Core.Levels;
using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Recording;

/// <summary>
/// One version of a level at one move limit, as people played it; no bot is involved. <see cref="Play"/> covers the
/// comparable attempts and counts them exactly as the bot comparison does; the attempts it leaves out are only counted.
/// </summary>
/// <param name="ContentVersion">Which revision of the level's content the attempts were played on.</param>
/// <param name="LevelKey">The lab's name for the level file, from the latest lab record; absent for the released game's records.</param>
/// <param name="Play">The comparable attempts' figures, without bot rates (<see cref="ComparisonFigures.Bots"/> is empty).</param>
/// <param name="MedianWinMoves">The median moves of the comparable attempts won within the limit; absent without such a win.</param>
/// <param name="Assisted">Attempts left out because help shaped them.</param>
/// <param name="ChosenSeeds">Attempts left out because their seeds were chosen by hand.</param>
/// <param name="LastPlayedUtc">When the latest attempt ended.</param>
public sealed record LevelAttempts(
    LevelId Level,
    ContentVersion ContentVersion,
    string? LevelKey,
    int MoveLimit,
    ComparisonFigures Play,
    double? MedianWinMoves,
    int Assisted,
    int ChosenSeeds,
    DateTimeOffset LastPlayedUtc);

/// <param name="Levels">Each level version and move limit played, the most recently played first.</param>
/// <param name="Overall">Every comparable attempt, of every level, without bot rates.</param>
/// <param name="Assisted">Attempts left out of the comparison because help shaped them.</param>
/// <param name="ChosenSeeds">Attempts left out of the comparison because their seeds were chosen by hand.</param>
public sealed record AttemptSummary(IReadOnlyList<LevelAttempts> Levels, ComparisonFigures Overall, int Assisted, int ChosenSeeds)
{
    /// <summary>
    /// Sums <paramref name="records"/> up by level version and move limit: what the players did, before any bot plays.
    /// A level version is its id, its content version and, for lab records, its fingerprint, so a sample never merges
    /// with a generated level and attempts from before a patch never merge with attempts after it.
    /// </summary>
    public static AttemptSummary Of(IEnumerable<AttemptRecord> records)
    {
        AttemptRecord[] all = records.ToArray();
        LevelAttempts[] levels = all
            .GroupBy(record => (record.Start.Level, record.Start.ContentVersion, Fingerprint: record.Lab?.LevelFingerprint, record.Start.MoveLimit))
            .Select(group => SummarizeLevel(group.Key.Level, group.Key.ContentVersion, group.Key.MoveLimit, group.ToArray()))
            .OrderByDescending(level => level.LastPlayedUtc)
            .ToArray();
        ILookup<AttemptStanding, AttemptRecord> byStanding = all.ToLookup(AttemptCounting.Standing);
        return new AttemptSummary(
            levels,
            ComparisonFigures.Of(byStanding[AttemptStanding.Comparable].ToArray(), []),
            byStanding[AttemptStanding.Assisted].Count(),
            byStanding[AttemptStanding.ChosenSeeds].Count());
    }

    private static LevelAttempts SummarizeLevel(LevelId level, ContentVersion contentVersion, int moveLimit, IReadOnlyList<AttemptRecord> records)
    {
        ILookup<AttemptStanding, AttemptRecord> byStanding = records.ToLookup(AttemptCounting.Standing);
        AttemptRecord[] comparable = byStanding[AttemptStanding.Comparable].ToArray();
        AttemptRecord latest = records.MaxBy(record => record.Outcome.EndedUtc)
            ?? throw new ArgumentException("A level group holds at least one record.", nameof(records));
        string? key = records.Where(record => record.Lab is not null).MaxBy(record => record.Outcome.EndedUtc)?.Lab?.LevelKey;
        return new LevelAttempts(
            level,
            contentVersion,
            key,
            moveLimit,
            ComparisonFigures.Of(comparable, []),
            AttemptCounting.MedianWinMoves(comparable),
            byStanding[AttemptStanding.Assisted].Count(),
            byStanding[AttemptStanding.ChosenSeeds].Count(),
            latest.Outcome.EndedUtc);
    }
}
