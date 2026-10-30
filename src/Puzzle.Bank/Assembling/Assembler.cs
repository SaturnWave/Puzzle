using System.Globalization;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;

namespace Puzzle.Bank.Assembling;

/// <summary>An assembly cannot be made as asked; the message names the level and what is missing.</summary>
public sealed class AssemblyException(string message) : Exception(message);

/// <summary>The bank as an assembly uses it: its candidates, the epoch whose games count, and where further games come from.</summary>
/// <param name="BankId">Which bank it is, for an assembly's manifest.</param>
/// <param name="Siblings">Where a level's own board at another amount comes from; absent when an assembly may not measure new candidates.</param>
/// <param name="Cut">
/// How long each shard file was when the assembly read the bank. Shards only ever grow at their end, so this names
/// exactly the content the choice was made from, whatever is added later.
/// </param>
public sealed record BankAccess(string BankId, BankCatalog Catalog, EpochId Epoch, IGameSource Games, ISiblingSource? Siblings = null, IReadOnlyList<ShardLength>? Cut = null);

/// <summary>A shard file and its length in bytes at one moment.</summary>
public sealed record ShardLength(string File, long Bytes);

/// <summary>What an assembly decided for one level number. Closed set (private constructor).</summary>
public abstract record LevelDecision
{
    private LevelDecision(LevelSlot slot) => Slot = slot;

    public LevelSlot Slot { get; }

    public int Number => Slot.Number;

    /// <summary>The policy pins the number: file and report row are copied byte for byte.</summary>
    public sealed record Pinned(LevelSlot Slot, FolderLevel Level) : LevelDecision(Slot);

    /// <summary>
    /// The level was decided under the very inputs it has today (<see cref="DecisionKey"/>, <see cref="DecisionStamp"/>),
    /// the plan still admits its board and its limit lies in its range: file and row are copied byte for byte.
    /// </summary>
    public sealed record Kept(LevelSlot Slot, FolderLevel Level, string Key) : LevelDecision(Slot);

    /// <summary>A bank candidate became the level, with a limit and a pool read from its games and checked on fresh ones.</summary>
    /// <param name="SameBoard">The candidate is the board the level had before: only its limit and pool were decided anew.</param>
    /// <param name="NotKept">Why the level the number had did not stay as it was.</param>
    /// <param name="TurnedDown">Candidates tried before this one, each with what its fresh games showed.</param>
    public sealed record Assembled(
        LevelSlot Slot,
        BankCandidate Candidate,
        Choice Choice,
        FinishedLevel Finished,
        LevelDefinition Level,
        string Key,
        bool SameBoard,
        string NotKept,
        IReadOnlyList<string> TurnedDown) : LevelDecision(Slot);

    /// <summary>
    /// No candidate became the level. The bank is too thin for it, and the assembly says so instead of bending a rule.
    /// </summary>
    /// <param name="Stale">The level the number had before, copied so the set stays playable although it no longer meets the policy; absent when there was none.</param>
    /// <param name="NotKept">Why the level the number had did not stay as it was.</param>
    public sealed record Unfilled(LevelSlot Slot, FolderLevel? Stale, UnfilledCause Cause, string Reason, string NotKept, IReadOnlyList<string> TurnedDown) : LevelDecision(Slot);
}

/// <summary>How a level of an assembled set falls short of the policy the set was made for. Closed set.</summary>
public enum ShortfallKind
{
    /// <summary>The number has no level at all.</summary>
    Missing,

    /// <summary>No candidate fits the number; it carries the level it had before, made for another policy or outside its move range.</summary>
    Stale,

    /// <summary>A pinned level whose own report row says it does not meet its move range.</summary>
    PinnedOutsideItsRange,

    /// <summary>A pinned level whose limit was decided under other rules of the engine or the bot than this build has.</summary>
    PinnedUnderOtherRules,
}

/// <summary>A level that is in the set without meeting the policy, so nobody takes the set for more than it is.</summary>
public sealed record LevelShortfall(int Level, ShortfallKind Kind, string Detail);

/// <summary>How many of the levels an assembly steers are of a kind, against the policy's target.</summary>
public sealed record KindShare(LevelKind Kind, int Levels, double Share, double Target)
{
    public double Off => Share - Target;
}

/// <summary>An assembly: one decision per level number, in number order.</summary>
/// <param name="Stamp">What the levels it decided, kept or took over stand under.</param>
public sealed record Assembly(DifficultyPolicy Policy, LevelFolder Base, BankAccess? Bank, DecisionStamp Stamp, IReadOnlyList<LevelDecision> Decisions)
{
    /// <summary>The levels of the set that do not meet the policy, in number order.</summary>
    public IReadOnlyList<LevelShortfall> Shortfalls => Decisions.Select(ShortfallOf).OfType<LevelShortfall>().ToArray();

    /// <summary>
    /// The mix of kinds among the levels the assembly steers (every level but the pinned ones), for each kind the
    /// policy has a target for.
    /// </summary>
    public IReadOnlyList<KindShare> KindMix
    {
        get
        {
            IReadOnlyList<LevelKind>[] steered = Decisions.Select(SteeredContent).OfType<CandidateContent>().Select(content => LevelKinds.Of(content.Features)).ToArray();
            return Policy.KindShares
                .OrderBy(target => target.Key)
                .Select(target => (Kind: target.Key, Target: target.Value, Levels: steered.Count(kinds => kinds.Contains(target.Key))))
                .Select(kind => new KindShare(kind.Kind, kind.Levels, steered.Length == 0 ? 0 : kind.Levels / (double)steered.Length, kind.Target))
                .ToArray();
        }
    }

    /// <summary>The kinds whose share lies further from its target than the policy tolerates.</summary>
    public IReadOnlyList<KindShare> MissedKinds => KindMix.Where(kind => Math.Abs(kind.Off) > Policy.KindTolerance + 1e-9).ToArray();

    /// <summary>Every number has a level made for this policy, and no kind is missed where the policy makes that count.</summary>
    public bool IsComplete => !Decisions.Any(decision => decision is LevelDecision.Unfilled) && (!Policy.FailOnKindMiss || MissedKinds.Count == 0);

    private static CandidateContent? SteeredContent(LevelDecision decision) => decision switch
    {
        LevelDecision.Pinned => null,
        LevelDecision.Kept kept => kept.Level.Content,
        LevelDecision.Assembled made => made.Candidate.Content,
        LevelDecision.Unfilled unfilled => unfilled.Stale?.Content,
        _ => throw new InvalidOperationException($"Unhandled decision {decision.GetType().Name}."),
    };

    private LevelShortfall? ShortfallOf(LevelDecision decision) => decision switch
    {
        LevelDecision.Unfilled { Stale: null } missing => new LevelShortfall(missing.Number, ShortfallKind.Missing, missing.Reason),
        LevelDecision.Unfilled stale => new LevelShortfall(stale.Number, ShortfallKind.Stale, $"{stale.NotKept}; {stale.Reason}"),
        LevelDecision.Pinned { Level.IsRelaxedOrStale: true } pinned => new LevelShortfall(pinned.Number, ShortfallKind.PinnedOutsideItsRange, "its report row says it does not meet its move range"),
        LevelDecision.Pinned pinned when pinned.Level.Stamp is DecisionStamp stamp && stamp.Rules != Stamp.Rules =>
            new LevelShortfall(pinned.Number, ShortfallKind.PinnedUnderOtherRules, $"decided under rules {stamp.Rules}, this build has {Stamp.Rules}"),
        _ => null,
    };
}

/// <summary>
/// Makes the game's levels from a policy, a folder of levels to start from, and the bank. For every level number, in
/// this order of preference:
/// <list type="number">
/// <item>a pinned number is copied as it is;</item>
/// <item>a level that still stands is copied as it is: decided under the inputs the policy and the plan still give
/// it, under this build's rules, on a board the plan still admits, with its limit inside its range;</item>
/// <item>a level whose board the bank holds keeps the board while it still fits, and gets the limit and pool the new
/// policy reads from its games;</item>
/// <item>a level whose board no longer fits, but whose goals measure an amount, keeps the board at the amount that
/// brings its limit back into the range (<see cref="ISiblingSource"/>);</item>
/// <item>any other level gets the cheapest allowed candidate of the bank (<see cref="Chooser"/>), the levels with
/// the fewest candidates choosing first;</item>
/// <item>a level no candidate fits is reported unfilled.</item>
/// </list>
/// A level people have recorded attempts on stops after the third step where the policy says so: it keeps its board
/// with a new limit, or it is reported, but it never gets another board or other amounts.
/// Every candidate that becomes a level passes the fresh-games check first (<see cref="Finisher"/>); one that fails
/// is turned down for that level and the next is tried. The same inputs give the same assembly: nothing here
/// depends on a clock or on the order of anything but level numbers and candidate ids.
/// </summary>
public sealed class Assembler
{
    private readonly DifficultyPolicy _policy;
    private readonly LevelFolder _base;
    private readonly BankAccess? _bank;
    private readonly RecordedLevels _recorded;
    private readonly DecisionStamp _stamp;
    private readonly Chooser _chooser;
    private readonly Finisher? _finisher;
    private readonly Dictionary<CandidateId, ChoiceCandidate> _choiceCandidates;
    private readonly Func<ChoiceCandidate, SlotDifficulty, int?> _draftLimit;

    /// <summary>
    /// Moves added to a limit drafted from a candidate's stored ladder games, to stand for the limit its confirm
    /// games will give. Measured on the 913 levels of an assembly of levels 51-1000 (2026-10-08): the confirm limit
    /// lay 0.57 (Easy), 0.66 (Normal), 0.71 (Hard) and 1.03 (Super Hard) moves above the draft. Without it the
    /// candidates drafted at the top of a move range are chosen and then turned down by their own confirm games.
    /// </summary>
    private const int DraftRunsLow = 1;

    /// <summary>How often a board's amount is moved to bring its limit back into the range before the level looks for another board.</summary>
    private const int AmountMoves = 2;

    /// <param name="baseLevels">The folder the assembly starts from: the live levels, or an earlier assembly.</param>
    /// <param name="bank">Absent when no bank is at hand: pinned and kept levels need none, every other level is then unfilled.</param>
    /// <param name="recorded">The levels of <paramref name="baseLevels"/> people have recorded attempts on.</param>
    /// <param name="stamp">What a level must have been decided under to stay; this build's rules and the policy's checks unless a test says otherwise.</param>
    public Assembler(DifficultyPolicy policy, LevelFolder baseLevels, BankAccess? bank, RecordedLevels recorded, DecisionStamp? stamp = null)
    {
        _policy = policy;
        _base = baseLevels;
        _bank = bank;
        _recorded = recorded;
        _stamp = stamp ?? DecisionStamp.Of(policy, bank?.Epoch);
        BankCandidate[] usable = bank is null ? [] : bank.Catalog.Candidates.Where(candidate => candidate.ReadingIn(bank.Epoch).IsUsable).ToArray();
        _choiceCandidates = usable.ToDictionary(candidate => candidate.Content.Id, candidate => ChoiceCandidate.Of(candidate.Content));
        _draftLimit = DraftLimits(bank);
        _chooser = new Chooser(
            _choiceCandidates.Values,
            _draftLimit,
            policy.KindShares,
            policy.Weights,
            policy.Plan.NoveltyWindow);
        _finisher = bank is null
            ? null
            : new Finisher(bank.Games, policy.Checks, PoolRules.Of(policy.Plan), policy.Plan.Calibration.WinRateTolerance, bank.Epoch, MeasurementSettings.Default.MoveCap);
    }

    /// <summary>The limits the bank's stored games give; without a bank there is no candidate to ask about.</summary>
    private static Func<ChoiceCandidate, SlotDifficulty, int?> DraftLimits(BankAccess? bank)
    {
        if (bank is null)
        {
            return (_, _) => null;
        }

        var limits = new LimitBook(bank.Epoch);
        return (candidate, ask) => bank.Catalog.Find(candidate.Id) is BankCandidate held ? limits.DraftLimit(held, ask.Player, ask.Share) + DraftRunsLow : null;
    }

    private Finisher FreshGames => _finisher ?? throw new InvalidOperationException("A candidate is only finished with a bank at hand.");

    /// <param name="onDecided">Told of every level the assembly had to decide anew, as soon as it is decided, for a progress line.</param>
    /// <exception cref="PolicyFormatException">A level is neither pinned nor covered by a band.</exception>
    /// <exception cref="AssemblyException">A pinned level has no file in the base folder.</exception>
    /// <exception cref="BankReadException">Stored games could not be read from the bank.</exception>
    /// <exception cref="BankWriteException">Games were played and could not be stored in the bank.</exception>
    public Assembly Run(IEnumerable<int> numbers, Action<LevelDecision>? onDecided = null)
    {
        var decisions = new SortedDictionary<int, LevelDecision>();
        var open = new List<OpenLevel>();
        foreach (LevelSlot slot in LevelSlots.Of(_policy, numbers, _recorded))
        {
            FolderLevel? level = _base.Find(slot.Number);
            if (slot.IsPinned)
            {
                decisions[slot.Number] = Pin(slot, level);
            }
            else if (WhyNotKept(slot, level) is string notKept)
            {
                open.Add(Opened(slot, level, notKept));
            }
            else
            {
                decisions[slot.Number] = Keep(slot, level!);
            }
        }

        // A level's own board first: it stays wherever it still fits, before any other level may look at it.
        foreach (OpenLevel level in open.Where(level => level.Incumbent is not null))
        {
            if (_chooser.Best(level.Slot, level.Incumbent) is { IsIncumbent: true } own)
            {
                level.Try(own, this);
                Report(level, onDecided);
            }
        }

        // A recorded board that could not stay keeps the level it had: the recordings are of that candidate and of no other.
        foreach (OpenLevel level in open.Where(level => level.Decision is null && level.Slot.KeepsBoard))
        {
            level.Decision = LeaveUnfilled(level);
            Report(level, onDecided);
        }

        // A board whose limit left the range stays too, where another amount of its goals brings the limit back.
        foreach (OpenLevel level in open.Where(level => level.Decision is null && level.Incumbent is not null))
        {
            if (SiblingInRange(level) is ChoiceCandidate sibling && _chooser.Best(level.Slot, sibling) is { IsIncumbent: true } sameBoard)
            {
                level.Try(sameBoard with { Why = "the level's own board fits at another amount of its goals" }, this);
                Report(level, onDecided);
            }
        }

        // Then the levels with the fewest candidates to choose from, so the plentiful kinds do not use up what a rare one needs.
        foreach (OpenLevel level in open.Where(level => level.Decision is null).OrderBy(level => _chooser.AllowedCount(level.Slot)).ThenBy(level => level.Slot.Number).ToArray())
        {
            while (level.Decision is null && level.Tried < _policy.Checks.CandidatesPerLevel && _chooser.Best(level.Slot, incumbent: null) is Choice choice)
            {
                level.Try(choice, this);
            }

            level.Decision ??= LeaveUnfilled(level);
            Report(level, onDecided);
        }

        foreach (OpenLevel level in open)
        {
            decisions[level.Slot.Number] = level.Decision ?? throw new InvalidOperationException($"Level {level.Slot.Number} was left undecided.");
        }

        return new Assembly(_policy, _base, _bank, _stamp, decisions.Values.ToArray());
    }

    private static void Report(OpenLevel level, Action<LevelDecision>? onDecided)
    {
        if (level.Decision is LevelDecision decided)
        {
            onDecided?.Invoke(decided);
        }
    }

    private LevelDecision.Pinned Pin(LevelSlot slot, FolderLevel? level)
    {
        FolderLevel pinned = level ?? throw new AssemblyException(
            $"Level {slot.Number} stays as it is ({slot.PinnedBecause}), but '{_base.Root}' has no file for it.");
        _chooser.Occupy(slot.Number, Occupant.Of(pinned.Level), pinned.Content?.Board, steered: false);
        return new LevelDecision.Pinned(slot, pinned);
    }

    private LevelDecision.Kept Keep(LevelSlot slot, FolderLevel level)
    {
        CandidateContent content = level.Content ?? throw new InvalidOperationException($"Level {slot.Number} stands without a candidate behind it.");
        _chooser.Occupy(slot.Number, Occupant.Of(level.Level), content.Board, steered: true);
        return new LevelDecision.Kept(slot, level, DecisionKey.Of(slot, slot.Difficulty!.For(content.Features), content.Id));
    }

    /// <summary>
    /// Why the level the base folder has for the number cannot stay as it is; absent when it stands. A level stands
    /// when nothing it was decided from has changed: what the policy and the plan ask of it (its key), the rules it
    /// was measured under and the way its pool and checks were made (its stamp), and when it really is a level of
    /// its number: a board the plan admits, with a limit inside the move range. The last two are asked outright,
    /// because a key only says what was asked of a level, not that the level met it.
    /// </summary>
    private string? WhyNotKept(LevelSlot slot, FolderLevel? level)
    {
        if (level is null)
        {
            return "the base folder has no level for the number";
        }

        if (level.Content is not CandidateContent content)
        {
            return "the level it had is no bank candidate (a fixed start board, or a palette out of colour order)";
        }

        SlotDifficulty ask = slot.Difficulty!.For(content.Features);
        if (level.DecisionKey is not string key)
        {
            return "its report row does not say what it was decided under";
        }

        if (key != DecisionKey.Of(slot, ask, content.Id))
        {
            return "the policy or the plan asks something else of the number (player, share, margin, move range, tier, size or colours)";
        }

        if (!slot.Structure.Admits(content.Features))
        {
            return $"the plan no longer admits its board ({content.Features.Kind} at a number that asks for {slot.Structure.Label})";
        }

        if (level.IsRelaxedOrStale || !ask.Moves.Contains(level.CalibratedMoves))
        {
            return string.Create(CultureInfo.InvariantCulture, $"its limit of {level.CalibratedMoves} moves lies outside its range {ask.Moves}");
        }

        // A level without a stamp comes from a folder no assembly wrote: this assembly takes it over under its own stamp.
        return (level.Stamp ?? _stamp).WhatChangedBy(_stamp);
    }

    /// <summary>A level to decide anew. Its present board, when the bank holds it, is held back for it until it is decided.</summary>
    private OpenLevel Opened(LevelSlot slot, FolderLevel? before, string notKept)
    {
        ChoiceCandidate? incumbent = before?.Content is CandidateContent content ? _choiceCandidates.GetValueOrDefault(content.Id) : null;
        if (incumbent is not null)
        {
            _chooser.Reserve(incumbent.Board, slot.Number);
        }

        return new OpenLevel(slot, before, incumbent, notKept);
    }

    /// <summary>
    /// The level's own board at the amount whose drafted limit lies in the level's move range, looked for in at most
    /// <see cref="AmountMoves"/> moves along the amount grid, each as far as the limit missed; absent when the board
    /// has no amount to move, the moves run out, or no sibling source is at hand.
    /// </summary>
    private ChoiceCandidate? SiblingInRange(OpenLevel level)
    {
        if (_bank?.Siblings is not ISiblingSource siblings || level.Incumbent is not ChoiceCandidate incumbent)
        {
            return null;
        }

        SlotDifficulty ask = level.Slot.Difficulty!.For(incumbent.Features);
        if (_draftLimit(incumbent, ask) is int own && ask.Moves.Contains(own))
        {
            return null; // the limit is not what keeps the board from staying (a twin level uses it, or the plan changed)
        }

        BankCandidate current = BankCandidateOf(incumbent.Id);
        for (int move = 0; move < AmountMoves; move++)
        {
            // A share out of reach within the games' move cap is a limit far too long.
            int limit = _draftLimit(Known(current), ask) ?? MeasurementSettings.Default.MoveCap + 1;
            if (siblings.Sibling(current, AmountGrid.StepsToward(limit, ask.Moves.Mid)) is not BankCandidate next)
            {
                return null;
            }

            current = next;
            if (_draftLimit(Known(current), ask) is int reached && ask.Moves.Contains(reached))
            {
                return Known(current);
            }
        }

        return null;
    }

    /// <summary>The candidate as the chooser knows it; a sibling measured just now is made known first.</summary>
    private ChoiceCandidate Known(BankCandidate candidate)
    {
        if (!_choiceCandidates.TryGetValue(candidate.Content.Id, out ChoiceCandidate? known))
        {
            _choiceCandidates[candidate.Content.Id] = known = ChoiceCandidate.Of(candidate.Content);
            _chooser.Add(known);
        }

        return known;
    }

    private static LevelDecision.Assembled Assemble(OpenLevel level, Choice choice, BankCandidate candidate, FinishedLevel finished)
    {
        LevelSlot slot = level.Slot;
        uint[] pool = finished.PoolSeeds.Select(seed => candidate.Seeds[seed]).ToArray();
        LevelDefinition made = candidate.Content.ToLevel(new LevelId(slot.Number), slot.Tier, finished.Limit, slot.Plan.Palette, pool);
        return new LevelDecision.Assembled(
            slot,
            candidate,
            choice,
            finished,
            made,
            DecisionKey.Of(slot, slot.Difficulty!.For(candidate.Content.Features), candidate.Content.Id),
            SameBoard: level.Before?.Content?.Board == candidate.Content.Board,
            level.NotKept,
            level.TurnedDown.ToArray());
    }

    private LevelDecision.Unfilled LeaveUnfilled(OpenLevel level)
    {
        (UnfilledCause cause, string reason) = WhyUnfilled(level);
        if (level.Before is FolderLevel stale)
        {
            _chooser.Occupy(level.Slot.Number, Occupant.Of(stale.Level), stale.Content?.Board, steered: true);
        }

        return new LevelDecision.Unfilled(level.Slot, level.Before, cause, reason, level.NotKept, level.TurnedDown.ToArray());
    }

    private (UnfilledCause Cause, string Reason) WhyUnfilled(OpenLevel level)
    {
        if (_bank is null)
        {
            return (UnfilledCause.NoBank, "no bank was given, and the level does not stand as it is under this policy");
        }

        if (level.Slot.KeepsBoard)
        {
            string what = level.Incumbent is null ? "the bank does not hold that board (bank import takes it in)"
                : level.TurnedDown.Count > 0 ? "the fresh games turned that board down"
                : "that board's limit under this policy lies outside the move range";
            return (UnfilledCause.RecordedBoard, $"people have recorded attempts on its board, and {what}; no other board is tried for it");
        }

        if (level.Tried >= _policy.Checks.CandidatesPerLevel)
        {
            return (UnfilledCause.TurnedDown, string.Create(CultureInfo.InvariantCulture, $"the fresh games turned down all {level.Tried} candidates tried"));
        }

        Shortage shortage = _chooser.ShortageOf(level.Slot);
        return (shortage.Cause, shortage.Describe(level.Slot));
    }

    private BankCandidate BankCandidateOf(CandidateId id) =>
        _bank?.Catalog.Find(id) ?? throw new InvalidOperationException($"Candidate {id} is not in the bank the assembly reads.");

    /// <summary>A level being decided: what it had before, why that did not stay, what was tried for it, and what it became.</summary>
    private sealed class OpenLevel(LevelSlot slot, FolderLevel? before, ChoiceCandidate? incumbent, string notKept)
    {
        public LevelSlot Slot => slot;

        public FolderLevel? Before => before;

        public ChoiceCandidate? Incumbent => incumbent;

        public string NotKept => notKept;

        public List<string> TurnedDown { get; } = [];

        public int Tried { get; private set; }

        public LevelDecision? Decision { get; set; }

        /// <summary>Puts <paramref name="choice"/> through the fresh-games check: it becomes the level, or is turned down for it.</summary>
        public void Try(Choice choice, Assembler assembler)
        {
            Tried++;
            BankCandidate candidate = assembler.BankCandidateOf(choice.Candidate.Id);
            switch (assembler.FreshGames.Finish(slot, candidate))
            {
                case FinishOutcome.Accepted accepted:
                    Decision = Assemble(this, choice, candidate, accepted.Level);
                    assembler._chooser.Occupy(slot.Number, Occupant.Of(choice.Candidate), choice.Candidate.Board, steered: true);
                    if (incumbent is not null && incumbent.Board != choice.Candidate.Board)
                    {
                        assembler._chooser.Release(incumbent.Board, slot.Number); // the level moved to another board; its old one is free again
                    }

                    break;
                case FinishOutcome.Rejected rejected:
                    assembler._chooser.Reject(slot, choice.Candidate);
                    TurnedDown.Add($"{choice.Candidate.Id}: {rejected.Reason}");
                    break;
                default:
                    throw new InvalidOperationException("Unhandled finish outcome.");
            }
        }
    }
}
