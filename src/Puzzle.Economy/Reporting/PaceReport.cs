using Puzzle.Core.Levels;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;

namespace Puzzle.Economy.Reporting;

/// <summary>Where the median player lands against the pace the owner wants. A verdict describes; nothing is adjusted to change it. Closed set.</summary>
public enum PaceVerdict
{
    /// <summary>The median player reaches the level before the first day of the target.</summary>
    Faster,

    Within,

    /// <summary>The median player reaches the level after the last day of the target.</summary>
    Slower,

    /// <summary>Fewer than half the players reach the level within the horizon.</summary>
    NotReached,
}

/// <summary>When a population reaches a milestone level.</summary>
/// <param name="IsMeasured">Whether the measurement reaches the level at all; every figure is unknown where it does not.</param>
/// <param name="DaysP50">The day on which the median player wins the level (day 1 is the first day); unknown when fewer than half get there.</param>
/// <param name="DaysP50OnEvenGames">The same median, the players meeting only the even stored games: with the odd one, the noise of the measurement.</param>
/// <param name="PlayHoursP50">The median time spent on moves until then.</param>
/// <param name="Verdict">Only on the level of the pace target.</param>
public sealed record PaceRow(
    Population Population,
    LevelId Level,
    bool IsMeasured,
    int Players,
    double? ReachedShare,
    int? DaysP10,
    int? DaysP50,
    int? DaysP90,
    int? DaysP50OnEvenGames,
    int? DaysP50OnOddGames,
    double? PlayHoursP50,
    int? AttemptsP50,
    PaceVerdict? Verdict);

public static class PaceReport
{
    public static IReadOnlyList<PaceRow> Rows(PopulationResult run)
    {
        ReportSettings report = run.Settings.Report;
        var rows = new List<PaceRow>();
        foreach (Population population in Population.Of(run.Settings.Players))
        {
            IReadOnlyList<PlayerOutcome> players = population.PlayersIn(run.All);
            IReadOnlyList<PlayerOutcome> onEvenGames = population.PlayersIn(run.EvenGames);
            IReadOnlyList<PlayerOutcome> onOddGames = population.PlayersIn(run.OddGames);
            for (int milestone = 0; milestone < report.Milestones.Count; milestone++)
            {
                LevelId level = report.Milestones[milestone];
                rows.Add(level.Value <= run.Levels.Count
                    ? RowOf(population, level, report.PaceTarget, Reached(players, milestone), players.Count, MedianDay(onEvenGames, milestone), MedianDay(onOddGames, milestone))
                    : new PaceRow(population, level, IsMeasured: false, players.Count, null, null, null, null, null, null, null, null, null));
            }
        }

        return rows;
    }

    public static CsvTable Table(string settingsName, IEnumerable<PaceRow> rows, PaceTarget target)
    {
        var table = new CsvTable(
            "settings", "population", "isAverage", "players", "level", "measured", "reachedShare", "daysP10", "daysP50", "daysP90",
            "daysP50EvenGames", "daysP50OddGames", "playHoursP50", "attemptsP50", "targetFromDays", "targetToDays", "verdict");
        foreach (PaceRow row in rows)
        {
            bool isTarget = row.Level == target.Level;
            table.Add(
                settingsName,
                row.Population.Name,
                Cell.YesOrNo(row.Population.IsAverage),
                Cell.Whole(row.Players),
                Cell.Whole(row.Level.Value),
                Cell.YesOrNo(row.IsMeasured),
                Cell.Share(row.ReachedShare),
                Cell.Whole(row.DaysP10),
                Cell.Whole(row.DaysP50),
                Cell.Whole(row.DaysP90),
                Cell.Whole(row.DaysP50OnEvenGames),
                Cell.Whole(row.DaysP50OnOddGames),
                Cell.Number(row.PlayHoursP50),
                Cell.Whole(row.AttemptsP50),
                isTarget ? Cell.Whole(target.From.Value) : string.Empty,
                isTarget ? Cell.Whole(target.To.Value) : string.Empty,
                row.Verdict?.ToString() ?? string.Empty);
        }

        return table;
    }

    /// <summary>Where a median day lands against the target; a median nobody reached is its own verdict.</summary>
    public static PaceVerdict VerdictOn(int? medianDay, PaceTarget target)
    {
        if (medianDay is not int day)
        {
            return PaceVerdict.NotReached;
        }

        if (day < target.From.Value)
        {
            return PaceVerdict.Faster;
        }

        return day > target.To.Value ? PaceVerdict.Slower : PaceVerdict.Within;
    }

    /// <summary>The checkpoints of the players who won the milestone's level.</summary>
    internal static IReadOnlyList<Checkpoint> Reached(IEnumerable<PlayerOutcome> players, int milestone) =>
        players.Select(player => player.AtMilestones[milestone]).OfType<Checkpoint>().ToArray();

    private static PaceRow RowOf(Population population, LevelId level, PaceTarget target, IReadOnlyList<Checkpoint> reached, int players, int? medianOnEvenGames, int? medianOnOddGames)
    {
        // A player who never gets there is later than everyone who does, in days, in hours played and in attempts.
        int[] days = reached.Select(checkpoint => checkpoint.At.Day.Value).ToArray();
        int? median = Statistics.QuantileOrLater(days, players, Statistics.Median);
        return new PaceRow(
            population,
            level,
            IsMeasured: true,
            players,
            players == 0 ? null : reached.Count / (double)players,
            Statistics.QuantileOrLater(days, players, Statistics.BandLow),
            median,
            Statistics.QuantileOrLater(days, players, Statistics.BandHigh),
            medianOnEvenGames,
            medianOnOddGames,
            Statistics.QuantileOrLater(reached.Select(checkpoint => checkpoint.Totals.PlayTime.TotalHours), players, Statistics.Median),
            Statistics.QuantileOrLater(reached.Select(checkpoint => checkpoint.Totals.Attempts), players, Statistics.Median),
            level == target.Level ? VerdictOn(median, target) : null);
    }

    private static int? MedianDay(IReadOnlyList<PlayerOutcome> players, int milestone) =>
        Statistics.QuantileOrLater(Reached(players, milestone).Select(checkpoint => checkpoint.At.Day.Value), players.Count, Statistics.Median);
}
