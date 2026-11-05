using System.Globalization;
using Puzzle.Core.Levels;

namespace Puzzle.Bank.Candidates;

/// <summary>An obstacle family a board can carry; each brings a goal of its own. Closed set.</summary>
public enum ObstacleKind
{
    Boxes,
    Jelly,
    Chains,
    Items,
}

/// <summary>
/// What a level plan can ask of a candidate, read from the candidate's content alone: its size and outline, how many
/// colours it plays with, which obstacle families lie on it and how strong, and how many collect goals it has.
/// </summary>
public sealed record CandidateFeatures
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int PlayableCells { get; init; }

    public required int Colors { get; init; }

    /// <summary>Most hit points of a box on the board; 0 without boxes.</summary>
    public required int MaxBoxHp { get; init; }

    /// <summary>Most jelly layers on a cell; 0 without jelly.</summary>
    public required int MaxJellyLayers { get; init; }

    /// <summary>Most hit points of a chain; 0 without chains.</summary>
    public required int MaxChainHp { get; init; }

    /// <summary>Items lie on the start board, spawn during play, or are a goal.</summary>
    public required bool HasItems { get; init; }

    public required int CollectGoals { get; init; }

    /// <summary>A goal measures an amount (collect N, drop N), so the same board exists at other amounts.</summary>
    public required bool IsScalable { get; init; }

    /// <summary>The board is a plain rectangle: no cell is cut out.</summary>
    public bool IsPlain => PlayableCells == Width * Height;

    /// <summary>The families on the board, in the order of <see cref="ObstacleKind"/>.</summary>
    public IReadOnlyList<ObstacleKind> Families => Enum.GetValues<ObstacleKind>().Where(kind => StrengthOf(kind) > 0).ToArray();

    /// <summary>How strong a family is on this board: box hit points, jelly layers, chain hit points, 1 for items; 0 when it is absent.</summary>
    public int StrengthOf(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => MaxBoxHp,
        ObstacleKind.Jelly => MaxJellyLayers,
        ObstacleKind.Chains => MaxChainHp,
        ObstacleKind.Items => HasItems ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>A short name of the kind of candidate, for tables: <c>10x11/shaped/6c/B2+C1/collect1</c>.</summary>
    public string Kind => string.Create(
        CultureInfo.InvariantCulture,
        $"{Width}x{Height}/{(IsPlain ? "plain" : "shaped")}/{Colors}c/{FamilyLabel}/collect{CollectGoals}");

    /// <summary>The families with their strength, as in <see cref="Kind"/>: <c>B2+C1</c>, <c>J1+I</c>, or <c>none</c>.</summary>
    public string FamilyLabel => Families.Count == 0
        ? "none"
        : string.Join('+', Families.Select(kind => kind == ObstacleKind.Items ? "I" : string.Create(CultureInfo.InvariantCulture, $"{InitialOf(kind)}{StrengthOf(kind)}")));

    public static CandidateFeatures Of(LevelDefinition level)
    {
        LayoutLayers layers = level.Layout.ToLayers();
        bool dropsItems = level.Goals.Any(goal => goal is GoalSpec.DropItems);
        return new CandidateFeatures
        {
            Width = level.Layout.Shape.Width,
            Height = level.Layout.Shape.Height,
            PlayableCells = level.Layout.Shape.PlayableCount,
            Colors = level.Spawn.Colors,
            MaxBoxHp = LargestDigit(layers.Tiles),
            MaxJellyLayers = LargestDigit(layers.Jelly),
            MaxChainHp = LargestDigit(layers.Chains),
            HasItems = dropsItems || level.Layout.PresetItemCount > 0 || level.Spawn.Items.Total > 0,
            CollectGoals = level.Goals.Count(goal => goal is GoalSpec.CollectColor),
            IsScalable = dropsItems || level.Goals.Any(goal => goal is GoalSpec.CollectColor),
        };
    }

    /// <summary>In every layer a digit is a count: a box's hit points among the tiles, layers of jelly, a chain's hit points.</summary>
    private static int LargestDigit(IReadOnlyList<string>? rows) =>
        rows is null ? 0 : rows.SelectMany(row => row).Where(char.IsAsciiDigit).Select(glyph => glyph - '0').DefaultIfEmpty(0).Max();

    private static char InitialOf(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => 'B',
        ObstacleKind.Jelly => 'J',
        ObstacleKind.Chains => 'C',
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
