using Puzzle.Core.Levels;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>
/// Counts whole numbers from 0 up to a bound; anything at or past the bound is counted together, so the histogram
/// has the same size however long a run is. A quantile that falls among those is unknown.
/// </summary>
public sealed class Histogram
{
    private readonly long[] _counts;

    /// <param name="bound">Values from 0 to one below this are counted one by one.</param>
    public Histogram(int bound) => _counts = new long[bound + 1];

    public long Count { get; private set; }

    private int Bound => _counts.Length - 1;

    public void Add(int value)
    {
        _counts[Math.Clamp(value, 0, Bound)]++;
        Count++;
    }

    public void Add(Histogram other)
    {
        for (int value = 0; value < _counts.Length; value++)
        {
            _counts[value] += other._counts[value];
        }

        Count += other.Count;
    }

    /// <summary>The value that <paramref name="percent"/> of the counted ones do not exceed (nearest rank); absent when nothing was counted, or the value lies at or past the bound.</summary>
    public int? Quantile(Percent percent) => QuantileOrLater(Count, percent);

    /// <summary>
    /// The same quantile over <paramref name="outOf"/> players of whom only the counted ones have a value: the others
    /// count as larger than everyone. Absent when the quantile falls among them, or at or past the bound.
    /// </summary>
    public int? QuantileOrLater(long outOf, Percent percent)
    {
        long rank = Math.Max(1, (long)Math.Ceiling(percent.Value / 100.0 * outOf));
        long seen = 0;
        for (int value = 0; value < Bound; value++)
        {
            seen += _counts[value];
            if (seen >= rank && outOf > 0)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>What players of one rung did on one level.</summary>
public sealed class LevelTally
{
    /// <summary>Attempts needed are counted one by one below this.</summary>
    public const int AttemptsCountedOneByOne = 100;

    /// <summary>Players who won the level.</summary>
    public int Winners { get; private set; }

    /// <summary>Players whose journey ended on this level, attempted and unwon.</summary>
    public int PlayersLeftOnIt { get; private set; }

    /// <summary>The attempts the winners needed, summed.</summary>
    public long AttemptsOfWinners { get; private set; }

    /// <summary>The attempts each winner needed.</summary>
    public Histogram AttemptsNeeded { get; } = new(AttemptsCountedOneByOne);

    public TimeSpan PlayTimeOfWinners { get; private set; }

    /// <summary>Winners for whom a day or more passed between their first attempt of the level and the win.</summary>
    public int WinnersWhoTookOverADay { get; private set; }

    /// <summary>Every attempt of the level, the unwon ones of players left on it included.</summary>
    public long Attempts { get; private set; }

    public long AttemptsWithExtraMoves { get; private set; }

    public long AttemptsWithAStartBooster { get; private set; }

    /// <summary>Winners who needed more attempts than the level's pool has seeds.</summary>
    public int WinnersWhoMetASeedTwice { get; private set; }

    /// <summary>Players who played the level: those who won it and those left on it.</summary>
    public int Players => Winners + PlayersLeftOnIt;

    /// <summary>
    /// The attempts a player spent on the level, the unwon ones of players left on it included; absent when nobody
    /// played it. A level nobody passes shows here as it is, where a mean over winners alone would have nothing to say.
    /// </summary>
    public double? AttemptsPerPlayer => Players == 0 ? null : Attempts / (double)Players;

    internal void CountAttempt(AttemptPlayed attempt)
    {
        Attempts++;
        if (attempt.Reading.ExtraMoves.IsTaken)
        {
            AttemptsWithExtraMoves++;
        }

        if (attempt.StartBooster.Taken is not null)
        {
            AttemptsWithAStartBooster++;
        }
    }

    internal void CountWin(int attemptsNeeded, TimeSpan playTime, TimeSpan sinceFirstAttempt, bool metASeedTwice)
    {
        Winners++;
        AttemptsOfWinners += attemptsNeeded;
        AttemptsNeeded.Add(attemptsNeeded);
        PlayTimeOfWinners += playTime;
        if (sinceFirstAttempt >= TimeSpan.FromDays(1))
        {
            WinnersWhoTookOverADay++;
        }

        if (metASeedTwice)
        {
            WinnersWhoMetASeedTwice++;
        }
    }

    internal void CountLeftOnIt() => PlayersLeftOnIt++;

    /// <summary>Adds what another group of players did on the same level.</summary>
    internal void Add(LevelTally other)
    {
        Winners += other.Winners;
        PlayersLeftOnIt += other.PlayersLeftOnIt;
        AttemptsOfWinners += other.AttemptsOfWinners;
        AttemptsNeeded.Add(other.AttemptsNeeded);
        PlayTimeOfWinners += other.PlayTimeOfWinners;
        WinnersWhoTookOverADay += other.WinnersWhoTookOverADay;
        Attempts += other.Attempts;
        AttemptsWithExtraMoves += other.AttemptsWithExtraMoves;
        AttemptsWithAStartBooster += other.AttemptsWithAStartBooster;
        WinnersWhoMetASeedTwice += other.WinnersWhoMetASeedTwice;
    }
}

/// <summary>What is tallied for a group of players across all their journeys: level by level, and how their sessions and attempts ran.</summary>
public sealed class GroupTallies
{
    private const int AttemptsPerSessionCountedOneByOne = 100;
    private const int SecondsPerAttemptCountedOneByOne = 1800;

    private readonly LevelTally[] _levels;

    public GroupTallies(int levelCount) => _levels = Enumerable.Range(0, levelCount).Select(_ => new LevelTally()).ToArray();

    /// <summary>Attempts per session, of the sessions in which something was played.</summary>
    public Histogram AttemptsPerSession { get; } = new(AttemptsPerSessionCountedOneByOne);

    /// <summary>The seconds the moves of an attempt took, without pauses and ads: what a recording of an attempt measures.</summary>
    public Histogram PlaySecondsPerAttempt { get; } = new(SecondsPerAttemptCountedOneByOne);

    public int LevelCount => _levels.Length;

    public LevelTally Of(LevelId level) => _levels[level.Value - 1];

    /// <summary>The tallies of several groups as one group.</summary>
    public static GroupTallies Sum(IReadOnlyCollection<GroupTallies> groups)
    {
        var sum = new GroupTallies(groups.First().LevelCount);
        foreach (GroupTallies group in groups)
        {
            sum.AttemptsPerSession.Add(group.AttemptsPerSession);
            sum.PlaySecondsPerAttempt.Add(group.PlaySecondsPerAttempt);
            for (int level = 0; level < sum._levels.Length; level++)
            {
                sum._levels[level].Add(group._levels[level]);
            }
        }

        return sum;
    }
}
