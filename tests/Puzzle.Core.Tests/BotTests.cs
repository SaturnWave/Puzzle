using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Core.Tests;

public class BotTests
{
    [Fact]
    public void Report_reads_the_whole_win_rate_curve_from_completion_moves()
    {
        LevelReport report = LevelReport.From(new[] { Run(10), Run(12), Run(12), Run(20), Run(null) }, moveCap: 150);

        Assert.Equal(0.0, report.WinRateAt(9));
        Assert.Equal(0.2, report.WinRateAt(10));
        Assert.Equal(0.6, report.WinRateAt(12));
        Assert.Equal(0.8, report.CompletionRate);
        Assert.Equal(12, report.MoveLimitFor(0.6));
        Assert.Equal(20, report.MoveLimitFor(0.7));
        Assert.Null(report.MoveLimitFor(0.9));
    }

    [Fact]
    public void Extra_moves_win_rate_is_read_five_moves_past_the_limit_and_only_within_the_cap()
    {
        LevelReport report = LevelReport.From(new[] { Run(10), Run(12), Run(12), Run(20), Run(null) }, moveCap: 18);

        Assert.Equal(0.6, report.WinRateWithExtraMoves(7));
        Assert.Equal(0.6, report.WinRateWithExtraMoves(13));
        Assert.Null(report.WinRateWithExtraMoves(14)); // 19 moves is past the cap of 18: the run at 20 was never seen to finish
    }

    [Fact]
    public void Near_miss_counts_losses_that_got_close()
    {
        var almost = new RunResult(1, GameStatus.Won, 6, 6, new[] { 0f, 0.5f, 0.85f, 0.9f, 0.9f, 0.95f, 1f }, PlayStats.None);
        var far = new RunResult(2, GameStatus.Won, 6, 6, new[] { 0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 1f }, PlayStats.None);
        LevelReport report = LevelReport.From(new[] { almost, far }, moveCap: 150);

        Assert.Equal(0.5, report.NearMissRateAt(3));
        Assert.Equal(0.0, report.NearMissRateAt(6));
    }

    [Fact]
    public void Greedy_bot_needs_far_fewer_moves_than_random()
    {
        LevelDefinition level = GameTests.ObstacleLevel();

        LevelReport greedy = Simulate(level, new GreedyPolicy(SkillProfile.Average));
        LevelReport random = Simulate(level, new RandomPolicy());

        Assert.Equal(1.0, greedy.CompletionRate);
        Assert.True(greedy.MovesNeeded(0.5) * 2 < (random.MovesNeeded(0.5) ?? int.MaxValue),
            $"greedy median {greedy.MovesNeeded(0.5)}, random median {random.MovesNeeded(0.5)}");
    }

    [Fact]
    public void Simulation_is_reproducible_from_its_seed()
    {
        LevelDefinition level = GameTests.ObstacleLevel();

        RunResult first = SimulationRunner.Play(level, new GreedyPolicy(SkillProfile.Casual), seed: 5, moveCap: 100);
        RunResult second = SimulationRunner.Play(level, new GreedyPolicy(SkillProfile.Casual), seed: 5, moveCap: 100);

        Assert.Equal(first.CompletedAtMove, second.CompletedAtMove);
        Assert.Equal(first.ProgressAfterMove, second.ProgressAfterMove);
    }

    private static LevelReport Simulate(LevelDefinition level, IBotPolicy policy) =>
        LevelReport.From(
            Enumerable.Range(0, 20).Select(i => SimulationRunner.Play(level, policy, (ulong)i, moveCap: 150)).ToArray(),
            moveCap: 150);

    private static RunResult Run(int? completedAt)
    {
        int moves = completedAt ?? 150;
        float[] progress = Enumerable.Range(0, moves + 1).Select(i => completedAt is null ? 0.5f : i / (float)moves).ToArray();
        return new RunResult(0, completedAt is null ? GameStatus.LostOutOfMoves : GameStatus.Won, moves, completedAt, progress, PlayStats.None);
    }
}
