using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Engine.Resolution;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

public class GravityTests
{
    [Fact]
    public void Tiles_fall_to_the_bottom_and_spawns_fill_from_the_top()
    {
        Board board = BoardOf(
            "p",
            ".",
            ".");

        Settle(board);

        Assert.Equal(Tile.Gem(GemColor.Purple), board.TileAt(P(0, 2)));
        Assert.Equal(Tile.Gem(GemColor.Orange), board.TileAt(P(0, 1)));
        Assert.Equal(Tile.Gem(GemColor.Orange), board.TileAt(P(0, 0)));
    }

    [Fact]
    public void Tiles_fall_through_gaps()
    {
        LevelLayout layout = LevelLayout.Parse(new LayoutLayers
        {
            Cells = new[] { "#", ".", "#" },
            Tiles = new[] { "p", ".", "." },
        });
        Board board = BoardFrom(layout);

        Settle(board);

        Assert.Equal(Tile.Gem(GemColor.Purple), board.TileAt(P(0, 2)));
        Assert.Equal(Tile.Gem(GemColor.Orange), board.TileAt(P(0, 0)));
    }

    [Fact]
    public void Box_blocks_the_column_and_cells_below_fill_diagonally()
    {
        Board board = BoardOf(
            "g1g",
            "g.g",
            "g.g");

        Settle(board);

        Assert.Equal(TileKind.Box, board.TileAt(P(1, 0)).Kind);
        Assert.True(board.TileAt(P(1, 1)).IsGem);
        Assert.True(board.TileAt(P(1, 2)).IsGem);
        Assert.All(AllPositions(board), p => Assert.False(board.TileAt(p).IsEmpty));
    }

    [Fact]
    public void Chained_tile_does_not_fall_and_blocks_tiles_above()
    {
        Board board = BoardOf(
            "p",
            "g",
            ".");
        board.SetChain(board.Shape.IndexOf(P(0, 1)), 1);

        Settle(board);

        Assert.Equal(Tile.Gem(GemColor.Purple), board.TileAt(P(0, 0)));
        Assert.Equal(Tile.Gem(GemColor.Green), board.TileAt(P(0, 1)));
        Assert.True(board.TileAt(P(0, 2)).IsEmpty); // single column: no diagonal neighbor to slide from
    }

    [Fact]
    public void Item_reaching_the_exit_is_collected()
    {
        Board board = BoardOf(
            "I",
            ".");
        var goals = new GoalTracker(new GoalSpec[] { new GoalSpec.DropItems(1) });
        var items = new ItemLedger { Spawned = 1 };

        Settle(board, goals, items);

        Assert.True(goals.AllComplete);
        Assert.Equal(1, items.Collected);
        Assert.True(board.TileAt(P(0, 1)).IsGem);
    }

    private static void Settle(Board board, GoalTracker? goals = null, ItemLedger? items = null)
    {
        var gravity = new Gravity(
            board,
            new ConstantSource(Tile.Gem(GemColor.Orange)),
            new MatchFinder(board),
            goals ?? new GoalTracker(new GoalSpec[] { new GoalSpec.BreakBoxes(1) }),
            new StatsCounter(),
            EventLog.Disabled,
            items ?? new ItemLedger());
        gravity.Settle();
    }

    private static Board BoardFrom(LevelLayout layout)
    {
        var board = new Board(layout.Shape);
        foreach (Position p in AllPositions(board).Where(layout.Shape.IsPlayable))
        {
            board.Set(layout.Shape.IndexOf(p), layout.PresetAt(p) ?? Tile.Empty);
        }

        return board;
    }

    private static IEnumerable<Position> AllPositions(Board board) =>
        from y in Enumerable.Range(0, board.Height)
        from x in Enumerable.Range(0, board.Width)
        select P(x, y);

    private sealed class ConstantSource : ITileSource
    {
        private readonly Tile _tile;

        public ConstantSource(Tile tile) => _tile = tile;

        public Tile Next(in SpawnContext context) => _tile;

        public ITileSource Clone() => this;
    }
}
