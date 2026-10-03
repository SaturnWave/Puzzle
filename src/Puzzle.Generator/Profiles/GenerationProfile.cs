using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;

namespace Puzzle.Generator.Profiles;

/// <summary>An obstacle feature the level plan can unlock. Closed set; parsed by name from the profile.</summary>
public enum Mechanic
{
    Box1,
    Box2,
    Box3,
    Jelly1,
    Jelly2,
    Chain1,
    Chain2,
    Items,
}

public readonly record struct BoardSize(int Width, int Height)
{
    /// <summary>Parses "10x12" (width x height).</summary>
    public static bool TryParse(string text, out BoardSize size)
    {
        size = default;
        string[] parts = text.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
        {
            return false;
        }

        size = new BoardSize(width, height);
        return width is >= 5 and <= 16 && height is >= 5 and <= 16;
    }

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>Preferred move budget of a level; calibration aims for its middle.</summary>
public readonly record struct MoveRange(int Min, int Max)
{
    public int Mid => (Min + Max) / 2;

    public bool Contains(int moves) => moves >= Min && moves <= Max;

    /// <summary>How many moves outside the range a limit is (0 inside).</summary>
    public int DistanceTo(int moves) => moves < Min ? Min - moves : moves > Max ? moves - Max : 0;

    public override string ToString() => $"{Min}-{Max}";
}

/// <summary>One value per difficulty tier.</summary>
public sealed record TierValues<T>(T Easy, T Normal, T Hard, T SuperHard)
{
    public T For(DifficultyTier tier) => tier switch
    {
        DifficultyTier.Easy => Easy,
        DifficultyTier.Normal => Normal,
        DifficultyTier.Hard => Hard,
        DifficultyTier.SuperHard => SuperHard,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };
}

/// <summary>The rules in force from <see cref="FromLevel"/> on, fully resolved (inheritance already applied).</summary>
/// <param name="CasualFloor">
/// Least win rate the profile's <see cref="GenerationProfile.FloorBot"/> must have at the move limit, per tier; absent
/// for a tier without a floor. Never above that tier's <paramref name="WinRate"/>.
/// </param>
public sealed record ProfileBand(
    int FromLevel,
    TierValues<double> WinRate,
    TierValues<double?> CasualFloor,
    TierValues<MoveRange> Moves,
    TierValues<int> Colors,
    int MaxObstacleTypes);

/// <param name="FixedUntil">Levels up to this number use <paramref name="Default"/>.</param>
/// <param name="PlainShapesUntil">Levels up to this number are plain rectangles, so early levels bring one new thing at a time.</param>
public sealed record SizeRules(BoardSize Default, int FixedUntil, int PlainShapesUntil, TierValues<IReadOnlyList<BoardSize>> ByTier);

/// <summary>From <see cref="NewColorsFrom"/> on, each chapter of <see cref="ChapterSize"/> levels gets its own palette drawn from all colors.</summary>
public sealed record PaletteRules(int NewColorsFrom, int ChapterSize);

public sealed record MechanicUnlock(int FromLevel, Mechanic Mechanic);

public sealed record CalibrationSettings
{
    /// <summary>Uncapped bot games used to place the move limit.</summary>
    public required int Runs { get; init; }

    /// <summary>Fresh-seed games that check the chosen limit.</summary>
    public required int VerifyRuns { get; init; }

    /// <summary>Games per extra bot profile reported (not used for the limit).</summary>
    public required int ReportRuns { get; init; }

    public required int MoveCap { get; init; }

    /// <summary>Accepted gap between a verified win rate and the one required: the target, or the casual floor (see <c>LimitVerification</c>).</summary>
    public required double WinRateTolerance { get; init; }

    public required int MaxCandidates { get; init; }

    public required int MaxScaleIterations { get; init; }
}

/// <summary>Everything that shapes a generated level set. Loaded from profiles/*.json by <see cref="ProfileLoader"/>.</summary>
public sealed record GenerationProfile
{
    public required string Name { get; init; }

    public required ulong MasterSeed { get; init; }

    /// <summary>The bot whose win rate the move limits are calibrated against.</summary>
    public required string CalibrationBot { get; init; }

    /// <summary>
    /// The player model behind the casual guard: on a tier with a <see cref="ProfileBand.CasualFloor"/>, this bot must
    /// reach the floor at the move limit too, and the limit grows when it would not.
    /// </summary>
    public required string FloorBot { get; init; }

    /// <summary>Extra bots whose win rate at the chosen limit is reported (e.g. casual, expert).</summary>
    public required IReadOnlyList<string> ReportBots { get; init; }

    /// <summary>Levels up to this number are always Easy.</summary>
    public required int TutorialUntil { get; init; }

    /// <summary>Repeating difficulty pattern (the saw-tooth curve).</summary>
    public required IReadOnlyList<DifficultyTier> Cycle { get; init; }

    /// <summary>Sorted by <see cref="ProfileBand.FromLevel"/>; the first starts at level 1.</summary>
    public required IReadOnlyList<ProfileBand> Bands { get; init; }

    public required SizeRules Sizes { get; init; }

    public required PaletteRules Palette { get; init; }

    public required IReadOnlyList<MechanicUnlock> Mechanics { get; init; }

    /// <summary>A newly unlocked mechanic is featured in this many consecutive levels.</summary>
    public required int HookLevels { get; init; }

    /// <summary>A candidate identical in shape and goals to one of this many previous levels is rejected.</summary>
    public required int NoveltyWindow { get; init; }

    public required CalibrationSettings Calibration { get; init; }

    public required SeedPoolSettings SeedPool { get; init; }

    /// <summary>Retries must not run out of patterns: pools smaller than this are widened (see <c>LevelGenerator</c>).</summary>
    public required int MinPoolSize { get; init; }

    public ProfileBand BandFor(int level) => Bands.Last(band => band.FromLevel <= level);
}
