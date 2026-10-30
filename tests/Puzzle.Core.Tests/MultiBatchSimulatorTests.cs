using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Tooling.Simulation;

namespace Puzzle.Core.Tests;

/// <summary><see cref="MultiBatchSimulator"/>: many batches of bot games as one queue, behind <c>botstats</c> and <c>ladder</c>.</summary>
public class MultiBatchSimulatorTests
{
    private static readonly LevelDefinition Open = GameTests.OpenLevel();
    private static readonly LevelDefinition Obstacles = GameTests.ObstacleLevel();

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(5)]
    public void Batches_played_as_one_queue_give_each_batchs_own_report_in_batch_order_whatever_the_workers(int? workers)
    {
        SimulationBatch[] batches =
        [
            new(Obstacles, () => new GreedyPolicy(SkillProfile.Expert), Seed: 5),
            new(Open, () => new RandomPolicy(), Seed: 6),
            new(Obstacles, () => new GreedyPolicy(SkillProfile.Novice), Seed: 5),
        ];

        LevelReport[] queued = MultiBatchSimulator.Run(batches, runs: 12, moveCap: 80, workers).ToArray();

        Assert.Equal(batches.Length, queued.Length);
        foreach ((SimulationBatch batch, LevelReport report) in batches.Zip(queued))
        {
            LevelReport alone = BatchSimulator.Run(batch.Level, batch.CreatePolicy, runs: 12, moveCap: 80, batch.Seed);

            Assert.Equal((12, 80), (report.Runs, report.MoveCap));
            Assert.Equal(alone.CompletionMoves, report.CompletionMoves);
            Assert.Equal(alone.AverageCascadesPerMove, report.AverageCascadesPerMove);
            Assert.Equal(alone.NearMissRateAt(batch.Level.MoveLimit), report.NearMissRateAt(batch.Level.MoveLimit));
        }
    }

    [Fact]
    public void A_game_that_fails_stops_the_reports_at_its_batch_after_the_batches_before_it_got_theirs()
    {
        var noBot = new InvalidOperationException("This batch has no bot.");
        SimulationBatch playable = new(Obstacles, () => new GreedyPolicy(SkillProfile.Expert), Seed: 5);
        SimulationBatch[] batches = [playable, new(Obstacles, () => throw noBot, Seed: 5), playable];

        using IEnumerator<LevelReport> reports = MultiBatchSimulator.Run(batches, runs: 4, moveCap: 60, workers: 2).GetEnumerator();

        Assert.True(reports.MoveNext());
        Assert.Equal(4, reports.Current.Runs);
        Assert.Same(noBot, Assert.Throws<InvalidOperationException>(() => reports.MoveNext()));
    }

    [Fact]
    public void Fewer_than_one_run_or_one_worker_is_refused_before_any_game()
    {
        SimulationBatch[] batches = [new(Open, () => throw new InvalidOperationException("No game should be played."), Seed: 1)];

        Assert.Throws<ArgumentOutOfRangeException>(() => MultiBatchSimulator.Run(batches, runs: 0, moveCap: 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => MultiBatchSimulator.Run(batches, runs: 4, moveCap: 60, workers: 0));
    }
}
