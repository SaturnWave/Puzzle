using System.Globalization;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Growing;

/// <summary>
/// What a level number asks of a board, apart from how hard it is: size, outline, colours, which obstacle families
/// are unlocked and how strong, how many may share a board, and what a hook level must show. A candidate either has
/// this structure or not; no measurement is involved.
/// </summary>
public sealed record StructureNeeds
{
    public required BoardSize Size { get; init; }

    public required bool PlainShape { get; init; }

    public required int Colors { get; init; }

    /// <summary>The strongest box unlocked, in hit points; 0 while boxes are locked. Likewise for the other families.</summary>
    public required int MaxBoxHp { get; init; }

    public required int MaxJellyLayers { get; init; }

    public required int MaxChainHp { get; init; }

    public required bool ItemsAllowed { get; init; }

    public required int MaxFamilies { get; init; }

    /// <summary>The strength a featured mechanic must really show on the board (a level that introduces two-hit boxes holds one); 0 when the family is not featured.</summary>
    public required int RequiredBoxHp { get; init; }

    public required int RequiredJellyLayers { get; init; }

    public required int RequiredChainHp { get; init; }

    public required bool ItemsRequired { get; init; }

    public static StructureNeeds Of(LevelPlan plan)
    {
        int required = Enum.GetValues<ObstacleKind>().Count(kind => Strongest(plan.Featured, kind) > 0);
        return new StructureNeeds
        {
            Size = plan.Size,
            PlainShape = plan.PlainShape,
            Colors = plan.Palette.Count,
            MaxBoxHp = Strongest(plan.Unlocked, ObstacleKind.Boxes),
            MaxJellyLayers = Strongest(plan.Unlocked, ObstacleKind.Jelly),
            MaxChainHp = Strongest(plan.Unlocked, ObstacleKind.Chains),
            ItemsAllowed = Strongest(plan.Unlocked, ObstacleKind.Items) > 0,
            MaxFamilies = Math.Max(plan.MaxObstacleTypes, required),
            RequiredBoxHp = Strongest(plan.Featured, ObstacleKind.Boxes),
            RequiredJellyLayers = Strongest(plan.Featured, ObstacleKind.Jelly),
            RequiredChainHp = Strongest(plan.Featured, ObstacleKind.Chains),
            ItemsRequired = Strongest(plan.Featured, ObstacleKind.Items) > 0,
        };
    }

    /// <summary>The strongest form of a family a board of this structure may carry; 0 when the family is locked.</summary>
    public int Unlocked(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => MaxBoxHp,
        ObstacleKind.Jelly => MaxJellyLayers,
        ObstacleKind.Chains => MaxChainHp,
        ObstacleKind.Items => ItemsAllowed ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The strength a board must show of a family; 0 when it need not carry it.</summary>
    public int Required(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => RequiredBoxHp,
        ObstacleKind.Jelly => RequiredJellyLayers,
        ObstacleKind.Chains => RequiredChainHp,
        ObstacleKind.Items => ItemsRequired ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// Whether a candidate has this structure: the size and colour count, a plain outline where one is asked for,
    /// no family stronger than unlocked, every featured strength really present, and no more families than allowed.
    /// </summary>
    public bool Admits(CandidateFeatures candidate) =>
        candidate.Width == Size.Width
        && candidate.Height == Size.Height
        && candidate.Colors == Colors
        && (!PlainShape || candidate.IsPlain)
        && candidate.Families.Count <= MaxFamilies
        && Enum.GetValues<ObstacleKind>().All(kind => candidate.StrengthOf(kind) <= Unlocked(kind) && candidate.StrengthOf(kind) >= Required(kind));

    /// <summary><c>10x11/6c/shaped/B3 J2 C2 I/max3</c>, with <c>/shows B2</c> on a hook level.</summary>
    public string Label
    {
        get
        {
            string unlocked = string.Join(' ', Enum.GetValues<ObstacleKind>().Where(kind => Unlocked(kind) > 0).Select(kind => Short(kind, Unlocked(kind))));
            string required = string.Join(' ', Enum.GetValues<ObstacleKind>().Where(kind => Required(kind) > 0).Select(kind => Short(kind, Required(kind))));
            string label = string.Create(CultureInfo.InvariantCulture, $"{Size}/{Colors}c/{(PlainShape ? "plain" : "shaped")}/{(unlocked.Length == 0 ? "none" : unlocked)}/max{MaxFamilies}");
            return required.Length == 0 ? label : $"{label}/shows {required}";
        }
    }

    private static string Short(ObstacleKind kind, int strength) => kind == ObstacleKind.Items
        ? "I"
        : string.Create(CultureInfo.InvariantCulture, $"{kind.ToString()[0]}{strength}");

    private static int Strongest(IReadOnlySet<Mechanic> mechanics, ObstacleKind kind) => kind switch
    {
        ObstacleKind.Boxes => mechanics.Contains(Mechanic.Box3) ? 3 : mechanics.Contains(Mechanic.Box2) ? 2 : mechanics.Contains(Mechanic.Box1) ? 1 : 0,
        ObstacleKind.Jelly => mechanics.Contains(Mechanic.Jelly2) ? 2 : mechanics.Contains(Mechanic.Jelly1) ? 1 : 0,
        ObstacleKind.Chains => mechanics.Contains(Mechanic.Chain2) ? 2 : mechanics.Contains(Mechanic.Chain1) ? 1 : 0,
        ObstacleKind.Items => mechanics.Contains(Mechanic.Items) ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>
/// The level numbers that ask the same of a candidate: one structure, and one difficulty slot (the tier's win share
/// and the move range the limit should land in). Candidates are not tied to a number, so a class, not a level, is
/// what the bank must be deep enough for.
/// </summary>
/// <param name="Share">The share of first tries the level's player should win at the calibrated limit.</param>
/// <param name="Moves">The range that limit should land in.</param>
public sealed record PlanClass(StructureNeeds Structure, DifficultyTier Tier, double Share, MoveRange Moves, IReadOnlyList<int> Levels)
{
    private const char LabelSeparator = '/';

    /// <summary>Move ranges of their own that boards of a kind have in this class, in place of <see cref="Moves"/>; empty when the policy gives none.</summary>
    public IReadOnlyDictionary<LevelKind, MoveRange> KindMoves { get; init; } = new Dictionary<LevelKind, MoveRange>();

    public string Label => LabelFor([]);

    /// <summary>The range the limit of a board with these families should land in.</summary>
    public MoveRange MovesFor(IReadOnlyList<ObstacleKind> families) => LevelKinds.MovesFor(LevelKinds.Of(families), KindMoves, Moves);

    /// <summary>
    /// The class as a board with these families sees it: the label with the move range that holds for such a board.
    /// A draw is recorded under this label, so what the trail learns of a family set (<see cref="AskOf"/>) is learnt
    /// for the range the set was really asked to fit, and a set that gets a range of its own starts with a clean record.
    /// </summary>
    public string LabelFor(IReadOnlyList<ObstacleKind> families) =>
        string.Create(CultureInfo.InvariantCulture, $"{Structure.Label}{LabelSeparator}{Tier} {Share:0.##} in {MovesFor(families)}");

    /// <summary>
    /// What a class asks of a board's length, read from its <see cref="Label"/>: the colours and the difficulty slot
    /// ("6c/Normal 0.75 in 20-33"), without the size and the unlocked strengths. Classes that ask the same are alike
    /// in which kinds of board their range can hold. A text that is no class label is its own ask.
    /// </summary>
    public static string AskOf(string classLabel)
    {
        string[] parts = classLabel.Split(LabelSeparator);
        return parts.Length >= 3 ? $"{parts[1]}{LabelSeparator}{parts[^1]}" : classLabel;
    }
}

/// <summary>The levels' plans do not say what the bank should grow toward; the message names the level and why.</summary>
public sealed class DemandException(string message) : Exception(message);

/// <summary>
/// What the bank is grown for: the classes of the planned levels, and the players (aims) whose limits should fit
/// them. The first aims are the players the plan itself calibrates with; further aims prepare the bank for a policy
/// that moves to another player, so that change finds candidates waiting.
/// </summary>
public sealed class Demand
{
    private Demand(IReadOnlyList<PlanClass> classes, IReadOnlyList<PlayerMix> aims, IReadOnlyDictionary<LevelKind, double> kindTargets)
    {
        Classes = classes;
        Aims = aims;
        KindTargets = kindTargets;
    }

    public IReadOnlyList<PlanClass> Classes { get; }

    public IReadOnlyList<PlayerMix> Aims { get; }

    /// <summary>The policy's target share per level kind, which the mix of drawn boards follows; empty for a demand read from a plan alone.</summary>
    public IReadOnlyDictionary<LevelKind, double> KindTargets { get; }

    /// <summary>
    /// Reads the demand from a level plan. The win shares and move ranges are the plan file's own for now; a policy
    /// file that takes them over supplies them the same way.
    /// </summary>
    /// <param name="alsoAims">Players beyond the plan's own to grow candidates for.</param>
    /// <exception cref="DemandException">A level is calibrated by a bot that is not on the skill ladder.</exception>
    public static Demand FromPlan(GenerationProfile profile, IEnumerable<int> levels, IEnumerable<PlayerMix> alsoAims)
    {
        var planner = new LevelPlanner(profile);
        LevelPlan[] plans = levels.Select(planner.Plan).ToArray();
        PlanClass[] classes = plans
            .GroupBy(plan => (Structure: StructureNeeds.Of(plan), plan.Tier, plan.TargetWinRate, plan.Moves))
            .Select(group => new PlanClass(group.Key.Structure, group.Key.Tier, group.Key.TargetWinRate, group.Key.Moves, group.Select(plan => plan.Number).ToArray()))
            .ToArray();
        PlayerMix[] aims = plans.Select(PlayerOf).Concat(alsoAims).DistinctBy(aim => aim.Label).ToArray();
        return new Demand(classes, aims, new Dictionary<LevelKind, double>());
    }

    /// <summary>
    /// Reads the demand from a difficulty policy: the classes of the levels it decides (a pinned level needs no
    /// board), with the policy's own players as the first aims.
    /// </summary>
    /// <param name="alsoAims">Players beyond the policy's own to grow candidates for.</param>
    /// <exception cref="PolicyFormatException">A level is neither pinned nor covered by a band of the policy.</exception>
    public static Demand FromPolicy(DifficultyPolicy policy, IEnumerable<int> levels, IEnumerable<PlayerMix> alsoAims)
    {
        (LevelSlot Slot, SlotDifficulty Ask)[] decided = LevelSlots.Of(policy, levels, RecordedLevels.None)
            .Where(slot => !slot.IsPinned && slot.Difficulty is not null)
            .Select(slot => (slot, slot.Difficulty!))
            .ToArray();
        PlanClass[] classes = decided
            .GroupBy(level => (level.Slot.Structure, level.Slot.Tier, level.Ask.Share, level.Ask.Moves, KindMoves: KindMovesText(level.Ask)))
            .Select(group => new PlanClass(group.Key.Structure, group.Key.Tier, group.Key.Share, group.Key.Moves, group.Select(level => level.Slot.Number).ToArray())
            {
                KindMoves = group.First().Ask.KindMoves,
            })
            .ToArray();
        PlayerMix[] aims = decided.Select(level => level.Ask.Player).Concat(alsoAims).DistinctBy(aim => aim.Label).ToArray();
        return new Demand(classes, aims, policy.KindShares);
    }

    /// <summary>The kinds' own ranges as one text, so levels are of one class only when they agree in them too.</summary>
    private static string KindMovesText(SlotDifficulty ask) =>
        string.Join(';', ask.KindMoves.OrderBy(kind => kind.Key).Select(kind => $"{kind.Key}:{kind.Value}"));

    private static PlayerMix PlayerOf(LevelPlan plan) => PlayerMix.TryParse(plan.CalibrationBot, out PlayerMix? player, out _)
        ? player
        : throw new DemandException(
            $"Level {plan.Number} is calibrated by '{plan.CalibrationBot}', which is not a player of the skill ladder; the bank holds games of the ladder only. Leave such levels out (--levels).");
}
