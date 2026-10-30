using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The celebration after a win (docs/GAME_DESIGN.md §19): every move left turns into a rocket on the board. It is
/// played only when asked for, after the result is settled, so nothing that measures a level sees it.
/// </summary>
public class FinaleTests
{
    /// <summary>The move that wins every level of these tests: the third purple gem comes down into the bottom row.</summary>
    private static readonly Move TheWinningMove = Move.Swap(P(2, 2), P(2, 3));

    /// <summary>A small board won by its first move, with <paramref name="moves"/> to spend.</summary>
    private static Game Won(int moves, ulong seed = 1)
    {
        string[] tiles = Filler(4, 4).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');
        Game game = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: moves), seed);
        Assert.True(game.TryPlay(TheWinningMove));
        Assert.Equal(GameStatus.Won, game.Status);
        return game;
    }

    private static bool IsRocketFiring(GameEvent gameEvent) =>
        gameEvent is GameEvent.EffectTriggered { Kind: EffectKind.ClearRow or EffectKind.ClearColumn };

    [Fact]
    public void Every_move_left_becomes_a_rocket_that_fires_once()
    {
        Game game = Won(moves: 8);
        int movesUsed = game.MovesUsed;
        int gemsCleared = game.TotalStats.GemsCleared;

        FinaleOutcome finale = game.PlayFinale();

        List<GameEvent> firstWave = game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted).ToList();
        List<GameEvent.SpecialCreated> placed = firstWave.TakeWhile(e => e is GameEvent.SpecialCreated).Cast<GameEvent.SpecialCreated>().ToList();
        Assert.Equal(7, finale.RocketsFired);
        Assert.Equal(7, placed.Count);
        Assert.All(placed, e => Assert.True(e.Kind.IsRocket()));
        Assert.Equal(7, placed.Select(e => e.At).Distinct().Count());
        Assert.Equal(7, firstWave.Count(IsRocketFiring));
        Assert.True(finale.Stats.SpecialsActivated >= 7);
        Assert.True(finale.Stats.GemsCleared > 0);

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(movesUsed, game.MovesUsed);
        Assert.Equal(7, game.MovesLeft);
        Assert.Equal(gemsCleared, game.TotalStats.GemsCleared);
        Assert.True(game.FinalePlayed);
    }

    [Fact]
    public void The_finale_counts_toward_no_goal_so_a_record_written_after_it_tells_the_play()
    {
        // Purple keeps falling and the rockets keep clearing it; the goal stays where the winning move left it.
        Game game = Won(moves: 20);
        int[] reached = Enumerable.Range(0, game.Goals.Count).Select(goal => game.Goals[goal].Done).ToArray();

        FinaleOutcome finale = game.PlayFinale();

        // Without purple among what the rockets cleared, this test would show nothing.
        Assert.True(finale.Stats.GemsCleared > 0);
        Assert.Contains(game.LastEvents.OfType<GameEvent.TileDestroyed>(), e => e.Tile.IsGemOf(GemColor.Purple));
        Assert.Equal(reached, Enumerable.Range(0, game.Goals.Count).Select(goal => game.Goals[goal].Done).ToArray());
        Assert.Equal(reached, AttemptOutcome.Of(game, new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero)).Goals.Select(goal => goal.Done).ToArray());
    }

    [Fact]
    public void The_same_game_has_the_same_finale()
    {
        Game one = Won(moves: 6, seed: 3);
        Game same = Won(moves: 6, seed: 3);
        Game other = Won(moves: 6, seed: 4);

        one.PlayFinale();
        same.PlayFinale();
        other.PlayFinale();

        Assert.Equal(one.Board.ComputeHash(), same.Board.ComputeHash());
        Assert.Equal(one.LastEvents.Count, same.LastEvents.Count);
        Assert.NotEqual(one.Board.ComputeHash(), other.Board.ComputeHash());
    }

    [Fact]
    public void A_board_with_fewer_free_gems_than_moves_left_is_served_in_waves()
    {
        // Sixteen cells and twenty-nine moves left: one wave cannot hold them.
        Game game = Won(moves: 30);

        FinaleOutcome finale = game.PlayFinale();

        Assert.Equal(29, finale.RocketsFired);
        Assert.True(game.LastEvents.Count(IsRocketFiring) >= 29);
    }

    [Fact]
    public void A_win_on_the_last_move_has_a_finale_with_nothing_in_it()
    {
        Game game = Won(moves: 1);
        ulong before = game.Board.ComputeHash();

        FinaleOutcome finale = game.PlayFinale();

        Assert.Equal(0, finale.RocketsFired);
        Assert.Empty(game.LastEvents);
        Assert.Equal(before, game.Board.ComputeHash());
    }

    [Fact]
    public void Only_a_won_game_has_a_finale_and_it_has_one()
    {
        Game playing = Start(Level(Filler(7, 7).With(3, 3, 'H')));
        Assert.Throws<InvalidOperationException>(() => playing.PlayFinale());
        Assert.False(playing.FinalePlayed);

        Game lost = Start(Level(Filler(7, 7).With(3, 3, 'H'), moves: 1));
        Assert.True(lost.TryPlay(Move.Tap(P(3, 3))));
        Assert.Throws<InvalidOperationException>(() => lost.PlayFinale());

        Game won = Won(moves: 4);
        won.PlayFinale();
        Assert.Throws<InvalidOperationException>(() => won.PlayFinale());
    }
}
