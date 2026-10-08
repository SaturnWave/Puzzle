using System.Diagnostics;
using System.Globalization;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Profiles;

namespace Puzzle.Bank.Growing;

/// <summary>How the grower measures and how hard it tries.</summary>
public sealed record GrowthSettings
{
    public MeasurementSettings Measurement { get; init; } = MeasurementSettings.Default;

    /// <summary>The players every stored candidate is measured with: the whole skill ladder unless narrowed.</summary>
    public IReadOnlyList<PlayerModel> Rungs { get; init; } = SkillLadder.Rungs.Select(PlayerModel.Of).ToArray();

    /// <summary>Play seeds a candidate gets; a level's retry pool is chosen among them.</summary>
    public int Seeds { get; init; } = 64;

    /// <summary>Breadth games per seed and rung.</summary>
    public int Repeats { get; init; } = 2;

    /// <summary>Seeds the aimed-at player plays before a variant is measured in full: enough to tell a limit several moves off, at a sixtieth of the cost.</summary>
    public int ProbeSeeds { get; init; } = 16;

    /// <summary>Amounts tried for one draw before the grower moves on.</summary>
    public int VariantsPerVisit { get; init; } = 6;

    /// <summary>Amount steps on either side of a stored scalable candidate that are measured too, so a later change of player finds the same board at another amount.</summary>
    public int Siblings { get; init; }

    public SpareRule Spare { get; init; } = SpareRule.Default;
}

/// <summary>When to stop growing: after a time, after a number of stored candidates, or whichever comes first.</summary>
public sealed record GrowthBudget(TimeSpan? Time, int? Candidates)
{
    public bool IsSpent(TimeSpan elapsed, int stored) =>
        (Time is TimeSpan time && elapsed >= time) || (Candidates is int candidates && stored >= candidates);
}

/// <summary>Why a run of the grower ended. Closed set.</summary>
public enum GrowthEnd
{
    BudgetSpent,

    /// <summary>Every class has the boards it needs for every aim.</summary>
    DeepEnough,

    /// <summary>
    /// Classes are still short of boards, but only of kinds that draws have not given them: a family set that almost
    /// never lands in the class's move range. More time would not help; another range or another rule would.
    /// </summary>
    OutOfReach,

    /// <summary>Stopped from outside; the candidate in hand was finished first.</summary>
    Stopped,
}

/// <param name="Fits">Visits that stored a candidate whose limit landed in the range aimed at.</param>
/// <param name="Probes">Variants given up after a few probe games, without a full measurement.</param>
/// <param name="OutOfReach">For a run that ended <see cref="GrowthEnd.OutOfReach"/>: each class still short of boards, with the kinds draws do not give it.</param>
public sealed record GrowthSummary(GrowthEnd End, int Visits, int Stored, int Fits, int Probes, TimeSpan Elapsed, IReadOnlyList<string> OutOfReach);

/// <summary>What one visit of the grower came to.</summary>
/// <param name="Stored">Candidates stored (more than one with siblings).</param>
/// <param name="Fit">A stored candidate's limit lies in the range aimed at.</param>
/// <param name="Probes">Variants given up after their probe games.</param>
/// <param name="Last">How the last variant ended, in words.</param>
public sealed record VisitResult(long Draw, string Target, string Aim, int Stored, bool Fit, int Probes, string Last);

/// <summary>
/// Grows the bank where it is thinnest. A visit takes the class and aim that are shortest of boards and the family
/// mix that class lacks most, draws a board for it, finds the amount at which the aimed-at player's limit lands in
/// the class's range with a few probe games per try, and then measures that one candidate on the whole ladder and
/// stores it with its games. What it draws next follows from the bank's content alone, so a stopped run is resumed
/// by starting it again: no draw is made twice and nothing half-written counts.
/// </summary>
public sealed class Grower(BankCatalog catalog, BankWriter writer, Demand demand, BankIdentity bank, EpochId epoch, GrowthSettings settings)
{
    /// <summary>Stream of a draw's seed that decides what the drawing code does not: the collect goals and where the amount search starts.</summary>
    private const ulong VisitStream = 7;

    private readonly LimitBook _limits = new(epoch);
    private readonly VisitTrail _trail = VisitTrail.Of(catalog.Draws);
    private readonly Remeasurer _remeasurer = new(writer, epoch, settings);

    private BankCatalog Catalog => catalog;

    private BankWriter Writer => writer;

    private BankIdentity Bank => bank;

    private EpochId Epoch => epoch;

    private GrowthSettings Settings => settings;

    /// <summary>
    /// A candidate's play seeds follow from its identity: distinct 32-bit seeds derived from its id, so the same
    /// candidate gets the same seeds whoever stores it.
    /// </summary>
    public static uint[] PlaySeedsOf(CandidateId id, int count)
    {
        var seeds = new List<uint>(count);
        for (ulong i = 0; seeds.Count < count; i++)
        {
            uint seed = (uint)Rng.Derive(id.Seed, i);
            if (!seeds.Contains(seed))
            {
                seeds.Add(seed);
            }
        }

        return [.. seeds];
    }

    /// <param name="onVisit">Told of every finished visit, for a progress line.</param>
    /// <exception cref="BankWriteException">The bank's disk failed; everything stored before is in the bank.</exception>
    public GrowthSummary Run(GrowthBudget budget, CancellationToken stop, Action<VisitResult>? onVisit = null)
    {
        var clock = Stopwatch.StartNew();
        (int visits, int stored, int fits, int probes) = (0, 0, 0, 0);
        GrowthSummary EndedBy(GrowthEnd end, IReadOnlyList<string>? outOfReach = null) => new(end, visits, stored, fits, probes, clock.Elapsed, outOfReach ?? []);
        while (true)
        {
            if (stop.IsCancellationRequested)
            {
                return EndedBy(GrowthEnd.Stopped);
            }

            if (budget.IsSpent(clock.Elapsed, stored))
            {
                return EndedBy(GrowthEnd.BudgetSpent);
            }

            if (ShortOfBoards() is not { Count: > 0 } shortOfBoards)
            {
                return EndedBy(GrowthEnd.DeepEnough);
            }

            if (NextTarget(shortOfBoards) is not Target target)
            {
                return EndedBy(GrowthEnd.OutOfReach, shortOfBoards.Select(DescribeOutOfReach).ToArray());
            }

            VisitResult result = new Visit(this, target).Run();
            _trail.Note(target.Aim.Label, target.Label, result.Fit);
            visits++;
            stored += result.Stored;
            fits += result.Fit ? 1 : 0;
            probes += result.Probes;
            onVisit?.Invoke(result);
        }
    }

    private static string DescribeOutOfReach(ClassSupply row) => string.Create(
        CultureInfo.InvariantCulture,
        $"{row.Class.Label} for {row.Aim.Label}: {row.Deficit} of {row.Needed} boards missing; draws do not give it {string.Join(", ", KindMix.SetsFor(row.Class.Structure).Select(KindMix.LabelOf))}");

    /// <summary>The classes and aims that still lack boards, in a fixed order.</summary>
    private IReadOnlyList<ClassSupply> ShortOfBoards() => SupplyTable.Of(catalog, demand, settings.Spare, _limits)
        .Where(row => row.Deficit > 0)
        .OrderBy(row => row.Aim.Label, StringComparer.Ordinal)
        .ThenBy(row => row.Class.Label, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// The thinnest class and aim that a draw can still help, and for it the family mix furthest below its target
    /// share. A target that keeps missing counts for less each time, so a kind that is hard to make does not hold
    /// the others up. Absent when every class still short of boards asks only for kinds that draws do not give it
    /// (<see cref="SetsWorthDrawing"/>): more visits would be spent without a board.
    /// </summary>
    private Target? NextTarget(IReadOnlyList<ClassSupply> shortOfBoards)
    {
        ClassSupply? thinnest = shortOfBoards
            .Where(row => SetsWorthDrawing(row).Count > 0)
            .MaxBy(row => row.Thinness / (1 + (_trail.MissesInARow(Target.ClassKeyOf(row)) / 4.0)));
        if (thinnest is null)
        {
            return null;
        }

        IReadOnlyList<ObstacleKind> families = SetsWorthDrawing(thinnest).MaxBy(set => Urgency(thinnest, set))
            ?? throw new InvalidOperationException($"No family set is worth drawing for {thinnest.Class.Label}.");
        return new Target(thinnest, families);
    }

    /// <summary>
    /// The family sets a visit for the class may ask for. A set that has had its visits and almost never gave a
    /// board to classes that ask the same length of it (<see cref="VisitTrail.GivesTooLittle"/>) sits out: a kind the
    /// move range cannot hold (a drop-item board in a short range) would otherwise take every visit, because it
    /// stays the set furthest below its share for ever.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<ObstacleKind>> SetsWorthDrawing(ClassSupply supply) => KindMix.SetsFor(supply.Class.Structure)
        .Where(set => !_trail.GivesTooLittle(supply.Aim.Label, new Target(supply, set).Label))
        .ToArray();

    /// <summary>
    /// The share of the boards a family set should have in the class that is still missing (1 = none yet), so the sets
    /// fill up side by side and a rare mix does not wait for the common ones; discounted by the set's misses in a row.
    /// </summary>
    private double Urgency(ClassSupply supply, IReadOnlyList<ObstacleKind> set)
    {
        int have = supply.Fitting
            .Where(candidate => candidate.Content.Features.Families.SequenceEqual(set))
            .Select(candidate => candidate.Content.Board)
            .Distinct()
            .Count();
        double wanted = KindMix.TargetShare(supply.Class.Structure, set, demand.KindTargets) * supply.Needed;
        if (wanted <= 0)
        {
            return double.NegativeInfinity; // a kind the policy wants none of is the last to be drawn
        }

        double missing = (wanted - have) / wanted;
        return missing > 0 ? missing / (1 + _trail.MissesInARow(new Target(supply, set).KindKey)) : missing;
    }

    /// <summary>What a visit aims at: a class and aim, and the families its board should carry.</summary>
    private sealed record Target(ClassSupply Supply, IReadOnlyList<ObstacleKind> Families)
    {
        public PlanClass Class => Supply.Class;

        public PlayerMix Aim => Supply.Aim;

        /// <summary>The range a board with these families should land its limit in: the class's, or the range of its own that the policy gives the kind.</summary>
        public MoveRange Moves => Class.MovesFor(Families);

        /// <summary>The class as this family set sees it and the set, as the draw records name the target.</summary>
        public string Label => VisitTrail.TargetLabel(Class.LabelFor(Families), KindMix.LabelOf(Families));

        public string KindKey => VisitTrail.KeyOf(Aim.Label, Label);

        /// <summary>
        /// The key of the class's misses in a row. A family set with a range of its own is recorded under another
        /// label (<see cref="PlanClass.LabelFor"/>), so its misses do not count against the class.
        /// </summary>
        public static string ClassKeyOf(ClassSupply supply) => VisitTrail.KeyOf(supply.Aim.Label, supply.Class.Label);
    }

    /// <summary>One draw and the search for its amount.</summary>
    private sealed class Visit(Grower grower, Target target)
    {
        private readonly long _draw = grower.Catalog.NextDrawIndex;
        private readonly HashSet<CandidateId> _tried = [];
        private int _variant;
        private int _stored;
        private int _probes;
        private bool _fit;

        private ulong Seed => Rng.Derive(grower.Bank.BankSeed, (ulong)_draw);

        private GrowthSettings Settings => grower.Settings;

        public VisitResult Run()
        {
            var dice = new Rng(Rng.Derive(Seed, VisitStream));
            int collectGoals = CollectGoals(dice);
            int densityStep = OnGrid(StartingDensity(target.Class.Tier) + dice.NextInt(3) - 1);
            int amountStep = 0;
            string last = "no variant";
            for (; _variant < Settings.VariantsPerVisit; _variant++)
            {
                Step step = Try(SpecFor(collectGoals, densityStep), amountStep);
                last = step.Description;
                if (step.MissedLimit is not int missed)
                {
                    break;
                }

                // A board with a goal that measures an amount keeps its layout and moves the goal; any other is drawn again denser or sparser.
                int steps = AmountGrid.StepsToward(missed, target.Moves.Mid);
                (int amountBefore, int densityBefore) = (amountStep, densityStep);
                amountStep = step.Scalable ? OnGrid(amountStep + steps) : amountStep;
                densityStep = step.Scalable ? densityStep : OnGrid(densityStep + steps);
                if ((amountStep, densityStep) == (amountBefore, densityBefore))
                {
                    break; // the grid ends here
                }
            }

            return new VisitResult(_draw, target.Label, target.Aim.Label, _stored, _fit, _probes, last);
        }

        private static int OnGrid(int step) => Math.Clamp(step, AmountGrid.SmallestStep, AmountGrid.LargestStep);

        /// <summary>Collect goals beside the families, in the proportions of the generator: one or two on a board without obstacles, sometimes one beside them.</summary>
        private int CollectGoals(Rng dice) => target.Families.Count == 0
            ? (dice.NextInt(100) < 60 ? 1 : 2)
            : (target.Families.Count < LevelDefinition.MaxGoals && dice.NextInt(100) < 30 ? 1 : 0);

        /// <summary>Where the generator's tier weights (0.7 for Easy to 1.5 for Super Hard) lie on the amount grid.</summary>
        private static int StartingDensity(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Easy => -2,
            DifficultyTier.Normal => 0,
            DifficultyTier.Hard => 1,
            DifficultyTier.SuperHard => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
        };

        private DrawSpec SpecFor(int collectGoals, int densityStep) => new()
        {
            Size = target.Class.Structure.Size,
            PlainShape = target.Class.Structure.PlainShape,
            Colors = target.Class.Structure.Colors,
            Families = target.Families.Select(kind => new FamilyDraw(kind, target.Class.Structure.Unlocked(kind))).ToArray(),
            CollectGoals = collectGoals,
            DensityStep = densityStep,
            FullJelly = target.Class.Tier >= DifficultyTier.Hard, // as the generator draws today: full-board jelly from Hard on
        };

        /// <summary>One variant: draw it, see whether it is the kind aimed at and new, probe it, and measure and store it when the probe lands.</summary>
        private Step Try(DrawSpec spec, int amountStep)
        {
            CandidateContent? board;
            try
            {
                board = BoardDrawer.TryDraw(spec, Seed);
            }
            catch (LevelFormatException ex)
            {
                return End(spec, amountStep, DrawOutcome.Invalid, content: null, ex.Message);
            }

            if (board is null)
            {
                return End(spec, amountStep, DrawOutcome.Unusable, content: null, detail: null);
            }

            var variant = new Variant(spec, amountStep, board, amountStep == 0 ? board : AmountGrid.AtStep(board, AmountGrid.BaseOf(board), amountStep));
            CandidateContent content = variant.Content;
            if (!target.Class.Structure.Admits(content.Features) || !content.Features.Families.SequenceEqual(target.Families))
            {
                return End(spec, amountStep, DrawOutcome.OffTarget, content, content.Features.Kind);
            }

            if (!_tried.Add(content.Id))
            {
                return End(spec, amountStep, DrawOutcome.Duplicate, content, "the amount does not move this board");
            }

            if (grower.Catalog.Find(content.Id) is BankCandidate known)
            {
                // A board held from before a rule change has no games of this epoch: it gets them here, or it would stand in the way of its own redraw for ever.
                grower._remeasurer.Remeasure(known);
                return Missed(variant, DrawOutcome.Duplicate, probeLimit: null, grower._limits.DraftLimit(known, target.Aim, target.Class.Share));
            }

            try
            {
                return ProbeThenMeasure(variant);
            }
            catch (LevelInitializationException ex)
            {
                return End(spec, amountStep, DrawOutcome.Invalid, content, ex.Message);
            }
        }

        private Step ProbeThenMeasure(Variant variant)
        {
            uint[] seeds = PlaySeedsOf(variant.Content.Id, Settings.Seeds);
            PlayerModel[] aimedAt = target.Aim.Parts.Select(part => PlayerModel.Of(RungOf(part))).ToArray();
            int? probeLimit = Read(Measure(variant.Content, seeds, Settings.ProbeSeeds, aimedAt));
            if (probeLimit is not int probe || !target.Moves.Contains(probe))
            {
                _probes++;
                return Missed(variant, DrawOutcome.ProbedOut, probeLimit, knownLimit: null);
            }

            int? limit = Store(variant, seeds, probeLimit, detail: null);
            StoreSiblings(variant);
            string reached = limit is int moves ? moves.ToString(CultureInfo.InvariantCulture) : "out of reach";
            return new Step($"stored {variant.Content.Features.Kind}, limit {reached}", MissedLimit: null, variant.Content.Features.IsScalable);
        }

        /// <summary>Measures the candidate on the whole ladder and stores it with its games and its draw record, in one write.</summary>
        /// <returns>The aimed-at player's draft limit on the stored games.</returns>
        private int? Store(Variant variant, uint[] seeds, int? probeLimit, string? detail)
        {
            var clock = Stopwatch.StartNew();
            RungGames[] games = Measure(variant.Content, seeds, seeds.Length, Settings.Rungs);
            int? limit = Read(games);
            bool fit = limit is int moves && target.Moves.Contains(moves);
            var origin = new CandidateOrigin.Drawn(_draw, _variant, Seed, variant.Spec, BoardDrawer.DrawCodeVersion);
            var candidate = new BankCandidate(variant.Content, seeds, AmountGrid.BaseOf(variant.Board), variant.AmountStep, origin, DateTime.UtcNow);
            DrawRecord record = Record(variant.Spec, variant.AmountStep, DrawOutcome.Stored, variant.Content, probeLimit, limit, fit, clock.Elapsed.TotalSeconds, detail);
            grower.Writer.Store(candidate, games, record);
            _stored++;
            _fit |= fit;
            return limit;
        }

        /// <summary>The same board at the neighbouring amounts, where the grid gives another candidate that the bank does not hold.</summary>
        private void StoreSiblings(Variant stored)
        {
            if (!stored.Content.Features.IsScalable)
            {
                return;
            }

            AmountBase amounts = AmountGrid.BaseOf(stored.Board);
            IEnumerable<int> steps = Enumerable.Range(1, Settings.Siblings)
                .SelectMany(distance => new[] { stored.AmountStep - distance, stored.AmountStep + distance })
                .Where(step => step is >= AmountGrid.SmallestStep and <= AmountGrid.LargestStep);
            foreach (int step in steps)
            {
                var sibling = new Variant(stored.Spec, step, stored.Board, AmountGrid.AtStep(stored.Board, amounts, step));
                if (_tried.Add(sibling.Content.Id) && !grower.Catalog.Contains(sibling.Content.Id))
                {
                    _variant++;
                    Store(sibling, PlaySeedsOf(sibling.Content.Id, Settings.Seeds), probeLimit: null, detail: "sibling");
                }
            }
        }

        private RungGames[] Measure(CandidateContent content, uint[] seeds, int seedCount, IReadOnlyList<PlayerModel> players) => LadderMeasurer.Measure(new MeasurementRequest
        {
            Candidate = content,
            Seeds = seeds,
            SeedIndices = Enumerable.Range(0, Math.Min(seedCount, seeds.Length)).ToArray(),
            Players = players,
            Purpose = GamePurpose.Breadth,
            RepeatFrom = GamePurpose.Breadth.FirstRepeat(),
            RepeatCount = Settings.Repeats,
            Settings = Settings.Measurement,
            Epoch = grower.Epoch,
        });

        /// <summary>The aimed-at player's draft limit for the class's share on these games.</summary>
        private int? Read(RungGames[] games) =>
            new LadderReading(games.Select(batch => new StoredGames(batch.Header, batch.Finishes(), Location: null))).DraftLimit(target.Aim, target.Class.Share);

        /// <summary>
        /// A variant whose limit is known without a full measurement: from its probe games, or because the bank
        /// already holds it. Outside the range, the visit goes on with another amount; inside (a held candidate), it
        /// ends, because the bank has nothing to gain.
        /// </summary>
        private Step Missed(Variant variant, DrawOutcome outcome, int? probeLimit, int? knownLimit)
        {
            grower.Writer.Note(Record(variant.Spec, variant.AmountStep, outcome, variant.Content, probeLimit, knownLimit, fit: false, seconds: 0, detail: null));
            int reached = probeLimit ?? knownLimit ?? Settings.Measurement.MoveCap + 1; // out of reach within the cap: far too long
            bool scalable = variant.Content.Features.IsScalable;
            return target.Moves.Contains(reached)
                ? new Step($"{outcome}: the bank holds this candidate", MissedLimit: null, scalable)
                : new Step($"{outcome} at limit {reached}", reached, scalable);
        }

        /// <summary>A variant that ends the visit without a limit to steer by.</summary>
        private Step End(DrawSpec spec, int amountStep, DrawOutcome outcome, CandidateContent? content, string? detail)
        {
            grower.Writer.Note(Record(spec, amountStep, outcome, content, probeLimit: null, limit: null, fit: false, seconds: 0, detail));
            return new Step(detail is null ? outcome.ToString() : $"{outcome}: {detail}", MissedLimit: null, Scalable: false);
        }

        private DrawRecord Record(DrawSpec spec, int amountStep, DrawOutcome outcome, CandidateContent? content, int? probeLimit, int? limit, bool fit, double seconds, string? detail) => new(
            _draw,
            _variant,
            target.Label,
            target.Aim.Label,
            RecordMapping.ToRecord(spec),
            Seed,
            amountStep,
            outcome,
            content?.Id.ToString(),
            probeLimit,
            limit,
            fit,
            seconds,
            detail);

        private static SkillProfile RungOf(MixPart part) => SkillLadder.TryFind(part.RungName, out SkillProfile? rung)
            ? rung
            : throw new InvalidOperationException($"{part.RungName} is not on the ladder (a player mix only holds rungs).");

        /// <param name="MissedLimit">The limit that missed the range, when the amount should move and another variant be tried; absent when the visit ends here.</param>
        private sealed record Step(string Description, int? MissedLimit, bool Scalable);

        /// <summary>A board at one amount: the board as drawn (step 0 of its amounts) and the candidate at <paramref name="AmountStep"/>.</summary>
        private sealed record Variant(DrawSpec Spec, int AmountStep, CandidateContent Board, CandidateContent Content);
    }
}

/// <summary>
/// What the visits for a target came to, read from the draw records: how often in a row they missed, and how many of
/// them gave a board at all. A target is a class and aim, or a family set within them. It is part of the bank's
/// content, so a restarted grower knows what the stopped one learnt.
/// </summary>
internal sealed class VisitTrail
{
    private const char Separator = '|';

    /// <summary>Visits a target gets before its record is believed.</summary>
    private const int VisitsToJudge = 20;

    /// <summary>Below one board in this many visits a target gives too little to go on asking for it.</summary>
    private const int VisitsPerBoardAtMost = 20;

    private readonly Dictionary<string, int> _missesInARow = [];

    /// <summary>Visits and fits by aim, by what the class asks (<see cref="PlanClass.AskOf"/>) and by family set.</summary>
    private readonly Dictionary<string, (int Visits, int Fits)> _records = [];

    public static VisitTrail Of(IEnumerable<DrawRecord> draws)
    {
        var trail = new VisitTrail();
        foreach (IGrouping<long, DrawRecord> visit in draws.GroupBy(draw => draw.Draw).OrderBy(visit => visit.Key))
        {
            trail.Note(visit.First().Aim, visit.First().Target, visit.Any(draw => draw.Fit));
        }

        return trail;
    }

    public static string TargetLabel(string classLabel, string familiesLabel) => $"{classLabel}{Separator}{familiesLabel}";

    public static string KeyOf(string aim, string target) => $"{aim}{Separator}{target}";

    public int MissesInARow(string key) => _missesInARow.GetValueOrDefault(key);

    /// <summary>
    /// The visits for this family set, in every class that asks the same of a board's length (colours, tier, share
    /// and move range; <see cref="PlanClass.AskOf"/>), number <see cref="VisitsToJudge"/> or more, and fewer than
    /// one in <see cref="VisitsPerBoardAtMost"/> of them gave a board. Whether a drop-item board fits a range does
    /// not depend on how strong the class's boxes may be, so the classes learn it together instead of twenty visits
    /// each. Unlike misses in a row this is a rate: a kind that fits one time in six also misses twenty times
    /// running sooner or later, and must not be given up for that.
    /// </summary>
    /// <param name="target">A target as <see cref="TargetLabel"/> writes it.</param>
    public bool GivesTooLittle(string aim, string target) =>
        _records.TryGetValue(RecordKeyOf(aim, target), out (int Visits, int Fits) record)
        && record.Visits >= VisitsToJudge
        && record.Fits * VisitsPerBoardAtMost < record.Visits;

    /// <param name="target">A target as <see cref="TargetLabel"/> writes it; its class is counted too.</param>
    public void Note(string aim, string target, bool fit)
    {
        string classLabel = target[..target.LastIndexOf(Separator)];
        foreach (string key in new[] { KeyOf(aim, target), KeyOf(aim, classLabel) })
        {
            _missesInARow[key] = fit ? 0 : MissesInARow(key) + 1;
        }

        string recordKey = RecordKeyOf(aim, target);
        (int visits, int fits) = _records.GetValueOrDefault(recordKey);
        _records[recordKey] = (visits + 1, fits + (fit ? 1 : 0));
    }

    private static string RecordKeyOf(string aim, string target)
    {
        int split = target.LastIndexOf(Separator);
        return KeyOf(aim, TargetLabel(PlanClass.AskOf(target[..split]), target[(split + 1)..]));
    }
}
