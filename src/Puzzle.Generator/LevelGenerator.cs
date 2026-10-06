using System.Diagnostics;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Composition;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator;

/// <summary>Board size + outline + goal kinds: two levels with equal signatures look alike to a player.</summary>
public sealed record LevelSignature(int Width, int Height, string Cells, string GoalKinds)
{
    public static LevelSignature Of(LevelDefinition level) => new(
        level.Layout.Shape.Width,
        level.Layout.Shape.Height,
        string.Concat(level.Layout.ToLayers().Cells),
        string.Join(',', level.Goals.Select(g => g.GetType().Name).OrderBy(n => n)));
}

/// <summary>How a level met its casual floor.</summary>
/// <param name="Bot">The floor bot whose games gave the measured win rate.</param>
/// <param name="WinRate">The casual floor and the floor bot's win rate at the move limit.</param>
public sealed record FloorCheck(string Bot, WinRateCheck WinRate);

/// <summary>Numbers for the generation report (one row per level).</summary>
public sealed record LevelMetrics
{
    public required int Number { get; init; }

    public required DifficultyTier Tier { get; init; }

    public required string Size { get; init; }

    public required int Playable { get; init; }

    public required int Colors { get; init; }

    public required string Style { get; init; }

    public required string Goals { get; init; }

    /// <summary>
    /// The final limit, the one the level file carries: <see cref="CalibratedMoves"/> plus <see cref="MoveMargin"/>.
    /// Less than that only for a relaxed level whose margin would pass the calibration move cap; it stops at the cap.
    /// </summary>
    public required int MoveLimit { get; init; }

    /// <summary>
    /// The calibrated limit: the smallest at which the calibration bot reached <see cref="TargetWinRate"/> (and the
    /// floor bot the casual floor), checked on fresh seeds.
    /// </summary>
    public required int CalibratedMoves { get; init; }

    /// <summary>The profile's move margin when the level was generated.</summary>
    public required MoveMargin MoveMargin { get; init; }

    /// <summary>The final limit stopped at the calibration move cap, short of the whole margin (see <see cref="MoveLimit"/>).</summary>
    public bool MarginCutByMoveCap => MoveLimit < MoveMargin.Soften(CalibratedMoves);

    /// <summary>The range <see cref="CalibratedMoves"/> was aimed at.</summary>
    public required MoveRange MoveRange { get; init; }

    /// <summary>The player model whose games set <see cref="CalibratedMoves"/> and balanced the seed pool.</summary>
    public required string CalibrationBot { get; init; }

    /// <summary>
    /// What the calibration bot was to win at <see cref="CalibratedMoves"/>. <see cref="WinRate"/> is read at the final
    /// limit, so with a move margin it sits above the target by what the extra moves give.
    /// </summary>
    public required double TargetWinRate { get; init; }

    /// <summary>Calibration bot's win rate at the final limit, measured on fresh seeds.</summary>
    public required double WinRate { get; init; }

    /// <summary>
    /// Calibration bot's win rate with the "+5 moves" offer (final limit + <see cref="ExtraMovesOffer.Moves"/>),
    /// read from the same games as <see cref="WinRate"/>. Absent when that limit goes past the calibration move cap.
    /// </summary>
    public required double? WinRateWithExtraMoves { get; init; }

    /// <summary>
    /// The casual floor, the floor bot and its win rate at the final limit, measured on the same fresh seeds as
    /// <see cref="WinRate"/>. Absent on a tier without a floor.
    /// </summary>
    public required FloorCheck? Floor { get; init; }

    /// <summary>The floor bot needed more moves than the target did, so the casual floor decided the calibrated limit.</summary>
    public required bool FloorSetLimit { get; init; }

    public required int? MovesP50 { get; init; }

    public required int? MovesP90 { get; init; }

    public required double NearMiss { get; init; }

    /// <summary>Win rate of each report bot (casual, expert, ...) at the final limit.</summary>
    public required IReadOnlyDictionary<string, double> BotWinRates { get; init; }

    public required int PoolSize { get; init; }

    public required double PoolMinWinRate { get; init; }

    public required double PoolMaxWinRate { get; init; }

    public required int CandidatesTried { get; init; }

    /// <summary>No candidate fit the move range; the closest one was kept (its limit still honors the win rate).</summary>
    public required bool Relaxed { get; init; }

    public required double Seconds { get; init; }
}

public sealed record GeneratedLevel(LevelDefinition Level, LevelMetrics Metrics);

/// <summary>No candidate of a level could even be calibrated (every board was broken).</summary>
public sealed class GenerationFailedException(string message) : Exception(message);

/// <summary>
/// Produces one level: plan it, draw candidates from the portfolio, calibrate them with the plan's calibration bot
/// (and the floor bot on a tier with a casual floor), keep the first that fits (searching a second time, board by
/// board, when none does; the closest when that fails too), add the plan's move margin to its calibrated limit, then
/// build its equal-difficulty seed pool and report bot win rates at that final limit.
/// </summary>
public sealed class LevelGenerator(GenerationProfile profile)
{
    private const ulong ReportStream = 500;
    private const ulong ClosestCheckStream = 401;

    /// <summary>The obstacle amounts the second search may give a board, as multiples of the planned amount.</summary>
    private const double SparsestAmount = 1.0 / 3;
    private const double DensestAmount = 3.0;
    private const int AmountSteps = 4;

    private readonly LevelPlanner _planner = new(profile);
    private readonly LevelCalibrator _calibrator = new(profile);

    public LevelPlan Plan(int number) => _planner.Plan(number);

    /// <param name="recent">Signatures of the levels just before this one, for the novelty check.</param>
    /// <exception cref="GenerationFailedException">Every candidate was broken.</exception>
    public GeneratedLevel Generate(int number, IReadOnlyList<LevelSignature> recent)
    {
        var clock = Stopwatch.StartNew();
        LevelPlan plan = _planner.Plan(number);
        var misses = new Misses(plan);
        KeptCandidate kept = FirstFit(plan, recent, misses) ?? FirstFitAtItsOwnAmount(plan, misses) ?? Closest(plan, misses);
        return Finish(plan, kept, clock);
    }

    /// <summary>
    /// The first search: one board per attempt, the obstacle amount handed on from each to the next (less after a
    /// too-long one, more after a too-short one). The first half of the attempts also turns down a board too similar
    /// to a recent level.
    /// </summary>
    private KeptCandidate? FirstFit(LevelPlan plan, IReadOnlyList<LevelSignature> recent, Misses misses)
    {
        double density = 1.0;
        int maxCandidates = profile.Calibration.MaxCandidates;
        for (int attempt = 0; attempt < maxCandidates; attempt++)
        {
            if (CandidateFactory.TryCreate(plan, attempt, density) is not Candidate candidate)
            {
                misses.Add("unusable board");
                continue;
            }

            switch (_calibrator.Calibrate(candidate, plan))
            {
                case CalibrationOutcome.Accepted accepted when attempt >= maxCandidates / 2 || !recent.Contains(LevelSignature.Of(accepted.Level)):
                    return Fit(accepted, candidate, misses);
                case CalibrationOutcome.Accepted:
                    misses.Add("too similar to a recent level");
                    break;
                case CalibrationOutcome.OutOfRange outOfRange:
                    misses.Add(outOfRange, candidate.Style);
                    density *= outOfRange.TooLong ? 0.7 : 1.35;
                    break;
                case CalibrationOutcome.Rejected rejected:
                    misses.Add(rejected.Reason);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled calibration outcome.");
            }
        }

        return null;
    }

    /// <summary>
    /// The second search, for a level whose first one found nothing. The first hands one obstacle amount from board
    /// to board, and a family whose length that amount does not decide (items to drop, a jelly band) can drive it down
    /// until every other board has next to nothing on it. Here each of those boards gets an amount of its own
    /// (<see cref="FitByAmount"/>). A board like a recent level's is taken too: better that than a level out of its
    /// move range.
    /// </summary>
    private KeptCandidate? FirstFitAtItsOwnAmount(LevelPlan plan, Misses misses)
    {
        for (int attempt = 0; attempt < profile.Calibration.MaxCandidates; attempt++)
        {
            if (FitByAmount(plan, attempt, misses) is KeptCandidate fit)
            {
                return fit;
            }
        }

        return null;
    }

    /// <summary>
    /// One board of the second search: starting from the planned obstacle amount, the amount moves halfway (as a
    /// ratio) toward the side its limit missed, until the board fits or the steps run out.
    /// </summary>
    private KeptCandidate? FitByAmount(LevelPlan plan, int attempt, Misses misses)
    {
        (double tooShortAt, double tooLongAt) = (SparsestAmount, DensestAmount);
        for (int step = 0; step < AmountSteps; step++)
        {
            double density = Math.Sqrt(tooShortAt * tooLongAt);
            if (CandidateFactory.TryCreate(plan, attempt, density) is not Candidate candidate)
            {
                misses.Add("unusable board");
                return null;
            }

            switch (_calibrator.Calibrate(candidate, plan))
            {
                case CalibrationOutcome.Accepted accepted:
                    return Fit(accepted, candidate, misses);
                case CalibrationOutcome.OutOfRange { TooLong: true } tooLong:
                    misses.Add(tooLong, candidate.Style);
                    tooLongAt = density;
                    break;
                case CalibrationOutcome.OutOfRange tooShort:
                    misses.Add(tooShort, candidate.Style);
                    tooShortAt = density;
                    break;
                case CalibrationOutcome.Rejected rejected:
                    misses.Add(rejected.Reason);
                    return null;
                default:
                    throw new InvalidOperationException("Unhandled calibration outcome.");
            }
        }

        return null;
    }

    /// <summary>
    /// The level when no board fit: the candidate whose limit came closest to the move range, marked relaxed. That
    /// limit was read from the calibration games, so it is checked on fresh seeds like an accepted one's and, when it
    /// does not stand there, read again from those games: a relaxed level leaves its move range, not its win rate.
    /// </summary>
    /// <exception cref="GenerationFailedException">Every candidate was broken.</exception>
    private KeptCandidate Closest(LevelPlan plan, Misses misses)
    {
        if (misses.Closest is not { } closest)
        {
            throw new GenerationFailedException($"Level {plan.Number}: no usable candidate ({string.Join("; ", misses.Reasons)}).");
        }

        LevelDefinition level = closest.Outcome.Level;
        LimitEvidence verification = _calibrator.Measure(level, plan, profile.Calibration.VerifyRuns, Rng.Derive(plan.Seed, ClosestCheckStream));
        var calibrated = new LimitChoice(level.MoveLimit, closest.Outcome.FloorSetLimit);
        LimitChoice limit = verification.Confirms(calibrated.Moves, profile.Calibration.WinRateTolerance)
            ? calibrated
            : verification.ReadLimit() ?? calibrated;
        return new KeptCandidate(level.WithMoveLimit(limit.Moves), verification, limit.FloorSetLimit, closest.Style, misses.Count, Relaxed: true);
    }

    private static KeptCandidate Fit(CalibrationOutcome.Accepted accepted, Candidate candidate, Misses misses) =>
        new(accepted.Level, accepted.Verification, accepted.FloorSetLimit, candidate.Style, misses.Count + 1, Relaxed: false);

    /// <summary>
    /// Turns the kept candidate into the level: its calibrated limit becomes the final one, and everything from here on
    /// (the seed pool, every reported win rate) is measured at the final limit, the one a player gets.
    /// </summary>
    private GeneratedLevel Finish(LevelPlan plan, KeptCandidate kept, Stopwatch clock)
    {
        LevelReport verification = kept.Verification.Target.Games;
        int calibratedMoves = kept.Level.MoveLimit;
        LevelDefinition withFinalLimit = kept.Level.WithMoveLimit(FinalLimit(plan, calibratedMoves));
        IReadOnlyList<SeedDifficulty> poolSeeds = BuildPool(withFinalLimit, plan.CalibrationBot, verification.WinRateAt(withFinalLimit.MoveLimit));
        LevelDefinition final = withFinalLimit.WithAttempts(new AttemptPolicy { SeedPool = poolSeeds.Select(d => d.Seed).ToArray() });
        var botWinRates = new Dictionary<string, double>();
        for (int i = 0; i < profile.ReportBots.Count; i++)
        {
            string bot = profile.ReportBots[i];
            LevelReport report = _calibrator.Simulate(final, bot, profile.Calibration.ReportRuns, Rng.Derive(plan.Seed, ReportStream + (ulong)i));
            botWinRates[bot] = report.WinRateAt(final.MoveLimit);
        }

        var metrics = new LevelMetrics
        {
            Number = plan.Number,
            Tier = plan.Tier,
            Size = plan.Size.ToString(),
            Playable = final.Layout.Shape.PlayableCount,
            Colors = final.Spawn.Colors,
            Style = kept.Style,
            Goals = string.Join(" ", final.Goals.Select(Describe)),
            MoveLimit = final.MoveLimit,
            CalibratedMoves = calibratedMoves,
            MoveMargin = plan.MoveMargin,
            MoveRange = plan.Moves,
            CalibrationBot = plan.CalibrationBot,
            TargetWinRate = plan.TargetWinRate,
            WinRate = verification.WinRateAt(final.MoveLimit),
            WinRateWithExtraMoves = verification.WinRateWithExtraMoves(final.MoveLimit),
            Floor = kept.Verification.Floor is MeasuredShare floor ? new FloorCheck(profile.FloorBot, floor.CheckAt(final.MoveLimit)) : null,
            FloorSetLimit = kept.FloorSetLimit,
            MovesP50 = verification.MovesNeeded(0.5),
            MovesP90 = verification.MovesNeeded(0.9),
            NearMiss = verification.NearMissRateAt(final.MoveLimit),
            BotWinRates = botWinRates,
            PoolSize = poolSeeds.Count,
            PoolMinWinRate = poolSeeds.Min(d => d.WinRateAtLimit),
            PoolMaxWinRate = poolSeeds.Max(d => d.WinRateAtLimit),
            CandidatesTried = kept.CandidatesTried,
            Relaxed = kept.Relaxed,
            Seconds = clock.Elapsed.TotalSeconds,
        };
        return new GeneratedLevel(final, metrics);
    }

    /// <summary>
    /// The final limit, the one the level gets: the calibrated limit plus the plan's move margin. Games are followed
    /// only to the move cap, so no win rate is known past it and the final limit stops there. Only a relaxed level,
    /// calibrated above its move range, can come that far: the profile loader makes sure a limit inside its range
    /// keeps its whole margin, and a calibrated limit is read from those games, so it never exceeds the cap itself.
    /// </summary>
    private int FinalLimit(LevelPlan plan, int calibratedMoves) =>
        Math.Min(plan.MoveMargin.Soften(calibratedMoves), profile.Calibration.MoveCap);

    /// <summary>
    /// Equal-difficulty seeds around <paramref name="targetWinRate"/>, the calibration bot's verified win rate at the
    /// level's move limit, so a retry is as hard as the first attempt at the limit the player actually gets. Retries
    /// must not run out of patterns: a pool under the profile's minimum is searched again with three times the
    /// candidates, and if still short it takes the candidates closest to the target regardless of tolerance.
    /// </summary>
    private IReadOnlyList<SeedDifficulty> BuildPool(LevelDefinition level, string calibrationBot, double targetWinRate)
    {
        SeedPoolReport Search(int candidates) => SeedPoolCalibrator.Calibrate(
            level,
            new StartBoard.Varied(),
            () => LevelCalibrator.CreatePolicy(calibrationBot),
            new SeedPoolSettings
            {
                Candidates = candidates,
                GamesPerSeed = profile.SeedPool.GamesPerSeed,
                PoolSize = profile.SeedPool.PoolSize,
                TolerancePoints = profile.SeedPool.TolerancePoints,
                MoveCap = profile.Calibration.MoveCap,
            },
            targetWinRate);

        SeedPoolReport report = Search(profile.SeedPool.Candidates);
        if (report.Pool.Count >= profile.MinPoolSize)
        {
            return report.Pool;
        }

        report = Search(profile.SeedPool.Candidates * 3);
        if (report.Pool.Count >= profile.MinPoolSize)
        {
            return report.Pool;
        }

        return report.Candidates
            .OrderBy(d => Math.Abs(d.WinRateAtLimit - targetWinRate))
            .ThenBy(d => d.Seed)
            .Take(profile.MinPoolSize)
            .ToArray();
    }

    /// <summary>The candidate a level keeps: its calibrated limit, the fresh-seed games that checked it, and how it was found.</summary>
    /// <param name="Relaxed">No candidate fit the move range; this is the closest one.</param>
    private sealed record KeptCandidate(LevelDefinition Level, LimitEvidence Verification, bool FloorSetLimit, string Style, int CandidatesTried, bool Relaxed);

    /// <summary>The candidates of one level that did not become it: how many, why, and the one whose limit came closest to the move range.</summary>
    private sealed class Misses(LevelPlan plan)
    {
        private readonly List<string> _reasons = [];

        public int Count => _reasons.Count;

        public IEnumerable<string> Reasons => _reasons.Distinct();

        public (CalibrationOutcome.OutOfRange Outcome, string Style)? Closest { get; private set; }

        public void Add(string reason) => _reasons.Add(reason);

        public void Add(CalibrationOutcome.OutOfRange outOfRange, string style)
        {
            _reasons.Add(outOfRange.TooLong ? "too long" : "too short");
            if (Closest is null || IsCloser(outOfRange, Closest.Value.Outcome, plan))
            {
                Closest = (outOfRange, style);
            }
        }
    }

    private static bool IsCloser(CalibrationOutcome.OutOfRange candidate, CalibrationOutcome.OutOfRange current, LevelPlan plan) =>
        plan.Moves.DistanceTo(candidate.Level.MoveLimit) < plan.Moves.DistanceTo(current.Level.MoveLimit);

    private static string Describe(GoalSpec goal) => goal switch
    {
        GoalSpec.CollectColor collect => $"{collect.Color}:{collect.Target}",
        GoalSpec.BreakBoxes _ => $"Boxes:{goal.Target}",
        GoalSpec.ClearJelly _ => $"Jelly:{goal.Target}",
        GoalSpec.BreakChains _ => $"Chains:{goal.Target}",
        GoalSpec.DropItems _ => $"Items:{goal.Target}",
        _ => throw new InvalidOperationException($"Unhandled goal {goal.GetType().Name}."),
    };
}
