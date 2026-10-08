using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Assembling;

/// <summary>
/// What a player would call a level by: its goals are colours to collect and nothing else, or it carries boxes,
/// jelly, chains or items to drop. A level with two obstacle families is of both kinds. Closed set.
/// </summary>
public enum LevelKind
{
    CollectOnly,
    Boxes,
    Jelly,
    Chains,
    Items,
}

public static class LevelKinds
{
    /// <summary>The kinds of a level, read from its content: one per obstacle family on it, or <see cref="LevelKind.CollectOnly"/> without any.</summary>
    public static IReadOnlyList<LevelKind> Of(CandidateFeatures features) => Of(features.Families);

    /// <summary>The kinds of a board that carries <paramref name="families"/>.</summary>
    public static IReadOnlyList<LevelKind> Of(IReadOnlyList<ObstacleKind> families) => families.Count == 0
        ? [LevelKind.CollectOnly]
        : families.Select(Of).ToArray();

    /// <summary>
    /// The move range of a level of these kinds: the range a kind has of its own where the policy gives one (of
    /// several, the one that reaches furthest: the kind that needs the most moves sets the length of a level that
    /// carries it), otherwise <paramref name="ofTheTier"/>.
    /// </summary>
    public static MoveRange MovesFor(IReadOnlyList<LevelKind> kinds, IReadOnlyDictionary<LevelKind, MoveRange> kindMoves, MoveRange ofTheTier)
    {
        MoveRange[] own = kinds.Where(kindMoves.ContainsKey).Select(kind => kindMoves[kind]).ToArray();
        return own.Length == 0 ? ofTheTier : own.MaxBy(range => range.Max);
    }

    private static LevelKind Of(ObstacleKind family) => family switch
    {
        ObstacleKind.Boxes => LevelKind.Boxes,
        ObstacleKind.Jelly => LevelKind.Jelly,
        ObstacleKind.Chains => LevelKind.Chains,
        ObstacleKind.Items => LevelKind.Items,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
    };
}

/// <summary>What recorded attempts of people protect of the level they were played on, when the policy changes. Closed set.</summary>
public enum RecordedProtection
{
    /// <summary>
    /// The level keeps its board and goal amounts: a recording stays a recording of this level. Its limit and seed
    /// pool follow the policy, the recorded seeds staying in the pool wherever they still fit. A board that cannot
    /// meet the policy keeps the level it had and is reported, never swapped for another.
    /// </summary>
    KeepBoard,

    /// <summary>The level is kept byte for byte, like a pinned one: the policy no longer reaches it.</summary>
    Freeze,

    /// <summary>Recordings protect nothing.</summary>
    None,
}

/// <summary>Level numbers from <see cref="From"/> to <see cref="To"/>, both included.</summary>
public readonly record struct LevelRange(int From, int To)
{
    public bool Contains(int level) => level >= From && level <= To;

    public override string ToString() => From == To ? $"{From}" : $"{From}-{To}";
}

/// <summary>
/// The difficulty in force from <see cref="FromLevel"/> on, fully resolved (what a band of the file leaves out is
/// taken from the band before it).
/// </summary>
/// <param name="Player">Who the levels are made for: a rung of the skill ladder, or a mix of rungs.</param>
/// <param name="WinShare">The share of first attempts that player should win at the calibrated limit, per tier.</param>
/// <param name="MoveMargin">Extra moves on top of the calibrated limit; none when the share is to hold at the limit itself.</param>
/// <param name="Moves">The range the calibrated limit should land in, per tier; absent when the plan's ranges apply.</param>
/// <param name="KindMoves">
/// Move ranges of their own for levels of a kind, per tier: a level that drops items needs more moves than the tier
/// allows a level of colours, and without a range of its own no item board fits. A tier that is not named keeps the
/// tier's range for that kind.
/// </param>
public sealed record PolicyBand(
    int FromLevel,
    PlayerMix Player,
    TierValues<double> WinShare,
    MoveMargin MoveMargin,
    TierValues<MoveRange>? Moves,
    IReadOnlyDictionary<LevelKind, IReadOnlyDictionary<DifficultyTier, MoveRange>> KindMoves)
{
    /// <summary>The ranges of their own that kinds have in <paramref name="tier"/>.</summary>
    public IReadOnlyDictionary<LevelKind, MoveRange> KindMovesFor(DifficultyTier tier) => KindMoves
        .Where(kind => kind.Value.ContainsKey(tier))
        .ToDictionary(kind => kind.Key, kind => kind.Value[tier]);
}

/// <summary>
/// How the cost of a candidate for a level is weighed (lower is better). A candidate must first be allowed at all:
/// the level's structure, a limit inside the move range, a board no other level uses, no neighbour that looks the same.
/// </summary>
/// <param name="Centre">Per half range that the drafted limit lies off the middle of the move range: a limit near an edge is the first to leave the range when fresh games move it.</param>
/// <param name="Variety">Per whole target that the candidate's kinds are short of among the levels decided so far (a kind at half its target counts 0.5).</param>
/// <param name="Repeat">For carrying the same obstacle families as the level just before or after it.</param>
public sealed record ChoiceWeights(double Centre, double Variety, double Repeat)
{
    public static ChoiceWeights Default { get; } = new(Centre: 1, Variety: 4, Repeat: 0.5);
}

/// <summary>How many games a chosen level is confirmed and audited with, and how far an audit may be off.</summary>
public sealed record CheckSettings
{
    public static CheckSettings Default { get; } = new();

    /// <summary>Seeds of a candidate, the ones nearest the wanted share in its stored games, that the level's player plays in depth.</summary>
    public int ConfirmSeeds { get; init; } = 24;

    /// <summary>Games of the player on each of those seeds; the limit and the seed pool are read from them.</summary>
    public int ConfirmGamesPerSeed { get; init; } = 32;

    /// <summary>Fresh games on each pool seed that check the limit: games no choice has read.</summary>
    public int AuditGamesPerSeed { get; init; } = 16;

    /// <summary>Fresh games on the first pool seed, the one every player's first attempt gets.</summary>
    public int FirstSeedGames { get; init; } = 256;

    /// <summary>How often a level may fail its audit, have its limit read again with those games added, and be audited on new ones.</summary>
    public int AuditRounds { get; init; } = 3;

    /// <summary>How far the first pool seed's own win share may lie from the wanted one before another seed takes its place.</summary>
    public double FirstTryTolerance { get; init; } = 0.10;

    /// <summary>
    /// Games on each pool seed after a level has passed its audit, on repeats no audit uses. A passed audit leans
    /// toward the target (it is the one that passed); these games took no part in accepting the level, so the win
    /// share a report states is read from them. 0 leaves them out, and the report states the passed audit.
    /// </summary>
    public int CheckGamesPerSeed { get; init; } = 16;

    /// <summary>Candidates tried for one level before it is left unfilled.</summary>
    public int CandidatesPerLevel { get; init; } = 6;
}

/// <summary>
/// The difficulty policy: who the levels are made for and how often that player should win, which numbers stay as
/// they are, and what mix of level kinds an assembly aims at. It names the plan (a generation profile) that says what
/// each level number is: tier, board size, colours, mechanics, move ranges, novelty window, seed-pool size and the
/// tolerance of a checked win share. Changing this file and assembling again changes limits and, where a board no
/// longer fits, boards; it never touches the bank.
/// </summary>
public sealed record DifficultyPolicy
{
    public required string Name { get; init; }

    /// <summary>The plan file, as a full path.</summary>
    public required string PlanPath { get; init; }

    public required GenerationProfile Plan { get; init; }

    /// <summary>Levels kept byte for byte from the folder an assembly starts from.</summary>
    public required IReadOnlyList<LevelRange> Pinned { get; init; }

    /// <summary>What recorded attempts protect of a level that is not pinned.</summary>
    public required RecordedProtection Recorded { get; init; }

    /// <summary>Sorted by <see cref="PolicyBand.FromLevel"/>. Levels before the first band have no difficulty of their own and must be pinned.</summary>
    public required IReadOnlyList<PolicyBand> Bands { get; init; }

    /// <summary>The share of the assembled (not pinned) levels each kind should have; a kind without a target is not steered.</summary>
    public required IReadOnlyDictionary<LevelKind, double> KindShares { get; init; }

    /// <summary>How far a kind's share may lie from its target before an assembly reports the kind as missed.</summary>
    public required double KindTolerance { get; init; }

    /// <summary>A missed kind makes the assembly incomplete (a non-zero exit code), not only a line in its summary.</summary>
    public required bool FailOnKindMiss { get; init; }

    public required ChoiceWeights Weights { get; init; }

    public required CheckSettings Checks { get; init; }

    /// <summary>The policy file's text, kept so an assembly can say in full what it was made with.</summary>
    public required string SourceText { get; init; }

    public bool Pins(int level) => Pinned.Any(range => range.Contains(level));

    /// <summary>The band in force at <paramref name="level"/>; absent before the first band.</summary>
    public PolicyBand? BandFor(int level) => Bands.LastOrDefault(band => band.FromLevel <= level);
}
