using System.IO;
using Puzzle.Bank.Measuring;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using Puzzle.Tooling;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The economy's measurement: bot games followed past the level's limit, kept in a file a run reads instead of
/// playing. The engine is the subject here, on boards small enough to play hundreds of games in a test.
/// </summary>
public class EconomyMeasurementTests
{
    private static readonly MoveCount Cap = new(150);
    private static readonly Percent[] Marks = [new(50), new(80), new(90)];
    private static readonly uint[] Pool = [101, 102, 103, 104, 105, 106, 107, 108];
    private static readonly SkillRung Rung = SkillRung.Parse("skill-55");

    private static readonly MeasurementPlan SmallPlan = new()
    {
        MoveCap = Cap,
        FirstRepeat = 10000,
        ProgressMarks = Marks,
        PlainGamesBySeedPosition = [4, 2],
        BoostedGamesPerSeed = 2,
        BoardStarts = [BoardStart.Plain, BoardStart.RocketAndBomb, BoardStart.ColorBomb],
    };

    private static LevelDefinition[] ThreeLevels() =>
        [.. Enumerable.Range(1, 3).Select(number => BankSupport.FixedLevel(number, pool: [11, 22, 33]))];

    private static MeasureRequest Request(string path, LevelDefinition[]? levels = null, int? workers = null) => new()
    {
        Path = path,
        Plan = SmallPlan,
        Levels = levels ?? ThreeLevels(),
        Rungs = [SkillRung.Parse("skill-45"), Rung],
        Workers = workers,
    };

    [Fact]
    public void A_game_followed_past_its_limit_tells_what_the_limit_the_plus_three_and_the_plus_five_win()
    {
        LevelDefinition level = LevelWithALimitHalfTheGamesWin();
        var seen = new HashSet<string>();

        foreach (BoardStart start in BoardStarts.All)
        {
            foreach (bool plusThree in new[] { false, true }.Where(with => !with || start == BoardStart.Plain))
            {
                foreach (bool takesTheOffer in new[] { false, true })
                {
                    foreach ((uint seed, int repeat) in SeedsAndRepeats())
                    {
                        MeasuredGame stored = EconomyGame.Play(level, new GreedyPolicy(Rung.Model.ToProfile()), start, seed, repeat, Cap, Marks);
                        AttemptReading read = Reading(stored, level, plusThree, takesTheOffer);
                        RealAttempt real = PlayForReal(level, start, plusThree, takesTheOffer, seed, repeat);

                        string game = $"{start}, +3 {plusThree}, offer {takesTheOffer}, seed {seed}, repeat {repeat}";
                        Assert.True(real.Ending == read.Ending, $"{game}: the engine says {real.Ending}, the stored game {read.Ending}");
                        Assert.True(real.MovesUsed == read.MovesPlayed.Value, $"{game}: {real.MovesUsed} moves in the engine, {read.MovesPlayed} read");
                        Assert.True(real.OfferTaken == read.ExtraMoves.IsTaken, $"{game}: offer taken {real.OfferTaken} in the engine");
                        Assert.True(real.GoalsDoneWhenOutOfMoves == read.GoalsDoneThen, $"{game}: goals done {real.GoalsDoneWhenOutOfMoves} in the engine, {read.GoalsDoneThen} read");
                        seen.Add($"{(plusThree ? "+3" : start.FileName())} {(real.OfferTaken ? "with the offer" : "at the limit")} {(real.Ending is AttemptEnding.Won ? "won" : "lost")}");
                    }
                }
            }
        }

        // The comparison saw every case it is about: each way of starting, won at the limit and won by the offer.
        string[] expected =
        [
            "plain at the limit won", "plain at the limit lost", "plain with the offer won", "plain with the offer lost",
            "+3 at the limit won", "+3 at the limit lost", "+3 with the offer won",
            "start-rocket-and-bomb at the limit won", "start-rocket-and-bomb with the offer won",
            "start-color-bomb at the limit won", "start-color-bomb with the offer won",
        ];
        Assert.Empty(expected.Except(seen));
    }

    [Fact]
    public void A_plain_economy_game_is_the_game_the_bank_plays_for_that_seed_and_repeat()
    {
        LevelDefinition level = BankSupport.FixedLevel(pool: Pool);
        var bot = new GreedyPolicy(Rung.Model.ToProfile());

        foreach ((uint seed, int repeat) in SeedsAndRepeats())
        {
            RunResult banks = LadderMeasurer.Play(level, bot, seed, repeat, Cap.Value);
            MeasuredGame economys = EconomyGame.Play(level, bot, BoardStart.Plain, seed, repeat, Cap, Marks);

            Assert.Equal(banks.CompletedAtMove, (economys.End as GameEnd.Won)?.OnMove.Value);
            Assert.Equal(banks.MovesUsed, economys.MovesPlayedWithin(Cap).Value);
            for (int mark = 0; mark < Marks.Length; mark++)
            {
                int reachedOn = banks.ProgressAfterMove.TakeWhile(progress => progress < Marks[mark].Value / 100f).Count();
                Assert.Equal(reachedOn < banks.ProgressAfterMove.Count ? reachedOn : null, economys.ReachedOn[mark]?.Value);
            }
        }
    }

    [Fact]
    public void Economy_games_use_repeats_no_bank_purpose_owns()
    {
        int lastOfTheBank = Enum.GetValues<GamePurpose>().Max(purpose => purpose.FirstRepeat() + GamePurposes.RepeatsPerPurpose - 1);

        Assert.Equal(lastOfTheBank + 1, SettingsLoader.FirstRepeatOutsideTheBank);
        Assert.True(EconomySupport.Settings().Measurement.FirstRepeat > lastOfTheBank);
        EconomySettingsFormatException refused = Assert.Throws<EconomySettingsFormatException>(
            () => EconomySupport.Settings($$"""{ "measurement": { "firstRepeat": {{lastOfTheBank}} } }"""));
        Assert.Contains("measurement.firstRepeat", refused.Message);
    }

    [Fact]
    public void A_stuck_game_keeps_the_move_it_got_stuck_on()
    {
        // Boxes all around one rocket: after it has fired there is nothing to move and nothing to shuffle.
        LevelDefinition level = Level(Blank(3, 3, '3').With(1, 1, 'H'), moves: 2);

        MeasuredGame stored = EconomyGame.Play(level, new GreedyPolicy(Rung.Model.ToProfile()), BoardStart.Plain, poolSeed: 5, repeat: 10000, Cap, Marks);

        Assert.Equal(new GameEnd.Stuck(new MoveCount(1)), stored.End);
        Assert.Equal(stored.End, GameEnd.FromStored(stored.End.ToStored()));
        Assert.Equal(new MoveCount(1), stored.MovesPlayedWithin(new MoveCount(40)));
        Assert.True(stored.StuckWithin(new MoveCount(1)));
        Assert.False(stored.StuckBefore(new MoveCount(1)));
        Assert.True(stored.StuckBefore(new MoveCount(2)));

        // With two moves the engine ends the game for lack of a move; with one it is out of moves, and takes no offer.
        Assert.Equal(new AttemptEnding.LostNoMovePossible(), Reading(stored, level, plusThree: false, takesTheOffer: true).Ending);
        Assert.Equal(new AttemptEnding.LostNoMovePossible(), PlayForReal(level, BoardStart.Plain, false, true, 5, 10000).Ending);
        LevelDefinition oneMove = Level(Blank(3, 3, '3').With(1, 1, 'H'), moves: 1);
        AttemptReading atTheLimit = Reading(stored, oneMove, plusThree: false, takesTheOffer: true);
        Assert.Equal(new AttemptEnding.LostOutOfMoves(), atTheLimit.Ending);
        Assert.IsType<ExtraMovesDecision.NotOffered>(atTheLimit.ExtraMoves);
        RealAttempt real = PlayForReal(oneMove, BoardStart.Plain, false, true, 5, 10000);
        Assert.Equal(new AttemptEnding.LostOutOfMoves(), real.Ending);
        Assert.False(real.OfferTaken);
    }

    [Fact]
    public void The_file_is_the_same_bytes_whatever_the_number_of_workers()
    {
        using var folder = new TemporaryEconomyFolder();

        MeasureOutcome one = Measurer.Run(Request(folder.File("one.jsonl"), workers: 1));
        MeasureOutcome four = Measurer.Run(Request(folder.File("four.jsonl"), workers: 4));

        Assert.Equal(18, one.LinesPlayed);
        Assert.Equal(one.GamesPlayed, four.GamesPlayed);
        Assert.Equal(File.ReadAllBytes(folder.File("one.jsonl")), File.ReadAllBytes(folder.File("four.jsonl")));
        Assert.DoesNotContain((byte)'\r', File.ReadAllBytes(folder.File("one.jsonl")));
        Assert.Equal(one.Lines.Select(line => line.Key), Measurement.Load(folder.File("one.jsonl")).Lines.Select(line => line.Key));
    }

    [Fact]
    public void A_measurement_cut_short_goes_on_and_ends_as_the_same_file()
    {
        using var folder = new TemporaryEconomyFolder();
        string whole = folder.File("whole.jsonl");
        string resumed = folder.File("resumed.jsonl");
        Measurer.Run(Request(whole));
        string[] lines = File.ReadAllLines(whole);

        // What a stopped run leaves: the first line, seven finished lines and one that was being written.
        File.WriteAllText(MeasurementFile.PartPathOf(resumed), string.Join('\n', lines.Take(8)) + "\n" + lines[8][..(lines[8].Length / 2)]);
        MeasureOutcome outcome = Measurer.Run(Request(resumed));

        Assert.Equal(7, outcome.LinesKept);
        Assert.Equal(11, outcome.LinesPlayed);
        Assert.Equal(File.ReadAllBytes(whole), File.ReadAllBytes(resumed));
        Assert.False(File.Exists(MeasurementFile.PartPathOf(resumed)));

        MeasureOutcome again = Measurer.Run(Request(resumed));
        Assert.Equal(18, again.LinesKept);
        Assert.Equal(0, again.GamesPlayed);
    }

    [Fact]
    public void A_changed_level_is_measured_again_and_the_others_are_kept()
    {
        using var folder = new TemporaryEconomyFolder();
        string path = folder.File("measurement.jsonl");
        Measurer.Run(Request(path));
        LevelDefinition[] levels = ThreeLevels();
        levels[1] = BankSupport.FixedLevel(2, moves: 31, pool: [11, 22, 33]);

        MeasureOutcome outcome = Measurer.Run(Request(path, levels));

        Assert.Equal(12, outcome.LinesKept);
        Assert.Equal(6, outcome.LinesPlayed);
        Measurement measurement = Measurement.Load(path);
        Assert.All(measurement.Lines.Where(line => line.Level.Value == 2), line => Assert.Equal(LevelJson.Fingerprint(levels[1]), line.Fingerprint));
        Assert.Equal(18, measurement.Lines.Count);

        // A range measures its own levels and keeps the lines of the others.
        MeasureOutcome range = Measurer.Run(Request(path, [levels[2]]) with { Restart = false });
        Assert.Equal(18, range.LinesKept);
    }

    [Fact]
    public void A_torn_last_line_is_dropped_and_any_other_broken_line_is_refused()
    {
        using var folder = new TemporaryEconomyFolder();
        string whole = folder.File("whole.jsonl");
        Measurer.Run(Request(whole));
        string[] lines = File.ReadAllLines(whole);
        string torn = folder.File("torn.jsonl");
        string broken = folder.File("broken.jsonl");

        File.WriteAllText(torn, string.Join('\n', lines.Take(5)) + "\n" + lines[5][..20]);
        Assert.Equal(4, MeasurementFile.ReadPart(torn)!.Lines.Count);
        MeasurementFormatException notWhole = Assert.Throws<MeasurementFormatException>(() => MeasurementFile.Read(torn));
        Assert.Contains("cut off", notWhole.Message);

        File.WriteAllText(broken, string.Join('\n', lines.Take(3)) + "\n" + lines[3][..20] + "\n" + lines[4] + "\n");
        MeasurementFormatException damaged = Assert.Throws<MeasurementFormatException>(() => MeasurementFile.ReadPart(broken));
        Assert.Contains("line 4", damaged.Message);
    }

    [Fact]
    public void A_measurement_made_otherwise_is_refused_until_it_is_started_again()
    {
        using var folder = new TemporaryEconomyFolder();
        string path = folder.File("measurement.jsonl");
        Measurer.Run(Request(path));
        byte[] before = File.ReadAllBytes(path);
        MeasureRequest deeper = Request(path) with { Plan = SmallPlan with { BoostedGamesPerSeed = 4 } };

        MeasurementMismatchException refused = Assert.Throws<MeasurementMismatchException>(() => Measurer.Run(deeper));

        Assert.Contains("boostedGamesPerSeed", refused.Message);
        Assert.Equal(before, File.ReadAllBytes(path));
        MeasureOutcome restarted = Measurer.Run(deeper with { Restart = true });
        Assert.Equal(0, restarted.LinesKept);
        Assert.Equal(4, restarted.Spec.BoostedGamesPerSeed);
    }

    [Fact]
    public void A_measurement_is_started_again_in_a_folder_that_is_not_there_yet()
    {
        using var folder = new TemporaryEconomyFolder();
        string path = Path.Combine(folder.Path, "a new folder", "measurement.jsonl");

        MeasureOutcome outcome = Measurer.Run(Request(path) with { Restart = true });

        Assert.Equal(18, outcome.LinesPlayed);
        Assert.Equal(18, Measurement.Load(path).Lines.Count);
    }

    [Fact]
    public void What_a_stopped_run_of_another_engine_left_behind_is_named_and_the_measurement_beside_it_is_not_blamed()
    {
        using var folder = new TemporaryEconomyFolder();
        string path = folder.File("measurement.jsonl");
        Measurer.Run(Request(path));
        byte[] before = File.ReadAllBytes(path);
        string partPath = MeasurementFile.PartPathOf(path);
        string[] lines = File.ReadAllLines(path);
        MeasurementSpec ofAnotherEngine = MeasurementSpec.OfThisBuild(SmallPlan) with { Engine = new string('a', 64) };
        File.WriteAllText(partPath, MeasurementFile.HeaderText(ofAnotherEngine) + "\n" + lines[1] + "\n");

        MeasurementMismatchException refused = Assert.Throws<MeasurementMismatchException>(() => Measurer.Run(Request(path)));

        // The advice is to delete the one stale file; following the old message would have discarded the good measurement.
        Assert.StartsWith($"'{partPath}' was measured otherwise", refused.Message);
        Assert.Contains("engine fingerprint", refused.Message);
        Assert.Contains("delete that one file", refused.Message);
        Assert.DoesNotContain("--restart", refused.Message);
        Assert.Equal(before, File.ReadAllBytes(path));

        File.Delete(partPath);
        Assert.Equal(18, Measurer.Run(Request(path)).LinesKept);

        // The measurement itself, when it is the one made otherwise, is the one named.
        MeasureRequest deeper = Request(path) with { Plan = SmallPlan with { BoostedGamesPerSeed = 4 } };
        MeasurementMismatchException ofTheFile = Assert.Throws<MeasurementMismatchException>(() => Measurer.Run(deeper));
        Assert.StartsWith($"'{path}' was measured otherwise", ofTheFile.Message);
        Assert.Contains("--restart", ofTheFile.Message);
    }

    [Fact]
    public void A_measurement_path_that_is_a_folder_is_refused_by_name()
    {
        using var folder = new TemporaryEconomyFolder();

        MeasurementFormatException refused = Assert.Throws<MeasurementFormatException>(() => Measurement.Load(folder.Path));

        Assert.Contains("measurement", refused.Message);
        Assert.Contains(folder.Path, refused.Message);
    }

    [Fact]
    public void A_run_refuses_a_measurement_of_another_engine_or_of_a_changed_level()
    {
        using var folder = new TemporaryEconomyFolder();
        MeasureOutcome measured = Measurer.Run(Request(folder.File("measurement.jsonl")));
        Dictionary<int, LevelDefinition> today = ThreeLevels().ToDictionary(level => level.Id.Value);
        Dictionary<int, LevelDefinition> changed = new(today) { [2] = BankSupport.FixedLevel(2, moves: 31, pool: [11, 22, 33]) };

        GuardReport sound = MeasurementGuard.Check(measured.Spec, measured.Lines, today);
        MeasurementMismatchException otherEngine = Assert.Throws<MeasurementMismatchException>(
            () => MeasurementGuard.Check(measured.Spec with { Engine = new string('0', 64) }, measured.Lines, today));
        MeasurementMismatchException otherLevel = Assert.Throws<MeasurementMismatchException>(() => MeasurementGuard.Check(measured.Spec, measured.Lines, changed));

        Assert.Equal(new GuardReport(18, MeasurementGuard.GamesReplayed), sound);
        Assert.Contains("another engine", otherEngine.Message);
        Assert.Contains(": 2.", otherLevel.Message);

        // The moves the booster and the offer add are played by nobody, so the guard compares the file's two numbers outright.
        MeasurementMismatchException otherMoves = Assert.Throws<MeasurementMismatchException>(
            () => MeasurementGuard.Check(measured.Spec with { StartBoosterExtraMoves = new MoveCount(9), ExtraMovesOfferMoves = new MoveCount(12) }, measured.Lines, today));
        Assert.Contains("the start booster's moves 9 instead of 3", otherMoves.Message);
        Assert.Contains("the extra-moves offer's moves 12 instead of 5", otherMoves.Message);
    }

    [Fact]
    public void A_stored_game_that_no_longer_replays_is_refused()
    {
        using var folder = new TemporaryEconomyFolder();
        MeasureOutcome measured = Measurer.Run(Request(folder.File("measurement.jsonl")));
        Dictionary<int, LevelDefinition> today = ThreeLevels().ToDictionary(level => level.Id.Value);

        // As a change of how a booster starts a board would leave them: the plain games stand, the boosted ones do not.
        MeasuredLine[] tampered = measured.Lines
            .Select(line => line.Start == BoardStart.Plain ? line : line with { Seeds = line.Seeds.Select(OneMoveLater).ToArray() })
            .ToArray();

        MeasurementMismatchException refused = Assert.Throws<MeasurementMismatchException>(() => MeasurementGuard.Check(measured.Spec, tampered, today));

        Assert.Contains("no longer replay", refused.Message);
        Assert.Contains("start-", refused.Message);
    }

    private static SeedGames OneMoveLater(SeedGames seed) => new(
        seed.Games.Select(game => new MeasuredGame(GameEnd.FromStored(game.End.ToStored() + 1), Marks, game.ReachedOn)).ToArray(),
        Marks);

    private static IEnumerable<(uint Seed, int Repeat)> SeedsAndRepeats() =>
        from seed in Pool
        from repeat in Enumerable.Range(10000, 6)
        select (seed, repeat);

    /// <summary>The small level with the limit the median plain game needs, so that the offer and the booster decide many games.</summary>
    private static LevelDefinition LevelWithALimitHalfTheGamesWin()
    {
        LevelDefinition unlimited = BankSupport.FixedLevel(pool: Pool);
        var bot = new GreedyPolicy(Rung.Model.ToProfile());
        int[] needed = SeedsAndRepeats()
            .Select(game => EconomyGame.Play(unlimited, bot, BoardStart.Plain, game.Seed, game.Repeat, Cap, Marks).End)
            .OfType<GameEnd.Won>()
            .Select(won => won.OnMove.Value)
            .Order()
            .ToArray();
        return BankSupport.FixedLevel(moves: needed[needed.Length / 2], pool: Pool);
    }

    /// <summary>The attempt as the simulation reads it from a stored game, by a player who takes the offer whenever it is made, or never.</summary>
    private static AttemptReading Reading(MeasuredGame stored, LevelDefinition level, bool plusThree, bool takesTheOffer)
    {
        var spec = MeasurementSpec.OfThisBuild(SmallPlan);
        var always = new ExtraMovesHabit { TakeWhenGoalsDone = new Percent(0), WatchesAds = takesTheOffer, PaysCoins = false };
        var noWorth = new InLevelBoosterHabit
        {
            AssumedMovesWorth = BoosterKinds.All.Where(BoosterKinds.IsInLevel).ToDictionary(booster => booster, _ => MoveCount.None),
            UseWhenGoalsDone = new Percent(80),
        };
        var reader = new AttemptReader(spec, noWorth, new ExtraMovesRule(always, new Coins(900)));
        return reader.Read(stored, new AttemptTerms(new MoveCount(level.MoveLimit), plusThree, InLevelBoosterAtHand: null, LossesOnLevel: 0, Coins.None, FreeAdsLeftToday: 1));
    }

    /// <summary>The same attempt played in the engine with the level's real limit, the booster taken and the offer taken when it is made.</summary>
    private static RealAttempt PlayForReal(LevelDefinition level, BoardStart start, bool plusThree, bool takesTheOffer, uint seed, int repeat)
    {
        StartBooster[] boosters = plusThree ? [.. start.Boosters(), StartBooster.ExtraMoves] : [.. start.Boosters()];
        Game game = Game.Start(level, level.Attempts.SeedsForPlaySeed(seed), new GameOptions { StartBoosters = boosters });
        var bot = new GreedyPolicy(Rung.Model.ToProfile());
        var choices = new Rng(Rng.Derive(seed, (ulong)repeat));
        PlayOn(game, bot, choices);
        Percent? goalsDone = game.Status == GameStatus.LostOutOfMoves ? LargestMarkReached(game) : null;
        bool taken = takesTheOffer && game.Status == GameStatus.LostOutOfMoves && game.TryTakeExtraMoves();
        PlayOn(game, bot, choices);
        AttemptEnding ending = game.Status switch
        {
            GameStatus.Won => new AttemptEnding.Won(new MoveCount(game.MovesLeft)),
            GameStatus.LostOutOfMoves => new AttemptEnding.LostOutOfMoves(),
            GameStatus.LostNoMovesPossible => new AttemptEnding.LostNoMovePossible(),
            _ => throw new InvalidOperationException($"The game stopped while {game.Status}."),
        };
        return new RealAttempt(ending, game.MovesUsed, taken, goalsDone);
    }

    private static void PlayOn(Game game, GreedyPolicy bot, Rng choices)
    {
        var legalMoves = new List<Move>();
        while (game.Status == GameStatus.Playing)
        {
            legalMoves.Clear();
            game.CollectLegalMoves(legalMoves);
            Assert.True(game.TryPlay(bot.ChooseMove(game, legalMoves, choices)));
        }
    }

    private static Percent LargestMarkReached(Game game) =>
        Marks.Where(mark => (float)game.Goals.OverallProgress >= mark.Value / 100f).DefaultIfEmpty(new Percent(0)).Max();

    private sealed record RealAttempt(AttemptEnding Ending, int MovesUsed, bool OfferTaken, Percent? GoalsDoneWhenOutOfMoves);
}
