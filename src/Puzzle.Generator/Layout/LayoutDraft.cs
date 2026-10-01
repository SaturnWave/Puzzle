using System.Text;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator.Layout;

/// <summary>
/// A level layout under construction. Every placement helper mirrors across the vertical axis, so generated
/// boards are left-right symmetric (the research's "symmetry mask" rule).
/// </summary>
internal sealed class LayoutDraft
{
    public const char Free = '.';

    public LayoutDraft(BoardSize size, bool[] playable)
    {
        Width = size.Width;
        Height = size.Height;
        Playable = playable;
        Tiles = Enumerable.Repeat(Free, playable.Length).ToArray();
        Jelly = new int[playable.Length];
        Chains = new int[playable.Length];
    }

    public int Width { get; }

    public int Height { get; }

    public bool[] Playable { get; }

    /// <summary>Tile-layer glyphs (<see cref="TileGlyphs"/>); '.' is a random gem.</summary>
    public char[] Tiles { get; }

    public int[] Jelly { get; }

    public int[] Chains { get; }

    public int PlayableCount => Playable.Count(p => p);

    public int BoxCount => Tiles.Count(t => t is '1' or '2' or '3');

    public int ItemCount => Tiles.Count(t => t == 'I');

    public int JellyCellCount => Jelly.Count(j => j > 0);

    public int ChainCount => Chains.Count(c => c > 0);

    public int Index(int x, int y) => (y * Width) + x;

    public int MirrorX(int x) => Width - 1 - x;

    public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public bool IsPlayable(int x, int y) => IsInside(x, y) && Playable[Index(x, y)];

    /// <summary>Playable, holding a random gem, no chain: free for any placement.</summary>
    public bool IsFree(int x, int y) => IsPlayable(x, y) && Tiles[Index(x, y)] == Free && Chains[Index(x, y)] == 0;

    public bool IsBox(int x, int y) => IsInside(x, y) && Tiles[Index(x, y)] is '1' or '2' or '3';

    public void SetTileMirrored(int x, int y, char glyph)
    {
        Tiles[Index(x, y)] = glyph;
        Tiles[Index(MirrorX(x), y)] = glyph;
    }

    public void SetJellyMirrored(int x, int y, int layers)
    {
        Jelly[Index(x, y)] = layers;
        Jelly[Index(MirrorX(x), y)] = layers;
    }

    public void SetChainMirrored(int x, int y, int hp)
    {
        Chains[Index(x, y)] = hp;
        Chains[Index(MirrorX(x), y)] = hp;
    }

    public LayoutLayers ToLayers() => new()
    {
        Cells = Rows(i => Playable[i] ? TileGlyphs.Playable : TileGlyphs.Gap),
        Tiles = Rows(i => Playable[i] ? Tiles[i] : TileGlyphs.NoLayer),
        Jelly = Rows(i => TileGlyphs.CountGlyph(Playable[i] ? Jelly[i] : 0)),
        Chains = Rows(i => TileGlyphs.CountGlyph(Playable[i] ? Chains[i] : 0)),
    };

    private string[] Rows(Func<int, char> glyphAt)
    {
        var rows = new string[Height];
        var row = new StringBuilder(Width);
        for (int y = 0; y < Height; y++)
        {
            row.Clear();
            for (int x = 0; x < Width; x++)
            {
                row.Append(glyphAt(Index(x, y)));
            }

            rows[y] = row.ToString();
        }

        return rows;
    }
}
