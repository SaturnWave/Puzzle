using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Core.Tests;

/// <summary>Builders for hand-written test boards.</summary>
internal static class TestBoards
{
    private const string FillerColors = "rgby";

    /// <summary>
    /// A board of red/green/blue/yellow in a diagonal pattern that never matches by itself. Tests draw their
    /// setups in purple ('p') and orange ('o'), which the filler never uses, so only the setup can match.
    /// </summary>
    public static string[] Filler(int width, int height) =>
        Enumerable.Range(0, height)
            .Select(y => new string(Enumerable.Range(0, width).Select(x => FillerColors[(x + 2 * y) % 4]).ToArray()))
            .ToArray();

    public static string[] With(this string[] rows, int x, int y, char glyph)
    {
        var copy = (string[])rows.Clone();
        char[] row = copy[y].ToCharArray();
        row[x] = glyph;
        copy[y] = new string(row);
        return copy;
    }

    public static string[] Blank(int width, int height, char glyph = '.') =>
        Enumerable.Repeat(new string(glyph, width), height).ToArray();

    public static LevelDefinition Level(
        string[] tiles,
        string[]? cells = null,
        string[]? jelly = null,
        string[]? chains = null,
        GoalSpec[]? goals = null,
        int moves = 30,
        int colors = 6,
        ItemSpawnRules? items = null)
    {
        LevelLayout layout = LevelLayout.Parse(new LayoutLayers
        {
            Cells = cells ?? Blank(tiles[0].Length, tiles.Length, '#'),
            Tiles = tiles,
            Jelly = jelly,
            Chains = chains,
        });
        return new LevelDefinition(
            new LevelId(1),
            DifficultyTier.Normal,
            layout,
            goals ?? new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 999) },
            moves,
            new SpawnRules { Palette = GemColors.First(colors), Items = items ?? ItemSpawnRules.None });
    }

    public static Game Start(LevelDefinition level, ulong seed = 1) =>
        Game.Start(level, seed, new GameOptions { RecordEvents = true });

    public static Board BoardOf(params string[] tiles)
    {
        LevelLayout layout = LevelLayout.Parse(new LayoutLayers
        {
            Cells = Blank(tiles[0].Length, tiles.Length, '#'),
            Tiles = tiles,
        });
        var board = new Board(layout.Shape);
        for (int y = 0; y < layout.Shape.Height; y++)
        {
            for (int x = 0; x < layout.Shape.Width; x++)
            {
                var position = new Position(x, y);
                board.Set(layout.Shape.IndexOf(position), layout.PresetAt(position) ?? Tile.Empty);
            }
        }

        return board;
    }

    /// <summary>Events of the player's own match round (before the first cascade round).</summary>
    public static IReadOnlyList<GameEvent> FirstRound(this Game game) =>
        game.LastEvents.TakeWhile(e => e is not GameEvent.RoundStarted { Round: > 0 }).ToList();

    public static Position P(int x, int y) => new Position(x, y);
}
