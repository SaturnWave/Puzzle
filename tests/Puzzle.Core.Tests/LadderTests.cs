using System.IO;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Tooling.Ladder;
using Puzzle.Tooling.Simulation;

namespace Puzzle.Core.Tests;

/// <summary>
/// The ladder report behind <c>puzzle ladder</c>: existing levels measured against the bots of the skill ladder
/// (<see cref="LadderMeasurement"/>), written as one CSV row per level and bot (<see cref="LadderCsv"/>) and summed up
/// per bot (<see cref="LadderSummary"/>).
/// </summary>
public sealed class LadderTests : IDisposable
{
    /// <summary>Few games on small boards: twenty per bot and level, each followed for sixty moves.</summary>
    private static readonly LadderSettings Quick = new() { Runs = 20, MoveCap = 60, Seed = 3 };

    /// <summary>Three levels the bots finish at different speeds: too tight for the weak ones, fair, and roomy.</summary>
    private static readonly LevelDefinition[] Levels =
    [
        Level(id: 4, DifficultyTier.Hard, redToCollect: 30, moves: 9),
        Level(id: 5, DifficultyTier.Normal, redToCollect: 30, moves: 14),
        Level(id: 9, DifficultyTier.Easy, redToCollect: 20, moves: 25),
    ];

    private static readonly SkillProfile[] Bots = [Rung(30), Rung(50), Rung(75), Rung(100)];

    private readonly TempFolder _folder = new();

    private string ReportPath => Path.Combine(_folder.Path, "reports", "ladder.csv");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void The_report_has_a_row_for_every_level_and_bot_under_the_ladder_columns_sorted_by_level_then_skill()
    {
        LadderCsv.Upsert(ReportPath, Measure(Levels.Reverse().ToArray(), Bots.Reverse().ToArray(), Quick));

        string[] lines = File.ReadAllLines(ReportPath);
        Assert.Equal("level,moveLimit,bot,skill,runs,winAtLimit,movesP50,movesP80,movesP90,movesP95,limitFor70,limitFor80,limitFor90,limitFor95", lines[0]);
        Assert.Equal(LadderCsv.Header, lines[0]);
        Assert.Equal(
            new[]
            {
                "4,9,skill-30,30,20", "4,9,skill-50,50,20", "4,9,skill-75,75,20", "4,9,skill-100,100,20",
                "5,14,skill-30,30,20", "5,14,skill-50,50,20", "5,14,skill-75,75,20", "5,14,skill-100,100,20",
                "9,25,skill-30,30,20", "9,25,skill-50,50,20", "9,25,skill-75,75,20", "9,25,skill-100,100,20",
            },
            lines.Skip(1).Select(line => string.Join(',', line.Split(',').Take(5))));
        Assert.All(lines, line => Assert.Equal(14, line.Split(',').Length));
        Assert.False(File.Exists(LadderCsv.PendingPathOf(ReportPath)));
    }

    [Fact]
    public void The_same_measurement_gives_the_same_rows_twice_and_whatever_the_number_of_workers()
    {
        string[] first = Lines(Measure(Levels, Bots, Quick));
        string[] second = Lines(Measure(Levels, Bots, Quick));
        string[] onOneWorker = Lines(Measure(Levels, Bots, Quick with { Workers = 1 }));
        string[] onThreeWorkers = Lines(Measure(Levels, Bots, Quick with { Workers = 3 }));
        string[] onManyWorkers = Lines(Measure(Levels, Bots, Quick with { Workers = 16 }));

        Assert.Equal(first, second);
        Assert.Equal(first, onOneWorker);
        Assert.Equal(first, onThreeWorkers);
        Assert.Equal(first, onManyWorkers);
        Assert.NotEqual(first, Lines(Measure(Levels, Bots, Quick with { Seed = 4 }))); // the seed is what the games come from
    }

    [Fact]
    public void Every_bot_of_a_level_plays_the_same_fresh_games_derived_from_the_seed_and_the_level()
    {
        LevelDefinition level = Levels[1];

        // Run i of the level, whichever bot plays it.
        RunResult[] GamesOf(SkillProfile bot) => Enumerable.Range(0, Quick.Runs)
            .Select(run => SimulationRunner.Play(level, new GreedyPolicy(bot), Rng.Derive(Rng.Derive(Quick.Seed, (ulong)level.Id.Value), (ulong)run), Quick.MoveCap))
            .ToArray();
        RunResult[][] games = Bots.Select(GamesOf).ToArray();
        string[] expected = Bots.Zip(games, (bot, played) => LadderCsv.Line(LadderRow.From(level, bot, LevelReport.From(played, Quick.MoveCap)))).ToArray();

        Assert.Equal(expected, Lines(Measure([level], Bots, Quick)));
        Assert.All(games, played => Assert.Equal(games[0].Select(game => game.Seed), played.Select(game => game.Seed)));
        Assert.Equal(Quick.Runs, games[0].Select(game => game.Seed).Distinct().Count());
    }

    [Fact]
    public void The_retry_pool_of_a_level_plays_no_part_in_its_measurement()
    {
        uint[] pool = [11, 22, 33, 44];
        LevelDefinition pooled = Level(id: 5, DifficultyTier.Normal, redToCollect: 30, moves: 14, pool);
        LevelDefinition unpooled = Level(id: 5, DifficultyTier.Normal, redToCollect: 30, moves: 14);

        Assert.Equal(Lines(Measure([unpooled], Bots, Quick)), Lines(Measure([pooled], Bots, Quick)));
    }

    [Fact]
    public void Measured_on_a_players_attempts_every_bot_plays_the_levels_retry_pool_in_turn_with_bot_choices_of_each_runs_own()
    {
        uint[] pool = [11, 22, 33];
        LevelDefinition level = Level(id: 5, DifficultyTier.Normal, redToCollect: 30, moves: 14, pool);
        LadderSettings onAttempts = Quick with { Games = BatchGames.PlayerAttempts };

        // Run i is attempt i of the level, whichever bot plays it; the run's own seed moves the bot alone.
        RunResult[] GamesOf(SkillProfile bot) => Enumerable.Range(0, onAttempts.Runs)
            .Select(run => SimulationRunner.Play(
                level,
                new GreedyPolicy(bot),
                level.Attempts.SeedsFor(run, unpooledSeed: 0),
                Rng.Derive(Rng.Derive(onAttempts.Seed, (ulong)level.Id.Value), (ulong)run),
                onAttempts.MoveCap))
            .ToArray();
        RunResult[][] games = Bots.Select(GamesOf).ToArray();
        string[] expected = Bots.Zip(games, (bot, played) => LadderCsv.Line(LadderRow.From(level, bot, LevelReport.From(played, onAttempts.MoveCap)))).ToArray();

        Assert.Equal(expected, Lines(Measure([level], Bots, onAttempts)));
        Assert.Equal(Enumerable.Range(0, onAttempts.Runs).Select(run => (ulong)pool[run % pool.Length]), games[0].Select(game => game.Seed));
        Assert.NotEqual(Lines(Measure([level], Bots, Quick)), Lines(Measure([level], Bots, onAttempts)));
    }

    [Fact]
    public void A_level_without_a_retry_pool_is_measured_on_a_players_attempts_as_on_fresh_seeds_of_another_kind()
    {
        LevelDefinition unpooled = Level(id: 5, DifficultyTier.Normal, redToCollect: 30, moves: 14);
        LadderSettings onAttempts = Quick with { Games = BatchGames.PlayerAttempts };

        LadderRow[] rows = Measure([unpooled], Bots, onAttempts);

        // Without a pool an attempt plays any seed, so the measurement still stands: every bot gets its row from all its runs.
        Assert.Equal(Bots.Length, rows.Length);
        Assert.All(rows, row => Assert.Equal(onAttempts.Runs, row.Runs));
        Assert.Equal(Lines(rows), Lines(Measure([unpooled], Bots, onAttempts)));
    }

    [Fact]
    public void A_level_is_measured_alike_alone_and_among_other_levels_and_bots()
    {
        string[] whole = Lines(Measure(Levels, Bots, Quick));

        string[] alone = Lines(Measure([Levels[1]], [Bots[2]], Quick));

        Assert.Equal(whole.Where(line => line.StartsWith("5,14,skill-75,", StringComparison.Ordinal)), alone);
    }

    [Fact]
    public void A_run_over_some_levels_replaces_their_rows_and_keeps_the_rows_of_the_other_levels()
    {
        LadderSettings longer = Quick with { Runs = 30 };
        int keptAtFirst = LadderCsv.Upsert(ReportPath, Measure(Levels, Bots, Quick));
        string[] before = File.ReadAllLines(ReportPath);

        int kept = LadderCsv.Upsert(ReportPath, Measure([Levels[1]], Bots, longer));

        string[] after = File.ReadAllLines(ReportPath);
        Assert.Equal((0, 8), (keptAtFirst, kept));
        Assert.Equal(before.Length, after.Length);
        Assert.Equal(before.Where(line => !IsOfLevel(line, 5)), after.Where(line => !IsOfLevel(line, 5)));
        Assert.Equal(Lines(Measure([Levels[1]], Bots, longer)), after.Where(line => IsOfLevel(line, 5)));
        Assert.Equal(new[] { "level", "4", "4", "4", "4", "5", "5", "5", "5", "9", "9", "9", "9" }, after.Select(line => line.Split(',')[0]));
    }

    [Fact]
    public void A_measurement_done_in_pieces_in_any_order_gives_the_report_of_one_run()
    {
        string inPieces = Path.Combine(_folder.Path, "pieces.csv");
        LadderCsv.Upsert(ReportPath, Measure(Levels, Bots, Quick));

        LadderCsv.Upsert(inPieces, Measure([Levels[2]], Bots, Quick));
        LadderCsv.Upsert(inPieces, Measure([Levels[0], Levels[1]], Bots, Quick));
        LadderCsv.Upsert(inPieces, Measure([Levels[1]], Bots, Quick)); // a repeated piece

        Assert.Equal(File.ReadAllBytes(ReportPath), File.ReadAllBytes(inPieces));
    }

    [Fact]
    public void A_level_measured_again_with_fewer_bots_keeps_no_row_of_its_earlier_measurement()
    {
        LadderCsv.Upsert(ReportPath, Measure(Levels, Bots, Quick));

        LadderCsv.Upsert(ReportPath, Measure([Levels[0]], [Bots[1]], Quick));

        // The bots of a level in the report always played the same games: its rows come from one measurement.
        string[] after = File.ReadAllLines(ReportPath);
        Assert.Equal(new[] { "4,9,skill-50,50,20" }, after.Where(line => IsOfLevel(line, 4)).Select(line => string.Join(',', line.Split(',').Take(5))));
        Assert.Equal(1 + 1 + 4 + 4, after.Length);
    }

    [Fact]
    public void Moves_needed_and_limits_never_fall_as_the_share_of_games_rises()
    {
        LadderRow[] rows = Measure(Levels, Bots, Quick);

        Assert.All(rows, row => Assert.Equal(NeverFalling(row.MovesNeeded), row.MovesNeeded));
        Assert.All(rows, row => Assert.Equal(NeverFalling(row.LimitFor), row.LimitFor));
        Assert.All(rows, row => Assert.Equal((4, 4), (row.MovesNeeded.Count, row.LimitFor.Count)));
        Assert.Equal(new[] { 70, 80, 90, 95 }, LadderRow.WinPercents);
        Assert.Equal(new[] { 50, 80, 90, 95 }, LadderRow.MovePercents);
    }

    [Fact]
    public void A_limit_for_a_win_rate_fits_the_move_limit_exactly_when_the_bot_wins_that_often_at_it()
    {
        LadderRow[] rows = Measure(Levels, Bots, Quick);

        var fits = new List<bool>();
        foreach (LadderRow row in rows)
        {
            foreach ((int percent, int? limit) in LadderRow.WinPercents.Zip(row.LimitFor))
            {
                bool limitFits = limit is int moves && moves <= row.MoveLimit;
                Assert.True(limitFits == (row.WinAtLimit >= percent / 100.0), $"level {row.Level} {row.Bot.Name}: wins {row.WinAtLimit:P0} at {row.MoveLimit} moves, limit for {percent} % is {limit}");
                fits.Add(limitFits);
            }
        }

        Assert.Contains(true, fits);    // the test levels have both kinds of rows
        Assert.Contains(false, fits);
    }

    [Fact]
    public void The_report_holds_the_rows_numbers_and_leaves_a_cell_empty_for_a_share_of_games_that_did_not_finish()
    {
        // Nobody collects 400 red gems within the 40 moves the games are followed for; 20 are collected long before.
        LevelDefinition hopeless = Level(id: 2, DifficultyTier.SuperHard, redToCollect: 400, moves: 10);
        LevelDefinition easy = Level(id: 3, DifficultyTier.Easy, redToCollect: 20, moves: 40);

        LadderRow[] rows = Measure([hopeless, easy], [Rung(100)], Quick with { MoveCap = 40 });

        Assert.Equal("2,10,skill-100,100,20,0.000,,,,,,,,", LadderCsv.Line(rows[0]));
        Assert.All(rows[0].MovesNeeded.Concat(rows[0].LimitFor), moves => Assert.Null(moves));
        Assert.Equal(1.0, rows[1].WinAtLimit);
        Assert.All(rows[1].MovesNeeded.Concat(rows[1].LimitFor), moves => Assert.InRange(Assert.NotNull(moves), 1, 40));
        Assert.StartsWith("3,40,skill-100,100,20,1.000,", LadderCsv.Line(rows[1]), StringComparison.Ordinal);
        Assert.Equal(
            rows[1].MovesNeeded.Concat(rows[1].LimitFor).Select(moves => $"{moves}"),
            LadderCsv.Line(rows[1]).Split(',').Skip(6));
    }

    [Fact]
    public void The_strongest_rung_needs_fewer_moves_than_the_weakest_and_wins_more_often_where_the_limit_is_tight()
    {
        LadderRow[] rows = Measure(Levels, [Rung(30), Rung(100)], Quick with { Runs = 40 });
        LadderRow[] weakest = rows.Where(row => row.Bot.NoticePercent == 30).ToArray();
        LadderRow[] strongest = rows.Where(row => row.Bot.NoticePercent == 100).ToArray();

        Assert.All(weakest.Zip(strongest), pair => Assert.True(pair.Second.MovesNeeded[0] < pair.First.MovesNeeded[0], $"level {pair.First.Level}: medians {pair.First.MovesNeeded[0]} and {pair.Second.MovesNeeded[0]}"));
        Assert.True(strongest[0].WinAtLimit > weakest[0].WinAtLimit, $"level {weakest[0].Level}: {weakest[0].WinAtLimit:P0} and {strongest[0].WinAtLimit:P0}");
    }

    [Fact]
    public void The_summary_is_each_bots_mean_win_rate_over_all_levels_and_over_those_of_each_tier()
    {
        LadderRow[] rows =
        [
            Row(level: 1, DifficultyTier.Easy, Rung(100), winAtLimit: 1.0),
            Row(level: 1, DifficultyTier.Easy, Rung(30), winAtLimit: 0.8),
            Row(level: 2, DifficultyTier.Hard, Rung(30), winAtLimit: 0.1),
            Row(level: 2, DifficultyTier.Hard, Rung(100), winAtLimit: 0.5),
            Row(level: 3, DifficultyTier.Easy, Rung(30), winAtLimit: 0.6),
            Row(level: 3, DifficultyTier.Easy, Rung(100), winAtLimit: 0.9),
        ];

        IReadOnlyList<LadderBotSummary> summary = LadderSummary.Of(rows);

        Assert.Equal(new[] { "skill-30", "skill-100" }, summary.Select(bot => bot.Bot.Name));
        Assert.Equal((3, 0.5), (summary[0].Overall.Levels, Math.Round(summary[0].Overall.WinRate, 9)));
        Assert.Equal((3, 0.8), (summary[1].Overall.Levels, Math.Round(summary[1].Overall.WinRate, 9)));
        Assert.Equal(new MeanWinAtLimit(Levels: 2, WinRate: 0.7), Rounded(summary[0].ByTier[DifficultyTier.Easy]));
        Assert.Equal(new MeanWinAtLimit(Levels: 1, WinRate: 0.1), Rounded(summary[0].ByTier[DifficultyTier.Hard]));
        Assert.Equal(new MeanWinAtLimit(Levels: 2, WinRate: 0.95), Rounded(summary[1].ByTier[DifficultyTier.Easy]));
        Assert.Equal(new[] { DifficultyTier.Easy, DifficultyTier.Hard }, summary[0].ByTier.Keys.Order()); // no measured level, no entry
    }

    [Fact]
    public void A_file_that_is_not_a_ladder_report_is_refused_and_left_as_it_is()
    {
        string otherReport = Path.Combine(_folder.Path, "report.csv");
        string brokenRow = Path.Combine(_folder.Path, "broken.csv");
        File.WriteAllLines(otherReport, ["level,tier,size", "1,Easy,10x10"]);
        File.WriteAllLines(brokenRow, [LadderCsv.Header, "4,9,skill-30,thirty,20,0.500,,,,,,,,"]);
        LadderRow[] rows = Measure([Levels[2]], [Rung(100)], Quick);

        LadderReportFormatException asked = Assert.Throws<LadderReportFormatException>(() => LadderCsv.RequireReportOrNoFile(otherReport));
        Assert.Throws<LadderReportFormatException>(() => LadderCsv.Upsert(otherReport, rows));
        LadderReportFormatException broken = Assert.Throws<LadderReportFormatException>(() => LadderCsv.Upsert(brokenRow, rows));

        Assert.Contains("is not a ladder report", asked.Message);
        Assert.Contains("line 2", broken.Message);
        Assert.Equal(new[] { "level,tier,size", "1,Easy,10x10" }, File.ReadAllLines(otherReport));
        Assert.Equal(2, File.ReadAllLines(brokenRow).Length);
        Assert.Equal(new[] { "broken.csv", "report.csv" }, Directory.GetFiles(_folder.Path).Select(Path.GetFileName).Order()); // and nothing written beside them
        LadderCsv.RequireReportOrNoFile(ReportPath); // no file there: nothing to refuse
    }

    [Fact]
    public void A_report_that_cannot_be_replaced_stays_and_the_complete_new_one_waits_beside_it()
    {
        // A folder where the report should be stands in for a report another program holds open: neither gives way.
        Directory.CreateDirectory(ReportPath);
        LadderRow[] rows = Measure([Levels[2]], [Rung(100)], Quick);

        IOException refused = Assert.Throws<IOException>(() => LadderCsv.Upsert(ReportPath, rows));

        string waiting = LadderCsv.PendingPathOf(ReportPath);
        Assert.Contains($"The new report is complete at '{waiting}'", refused.Message);
        Assert.NotNull(refused.InnerException);
        Assert.True(Directory.Exists(ReportPath));
        Assert.Equal(new[] { LadderCsv.Header, LadderCsv.Line(rows[0]) }, File.ReadAllLines(waiting));
    }

    [Fact]
    public void A_level_whose_move_limit_is_past_the_move_cap_is_refused_before_any_game()
    {
        LadderSettings cappedAt20 = Quick with { MoveCap = 20 };

        ArgumentException refused = Assert.Throws<ArgumentException>(() => LadderMeasurement.Measure(Levels, Bots, cappedAt20));

        Assert.Contains("Level 9 has a move limit of 25, past the move cap of 20", refused.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => Quick with { Runs = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => Quick with { MoveCap = 0 });
    }

    [Fact]
    public void Two_rows_for_the_same_level_and_skill_are_refused()
    {
        LadderRow[] twice = Measure([Levels[2]], [Rung(100), Rung(100)], Quick);

        Assert.Throws<ArgumentException>(() => LadderCsv.Upsert(ReportPath, twice));
        Assert.False(File.Exists(ReportPath));
    }

    private static LadderRow[] Measure(LevelDefinition[] levels, SkillProfile[] bots, LadderSettings settings) =>
        LadderMeasurement.Measure(levels, bots, settings).ToArray();

    private static string[] Lines(IEnumerable<LadderRow> rows) => rows.Select(LadderCsv.Line).ToArray();

    private static bool IsOfLevel(string line, int level) => line.StartsWith($"{level},", StringComparison.Ordinal);

    /// <summary>The numbers as they would be if none were lower than one before it; an absent one is past every number.</summary>
    private static IEnumerable<int?> NeverFalling(IReadOnlyList<int?> moves) => moves.OrderBy(value => value ?? int.MaxValue);

    private static MeanWinAtLimit Rounded(MeanWinAtLimit mean) => mean with { WinRate = Math.Round(mean.WinRate, 9) };

    private static SkillProfile Rung(int skill) =>
        SkillLadder.TryFind(SkillLadder.NameOf(skill), out SkillProfile? rung) ? rung : throw new ArgumentOutOfRangeException(nameof(skill), skill, "Not a rung of the ladder.");

    private static LadderRow Row(int level, DifficultyTier tier, SkillProfile bot, double winAtLimit) =>
        new(new LevelId(level), tier, MoveLimit: 20, bot, Runs: 10, winAtLimit, MovesNeeded: [null, null, null, null], LimitFor: [null, null, null, null]);

    /// <summary>An open 8x8 board on which <paramref name="redToCollect"/> red gems are to be collected within <paramref name="moves"/> moves.</summary>
    private static LevelDefinition Level(int id, DifficultyTier tier, int redToCollect, int moves, uint[]? seedPool = null) => new(
        new LevelId(id),
        tier,
        LevelLayout.Parse(new LayoutLayers { Cells = TestBoards.Blank(8, 8, '#') }),
        new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, redToCollect) },
        moves,
        new SpawnRules { Palette = GemColors.First(5) },
        new AttemptPolicy { Board = new StartBoard.Varied(), SeedPool = seedPool ?? [] });
}
