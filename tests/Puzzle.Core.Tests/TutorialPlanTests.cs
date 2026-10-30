using System.IO;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
using Puzzle.Museum;
using Puzzle.Tooling;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the tutorial's steps may come (<c>TutorialPlan</c>, the Unity project's source file, compiled into these tests):
/// the first level's three steps, the four and the five on their own levels and a few after, an obstacle's step on
/// any board until it is seen, and what a save of a layout before the tutorial has seen of it.
/// </summary>
public class TutorialPlanTests
{
    private static readonly TutorialStep[] Obstacles = { TutorialStep.Box, TutorialStep.Jelly, TutorialStep.Chain, TutorialStep.Item };

    /// <summary>The tests' pack: the spatula opens on level 7, the brushes on 12 and 16, the shuffle on 20.</summary>
    private static readonly BoosterUnlocks Unlocks = MetaSupport.Rules().Unlocks;

    [Fact]
    public void A_new_player_meets_the_goal_the_match_and_the_moves_on_the_first_level_in_that_order_before_any_obstacle()
    {
        IReadOnlyList<TutorialStep> onTheFirstLevel = TutorialPlan.MayComeOn(1, TutorialSeen.None, Unlocks);

        Assert.Equal(
            new[] { TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves, TutorialStep.Box, TutorialStep.Jelly, TutorialStep.Chain, TutorialStep.Item },
            onTheFirstLevel);
    }

    [Fact]
    public void The_four_and_the_five_wait_for_their_move_on_their_own_level_and_three_levels_after_it_and_are_let_go_after_that()
    {
        bool MayComeOn(int level, TutorialStep step) => TutorialPlan.MayComeOn(level, TutorialSeen.None, Unlocks).Contains(step);

        Assert.Equal(new[] { 2, 3, 4, 5 }, Enumerable.Range(1, 12).Where(level => MayComeOn(level, TutorialStep.FourInALine)));
        Assert.Equal(new[] { 3, 4, 5, 6 }, Enumerable.Range(1, 12).Where(level => MayComeOn(level, TutorialStep.FiveInALine)));
        Assert.Equal(new[] { 1, 2, 3, 4 }, Enumerable.Range(1, 12).Where(level => MayComeOn(level, TutorialStep.Match)));
        Assert.Equal(TutorialPlan.LevelsAfterItsOwn, 3);
    }

    [Fact]
    public void An_obstacles_step_may_come_on_any_level_until_it_is_seen_and_no_step_comes_twice()
    {
        TutorialSeen seen = TutorialSeen.None.With(TutorialStep.Box).With(TutorialStep.FourInALine);

        Assert.All(new[] { 1, 11, 21, 400 }, level => Assert.Contains(TutorialStep.Item, TutorialPlan.MayComeOn(level, TutorialSeen.None, Unlocks)));
        Assert.All(Enumerable.Range(1, 60), level =>
        {
            Assert.DoesNotContain(TutorialStep.Box, TutorialPlan.MayComeOn(level, seen, Unlocks));
            Assert.DoesNotContain(TutorialStep.FourInALine, TutorialPlan.MayComeOn(level, seen, Unlocks));
        });
        Assert.Empty(TutorialPlan.MayComeOn(1, TutorialSeen.All, Unlocks));
    }

    [Fact]
    public void The_steps_of_the_jigsaw_never_come_on_a_board()
    {
        Assert.All(Enumerable.Range(1, 60), level =>
        {
            Assert.DoesNotContain(TutorialStep.FirstPiece, TutorialPlan.MayComeOn(level, TutorialSeen.None, Unlocks));
            Assert.DoesNotContain(TutorialStep.FirstPainting, TutorialPlan.MayComeOn(level, TutorialSeen.None, Unlocks));
        });
    }

    [Fact]
    public void A_save_from_before_the_tutorial_has_seen_the_steps_of_the_levels_it_has_won_and_nothing_else()
    {
        Assert.Equal(TutorialSeen.None, TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 0, PlacedPieces.None)));
        Assert.Equal(
            TutorialSeen.Of(new[] { TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves }),
            TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 1, PlacedPieces.None)));
        Assert.Equal(
            TutorialSeen.Of(new[] { TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves, TutorialStep.FourInALine, TutorialStep.FiveInALine }),
            TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 10, PlacedPieces.None)));

        // Standing on level 11 the crates are still ahead; won, they are behind.
        Assert.False(TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 10, PlacedPieces.None)).Has(TutorialStep.Box));
        Assert.True(TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 11, PlacedPieces.None)).Has(TutorialStep.Box));
        Assert.Equal(
            TutorialSeen.Of(TutorialSteps.All.Where(step => step.IsOnTheBoard() && step.BoosterOf() == null && step != TutorialStep.LongPress)),
            TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 51, PlacedPieces.None)));
    }

    [Fact]
    public void A_save_from_before_the_tutorial_has_seen_the_first_piece_once_a_piece_is_in_place_and_the_first_painting_once_one_is_whole()
    {
        var first = new PaintingNumber(1);
        MuseumProgress oneInPlace = new MuseumProgress(levelsWon: 12, PlacedPieces.None).WithPiecePlaced(first, 3);
        MuseumProgress oneWhole = new MuseumProgress(levelsWon: 12, PlacedPieces.None).WithTrayPlaced(first);

        Assert.False(TutorialPlan.OfAnOlderSave(new MuseumProgress(levelsWon: 12, PlacedPieces.None)).Has(TutorialStep.FirstPiece));
        Assert.True(TutorialPlan.OfAnOlderSave(oneInPlace).Has(TutorialStep.FirstPiece));
        Assert.False(TutorialPlan.OfAnOlderSave(oneInPlace).Has(TutorialStep.FirstPainting));
        Assert.True(TutorialPlan.OfAnOlderSave(oneWhole).Has(TutorialStep.FirstPainting));
    }

    /// <summary>The plan's first levels of the obstacles are only read for an older save, but must be the levels' own: one regenerated level would move them.</summary>
    [Fact]
    public void The_first_level_with_each_obstacle_is_where_the_level_files_first_have_it()
    {
        LevelDefinition[] levels = Enumerable.Range(1, 60)
            .Select(number => LevelJson.Load(Path.Combine(RepoFiles.LiveLevelsFolder, $"level_{number:0000}.json")))
            .ToArray();

        foreach (TutorialStep obstacle in Obstacles)
        {
            int first = levels.First(level => Has(level, obstacle)).Id.Value;
            Assert.True(TutorialPlan.FirstLevelWith[obstacle] == first, $"the plan has {obstacle} first on level {TutorialPlan.FirstLevelWith[obstacle]}, the level files on {first}");
        }
    }

    [Fact]
    public void Every_step_has_a_name_of_its_own_that_reads_back_as_the_step()
    {
        string[] names = TutorialSteps.All.Select(TutorialSteps.NameOf).ToArray();

        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.All(TutorialSteps.All, step => Assert.Equal(step, TutorialSteps.Find(step.NameOf())));
        Assert.Null(TutorialSteps.Find("booster-intro"));
        Assert.Equal("goal, four-in-a-line, first-painting", TutorialSeen.Of(new[] { TutorialStep.FirstPainting, TutorialStep.Goal, TutorialStep.FourInALine }).ToString());
    }

    private static bool Has(LevelDefinition level, TutorialStep obstacle)
    {
        Game game = Game.Start(level, LevelStart.SeedsOf(level, attempt: 0));
        BoardShape shape = game.Board.Shape;
        IEnumerable<Position> cells = Enumerable.Range(0, shape.Height).SelectMany(y => Enumerable.Range(0, shape.Width).Select(x => new Position(x, y)));
        return obstacle switch
        {
            TutorialStep.Box => cells.Any(cell => game.Board.TileAt(cell).Kind == TileKind.Box),
            TutorialStep.Jelly => cells.Any(cell => game.Board.JellyAt(cell) > 0),
            TutorialStep.Chain => cells.Any(cell => game.Board.ChainAt(cell) > 0),
            TutorialStep.Item => cells.Any(cell => game.Board.TileAt(cell).Kind == TileKind.Item) || level.Goals.Any(goal => goal is GoalSpec.DropItems),
            _ => throw new ArgumentOutOfRangeException(nameof(obstacle), obstacle, "not an obstacle"),
        };
    }
}
