using System.Text.Json;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;

namespace Puzzle.Generator.Profiles;

/// <summary>A generation profile is malformed; the message names the field.</summary>
public sealed class ProfileFormatException : Exception
{
    public ProfileFormatException(string message)
        : base(message)
    {
    }

    public ProfileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Reads profiles/*.json. Bands inherit every field they omit from the band before them.</summary>
public static class ProfileLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <exception cref="ProfileFormatException">The file is missing, not JSON, or inconsistent.</exception>
    public static GenerationProfile Load(string path)
    {
        ProfileFile file;
        try
        {
            file = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path), Options)
                ?? throw new ProfileFormatException($"'{path}' is empty.");
        }
        catch (IOException ex)
        {
            throw new ProfileFormatException($"Cannot read profile '{path}': {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new ProfileFormatException($"Profile '{path}' is not valid JSON: {ex.Message}", ex);
        }

        return Build(file);
    }

    private static GenerationProfile Build(ProfileFile file)
    {
        string calibrationBot = file.CalibrationBot ?? "average";
        string knownBots = $"{string.Join(", ", BotPolicies.Names.Concat(BotPolicies.MeasurementNames))} or usta-<n>";
        List<string?> listedReportBots = file.ReportBots ?? [];
        Require(!listedReportBots.Contains(null), $"reportBots has a null entry; every entry must name a bot ({knownBots})");
        IReadOnlyList<string> reportBots = listedReportBots.OfType<string>().ToArray();
        foreach (string bot in reportBots.Append(calibrationBot))
        {
            Require(BotPolicies.TryCreate(bot, out _), $"unknown bot '{bot}' (use {knownBots})");
        }

        Require(
            BotPolicies.Names.Contains(calibrationBot, StringComparer.OrdinalIgnoreCase),
            $"calibrationBot '{calibrationBot}' is a measurement bot; move limits are set against a player model ({string.Join(", ", BotPolicies.Names)}), measurement bots belong in reportBots");

        return new GenerationProfile
        {
            Name = file.Name ?? "unnamed",
            MasterSeed = file.MasterSeed ?? 1,
            CalibrationBot = calibrationBot,
            ReportBots = reportBots,
            TutorialUntil = file.TutorialUntil ?? 0,
            Cycle = ParseCycle(file.Cycle),
            Bands = ParseBands(file.Bands),
            Sizes = ParseSizes(file.Sizes),
            Palette = new PaletteRules(file.Palette?.NewColorsFrom ?? int.MaxValue, Positive(file.Palette?.ChapterSize ?? 50, "palette.chapterSize")),
            Mechanics = (file.Mechanics ?? []).Select(ParseUnlock).OrderBy(u => u.FromLevel).ToArray(),
            HookLevels = file.HookLevels ?? 3,
            NoveltyWindow = file.NoveltyWindow ?? 5,
            Calibration = ParseCalibration(file.Calibration ?? new CalibrationFile()),
            SeedPool = new SeedPoolSettings
            {
                Candidates = Positive(file.SeedPool?.Candidates ?? 32, "seedPool.candidates"),
                GamesPerSeed = Positive(file.SeedPool?.GamesPerSeed ?? 32, "seedPool.gamesPerSeed"),
                PoolSize = Positive(file.SeedPool?.PoolSize ?? 16, "seedPool.poolSize"),
                TolerancePoints = Positive(file.SeedPool?.TolerancePoints ?? 7, "seedPool.tolerancePoints"),
            },
            MinPoolSize = Math.Min(Positive(file.SeedPool?.MinPoolSize ?? 8, "seedPool.minPoolSize"), file.SeedPool?.PoolSize ?? 16),
        };
    }

    private static DifficultyTier[] ParseCycle(List<string>? cycle)
    {
        Require(cycle is { Count: > 0 }, "cycle needs at least one tier");
        return cycle!.Select(ParseTier).ToArray();
    }

    private static ProfileBand[] ParseBands(List<BandFile>? bands)
    {
        Require(bands is { Count: > 0 }, "bands needs at least one band");
        List<BandFile> sorted = bands!.OrderBy(b => b.FromLevel).ToList();
        Require(sorted[0].FromLevel == 1, "the first band must start at fromLevel 1");

        var resolved = new List<ProfileBand>();
        ProfileBand? previous = null;
        foreach (BandFile band in sorted)
        {
            string where = $"band fromLevel {band.FromLevel}";
            var current = new ProfileBand(
                band.FromLevel,
                MergeTiers(previous?.WinRate, band.WinRate, v => Require(v is > 0 and < 1, $"{where}: win rates must be between 0 and 1"), where + " winRate"),
                MergeTiers(previous?.Moves, band.Moves is null ? null : band.Moves.ToDictionary(p => p.Key, p => ParseRange(p.Value, where)), _ => { }, where + " moves"),
                MergeTiers(previous?.Colors, band.Colors, v => Require(v is >= 3 and <= 8, $"{where}: colors must be 3..8"), where + " colors"),
                band.MaxObstacleTypes ?? previous?.MaxObstacleTypes ?? 1);
            Require(current.MaxObstacleTypes is >= 0 and <= 4, $"{where}: maxObstacleTypes must be 0..4");
            resolved.Add(current);
            previous = current;
        }

        return resolved.ToArray();
    }

    /// <summary>Overlays the band's tier values on the inherited ones; the first band must name all four tiers.</summary>
    private static TierValues<T> MergeTiers<T>(TierValues<T>? inherited, Dictionary<string, T>? overrides, Action<T> validate, string where)
    {
        T Pick(DifficultyTier tier)
        {
            if (overrides is not null && overrides.TryGetValue(tier.ToString(), out T? value))
            {
                validate(value);
                return value;
            }

            return inherited is not null ? inherited.For(tier) : throw new ProfileFormatException($"{where}: missing {tier}");
        }

        foreach (string key in overrides?.Keys ?? Enumerable.Empty<string>())
        {
            ParseTier(key);
        }

        return new TierValues<T>(Pick(DifficultyTier.Easy), Pick(DifficultyTier.Normal), Pick(DifficultyTier.Hard), Pick(DifficultyTier.SuperHard));
    }

    private static MoveRange ParseRange(int[] pair, string where)
    {
        Require(pair.Length == 2 && pair[0] >= 5 && pair[1] >= pair[0] && pair[1] <= LevelDefinition.MaxMoveLimit, $"{where}: a move range is [min, max] with 5 <= min <= max");
        return new MoveRange(pair[0], pair[1]);
    }

    private static SizeRules ParseSizes(SizesFile? sizes)
    {
        Require(sizes is not null, "sizes is required");
        BoardSize Size(string text, string field) =>
            BoardSize.TryParse(text, out BoardSize size) ? size : throw new ProfileFormatException($"sizes.{field}: '{text}' is not WxH within 5..16");
        IReadOnlyList<BoardSize> Sizes(List<string>? list, string field)
        {
            Require(list is { Count: > 0 }, $"sizes.{field} needs at least one size");
            return list!.Select(s => Size(s, field)).ToArray();
        }

        return new SizeRules(
            Size(sizes!.Default ?? "10x10", "default"),
            sizes.FixedUntil ?? 0,
            new TierValues<IReadOnlyList<BoardSize>>(Sizes(sizes.Easy, "Easy"), Sizes(sizes.Normal, "Normal"), Sizes(sizes.Hard, "Hard"), Sizes(sizes.SuperHard, "SuperHard")));
    }

    private static MechanicUnlock ParseUnlock(UnlockFile unlock) =>
        Enum.TryParse(unlock.Unlock, ignoreCase: false, out Mechanic mechanic) && Enum.IsDefined(mechanic)
            ? new MechanicUnlock(Positive(unlock.FromLevel, "mechanics.fromLevel"), mechanic)
            : throw new ProfileFormatException($"unknown mechanic '{unlock.Unlock}' (use {string.Join(", ", Enum.GetNames<Mechanic>())})");

    private static CalibrationSettings ParseCalibration(CalibrationFile calibration)
    {
        double tolerance = calibration.WinRateTolerance ?? 0.07;
        Require(tolerance is > 0 and < 0.5, "calibration.winRateTolerance must be between 0 and 0.5");
        return new CalibrationSettings
        {
            Runs = Positive(calibration.Runs ?? 120, "calibration.runs"),
            VerifyRuns = Positive(calibration.VerifyRuns ?? 200, "calibration.verifyRuns"),
            ReportRuns = Positive(calibration.ReportRuns ?? 60, "calibration.reportRuns"),
            MoveCap = Positive(calibration.MoveCap ?? 150, "calibration.moveCap"),
            WinRateTolerance = tolerance,
            MaxCandidates = Positive(calibration.MaxCandidates ?? 8, "calibration.maxCandidates"),
            MaxScaleIterations = Positive(calibration.MaxScaleIterations ?? 4, "calibration.maxScaleIterations"),
        };
    }

    private static DifficultyTier ParseTier(string text) =>
        Enum.TryParse(text, ignoreCase: false, out DifficultyTier tier) && Enum.IsDefined(tier)
            ? tier
            : throw new ProfileFormatException($"unknown tier '{text}' (use Easy, Normal, Hard, SuperHard)");

    private static int Positive(int value, string field) =>
        value > 0 ? value : throw new ProfileFormatException($"{field} must be positive, got {value}");

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new ProfileFormatException(problem);
        }
    }

    // Wire format of profiles/*.json. Every field is optional here; Build applies defaults and validation.
    private sealed class ProfileFile
    {
        public string? Name { get; set; }

        public ulong? MasterSeed { get; set; }

        public string? CalibrationBot { get; set; }

        /// <summary>JSON may hold null entries; <see cref="Build"/> rejects them.</summary>
        public List<string?>? ReportBots { get; set; }

        public int? TutorialUntil { get; set; }

        public List<string>? Cycle { get; set; }

        public List<BandFile>? Bands { get; set; }

        public SizesFile? Sizes { get; set; }

        public PaletteFile? Palette { get; set; }

        public List<UnlockFile>? Mechanics { get; set; }

        public int? HookLevels { get; set; }

        public int? NoveltyWindow { get; set; }

        public CalibrationFile? Calibration { get; set; }

        public SeedPoolFile? SeedPool { get; set; }
    }

    private sealed class BandFile
    {
        public int FromLevel { get; set; }

        public Dictionary<string, double>? WinRate { get; set; }

        public Dictionary<string, int[]>? Moves { get; set; }

        public Dictionary<string, int>? Colors { get; set; }

        public int? MaxObstacleTypes { get; set; }
    }

    private sealed class SizesFile
    {
        public string? Default { get; set; }

        public int? FixedUntil { get; set; }

        public List<string>? Easy { get; set; }

        public List<string>? Normal { get; set; }

        public List<string>? Hard { get; set; }

        public List<string>? SuperHard { get; set; }
    }

    private sealed class PaletteFile
    {
        public int? NewColorsFrom { get; set; }

        public int? ChapterSize { get; set; }
    }

    private sealed class UnlockFile
    {
        public int FromLevel { get; set; }

        public string Unlock { get; set; } = string.Empty;
    }

    private sealed class CalibrationFile
    {
        public int? Runs { get; set; }

        public int? VerifyRuns { get; set; }

        public int? ReportRuns { get; set; }

        public int? MoveCap { get; set; }

        public double? WinRateTolerance { get; set; }

        public int? MaxCandidates { get; set; }

        public int? MaxScaleIterations { get; set; }
    }

    private sealed class SeedPoolFile
    {
        public int? Candidates { get; set; }

        public int? GamesPerSeed { get; set; }

        public int? PoolSize { get; set; }

        public int? TolerancePoints { get; set; }

        public int? MinPoolSize { get; set; }
    }
}
