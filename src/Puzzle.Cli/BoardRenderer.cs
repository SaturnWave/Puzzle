using System.Text;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Cli;

/// <summary>Plain-text board view for the demo command. Jelly shows as a trailing mark, chains in brackets.</summary>
internal static class BoardRenderer
{
    public static string Render(Game game)
    {
        Board board = game.Board;
        var text = new StringBuilder();
        text.Append("    ");
        for (int x = 0; x < board.Width; x++)
        {
            text.Append(x.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).Append("  ");
        }

        text.AppendLine();
        for (int y = 0; y < board.Height; y++)
        {
            text.Append(y.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).Append("  ");
            for (int x = 0; x < board.Width; x++)
            {
                text.Append(Cell(board, new Position(x, y)));
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    public static string Goals(Game game)
    {
        var parts = new List<string>();
        for (int i = 0; i < game.Goals.Count; i++)
        {
            GoalProgress progress = game.Goals[i];
            string name = progress.Goal switch
            {
                GoalSpec.CollectColor collect => collect.Color.ToString(),
                GoalSpec.BreakBoxes _ => "Boxes",
                GoalSpec.ClearJelly _ => "Jelly",
                GoalSpec.BreakChains _ => "Chains",
                GoalSpec.DropItems _ => "Items",
                _ => throw new InvalidOperationException($"Unhandled goal {progress.Goal.GetType().Name}."),
            };
            parts.Add($"{name} {Math.Min(progress.Done, progress.Goal.Target)}/{progress.Goal.Target}");
        }

        return string.Join("  ", parts);
    }

    private static string Cell(Board board, Position p)
    {
        if (!board.Shape.IsPlayable(p))
        {
            return "    ";
        }

        Tile tile = board.TileAt(p);
        char glyph = tile.Kind switch
        {
            TileKind.Empty => ' ',
            TileKind.Hidden => '?',
            TileKind.Gem => char.ToUpperInvariant(TileGlyphs.GemGlyph(tile.Color)),
            TileKind.RocketH => '-',
            TileKind.RocketV => '|',
            TileKind.Bomb => '*',
            TileKind.BigBomb => '%',
            TileKind.Seeker => '+',
            TileKind.ColorBomb => '@',
            TileKind.Box => (char)('0' + tile.Hp),
            TileKind.Item => '$',
            _ => throw new InvalidOperationException($"Unhandled tile kind {tile.Kind}."),
        };
        bool chained = board.ChainAt(p) > 0;
        char jelly = board.JellyAt(p) switch
        {
            0 => ' ',
            1 => '\'',
            _ => '"',
        };
        return chained ? $"[{glyph}]{jelly}" : $" {glyph}{jelly} ";
    }
}
