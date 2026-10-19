using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Tooling.Hints;

namespace Puzzle.Core.Tests;

public class HintEffectTests
{
    [Fact]
    public void A_hinted_player_follows_no_more_hints_than_an_attempt_shows()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        Assert.True(HintAdvisor.TryCreate("casual", out HintAdvisor? advisor));
        var player = new HintedPlayer(new GreedyPolicy(SkillProfile.Novice), advisor, new HintMoment.WhenIdle(100), hintsPerAttempt: 3);

        RunResult run = SimulationRunner.Play(level, player, seed: 3, moveCap: 40);

        Assert.True(run.MovesUsed > 3);
        Assert.Equal(3, player.HintsFollowed);
    }

    [Fact]
    public void A_hinted_player_plays_the_hinted_move_when_a_hint_is_shown_and_its_own_move_otherwise()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        Game game = Game.Start(level, seed: 8);
        Assert.True(HintAdvisor.TryCreate("expert", out HintAdvisor? advisor));
        var plain = new GreedyPolicy(SkillProfile.Novice);
        var hinted = new HintedPlayer(new GreedyPolicy(SkillProfile.Novice), advisor, new HintMoment.WhenIdle(100), hintsPerAttempt: 1);

        Move own = plain.ChooseMove(game, game.FindLegalMoves(), new Puzzle.Core.Randomness.Rng(5));
        Move first = hinted.ChooseMove(game, game.FindLegalMoves(), new Puzzle.Core.Randomness.Rng(5));
        Move second = hinted.ChooseMove(game, game.FindLegalMoves(), new Puzzle.Core.Randomness.Rng(5));

        Assert.True(game.IsLegal(first));
        Assert.Equal(1, hinted.HintsFollowed);
        Assert.Equal(own, second); // the cap is reached: the player's own choice, drawn as in the plain game
    }

    [Fact]
    public void The_change_hints_make_is_read_from_the_attempts_paired_with_their_plain_games()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        HintedAttempt[] attempts =
        [
            new(level, 0, WonPlain: false, [(true, 3)]),
            new(level, 1, WonPlain: true, [(true, 2)]),
            new(level, 2, WonPlain: true, [(false, 3)]),
            new(level, 3, WonPlain: false, [(true, 1)]),
        ];

        HintEffectSummary effect = Assert.Single(HintEffect.Summarize(attempts, ["casual"]));

        Assert.Equal((0.5, 0.75, 0.25), (effect.PlainWinRate, effect.HintedWinRate, effect.Change));
        Assert.Equal(Math.Sqrt(2.75 / 3 / 4), effect.StandardError, precision: 12); // differences 1, 0, -1, 1 about their mean 0.25: squares 2.75 over 3
        Assert.Equal((2.25, 1.0), (effect.MeanHints, effect.ShareWithAHint));
    }
}
