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

    /// <param name="density">Obstacle amount multiplier; the generator lowers it after too-long candidates and raises it after too-short ones.</param>
    /// <returns>A candidate, or absent when this attempt could not draw a usable board.</returns>
    public static Candidate? TryCreate(LevelPlan plan, int attempt, double density)
    {
        var rng = new Rng(Rng.Derive(plan.Seed, CandidateStream + (ulong)attempt));
        if (DrawShape(plan, rng) is not (bool[] mask, ShapeStyle shape))
        {
            return null;
        }

        var draft = new LayoutDraft(plan.Size, mask);
        List<ObstacleFamily> families = ChooseFamilies(plan, rng);
        var style = new List<string> { shape.ToString() };
        int presetItems = 0;
        foreach (ObstacleFamily family in families)
        {
            if (!Place(draft, family, plan, density, rng, style, ref presetItems))
            {
                return null;
            }
        }

        GemColor[] collectColors = ChooseCollectColors(plan, families, rng);
        double share = families.Count == 0 ? 1.0 : 0.55;
        int perGoal = (int)Math.Round(plan.Moves.Mid * (16.0 / plan.Palette.Count) * share / Math.Max(1, collectColors.Length));
        int itemSpawnBase = 2 + (plan.Moves.Mid / 8);
        if (collectColors.Length > 0)
        {
            style.Add($"Collect{collectColors.Length}");
        }

        return new Candidate(draft, families, collectColors, perGoal, presetItems, itemSpawnBase, string.Join('+', style));
    }

    private static (bool[] Mask, ShapeStyle Style)? DrawShape(LevelPlan plan, Rng rng)
    {
        // Early levels keep the plain rectangle; shapes grow more varied with difficulty.
        ShapeStyle[] styles = plan.Number <= 10
            ? [ShapeStyle.Full]
            : Enum.GetValues<ShapeStyle>();
        for (int i = 0; i < MaxShapeTries; i++)
        {
            ShapeStyle style = styles[rng.NextInt(styles.Length)];
            bool[] mask = ShapeBuilder.Build(style, plan.Size, rng);
            if (ShapeBuilder.IsUsable(mask, plan.Size))
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

    private static bool Place(LayoutDraft draft, ObstacleFamily family, LevelPlan plan, double density, Rng rng, List<string> style, ref int presetItems)
    {
        int playable = draft.PlayableCount;
        double tierWeight = plan.Tier switch
        {
            DifficultyTier.Easy => 0.7,
            DifficultyTier.Normal => 1.0,
            DifficultyTier.Hard => 1.25,
            DifficultyTier.SuperHard => 1.5,
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Tier, null),
        };
        switch (family)
        {
            case ObstacleFamily.Boxes:
            {
                var pattern = (BoxPattern)rng.NextInt(Enum.GetValues<BoxPattern>().Length);
                int target = (int)Math.Round(playable * 0.09 * tierWeight * density);
                style.Add($"Box{pattern}");
                return ObstaclePainter.PlaceBoxes(draft, pattern, Math.Max(2, target), MaxLevel(plan.Unlocked, family), rng) >= 2;
            }

            case ObstacleFamily.Jelly:
            {
                JellyPattern[] patterns = plan.Tier >= DifficultyTier.Hard
                    ? Enum.GetValues<JellyPattern>()
                    : Enum.GetValues<JellyPattern>().Where(p => p != JellyPattern.Full).ToArray();
                JellyPattern pattern = patterns[rng.NextInt(patterns.Length)];
                ObstaclePainter.PlaceJelly(draft, pattern, Math.Clamp(density * (0.8 + (rng.NextDouble() * 0.4)), 0.5, 1.5), MaxLevel(plan.Unlocked, family), rng);
                style.Add($"Jelly{pattern}");
                return draft.JellyCellCount >= 4;
            }

            case ObstacleFamily.Chains:
            {
                int target = (int)Math.Round(playable * 0.07 * tierWeight * density);
                style.Add("Chains");
                return ObstaclePainter.PlaceChains(draft, Math.Max(2, target), MaxLevel(plan.Unlocked, family), rng) >= 2;
            }

            case ObstacleFamily.Items:
            {
                presetItems = ObstaclePainter.PlaceItems(draft, 1 + rng.NextInt(3), rng);
                style.Add("Items");
                return presetItems >= 1;
            }

            default:
                throw new InvalidOperationException($"Unhandled obstacle family {family}.");
        }
    }

    /// <summary>Collect goals: always when there is no obstacle, sometimes on top of one (max three goals).</summary>
    private static GemColor[] ChooseCollectColors(LevelPlan plan, List<ObstacleFamily> families, Rng rng)
    {
        int count = families.Count == 0
            ? (rng.NextInt(100) < 60 ? 1 : 2)
            : (families.Count < LevelDefinition.MaxGoals && rng.NextInt(100) < 30 ? 1 : 0);
        GemColor[] colors = plan.Palette.ToArray();
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
