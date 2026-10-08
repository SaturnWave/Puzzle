using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Assembling;

/// <summary>A difficulty policy is malformed; the message names the field.</summary>
public sealed class PolicyFormatException : Exception
{
    public PolicyFormatException(string message)
        : base(message)
    {
    }

    public PolicyFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Reads policies/*.json. Like the profile loader it refuses a field the format does not have: every field has a
/// default or an inherited value, so a misspelt one would load in silence and set a thousand limits with the wrong
/// number. A field stated twice is refused for the same reason: the second would replace the first unseen. A band inherits what it leaves out from the band before it; the first band must say who the player is,
/// what that player wins per tier, and the move margin.
/// </summary>
public static class PolicyLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new EnumKeyedConverter<DifficultyTier, int[]>() }, // for the tiers inside kindMoves
    };

    private static readonly JsonReaderOptions ReaderOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly DifficultyTier[] Tiers = Enum.GetValues<DifficultyTier>();

    /// <exception cref="PolicyFormatException">The file is missing, not JSON, inconsistent, or names a plan that does not load.</exception>
    public static DifficultyPolicy Load(string path)
    {
        string text;
        PolicyFile file;
        try
        {
            text = File.ReadAllText(path);
            RequireNoRepeatedField(text, path);
            file = JsonSerializer.Deserialize<PolicyFile>(text, Options) ?? throw new PolicyFormatException($"'{path}' is empty.");
        }
        catch (IOException ex)
        {
            throw new PolicyFormatException($"Cannot read policy '{path}': {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            // Malformed JSON, a value of the wrong kind, or a field the policy format does not have; the path says where.
            throw new PolicyFormatException($"Policy '{path}' is not a valid policy at {ex.Path ?? "$"}: {ex.Message}", ex);
        }

        string planPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, Required(file.Plan, "plan")));
        return Build(file, text, planPath, LoadPlan(planPath));
    }

    /// <summary>
    /// The JSON reader keeps the last of two fields of one name and says nothing. In a policy that would be a
    /// margin or a share nobody meant, applied to every level.
    /// </summary>
    /// <exception cref="PolicyFormatException">An object states a field twice.</exception>
    /// <exception cref="JsonException">The text is not JSON.</exception>
    private static void RequireNoRepeatedField(string text, string path)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), ReaderOptions);
        var openObjects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    openObjects.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    break;
                case JsonTokenType.EndObject:
                    openObjects.Pop();
                    break;
                case JsonTokenType.PropertyName when !openObjects.Peek().Add(reader.GetString()!):
                    throw new PolicyFormatException($"Policy '{path}' states '{reader.GetString()}' twice in one object; say it once.");
                default:
                    break;
            }
        }
    }

    private static GenerationProfile LoadPlan(string planPath)
    {
        try
        {
            return ProfileLoader.Load(planPath);
        }
        catch (ProfileFormatException ex)
        {
            throw new PolicyFormatException($"The policy's plan does not load: {ex.Message}", ex);
        }
    }

    private static DifficultyPolicy Build(PolicyFile file, string text, string planPath, GenerationProfile plan)
    {
        CheckSettings checks = ParseChecks(file.Checks ?? new ChecksFile(), plan);
        return new DifficultyPolicy
        {
            Name = file.Name ?? "unnamed",
            PlanPath = planPath,
            Plan = plan,
            Pinned = (file.Pinned ?? []).Select(ParseRange).ToArray(),
            Recorded = ParseRecorded(file.Recorded),
            Bands = ParseBands(file.Bands, plan),
            KindShares = ParseVariety(file.Variety ?? []),
            KindTolerance = ParseKindTolerance(file.VarietyTolerance),
            FailOnKindMiss = file.FailOnVarietyMiss ?? false,
            Weights = ParseWeights(file.Weights),
            Checks = checks,
            SourceText = text,
        };
    }

    private static PolicyBand[] ParseBands(List<BandFile>? files, GenerationProfile plan)
    {
        Require(files is { Count: > 0 }, "bands must list at least one band (who the player is and what that player wins)");
        var bands = new List<PolicyBand>();
        foreach (BandFile file in files!.OrderBy(band => band.FromLevel ?? 0))
        {
            int from = file.FromLevel ?? throw new PolicyFormatException("A band needs fromLevel.");
            string where = $"band fromLevel {from}";
            Require(from >= 1, $"{where}: levels start at 1");
            Require(bands.All(band => band.FromLevel != from), $"{where}: two bands start at the same level");
            PolicyBand? previous = bands.LastOrDefault();
            var band = new PolicyBand(
                from,
                file.Player is string player ? ParsePlayer(player, where) : previous?.Player ?? throw Missing(where, "player"),
                ParseShares(file.WinShare, previous?.WinShare, where),
                file.MoveMargin is decimal margin ? ParseMargin(margin, where) : previous?.MoveMargin ?? throw Missing(where, "moveMargin"),
                ParseMoves(file.Moves, previous?.Moves, where),
                ParseKindMoves(file.KindMoves, previous?.KindMoves, where));
            RequireWholeMarginUnderMoveCap(band, plan, where);
            bands.Add(band);
        }

        return [.. bands];
    }

    private static PolicyFormatException Missing(string where, string field) =>
        new($"{where}: the first band must state {field}; later bands inherit it.");

    private static PlayerMix ParsePlayer(string text, string where) => PlayerMix.TryParse(text, out PlayerMix? player, out string? problem)
        ? player
        : throw new PolicyFormatException($"{where}: player: {problem}");

    private static MoveMargin ParseMargin(decimal share, string where) => MoveMargin.TryCreate(share, out MoveMargin margin)
        ? margin
        : throw new PolicyFormatException($"{where}: moveMargin {share.ToString(CultureInfo.InvariantCulture)} must be a share from 0 to 1 (0.25 gives a quarter more moves).");

    private static TierValues<double> ParseShares(Dictionary<DifficultyTier, double>? stated, TierValues<double>? inherited, string where)
    {
        double ShareOf(DifficultyTier tier)
        {
            if (stated is not null && stated.TryGetValue(tier, out double share))
            {
                Require(share is > 0 and <= 1, $"{where}: winShare.{tier} must be a share above 0 and up to 1");
                return share;
            }

            return inherited is not null ? inherited.For(tier) : throw Missing(where, $"winShare.{tier}");
        }

        return new TierValues<double>(ShareOf(DifficultyTier.Easy), ShareOf(DifficultyTier.Normal), ShareOf(DifficultyTier.Hard), ShareOf(DifficultyTier.SuperHard));
    }

    /// <summary>A band that states move ranges states them for every tier it changes; the other tiers keep the earlier override, or the plan's range.</summary>
    private static TierValues<MoveRange>? ParseMoves(Dictionary<DifficultyTier, int[]>? stated, TierValues<MoveRange>? inherited, string where)
    {
        if (stated is null)
        {
            return inherited;
        }

        Require(inherited is not null || Tiers.All(stated.ContainsKey), $"{where}: the first moves override must name every tier ({string.Join(", ", Tiers)})");
        MoveRange RangeOf(DifficultyTier tier)
        {
            if (!stated.TryGetValue(tier, out int[]? ends))
            {
                return inherited!.For(tier);
            }

            Require(ends is { Length: 2 } && ends[0] >= 1 && ends[0] <= ends[1], $"{where}: moves.{tier} must be [min, max] with 1 <= min <= max");
            return new MoveRange(ends![0], ends[1]);
        }

        return new TierValues<MoveRange>(RangeOf(DifficultyTier.Easy), RangeOf(DifficultyTier.Normal), RangeOf(DifficultyTier.Hard), RangeOf(DifficultyTier.SuperHard));
    }

    /// <summary>
    /// The ranges of their own that kinds get from this band on: what the band states, over what the band before it
    /// left. A kind's range replaces the tier's for a level of that kind and nothing else.
    /// </summary>
    private static Dictionary<LevelKind, IReadOnlyDictionary<DifficultyTier, MoveRange>> ParseKindMoves(
        Dictionary<LevelKind, Dictionary<DifficultyTier, int[]>>? stated,
        IReadOnlyDictionary<LevelKind, IReadOnlyDictionary<DifficultyTier, MoveRange>>? inherited,
        string where)
    {
        var ranges = (inherited ?? new Dictionary<LevelKind, IReadOnlyDictionary<DifficultyTier, MoveRange>>())
            .ToDictionary(kind => kind.Key, kind => kind.Value);
        foreach ((LevelKind kind, Dictionary<DifficultyTier, int[]> tiers) in stated ?? [])
        {
            Dictionary<DifficultyTier, MoveRange> ofKind = ranges.TryGetValue(kind, out IReadOnlyDictionary<DifficultyTier, MoveRange>? before) ? before.ToDictionary() : [];
            foreach ((DifficultyTier tier, int[] ends) in tiers)
            {
                Require(ends is { Length: 2 } && ends[0] >= 1 && ends[0] <= ends[1], $"{where}: kindMoves.{kind}.{tier} must be [min, max] with 1 <= min <= max");
                ofKind[tier] = new MoveRange(ends[0], ends[1]);
            }

            ranges[kind] = ofKind;
        }

        return ranges;
    }

    /// <summary>Stored games are followed to the move cap and no further, so no limit, margin included, may lie past it.</summary>
    private static void RequireWholeMarginUnderMoveCap(PolicyBand band, GenerationProfile plan, string where)
    {
        int moveCap = MeasurementSettings.Default.MoveCap;
        IEnumerable<MoveRange> tierRanges = band.Moves is TierValues<MoveRange> own ? Tiers.Select(own.For) : PlanRangesFrom(plan, band.FromLevel);
        IEnumerable<MoveRange> ranges = tierRanges.Concat(band.KindMoves.Values.SelectMany(kind => kind.Values));
        foreach (MoveRange range in ranges)
        {
            Require(
                band.MoveMargin.Soften(range.Max) <= moveCap,
                $"{where}: the move range {range} with a margin of {band.MoveMargin} reaches {band.MoveMargin.Soften(range.Max)} moves, past the {moveCap} the bank's games are followed to");
        }
    }

    /// <summary>Every move range the plan has in force at <paramref name="level"/> or later: the ranges a band without its own takes over.</summary>
    private static IEnumerable<MoveRange> PlanRangesFrom(GenerationProfile plan, int level) => plan.Bands
        .Where(planBand => planBand.FromLevel >= level || planBand == plan.BandFor(level))
        .SelectMany(planBand => Tiers.Select(planBand.Moves.For));

    private static LevelRange ParseRange(string text)
    {
        string[] ends = text.Split('-', StringSplitOptions.TrimEntries);
        bool reads = ends.Length is 1 or 2 && ends.All(end => int.TryParse(end, NumberStyles.None, CultureInfo.InvariantCulture, out _));
        Require(reads, $"pinned: '{text}' is not a level or a range of levels (\"7\" or \"1-50\")");
        (int from, int to) = (int.Parse(ends[0], CultureInfo.InvariantCulture), int.Parse(ends[^1], CultureInfo.InvariantCulture));
        Require(from >= 1 && from <= to, $"pinned: '{text}' must run upward from level 1 or later");
        return new LevelRange(from, to);
    }

    private static RecordedProtection ParseRecorded(string? stated) => stated?.ToLowerInvariant() switch
    {
        null or "keepboard" => RecordedProtection.KeepBoard,
        "freeze" => RecordedProtection.Freeze,
        "none" => RecordedProtection.None,
        _ => throw new PolicyFormatException($"Invalid policy: recorded '{stated}' is not one of keepBoard, freeze, none."),
    };

    /// <summary>Three points: a thousand levels land a kind within one or two points of a target the bank can supply.</summary>
    private const double DefaultKindTolerance = 0.03;

    private static double ParseKindTolerance(double? stated)
    {
        double tolerance = stated ?? DefaultKindTolerance;
        Require(tolerance is > 0 and <= 1, "varietyTolerance must be a share above 0 and up to 1 (0.03 lets a kind lie three points off its target)");
        return tolerance;
    }

    private static Dictionary<LevelKind, double> ParseVariety(Dictionary<LevelKind, double> stated)
    {
        foreach ((LevelKind kind, double share) in stated)
        {
            Require(share is >= 0 and <= 1, $"variety.{kind} must be a share from 0 to 1");
        }

        return stated;
    }

    private static ChoiceWeights ParseWeights(WeightsFile? file)
    {
        var weights = new ChoiceWeights(
            file?.Centre ?? ChoiceWeights.Default.Centre,
            file?.Variety ?? ChoiceWeights.Default.Variety,
            file?.Repeat ?? ChoiceWeights.Default.Repeat);
        Require(weights is { Centre: >= 0, Variety: >= 0, Repeat: >= 0 }, "weights cannot be negative");
        return weights;
    }

    private static CheckSettings ParseChecks(ChecksFile file, GenerationProfile plan)
    {
        CheckSettings defaults = CheckSettings.Default;
        var checks = new CheckSettings
        {
            ConfirmSeeds = Positive(file.ConfirmSeeds ?? defaults.ConfirmSeeds, "checks.confirmSeeds"),
            ConfirmGamesPerSeed = Positive(file.ConfirmGamesPerSeed ?? defaults.ConfirmGamesPerSeed, "checks.confirmGamesPerSeed"),
            AuditGamesPerSeed = Positive(file.AuditGamesPerSeed ?? defaults.AuditGamesPerSeed, "checks.auditGamesPerSeed"),
            FirstSeedGames = Positive(file.FirstSeedGames ?? defaults.FirstSeedGames, "checks.firstSeedGames"),
            AuditRounds = Positive(file.AuditRounds ?? defaults.AuditRounds, "checks.auditRounds"),
            FirstTryTolerance = file.FirstTryTolerance ?? defaults.FirstTryTolerance,
            CandidatesPerLevel = Positive(file.CandidatesPerLevel ?? defaults.CandidatesPerLevel, "checks.candidatesPerLevel"),
            CheckGamesPerSeed = file.CheckGamesPerSeed ?? defaults.CheckGamesPerSeed,
        };
        Require(checks.CheckGamesPerSeed >= 0, "checks.checkGamesPerSeed cannot be negative (0 leaves the games after the audit out)");
        Require(checks.FirstTryTolerance is > 0 and <= 1, "checks.firstTryTolerance must be a share above 0 and up to 1");
        Require(checks.ConfirmSeeds >= plan.MinPoolSize, $"checks.confirmSeeds must be at least the plan's smallest seed pool ({plan.MinPoolSize})");
        Require(checks.ConfirmGamesPerSeed <= GamePurposes.RepeatsPerPurpose, $"checks.confirmGamesPerSeed can be at most {GamePurposes.RepeatsPerPurpose}");
        Require(checks.FirstSeedGames >= checks.AuditGamesPerSeed, "checks.firstSeedGames cannot be fewer than checks.auditGamesPerSeed: the first seed is a pool seed too");
        Require(
            (checks.AuditRounds * checks.FirstSeedGames) + checks.CheckGamesPerSeed <= GamePurposes.RepeatsPerPurpose,
            $"checks.auditRounds times checks.firstSeedGames, plus checks.checkGamesPerSeed, can be at most {GamePurposes.RepeatsPerPurpose}: every round plays games no earlier round has played");
        return checks;
    }

    private static int Positive(int value, string field)
    {
        Require(value >= 1, $"{field} must be at least 1");
        return value;
    }

    private static string Required(string? value, string field) =>
        value ?? throw new PolicyFormatException($"The policy must name its {field}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new PolicyFormatException($"Invalid policy: {message}.");
        }
    }

    private sealed class PolicyFile
    {
        public string? Name { get; set; }

        public string? Plan { get; set; }

        public List<string>? Pinned { get; set; }

        public string? Recorded { get; set; }

        public List<BandFile>? Bands { get; set; }

        [JsonConverter(typeof(EnumKeyedConverter<LevelKind, double>))]
        public Dictionary<LevelKind, double>? Variety { get; set; }

        public double? VarietyTolerance { get; set; }

        public bool? FailOnVarietyMiss { get; set; }

        public WeightsFile? Weights { get; set; }

        public ChecksFile? Checks { get; set; }
    }

    private sealed class BandFile
    {
        public int? FromLevel { get; set; }

        public string? Player { get; set; }

        public decimal? MoveMargin { get; set; }

        [JsonConverter(typeof(EnumKeyedConverter<DifficultyTier, double>))]
        public Dictionary<DifficultyTier, double>? WinShare { get; set; }

        [JsonConverter(typeof(EnumKeyedConverter<DifficultyTier, int[]>))]
        public Dictionary<DifficultyTier, int[]>? Moves { get; set; }

        [JsonConverter(typeof(EnumKeyedConverter<LevelKind, Dictionary<DifficultyTier, int[]>>))]
        public Dictionary<LevelKind, Dictionary<DifficultyTier, int[]>>? KindMoves { get; set; }
    }

    private sealed class WeightsFile
    {
        public double? Centre { get; set; }

        public double? Variety { get; set; }

        public double? Repeat { get; set; }
    }

    private sealed class ChecksFile
    {
        public int? ConfirmSeeds { get; set; }

        public int? ConfirmGamesPerSeed { get; set; }

        public int? AuditGamesPerSeed { get; set; }

        public int? FirstSeedGames { get; set; }

        public int? AuditRounds { get; set; }

        public double? FirstTryTolerance { get; set; }

        public int? CandidatesPerLevel { get; set; }

        public int? CheckGamesPerSeed { get; set; }
    }

    /// <summary>
    /// Reads an object whose property names are the names of a closed set (tiers, level kinds). A name outside the
    /// set is an error with the names that exist, like any other unknown field of the policy.
    /// </summary>
    private sealed class EnumKeyedConverter<TKey, TValue> : JsonConverter<Dictionary<TKey, TValue>>
        where TKey : struct, Enum
    {
        public override Dictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"An object keyed by {typeof(TKey).Name} is expected.");
            }

            var values = new Dictionary<TKey, TValue>();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString()!;
                if (!Enum.TryParse(name, ignoreCase: true, out TKey key) || !Enum.IsDefined(key) || int.TryParse(name, out _))
                {
                    throw new JsonException($"'{name}' is not one of {string.Join(", ", Enum.GetNames<TKey>())}.");
                }

                reader.Read();
                values[key] = JsonSerializer.Deserialize<TValue>(ref reader, options) ?? throw new JsonException($"{name} has no value.");
            }

            return values;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options) =>
            throw new NotSupportedException("Policies are read, not written.");
    }
}
