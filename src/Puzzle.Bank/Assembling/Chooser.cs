using System.Globalization;
using Puzzle.Bank.Candidates;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Assembling;

/// <summary>What the choice of a level's board knows of a candidate: who it is, and how it would look to a player.</summary>
/// <param name="Signature">Outline and goal kinds: two levels with the same signature look alike.</param>
public sealed record ChoiceCandidate(CandidateId Id, BoardId Board, CandidateFeatures Features, LevelSignature Signature)
{
    public IReadOnlyList<LevelKind> Kinds { get; } = LevelKinds.Of(Features);

    public static ChoiceCandidate Of(CandidateContent content) => new(content.Id, content.Board, content.Features, LevelSignature.Of(content.Level));
}

/// <summary>What stands at a level number once it is decided, as far as its neighbours and the kind mix care.</summary>
public sealed record Occupant(LevelSignature Signature, IReadOnlyList<LevelKind> Kinds, string Families)
{
    public static Occupant Of(LevelDefinition level)
    {
        CandidateFeatures features = CandidateFeatures.Of(level);
        return new Occupant(LevelSignature.Of(level), LevelKinds.Of(features), features.FamilyLabel);
    }

    public static Occupant Of(ChoiceCandidate candidate) => new(candidate.Signature, candidate.Kinds, candidate.Features.FamilyLabel);
}

/// <summary>A candidate chosen for a level, with what the choice was made from.</summary>
/// <param name="DraftLimit">The limit the candidate's stored games give the level's player, before any fresh game.</param>
/// <param name="IsIncumbent">The board the level already had: it stays whenever it still fits, whatever the others cost.</param>
/// <param name="Why">The cost in words, for the assembly's table.</param>
public sealed record Choice(ChoiceCandidate Candidate, int DraftLimit, double Cost, bool IsIncumbent, string Why);

/// <summary>Why a level number got no board. Closed set.</summary>
public enum UnfilledCause
{
    /// <summary>The assembly had no bank to choose from.</summary>
    NoBank,

    /// <summary>The bank has no candidate of the level's size, colours and mechanics.</summary>
    NoStructure,

    /// <summary>Candidates of the structure exist, none with a limit in the level's move range for its player.</summary>
    NoLimitInRange,

    /// <summary>Every fitting candidate stands on a board another level uses.</summary>
    BoardsInUse,

    /// <summary>Every free fitting candidate looks like a level within the novelty window.</summary>
    LooksLikeANeighbour,

    /// <summary>Candidates were tried and the fresh games turned each of them down.</summary>
    TurnedDown,

    /// <summary>People have recorded attempts on the level's board, that board does not meet the policy, and no other is tried for it.</summary>
    RecordedBoard,
}

/// <summary>
/// Why a level found no candidate: how many candidates were left after each requirement, in the order they are
/// asked. The first zero names what the bank is short of.
/// </summary>
/// <param name="WithStructure">Candidates with the level's size, colours and mechanics.</param>
/// <param name="InRange">Of those, with a drafted limit inside the level's move range.</param>
/// <param name="Free">Of those, on a board no other level uses.</param>
/// <param name="Fresh">Of those, not looking like a level within the novelty window.</param>
/// <param name="Untried">Of those, not already turned down by this level's fresh games.</param>
public sealed record Shortage(int WithStructure, int InRange, int Free, int Fresh, int Untried)
{
    public UnfilledCause Cause => this switch
    {
        { WithStructure: 0 } => UnfilledCause.NoStructure,
        { InRange: 0 } => UnfilledCause.NoLimitInRange,
        { Free: 0 } => UnfilledCause.BoardsInUse,
        { Fresh: 0 } => UnfilledCause.LooksLikeANeighbour,
        _ => UnfilledCause.TurnedDown,
    };

    public string Describe(LevelSlot slot)
    {
        SlotDifficulty ask = slot.Difficulty ?? throw new InvalidOperationException($"Level {slot.Number} is pinned; nothing is chosen for it.");
        string limit = string.Create(CultureInfo.InvariantCulture, $"a limit in {ask.Moves} for {ask.Player.Label} at {ask.Share:0.##}");
        return Cause switch
        {
            UnfilledCause.NoStructure => $"the bank has no candidate of the structure {slot.Structure.Label}",
            UnfilledCause.NoLimitInRange => $"none of the {WithStructure} candidates of the structure {slot.Structure.Label} has {limit}",
            UnfilledCause.BoardsInUse => $"every one of the {InRange} candidates with {limit} is on a board another level uses",
            UnfilledCause.LooksLikeANeighbour => $"the {Free} free candidates all look like a level within the novelty window",
            UnfilledCause.TurnedDown => $"the fresh games turned down every one of the {Fresh} candidates left",
            UnfilledCause.NoBank or UnfilledCause.RecordedBoard => throw new InvalidOperationException("A shortage is counted among the candidates of a bank."),
            _ => throw new InvalidOperationException($"Unhandled cause {Cause}."),
        };
    }
}

/// <summary>
/// Chooses a board for a level number among the bank's candidates. A candidate is allowed when it has the level's
/// structure, its drafted limit lies in the level's move range (the range of its own kind, where the policy gives
/// the kind one), no other level uses its board, and no level within the novelty window looks the same. Among the allowed ones the cheapest is taken (<see cref="ChoiceWeights"/>):
/// near the middle of the move range, of a kind the assembly is short of, unlike its direct neighbours. A level's own
/// earlier board comes before all of that: while it is allowed, it stays.
/// The chooser plays no game and reads no file: what it is told (<see cref="Occupy"/>, <see cref="Reject"/>) and the
/// drafted limits are all it knows.
/// </summary>
public sealed class Chooser
{
    private readonly Dictionary<(int Width, int Height, int Colors), List<ChoiceCandidate>> _bySizeAndColors = [];
    private readonly Func<ChoiceCandidate, SlotDifficulty, int?> _draftLimit;
    private readonly IReadOnlyDictionary<LevelKind, double> _kindShares;
    private readonly ChoiceWeights _weights;
    private readonly int _noveltyWindow;
    private readonly Dictionary<int, Occupant> _occupants = [];
    private readonly Dictionary<BoardId, int> _boardUsers = [];
    /// <summary>Moves outside its move range at which a level's own board is still tried (<see cref="LimitNearRange"/>).</summary>
    private const int IncumbentSlack = 1;

    private readonly HashSet<(CandidateId Candidate, string Ask)> _turnedDown = [];
    private readonly Dictionary<LevelKind, int> _steeredByKind = [];
    private int _steered;

    /// <param name="draftLimit">The limit a candidate's stored games give a player for a share; absent when they give none (not measured, unusable, out of reach).</param>
    /// <param name="kindShares">The policy's target share per level kind.</param>
    /// <param name="noveltyWindow">A level may not look like any level this many numbers before or after it.</param>
    public Chooser(
        IEnumerable<ChoiceCandidate> candidates,
        Func<ChoiceCandidate, SlotDifficulty, int?> draftLimit,
        IReadOnlyDictionary<LevelKind, double> kindShares,
        ChoiceWeights weights,
        int noveltyWindow)
    {
        _draftLimit = draftLimit;
        _kindShares = kindShares;
        _weights = weights;
        _noveltyWindow = noveltyWindow;
        foreach (ChoiceCandidate candidate in candidates)
        {
            Add(candidate);
        }
    }

    /// <summary>A candidate the chooser may choose from now on: one the bank held from the start, or a sibling measured while the assembly runs.</summary>
    public void Add(ChoiceCandidate candidate)
    {
        (int, int, int) key = (candidate.Features.Width, candidate.Features.Height, candidate.Features.Colors);
        if (!_bySizeAndColors.TryGetValue(key, out List<ChoiceCandidate>? alike))
        {
            _bySizeAndColors[key] = alike = [];
        }

        alike.Add(candidate);
    }

    /// <summary>How many of the levels that count for the kind mix are decided, and how many of them are of each kind.</summary>
    public (int Levels, IReadOnlyDictionary<LevelKind, int> ByKind) KindMix => (_steered, _steeredByKind);

    /// <summary>
    /// Tells the chooser what stands at <paramref name="number"/> from now on.
    /// </summary>
    /// <param name="board">The board it stands on, which no other level may use; absent when the level is no bank candidate.</param>
    /// <param name="steered">The level counts for the kind mix: every level the assembly decides or keeps, not a pinned one.</param>
    public void Occupy(int number, Occupant occupant, BoardId? board, bool steered)
    {
        _occupants[number] = occupant;
        if (board is BoardId taken)
        {
            _boardUsers[taken] = number;
        }

        if (!steered)
        {
            return;
        }

        _steered++;
        foreach (LevelKind kind in occupant.Kinds)
        {
            _steeredByKind[kind] = _steeredByKind.GetValueOrDefault(kind) + 1;
        }
    }

    /// <summary>Holds a level's earlier board back for it until the level is decided, so no other level takes it first.</summary>
    public void Reserve(BoardId board, int number) => _boardUsers.TryAdd(board, number);

    /// <summary>Gives a reserved board up: its level did not keep it.</summary>
    public void Release(BoardId board, int number)
    {
        if (_boardUsers.GetValueOrDefault(board) == number)
        {
            _boardUsers.Remove(board);
        }
    }

    /// <summary>
    /// The fresh games turned <paramref name="candidate"/> down for <paramref name="slot"/>. What they showed depends
    /// on the candidate and on what is asked of it, not on the level number, so it is not offered again to any level
    /// that asks the same (player, share, move range, margin); a level that asks otherwise may still take it.
    /// </summary>
    public void Reject(LevelSlot slot, ChoiceCandidate candidate) => _turnedDown.Add((candidate.Id, AskOf(slot, candidate)));

    /// <summary>
    /// The candidate to try next for <paramref name="slot"/>: its incumbent while that is allowed, otherwise the
    /// cheapest allowed candidate; absent when none is allowed (<see cref="ShortageOf"/> says why).
    /// </summary>
    /// <param name="incumbent">The board the level has today, when the bank holds it.</param>
    public Choice? Best(LevelSlot slot, ChoiceCandidate? incumbent)
    {
        if (incumbent is not null && IsUntried(slot, incumbent) && IsFreeFor(slot, incumbent) && LimitNearRange(slot, incumbent) is int stays)
        {
            return new Choice(incumbent, stays, Cost: 0, IsIncumbent: true, "the level's own board still fits");
        }

        return Allowed(slot)
            .Select(candidate => Scored(slot, AskOf(slot, candidate.Candidate.Features), candidate.Candidate, candidate.Limit))
            .OrderBy(choice => choice.Cost)
            .ThenBy(choice => choice.Candidate.Id.Value)
            .FirstOrDefault();
    }

    /// <summary>How many candidates are allowed for the level right now: the fewer, the earlier it should choose.</summary>
    public int AllowedCount(LevelSlot slot) => Allowed(slot).Count();

    public Shortage ShortageOf(LevelSlot slot)
    {
        ChoiceCandidate[] withStructure = WithStructure(slot).ToArray();
        ChoiceCandidate[] inRange = withStructure.Where(candidate => LimitInRange(slot, candidate) is not null).ToArray();
        ChoiceCandidate[] free = inRange.Where(candidate => IsFreeFor(slot, candidate)).ToArray();
        ChoiceCandidate[] fresh = free.Where(candidate => !LooksLikeANeighbour(slot.Number, candidate)).ToArray();
        return new Shortage(withStructure.Length, inRange.Length, free.Length, fresh.Length, fresh.Count(candidate => IsUntried(slot, candidate)));
    }

    private IEnumerable<(ChoiceCandidate Candidate, int Limit)> Allowed(LevelSlot slot)
    {
        foreach (ChoiceCandidate candidate in WithStructure(slot))
        {
            if (IsUntried(slot, candidate) && IsFreeFor(slot, candidate) && !LooksLikeANeighbour(slot.Number, candidate) && LimitInRange(slot, candidate) is int limit)
            {
                yield return (candidate, limit);
            }
        }
    }

    private IEnumerable<ChoiceCandidate> WithStructure(LevelSlot slot) =>
        _bySizeAndColors.GetValueOrDefault((slot.Structure.Size.Width, slot.Structure.Size.Height, slot.Structure.Colors), [])
            .Where(candidate => slot.Structure.Admits(candidate.Features));

    private int? LimitInRange(LevelSlot slot, ChoiceCandidate candidate)
    {
        SlotDifficulty ask = AskOf(slot, candidate.Features);
        return slot.Structure.Admits(candidate.Features) && _draftLimit(candidate, ask) is int limit && ask.Moves.Contains(limit) ? limit : null;
    }

    /// <summary>
    /// A level's own board is tried when its drafted limit lies in the move range or <see cref="IncumbentSlack"/>
    /// outside it: a drafted limit is a move or so off the one the fresh games will read, and they decide. Keeping
    /// the board a player knows is worth that try; a new board is only chosen with its drafted limit inside.
    /// </summary>
    private int? LimitNearRange(LevelSlot slot, ChoiceCandidate candidate)
    {
        SlotDifficulty ask = AskOf(slot, candidate.Features);
        return slot.Structure.Admits(candidate.Features) && _draftLimit(candidate, ask) is int limit && ask.Moves.DistanceTo(limit) <= IncumbentSlack ? limit : null;
    }

    private bool IsFreeFor(LevelSlot slot, ChoiceCandidate candidate) =>
        !_boardUsers.TryGetValue(candidate.Board, out int user) || user == slot.Number;

    private bool IsUntried(LevelSlot slot, ChoiceCandidate candidate) => !_turnedDown.Contains((candidate.Id, AskOf(slot, candidate)));

    private static string AskOf(LevelSlot slot, ChoiceCandidate candidate)
    {
        SlotDifficulty ask = AskOf(slot, candidate.Features);
        return string.Create(CultureInfo.InvariantCulture, $"{ask.Player.Label}|{ask.Share:R}|{ask.Moves}|{ask.Margin}");
    }

    /// <summary>What the level asks of a board of this kind.</summary>
    private static SlotDifficulty AskOf(LevelSlot slot, CandidateFeatures board) => DifficultyOf(slot).For(board);

    private bool LooksLikeANeighbour(int number, ChoiceCandidate candidate)
    {
        for (int distance = 1; distance <= _noveltyWindow; distance++)
        {
            if (LooksLike(number - distance, candidate) || LooksLike(number + distance, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private bool LooksLike(int number, ChoiceCandidate candidate) =>
        _occupants.TryGetValue(number, out Occupant? occupant) && occupant.Signature == candidate.Signature;

    private Choice Scored(LevelSlot slot, SlotDifficulty ask, ChoiceCandidate candidate, int limit)
    {
        double halfRange = Math.Max(1, (ask.Moves.Max - ask.Moves.Min) / 2.0);
        double offCentre = Math.Abs(limit - ((ask.Moves.Min + ask.Moves.Max) / 2.0)) / halfRange;
        double shortOfTarget = candidate.Kinds.Sum(ShortOfTarget);
        int alikeNeighbours = new[] { slot.Number - 1, slot.Number + 1 }
            .Count(number => _occupants.TryGetValue(number, out Occupant? neighbour) && neighbour.Families == candidate.Features.FamilyLabel);
        double cost = (_weights.Centre * offCentre) - (_weights.Variety * shortOfTarget) + (_weights.Repeat * alikeNeighbours);
        string why = string.Create(
            CultureInfo.InvariantCulture,
            $"limit {limit} is {offCentre:0.00} of half the range off its middle; its kinds are {shortOfTarget:+0.000;-0.000} short of target; {alikeNeighbours} direct neighbours carry the same families");
        return new Choice(candidate, limit, cost, IsIncumbent: false, why);
    }

    /// <summary>
    /// How far a kind's share among the decided levels is below its target, as a share of the target (1 = none of it
    /// yet, 0 = on target, negative when above); 0 for a kind the policy does not steer. Measured against the target
    /// and not in points of share, because a rare kind is short in a way a common one is not: drop-item levels at
    /// 3 % of a 6 % target lack half of what was asked, and in points that pull was weaker than the pull toward the
    /// middle of a move range. (Steering by what the whole set should hold instead of by the share so far was tried
    /// on the real bank: four more item levels, and every common kind one to three points further off its target.)
    /// </summary>
    private double ShortOfTarget(LevelKind kind)
    {
        if (!_kindShares.TryGetValue(kind, out double target))
        {
            return 0;
        }

        double share = _steered == 0 ? 0 : _steeredByKind.GetValueOrDefault(kind) / (double)_steered;
        return target > 0 ? (target - share) / target : -share;
    }

    private static SlotDifficulty DifficultyOf(LevelSlot slot) =>
        slot.Difficulty ?? throw new InvalidOperationException($"Level {slot.Number} has no difficulty of its own; only a level the policy covers is chosen for.");
}
