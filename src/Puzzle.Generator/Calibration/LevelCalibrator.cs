using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Generator.Composition;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Generator.Calibration;

/// <summary>What happened when a candidate was calibrated. Closed set (private constructor).</summary>
internal abstract record CalibrationOutcome
{
    private CalibrationOutcome()
    {
    }

    /// <summary>The move limit landed in the plan's range and was checked on fresh seeds.</summary>
    public sealed record Accepted(LevelDefinition Level, bool FloorSetLimit, LimitEvidence Verification) : CalibrationOutcome;

    /// <summary>The target win rate or the casual floor needs a limit outside the range; kept as a fallback (the limit honors both).</summary>
    public sealed record OutOfRange(LevelDefinition Level, bool FloorSetLimit, bool TooLong) : CalibrationOutcome;

    public sealed record Rejected(string Reason) : CalibrationOutcome;
}

/// <summary>
/// Finds the move limit of a candidate: plays it uncapped with the calibration bot (and, on a tier with a casual
/// floor, with the floor bot on the same seeds), reads the smallest limit that reaches the target win rate and the
/// floor, and rescales the candidate's amount goals until that limit lands in the plan's move range. The accepted
/// limit is re-checked on seeds the calibration never saw.
/// </summary>
internal sealed class LevelCalibrator(GenerationProfile profile)
{
    private const ulong CalibrationStream = 300;
    private const ulong VerificationStream = 400;

    private CalibrationSettings Settings => profile.Calibration;

    public CalibrationOutcome Calibrate(Candidate candidate, LevelPlan plan)
    {
        double scale = 1;
        CalibrationOutcome.OutOfRange? closest = null;
        for (int iteration = 0; iteration < Settings.MaxScaleIterations; iteration++)
        {
            LevelDefinition level;
            LimitEvidence evidence;
            try
            {
                level = candidate.Build(plan, scale);
                evidence = Measure(level, plan, Settings.Runs, Rng.Derive(plan.Seed, CalibrationStream + (ulong)iteration));
            }
            catch (LevelFormatException ex)
            {
                return new CalibrationOutcome.Rejected($"invalid level: {ex.Message}");
            }
            catch (LevelInitializationException ex)
            {
                return new CalibrationOutcome.Rejected($"no starting board: {ex.Message}");
            }

            if (evidence.AnyRunStuck)
            {
                return new CalibrationOutcome.Rejected("some runs ran out of legal moves");
            }

            LimitChoice? limit = evidence.ReadLimit();
            if (limit is LimitChoice inRange && plan.Moves.Contains(inRange.Moves))
            {
                return Verify(level.WithMoveLimit(inRange.Moves), inRange.FloorSetLimit, plan);
            }

            if (limit is LimitChoice outside && IsCloser(outside.Moves, closest, plan))
            {
                closest = new CalibrationOutcome.OutOfRange(level.WithMoveLimit(outside.Moves), outside.FloorSetLimit, outside.Moves > plan.Moves.Max);
            }

            if (!candidate.IsScalable)
            {
                break;
            }

            scale *= limit is LimitChoice known ? Math.Clamp(plan.Moves.Mid / (double)known.Moves, 0.4, 2.5) : 0.5;
        }

        string unreached = plan.CasualFloor is null ? "the target win rate" : "both the target win rate and the casual floor";
        return closest is not null
            ? closest
            : new CalibrationOutcome.Rejected($"never reached {unreached} within the move cap");
    }

    /// <summary>
    /// Plays <paramref name="runs"/> uncapped games with the calibration bot and, when the plan has a casual floor, as
    /// many with the floor bot on the same seeds.
    /// </summary>
    public LimitEvidence Measure(LevelDefinition level, LevelPlan plan, int runs, ulong seed) => new(
        new MeasuredShare(plan.TargetWinRate, Simulate(level, profile.CalibrationBot, runs, seed)),
        plan.CasualFloor is double floor ? new MeasuredShare(floor, Simulate(level, profile.FloorBot, runs, seed)) : null);

    public LevelReport Simulate(LevelDefinition level, string bot, int runs, ulong seed) =>
        BatchSimulator.Run(level, () => CreatePolicy(bot), runs, Settings.MoveCap, seed);

    public static IBotPolicy CreatePolicy(string bot) => BotPolicies.TryCreate(bot, out IBotPolicy policy)
        ? policy
        : throw new InvalidOperationException($"Unknown bot '{bot}' (the profile loader should have rejected it).");

    /// <summary>
    /// Re-checks the limit on fresh seeds (<see cref="LimitVerification"/>). A miss is fixed by re-reading the limit,
    /// the larger of the target's and the floor's, from those games; when the fixed limit would leave the move range,
    /// the candidate is handed back as out of range so another one is tried.
    /// </summary>
    private CalibrationOutcome Verify(LevelDefinition level, bool floorSetLimit, LevelPlan plan)
    {
        LimitEvidence verification = Measure(level, plan, Settings.VerifyRuns, Rng.Derive(plan.Seed, VerificationStream));
        if (verification.Confirms(level.MoveLimit, Settings.WinRateTolerance))
        {
            return new CalibrationOutcome.Accepted(level, floorSetLimit, verification);
        }

        LimitChoice? adjusted = verification.ReadLimit();
        if (adjusted is LimitChoice fixedLimit && plan.Moves.Contains(fixedLimit.Moves))
        {
            return new CalibrationOutcome.Accepted(level.WithMoveLimit(fixedLimit.Moves), fixedLimit.FloorSetLimit, verification);
        }

        return adjusted is LimitChoice outside
            ? new CalibrationOutcome.OutOfRange(level.WithMoveLimit(outside.Moves), outside.FloorSetLimit, outside.Moves > plan.Moves.Max)
            : new CalibrationOutcome.OutOfRange(level, floorSetLimit, TooLong: true);
    }

    private static bool IsCloser(int limit, CalibrationOutcome.OutOfRange? current, LevelPlan plan) =>
        current is null || plan.Moves.DistanceTo(limit) < plan.Moves.DistanceTo(current.Level.MoveLimit);
}
