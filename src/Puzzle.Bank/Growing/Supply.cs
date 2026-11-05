using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;

namespace Puzzle.Bank.Growing;

/// <summary>
/// How deep the bank should be for a class: a board for each of its levels, and spares on top, because an assembly
/// uses a board once and a policy change can push many levels of one class out of their range at the same time.
/// </summary>
/// <param name="Share">Spare boards as a share of the class's levels.</param>
/// <param name="AtLeast">Spare boards even the smallest class gets.</param>
public sealed record SpareRule(double Share, int AtLeast)
{
    public static SpareRule Default { get; } = new(0.25, 4);

    public int NeededFor(int levels) => levels + Math.Max(AtLeast, (int)Math.Ceiling(Share * levels));
}

/// <summary>What the bank holds for one class and one aim.</summary>
/// <param name="Fitting">Candidates of the class's structure whose draft limit for the aim lies in the class's range. A class shares them with every class they also fit.</param>
/// <param name="Allotted">Boards left to this class after the classes that are shorter of boards took theirs: what the class can really count on.</param>
public sealed record ClassSupply(PlanClass Class, PlayerMix Aim, int Needed, IReadOnlyList<BankCandidate> Fitting, int Allotted)
{
    public int Deficit => Math.Max(0, Needed - Allotted);

    /// <summary>The share of what the class needs that is missing: 0 for a class deep enough, 1 for one with nothing.</summary>
    public double Thinness => Deficit / (double)Needed;
}

/// <summary>
/// Draft limits already read, by candidate, player and share. A candidate's breadth games do not change while a
/// bank grows, and the supply is counted again after every stored candidate.
/// </summary>
public sealed class LimitBook(EpochId epoch)
{
    private readonly Dictionary<(CandidateId, string, double), int?> _limits = [];

    public EpochId Epoch => epoch;

    public int? DraftLimit(BankCandidate candidate, PlayerMix player, double share)
    {
        (CandidateId, string, double) key = (candidate.Content.Id, player.Label, share);
        if (!_limits.TryGetValue(key, out int? limit))
        {
            limit = candidate.ReadingIn(epoch).DraftLimit(player, share);
            _limits[key] = limit;
        }

        return limit;
    }
}

/// <summary>Counts, for every class and aim of a demand, what the bank holds and what is missing.</summary>
public static class SupplyTable
{
    /// <summary>
    /// One row per aim and class. A board serves one level of an assembly, and a candidate can fit several classes,
    /// so boards are handed out once: the class with the fewest fitting boards for what it needs chooses first.
    /// Counting each class's fitting candidates alone would call a bank deep enough that is not.
    /// </summary>
    public static IReadOnlyList<ClassSupply> Of(BankCatalog catalog, Demand demand, SpareRule spare, LimitBook limits)
    {
        ILookup<(int, int, int), BankCandidate> bySizeAndColors = catalog.Candidates
            .Where(candidate => candidate.ReadingIn(limits.Epoch).IsUsable)
            .OrderBy(candidate => candidate.Content.Id.Value)
            .ToLookup(candidate => (candidate.Content.Features.Width, candidate.Content.Features.Height, candidate.Content.Features.Colors));
        var rows = new List<ClassSupply>();
        foreach (PlayerMix aim in demand.Aims)
        {
            var taken = new HashSet<BoardId>();
            var fitting = demand.Classes
                .Select(planClass => (Class: planClass, Fitting: FittingOf(planClass, aim, bySizeAndColors, limits)))
                .OrderBy(row => BoardsOf(row.Fitting).Count() / (double)spare.NeededFor(row.Class.Levels.Count))
                .ThenBy(row => row.Class.Label, StringComparer.Ordinal);
            foreach ((PlanClass planClass, BankCandidate[] candidates) in fitting)
            {
                int needed = spare.NeededFor(planClass.Levels.Count);
                rows.Add(new ClassSupply(planClass, aim, needed, candidates, Allot(BoardsOf(candidates), needed, taken)));
            }
        }

        return rows;
    }

    /// <summary>Takes up to <paramref name="needed"/> of the boards nobody took yet; returns how many it got.</summary>
    private static int Allot(IEnumerable<BoardId> boards, int needed, HashSet<BoardId> taken)
    {
        int allotted = 0;
        foreach (BoardId board in boards)
        {
            if (allotted == needed)
            {
                break;
            }

            if (taken.Add(board))
            {
                allotted++;
            }
        }

        return allotted;
    }

    private static BankCandidate[] FittingOf(PlanClass planClass, PlayerMix aim, ILookup<(int, int, int), BankCandidate> bySizeAndColors, LimitBook limits)
    {
        StructureNeeds structure = planClass.Structure;
        return bySizeAndColors[(structure.Size.Width, structure.Size.Height, structure.Colors)]
            .Where(candidate => structure.Admits(candidate.Content.Features))
            .Where(candidate => limits.DraftLimit(candidate, aim, planClass.Share) is int limit && planClass.MovesFor(candidate.Content.Features.Families).Contains(limit))
            .ToArray();
    }

    private static IEnumerable<BoardId> BoardsOf(IEnumerable<BankCandidate> candidates) =>
        candidates.Select(candidate => candidate.Content.Board).Distinct();
}

/// <summary>
/// Which obstacle families the boards of a class should carry, as target shares: variety is aimed at, not left to
/// the dice. Levels of collect goals alone stay the largest single kind; busier mixes are rarer. The policy's targets
/// per level kind steer it where a demand has them: the share of boards without obstacles is the policy's
/// collect-only target, and among boards with as many families a set weighs what the targets of its kinds sum to,
/// so the bank is grown toward the mix the assembler will ask it for.
/// </summary>
public static class KindMix
{
    /// <summary>Target share of boards by their number of families, when no policy says otherwise.</summary>
    private static readonly double[] ShareByFamilyCount = [0.35, 0.40, 0.18, 0.07];

    private static readonly IReadOnlyDictionary<LevelKind, double> NoTargets = new Dictionary<LevelKind, double>();

    /// <summary>The families a board of this structure may carry together: every set within the limit that holds the featured ones.</summary>
    public static IReadOnlyList<IReadOnlyList<ObstacleKind>> SetsFor(StructureNeeds structure)
    {
        ObstacleKind[] unlocked = Enum.GetValues<ObstacleKind>().Where(kind => structure.Unlocked(kind) > 0).ToArray();
        return Enumerable.Range(0, 1 << unlocked.Length)
            .Select(mask => (IReadOnlyList<ObstacleKind>)unlocked.Where((_, bit) => (mask & (1 << bit)) != 0).ToArray())
            .Where(set => set.Count <= Math.Min(structure.MaxFamilies, ShareByFamilyCount.Length - 1))
            .Where(set => Enum.GetValues<ObstacleKind>().All(kind => structure.Required(kind) == 0 || set.Contains(kind)))
            .ToArray();
    }

    /// <summary>
    /// The share of a class's boards that should carry exactly <paramref name="set"/>: the share of its family count
    /// among the counts the structure allows, split between the sets of that count by what the targets of their
    /// kinds sum to (evenly without targets, or when none of the sets has a kind with a target above zero).
    /// </summary>
    /// <param name="kindTargets">The policy's target share per level kind; absent or empty for the shares above.</param>
    public static double TargetShare(StructureNeeds structure, IReadOnlyList<ObstacleKind> set, IReadOnlyDictionary<LevelKind, double>? kindTargets = null)
    {
        IReadOnlyDictionary<LevelKind, double> targets = kindTargets ?? NoTargets;
        IReadOnlyList<IReadOnlyList<ObstacleKind>> allowed = SetsFor(structure);
        double shareOfAllowedCounts = allowed.Select(other => other.Count).Distinct().Sum(count => ShareOfCount(count, targets));
        IReadOnlyList<ObstacleKind>[] ofThisCount = allowed.Where(other => other.Count == set.Count).ToArray();
        double weightOfThisCount = ofThisCount.Sum(other => Weight(other, targets));
        double shareWithinCount = weightOfThisCount > 0 ? Weight(set, targets) / weightOfThisCount : 1.0 / ofThisCount.Length;
        return ShareOfCount(set.Count, targets) / shareOfAllowedCounts * shareWithinCount;
    }

    /// <summary>
    /// The share of boards with this many families: the policy's collect-only target for none, and what that leaves
    /// split between one, two and three families in the proportions of <see cref="ShareByFamilyCount"/>.
    /// </summary>
    private static double ShareOfCount(int families, IReadOnlyDictionary<LevelKind, double> targets)
    {
        double collectOnly = targets.GetValueOrDefault(LevelKind.CollectOnly, ShareByFamilyCount[0]);
        return families == 0 ? collectOnly : (1 - collectOnly) * ShareByFamilyCount[families] / (1 - ShareByFamilyCount[0]);
    }

    /// <summary>What a set weighs among the sets of its size: the sum of its kinds' targets; one per family without targets.</summary>
    private static double Weight(IReadOnlyList<ObstacleKind> set, IReadOnlyDictionary<LevelKind, double> targets) =>
        LevelKinds.Of(set).Sum(kind => targets.Count == 0 ? 1 : targets.GetValueOrDefault(kind));

    public static string LabelOf(IReadOnlyList<ObstacleKind> set) => set.Count == 0 ? "collect only" : string.Join('+', set);
}
