using System.Diagnostics;
using System.Globalization;
using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>economy measure|run|compare</c>: the economy simulator (docs/GAME_DESIGN.md section 19). It models free players,
/// players who pay real money as their settings say, and prices; nothing in it changes a level's difficulty or needs it changed.
/// <list type="bullet">
/// <item><c>measure [settings.json] [--levels levels/generated] [--from 1] [--to &lt;last&gt;] [--out out/economy/measurement.jsonl]
/// [--workers N] [--restart] [--low-priority]</c>: bots of the settings' rungs play every level on its own pool seeds past its limit,
/// plain and with each board booster, and what the economy needs of every game is kept in the measurement file. A
/// stopped run goes on where it stopped; levels that did not change are not played again. Progress goes to stderr.
/// <c>--low-priority</c> lets everything else on the machine go first.</item>
/// <item><c>run [settings.json] [--measurement ...] [--levels ...] [--out out/economy/&lt;settings name&gt;] [--trust-measurement]</c>:
/// simulates the settings' players from the measurement alone and writes the reports (pace, stalls, lives, coins,
/// spending, purchases, boosters, offers, settings as CSV, and summary.txt, which is also printed).</item>
/// <item><c>compare &lt;a.json&gt; &lt;b.json&gt; [--measurement ...] [--levels ...] [--out out/economy/compare-&lt;a&gt;-vs-&lt;b&gt;]
/// [--trust-measurement]</c>: two settings files on the same measurement, as compare.csv and differences.txt.</item>
/// </list>
/// Before reading a measurement, run and compare check it against this build's engine and today's level files and
/// replay a sample of its games; <c>--trust-measurement</c> skips that, and then no game is played at all. Every
/// economy value lives in the settings file; the options are paths, the level range, the worker count and how the
/// command runs. <c>--restart</c>, <c>--low-priority</c> and <c>--trust-measurement</c> take no value: one that is
/// followed by a value has swallowed an argument, and is refused.
/// Exit codes: 0 done; 1 a settings file, a measurement or a level set that is unreadable, stale or does not cover
/// the run, or a file that cannot be read or written; 2 usage.
/// </summary>
internal static class EconomyCommand
{
    private const int Unusable = 1;

    /// <summary>The most lines the runtime plays at the same time (the limit of a parallel query's degree of parallelism).</summary>
    private const int MostWorkers = 512;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeSpan ProgressEvery = TimeSpan.FromSeconds(30);

    public static int Run(CliArgs args)
    {
        string action = args.Required(0, "measure|run|compare");
        try
        {
            return action switch
            {
                "measure" => Measure(args),
                "run" => Simulate(args),
                "compare" => Compare(args),
                _ => throw new UsageException($"Unknown economy action '{action}' (measure, run or compare)."),
            };
        }
        catch (EconomySettingsFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Unusable;
        }
        catch (MeasurementFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Unusable;
        }
        catch (MeasurementMismatchException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Unusable;
        }
        catch (UnmeasurableLevelException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Unusable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A report or a measurement that cannot be put where it was asked for: a folder in a file's place, a path this user may not write.
            Console.Error.WriteLine($"A file could not be read or written: {ex.Message}");
            return Unusable;
        }
    }

    private static int Measure(CliArgs args)
    {
        // The switches first: one that swallowed the settings path must stop the command before it reads the default settings in its place.
        bool restart = Switch(args, "restart");
        bool lowPriority = Switch(args, "low-priority");
        int? workers = args.OptionalInt("workers");
        if (workers is < 1 or > MostWorkers)
        {
            throw new UsageException($"--workers must be from 1 to {MostWorkers}.");
        }

        string settingsPath = SettingsPathFrom(args, position: 1);
        EconomySettings settings = SettingsLoader.Load(settingsPath);
        string outPath = MeasurementPathFrom(args, option: "out");
        LevelDefinition[] levels = LevelsInRange(args, settings.Measurement);
        if (lowPriority)
        {
            LetEverythingElseGoFirst();
        }

        Console.Error.WriteLine($"measuring levels {levels[0].Id}..{levels[^1].Id} for {string.Join(", ", settings.Players.Rungs)} into {outPath}");
        var clock = Stopwatch.StartNew();
        var progress = new ProgressLine(clock);
        MeasureOutcome outcome = Measurer.Run(
            new MeasureRequest
            {
                Path = outPath,
                Plan = settings.Measurement,
                Levels = levels,
                Rungs = settings.Players.Rungs.ToArray(),
                Workers = workers,
                Restart = restart,
            },
            progress.Report);

        Console.Write(MeasurementSummary.Text(outcome.Spec, MeasurementSummary.Of(outcome.Spec, outcome.Lines)));
        Console.WriteLine(string.Create(
            Invariant,
            $"measurement {outPath}: engine {MeasurementSpec.Abbreviated(outcome.Spec.Engine)}, {outcome.Lines.Count} lines; {outcome.LinesPlayed} played now ({outcome.GamesPlayed} games), {outcome.LinesKept} kept"));
        ReportTiming(outcome.GamesPlayed, clock.Elapsed, workers ?? Environment.ProcessorCount);
        return 0;
    }

    private static int Simulate(CliArgs args)
    {
        bool trusted = Switch(args, "trust-measurement");
        string settingsPath = SettingsPathFrom(args, position: 1);
        EconomySettings settings = SettingsLoader.Load(settingsPath);
        string measurementPath = MeasurementPathFrom(args, option: "measurement");
        string outFolder = args.OptionalText("out") ?? InRepository("out", "economy", settings.Name);
        Measurement measurement = Measurement.Load(measurementPath);
        SimulatedLevels levels = SimulatedLevels.Of(settings, measurement);
        GuardReport? guard = trusted ? null : Checked(args, measurement, [levels]);

        var clock = Stopwatch.StartNew();
        PopulationResult run = PopulationRun.Play(settings, levels);
        var identity = new MeasurementIdentity(measurementPath, measurement.Spec, measurement.GameCount, levels.Count, guard);
        EconomyReport report = EconomyReport.Of(settingsPath, run, measurement, identity);
        report.WriteTo(outFolder);

        Console.Write(Summary.Of(report));
        Console.WriteLine($"wrote {outFolder}: {string.Join(", ", report.Files().Keys)}");
        Console.Error.WriteLine(string.Create(Invariant, $"simulated {settings.Players.Count} players three times in {clock.Elapsed.TotalSeconds:0.0} s"));
        return 0;
    }

    private static int Compare(CliArgs args)
    {
        bool trusted = Switch(args, "trust-measurement");
        string pathOfA = args.Required(1, "a.json");
        string pathOfB = args.Required(2, "b.json");
        EconomySettings a = SettingsLoader.Load(pathOfA);
        EconomySettings b = SettingsLoader.Load(pathOfB);
        string outFolder = args.OptionalText("out") ?? InRepository("out", "economy", $"compare-{a.Name}-vs-{b.Name}");
        Measurement measurement = Measurement.Load(MeasurementPathFrom(args, option: "measurement"));
        SimulatedLevels levelsOfA = SimulatedLevels.Of(a, measurement);
        SimulatedLevels levelsOfB = SimulatedLevels.Of(b, measurement);
        GuardReport? guard = trusted ? null : Checked(args, measurement, [levelsOfA, levelsOfB]);

        PassResult passOfA = PopulationRun.PlayPass(a, levelsOfA, MeasuredHalf.All, dayEndsKept: []);
        PassResult passOfB = PopulationRun.PlayPass(b, levelsOfB, MeasuredHalf.All, dayEndsKept: []);
        Comparison comparison = Comparison.Of(new Comparison.Simulated(a, passOfA, levelsOfA.Count), new Comparison.Simulated(b, passOfB, levelsOfB.Count), measurement.Spec);
        comparison.WriteTo(outFolder);

        Console.Write(comparison.DifferencesText());
        Console.WriteLine(guard is null
            ? "The measurement was NOT CHECKED against this build or the level files (--trust-measurement)."
            : $"The measurement was checked against this build and the level files ({guard.GamesReplayed} stored games replayed).");
        Console.WriteLine($"wrote {outFolder}: {Comparison.TableFile}, {Comparison.TextFile}");
        return 0;
    }

    /// <summary>
    /// Checks the lines the runs will read against this build and today's level files. A command told to trust the
    /// measurement does not come here: then nothing is played and no level file is read.
    /// </summary>
    private static GuardReport Checked(CliArgs args, Measurement measurement, SimulatedLevels[] runs)
    {
        MeasuredLine[] linesRead = runs.SelectMany(levels => levels.LinesRead()).DistinctBy(line => line.Key).OrderBy(line => line.Key).ToArray();
        Dictionary<int, LevelDefinition> levelFiles = LoadLevels(args).ToDictionary(level => level.Id.Value);
        return MeasurementGuard.Check(measurement.Spec, linesRead, levelFiles);
    }

    /// <summary>The levels numbered --from to --to, in level order. A broken file stops the command before any game.</summary>
    /// <exception cref="UsageException">No level is in the range, or a level's limit and what a player can add to it do not fit under the move cap.</exception>
    private static LevelDefinition[] LevelsInRange(CliArgs args, MeasurementPlan plan)
    {
        int from = args.Int("from", 1);
        int to = args.OptionalInt("to") ?? int.MaxValue;
        if (from < 1 || to < from)
        {
            throw new UsageException("--from must be >= 1 and --to >= --from.");
        }

        LevelDefinition[] levels = LoadLevels(args).Where(level => level.Id.Value >= from && level.Id.Value <= to).ToArray();
        if (levels.Length == 0)
        {
            throw new UsageException($"No level numbered {from} or above in the level folder.");
        }

        if (levels.FirstOrDefault(level => level.MoveLimit > plan.MoveCap.Value) is { } cutShort)
        {
            throw new UsageException($"measurement.moveCap {plan.MoveCap} is below the move limit of level {cutShort.Id}, {cutShort.MoveLimit}; games are followed only to the cap.");
        }

        return levels;
    }

    /// <summary>Every level of the level folder, in level order.</summary>
    /// <exception cref="UsageException">The folder is missing, or two of its files hold the same level.</exception>
    private static LevelDefinition[] LoadLevels(CliArgs args)
    {
        string folder = args.OptionalText("levels") ?? InRepository("levels", "generated");
        if (!Directory.Exists(folder))
        {
            throw new UsageException($"No level folder at '{folder}'.");
        }

        LevelDefinition[] levels = LevelFiles.Under(folder).Select(LevelJson.Load).OrderBy(level => level.Id.Value).ToArray();
        if (levels.GroupBy(level => level.Id).FirstOrDefault(sameId => sameId.Count() > 1) is { } twice)
        {
            throw new UsageException($"Two files in '{folder}' hold level {twice.Key}; name one level set.");
        }

        return levels;
    }

    /// <summary>
    /// Whether an option that takes no value was given. The command line reads the word after an option as its value,
    /// so such an option placed before the settings file would swallow it, and the command would run the default
    /// settings without a word: that is refused here.
    /// </summary>
    /// <exception cref="UsageException">The option is followed by a value.</exception>
    private static bool Switch(CliArgs args, string option) => args.OptionalText(option) switch
    {
        null => false,
        "true" => true,
        string swallowed => throw new UsageException($"--{option} takes no value, and '{swallowed}' was read as one: put the settings file before the options."),
    };

    /// <summary>Runs the measurement below everything else on the machine: for a machine that serves people while it measures.</summary>
    private static void LetEverythingElseGoFirst()
    {
        using Process self = Process.GetCurrentProcess();
        self.PriorityClass = ProcessPriorityClass.BelowNormal;
    }

    private static string SettingsPathFrom(CliArgs args, int position) =>
        args.Positionals.Count > position ? args.Positionals[position] : InRepository("economy", "default.json");

    private static string MeasurementPathFrom(CliArgs args, string option) =>
        args.OptionalText(option) ?? InRepository("out", "economy", "measurement.jsonl");

    /// <summary>A path below the repository root, for a default.</summary>
    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string InRepository(params string[] parts)
    {
        string root = LevelLibrary.FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? throw new UsageException($"Not inside the repository (no 'levels' folder above here); give the path of {string.Join('/', parts)}.");
        return Path.Combine([root, .. parts]);
    }

    /// <summary>What a game cost, on stderr: stdout holds only what the same command prints every time.</summary>
    private static void ReportTiming(long games, TimeSpan elapsed, int workers)
    {
        if (games > 0)
        {
            Console.Error.WriteLine(string.Create(
                Invariant,
                $"{games} games in {elapsed.TotalSeconds:0} s: {games / elapsed.TotalSeconds:0} games a second, {elapsed.TotalMilliseconds * workers / games:0.00} worker-ms a game on {workers} workers"));
        }
    }

    /// <summary>A line on stderr every half minute and at the end: the lines done, the rate and when the measurement should be over.</summary>
    private sealed class ProgressLine(Stopwatch clock)
    {
        private TimeSpan _lastShown = TimeSpan.Zero;

        public void Report(MeasureProgress progress)
        {
            TimeSpan elapsed = clock.Elapsed;
            bool isLast = progress.LinesDone == progress.LinesToPlay;
            if (!isLast && elapsed - _lastShown < ProgressEvery)
            {
                return;
            }

            _lastShown = elapsed;
            double gamesASecond = progress.GamesDone / Math.Max(elapsed.TotalSeconds, 0.001);
            double secondsLeft = (progress.GamesToPlay - progress.GamesDone) / Math.Max(gamesASecond, 0.001);
            Console.Error.WriteLine(string.Create(
                Invariant,
                $"{progress.LinesDone}/{progress.LinesToPlay} lines, {progress.GamesDone}/{progress.GamesToPlay} games, {gamesASecond:0} games a second, about {secondsLeft / 60:0.0} minutes left"));
        }
    }
}
