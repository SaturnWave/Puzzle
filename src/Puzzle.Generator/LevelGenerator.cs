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

    public required int MoveLimit { get; init; }

    public required MoveRange MoveRange { get; init; }

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

    /// <summary>The floor bot needed more moves than the target did, so the casual floor decided the limit.</summary>
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
/// Produces one level: plan it, draw candidates from the portfolio, calibrate them with the bot (and the floor bot
/// on a tier with a casual floor), keep the first that fits (or the closest), then build its equal-difficulty seed
/// pool and report bot win rates.
/// </summary>
public sealed class LevelGenerator(GenerationProfile profile)
{
    private const ulong ReportStream = 500;

    private readonly LevelPlanner _planner = new(profile);
    private readonly LevelCalibrator _calibrator = new(profile);

    public LevelPlan Plan(int number) => _planner.Plan(number);

    /// <param name="recent">Signatures of the levels just before this one, for the novelty check.</param>
    /// <exception cref="GenerationFailedException">Every candidate was broken.</exception>
    public GeneratedLevel Generate(int number, IReadOnlyList<LevelSignature> recent)
    {
        var clock = Stopwatch.StartNew();
        LevelPlan plan = _planner.Plan(number);
        double density = 1.0;
        (CalibrationOutcome.OutOfRange Outcome, string Style)? fallback = null;
        var reasons = new List<string>();
        int maxCandidates = profile.Calibration.MaxCandidates;
        for (int attempt = 0; attempt < maxCandidates; attempt++)
        {
            if (CandidateFactory.TryCreate(plan, attempt, density) is not Candidate candidate)
            {
                reasons.Add("unusable board");
                continue;
            }

            switch (_calibrator.Calibrate(candidate, plan))
            {
                case CalibrationOutcome.Accepted accepted when attempt >= maxCandidates / 2 || !recent.Contains(LevelSignature.Of(accepted.Level)):
                    return Finish(plan, new KeptCandidate(accepted.Level, accepted.Verification, accepted.FloorSetLimit, candidate.Style, attempt + 1, Relaxed: false), clock);
                case CalibrationOutcome.Accepted:
                    reasons.Add("too similar to a recent level");
                    break;
                case CalibrationOutcome.OutOfRange outOfRange:
                    reasons.Add(outOfRange.TooLong ? "too long" : "too short");
                    density *= outOfRange.TooLong ? 0.7 : 1.35;
                    if (fallback is null || IsCloser(outOfRange, fallback.Value.Outcome, plan))
                    {
                        fallback = (outOfRange, candidate.Style);
                    }

                    break;
                case CalibrationOutcome.Rejected rejected:
                    reasons.Add(rejected.Reason);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled calibration outcome.");
            }
        }

        if (fallback is not { } closest)
        {
            throw new GenerationFailedException($"Level {number}: no usable candidate ({string.Join("; ", reasons.Distinct())}).");
        }

        LimitEvidence verification = _calibrator.Measure(closest.Outcome.Level, plan, profile.Calibration.VerifyRuns, Rng.Derive(plan.Seed, 401));
        return Finish(plan, new KeptCandidate(closest.Outcome.Level, verification, closest.Outcome.FloorSetLimit, closest.Style, maxCandidates, Relaxed: true), clock);
    }

    private GeneratedLevel Finish(LevelPlan plan, KeptCandidate kept, Stopwatch clock)
    {
        LevelReport verification = kept.Verification.Target.Games;
        double targetWinRate = verification.WinRateAt(kept.Level.MoveLimit);
        IReadOnlyList<SeedDifficulty> poolSeeds = BuildPool(kept.Level, targetWinRate);
        LevelDefinition final = kept.Level.WithAttempts(new AttemptPolicy { SeedPool = poolSeeds.Select(d => d.Seed).ToArray() });
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
            MoveRange = plan.Moves,
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
    /// Equal-difficulty seeds around the level's verified win rate. Retries must not run out of patterns: a pool
    /// under the profile's minimum is searched again with three times the candidates, and if still short it takes
    /// the candidates closest to the target regardless of tolerance.
    /// </summary>
    private IReadOnlyList<SeedDifficulty> BuildPool(LevelDefinition level, double targetWinRate)
    {
        SeedPoolReport Search(int candidates) => SeedPoolCalibrator.Calibrate(
            level,
            new StartBoard.Varied(),
            () => LevelCalibrator.CreatePolicy(profile.CalibrationBot),
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

    /// <summary>The candidate a level keeps: its limit, the fresh-seed games that checked it, and how it was found.</summary>
    /// <param name="Relaxed">No candidate fit the move range; this is the closest one.</param>
    private sealed record KeptCandidate(LevelDefinition Level, LimitEvidence Verification, bool FloorSetLimit, string Style, int CandidatesTried, bool Relaxed);

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
