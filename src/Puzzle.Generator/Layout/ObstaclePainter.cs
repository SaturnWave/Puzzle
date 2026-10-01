using Puzzle.Core.Randomness;

namespace Puzzle.Generator.Layout;

internal enum BoxPattern
{
    Pairs,
    Pillars,
    Clusters,
    Scatter,
}

internal enum JellyPattern
{
    BottomBand,
    TopBand,
    CenterBlob,
    Frame,
    Checker,
    Full,
}

/// <summary>
/// Places obstacles symmetrically. Walls (boxes, chains) are only kept when every cell stays fed
/// (<see cref="Feedability"/>); row 0 stays clear so spawners always work.
/// </summary>
internal static class ObstaclePainter
{
    private const int MaxPlacementTries = 400;

    /// <returns>Boxes actually placed (mirrors included).</returns>
    public static int PlaceBoxes(LayoutDraft draft, BoxPattern pattern, int target, int maxHp, Rng rng)
    {
        int placed = 0;
        for (int attempt = 0; attempt < MaxPlacementTries && placed < target; attempt++)
        {
            List<(int X, int Y)> shape = BoxShape(draft, pattern, rng);
            if (shape.Count == 0 || !shape.All(c => c.Y >= 1 && draft.IsFree(c.X, c.Y) && draft.IsFree(draft.MirrorX(c.X), c.Y)))
            {
                continue;
            }

            char hp = (char)('0' + (maxHp == 1 ? 1 : 1 + rng.NextInt(maxHp)));
            foreach ((int x, int y) in shape)
            {
                draft.SetTileMirrored(x, y, hp);
            }

            if (Feedability.AllCellsFed(draft))
            {
                placed = draft.BoxCount;
                continue;
            }

            foreach ((int x, int y) in shape)
            {
                draft.SetTileMirrored(x, y, LayoutDraft.Free);
            }
        }

        return placed;
    }

    /// <summary>Jelly never goes under boxes; <paramref name="coverage"/> (≈0.6..1.4) scales the pattern's size.</summary>
    public static void PlaceJelly(LayoutDraft draft, JellyPattern pattern, double coverage, int maxLayers, Rng rng)
    {
        int w = draft.Width;
        int h = draft.Height;
        int band = Math.Clamp((int)Math.Round(h * 0.35 * coverage), 2, h);
        int radius = Math.Max(2, (int)Math.Round(Math.Min(w, h) * 0.35 * coverage));
        int frame = coverage > 1.1 ? 2 : 1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x <= (w - 1) / 2; x++)
            {
                if (!draft.IsPlayable(x, y) || draft.IsBox(x, y))
                {
                    continue;
                }

                bool covered = pattern switch
                {
                    JellyPattern.BottomBand => y >= h - band,
                    JellyPattern.TopBand => y < band,
                    JellyPattern.CenterBlob => Math.Abs(x - ((w - 1) / 2.0)) + Math.Abs(y - ((h - 1) / 2.0)) <= radius,
                    JellyPattern.Frame => DistanceToEdge(draft, x, y) < frame,
                    JellyPattern.Checker => (x + y) % 2 == 0 && y >= h / 4 && y < h - (h / 4),
                    JellyPattern.Full => true,
                    _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null),
                };
                if (covered)
                {
                    int layers = maxLayers > 1 && rng.NextInt(100) < 45 ? 2 : 1;
                    draft.SetJellyMirrored(x, y, layers);
                }
            }
        }
    }

    /// <returns>Chained cells actually placed (mirrors included).</returns>
    public static int PlaceChains(LayoutDraft draft, int target, int maxHp, Rng rng)
    {
        int placed = 0;
        for (int attempt = 0; attempt < MaxPlacementTries && placed < target; attempt++)
        {
            int x = rng.NextInt((draft.Width + 1) / 2);
            int y = 1 + rng.NextInt(draft.Height - 1);
            if (!draft.IsFree(x, y) || !draft.IsFree(draft.MirrorX(x), y))
            {
                continue;
            }

            draft.SetChainMirrored(x, y, maxHp == 1 ? 1 : 1 + rng.NextInt(maxHp));
            if (Feedability.AllCellsFed(draft))
            {
                placed = draft.ChainCount;
            }
            else
            {
                draft.SetChainMirrored(x, y, 0);
            }
        }

        return placed;
    }

    /// <summary>Items start in the top two rows, mirrored in pairs (or on the center column).</summary>
    public static int PlaceItems(LayoutDraft draft, int target, Rng rng)
    {
        for (int attempt = 0; attempt < MaxPlacementTries && draft.ItemCount < target; attempt++)
        {
            int x = rng.NextInt((draft.Width + 1) / 2);
            int y = rng.NextInt(2);
            bool pairFits = draft.ItemCount + (x == draft.MirrorX(x) ? 1 : 2) <= target;
            if (pairFits && draft.IsFree(x, y) && draft.IsFree(draft.MirrorX(x), y))
            {
                draft.SetTileMirrored(x, y, 'I');
            }
        }

        return draft.ItemCount;
    }

    private static List<(int X, int Y)> BoxShape(LayoutDraft draft, BoxPattern pattern, Rng rng)
    {
        int half = (draft.Width + 1) / 2;
        int x = rng.NextInt(half);
        int y = 1 + rng.NextInt(Math.Max(1, draft.Height - 1));
        return pattern switch
        {
            BoxPattern.Pairs when x + 1 < half => [(x, y), (x + 1, y)],
            BoxPattern.Pillars when y + 1 < draft.Height => rng.NextInt(2) == 0 || y + 2 >= draft.Height
                ? [(x, y), (x, y + 1)]
                : [(x, y), (x, y + 1), (x, y + 2)],
            BoxPattern.Clusters when x + 1 < half && y + 1 < draft.Height => [(x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)],
            BoxPattern.Scatter => [(x, y)],
            _ => [],
        };
    }

    /// <summary>Steps to the nearest gap or board edge in the four directions (0 = on the rim).</summary>
    private static int DistanceToEdge(LayoutDraft draft, int x, int y)
    {
        int Steps(int dx, int dy)
        {
            int d = 0;
            while (draft.IsPlayable(x + ((d + 1) * dx), y + ((d + 1) * dy)))
            {
                d++;
            }

            return d;
        }

        return Math.Min(Math.Min(Steps(-1, 0), Steps(1, 0)), Math.Min(Steps(0, -1), Steps(0, 1)));
    }
}
