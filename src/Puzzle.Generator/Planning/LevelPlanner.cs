using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator.Planning;

/// <summary>What one level must be, before any board is drawn.</summary>
public sealed record LevelPlan
{
    public required int Number { get; init; }

    public required DifficultyTier Tier { get; init; }

    /// <summary>
    /// The player model that must reach <see cref="TargetWinRate"/>; the retry seed pool is balanced for it too.
    /// </summary>
    public required string CalibrationBot { get; init; }

    /// <summary>
    /// Win rate <see cref="CalibrationBot"/> should have at the calibrated move limit, before the profile's move margin
    /// adds moves.
    /// </summary>
    public required double TargetWinRate { get; init; }

    /// <summary>
    /// Least win rate the profile's floor bot must have at the calibrated move limit; absent when the tier has no
    /// floor. The calibrated limit is the larger of what the target and the floor need.
    /// </summary>
    public required double? CasualFloor { get; init; }

    /// <summary>The move margin the level's final limit carries on top of its calibrated one: its band's, or the profile's.</summary>
    public required MoveMargin MoveMargin { get; init; }

    /// <summary>The range the calibrated move limit should land in.</summary>
    public required MoveRange Moves { get; init; }

    public required BoardSize Size { get; init; }

    /// <summary>The board must be a plain rectangle (no cut-out shape).</summary>
    public required bool PlainShape { get; init; }

    public required IReadOnlyList<GemColor> Palette { get; init; }

    public required IReadOnlySet<Mechanic> Unlocked { get; init; }

    /// <summary>Mechanics unlocked within the last few levels: every candidate must feature them.</summary>
    public required IReadOnlySet<Mechanic> Featured { get; init; }

    public required int MaxObstacleTypes { get; init; }

    /// <summary>Root of all randomness for this level; equal profile and number give an equal level.</summary>
    public required ulong Seed { get; init; }
}

/// <summary>Turns a level number into a <see cref="LevelPlan"/> using the profile's curve, bands and schedules.</summary>
public sealed class LevelPlanner(GenerationProfile profile)
{
    private const ulong SizeStream = 1;
    private const ulong ChapterStream = 1000;

    public LevelPlan Plan(int number)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Levels start at 1.");
        }

        ulong seed = Rng.Derive(profile.MasterSeed, (ulong)number);
        DifficultyTier tier = number <= profile.TutorialUntil ? DifficultyTier.Easy : profile.Cycle[(number - 1) % profile.Cycle.Count];
        ProfileBand band = profile.BandFor(number);
        HashSet<Mechanic> unlocked = profile.Mechanics.Where(u => u.FromLevel <= number).Select(u => u.Mechanic).ToHashSet();
        HashSet<Mechanic> featured = profile.Mechanics
            .Where(u => u.FromLevel <= number && number < u.FromLevel + profile.HookLevels)
            .Select(u => u.Mechanic)
            .ToHashSet();

        return new LevelPlan
        {
            Number = number,
            Tier = tier,
            CalibrationBot = band.CalibrationBot,
            TargetWinRate = band.WinRate.For(tier),
            CasualFloor = band.CasualFloor.For(tier),
            MoveMargin = band.MarginUnder(profile.Calibration),
            Moves = band.Moves.For(tier),
            Size = ChooseSize(number, tier, seed),
            PlainShape = number <= profile.Sizes.PlainShapesUntil,
            Palette = ChoosePalette(number, band.Colors.For(tier)),
            Unlocked = unlocked,
            Featured = featured,
            MaxObstacleTypes = Math.Max(band.MaxObstacleTypes, featured.Count == 0 ? 0 : 1),
            Seed = seed,
        };
    }

    private BoardSize ChooseSize(int number, DifficultyTier tier, ulong seed)
    {
        if (number <= profile.Sizes.FixedUntil)
        {
            return profile.Sizes.Default;
        }

        IReadOnlyList<BoardSize> options = profile.Sizes.ByTier.For(tier);
        return options[new Rng(Rng.Derive(seed, SizeStream)).NextInt(options.Count)];
    }

    /// <summary>
    /// The first N colors until <see cref="PaletteRules.NewColorsFrom"/>; after that every chapter draws its own
    /// N colors from all eight, so new colors keep appearing as the player progresses.
    /// </summary>
    private IReadOnlyList<GemColor> ChoosePalette(int number, int colors)
    {
        if (number < profile.Palette.NewColorsFrom || colors == GemColors.Count)
        {
            return GemColors.First(colors);
        }

        int chapter = (number - 1) / profile.Palette.ChapterSize;
        var rng = new Rng(Rng.Derive(profile.MasterSeed, ChapterStream + (ulong)chapter));
        var all = Enumerable.Range(0, GemColors.Count).Select(GemColors.FromIndex).ToArray();
        for (int i = all.Length - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }

        return all.Take(colors).OrderBy(c => c).ToArray();
    }
}
