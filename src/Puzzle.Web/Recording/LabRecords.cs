using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Web.Recording;

/// <summary>Whose recorded attempts the lab's recordings panel shows.</summary>
public enum RecordScope
{
    /// <summary>The attempts of the device asking.</summary>
    Device,

    /// <summary>Every device's attempts.</summary>
    All,
}

/// <summary>Picks the recorded attempts the lab shows and compares.</summary>
public static class LabRecords
{
    /// <summary>The records <paramref name="scope"/> covers for <paramref name="device"/>.</summary>
    public static IEnumerable<AttemptRecord> InScope(IEnumerable<AttemptRecord> records, RecordScope scope, DeviceId device) => scope switch
    {
        RecordScope.Device => records.Where(record => record.Start.Device == device),
        RecordScope.All => records,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown record scope."),
    };

    /// <summary>
    /// The records a bounded comparison takes: every record left out of the comparison anyway (they only add to its tally),
    /// but of the comparable ones, which each cost the bots games, only the <paramref name="maxComparable"/> that ended last.
    /// </summary>
    public static IReadOnlyList<AttemptRecord> ForComparison(IReadOnlyList<AttemptRecord> records, int maxComparable)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxComparable);
        IEnumerable<AttemptRecord> latestComparable = records
            .Where(IsComparable)
            .OrderByDescending(record => record.Outcome.EndedUtc)
            .Take(maxComparable);
        return records.Where(record => !IsComparable(record)).Concat(latestComparable).ToArray();
    }

    private static bool IsComparable(AttemptRecord record) => AttemptCounting.Standing(record) == AttemptStanding.Comparable;
}
