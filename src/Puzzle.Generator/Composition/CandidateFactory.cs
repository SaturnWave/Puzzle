using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Layout;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator.Composition;

/// <summary>
/// The generator portfolio: each attempt draws a different shape style, obstacle mix and pattern from the
/// plan's options, so the calibrator can choose among genuinely different boards.
/// </summary>
internal static class CandidateFactory
{
    private const int MaxShapeTries = 12;
    private const ulong CandidateStream = 100;

    /// <param name="attempt">
    /// Which board of the plan: its shape and its obstacle families follow from it alone. Everything drawn after the
    /// first obstacle is placed (where obstacles go, a second family's pattern, an added collect goal) can come out
    /// differently at another <paramref name="density"/>, because placing more or fewer obstacles moves the draws.
    /// </param>
    /// <param name="density">Obstacle amount multiplier; the generator searches it for each board, less after a too-long level and more after a too-short one.</param>
    /// <returns>A candidate, or absent when this attempt could not draw a usable board.</returns>
    public static Candidate? TryCreate(LevelPlan plan, int attempt, double density)
    {
        var rng = new Rng(Rng.Derive(plan.Seed, CandidateStream + (ulong)attempt));
        if (DrawShape(plan.Size, plan.PlainShape, rng) is not (bool[] mask, ShapeStyle shape))
        {
            return null;
        }

        var draft = new LayoutDraft(plan.Size, mask);
        FamilyStrength[] families = ChooseFamilies(plan, rng).Select(family => new FamilyStrength(family, MaxLevel(plan.Unlocked, family))).ToArray();
        var amount = new ObstacleAmount(TierWeightOf(plan.Tier), density);
        if (PlaceAll(draft, families, amount, plan.Tier >= DifficultyTier.Hard, rng, shape) is not Placement placement)
        {
            return null;
        }

        int collectGoals = RollCollectGoals(families.Length, rng);
        return Compose(draft, placement, PickCollectColors(plan.Palette, collectGoals, rng), plan.Moves.Mid, plan.Palette.Count);
    }

    /// <summary>
    /// Draws the board <paramref name="draw"/> describes: the same shapes, obstacle patterns and goal amounts as a
    /// plan's candidates, but with the families, their strength, the number of collect goals and the obstacle amount
    /// named by the caller instead of rolled, so a board can be drawn for a kind and for no level number.
    /// </summary>
    /// <returns>A candidate, or absent when the dice gave no usable board of that description.</returns>
    public static Candidate? TryCreate(BoardDraw draw)
    {
        var rng = new Rng(draw.Seed);
        if (DrawShape(draw.Size, draw.PlainShape, rng) is not (bool[] mask, ShapeStyle shape))
        {
            return null;
        }

        var draft = new LayoutDraft(draw.Size, mask);
        var amount = new ObstacleAmount(TierWeight: 1, draw.ObstacleAmount);
        if (PlaceAll(draft, draw.Families, amount, draw.FullJellyAllowed, rng, shape) is not Placement placement)
        {
            return null;
        }

        return Compose(draft, placement, PickCollectColors(draw.Palette, draw.CollectGoals, rng), draw.ReferenceMoves, draw.Palette.Count);
    }

    private static (bool[] Mask, ShapeStyle Style)? DrawShape(BoardSize size, bool plainShape, Rng rng)
    {
        ShapeStyle[] styles = plainShape
            ? [ShapeStyle.Full]
            : Enum.GetValues<ShapeStyle>();
        for (int i = 0; i < MaxShapeTries; i++)
        {
            ShapeStyle style = styles[rng.NextInt(styles.Length)];
            bool[] mask = ShapeBuilder.Build(style, size, rng);
            if (ShapeBuilder.IsUsable(mask, size))
            {
                return (mask, style);
            }
        }

        return null;
    }

    private static List<ObstacleFamily> ChooseFamilies(LevelPlan plan, Rng rng)
    {
        List<ObstacleFamily> available = Enum.GetValues<ObstacleFamily>().Where(f => MaxLevel(plan.Unlocked, f) > 0).ToList();
        var chosen = plan.Featured.Select(FamilyOf).Distinct().ToList();
        int max = Math.Min(plan.MaxObstacleTypes, available.Count);
        if (max == 0)
        {
            return chosen;
        }

        // Pure "collect" levels stay common; mixes get rarer as they get busier.
        int roll = rng.NextInt(100);
        int wanted = roll < 20 ? 0 : roll < 65 ? 1 : roll < 90 ? 2 : 3;
        wanted = Math.Clamp(wanted, chosen.Count, max);
        while (chosen.Count < wanted)
        {
            List<ObstacleFamily> remaining = available.Except(chosen).ToList();
            chosen.Add(remaining[rng.NextInt(remaining.Count)]);
        }

        return chosen;
    }

    /// <summary>Places every family in turn; absent when one of them found no room for its smallest form.</summary>
    private static Placement? PlaceAll(LayoutDraft draft, IReadOnlyList<FamilyStrength> families, ObstacleAmount amount, bool fullJellyAllowed, Rng rng, ShapeStyle shape)
    {
        var style = new List<string> { shape.ToString() };
        int presetItems = 0;
        foreach (FamilyStrength family in families)
        {
            if (!Place(draft, family, amount, fullJellyAllowed, rng, style, ref presetItems))
            {
                return null;
            }
        }

        return new Placement(families.Select(family => family.Family).ToArray(), presetItems, style);
    }

    private static bool Place(LayoutDraft draft, FamilyStrength family, ObstacleAmount amount, bool fullJellyAllowed, Rng rng, List<string> style, ref int presetItems)
    {
        int playable = draft.PlayableCount;
        switch (family.Family)
        {
            case ObstacleFamily.Boxes:
            {
                var pattern = (BoxPattern)rng.NextInt(Enum.GetValues<BoxPattern>().Length);
                int target = (int)Math.Round(playable * 0.09 * amount.TierWeight * amount.Density);
                style.Add($"Box{pattern}");
                return ObstaclePainter.PlaceBoxes(draft, pattern, Math.Max(2, target), family.MaxLevel, rng) >= 2;
            }

            case ObstacleFamily.Jelly:
            {
                JellyPattern[] patterns = fullJellyAllowed
                    ? Enum.GetValues<JellyPattern>()
                    : Enum.GetValues<JellyPattern>().Where(p => p != JellyPattern.Full).ToArray();
                JellyPattern pattern = patterns[rng.NextInt(patterns.Length)];
                ObstaclePainter.PlaceJelly(draft, pattern, Math.Clamp(amount.Density * (0.8 + (rng.NextDouble() * 0.4)), 0.5, 1.5), family.MaxLevel, rng);
                style.Add($"Jelly{pattern}");
                return draft.JellyCellCount >= 4;
            }

            case ObstacleFamily.Chains:
            {
                int target = (int)Math.Round(playable * 0.07 * amount.TierWeight * amount.Density);
                style.Add("Chains");
                return ObstaclePainter.PlaceChains(draft, Math.Max(2, target), family.MaxLevel, rng) >= 2;
            }

            case ObstacleFamily.Items:
            {
                presetItems = ObstaclePainter.PlaceItems(draft, 1 + rng.NextInt(3), rng);
                style.Add("Items");
                return presetItems >= 1;
            }

            default:
                throw new InvalidOperationException($"Unhandled obstacle family {family.Family}.");
        }
    }

    /// <summary>
    /// The goals' amounts at scale 1: collect goals share what a level of <paramref name="referenceMoves"/> moves
    /// clears of one colour (all of it without obstacles, a good half beside them), and items spawn at the rate that
    /// keeps a level of that length busy.
    /// </summary>
    private static Candidate Compose(LayoutDraft draft, Placement placement, GemColor[] collectColors, int referenceMoves, int paletteCount)
    {
        double share = placement.Families.Count == 0 ? 1.0 : 0.55;
        int perGoal = (int)Math.Round(referenceMoves * (16.0 / paletteCount) * share / Math.Max(1, collectColors.Length));
        int itemSpawnBase = 2 + (referenceMoves / 8);
        if (collectColors.Length > 0)
        {
            placement.Style.Add($"Collect{collectColors.Length}");
        }

        return new Candidate(draft, placement.Families, collectColors, perGoal, placement.PresetItems, itemSpawnBase, string.Join('+', placement.Style));
    }

    /// <summary>How much more a tier's boards are walled in than a Normal level's, before any search of the amount.</summary>
    private static double TierWeightOf(DifficultyTier tier) => tier switch
    {
        DifficultyTier.Easy => 0.7,
        DifficultyTier.Normal => 1.0,
        DifficultyTier.Hard => 1.25,
        DifficultyTier.SuperHard => 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };

    /// <summary>Collect goals: always when there is no obstacle, sometimes on top of one (max three goals).</summary>
    private static int RollCollectGoals(int families, Rng rng) => families == 0
        ? (rng.NextInt(100) < 60 ? 1 : 2)
        : (families < LevelDefinition.MaxGoals && rng.NextInt(100) < 30 ? 1 : 0);

    /// <summary>The first <paramref name="count"/> colours of the palette after a shuffle.</summary>
    private static GemColor[] PickCollectColors(IReadOnlyList<GemColor> palette, int count, Rng rng)
    {
        GemColor[] colors = palette.ToArray();
        for (int i = colors.Length - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (colors[i], colors[j]) = (colors[j], colors[i]);
        }

        return colors.Take(count).ToArray();
    }

    private static ObstacleFamily FamilyOf(Mechanic mechanic) => mechanic switch
    {
        Mechanic.Box1 or Mechanic.Box2 or Mechanic.Box3 => ObstacleFamily.Boxes,
        Mechanic.Jelly1 or Mechanic.Jelly2 => ObstacleFamily.Jelly,
        Mechanic.Chain1 or Mechanic.Chain2 => ObstacleFamily.Chains,
        Mechanic.Items => ObstacleFamily.Items,
        _ => throw new ArgumentOutOfRangeException(nameof(mechanic), mechanic, null),
    };

    /// <summary>Strongest unlocked level of a family: box hit points, jelly layers, chain hit points (0 = locked).</summary>
    private static int MaxLevel(IReadOnlySet<Mechanic> unlocked, ObstacleFamily family) => family switch
    {
        ObstacleFamily.Boxes => unlocked.Contains(Mechanic.Box3) ? 3 : unlocked.Contains(Mechanic.Box2) ? 2 : unlocked.Contains(Mechanic.Box1) ? 1 : 0,
        ObstacleFamily.Jelly => unlocked.Contains(Mechanic.Jelly2) ? 2 : unlocked.Contains(Mechanic.Jelly1) ? 1 : 0,
        ObstacleFamily.Chains => unlocked.Contains(Mechanic.Chain2) ? 2 : unlocked.Contains(Mechanic.Chain1) ? 1 : 0,
        ObstacleFamily.Items => unlocked.Contains(Mechanic.Items) ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };
}

/// <summary>An obstacle family with the strongest form it may take on a board: box hit points, jelly layers, chain hit points (1 for items).</summary>
internal readonly record struct FamilyStrength(ObstacleFamily Family, int MaxLevel);

/// <summary>
/// How many obstacles a board gets, as two multiples of the planned amount: boxes and chains follow both, jelly only
/// <paramref name="Density"/> (a plan's tier never widened its jelly; it opens the full-board pattern instead). They
/// stay two numbers because a plan's boards multiply them one after the other, and their product taken first would
/// round a box count differently now and then.
/// </summary>
internal readonly record struct ObstacleAmount(double TierWeight, double Density);

/// <summary>What was placed on a draft: the families in the order of their goals, the items lying on the start board, and the choices by name.</summary>
internal sealed record Placement(IReadOnlyList<ObstacleFamily> Families, int PresetItems, List<string> Style);

/// <summary>
/// A board to draw with nothing left open but the dice. No level number, tier or difficulty setting takes part: what
/// a plan's candidates take from those is named here outright.
/// </summary>
internal sealed record BoardDraw
{
    public required BoardSize Size { get; init; }

    public required bool PlainShape { get; init; }

    public required IReadOnlyList<GemColor> Palette { get; init; }

    /// <summary>The families on the board, in the order of their goals; empty for a board with collect goals alone.</summary>
    public required IReadOnlyList<FamilyStrength> Families { get; init; }

    /// <summary>Collect goals beside the families' own; with the families at most <see cref="LevelDefinition.MaxGoals"/>.</summary>
    public required int CollectGoals { get; init; }

    /// <summary>Multiple of the planned obstacle amount (1 = a Normal level's).</summary>
    public required double ObstacleAmount { get; init; }

    /// <summary>Whether jelly may cover the whole board (a plan allows it from Hard on).</summary>
    public required bool FullJellyAllowed { get; init; }

    /// <summary>The level length the goals' amounts at scale 1 are sized for.</summary>
    public required int ReferenceMoves { get; init; }

    public required ulong Seed { get; init; }
}
