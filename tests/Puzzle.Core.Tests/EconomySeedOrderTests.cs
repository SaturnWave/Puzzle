using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>A simulated player meets a level's pool seeds in the order a real player does: attempt k plays seed k modulo the pool size.</summary>
public class EconomySeedOrderTests
{
    [Fact]
    public void Attempt_k_meets_the_seed_the_engine_gives_attempt_k()
    {
        uint[] pool = [11, 22, 33];
        LevelDefinition level = BankSupport.FixedLevel(pool: pool);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            SeedPosition position = new AttemptNumber(attempt).PositionIn(pool.Length);
            Assert.Equal(level.Attempts.SeedsFor(attempt, unpooledSeed: 0).Play, pool[position.Value]);
        }

        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()), Always(Unfinished()), Always(Won(4)));
        AttemptPlayed[] attempts = Journey(OnePlayer(), levels).Attempts.ToArray();
        Assert.Equal([0, 1, 2], attempts.Select(attempt => attempt.Met.Seed.Value));
        Assert.Equal([false, false, true], attempts.Select(attempt => attempt.IsWon));
    }

    [Fact]
    public void A_won_level_starts_the_next_at_its_first_seed()
    {
        HandMadeLevels levels = new HandMadeLevels()
            .Level(6, Always(Unfinished()), Always(Won(4)))
            .Level(6, Always(Won(4)), Always(Unfinished()));

        AttemptPlayed[] attempts = Journey(OnePlayer(), levels).Attempts.ToArray();

        Assert.Equal([(1, 0, 0), (1, 1, 1), (2, 0, 0)], attempts.Select(attempt => (attempt.Level.Value, attempt.Attempt.Value, attempt.Met.Seed.Value)));
    }

    [Fact]
    public void A_players_luck_picks_every_stored_game_of_a_seed_about_as_often()
    {
        MeasuredLine line = new HandMadeLevels().Level(6, Enumerable.Repeat(Won(4), 64).ToArray()).Build().Lines.Single();
        var met = new int[64];
        var metOnEvenGames = new int[64];
        var metOnOddGames = new int[64];

        foreach (SimulatedPlayer player in PopulationRun.PlayersOf(OnePlayer("""{ "players": { "count": 2000 } }""").Players))
        {
            for (int level = 1; level <= 50; level++)
            {
                met[GameMet.By(player.Seed, new LevelId(level), AttemptNumber.First, line, MeasuredHalf.All).GameOfSeed]++;
                metOnEvenGames[GameMet.By(player.Seed, new LevelId(level), AttemptNumber.First, line, MeasuredHalf.Even).GameOfSeed]++;
                metOnOddGames[GameMet.By(player.Seed, new LevelId(level), AttemptNumber.First, line, MeasuredHalf.Odd).GameOfSeed]++;
            }
        }

        // 100 000 picks among 64 games: 1562 each on average, and none far from it.
        Assert.All(met, count => Assert.InRange(count, 1350, 1800));
        Assert.All(Enumerable.Range(0, 64), game => Assert.InRange(metOnEvenGames[game], game % 2 == 0 ? 2800 : 0, game % 2 == 0 ? 3450 : 0));
        Assert.All(Enumerable.Range(0, 64), game => Assert.InRange(metOnOddGames[game], game % 2 == 1 ? 2800 : 0, game % 2 == 1 ? 3450 : 0));
    }

    [Fact]
    public void A_pool_of_eight_is_met_again_on_the_ninth_attempt()
    {
        HandMadeLevels levels = new HandMadeLevels().Level(6, Enumerable.Repeat(Always(Unfinished()), 8).ToArray());

        AttemptPlayed[] attempts = Journey(OnePlayer(), levels).Attempts.Take(10).ToArray();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 0, 1], attempts.Select(attempt => attempt.Met.Seed.Value));
        Assert.Equal(Enumerable.Range(0, 10), attempts.Select(attempt => attempt.Attempt.Value));
    }
}
