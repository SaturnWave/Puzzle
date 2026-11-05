namespace Puzzle.Generator.Calibration;

/// <summary>A win rate a bot must have at the move limit, next to the one it had in the games played.</summary>
public readonly record struct WinRateCheck(double Required, double Measured)
{
    /// <summary>How far the measured win rate is above the required one; negative when below.</summary>
    public double Margin => Measured - Required;
}

/// <summary>
/// When a move limit checked on fresh seeds stands. The calibration bot must reach the target and, on a tier with a
/// casual floor, the floor bot must reach the floor. The limit is the smallest that gives both, so one requirement
/// decides it and sits near its share while the other may sit well above. A limit stands when the tighter margin is
/// within the tolerance: no bot falls short by more than the tolerance, and the deciding requirement is not overshot
/// by more than it. Without a floor this is the plain |win - target| &lt;= tolerance.
/// </summary>
public static class LimitVerification
{
    public static bool Stands(WinRateCheck target, WinRateCheck? floor, double tolerance) =>
        Math.Abs(TighterMargin(target, floor)) <= tolerance;

    /// <summary>
    /// What is left of <see cref="Stands"/> for a level with a move margin, judged on win rates read at its final limit
    /// (the calibrated limit plus the margin): no bot falls short of its share by more than the tolerance. More moves
    /// only raise a win rate, so an overshoot there is the move margin at work and says nothing about the calibrated
    /// limit underneath; whether that limit sat on its target can no longer be told from these win rates.
    /// </summary>
    internal static bool StandsWithMoveMargin(WinRateCheck target, WinRateCheck? floor, double tolerance) =>
        TighterMargin(target, floor) >= -tolerance;

    private static double TighterMargin(WinRateCheck target, WinRateCheck? floor) =>
        floor is WinRateCheck guard ? Math.Min(target.Margin, guard.Margin) : target.Margin;
}
