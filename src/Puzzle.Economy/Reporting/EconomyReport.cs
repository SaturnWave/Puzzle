using System.Globalization;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;

namespace Puzzle.Economy.Reporting;

/// <summary>Which measurement a report was read from, and whether it was checked against the engine and the level files of today.</summary>
/// <param name="Checked">What the guard found; absent when the run was told to trust the measurement.</param>
public sealed record MeasurementIdentity(string Path, MeasurementSpec Spec, long Games, int LastLevel, GuardReport? Checked);

/// <summary>One value of the settings as a report lists it: with its unit, with where it comes from and with who plays by it.</summary>
public sealed record SettingRow(string Field, string Value, string Unit, Provenance Provenance, SettingUse Use);

/// <summary>
/// Everything a run reports: the rows of every table, read from one simulation and one measurement. The CSV files and
/// the summary are two ways of writing the same rows down.
/// </summary>
public sealed record EconomyReport
{
    public const string SummaryFile = "summary.txt";

    /// <summary>The fields under which the list of settings says what the measurement that was read is like: the first line of its file.</summary>
    public const string MeasurementReadPrefix = "measurementRead.";

    public required string SettingsPath { get; init; }

    public required EconomySettings Settings { get; init; }

    public required MeasurementIdentity Measurement { get; init; }

    public required IReadOnlyList<Population> Populations { get; init; }

    public required IReadOnlyList<PaceRow> Pace { get; init; }

    public required IReadOnlyList<StallRow> Stalls { get; init; }

    public required IReadOnlyList<LivesRow> Lives { get; init; }

    public required IReadOnlyList<CoinsRow> Coins { get; init; }

    public required IReadOnlyList<SpendingRow> Spending { get; init; }

    public required IReadOnlyList<BoosterRow> Boosters { get; init; }

    public required IReadOnlyList<HabitRow> Habit { get; init; }

    public required IReadOnlyList<OfferRow> Offers { get; init; }

    /// <summary>What the stored games say by rung and group of levels, before any player is simulated.</summary>
    public required IReadOnlyList<MeasuredGroup> Measured { get; init; }

    /// <summary>Every value of the settings file, then what the measurement that was read is like.</summary>
    public required IReadOnlyList<SettingRow> SettingRows { get; init; }

    /// <summary>
    /// In words, where the measurement block of the settings asks for another measurement than the one that was read;
    /// none when they agree. The block says how a measurement would be made: a run reads the file as it is.
    /// </summary>
    public required IReadOnlyList<string> SettingsAskForAnotherMeasurement { get; init; }

    /// <summary>The levels a rung cannot pass with anything a player can add.</summary>
    public required IReadOnlyList<Wall> Walls { get; init; }

    public static EconomyReport Of(string settingsPath, PopulationResult run, Measurement measurement, MeasurementIdentity identity) => new()
    {
        SettingsPath = settingsPath,
        Settings = run.Settings,
        Measurement = identity,
        Populations = Population.Of(run.Settings.Players),
        Pace = PaceReport.Rows(run),
        Stalls = StallReport.Rows(run),
        Lives = JourneyReports.Lives(run),
        Coins = JourneyReports.Coins(run),
        Spending = JourneyReports.Spending(run),
        Boosters = JourneyReports.Boosters(run),
        Habit = JourneyReports.Habit(run),
        Offers = OfferReport.Rows(run.Settings, measurement, run.Levels.Count),
        Measured = MeasurementSummary.Of(measurement.Spec, LinesOfTheRun(run, measurement)),
        SettingRows = [.. SettingsOf(run.Settings), .. MeasurementRead(identity.Spec)],
        SettingsAskForAnotherMeasurement = identity.Spec.DifferencesFromThePlan(run.Settings.Measurement),
        Walls = run.Levels.Walls(),
    };

    /// <summary>Every line of the measurement for the levels and the rungs of the run, whatever its board start.</summary>
    private static MeasuredLine[] LinesOfTheRun(PopulationResult run, Measurement measurement) => measurement.Lines
        .Where(line => line.Level.Value <= run.Levels.Count && run.Levels.Rungs.Any(rung => rung.Skill == line.Player.NoticePercent))
        .ToArray();

    /// <summary>Every value of the settings with what the catalogue says of its field.</summary>
    public static IReadOnlyList<SettingRow> SettingsOf(EconomySettings settings) => settings.Stated
        .Select(stated => (stated, Described: SettingsCatalogue.Describe(stated.Path)))
        .Select(setting => new SettingRow(setting.stated.Path, setting.stated.Value, setting.Described.Unit, setting.Described.Provenance, setting.Described.Use))
        .ToArray();

    /// <summary>
    /// What the measurement that was read is like, as rows beside the settings: the settings' own measurement block
    /// says how a measurement would be made, which need not be how the file a run read was made.
    /// </summary>
    public static IReadOnlyList<SettingRow> MeasurementRead(MeasurementSpec spec)
    {
        SettingRow Row(string name, string value, string unit) => new(MeasurementReadPrefix + name, value, unit, Provenance.Tool, SettingUse.Simulated);
        return
        [
            Row("engine", MeasurementSpec.Abbreviated(spec.Engine), "fingerprint"),
            Row("moveCap", spec.MoveCap.ToString(), "moves"),
            Row("firstRepeat", spec.FirstRepeat.ToString(CultureInfo.InvariantCulture), "bot repeat"),
            Row("progressMarksPercent", string.Join(' ', spec.ProgressMarks), "percent of the goals"),
            Row("plainGamesBySeedPosition", string.Join(' ', spec.PlainGamesBySeedPosition.Select(games => games.ToString(CultureInfo.InvariantCulture))), "games"),
            Row("boostedGamesPerSeed", spec.BoostedGamesPerSeed.ToString(CultureInfo.InvariantCulture), "games"),
            Row("startBoosterExtraMoves", spec.StartBoosterExtraMoves.ToString(), "moves"),
            Row("extraMovesOfferMoves", spec.ExtraMovesOfferMoves.ToString(), "moves"),
        ];
    }

    /// <summary>The report's files by name, each as its text: the same run always gives the same texts.</summary>
    public IReadOnlyDictionary<string, string> Files() => new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["pace.csv"] = PaceReport.Table(Settings.Name, Pace, Settings.Report.PaceTarget).ToText(),
        ["stalls.csv"] = StallReport.Table(Stalls).ToText(),
        ["lives.csv"] = JourneyReports.LivesTable(Lives).ToText(),
        ["coins.csv"] = JourneyReports.CoinsTable(Coins).ToText(),
        ["spending.csv"] = JourneyReports.SpendingTable(Spending).ToText(),
        ["boosters.csv"] = JourneyReports.BoostersTable(Boosters).ToText(),
        ["offers.csv"] = OfferReport.Table(Offers).ToText(),
        ["settings.csv"] = SettingsTable(SettingRows).ToText(),
        [SummaryFile] = Summary.Of(this),
    };

    /// <exception cref="IOException">A file could not be written.</exception>
    public void WriteTo(string folder)
    {
        foreach ((string name, string text) in Files())
        {
            ReportFiles.Write(folder, name, text);
        }
    }

    private static CsvTable SettingsTable(IEnumerable<SettingRow> rows)
    {
        var table = new CsvTable("field", "value", "unit", "provenance");
        foreach (SettingRow row in rows)
        {
            table.Add(row.Field, row.Value, UnitAsListed(row), row.Provenance.ToString());
        }

        return table;
    }

    /// <summary>The list keeps its four columns, so a value no simulated player meets says so beside its unit.</summary>
    private static string UnitAsListed(SettingRow row) => row.Use switch
    {
        SettingUse.Simulated => row.Unit,
        SettingUse.GameOnly => $"{row.Unit} (the game's; not simulated)",
        _ => throw new ArgumentOutOfRangeException(nameof(row), row.Use, "Unknown use of a setting."),
    };
}
