namespace Puzzle.Generator.Layout;

/// <summary>
/// Checks that falling tiles can reach every cell even if no box or chain ever breaks — the "sealed cell"
/// trap found in Phase 1 (cells under a row of boxes stay empty forever).
/// </summary>
internal static class Feedability
{
    /// <remarks>
    /// Boxes and chained tiles never fall, so they act as walls. A cell is fed when the nearest playable cell
    /// above it (gaps are fall-through) is fed, or when it is open to the spawner at the top, or when an upper
    /// diagonal neighbor is fed (tiles slide diagonally into sealed cells). Solved as a fixed point.
    /// </remarks>
    public static bool AllCellsFed(LayoutDraft draft)
    {
        var fed = new bool[draft.Playable.Length];
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int y = 0; y < draft.Height; y++)
            {
                for (int x = 0; x < draft.Width; x++)
                {
                    int i = draft.Index(x, y);
                    if (!draft.Playable[i] || IsWall(draft, x, y) || fed[i])
                    {
                        continue;
                    }

                    if (IsFedFromAbove(draft, fed, x, y) || IsFedSource(draft, fed, x - 1, y - 1) || IsFedSource(draft, fed, x + 1, y - 1))
                    {
                        fed[i] = true;
                        changed = true;
                    }
                }
            }
        }

        for (int y = 0; y < draft.Height; y++)
        {
            for (int x = 0; x < draft.Width; x++)
            {
                if (draft.IsPlayable(x, y) && !IsWall(draft, x, y) && !fed[draft.Index(x, y)])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsWall(LayoutDraft draft, int x, int y) =>
        draft.IsBox(x, y) || draft.Chains[draft.Index(x, y)] > 0;

    private static bool IsFedFromAbove(LayoutDraft draft, bool[] fed, int x, int y)
    {
        for (int up = y - 1; up >= 0; up--)
        {
            if (draft.IsPlayable(x, up))
            {
                return !IsWall(draft, x, up) && fed[draft.Index(x, up)];
            }
        }

        return true; // open to the column's spawner
    }

    private static bool IsFedSource(LayoutDraft draft, bool[] fed, int x, int y) =>
        draft.IsPlayable(x, y) && !IsWall(draft, x, y) && fed[draft.Index(x, y)];
}
