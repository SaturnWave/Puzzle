using System;
using System.Collections.Generic;
using System.Text;
using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>Raw character-grid layers of a level layout, one string per row (top row first).</summary>
    public sealed class LayoutLayers
    {
        public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();

        /// <summary>Preset tiles; omitted means every cell starts with a random gem.</summary>
        public IReadOnlyList<string>? Tiles { get; init; }

        public IReadOnlyList<string>? Jelly { get; init; }

        public IReadOnlyList<string>? Chains { get; init; }
    }

    /// <summary>
    /// The validated static starting state of a level: board shape, preset tiles, chains and jelly.
    /// </summary>
    public sealed class LevelLayout
    {
        public const int MaxJellyLayers = 2;
        public const int MaxChainHp = 2;

        private readonly Tile?[] _presets;
        private readonly byte[] _chains;
        private readonly byte[] _jelly;

        private LevelLayout(BoardShape shape, Tile?[] presets, byte[] chains, byte[] jelly)
        {
            Shape = shape;
            _presets = presets;
            _chains = chains;
            _jelly = jelly;
        }

        public BoardShape Shape { get; }

        public int BoxCount => Count(i => _presets[i] is Tile tile && tile.Kind == TileKind.Box);

        public int PresetItemCount => Count(i => _presets[i] is Tile tile && tile.Kind == TileKind.Item);

        public int JellyCellCount => Count(i => _jelly[i] > 0);

        public int ChainCount => Count(i => _chains[i] > 0);

        public bool HasRandomCells => Count(i => Shape.IsPlayable(i) && _presets[i] is null) > 0;

        /// <summary>The preset tile, or <c>null</c> when the cell starts with a random gem.</summary>
        public Tile? PresetAt(Position position) => _presets[Shape.IndexOf(position)];

        public int ChainAt(Position position) => _chains[Shape.IndexOf(position)];

        public int JellyAt(Position position) => _jelly[Shape.IndexOf(position)];

        /// <exception cref="LevelFormatException">The layers are malformed or contradict each other.</exception>
        public static LevelLayout Parse(LayoutLayers layers)
        {
            BoardShape shape = ParseShape(layers.Cells);
            var presets = new Tile?[shape.CellCount];
            var chains = new byte[shape.CellCount];
            var jelly = new byte[shape.CellCount];

            ForEachGlyph(shape, layers.Tiles, "tiles", (index, glyph) =>
            {
                if (!TileGlyphs.TryParseTile(glyph, out Tile? tile))
                {
                    throw Error(shape, index, "tiles", $"unknown tile glyph '{glyph}'");
                }

                presets[index] = tile;
            });
            ForEachGlyph(shape, layers.Jelly, "jelly", (index, glyph) =>
            {
                jelly[index] = ParseCount(shape, index, "jelly", glyph, MaxJellyLayers);
            });
            ForEachGlyph(shape, layers.Chains, "chains", (index, glyph) =>
            {
                chains[index] = ParseCount(shape, index, "chains", glyph, MaxChainHp);
                bool holdsChainableTile = presets[index] is not Tile tile || tile.IsGem || tile.IsSpecial;
                if (chains[index] > 0 && !holdsChainableTile)
                {
                    throw Error(shape, index, "chains", "only gems and specials can be chained");
                }
            });

            return new LevelLayout(shape, presets, chains, jelly);
        }

        public LayoutLayers ToLayers() => new LayoutLayers
        {
            Cells = BuildRows(i => Shape.IsPlayable(i) ? TileGlyphs.Playable : TileGlyphs.Gap),
            Tiles = BuildRows(i => TileGlyphs.ToGlyph(_presets[i])),
            Jelly = BuildRows(i => TileGlyphs.CountGlyph(_jelly[i])),
            Chains = BuildRows(i => TileGlyphs.CountGlyph(_chains[i])),
        };

        internal Tile? Preset(int index) => _presets[index];

        internal int Chain(int index) => _chains[index];

        internal int Jelly(int index) => _jelly[index];

        /// <summary>Distinct colors of the preset gems.</summary>
        internal IEnumerable<GemColor> PresetColors()
        {
            var colors = new HashSet<GemColor>();
            foreach (Tile? preset in _presets)
            {
                if (preset is Tile tile && tile.IsGem)
                {
                    colors.Add(tile.Color);
                }
            }

            return colors;
        }

        private static BoardShape ParseShape(IReadOnlyList<string> rows)
        {
            if (rows.Count == 0)
            {
                throw new LevelFormatException("The cells layer is empty.");
            }

            int width = rows[0].Length;
            var playable = new bool[rows.Count * width];
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new LevelFormatException($"cells row {y} has {rows[y].Length} columns, expected {width}.");
                }

                for (int x = 0; x < width; x++)
                {
                    char glyph = rows[y][x];
                    if (glyph != TileGlyphs.Playable && glyph != TileGlyphs.Gap)
                    {
                        throw new LevelFormatException($"cells ({x},{y}): unknown glyph '{glyph}'.");
                    }

                    playable[y * width + x] = glyph == TileGlyphs.Playable;
                }
            }

            try
            {
                return new BoardShape(width, rows.Count, playable);
            }
            catch (ArgumentException ex)
            {
                throw new LevelFormatException($"Invalid board shape: {ex.Message}", ex);
            }
        }

        private static void ForEachGlyph(BoardShape shape, IReadOnlyList<string>? rows, string layer, Action<int, char> parse)
        {
            if (rows is null || rows.Count == 0)
            {
                return;
            }

            if (rows.Count != shape.Height)
            {
                throw new LevelFormatException($"The {layer} layer has {rows.Count} rows, expected {shape.Height}.");
            }

            for (int y = 0; y < shape.Height; y++)
            {
                if (rows[y].Length != shape.Width)
                {
                    throw new LevelFormatException($"{layer} row {y} has {rows[y].Length} columns, expected {shape.Width}.");
                }

                for (int x = 0; x < shape.Width; x++)
                {
                    int index = shape.IndexOf(x, y);
                    char glyph = rows[y][x];
                    if (!shape.IsPlayable(index))
                    {
                        if (glyph != TileGlyphs.NoLayer)
                        {
                            throw Error(shape, index, layer, "a gap cell must be '.'");
                        }

                        continue;
                    }

                    parse(index, glyph);
                }
            }
        }

        private static byte ParseCount(BoardShape shape, int index, string layer, char glyph, int max) =>
            TileGlyphs.TryParseCount(glyph, max, out int count)
                ? (byte)count
                : throw Error(shape, index, layer, $"'{glyph}' is not '.' or 1..{max}");

        private static LevelFormatException Error(BoardShape shape, int index, string layer, string problem) =>
            new LevelFormatException($"{layer} {shape.PositionOf(index)}: {problem}.");

        private string[] BuildRows(Func<int, char> glyphAt)
        {
            var rows = new string[Shape.Height];
            var row = new StringBuilder(Shape.Width);
            for (int y = 0; y < Shape.Height; y++)
            {
                row.Clear();
                for (int x = 0; x < Shape.Width; x++)
                {
                    row.Append(glyphAt(Shape.IndexOf(x, y)));
                }

                rows[y] = row.ToString();
            }

            return rows;
        }

        private int Count(Func<int, bool> predicate)
        {
            int count = 0;
            for (int i = 0; i < Shape.CellCount; i++)
            {
                count += predicate(i) ? 1 : 0;
            }

            return count;
        }
    }
}
