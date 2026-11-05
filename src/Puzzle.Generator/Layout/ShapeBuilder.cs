using Puzzle.Core.Randomness;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator.Layout;

/// <summary>Board outline families. Every style is mirrored left-right.</summary>
internal enum ShapeStyle
{
    Full,
    CornerCut,
    Diamond,
    Notches,
    CenterHole,
    ColumnGap,
    Hourglass,
}

/// <summary>
/// Draws symmetric board masks and rejects unusable ones: too few cells, disconnected regions, or cells that
/// could never take part in a match.
/// </summary>
internal static class ShapeBuilder
{
    public const double MinPlayableShare = 0.70;

    public static bool[] Build(ShapeStyle style, BoardSize size, Rng rng)
    {
        var mask = Enumerable.Repeat(true, size.Width * size.Height).ToArray();
        int w = size.Width;
        int h = size.Height;
        void Cut(int x, int y)
        {
            mask[(y * w) + x] = false;
            mask[(y * w) + (w - 1 - x)] = false;
        }

        switch (style)
        {
            case ShapeStyle.Full:
                break;
            case ShapeStyle.CornerCut:
            {
                int k = 1 + rng.NextInt(Math.Max(1, Math.Min(3, w / 4)));
                bool bottomToo = rng.NextInt(2) == 0;
                for (int y = 0; y < k; y++)
                {
                    for (int x = 0; x < k - y; x++)
                    {
                        Cut(x, y);
                        if (bottomToo)
                        {
                            Cut(x, h - 1 - y);
                        }
                    }
                }

                break;
            }

            case ShapeStyle.Diamond:
            {
                int k = Math.Max(2, Math.Min(w, h) / 3);
                for (int y = 0; y < k; y++)
                {
                    for (int x = 0; x < k - y; x++)
                    {
                        Cut(x, y);
                        Cut(x, h - 1 - y);
                    }
                }

                break;
            }

            case ShapeStyle.Notches:
            {
                int notches = 1 + rng.NextInt(2);
                for (int n = 0; n < notches; n++)
                {
                    int top = 1 + rng.NextInt(Math.Max(1, h - 4));
                    int span = 2 + rng.NextInt(2);
                    for (int y = top; y < Math.Min(h - 1, top + span); y++)
                    {
                        Cut(0, y);
                    }
                }

                break;
            }

            case ShapeStyle.CenterHole:
            {
                int holeHeight = 2 + rng.NextInt(2);
                int top = (h - holeHeight) / 2;
                // Even widths get a 2-wide hole; odd widths a 1- or 3-wide one (Cut mirrors each column).
                int halfWidth = w % 2 == 0 ? 1 : rng.NextInt(2);
                for (int y = top; y < top + holeHeight; y++)
                {
                    for (int x = (w / 2) - halfWidth; x <= (w - 1) / 2; x++)
                    {
                        Cut(x, y);
                    }
                }

                break;
            }

            case ShapeStyle.ColumnGap:
            {
                int top = 2 + rng.NextInt(Math.Max(1, h / 4));
                int bottom = h - 2 - rng.NextInt(Math.Max(1, h / 4));
                for (int y = top; y < bottom; y++)
                {
                    Cut((w - 1) / 2, y);
                }

                break;
            }

            case ShapeStyle.Hourglass:
            {
                int middle = h / 2;
                int reach = Math.Max(1, h / 5);
                for (int y = middle - reach; y <= middle + reach; y++)
                {
                    Cut(0, y);
                    if (Math.Abs(y - middle) < reach && w >= 9)
                    {
                        Cut(1, y);
                    }
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(style), style, null);
        }

        return mask;
    }

    /// <summary>Enough cells, one connected region, and every cell can be part of a line of three or a square.</summary>
    public static bool IsUsable(bool[] mask, BoardSize size)
    {
        int playable = mask.Count(p => p);
        if (playable < MinPlayableShare * mask.Length)
        {
            return false;
        }

        return IsConnected(mask, size) && EveryCellCanMatch(mask, size);
    }

    private static bool IsConnected(bool[] mask, BoardSize size)
    {
        int start = Array.IndexOf(mask, true);
        var seen = new bool[mask.Length];
        var stack = new Stack<int>([start]);
        seen[start] = true;
        int count = 1;
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % size.Width;
            int y = i / size.Width;
            foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
            {
                if (nx < 0 || ny < 0 || nx >= size.Width || ny >= size.Height)
                {
                    continue;
                }

                int n = (ny * size.Width) + nx;
                if (mask[n] && !seen[n])
                {
                    seen[n] = true;
                    count++;
                    stack.Push(n);
                }
            }
        }

        return count == mask.Count(p => p);
    }

    private static bool EveryCellCanMatch(bool[] mask, BoardSize size)
    {
        bool At(int x, int y) => x >= 0 && y >= 0 && x < size.Width && y < size.Height && mask[(y * size.Width) + x];
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                if (!At(x, y))
                {
                    continue;
                }

                bool inLine = (At(x - 2, y) && At(x - 1, y)) || (At(x - 1, y) && At(x + 1, y)) || (At(x + 1, y) && At(x + 2, y))
                    || (At(x, y - 2) && At(x, y - 1)) || (At(x, y - 1) && At(x, y + 1)) || (At(x, y + 1) && At(x, y + 2));
                bool inSquare = (At(x - 1, y) && At(x, y - 1) && At(x - 1, y - 1)) || (At(x + 1, y) && At(x, y - 1) && At(x + 1, y - 1))
                    || (At(x - 1, y) && At(x, y + 1) && At(x - 1, y + 1)) || (At(x + 1, y) && At(x, y + 1) && At(x + 1, y + 1));
                if (!inLine && !inSquare)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
