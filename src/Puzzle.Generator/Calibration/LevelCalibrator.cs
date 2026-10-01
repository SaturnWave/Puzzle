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
    public sealed record Accepted(LevelDefinition Level, LevelReport Calibration, LevelReport Verification) : CalibrationOutcome;

    /// <summary>The target win rate needs a limit outside the range; kept as a fallback (limit honors the win rate).</summary>
    public sealed record OutOfRange(LevelDefinition Level, LevelReport Calibration, bool TooLong) : CalibrationOutcome;

    public sealed record Rejected(string Reason) : CalibrationOutcome;
}

/// <summary>
/// Finds the move limit of a candidate: plays it uncapped with the calibration bot, reads the smallest limit
/// that reaches the target win rate, and rescales the candidate's amount goals until that limit lands in the
/// plan's move range. The accepted limit is re-checked on seeds the calibration never saw.
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
            LevelReport report;
            try
            {
                level = candidate.Build(plan, scale);
                report = Simulate(level, profile.CalibrationBot, Settings.Runs, Rng.Derive(plan.Seed, CalibrationStream + (ulong)iteration));
            }
            catch (LevelFormatException ex)
            {
                return new CalibrationOutcome.Rejected($"invalid level: {ex.Message}");
            }
            catch (LevelInitializationException ex)
            {
                return new CalibrationOutcome.Rejected($"no starting board: {ex.Message}");
            }

            if (report.StuckRate > 0)
            {
                return new CalibrationOutcome.Rejected("some runs ran out of legal moves");
            }

            int? limit = report.MoveLimitFor(plan.TargetWinRate);
            if (limit is int inRange && plan.Moves.Contains(inRange))
            {
                return Verify(level.WithMoveLimit(inRange), report, plan);
            }

            if (limit is int outside && IsCloser(outside, closest, plan))
            {
                closest = new CalibrationOutcome.OutOfRange(level.WithMoveLimit(outside), report, outside > plan.Moves.Max);
            }

            if (!candidate.IsScalable)
            {
                break;
            }

            scale *= limit is int known ? Math.Clamp(plan.Moves.Mid / (double)known, 0.4, 2.5) : 0.5;
        }

        return closest is not null
            ? closest
            : new CalibrationOutcome.Rejected("never reached the target win rate within the move cap");
    }

    public LevelReport Simulate(LevelDefinition level, string bot, int runs, ulong seed) =>
        BatchSimulator.Run(level, () => CreatePolicy(bot), runs, Settings.MoveCap, seed);

    public static IBotPolicy CreatePolicy(string bot) => BotPolicies.TryCreate(bot, out IBotPolicy policy)
        ? policy
        : throw new InvalidOperationException($"Unknown bot '{bot}' (the profile loader should have rejected it).");

    private CalibrationOutcome.Accepted Verify(LevelDefinition level, LevelReport calibration, LevelPlan plan)
    {
        LevelReport verification = Simulate(level, profile.CalibrationBot, Settings.VerifyRuns, Rng.Derive(plan.Seed, VerificationStream));
        bool offTarget = Math.Abs(verification.WinRateAt(level.MoveLimit) - plan.TargetWinRate) > Settings.WinRateTolerance;
        if (offTarget && verification.MoveLimitFor(plan.TargetWinRate) is int adjusted && plan.Moves.Contains(adjusted))
        {
            level = level.WithMoveLimit(adjusted);
        }

        return new CalibrationOutcome.Accepted(level, calibration, verification);
    }

    private static bool IsCloser(int limit, CalibrationOutcome.OutOfRange? current, LevelPlan plan) =>
        current is null || plan.Moves.DistanceTo(limit) < plan.Moves.DistanceTo(current.Level.MoveLimit);
}
