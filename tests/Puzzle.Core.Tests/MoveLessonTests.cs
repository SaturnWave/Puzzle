using System.IO;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Tooling;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the tutorial finds the move it shows by hand (docs/GAME_DESIGN.md §19): what a move teaches is what the engine
/// makes of it on a lookahead copy (Puzzle.Bot's MoveLessons), and the move shown is the hint's advisor's among the
/// moves that teach the lesson (HintAdvisor.MoveFor with a lesson). The boards are the tests' filler, which never
/// matches by itself and offers no move of its own, with a setup drawn in purple and orange.
/// </summary>
public class MoveLessonTests
{
    private const ulong AttemptSeed = 20261019;
    private const ulong AnySeed = 7;

    /// <summary>Purple at (0,0) and (1,0), and at (2,1) under the gap: the trade of (2,1) up into (2,0) lines up three.</summary>
    private static readonly string[] ThreeInARow = Filler(6, 6).With(0, 0, 'p').With(1, 0, 'p').With(2, 1, 'p');

    /// <summary>As <see cref="ThreeInARow"/>, with purple at (3,0) too: the same trade lines up four.</summary>
    private static readonly string[] FourInARow = ThreeInARow.With(3, 0, 'p');

    /// <summary>As <see cref="FourInARow"/>, with purple at (4,0) too: the same trade lines up five.</summary>
    private static readonly string[] FiveInARow = FourInARow.With(4, 0, 'p');

    private static readonly Move TradeUp = Move.Swap(P(2, 1), P(2, 0));

    [Fact]
    public void A_trade_into_three_in_a_row_teaches_the_match_and_neither_the_four_nor_the_five()
    {
        Game game = Start(Level(ThreeInARow));

        Assert.True(MoveLessons.Teaches(game, TradeUp, MoveLesson.Match, AnySeed));
        Assert.False(MoveLessons.Teaches(game, TradeUp, MoveLesson.FourInALine, AnySeed));
        Assert.False(MoveLessons.Teaches(game, TradeUp, MoveLesson.FiveInALine, AnySeed));
    }

    [Fact]
    public void A_trade_into_four_in_a_row_teaches_the_four_and_one_into_five_the_five()
    {
        Game four = Start(Level(FourInARow));
        Game five = Start(Level(FiveInARow));

        Assert.Equal(new[] { false, true, false }, Lessons.Select(lesson => MoveLessons.Teaches(four, TradeUp, lesson, AnySeed)));
        Assert.Equal(new[] { false, false, true }, Lessons.Select(lesson => MoveLessons.Teaches(five, TradeUp, lesson, AnySeed)));

        // What the copy counted is what the game makes of the move.
        Assert.True(four.TryPlay(TradeUp));
        Assert.Contains(four.FirstRound(), e => e is Core.Engine.Events.GameEvent.SpecialCreated { Kind: TileKind.RocketH or TileKind.RocketV });
        Assert.True(five.TryPlay(TradeUp));
        Assert.Contains(five.FirstRound(), e => e is Core.Engine.Events.GameEvent.SpecialCreated { Kind: TileKind.ColorBomb });
    }

    [Fact]
    public void An_L_of_five_makes_a_splash_and_teaches_none_of_the_three_lessons()
    {
        // Purple along the top and down column 2: the trade of (3,0) left into (2,0) makes the corner of an L.
        Game game = Start(Level(Filler(6, 6).With(0, 0, 'p').With(1, 0, 'p').With(3, 0, 'p').With(2, 1, 'p').With(2, 2, 'p')));
        Move intoTheCorner = Move.Swap(P(3, 0), P(2, 0));

        Assert.All(Lessons, lesson => Assert.False(MoveLessons.Teaches(game, intoTheCorner, lesson, AnySeed)));
        Assert.True(game.TryPlay(intoTheCorner));
        Assert.Contains(game.FirstRound(), e => e is Core.Engine.Events.GameEvent.SpecialCreated { Kind: TileKind.Bomb });
    }

    [Fact]
    public void A_match_that_pieces_falling_after_it_follow_with_a_second_one_teaches_nothing()
    {
        // Three in row 3; when they go, the orange at (2,2) falls beside the two at (3,3) and (4,3) into a second match.
        string[] tiles = Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 4, 'p').With(2, 2, 'o').With(3, 3, 'o').With(4, 3, 'o');
        Game game = Start(Level(tiles));
        Move trade = Move.Swap(P(2, 4), P(2, 3));

        Assert.All(Lessons, lesson => Assert.False(MoveLessons.Teaches(game, trade, lesson, AnySeed)));
        Assert.True(game.TryPlay(trade));
        Assert.True(game.LastMove.Cascades > 0, "the setup made no second match");
    }

    [Fact]
    public void A_tap_on_a_special_teaches_nothing_and_a_move_the_game_refuses_is_refused()
    {
        Game game = Start(Level(ThreeInARow.With(5, 5, 'H')));

        Assert.All(Lessons, lesson => Assert.False(MoveLessons.Teaches(game, Move.Tap(P(5, 5)), lesson, AnySeed)));
        Assert.Throws<ArgumentException>(() => MoveLessons.Teaches(game, Move.Swap(P(4, 4), P(5, 4)), MoveLesson.Match, AnySeed));
    }

    [Fact]
    public void The_advisor_shows_the_move_that_teaches_the_lesson_and_nothing_for_a_lesson_the_board_does_not_offer()
    {
        HintAdvisor advisor = Expert();

        Assert.Equal(PiecesOf(TradeUp), PiecesOf(advisor.MoveFor(Start(Level(ThreeInARow)), AttemptSeed, MoveLesson.Match)));
        Assert.Null(advisor.MoveFor(Start(Level(ThreeInARow)), AttemptSeed, MoveLesson.FourInALine));
        Assert.Equal(PiecesOf(TradeUp), PiecesOf(advisor.MoveFor(Start(Level(FourInARow)), AttemptSeed, MoveLesson.FourInALine)));
        Assert.Null(advisor.MoveFor(Start(Level(FourInARow)), AttemptSeed, MoveLesson.FiveInALine));
        Assert.Equal(PiecesOf(TradeUp), PiecesOf(advisor.MoveFor(Start(Level(FiveInARow)), AttemptSeed, MoveLesson.FiveInALine)));
    }

    /// <summary>The two cells a move trades, in reading order: the game offers each trade from both of its pieces.</summary>
    internal static (Position, Position) PiecesOf(Move? move)
    {
        Move shown = move ?? throw new Xunit.Sdk.XunitException("no move was shown");
        return shown.From.Y < shown.To.Y || (shown.From.Y == shown.To.Y && shown.From.X < shown.To.X) ? (shown.From, shown.To) : (shown.To, shown.From);
    }

    /// <summary>
    /// On the first boards of the first levels, every lesson is searched among all the legal moves: the advisor shows
    /// a move exactly when one teaches the lesson, always one that does, and asking for it changes nothing of the game.
    /// </summary>
    [Fact]
    public void On_the_first_levels_boards_the_advisor_shows_a_lesson_move_exactly_when_one_is_there_and_the_game_is_left_as_it_was()
    {
        HintAdvisor advisor = Expert();
        int offered = 0;
        foreach (int number in Enumerable.Range(1, 6))
        {
            LevelDefinition level = LevelJson.Load(Path.Combine(RepoFiles.LiveLevelsFolder, $"level_{number:0000}.json"));
            foreach (int attempt in Enumerable.Range(0, 4))
            {
                Game game = LevelStart.Attempt(level, attempt);
                ulong before = game.Board.ComputeHash();
                ulong seed = LevelStart.SeedsOf(level, attempt).Play;
                foreach (MoveLesson lesson in Lessons)
                {
                    bool isThere = game.FindLegalMoves().Any(move => MoveLessons.Teaches(game, move, lesson, AnySeed));
                    Move? shown = advisor.MoveFor(game, seed, lesson);

                    Assert.True(isThere == shown.HasValue, $"level {number}, attempt {attempt}: {lesson} is {(isThere ? string.Empty : "not ")}there, the advisor shows {shown}");
                    if (shown is Move move)
                    {
                        offered++;
                        Assert.True(game.IsLegal(move) && MoveLessons.Teaches(game, move, lesson, AnySeed), $"level {number}: {move} does not teach {lesson}");
                        Assert.Equal(shown, advisor.MoveFor(game, seed, lesson));
                    }
                }

                Assert.Equal(before, game.Board.ComputeHash());
            }
        }

        Assert.True(offered > 0, "no first board of levels 1 to 6 offered any lesson");
    }

    [Fact]
    public void Every_first_board_of_the_first_level_offers_a_match_to_show()
    {
        LevelDefinition first = LevelJson.Load(Path.Combine(RepoFiles.LiveLevelsFolder, "level_0001.json"));
        HintAdvisor advisor = Expert();

        Assert.All(Enumerable.Range(0, first.Attempts.SeedPool.Count), attempt =>
            Assert.NotNull(advisor.MoveFor(LevelStart.Attempt(first, attempt), LevelStart.SeedsOf(first, attempt).Play, MoveLesson.Match)));
    }

    private static readonly MoveLesson[] Lessons = { MoveLesson.Match, MoveLesson.FourInALine, MoveLesson.FiveInALine };

    private static HintAdvisor Expert() => HintAdvisor.TryCreate("expert", out HintAdvisor? advisor) ? advisor : throw new InvalidOperationException("no expert");
}
