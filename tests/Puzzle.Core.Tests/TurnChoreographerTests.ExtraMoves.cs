using Puzzle.Bot;
using Puzzle.Client.Play;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Randomness;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The extra moves as the Unity game shows them (TurnChoreographer.ComposeTheExtraMoves): a board that had a move
/// left gets nothing to show; one that had none is dealt anew as after a turn, and no move is spent.
/// </summary>
public partial class TurnChoreographerTests
{
    [Fact]
    public void The_extra_moves_deal_a_board_without_a_move_anew_and_spend_no_move()
    {
        Table table = OutOfMovesOnABoardWithNoMoveLeft();

        Assert.True(table.Game.TryTakeExtraMoves());
        TurnScript script = TurnChoreographer.ComposeTheExtraMoves(table.Ledger, table.Game.LastEvents, table.Game.Board);

        Assert.Single(script.Cues.OfType<Cue.Reshuffle>());
        Assert.Empty(script.Cues.OfType<Cue.MoveSpent>());
        Assert.Empty(table.Ledger.DifferencesFrom(table.Game.Board));
    }

    [Fact]
    public void The_extra_moves_of_a_board_that_has_a_move_have_nothing_to_show()
    {
        Table table = TableOf(Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p'));
        Game game = table.Game;
        while (game.Status == GameStatus.Playing)
        {
            table.Play(game.FindLegalMoves()[0]);
        }

        Assert.True(game.Status == GameStatus.LostOutOfMoves && game.FindLegalMoves().Count > 0, "the board does not run out of moves with a move left on it");
        Assert.True(game.TryTakeExtraMoves());
        TurnScript script = TurnChoreographer.ComposeTheExtraMoves(table.Ledger, game.LastEvents, game.Board);

        Assert.Empty(script.Cues);
        Assert.Equal(0f, script.Seconds);
    }

    /// <summary>
    /// Small and many-coloured boards soon have no move left: the random bot plays such a level, seed after seed,
    /// until its moves run out on a board with none. The same search finds the same game every time.
    /// </summary>
    private static Table OutOfMovesOnABoardWithNoMoveLeft()
    {
        var bot = new RandomPolicy();
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var table = new Table(Game.Start(Level(Blank(6, 6), colors: 8, moves: 6), seed, new GameOptions { RecordEvents = true }));
            var rng = new Rng(seed, 11);
            while (table.Game.Status == GameStatus.Playing)
            {
                table.Play(bot.ChooseMove(table.Game, table.Game.FindLegalMoves(), rng));
            }

            if (table.Game.Status == GameStatus.LostOutOfMoves && table.Game.FindLegalMoves().Count == 0)
            {
                return table;
            }
        }

        throw new InvalidOperationException("No game of the level ran out of moves on a board with none; pick another level or more seeds.");
    }
}
