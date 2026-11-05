using Puzzle.Bank.Assembling;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestBoards;
using static Puzzle.Core.Tests.TestRecords;

namespace Puzzle.Core.Tests;

/// <summary>
/// The start boosters chosen on a level's card, taken into its attempt as the Unity game starts one
/// (<c>LevelStart</c>, the Unity project's source file, compiled into these tests), on the levels the game ships:
/// their specials lie on gems of the board the level deals without them and their moves are on the plaque; the
/// attempt's record names them, and its line as the phone writes it replays to the same end. A board with no free
/// gem for a special opens without it.
/// </summary>
public class StartBoosterAttemptTests
{
    private static readonly Lazy<LevelFolder> Live = new(() => new LevelFolder(RepoFiles.LiveLevelsFolder));

    private static readonly StartBooster[] AllThree = { StartBooster.RocketAndBomb, StartBooster.ColorBomb, StartBooster.ExtraMoves };

    public static TheoryData<int, int, StartBooster[]> ChosenOnTheCard() => new TheoryData<int, int, StartBooster[]>
    {
        { 1, 0, new[] { StartBooster.RocketAndBomb } },
        { 40, 2, new[] { StartBooster.ColorBomb } },
        { 47, 0, new[] { StartBooster.ExtraMoves } },
        { 47, 1, AllThree },
        { 120, 0, new[] { StartBooster.ColorBomb, StartBooster.RocketAndBomb } },
    };

    private static LevelDefinition LiveLevel(int number) =>
        Live.Value.Find(number)?.Level ?? throw new InvalidOperationException($"The shipped levels have no level {number}.");

    private static void PlayOut(Game game)
    {
        var bot = new GreedyPolicy(SkillProfile.Expert);
        var rng = new Rng(20261014);
        while (game.Status == GameStatus.Playing)
        {
            Assert.True(game.TryPlay(bot.ChooseMove(game, game.FindLegalMoves(), rng)));
        }
    }

    [Theory]
    [MemberData(nameof(ChosenOnTheCard))]
    public void An_attempt_with_start_boosters_chosen_on_the_card_is_recorded_with_them_and_its_phone_line_replays_to_the_same_end(
        int levelNumber, int attempt, StartBooster[] chosen)
    {
        LevelDefinition level = LiveLevel(levelNumber);
        AttemptSeeds seeds = LevelStart.SeedsOf(level, attempt);
        Game game = LevelStart.Attempt(level, attempt, chosen);
        PlayOut(game);
        var start = new AttemptStart(level.Id, level.ContentVersion, level.MoveLimit, seeds, attempt, Phone, "unity", Noon);
        var record = new AttemptRecord(start, AttemptOutcome.Of(game, Noon.AddMinutes(4)), lab: null, game.Actions);

        AttemptRecord read = AttemptRecordJson.Parse(AttemptLine.Of(record));

        string[] named = chosen.OrderBy(booster => booster).Select(BoosterNames.Of).ToArray();
        Assert.Equal(named, read.Outcome.BoostersUsed);
        Assert.Equal(level.MoveLimit, read.Start.MoveLimit); // the record keeps the level's moves; the booster's are named, not added
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(read, level));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(47)]
    [InlineData(120)]
    public void The_board_opens_with_the_specials_of_the_start_boosters_on_gems_of_the_board_the_level_deals_without_them(int levelNumber)
    {
        LevelDefinition level = LiveLevel(levelNumber);

        Game without = LevelStart.Attempt(level, attempt: 0);
        Game with = LevelStart.Attempt(level, attempt: 0, AllThree);

        BoardShape shape = without.Board.Shape;
        int[] changed = Enumerable.Range(0, shape.CellCount).Where(cell => shape.IsPlayable(cell) && without.Board.Get(cell) != with.Board.Get(cell)).ToArray();
        TileKind[] specials = changed.Select(cell => with.Board.Get(cell).Kind).OrderBy(kind => kind).ToArray();
        Assert.Equal(3, changed.Length);
        Assert.All(changed, cell => Assert.True(without.Board.Get(cell).IsGem));
        Assert.Single(specials, kind => kind == TileKind.RocketH || kind == TileKind.RocketV);
        Assert.Single(specials, kind => kind == TileKind.Bomb);
        Assert.Single(specials, kind => kind == TileKind.ColorBomb);
        Assert.Equal(without.MovesLeft + RuleSet.Default.StartBoosterExtraMoves, with.MovesLeft);

        // The board's entrance finds them in the start's events.
        Assert.Equal(
            changed.Select(shape.PositionOf).OrderBy(at => at.Y).ThenBy(at => at.X),
            with.LastEvents.OfType<GameEvent.SpecialCreated>().Select(created => created.At).OrderBy(at => at.Y).ThenBy(at => at.X));
        Assert.Empty(without.LastEvents);
    }

    [Fact]
    public void A_board_with_too_few_free_gems_for_the_specials_opens_without_the_boosters_that_need_one_and_keeps_the_extra_moves()
    {
        // Every gem is chained but the two at the right of the top row, whose swap makes the move: room for the brush and
        // the splat, and none left for the palette.
        string[] tiles = { "rrbr", "gbyg", "ybgy" };
        string[] chains = { "11..", "1111", "1111" };
        LevelDefinition level = Level(tiles, chains: chains, goals: new GoalSpec[] { new GoalSpec.BreakChains(10) }, moves: 12);

        Game game = LevelStart.Attempt(level, attempt: 0, AllThree);

        Assert.Equal(new[] { BoosterNames.Of(StartBooster.ExtraMoves) }, game.BoostersUsed);
        Assert.Equal(12 + RuleSet.Default.StartBoosterExtraMoves, game.MovesLeft);
        Assert.Throws<LevelInitializationException>(() => Game.Start(level, LevelStart.SeedsOf(level, 0), new GameOptions { StartBoosters = AllThree }));
    }
}
