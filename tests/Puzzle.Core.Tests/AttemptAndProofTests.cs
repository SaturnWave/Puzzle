using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class AttemptAndProofTests
{
    [Fact]
    public void Single_seed_start_equals_the_attempt_seed_start()
    {
        LevelDefinition level = GameTests.ObstacleLevel();

        Game plain = Game.Start(level, 77);
        Game split = Game.Start(level, AttemptSeeds.From(77));

        Assert.Equal(plain.Board.ComputeHash(), split.Board.ComputeHash());
    }

    [Fact]
    public void Fixed_board_keeps_the_start_and_changes_the_future()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game first = Game.Start(level, AttemptSeeds.WithFixedBoard(5, 100));
        Game second = Game.Start(level, AttemptSeeds.WithFixedBoard(5, 200));

        Assert.Equal(first.Board.ComputeHash(), second.Board.ComputeHash());

        bool diverged = false;
        for (int i = 0; i < 5 && !diverged; i++)
        {
            Move move = first.FindLegalMoves()[0];
            first.TryPlay(move);
            second.TryPlay(move);
            diverged = first.Board.ComputeHash() != second.Board.ComputeHash();
        }

        Assert.True(diverged, "different play seeds must bring different refills");
    }

    [Fact]
    public void Attempts_cycle_through_the_pool_and_honor_the_start_board()
    {
        var policy = new AttemptPolicy { Board = new StartBoard.Fixed(9), SeedPool = new uint[] { 11, 22, 33 } };

        Assert.Equal(AttemptSeeds.WithFixedBoard(9, 11), policy.SeedsFor(0, unpooledSeed: 999));
        Assert.Equal(AttemptSeeds.WithFixedBoard(9, 33), policy.SeedsFor(2, unpooledSeed: 999));
        Assert.Equal(AttemptSeeds.WithFixedBoard(9, 11), policy.SeedsFor(3, unpooledSeed: 999));
        Assert.Equal(AttemptSeeds.From(999), AttemptPolicy.Default.SeedsFor(4, unpooledSeed: 999));
    }

    [Fact]
    public void Attempt_policy_survives_the_level_file()
    {
        LevelDefinition level = GameTests.OpenLevel()
            .WithAttempts(new AttemptPolicy { Board = new StartBoard.Fixed(1234), SeedPool = new uint[] { 5, 6, 7 } });

        LevelDefinition restored = LevelFileMapper.ToDefinition(LevelFileMapper.ToFile(level));

        Assert.Equal(new StartBoard.Fixed(1234), restored.Attempts.Board);
        Assert.Equal(new uint[] { 5, 6, 7 }, restored.Attempts.SeedPool);
        Assert.Null(LevelFileMapper.ToFile(GameTests.OpenLevel()).attempts);
    }

    [Fact]
    public void Seed_pool_keeps_only_seeds_near_the_typical_difficulty()
    {
        var settings = new SeedPoolSettings { Candidates = 12, GamesPerSeed = 8, PoolSize = 6, TolerancePoints = 15, MoveCap = 60 };

        SeedPoolReport report = SeedPoolCalibrator.Calibrate(
            GameTests.ObstacleLevel(), new StartBoard.Varied(), () => new GreedyPolicy(SkillProfile.Casual), settings);

        Assert.Equal(12, report.Candidates.Count);
        Assert.InRange(report.Pool.Count, 1, 6);
        Assert.All(report.Pool, seed => Assert.InRange(seed.WinRateAtLimit, report.TypicalWinRate - 0.15 - 1e-9, report.TypicalWinRate + 0.15 + 1e-9));
    }

    [Fact]
    public void Exhaustive_search_finds_a_one_move_win_and_proves_its_absence()
    {
        string[] tiles = Filler(7, 7).With(0, 5, 'p').With(1, 5, 'p').With(2, 4, 'p');
        Game winnable = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }));
        Game hopeless = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 40) }));

        Assert.True(ExhaustiveSearch.FindWinWithin(winnable, depth: 1).WinFound);
        ExhaustiveResult none = ExhaustiveSearch.FindWinWithin(hopeless, depth: 1);
        Assert.False(none.WinFound);
        Assert.Equal(hopeless.FindLegalMoves().Count, none.PositionsExamined);
        Assert.False(ExhaustiveSearch.FindWinWithin(winnable, depth: 0).WinFound);
    }
}
