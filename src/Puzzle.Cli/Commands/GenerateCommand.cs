using System.Globalization;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;
using Puzzle.Tooling;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>generate [--profile profiles/default.json] [--from 1] [--to 20] [--out levels/generated] [--quick] [--overwrite] [--repair]</c>:
/// produces calibrated levels. Existing level files are kept (and feed the novelty check) unless --overwrite;
/// --repair regenerates only those whose report row no longer holds (<see cref="ReportRow.StillHolds"/>): a small seed
/// pool, a calibration bot, target win rate, casual floor, floor bot or move margin other than the profile's, or a
/// limit that does not stand on the recorded win rates. Without a move margin that is a win rate off its target on
/// either side or a missed floor. With one, the win rates are read at the final limit (calibrated plus margin), where
/// a win rate above its target is the margin at work, so only a shortfall counts: --repair then does not notice a
/// calibrated limit that overshot its target or a tolerance tightened since, and --overwrite is the way to apply those.
/// </summary>
internal static class GenerateCommand
{
    public static int Run(CliArgs args)
    {
        GenerationProfile profile = ProfileLoader.Load(args.Text("profile", "profiles/default.json"));
        if (args.Flag("quick"))
        {
            profile = Quick(profile);
        }

        int from = args.Int("from", 1);
        int to = args.Int("to", from + 19);
        string outDir = args.Text("out", "levels/generated");
        bool overwrite = args.Flag("overwrite");
        bool repair = args.Flag("repair");
        if (from < 1 || to < from)
        {
            throw new UsageException("--from must be >= 1 and --to >= --from.");
        }

        Directory.CreateDirectory(outDir);
        var generator = new LevelGenerator(profile);
        IReadOnlyDictionary<int, ReportRow> recorded = repair
            ? GenerationReport.Read(Path.Combine(outDir, GenerationReport.FileName), profile)
            : new Dictionary<int, ReportRow>();
        bool NeedsRepair(int number) => !recorded.TryGetValue(number, out ReportRow? row) || !row.StillHolds(generator.Plan(number), profile);
        Console.WriteLine($"profile '{profile.Name}' -> {outDir}, levels {from}..{to}{(args.Flag("quick") ? " (quick)" : string.Empty)}");
        var recent = new Queue<LevelSignature>(LoadRecent(outDir, from, profile.NoveltyWindow));
        var rows = new List<LevelMetrics>();
        int failures = 0;
        for (int number = from; number <= to; number++)
        {
            string path = Path.Combine(outDir, FileNameOf(number));
            LevelSignature signature;
            if (File.Exists(path) && !overwrite && !(repair && NeedsRepair(number)))
            {
                signature = LevelSignature.Of(LevelJson.Load(path));
                Console.WriteLine($"#{number:0000} kept (exists)");
            }
            else
            {
                try
                {
                    GeneratedLevel generated = generator.Generate(number, recent.ToArray());
                    LevelJson.Save(generated.Level, path);
                    rows.Add(generated.Metrics);
                    signature = LevelSignature.Of(generated.Level);
                    Console.WriteLine(Describe(generated.Metrics));
                }
                catch (GenerationFailedException ex)
                {
                    failures++;
                    Console.WriteLine($"#{number:0000} FAILED: {ex.Message}");
                    continue;
                }
            }

            recent.Enqueue(signature);
            while (recent.Count > profile.NoveltyWindow)
            {
                recent.Dequeue();
            }
        }

        GenerationReport.Upsert(Path.Combine(outDir, GenerationReport.FileName), rows);
        if (rows.Count > 0)
        {
            Console.WriteLine();
            Console.Write(GenerationReport.Summarize(rows));
            Console.WriteLine($"{rows.Count} generated in {rows.Sum(r => r.Seconds):0} s, report: {Path.Combine(outDir, GenerationReport.FileName)}");
        }

        return failures == 0 ? 0 : 1;
    }

    private static string FileNameOf(int number) => $"level_{number.ToString("0000", CultureInfo.InvariantCulture)}.json";

    private static IEnumerable<LevelSignature> LoadRecent(string outDir, int from, int window)
    {
        for (int number = Math.Max(1, from - window); number < from; number++)
        {
            string path = Path.Combine(outDir, FileNameOf(number));
            if (File.Exists(path))
            {
                yield return LevelSignature.Of(LevelJson.Load(path));
            }
        }
    }

    /// <summary>Fewer games everywhere: for previewing a profile, not for shipping levels.</summary>
    private static GenerationProfile Quick(GenerationProfile profile) => profile with
    {
        Calibration = profile.Calibration with { Runs = 60, VerifyRuns = 80, ReportRuns = 30 },
        SeedPool = new SeedPoolSettings { Candidates = 12, GamesPerSeed = 12, PoolSize = 8, TolerancePoints = 10 },
    };

    private static string Describe(LevelMetrics m)
    {
        string bots = string.Join(" ", m.BotWinRates.Select(b => $"{b.Key} {b.Value:P0}"));
        string relaxed = m.Relaxed ? " RELAXED" : string.Empty;
        string floor = m.Floor is FloorCheck guard
            ? string.Format(CultureInfo.InvariantCulture, "  floor {0:P0} (min {1:P0}{2})", guard.WinRate.Measured, guard.WinRate.Required, m.FloorSetLimit ? ", set the limit" : string.Empty)
            : string.Empty;
        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0:0000} {1,-9} {2,-5} {3}c  moves {4}  {5} win {6:P0} (target {7:P0}){8}  {9}  pool {10} ({11:P0}-{12:P0})  {13}  {14:0.0}s{15}",
            m.Number,
            m.Tier,
            m.Size,
            m.Colors,
            DescribeMoves(m),
            m.CalibrationBot,
            m.WinRate,
            m.TargetWinRate,
            floor,
            bots,
            m.PoolSize,
            m.PoolMinWinRate,
            m.PoolMaxWinRate,
            m.Style,
            m.Seconds,
            relaxed);
    }

    /// <summary>
    /// The final limit and the move range. With a move margin the calibrated limit is shown too, next to the range: the
    /// range, like the target win rate, is about the calibrated limit, not the final one. The margin is printed as the
    /// share the profile and the report hold (a rounded percentage would show a small margin as none).
    /// </summary>
    private static string DescribeMoves(LevelMetrics m) => m.MoveMargin.IsNone
        ? string.Format(CultureInfo.InvariantCulture, "{0,2} [{1}]", m.MoveLimit, m.MoveRange)
        : string.Format(
            CultureInfo.InvariantCulture,
            "{0,2} (calibrated {1,2} [{2}], margin {3}{4})",
            m.MoveLimit,
            m.CalibratedMoves,
            m.MoveRange,
            m.MoveMargin,
            m.MarginCutByMoveCap ? ", cut at the move cap" : string.Empty);
}
