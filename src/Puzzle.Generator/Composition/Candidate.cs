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

    /// <summary>Whether <paramref name="other"/> builds the same level at every scale: calibrating it would repeat this one's games.</summary>
    public bool IsSameLevelAs(Candidate other) =>
        _layers.Cells.SequenceEqual(other._layers.Cells)
        && SameLayer(_layers.Tiles, other._layers.Tiles)
        && SameLayer(_layers.Jelly, other._layers.Jelly)
        && SameLayer(_layers.Chains, other._layers.Chains)
        && Families.SequenceEqual(other.Families)
        && CollectColors.SequenceEqual(other.CollectColors)
        && (CollectBase, PresetItems, ItemSpawnBase) == (other.CollectBase, other.PresetItems, other.ItemSpawnBase);

    private static bool SameLayer(IReadOnlyList<string>? layer, IReadOnlyList<string>? other) =>
        layer is null || other is null ? layer is null && other is null : layer.SequenceEqual(other);

    /// <exception cref="LevelFormatException">The layout or goals are inconsistent (a generator bug).</exception>
    public LevelDefinition Build(LevelPlan plan, double scale) =>
        Build(new LevelId(plan.Number), plan.Tier, plan.Moves.Max, plan.Palette, scale);

    /// <summary>The level of this board at <paramref name="scale"/>, under a number, tier, move limit and palette that need no plan.</summary>
    /// <exception cref="LevelFormatException">The layout or goals are inconsistent (a generator bug).</exception>
    public LevelDefinition Build(LevelId id, DifficultyTier tier, int moveLimit, IReadOnlyList<GemColor> palette, double scale)
    {
        LevelLayout layout = LevelLayout.Parse(_layers);
        var goals = new List<GoalSpec>();
        int spawnedItems = AmountRules.SpawnedItems(ItemSpawnBase, scale);
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
            goals.Add(new GoalSpec.CollectColor(color, AmountRules.CollectTarget(CollectBase, scale)));
        }

        ItemSpawnRules items = Families.Contains(ObstacleFamily.Items) ? AmountRules.ItemRules(spawnedItems) : ItemSpawnRules.None;
        return new LevelDefinition(id, tier, layout, goals, moveLimit, new SpawnRules { Palette = palette, Items = items });
    }
}

/// <summary>
/// How the goals that measure an amount follow a scale. One place for the rules, so a level the calibrator stretched
/// and the same board given another amount later (a sibling in the level bank) are sized alike.
/// </summary>
internal static class AmountRules
{
    private const int ItemChancePercent = 7;

    /// <summary>A collect goal's target: the board's amount at scale 1 times the scale, never so few that the first cascade meets it.</summary>
    public static int CollectTarget(int collectBase, double scale) => Math.Max(6, (int)Math.Round(collectBase * scale));

    /// <summary>Items spawned during play: at least one, so a drop goal always has something falling in.</summary>
    public static int SpawnedItems(int itemSpawnBase, double scale) => Math.Clamp((int)Math.Round(itemSpawnBase * scale), 1, 20);

    /// <summary>How many items may lie on the board at once: a long item level shows three.</summary>
    public static int MaxItemsOnBoard(int spawnedItems) => spawnedItems > 6 ? 3 : 2;

    public static ItemSpawnRules ItemRules(int spawnedItems) =>
        new() { Total = spawnedItems, MaxOnBoard = MaxItemsOnBoard(spawnedItems), ChancePercent = ItemChancePercent };
}
