using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Bank.Candidates;

/// <summary>A level cannot be a bank candidate as it is; the message says what stands in the way.</summary>
public sealed class CandidateFormatException(string message) : Exception(message);

/// <summary>
/// What a bank candidate is: the content of a level without what an assembly decides later. It has no number, no
/// tier, no move limit, no seed pool and no colour names: its palette is the first N colours, and its collect goals
/// name palette slots through them. The games a level plays depend on none of the dropped parts, so every level that
/// shares this content shares the candidate's measurement.
/// </summary>
public sealed class CandidateContent
{
    /// <summary>The number a candidate's level carries; a candidate belongs to no level.</summary>
    private static readonly LevelId NoNumber = new(1);

    private CandidateContent(LevelDefinition level)
        : this(level, new CandidateId(ContentHash.Of(CanonicalText.OfCandidate(level))), new BoardId(ContentHash.Of(CanonicalText.OfBoard(level))))
    {
    }

    private CandidateContent(LevelDefinition level, CandidateId id, BoardId board)
    {
        Level = level;
        Id = id;
        Board = board;
        Features = CandidateFeatures.Of(level);
    }

    /// <summary>
    /// The content as a playable level: number 1, Easy, the first N colours, no seed pool, and the largest move limit
    /// a level may have, because a candidate has none (its games are followed to a move cap instead).
    /// </summary>
    public LevelDefinition Level { get; }

    public CandidateId Id { get; }

    public BoardId Board { get; }

    public CandidateFeatures Features { get; }

    /// <summary>
    /// The candidate behind <paramref name="level"/>: the level stripped of number, tier, limit and pool, its colours
    /// moved slot by slot onto the first N. Two levels give the same candidate exactly when they play the same games.
    /// </summary>
    /// <exception cref="CandidateFormatException">
    /// The palette is not in ascending colour order, or the level keeps one start board for every attempt. A colour's
    /// place among the colours decides ties in the engine (the colour a bomb picks), so only an order-preserving
    /// recolouring plays the same games; and a candidate's seeds each draw their own start board.
    /// </exception>
    public static CandidateContent Of(LevelDefinition level)
    {
        if (level.Attempts.Board is not StartBoard.Varied)
        {
            throw new CandidateFormatException($"Level {level.Id} keeps a fixed start board; a candidate's seeds each draw their own.");
        }

        IReadOnlyList<GemColor> palette = level.Spawn.Palette;
        if (!palette.SequenceEqual(palette.Order()))
        {
            throw new CandidateFormatException($"Level {level.Id} lists its palette out of colour order ({string.Join(", ", palette)}); recolouring it would change its games.");
        }

        var slotOf = palette.Select((color, slot) => (color, slot)).ToDictionary(pair => pair.color, pair => GemColors.FromIndex(pair.slot));
        return new CandidateContent(Recoloured(level, slotOf, GemColors.First(palette.Count), NoNumber, DifficultyTier.Easy, LevelDefinition.MaxMoveLimit, AttemptPolicy.Default));
    }

    /// <summary>
    /// The level an assembly makes of this candidate: its content under a number, a tier, a move limit and a seed
    /// pool, its palette slots given the colours of <paramref name="palette"/> in order. The way back from
    /// <see cref="Of"/>: the candidate behind that level is this one again.
    /// </summary>
    /// <exception cref="ArgumentException">The palette has another number of colours than the candidate, or is not in ascending colour order.</exception>
    public LevelDefinition ToLevel(LevelId id, DifficultyTier tier, int moveLimit, IReadOnlyList<GemColor> palette, IReadOnlyList<uint> seedPool)
    {
        if (palette.Count != Level.Spawn.Colors || !palette.SequenceEqual(palette.Order()))
        {
            throw new ArgumentException($"A candidate of {Level.Spawn.Colors} colours takes as many, in colour order; got {string.Join(", ", palette)}.", nameof(palette));
        }

        var colorOf = palette.Select((color, slot) => (color, slot)).ToDictionary(pair => GemColors.FromIndex(pair.slot), pair => pair.color);
        return Recoloured(Level, colorOf, palette, id, tier, moveLimit, new AttemptPolicy { SeedPool = seedPool.ToArray() });
    }

    /// <summary>
    /// A candidate read back from the bank, under the ids it was stored with. An id is given once: should the
    /// canonical text ever change, a stored candidate keeps its id and its games.
    /// </summary>
    /// <param name="level">The candidate's level as <see cref="Level"/> gave it when it was stored.</param>
    internal static CandidateContent FromStored(LevelDefinition level, CandidateId id, BoardId board) => new(level, id, board);

    /// <summary>The same candidate with other goals and item spawn rules: a sibling on the same board (<see cref="AmountGrid"/>).</summary>
    internal CandidateContent With(IReadOnlyList<GoalSpec> goals, ItemSpawnRules items)
    {
        var spawn = new SpawnRules
        {
            Palette = Level.Spawn.Palette,
            SmoothingPermille = Level.Spawn.SmoothingPermille,
            GoalBiasPercent = Level.Spawn.GoalBiasPercent,
            CascadeBiasPercent = Level.Spawn.CascadeBiasPercent,
            Items = items,
        };
        return new CandidateContent(new LevelDefinition(NoNumber, DifficultyTier.Easy, Level.Layout, goals, LevelDefinition.MaxMoveLimit, spawn));
    }

    /// <summary>The same content with every colour replaced through <paramref name="colorOf"/>, under the given palette, number, tier, limit and attempts.</summary>
    private static LevelDefinition Recoloured(
        LevelDefinition level,
        Dictionary<GemColor, GemColor> colorOf,
        IReadOnlyList<GemColor> palette,
        LevelId id,
        DifficultyTier tier,
        int moveLimit,
        AttemptPolicy attempts)
    {
        var spawn = new SpawnRules
        {
            Palette = palette,
            SmoothingPermille = level.Spawn.SmoothingPermille,
            GoalBiasPercent = level.Spawn.GoalBiasPercent,
            CascadeBiasPercent = level.Spawn.CascadeBiasPercent,
            Items = level.Spawn.Items,
        };
        GoalSpec[] goals = level.Goals.Select(goal => Recoloured(goal, colorOf)).ToArray();
        LevelLayout layout = LevelLayout.Parse(Recoloured(level.Layout.ToLayers(), colorOf));
        return new LevelDefinition(id, tier, layout, goals, moveLimit, spawn, attempts);
    }

    private static GoalSpec Recoloured(GoalSpec goal, Dictionary<GemColor, GemColor> slotOf) => goal is GoalSpec.CollectColor collect
        ? new GoalSpec.CollectColor(slotOf[collect.Color], collect.Target)
        : goal;

    /// <summary>Preset gems are the only layer glyphs that name a colour.</summary>
    private static LayoutLayers Recoloured(LayoutLayers layers, Dictionary<GemColor, GemColor> slotOf)
    {
        var glyphOf = slotOf.ToDictionary(pair => TileGlyphs.GemGlyph(pair.Key), pair => TileGlyphs.GemGlyph(pair.Value));
        return new LayoutLayers
        {
            Cells = layers.Cells,
            Tiles = layers.Tiles?.Select(row => string.Concat(row.Select(glyph => glyphOf.GetValueOrDefault(glyph, glyph)))).ToArray(),
            Jelly = layers.Jelly,
            Chains = layers.Chains,
        };
    }
}
