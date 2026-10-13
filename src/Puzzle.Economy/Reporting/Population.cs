using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>A group of simulated players a report has rows for: one rung on its own, or the whole mix.</summary>
/// <param name="IsAverage">The rung the settings name as the average player.</param>
public sealed record Population(string Name, IReadOnlyList<SkillRung> Rungs, bool IsAverage)
{
    public const string MixName = "mix";

    /// <summary>Each rung of the mix on its own, weakest first, then the mix when it has more than one rung.</summary>
    public static IReadOnlyList<Population> Of(PopulationSettings settings)
    {
        SkillRung[] rungs = settings.Rungs.ToArray();
        IEnumerable<Population> alone = rungs.Select(rung => new Population(rung.Name, [rung], rung == settings.AverageRung));
        return rungs.Length > 1 ? [.. alone, new Population(MixName, rungs, IsAverage: false)] : [.. alone];
    }

    public IReadOnlyList<PlayerOutcome> PlayersIn(PassResult pass) => pass.Players.Where(player => Rungs.Contains(player.Player.Rung)).ToArray();

    public GroupTallies TalliesIn(PassResult pass) => Rungs.Count == 1
        ? pass.ByRung[Rungs[0]]
        : GroupTallies.Sum(Rungs.Select(rung => pass.ByRung[rung]).ToArray());

    /// <summary>The share of this population's players each of its rungs has; the shares sum to 1.</summary>
    public IReadOnlyDictionary<SkillRung, double> SharesIn(PopulationSettings settings)
    {
        MixShare[] parts = settings.SkillMix.Where(part => Rungs.Contains(part.Rung)).ToArray();
        double all = parts.Sum(part => part.Share.Value);
        return parts.ToDictionary(part => part.Rung, part => part.Share.Value / all);
    }
}

/// <summary>The statistics of the reports. Shares and quantiles are statistics and may be fractions; coins never are.</summary>
public static class Statistics
{
    /// <summary>The two-sided 90 % point of the normal distribution.</summary>
    private const double Z90 = 1.6448536269514722;

    public static Percent Median { get; } = new(50);

    /// <summary>The lower end of the band every report gives around a median: nine players in ten lie above it.</summary>
    public static Percent BandLow { get; } = new(10);

    /// <summary>The upper end of the band: nine players in ten lie at or below it.</summary>
    public static Percent BandHigh { get; } = new(90);

    /// <summary>
    /// The nearest-rank quantile over <paramref name="outOf"/> players of whom only some have a value: the others
    /// count as later, or larger, than everyone who has one. Absent when the quantile falls among those others, or
    /// when there are no players.
    /// </summary>
    public static T? QuantileOrLater<T>(IEnumerable<T> ofThoseWithOne, int outOf, Percent percent)
        where T : struct, IComparable<T>
    {
        T[] sorted = [.. ofThoseWithOne.Order()];
        int rank = Math.Max(1, (int)Math.Ceiling(percent.Value / 100.0 * outOf));
        return outOf > 0 && rank <= sorted.Length ? sorted[rank - 1] : null;
    }

    /// <summary>The nearest-rank quantile of the values; absent when there are none.</summary>
    public static T? Quantile<T>(IReadOnlyCollection<T> values, Percent percent)
        where T : struct, IComparable<T> => QuantileOrLater(values, values.Count, percent);

    public static double? Mean(IReadOnlyCollection<double> values) => values.Count == 0 ? null : values.Average();

    /// <summary>A mean of amounts of coins, as a whole amount (rounded to the nearest coin); absent when there are none.</summary>
    public static Coins? MeanCoins(IReadOnlyCollection<Coins> amounts)
    {
        if (amounts.Count == 0)
        {
            return null;
        }

        long sum = amounts.Sum(amount => amount.Amount);
        return new Coins((sum + (amounts.Count / 2)) / amounts.Count);
    }

    /// <summary>The Wilson interval that holds a share with 90 % confidence, given the games it was counted on; absent without games.</summary>
    public static (double Low, double High)? Wilson90(double share, double games)
    {
        if (games <= 0)
        {
            return null;
        }

        double z2 = Z90 * Z90;
        double centre = (share + (z2 / (2 * games))) / (1 + (z2 / games));
        double half = Z90 * Math.Sqrt((share * (1 - share) / games) + (z2 / (4 * games * games))) / (1 + (z2 / games));
        return (Math.Max(0, centre - half), Math.Min(1, centre + half));
    }
}
