using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>
    /// The single-character legend of the layered level format (see docs/GAME_DESIGN.md §10).
    /// </summary>
    public static class TileGlyphs
    {
        public const char Playable = '#';
        public const char Gap = '.';
        public const char RandomGem = '.';
        public const char NoLayer = '.';

        /// <summary>Parses a tile-layer glyph. <c>null</c> means "random gem chosen at game start".</summary>
        public static bool TryParseTile(char glyph, out Tile? tile)
        {
            tile = glyph switch
            {
                'r' => Tile.Gem(GemColor.Red),
                'g' => Tile.Gem(GemColor.Green),
                'b' => Tile.Gem(GemColor.Blue),
                'y' => Tile.Gem(GemColor.Yellow),
                'p' => Tile.Gem(GemColor.Purple),
                'o' => Tile.Gem(GemColor.Orange),
                'H' => Tile.Special(TileKind.RocketH),
                'V' => Tile.Special(TileKind.RocketV),
                'X' => Tile.Special(TileKind.Bomb),
                'S' => Tile.Special(TileKind.Seeker),
                'C' => Tile.Special(TileKind.ColorBomb),
                '1' => Tile.Box(1),
                '2' => Tile.Box(2),
                '3' => Tile.Box(3),
                'I' => Tile.Item,
                _ => (Tile?)null,
            };
            return tile is not null || glyph == RandomGem;
        }

        public static char ToGlyph(Tile? tile)
        {
            if (tile is not Tile value)
            {
                return RandomGem;
            }

            return value.Kind switch
            {
                TileKind.Gem => GemGlyph(value.Color),
                TileKind.RocketH => 'H',
                TileKind.RocketV => 'V',
                TileKind.Bomb => 'X',
                TileKind.Seeker => 'S',
                TileKind.ColorBomb => 'C',
                TileKind.Box => (char)('0' + value.Hp),
                TileKind.Item => 'I',
                _ => throw new System.ArgumentOutOfRangeException(nameof(tile), value.Kind, "This tile kind cannot appear in a level file."),
            };
        }

        public static char GemGlyph(GemColor color) => color switch
        {
            GemColor.Red => 'r',
            GemColor.Green => 'g',
            GemColor.Blue => 'b',
            GemColor.Yellow => 'y',
            GemColor.Purple => 'p',
            GemColor.Orange => 'o',
            _ => throw new System.ArgumentOutOfRangeException(nameof(color), color, "Unknown gem color."),
        };

        /// <summary>Parses a count layer glyph ('.' or a digit) into 0..<paramref name="max"/>.</summary>
        public static bool TryParseCount(char glyph, int max, out int count)
        {
            count = glyph == NoLayer ? 0 : glyph - '0';
            return glyph == NoLayer || (count >= 1 && count <= max);
        }

        public static char CountGlyph(int count) => count == 0 ? NoLayer : (char)('0' + count);
    }
}
