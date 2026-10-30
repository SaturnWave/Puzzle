using System.Globalization;
using System.Text;
using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;

namespace Puzzle.Economy.Reporting;

/// <summary>A settings field in which two files differ; a value is absent when the file does not state the field.</summary>
public sealed record FieldDifference(string Field, string? A, string? B);

/// <summary>What a compared figure counts. Closed set.</summary>
public enum FigureUnit
{
    Days,
    Hours,
    Attempts,
    Coins,
    TimesPerPlayer,
    ShareOfThePlayers,
    ShareOfAPlayersSessions,
}

public static class FigureUnits
{
    /// <summary>The unit as a comparison names it.</summary>
    public static string Name(this FigureUnit unit) => unit switch
    {
        FigureUnit.Days => "days",
        FigureUnit.Hours => "hours",
        FigureUnit.Attempts => "attempts",
        FigureUnit.Coins => "coins",
        FigureUnit.TimesPerPlayer => "times per player",
        FigureUnit.ShareOfThePlayers => "share of the players",
        FigureUnit.ShareOfAPlayersSessions => "share of a player's sessions",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit."),
    };

    /// <summary>
    /// A figure of the unit as text: coins, days and attempts whole (they are compared as medians, and a median of whole
    /// numbers is one of them), hours and times to two places, shares to three. It is rounded as every other report
    /// rounds (<see cref="Cell"/>), so the same mean reads the same in every file; a figure that rounds to nothing has
    /// no sign.
    /// </summary>
    public static string Written(this FigureUnit unit, double figure)
    {
        string text = unit switch
        {
            FigureUnit.Coins or FigureUnit.Days or FigureUnit.Attempts => figure.ToString("0", CultureInfo.InvariantCulture),
            FigureUnit.Hours or FigureUnit.TimesPerPlayer => Cell.Fine(figure),
            FigureUnit.ShareOfThePlayers or FigureUnit.ShareOfAPlayersSessions => Cell.Share(figure),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown unit."),
        };
        return text.TrimStart('-').All(character => character is '0' or '.') ? text.TrimStart('-') : text;
    }
}

/// <summary>A figure two settings files are compared in. Closed set.</summary>
public enum ComparedFigure
{
    ReachedShare,
    DaysP50,
    PlayHoursP50,
    AttemptsP50,
    CoinsBalanceP50,
    ExtraMovesByAd,
    ExtraMovesBought,
    ExtraMovesWantedUnpaid,
    GapsAtUnpaidWants,
    PriceOfUnpaidWants,
    StartBoostersUsed,
    StartBoostersBought,
    Purchases,
    CoinsBoughtForMoney,
    EndedByLivesShare,
    HoursDeniedByLives,
}

public static class ComparedFigures
{
    /// <summary>The figure's name in a comparison: the name of its column in the run's own reports.</summary>
    public static string Name(this ComparedFigure figure) => figure switch
    {
        ComparedFigure.ReachedShare => "reachedShare",
        ComparedFigure.DaysP50 => "daysP50",
        ComparedFigure.PlayHoursP50 => "playHoursP50",
        ComparedFigure.AttemptsP50 => "attemptsP50",
        ComparedFigure.CoinsBalanceP50 => "coinsBalanceP50",
        ComparedFigure.ExtraMovesByAd => "extraMovesByAd",
        ComparedFigure.ExtraMovesBought => "extraMovesBought",
        ComparedFigure.ExtraMovesWantedUnpaid => "extraMovesWantedUnpaid",
        ComparedFigure.GapsAtUnpaidWants => "gapsAtUnpaidWants",
        ComparedFigure.PriceOfUnpaidWants => "priceOfUnpaidWants",
        ComparedFigure.StartBoostersUsed => "startBoostersUsed",
        ComparedFigure.StartBoostersBought => "startBoostersBought",
        ComparedFigure.Purchases => "purchasesForMoney",
        ComparedFigure.CoinsBoughtForMoney => "coinsBoughtForMoney",
        ComparedFigure.EndedByLivesShare => "endedByLivesShare",
        ComparedFigure.HoursDeniedByLives => "hoursDeniedByLives",
        _ => throw new ArgumentOutOfRangeException(nameof(figure), figure, "Unknown figure."),
    };
}

/// <summary>One figure under both settings files.</summary>
/// <param name="Population">The population as the first file has it; the second has one of the same name.</param>
/// <param name="Change">B less A.</param>
/// <param name="ChangeP10">The 10 % point of each player's own change (the same player under B less under A), over the players who reached the level under both; unknown when the two files do not simulate the same players, and for a figure no single player has.</param>
public sealed record ComparisonRow(Population Population, ComparedFigure Figure, LevelId Level, FigureUnit Unit, double? A, double? B, double? Change, double? ChangeP10, double? ChangeP90);

/// <summary>
/// Two settings files side by side on the same measurement: the fields in which they differ, then what the players
/// did up to each milestone under each. When both files simulate the same players, a player meets the same stored
/// games under both, so each player's own change is read too, and the figures of those who reached a level are taken
/// over the players who reached it under both files: the two sides then describe the same people.
/// </summary>
public sealed record Comparison(
    EconomySettings A,
    EconomySettings B,
    bool HaveTheSamePlayers,
    IReadOnlyList<FieldDifference> Fields,
    IReadOnlyList<ComparisonRow> Rows,
    IReadOnlyList<string> AAsksForAnotherMeasurement,
    IReadOnlyList<string> BAsksForAnotherMeasurement)
{
    public const string TableFile = "compare.csv";
    public const string TextFile = "differences.txt";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Metric[] Metrics =
    [
        new(ComparedFigure.DaysP50, FigureUnit.Days, Summed.AsMedianWithTheOthersLater, reached => reached.At.Day.Value),
        new(ComparedFigure.PlayHoursP50, FigureUnit.Hours, Summed.AsMedianWithTheOthersLater, reached => reached.Totals.PlayTime.TotalHours),
        new(ComparedFigure.AttemptsP50, FigureUnit.Attempts, Summed.AsMedianWithTheOthersLater, reached => reached.Totals.Attempts),
        new(ComparedFigure.CoinsBalanceP50, FigureUnit.Coins, Summed.AsMedian, reached => reached.Standing.Balance.Amount),
        new(ComparedFigure.ExtraMovesByAd, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.ExtraMovesByAd),
        new(ComparedFigure.ExtraMovesBought, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.ExtraMovesBought),
        new(ComparedFigure.ExtraMovesWantedUnpaid, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.ExtraMovesWantedUnpaid),
        new(ComparedFigure.GapsAtUnpaidWants, FigureUnit.Coins, Summed.AsMean, reached => reached.Totals.UnpaidInAll.Gaps.Amount),
        new(ComparedFigure.PriceOfUnpaidWants, FigureUnit.Coins, Summed.AsMean, reached => reached.Totals.UnpaidInAll.Price.Amount),
        new(ComparedFigure.StartBoostersUsed, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.StartBoostersUsed),
        new(ComparedFigure.StartBoostersBought, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.StartBoostersBought),
        new(ComparedFigure.Purchases, FigureUnit.TimesPerPlayer, Summed.AsMean, reached => reached.Totals.Purchases),
        new(ComparedFigure.CoinsBoughtForMoney, FigureUnit.Coins, Summed.AsMean, reached => reached.Totals.Earned(CoinSource.BoughtForMoney).Amount),
        new(ComparedFigure.EndedByLivesShare, FigureUnit.ShareOfAPlayersSessions, Summed.AsMean, reached => reached.Totals.Sessions == 0 ? 0 : reached.Totals.SessionsEndedByLives / (double)reached.Totals.Sessions),
        new(ComparedFigure.HoursDeniedByLives, FigureUnit.Hours, Summed.AsMean, reached => reached.Totals.PlayDeniedByLives.TotalHours),
    ];

    /// <summary>How the players' values become the one figure of a row. Closed set.</summary>
    private enum Summed
    {
        /// <summary>The median over every player of the file, those who never reach the level counting as later than all who do.</summary>
        AsMedianWithTheOthersLater,

        /// <summary>The median over the players who reached the level.</summary>
        AsMedian,

        /// <summary>The mean over the players who reached the level.</summary>
        AsMean,
    }

    /// <param name="a">The first file and how its players fared on every stored game.</param>
    /// <param name="measured">The measurement both were read from.</param>
    public static Comparison Of(Simulated a, Simulated b, MeasurementSpec measured)
    {
        PopulationSettings playersOfA = a.Settings.Players;
        PopulationSettings playersOfB = b.Settings.Players;
        bool samePlayers = playersOfA.Count == playersOfB.Count && playersOfA.Seed == playersOfB.Seed && playersOfA.SkillMix.SequenceEqual(playersOfB.SkillMix);
        var rows = new List<ComparisonRow>();
        foreach (Population population in Population.Of(playersOfA).Where(population => Population.Of(playersOfB).Any(other => other.Name == population.Name)))
        {
            IReadOnlyList<PlayerOutcome> ofA = population.PlayersIn(a.Pass);
            IReadOnlyList<PlayerOutcome> ofB = Population.Of(playersOfB).Single(other => other.Name == population.Name).PlayersIn(b.Pass);
            IReadOnlyList<LevelId> milestonesOfA = a.Settings.Report.Milestones;
            IReadOnlyList<LevelId> milestonesOfB = b.Settings.Report.Milestones;
            foreach (LevelId level in milestonesOfA.Where(level => milestonesOfB.Contains(level) && level.Value <= Math.Min(a.LastLevel, b.LastLevel)))
            {
                var reached = new ReachedInBoth(
                    ofA.Select(player => player.AtMilestones[IndexOf(milestonesOfA, level)]).ToArray(),
                    ofB.Select(player => player.AtMilestones[IndexOf(milestonesOfB, level)]).ToArray(),
                    samePlayers);
                rows.Add(ReachedShareRow(population, level, reached));
                rows.AddRange(Metrics.Select(metric => metric.RowOf(population, level, reached)));
            }
        }

        return new Comparison(
            a.Settings,
            b.Settings,
            samePlayers,
            FieldDifferences(a.Settings, b.Settings),
            rows,
            measured.DifferencesFromThePlan(a.Settings.Measurement),
            measured.DifferencesFromThePlan(b.Settings.Measurement));
    }

    public string TableText()
    {
        var table = new CsvTable("population", "metric", "level", "unit", "a", "b", "change", "changeP10", "changeP90");
        foreach (ComparisonRow row in Rows)
        {
            table.Add(
                row.Population.Name,
                row.Figure.Name(),
                Cell.Whole(row.Level.Value),
                row.Unit.Name(),
                Written(row.A, row.Unit),
                Written(row.B, row.Unit),
                Written(row.Change, row.Unit),
                Written(row.ChangeP10, row.Unit),
                Written(row.ChangeP90, row.Unit));
        }

        return table.ToText();
    }

    public string DifferencesText()
    {
        var text = new StringBuilder();
        text.AppendLine(Invariant, $"ECONOMY COMPARISON: a = '{A.Name}', b = '{B.Name}', on the same measurement");
        text.AppendLine(HaveTheSamePlayers
            ? "Both files simulate the same players: each player meets the same stored games under both. Balances and means are over the players who reached the level under both files, and the last two columns are the 10 % and 90 % points of each player's own change."
            : "The two files do not simulate the same players (count, seed or mix differ): only the figures are compared, each over the players who reached the level under its own file, and not each player's own change.");
        text.AppendLine("Where reachedShare is below 1, the figures after it are about the players who got there within the horizon, the faster ones.");
        AppendMeasurementNote(text, "a", AAsksForAnotherMeasurement);
        AppendMeasurementNote(text, "b", BAsksForAnotherMeasurement);
        text.AppendLine();
        text.AppendLine(Fields.Count == 0 ? "The two files state the same settings." : "SETTINGS THAT DIFFER");
        foreach (FieldDifference field in Fields)
        {
            text.AppendLine(Invariant, $"  {field.Field}: a = {field.A ?? "(not stated)"}, b = {field.B ?? "(not stated)"}");
        }

        text.AppendLine();
        text.AppendLine("UP TO EACH MILESTONE (change is b less a)");
        var table = new TextTable("population", "level", "figure", "unit", "a", "b", "change", "each player's change p10", "p90");
        foreach (ComparisonRow row in Rows)
        {
            table.Add(
                row.Population.Name,
                Cell.Whole(row.Level.Value),
                row.Figure.Name(),
                row.Unit.Name(),
                Shown(row.A, row.Unit),
                Shown(row.B, row.Unit),
                Shown(row.Change, row.Unit),
                Shown(row.ChangeP10, row.Unit),
                Shown(row.ChangeP90, row.Unit));
        }

        table.AppendTo(text);
        return ReportFiles.WithLineFeeds(text);
    }

    /// <exception cref="IOException">A file could not be written.</exception>
    public void WriteTo(string folder)
    {
        ReportFiles.Write(folder, TableFile, TableText());
        ReportFiles.Write(folder, TextFile, DifferencesText());
    }

    /// <summary>A file's measurement block is listed among the settings that differ, though both sides read one measurement: say so where it matters.</summary>
    private static void AppendMeasurementNote(StringBuilder text, string file, IReadOnlyList<string> asksOtherwise)
    {
        if (asksOtherwise.Count > 0)
        {
            text.AppendLine(Invariant, $"NOTE: the measurement block of {file} asks for another measurement than the one both were read from ({string.Join("; ", asksOtherwise)}). The block says how a measurement would be made; it changes no figure here.");
        }
    }

    /// <summary>Every field the two resolved files state differently, or only one of them states.</summary>
    private static List<FieldDifference> FieldDifferences(EconomySettings a, EconomySettings b)
    {
        Dictionary<string, string> ofA = a.Stated.ToDictionary(stated => stated.Path, stated => stated.Value);
        Dictionary<string, string> ofB = b.Stated.ToDictionary(stated => stated.Path, stated => stated.Value);
        return ofA.Keys.Union(ofB.Keys)
            .Select(field => new FieldDifference(field, ofA.GetValueOrDefault(field), ofB.GetValueOrDefault(field)))
            .Where(field => field.A != field.B)
            .ToList();
    }

    private static ComparisonRow ReachedShareRow(Population population, LevelId level, ReachedInBoth reached)
    {
        double? shareInA = ShareReached(reached.InA);
        double? shareInB = ShareReached(reached.InB);
        return new ComparisonRow(population, ComparedFigure.ReachedShare, level, FigureUnit.ShareOfThePlayers, shareInA, shareInB, shareInB - shareInA, null, null);
    }

    private static double? ShareReached(Checkpoint?[] players) => players.Length == 0 ? null : players.Count(reached => reached is not null) / (double)players.Length;

    private static int IndexOf(IReadOnlyList<LevelId> milestones, LevelId level)
    {
        for (int index = 0; index < milestones.Count; index++)
        {
            if (milestones[index] == level)
            {
                return index;
            }
        }

        throw new ArgumentException($"Level {level} is no milestone.", nameof(level));
    }

    /// <summary>A figure in the table, written as its unit asks; an unknown one is an empty cell.</summary>
    private static string Written(double? value, FigureUnit unit) => value is double known ? unit.Written(known) : string.Empty;

    private static string Shown(double? value, FigureUnit unit) => value is null ? "-" : Written(value, unit);

    /// <summary>One settings file and how its players fared: what a comparison sets beside another.</summary>
    /// <param name="Pass">Its players on every stored game.</param>
    /// <param name="LastLevel">The last level its players can reach.</param>
    public sealed record Simulated(EconomySettings Settings, PassResult Pass, int LastLevel);

    /// <summary>
    /// Where each player stood when they won a milestone's level under each file; absent for a player who never did.
    /// With the same players under both files the two lists are in the same order, player by player.
    /// </summary>
    private sealed record ReachedInBoth(Checkpoint?[] InA, Checkpoint?[] InB, bool SamePlayers)
    {
        /// <summary>The players of A a figure of those who reached is taken over: with the same players, those who reached under both files.</summary>
        public Checkpoint[] CountedInA => SamePlayers ? Pairs.Select(pair => pair.InA).ToArray() : InA.OfType<Checkpoint>().ToArray();

        public Checkpoint[] CountedInB => SamePlayers ? Pairs.Select(pair => pair.InB).ToArray() : InB.OfType<Checkpoint>().ToArray();

        /// <summary>Each player who reached the level under both files, as they stood under each; none when the files simulate other players.</summary>
        public IEnumerable<(Checkpoint InA, Checkpoint InB)> Pairs => SamePlayers
            ? InA.Zip(InB).Where(player => player.First is not null && player.Second is not null).Select(player => (player.First!, player.Second!))
            : [];
    }

    /// <summary>A figure every player who reached a milestone has, and how the players' figures become one.</summary>
    private sealed record Metric(ComparedFigure Figure, FigureUnit Unit, Summed Summed, Func<Checkpoint, double> Of)
    {
        public ComparisonRow RowOf(Population population, LevelId level, ReachedInBoth reached)
        {
            double? a = FigureOf(reached.InA, reached.CountedInA);
            double? b = FigureOf(reached.InB, reached.CountedInB);
            double[] ownChanges = reached.Pairs.Select(player => Of(player.InB) - Of(player.InA)).ToArray();
            return new ComparisonRow(
                population, Figure, level, Unit, a, b, b - a, Statistics.Quantile(ownChanges, Statistics.BandLow), Statistics.Quantile(ownChanges, Statistics.BandHigh));
        }

        /// <param name="players">Every player of the file, those who never reached the level absent.</param>
        /// <param name="counted">The players a figure of those who reached is taken over.</param>
        private double? FigureOf(Checkpoint?[] players, Checkpoint[] counted) => Summed switch
        {
            Summed.AsMedianWithTheOthersLater => Statistics.QuantileOrLater(players.OfType<Checkpoint>().Select(Of), players.Length, Statistics.Median),
            Summed.AsMedian => Statistics.Quantile(counted.Select(Of).ToArray(), Statistics.Median),
            Summed.AsMean => Statistics.Mean(counted.Select(Of).ToArray()),
            _ => throw new InvalidOperationException($"Unhandled way of summing {Summed}."),
        };
    }
}
