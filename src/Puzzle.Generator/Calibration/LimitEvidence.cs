using Puzzle.Bot.Simulation;

namespace Puzzle.Generator.Calibration;

/// <summary>A win rate a bot must reach, and that bot's uncapped games of the level.</summary>
internal sealed record MeasuredShare(double Required, LevelReport Games)
{
    /// <summary>The smallest limit at which the games reach the share; absent when they never do within the move cap.</summary>
    public int? LimitNeeded => Games.MoveLimitFor(Required);

    public WinRateCheck CheckAt(int moveLimit) => new(Required, Games.WinRateAt(moveLimit));
}

/// <summary>A move limit, and whether the casual floor rather than the target win rate decided it.</summary>
internal readonly record struct LimitChoice(int Moves, bool FloorSetLimit);

/// <summary>
/// The games a move limit is read from: the calibration bot's against the target and, on a tier with a casual floor,
/// the floor bot's against the floor, both played on the same seeds.
/// </summary>
internal sealed record LimitEvidence(MeasuredShare Target, MeasuredShare? Floor)
{
    /// <summary>Some game ended with no legal move even after shuffling: the level is broken.</summary>
    public bool AnyRunStuck => Target.Games.StuckRate > 0 || (Floor is not null && Floor.Games.StuckRate > 0);

    /// <summary>
    /// The smallest limit at which every bot reaches its share: the larger of the target's and the floor's. Absent when
    /// a bot never reaches its share within the move cap, the same as a candidate that cannot reach its target today.
    /// </summary>
    public LimitChoice? ReadLimit()
    {
        if (Target.LimitNeeded is not int targetLimit)
        {
            return null;
        }

        if (Floor is null)
        {
            return new LimitChoice(targetLimit, FloorSetLimit: false);
        }

        return Floor.LimitNeeded is int floorLimit
            ? new LimitChoice(Math.Max(targetLimit, floorLimit), FloorSetLimit: floorLimit > targetLimit)
            : null;
    }

    /// <summary>Whether <paramref name="moveLimit"/> stands on these games (<see cref="LimitVerification.Stands"/>).</summary>
    public bool Confirms(int moveLimit, double tolerance) =>
        LimitVerification.Stands(Target.CheckAt(moveLimit), Floor?.CheckAt(moveLimit), tolerance);
}
