using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Generator.Composition;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Candidates;

/// <summary>An obstacle family on a board to draw, with the strongest form it may take (box hit points, jelly layers, chain hit points; 1 for items).</summary>
public readonly record struct FamilyDraw(ObstacleKind Kind, int Strength);

/// <summary>
/// What kind of board to draw. It names everything a level plan would otherwise decide by number, tier or dice:
/// which families (none for a board of collect goals alone), how strong, how many collect goals, how much of the
/// obstacles. No difficulty setting is in it, so the bank's boards depend on no policy.
/// </summary>
public sealed record DrawSpec
{
    public required BoardSize Size { get; init; }

    /// <summary>A plain rectangle; otherwise the outline is drawn from every shape style.</summary>
    public required bool PlainShape { get; init; }

    public required int Colors { get; init; }

    /// <summary>In the order of their goals.</summary>
    public required IReadOnlyList<FamilyDraw> Families { get; init; }

    public required int CollectGoals { get; init; }

    /// <summary>
    /// The obstacle amount as a step of the amount grid: 1.25^step times what the generator plans for a Normal level.
    /// What a plan's tier used to add (0.7 for Easy to 1.5 for Super Hard) is a step here like any other.
    /// </summary>
    public required int DensityStep { get; init; }

    /// <summary>Jelly may cover the whole board.</summary>
    public required bool FullJelly { get; init; }
}

/// <summary>
/// Draws bank candidates with the generator's own shapes, obstacle patterns and goal rules
/// (<see cref="CandidateFactory"/>): the one place where the bank's draw specification meets the generator.
/// </summary>
public static class BoardDrawer
{
    /// <summary>
    /// Raised whenever the same specification and seed would draw another board than before (a new shape style, a
    /// changed pattern). Stored with every drawn candidate; <c>BoardDrawerTests</c> pins a few drawn boards, so a
    /// change of the drawing code is noticed and this number raised with it.
    /// </summary>
    public const int DrawCodeVersion = 1;

    /// <summary>
    /// The level length the amounts at step 0 are sized for. A fixed number, so a board's base amounts depend on no
    /// policy's move range; the amount grid moves them from there.
    /// </summary>
    public const int ReferenceMoves = 24;

    /// <summary>The candidate <paramref name="spec"/> and <paramref name="seed"/> draw, at step 0 of its amounts; absent when the dice gave no usable board.</summary>
    /// <exception cref="LevelFormatException">The drawn layout and goals contradict each other (a fault of the drawing code).</exception>
    public static CandidateContent? TryDraw(DrawSpec spec, ulong seed)
    {
        IReadOnlyList<GemColor> palette = GemColors.First(spec.Colors);
        var draw = new BoardDraw
        {
            Size = spec.Size,
            PlainShape = spec.PlainShape,
            Palette = palette,
            Families = spec.Families.Select(family => new FamilyStrength(FamilyOf(family.Kind), family.Strength)).ToArray(),
            CollectGoals = spec.CollectGoals,
            ObstacleAmount = Math.Pow(AmountGrid.Ratio, spec.DensityStep),
            FullJellyAllowed = spec.FullJelly,
            ReferenceMoves = ReferenceMoves,
            Seed = seed,
        };
        return CandidateFactory.TryCreate(draw) is Candidate candidate
            ? CandidateContent.Of(candidate.Build(new LevelId(1), DifficultyTier.Easy, LevelDefinition.MaxMoveLimit, palette, scale: 1))
            : null;
    }

    private static ObstacleFamily FamilyOf(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => ObstacleFamily.Boxes,
        ObstacleKind.Jelly => ObstacleFamily.Jelly,
        ObstacleKind.Chains => ObstacleFamily.Chains,
        ObstacleKind.Items => ObstacleFamily.Items,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
