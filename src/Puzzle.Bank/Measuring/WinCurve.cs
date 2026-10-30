namespace Puzzle.Bank.Measuring;

/// <summary>
/// How many of a set of stored games were won within each move limit. The greedy bots never look at the moves left,
/// so a game that finished on move k is a win under every limit of k or more: the finishing moves alone give the
/// whole curve, and the limit for a win share is a look-up.
/// </summary>
public sealed class WinCurve
{
    private readonly int[] _wonWithin;

    private WinCurve(int[] wonWithin, int games, int stuck)
    {
        _wonWithin = wonWithin;
        Games = games;
        Stuck = stuck;
    }

    public int Games { get; }

    /// <summary>Games that ran out of legal moves; they count as lost under every limit.</summary>
    public int Stuck { get; }

    /// <summary>The games were followed this far; no share is known past it.</summary>
    public int MoveCap => _wonWithin.Length - 1;

    /// <param name="finishes">Finishing moves as <see cref="RungGames"/> stores them.</param>
    public static WinCurve Of(ReadOnlySpan<byte> finishes, int moveCap)
    {
        var wonWithin = new int[moveCap + 1];
        int stuck = 0;
        foreach (byte finish in finishes)
        {
            if (finish == RungGames.Stuck)
            {
                stuck++;
            }
            else if (finish != RungGames.StillPlaying && finish <= moveCap)
            {
                wonWithin[finish]++;
            }
        }

        for (int limit = 1; limit <= moveCap; limit++)
        {
            wonWithin[limit] += wonWithin[limit - 1];
        }

        return new WinCurve(wonWithin, finishes.Length, stuck);
    }

    /// <summary>The share of the games won within <paramref name="limit"/> moves; past the cap, the share at the cap.</summary>
    public double ShareAt(int limit) => limit < 1 ? 0 : _wonWithin[Math.Min(limit, MoveCap)] / (double)Games;

    /// <summary>
    /// The smallest limit at which the win share reaches <paramref name="share"/>; absent when it never does within
    /// the cap. The same reading as <c>LevelReport.MoveLimitFor</c> gives on the same games.
    /// </summary>
    public int? LimitFor(double share)
    {
        int needed = Math.Clamp((int)Math.Ceiling(share * Games), 1, Games);
        for (int limit = 1; limit <= MoveCap; limit++)
        {
            if (_wonWithin[limit] >= needed)
            {
                return limit;
            }
        }

        return null;
    }
}

/// <summary>
/// When stuck games make a candidate unusable. A fixed "not one stuck game" would turn the same board down more
/// often the more it is measured, so the rule is about the rate: a candidate is unusable when its games leave it
/// 95 % sure that more than one game in 5,000 gets stuck.
/// </summary>
public static class StuckRule
{
    public const double ToleratedRate = 1.0 / 5000;

    /// <summary>The lower 95 % confidence bound of a Poisson mean after seeing that many events, for 0..10 events.</summary>
    private static readonly double[] LowerBounds = [0, 0.0513, 0.355, 0.818, 1.366, 1.970, 2.613, 3.285, 3.981, 4.695, 5.425];

    public static bool Tolerates(int stuck, int games) => LowerBound(stuck) / Math.Max(1, games) <= ToleratedRate;

    /// <summary>Past the table the normal approximation is close enough: such a board is far beyond the rate anyway.</summary>
    private static double LowerBound(int events) =>
        events < LowerBounds.Length ? LowerBounds[events] : events - (1.645 * Math.Sqrt(events));
}
