using System.Diagnostics;
using System.Globalization;
using Puzzle.Bot;
using Puzzle.Core.Levels;
using Puzzle.Tooling;
using Puzzle.Tooling.Ladder;
using Puzzle.Tooling.Simulation;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>ladder &lt;dir|file&gt; [--from 1] [--to &lt;last&gt;] [--from-skill 30] [--to-skill 100] [--step 5] [--runs 60]
/// [--cap 150] [--seed 7] [--games fresh|attempts] [--out reports/ladder.csv]</c>: measures existing levels against the bots of the skill ladder
/// (<see cref="SkillLadder"/>), every bot of a level on the same fresh games, and writes one CSV row per level and bot
/// (<see cref="LadderCsv"/>). No level is changed. <c>--games attempts</c> plays the attempts a player meets (the level's
/// retry pool in turn) instead of fresh seeds; the report does not say which, so keep the two in files of their own.
/// A long measurement can be done in pieces with --from/--to: each run
/// replaces the rows of its own levels in the report and keeps the others. Progress goes to stderr; the summary on
/// stdout is each bot's mean win rate at the levels' move limits, overall and by tier.
/// </summary>
internal static class LadderCommand
{
    private const int LevelsPerProgressLine = 10;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string target = args.Required(0, "dir|file");
        string outPath = args.Text("out", "reports/ladder.csv");
        LadderSettings settings = SettingsFrom(args);
        SkillProfile[] bots = BotsFrom(args);
        LevelDefinition[] levels = LevelsFrom(target, args, settings.MoveCap);
        LadderCsv.RequireReportOrNoFile(outPath); // a wrong --out stops the run before the first game, not after the last

        List<LadderRow> rows = MeasureReportingProgress(levels, bots, settings);
        PrintSummary(rows, settings);
        return WriteReport(rows, outPath);
    }

    /// <summary>Every level against every bot, with the progress and, at the end, the cost on stderr.</summary>
    private static List<LadderRow> MeasureReportingProgress(LevelDefinition[] levels, SkillProfile[] bots, LadderSettings settings)
    {
        var rows = new List<LadderRow>(levels.Length * bots.Length);
        var clock = Stopwatch.StartNew();
        foreach (LadderRow row in LadderMeasurement.Measure(levels, bots, settings))
        {
            rows.Add(row);
            ReportProgress(rows.Count, bots.Length, levels.Length, clock.Elapsed);
        }

        ReportTiming(rows.Count, settings.Runs, clock.Elapsed);
        return rows;
    }

    /// <exception cref="UsageException">Fewer than one run, or a move cap below one.</exception>
    private static LadderSettings SettingsFrom(CliArgs args)
    {
        var defaults = new LadderSettings();
        int runs = args.Int("runs", defaults.Runs);
        int cap = args.Int("cap", defaults.MoveCap);
        if (runs < 1 || cap < 1)
        {
            throw new UsageException("--runs and --cap must be at least 1.");
        }

        return defaults with { Runs = runs, MoveCap = cap, Seed = args.ULong("seed", defaults.Seed), Games = GamesFrom(args) };
    }

    /// <summary>--games: fresh seeds unless the attempts a player meets are asked for.</summary>
    /// <exception cref="UsageException">The option names neither kind of games.</exception>
    private static BatchGames GamesFrom(CliArgs args) => args.Text("games", "fresh") switch
    {
        "fresh" => BatchGames.FreshSeeds,
        "attempts" => BatchGames.PlayerAttempts,
        string other => throw new UsageException($"--games is 'fresh' (seeds of the run's own) or 'attempts' (each level's retry pool in turn), not '{other}'."),
    };

    private static string Described(BatchGames games) => games switch
    {
        BatchGames.FreshSeeds => "fresh seeds",
        BatchGames.PlayerAttempts => "the levels' own attempts (their retry pools in turn)",
        _ => throw new InvalidOperationException($"Unhandled batch games {games}."),
    };

    /// <summary>The rungs from --from-skill to --to-skill, every --step points, weakest first.</summary>
    /// <exception cref="UsageException">The options do not name rungs of the ladder.</exception>
    private static SkillProfile[] BotsFrom(CliArgs args)
    {
        int fromSkill = args.Int("from-skill", SkillLadder.WeakestSkill);
        int toSkill = args.Int("to-skill", SkillLadder.StrongestSkill);
        int step = args.Int("step", SkillLadder.Step);
        if (!IsRung(fromSkill) || !IsRung(toSkill) || toSkill < fromSkill)
        {
            throw new UsageException(
                $"--from-skill and --to-skill must be rungs of the skill ladder, the weaker one first: {SkillLadder.NamePattern}.");
        }

        if (step < SkillLadder.Step || step % SkillLadder.Step != 0)
        {
            throw new UsageException($"--step must be {SkillLadder.Step} or a multiple of it: the rungs are {SkillLadder.Step} points apart.");
        }

        return SkillLadder.Rungs
            .Where(rung => rung.NoticePercent >= fromSkill && rung.NoticePercent <= toSkill)
            .Where(rung => (rung.NoticePercent - fromSkill) % step == 0)
            .ToArray();
    }

    private static bool IsRung(int skill) => SkillLadder.TryFind(SkillLadder.NameOf(skill), out _);

    /// <summary>The levels numbered --from to --to, in level order. A broken file stops the run before any game.</summary>
    /// <exception cref="UsageException">
    /// No level is in the range, two files hold the same level, or a level's move limit is past the move cap.
    /// </exception>
    private static LevelDefinition[] LevelsFrom(string target, CliArgs args, int cap)
    {
        var range = new LevelRange(args.Int("from", 1), args.OptionalInt("to"));
        if (!range.IsWellFormed)
        {
            throw new UsageException("--from must be >= 1 and --to >= --from.");
        }

        LevelDefinition[] levels = LevelFiles.Under(target)
            .Select(LevelJson.Load)
            .Where(level => range.Contains(level.Id))
            .OrderBy(level => level.Id.Value)
            .ToArray();
        if (levels.Length == 0)
        {
            throw new UsageException($"No level numbered {range} in '{target}'.");
        }

        if (levels.GroupBy(level => level.Id).FirstOrDefault(sameId => sameId.Count() > 1) is { } twice)
        {
            throw new UsageException($"Two files in '{target}' hold level {twice.Key}; the report has one row per level and bot, so name one level set.");
        }

        if (levels.FirstOrDefault(level => level.MoveLimit > cap) is { } cutShort)
        {
            throw new UsageException($"--cap {cap} is below the move limit of level {cutShort.Id}, {cutShort.MoveLimit}; games are followed only to the cap.");
        }

        return levels;
    }

    /// <summary>The level numbers --from and --to select; without --to, every level from <paramref name="From"/> on.</summary>
    private readonly record struct LevelRange(int From, int? To)
    {
        public bool IsWellFormed => From >= 1 && Last >= From;

        private int Last => To ?? int.MaxValue;

        public bool Contains(LevelId level) => level.Value >= From && level.Value <= Last;

        public override string ToString() => To is int to ? $"{From} to {to}" : $"{From} or above";
    }

    /// <summary>A line on stderr for every few finished levels and for the last one; a level is finished with its last bot's row.</summary>
    private static void ReportProgress(int rowsDone, int botsPerLevel, int levels, TimeSpan elapsed)
    {
        if (rowsDone % botsPerLevel != 0)
        {
            return;
        }

        int levelsDone = rowsDone / botsPerLevel;
        if (levelsDone % LevelsPerProgressLine == 0 || levelsDone == levels)
        {
            Console.Error.WriteLine(string.Create(Invariant, $"{levelsDone}/{levels} levels, {elapsed.TotalSeconds:0} s"));
        }
    }

    /// <summary>What a level and bot cost, on stderr: stdout holds only what the same command prints every time.</summary>
    private static void ReportTiming(int pairings, int runs, TimeSpan elapsed) => Console.Error.WriteLine(string.Create(
        Invariant,
        $"{pairings} level-bot pairs, {(long)pairings * runs} games in {elapsed.TotalSeconds:0.0} s: {elapsed.TotalMilliseconds / pairings:0} ms per level and bot, {elapsed.TotalMilliseconds / pairings / runs:0.00} ms per game, on {Environment.ProcessorCount} threads"));

    /// <summary>
    /// Each bot's mean win rate at the move limits, over all measured levels and over those of each tier that has any.
    /// Every bot played every level, so the first bot's level counts are everyone's.
    /// </summary>
    private static void PrintSummary(List<LadderRow> rows, LadderSettings settings)
    {
        IReadOnlyList<LadderBotSummary> bots = LadderSummary.Of(rows);
        LadderBotSummary first = bots[0];
        DifficultyTier[] tiers = Enum.GetValues<DifficultyTier>().Where(first.ByTier.ContainsKey).ToArray();
        string levelsByTier = string.Join(", ", tiers.Select(tier => string.Create(Invariant, $"{tier} {first.ByTier[tier].Levels}")));
        Console.WriteLine(string.Create(
            Invariant,
            $"levels {rows[0].Level}..{rows[^1].Level}: {first.Overall.Levels} ({levelsByTier}); {bots.Count} bots, {settings.Runs} games per bot and level on {Described(settings.Games)}, cap {settings.MoveCap}, seed {settings.Seed}"));
        Console.WriteLine("mean share of games won within the level's move limit:");
        Console.WriteLine($"{"bot",-9} {"skill",5} {"all",7} {string.Join(' ', tiers.Select(tier => $"{tier,9}"))}  named");
        foreach (LadderBotSummary bot in bots)
        {
            string byTier = string.Join(' ', tiers.Select(tier => $"{SimCommand.Percent(bot.ByTier[tier].WinRate),9}"));
            string line = $"{bot.Bot.Name,-9} {bot.Bot.NoticePercent.ToString(Invariant),5} {SimCommand.Percent(bot.Overall.WinRate),7} {byTier}  {NamedProfileLike(bot.Bot)}";
            Console.WriteLine(line.TrimEnd());
        }
    }

    /// <summary>The name of the named profile that plays like the rung; empty for a rung between or below the named ones.</summary>
    private static string NamedProfileLike(SkillProfile rung) =>
        SkillProfile.All.FirstOrDefault(named => named.PlaysLike(rung))?.Name ?? string.Empty;

    /// <summary>
    /// Puts the rows into the report. A report that cannot be written is said so and fails the command; when only the
    /// old report could not be replaced (it is open in another program, say), the message names the file that holds the
    /// complete new one, so the games need not be played again.
    /// </summary>
    private static int WriteReport(List<LadderRow> rows, string outPath)
    {
        try
        {
            int kept = LadderCsv.Upsert(outPath, rows);
            Console.WriteLine($"wrote {outPath}: {rows.Count} rows of this run, {kept} rows of other levels kept");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not write '{outPath}': {ex.Message}");
            return 1;
        }
    }
}
