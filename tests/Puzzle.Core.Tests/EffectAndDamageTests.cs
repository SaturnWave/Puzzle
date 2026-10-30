using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class EffectAndDamageTests
{
    [Fact]
    public void Tapped_horizontal_rocket_clears_its_row()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H')));

        Assert.True(game.TryPlay(Move.Tap(P(3, 3))));

        List<Position> destroyed = GemsDestroyedBeforeCascades(game);
        Assert.Equal(6, destroyed.Count);
        Assert.All(destroyed, p => Assert.Equal(3, p.Y));
    }

    [Theory]
    [InlineData('X', 1)] // bomb: 3x3
    [InlineData('M', 2)] // big bomb: 5x5
    public void Tapped_bomb_clears_a_square_of_its_radius(char glyph, int radius)
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, glyph)));

        game.TryPlay(Move.Tap(P(3, 3)));

        List<Position> gems = GemsDestroyedBeforeCascades(game);
        Assert.Equal(((2 * radius) + 1) * ((2 * radius) + 1) - 1, gems.Count);
        Assert.All(gems, p => Assert.True(Math.Abs(p.X - 3) <= radius && Math.Abs(p.Y - 3) <= radius));
    }

    [Fact]
    public void Bomb_blast_is_clipped_at_the_board_edge()
    {
        Game game = Start(Level(Filler(7, 7).With(0, 0, 'X')));

        game.TryPlay(Move.Tap(P(0, 0)));

        Assert.Equal(3, GemsDestroyedBeforeCascades(game).Count);
    }

    [Fact]
    public void Color_bomb_swapped_with_a_gem_clears_every_gem_of_that_color()
    {
        string[] tiles = Filler(7, 7).With(3, 3, 'C');
        Game game = Start(Level(tiles));
        GemColor partnerColor = game.Board.TileAt(P(4, 3)).Color;
        int gemsOfColor = CountGems(game.Board, partnerColor);

        game.TryPlay(Move.Swap(P(3, 3), P(4, 3)));

        Assert.Equal(gemsOfColor, GemsDestroyedBeforeCascades(game).Count);
    }

    [Fact]
    public void Rocket_plus_rocket_clears_a_row_and_a_column()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H').With(4, 3, 'V')));

        game.TryPlay(Move.Swap(P(3, 3), P(4, 3)));

        List<Position> destroyed = GemsDestroyedBeforeCascades(game);
        Assert.Equal(5 + 6, destroyed.Count);
        Assert.All(destroyed, p => Assert.True(p.Y == 3 || p.X == 4));
    }

    [Theory]
    [InlineData('X', 'X', 23)]     // radius 1+1: 5x5 around (3,4) minus the two bomb cells
    [InlineData('X', 'M', 7 * 6 - 2)] // radius 1+2: rows 1..6 of a 7x7 board
    [InlineData('M', 'M', 7 * 7 - 2)] // radius 2+2: the whole board
    public void Two_bombs_blast_with_the_sum_of_their_radii(char first, char second, int expectedGems)
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, first).With(3, 4, second)));

        game.TryPlay(Move.Swap(P(3, 3), P(3, 4)));

        Assert.Equal(expectedGems, GemsDestroyedBeforeCascades(game).Count);
    }

    [Fact]
    public void Rocket_plus_bomb_clears_three_rows_and_three_columns()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H').With(4, 3, 'X')));

        game.TryPlay(Move.Swap(P(3, 3), P(4, 3)));

        List<Position> gems = GemsDestroyedBeforeCascades(game);
        Assert.Equal(21 + 21 - 9 - 2, gems.Count);
        Assert.All(gems, p => Assert.True(Math.Abs(p.Y - 3) <= 1 || Math.Abs(p.X - 4) <= 1));
    }

    [Fact]
    public void Color_bomb_plus_rocket_converts_the_most_common_color()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'C').With(4, 3, 'H')));

        game.TryPlay(Move.Swap(P(3, 3), P(4, 3)));

        int converted = game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted).OfType<GameEvent.SpecialCreated>().Count();
        Assert.True(converted >= 10, $"expected a full color worth of rockets, got {converted}");
        Assert.True(game.LastMove.SpecialsActivated > converted);
    }

    [Fact]
    public void Adjacent_match_damages_a_box_once()
    {
        string[] tiles = Filler(7, 7)
            .With(0, 5, 'p').With(1, 5, 'p').With(2, 4, 'p')
            .With(1, 6, '2');
        Game game = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.BreakBoxes(1) }));

        game.TryPlay(Move.Swap(P(2, 4), P(2, 5)));

        GameEvent.BoxDamaged damage = Assert.Single(game.FirstRound().OfType<GameEvent.BoxDamaged>());
        Assert.Equal(P(1, 6), damage.At);
        Assert.Equal(1, damage.HpLeft);
    }

    [Fact]
    public void Chain_absorbs_the_hit_and_keeps_the_gem()
    {
        string[] chains = Blank(7, 7).With(5, 3, '1');
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H'), chains: chains));

        game.TryPlay(Move.Tap(P(3, 3)));

        IReadOnlyList<GameEvent> effectEvents = game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted).ToList();
        Assert.Contains(new GameEvent.ChainDamaged(P(5, 3), 0), effectEvents);
        Assert.DoesNotContain(P(5, 3), GemsDestroyedBeforeCascades(game));
        Assert.Equal(1, game.LastMove.ChainsBroken);
    }

    [Fact]
    public void Clearing_the_last_jelly_completes_the_goal_and_wins()
    {
        string[] tiles = Filler(7, 7).With(0, 5, 'p').With(1, 5, 'p').With(2, 4, 'p');
        string[] jelly = Blank(7, 7).With(0, 5, '1').With(1, 5, '1').With(2, 5, '1');
        Game game = Start(Level(tiles, jelly: jelly, goals: new GoalSpec[] { new GoalSpec.ClearJelly(3) }));

        game.TryPlay(Move.Swap(P(2, 4), P(2, 5)));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(0, game.Board.JellyAt(P(1, 5)));
    }

    [Fact]
    public void Seeker_flies_to_the_goal_box()
    {
        string[] tiles = Filler(7, 7).With(0, 0, 'S').With(6, 6, '1');
        Game game = Start(Level(tiles, goals: new GoalSpec[] { new GoalSpec.BreakBoxes(1) }));

        game.TryPlay(Move.Tap(P(0, 0)));

        Assert.Contains(new GameEvent.SeekerLaunched(P(0, 0), P(6, 6)), game.LastEvents);
        Assert.Equal(GameStatus.Won, game.Status);
    }

    [Fact]
    public void Swap_without_a_match_is_rejected_and_costs_nothing()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'X')));
        ulong before = game.Board.ComputeHash();

        Assert.False(game.TryPlay(Move.Swap(P(0, 0), P(1, 0))));

        Assert.Equal(0, game.MovesUsed);
        Assert.Equal(before, game.Board.ComputeHash());
    }

    [Fact]
    public void Running_out_of_moves_loses()
    {
        Game game = Start(Level(Filler(7, 7).With(3, 3, 'H'), moves: 1));

        game.TryPlay(Move.Tap(P(3, 3)));

        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);
        Assert.Throws<InvalidOperationException>(() => game.TryPlay(Move.Tap(P(0, 0))));
    }

    /// <summary>Gems destroyed by the move's own effects (specials consumed as they fire are not counted).</summary>
    private static List<Position> GemsDestroyedBeforeCascades(Game game) =>
        game.LastEvents
            .TakeWhile(e => e is not GameEvent.RoundStarted)
            .OfType<GameEvent.TileDestroyed>()
            .Where(e => e.Tile.IsGem)
            .Select(e => e.At)
            .ToList();

    private static int CountGems(Board board, GemColor color) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         where board.TileAt(P(x, y)).IsGemOf(color)
         select 1).Count();
}
