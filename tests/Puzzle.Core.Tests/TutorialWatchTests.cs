using Puzzle.Bot;
using Puzzle.Client.Flow;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The tutorial on one board (<c>TutorialWatch</c>, the Unity project's source file, compiled into these tests): which
/// step shows when, what the board takes while it shows, and how it ends. The boards are the tests' filler with a
/// setup in purple, as in <see cref="MoveLessonTests"/>.
/// </summary>
public class TutorialWatchTests
{
    private const ulong AttemptSeed = 20261019;

    private static readonly string[] ThreeInARow = Filler(6, 6).With(0, 0, 'p').With(1, 0, 'p').With(2, 1, 'p');
    private static readonly string[] FourInARow = ThreeInARow.With(3, 0, 'p');
    private static readonly Move TradeUp = Move.Swap(P(2, 1), P(2, 0));

    [Fact]
    public void On_a_new_players_first_board_the_goal_shows_first_the_match_is_shown_by_hand_and_the_moves_come_after_the_first_move()
    {
        Game game = Start(Level(ThreeInARow));
        TutorialWatch watch = WatchOf(TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves);

        var goal = Assert.IsType<Lesson.Look>(watch.BeginWhatIsDue(game));
        Assert.Equal((TutorialStep.Goal, LessonSpot.GoalPlaque), (goal.Step, goal.Spot));
        Assert.False(watch.Takes(TradeUp));
        Assert.Equal(TutorialStep.Goal, watch.Touched());

        var match = Assert.IsType<Lesson.Guided>(watch.BeginWhatIsDue(game));
        Assert.Equal((TutorialStep.Match, MoveLessonTests.PiecesOf(TradeUp)), (match.Step, MoveLessonTests.PiecesOf(match.Move)));
        Assert.True(game.TryPlay(TradeUp));
        Assert.Equal(TutorialStep.Match, watch.Played(TradeUp));

        var moves = Assert.IsType<Lesson.Look>(watch.BeginWhatIsDue(game));
        Assert.Equal((TutorialStep.Moves, LessonSpot.MovesMedallion), (moves.Step, moves.Spot));
        Assert.Equal(TutorialStep.Moves, watch.Touched());

        Assert.Null(watch.BeginWhatIsDue(game));
        Assert.Equal(new[] { TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves }, watch.TakeWhatWasDone());
        Assert.Empty(watch.TakeWhatWasDone());
        Assert.Equal(1, watch.GuidedMoves);
    }

    [Fact]
    public void While_a_move_is_shown_by_hand_the_board_takes_that_trade_from_either_piece_and_no_other_move_and_a_touch_does_not_end_it()
    {
        Game game = Start(Level(ThreeInARow.With(5, 5, 'H')));
        TutorialWatch watch = WatchOf(TutorialStep.Match);
        Move fromTheOtherPiece = Move.Swap(P(2, 0), P(2, 1));

        Assert.IsType<Lesson.Guided>(watch.BeginWhatIsDue(game));

        Assert.True(watch.Takes(TradeUp));
        Assert.True(watch.Takes(fromTheOtherPiece));
        Assert.False(watch.Takes(Move.Tap(P(5, 5))));
        Assert.False(watch.Takes(Move.Swap(P(1, 0), P(2, 0))));
        Assert.Null(watch.Touched());
        Assert.Null(watch.Played(Move.Tap(P(5, 5))));
        Assert.Equal(0, watch.GuidedMoves);
        Assert.IsType<Lesson.Guided>(watch.Showing);

        Assert.True(game.TryPlay(fromTheOtherPiece));
        Assert.Equal(TutorialStep.Match, watch.Played(fromTheOtherPiece));
        Assert.Equal(1, watch.GuidedMoves);
    }

    [Fact]
    public void A_step_shown_by_hand_says_nothing_while_the_board_offers_no_such_move_and_the_board_takes_every_move_meanwhile()
    {
        Game three = Start(Level(ThreeInARow));
        TutorialWatch waiting = WatchOf(TutorialStep.FourInALine);

        Assert.Null(waiting.BeginWhatIsDue(three));
        Assert.Null(waiting.Showing);
        Assert.True(waiting.Takes(TradeUp));
        Assert.Equal(new[] { TutorialStep.FourInALine }, waiting.MayStillCome);

        Game four = Start(Level(FourInARow));
        TutorialWatch offered = WatchOf(TutorialStep.FourInALine);
        Assert.Equal(MoveLessonTests.PiecesOf(TradeUp), MoveLessonTests.PiecesOf(Assert.IsType<Lesson.Guided>(offered.BeginWhatIsDue(four)).Move));
    }

    [Fact]
    public void The_goal_is_let_go_once_the_attempt_has_begun()
    {
        Game game = Start(Level(ThreeInARow));
        TutorialWatch watch = WatchOf(TutorialStep.Goal);

        Assert.True(game.TryPlay(TradeUp));

        Assert.Null(watch.BeginWhatIsDue(game));
    }

    [Fact]
    public void Steps_come_in_the_plans_order_and_one_the_board_does_not_offer_lets_the_next_one_come()
    {
        // A crate in the corner and no four anywhere: the four waits, the crate is pointed at.
        Game game = Start(Level(ThreeInARow.With(5, 5, '1')));
        TutorialWatch watch = WatchOf(TutorialStep.FourInALine, TutorialStep.Box);

        var crate = Assert.IsType<Lesson.Look>(watch.BeginWhatIsDue(game));

        Assert.Equal(TutorialStep.Box, crate.Step);
        Assert.Equal(P(5, 5), Assert.IsType<LessonSpot.Cell>(crate.Spot).At);
        Assert.Equal(TutorialStep.Box, watch.Touched());
        Assert.Equal(new[] { TutorialStep.FourInALine }, watch.MayStillCome);
    }

    [Fact]
    public void An_obstacle_is_pointed_at_where_one_lies_nearest_the_middle_of_the_board()
    {
        // A match to make along the foot of the board, so that the board has a move.
        string[] tiles = Filler(7, 7).With(0, 6, 'p').With(1, 6, 'p').With(2, 5, 'p').With(0, 0, '1').With(6, 6, '2').With(4, 3, '1');
        string[] jelly = Blank(7, 7).With(1, 1, '1').With(3, 2, '2');
        string[] chains = Blank(7, 7).With(5, 1, '1').With(3, 4, '1').With(2, 3, '1');
        Game game = Start(Level(tiles, jelly: jelly, chains: chains));

        Assert.Equal(P(4, 3), SpotOf(game, TutorialStep.Box));
        Assert.Equal(P(3, 2), SpotOf(game, TutorialStep.Jelly));
        Assert.Equal(P(2, 3), SpotOf(game, TutorialStep.Chain)); // as near as (3,4): the first in reading order
        Assert.Null(WatchOf(TutorialStep.Item).BeginWhatIsDue(game));
    }

    [Fact]
    public void A_step_cut_short_is_not_done_and_a_board_never_shows_a_step_of_the_jigsaw()
    {
        Game game = Start(Level(ThreeInARow));
        TutorialWatch watch = WatchOf(TutorialStep.Goal);

        Assert.NotNull(watch.BeginWhatIsDue(game));

        Assert.Empty(watch.TakeWhatWasDone());
        Assert.Throws<ArgumentException>(() => WatchOf(TutorialStep.FirstPiece));
    }

    [Fact]
    public void Every_step_says_what_to_do_and_why_and_a_step_that_is_looked_at_says_that_a_touch_goes_on()
    {
        TutorialStep[] steps = TutorialSteps.All.Where(step => step.BoosterOf() == null && step != TutorialStep.StartBoosters && step != TutorialStep.LongPress).ToArray();
        Assert.All(steps, step =>
        {
            PillWords.Lesson looked = TutorialWords.Of(step, asksForATouch: true);
            PillWords.Lesson guided = TutorialWords.Of(step, asksForATouch: false);

            Assert.False(string.IsNullOrWhiteSpace(looked.Says) || string.IsNullOrWhiteSpace(looked.Why), $"{step} says nothing");
            Assert.NotEqual(looked.Says, looked.Why);
            Assert.False(string.IsNullOrWhiteSpace(looked.Aside));
            Assert.Null(guided.Aside);
        });
        Assert.Equal(steps.Length, steps.Select(step => TutorialWords.Of(step, asksForATouch: false).Says).Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => TutorialWords.Of(TutorialStep.BoosterHitCell, asksForATouch: false));
    }

    [Fact]
    public void A_trade_shown_by_hand_lights_the_line_it_makes_and_a_tap_or_a_trade_that_makes_none_lights_no_line()
    {
        Board four = Start(Level(FourInARow)).Board;
        Board crossing = Start(Level(FourInARow.With(2, 2, 'o').With(2, 3, 'p').With(2, 4, 'p'))).Board;

        // The purple coming up lines up four across; the filler going down lines up nothing.
        Assert.Equal(new[] { (P(0, 0), P(3, 0)) }, TradeLine.RunsOf(four, TradeUp));
        Assert.Equal(new[] { (P(0, 0), P(3, 0)) }, TradeLine.RunsOf(four, Move.Swap(P(2, 0), P(2, 1))));
        Assert.Empty(TradeLine.RunsOf(four, Move.Tap(P(0, 0))));
        Assert.Empty(TradeLine.RunsOf(four, Move.Swap(P(4, 4), P(5, 4))));

        // An orange between the purple at (2,1) and two under it: traded, the purple comes down into three down column 2, the orange makes nothing.
        Assert.Equal(new[] { (P(2, 2), P(2, 4)) }, TradeLine.RunsOf(crossing, Move.Swap(P(2, 2), P(2, 1))));
    }

    private static Position SpotOf(Game game, TutorialStep step) =>
        Assert.IsType<LessonSpot.Cell>(Assert.IsType<Lesson.Look>(WatchOf(step).BeginWhatIsDue(game)).Spot).At;

    private static TutorialWatch WatchOf(params TutorialStep[] steps) =>
        new(steps, HintAdvisor.TryCreate("expert", out HintAdvisor? advisor) ? advisor : throw new InvalidOperationException("no expert"), AttemptSeed);
}
