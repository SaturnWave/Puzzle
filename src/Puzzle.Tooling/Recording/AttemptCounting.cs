using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Recording;

/// <summary>Whether a recorded attempt can stand next to the bots' games, and when it cannot, why.</summary>
public enum AttemptStanding
{
    /// <summary>The person's own play on a retry drawn from the level's attempt policy: a bot can play the same game.</summary>
    Comparable,

    /// <summary>Help shaped the attempt: the lab's tools (DDA assist, hints, the solver, bot moves) or boosters.</summary>
    Assisted,

    /// <summary>
    /// No retry number: seeds chosen by hand, or an attempt the lab found outside its device's sequence of retries (a
    /// replay past the seed pool, a device the browser does not keep). Either may replay a game already seen.
    /// </summary>
    ChosenSeeds,
}

/// <summary>How an attempt stands at its move limit.</summary>
public enum LimitVerdict
{
    Won,
    Lost,

    /// <summary>Abandoned before the limit was used up: how it would have ended is unknown.</summary>
    Undecided,
}

/// <summary>
/// The rules by which recorded attempts are counted (docs/GAME_DESIGN.md §20), shared by the lab's own summary and the
/// comparison with the bots, so the two always count an attempt the same way.
/// </summary>
public static class AttemptCounting
{
    /// <summary>The standing of <paramref name="record"/>; help outranks hand-chosen seeds.</summary>
    public static AttemptStanding Standing(AttemptRecord record)
    {
        bool helped = record.Lab?.IsAssisted == true || record.Outcome.BoostersUsed.Count > 0;
        if (helped)
        {
            return AttemptStanding.Assisted;
        }

        return record.Start.Attempt is null ? AttemptStanding.ChosenSeeds : AttemptStanding.Comparable;
    }

    /// <summary>
    /// A win after buying extra moves is a loss at the limit, and so is an attempt abandoned only after it used the limit
    /// up. An attempt abandoned earlier is undecided.
    /// </summary>
    public static LimitVerdict AtLimit(AttemptRecord record)
    {
        bool withinLimit = record.Outcome.MovesUsed <= record.Start.MoveLimit;
        bool limitUsedUp = record.Outcome.MovesUsed >= record.Start.MoveLimit;
        return record.Outcome.Result switch
        {
            AttemptResult.Won => withinLimit ? LimitVerdict.Won : LimitVerdict.Lost,
            AttemptResult.OutOfMoves or AttemptResult.NoMovesPossible => LimitVerdict.Lost,
            AttemptResult.Abandoned => limitUsedUp ? LimitVerdict.Lost : LimitVerdict.Undecided,
            _ => throw new ArgumentOutOfRangeException(nameof(record), record.Outcome.Result, "Unknown attempt result."),
        };
    }

    /// <summary>Whether the attempt was decided at its move limit, won or lost, rather than abandoned undecided.</summary>
    public static bool IsDecided(AttemptRecord record) => AtLimit(record) != LimitVerdict.Undecided;

    /// <summary>The median of the moves the attempts won within their limit used; absent without such a win.</summary>
    public static double? MedianWinMoves(IEnumerable<AttemptRecord> records)
    {
        int[] sorted = records
            .Where(record => AtLimit(record) == LimitVerdict.Won)
            .Select(record => record.Outcome.MovesUsed)
            .Order()
            .ToArray();
        return sorted.Length switch
        {
            0 => null,
            int count when count % 2 == 1 => sorted[count / 2],
            int count => (sorted[(count / 2) - 1] + sorted[count / 2]) / 2.0,
        };
    }
}
