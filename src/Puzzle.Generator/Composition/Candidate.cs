using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Generator.Layout;
using Puzzle.Generator.Planning;

namespace Puzzle.Generator.Composition;

/// <summary>Obstacle families a level can feature; each one brings its own goal.</summary>
internal enum ObstacleFamily
{
    Boxes,
    Jelly,
    Chains,
    Items,
}

/// <summary>
/// A drawn layout plus its goals. Goals that measure an amount (collect N gems, drop N items) scale with a
/// factor so the calibrator can stretch or shrink the level toward its move range; obstacle goals are fixed
/// by the layout.
/// </summary>
internal sealed class Candidate
{
    private readonly LayoutLayers _layers;

    public Candidate(LayoutDraft draft, IReadOnlyList<ObstacleFamily> families, IReadOnlyList<GemColor> collectColors, int collectBase, int presetItems, int itemSpawnBase, string style)
    {
        _layers = draft.ToLayers();
        Families = families;
        CollectColors = collectColors;
        CollectBase = collectBase;
        PresetItems = presetItems;
        ItemSpawnBase = itemSpawnBase;
        Style = style;
    }

    public IReadOnlyList<ObstacleFamily> Families { get; }

    public IReadOnlyList<GemColor> CollectColors { get; }

    /// <summary>Target of each collect goal at scale 1.</summary>
    public int CollectBase { get; }

    public int PresetItems { get; }

    /// <summary>Items spawned during play at scale 1 (the drop goal adds the preset ones).</summary>
    public int ItemSpawnBase { get; }

    /// <summary>Human-readable generator choices, for the report.</summary>
    public string Style { get; }

    public bool IsScalable => CollectColors.Count > 0 || Families.Contains(ObstacleFamily.Items);

    /// <exception cref="LevelFormatException">The layout or goals are inconsistent (a generator bug).</exception>
    public LevelDefinition Build(LevelPlan plan, double scale)
    {
        LevelLayout layout = LevelLayout.Parse(_layers);
        var goals = new List<GoalSpec>();
        int spawnedItems = Math.Clamp((int)Math.Round(ItemSpawnBase * scale), 1, 20);
        foreach (ObstacleFamily family in Families)
        {
            switch (family)
            {
                case ObstacleFamily.Boxes:
                    goals.Add(new GoalSpec.BreakBoxes(layout.BoxCount));
                    break;
                case ObstacleFamily.Jelly:
                    goals.Add(new GoalSpec.ClearJelly(layout.JellyCellCount));
                    break;
                case ObstacleFamily.Chains:
                    goals.Add(new GoalSpec.BreakChains(layout.ChainCount));
                    break;
                case ObstacleFamily.Items:
                    goals.Add(new GoalSpec.DropItems(PresetItems + spawnedItems));
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled obstacle family {family}.");
            }
        }

        foreach (GemColor color in CollectColors)
        {
            goals.Add(new GoalSpec.CollectColor(color, Math.Max(6, (int)Math.Round(CollectBase * scale))));
        }

        ItemSpawnRules items = Families.Contains(ObstacleFamily.Items)
            ? new ItemSpawnRules { Total = spawnedItems, MaxOnBoard = spawnedItems > 6 ? 3 : 2, ChancePercent = 7 }
            : ItemSpawnRules.None;
        return new LevelDefinition(
            new LevelId(plan.Number),
            plan.Tier,
            layout,
            goals,
            plan.Moves.Max,
            new SpawnRules { Palette = plan.Palette, Items = items });
    }
}
