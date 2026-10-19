using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class HintAdvisorTests
{
    private const ulong AttemptSeed = 20261019;

    [Theory]
    [InlineData("random")]
    [InlineData("novice")]
    [InlineData("casual")]
    [InlineData("average")]
    [InlineData("expert")]
    [InlineData("skill-55")]
    [InlineData("SKILL-30")]
    public void Every_player_model_of_the_bot_family_can_give_hints(string botName)
    {
        Assert.True(HintAdvisor.TryCreate(botName, out HintAdvisor? advisor));
        Assert.Equal(botName, advisor.BotName);
    }

    [Theory]
    [InlineData("usta")]
    [InlineData("usta-48")]
    [InlineData("skill-52")]
    [InlineData("helper")]
    [InlineData("")]
    [InlineData(null)]
    public void A_measurement_bot_or_a_name_of_no_bot_gives_no_hints(string? botName)
    {
        Assert.False(HintAdvisor.TryCreate(botName, out HintAdvisor? advisor));
        Assert.Null(advisor);
    }

    [Theory]
    [InlineData("random")]
    [InlineData("novice")]
    [InlineData("casual")]
    [InlineData("average")]
    public void A_hint_is_always_a_move_the_game_accepts_at_that_moment(string botName)
    {
        HintAdvisor advisor = AdvisorOf(botName);
        foreach (ulong seed in new ulong[] { 1, 2, 3 })
        {
            Game game = Game.Start(GameTests.ObstacleLevel(), seed);
            var player = new GreedyPolicy(SkillProfile.Novice);
            var rng = new Rng(seed);
            while (game.Status == GameStatus.Playing)
            {
                Move hint = advisor.MoveFor(game, AttemptSeed) ?? throw new Xunit.Sdk.XunitException($"No hint after {game.MovesUsed} moves of a game being played.");

                Assert.True(game.IsLegal(hint), $"{botName} hinted {hint} after {game.MovesUsed} moves, which the game refuses.");
                Assert.True(game.TryPlay(player.ChooseMove(game, game.FindLegalMoves(), rng)));
            }
        }
    }

    [Fact]
    public void Asking_for_a_hint_before_every_move_leaves_the_game_to_play_on_exactly_as_without_hints()
    {
        LevelDefinition level = GameTests.ObstacleLevel();
        var options = new GameOptions { RecordEvents = true };
        Game hinted = Game.Start(level, AttemptSeeds.From(9), options);
        Game plain = Game.Start(level, AttemptSeeds.From(9), options);
        HintAdvisor advisor = AdvisorOf("casual");
        var player = new GreedyPolicy(SkillProfile.Casual);
        var rng = new Rng(4);
        while (plain.Status == GameStatus.Playing)
        {
            advisor.MoveFor(hinted, AttemptSeed);
            advisor.MoveFor(hinted, AttemptSeed + 1);
            Move move = player.ChooseMove(plain, plain.FindLegalMoves(), rng);

            Assert.True(plain.TryPlay(move));
            Assert.True(hinted.TryPlay(move));
            Assert.Equal(plain.Board.ComputeHash(), hinted.Board.ComputeHash());
            Assert.Equal(plain.LastEvents.Select(change => change.ToString()), hinted.LastEvents.Select(change => change.ToString()));
        }

        Assert.Equal(plain.Status, hinted.Status);
        Assert.Equal(plain.MovesUsed, hinted.MovesUsed);
    }

    [Fact]
    public void The_same_attempt_at_the_same_point_gives_the_same_hint_however_often_it_is_asked()
    {
        Game game = Game.Start(GameTests.ObstacleLevel(), seed: 5);
        HintAdvisor first = AdvisorOf("novice");
        HintAdvisor second = AdvisorOf("novice");

        Move? hint = first.MoveFor(game, AttemptSeed);

        Assert.NotNull(hint);
        Assert.Equal(hint, first.MoveFor(game, AttemptSeed));
        Assert.Equal(hint, second.MoveFor(game, AttemptSeed));
    }

    [Fact]
    public void A_hint_shows_the_winning_move_to_a_bot_that_notices_it()
    {
        string[] tiles = Filler(6, 6).With(0, 0, 'p').With(1, 0, 'p').With(2, 1, 'p');
        LevelDefinition level = Level(tiles, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) });
        Game game = Start(level);

        Move? hint = AdvisorOf("expert").MoveFor(game, AttemptSeed);

        Assert.True(hint == Move.Swap(new Position(2, 1), new Position(2, 0)) || hint == Move.Swap(new Position(2, 0), new Position(2, 1)), $"hinted {hint}");
    }

    [Fact]
    public void A_game_that_is_over_has_no_hint()
    {
        Game game = Game.Start(GameTests.ObstacleLevel(), seed: 3, new GameOptions { MoveLimitOverride = 1 });
        Assert.True(game.TryPlay(game.FindLegalMoves()[0]));

        Assert.NotEqual(GameStatus.Playing, game.Status);
        Assert.Null(AdvisorOf("casual").MoveFor(game, AttemptSeed));
    }

    private static HintAdvisor AdvisorOf(string botName) =>
        HintAdvisor.TryCreate(botName, out HintAdvisor? advisor) ? advisor : throw new ArgumentException($"{botName} gives no hints.", nameof(botName));
}
