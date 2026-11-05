using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>A level no stored plain game of a rung wins, even with the start booster's moves and the offer's: players of that rung never pass it.</summary>
public sealed record Wall(LevelId Level, SkillRung Rung);

/// <summary>
/// The levels a run plays: level 1 to the last level the measurement covers without a gap for every rung of the mix
/// and every board start the player rules can start with. Players are simulated that far and no further.
/// </summary>
public sealed class SimulatedLevels
{
    private readonly Measurement _measurement;

    private SimulatedLevels(Measurement measurement, IReadOnlyList<SkillRung> rungs, IReadOnlyList<BoardStart> starts, int count)
    {
        _measurement = measurement;
        Rungs = rungs;
        Starts = starts;
        Count = count;
    }

    /// <summary>The last level players can reach; the levels are 1 to this.</summary>
    public int Count { get; }

    public IReadOnlyList<SkillRung> Rungs { get; }

    /// <summary>The board starts a run reads: plain, and every start the player rules can use or buy a booster for.</summary>
    public IReadOnlyList<BoardStart> Starts { get; }

    public MeasurementSpec Spec => _measurement.Spec;

    public IEnumerable<LevelId> Levels => Enumerable.Range(1, Count).Select(number => new LevelId(number));

    /// <summary>
    /// Checks that the measurement can carry a run with these settings and says how far.
    /// </summary>
    /// <exception cref="MeasurementMismatchException">
    /// The measurement lacks level 1 for a rung or a board start, keeps no mark a player rule waits for, follows games
    /// less far than a rule can read them, holds a level in parts that were measured on different contents, or says
    /// other moves for the start booster or the offer than the engine adds today.
    /// </exception>
    public static SimulatedLevels Of(EconomySettings settings, Measurement measurement)
    {
        measurement.Spec.RequireTheAddedMovesOfThisBuild();
        SkillRung[] rungs = settings.Players.Rungs.ToArray();
        BoardStart[] starts = StartsARuleCanUse(settings.Player.StartBoosters);
        RequireMarks(settings.Player, measurement.Spec);
        RequireHalves(measurement.Spec);
        int count = measurement.LastLevelWithoutGap(rungs, starts);
        if (count == 0)
        {
            throw new MeasurementMismatchException(
                $"The measurement has no games of level 1 for every rung ({string.Join(", ", rungs)}) and board start ({string.Join(", ", starts.Select(BoardStarts.FileName))}) "
                + "these settings need; measure with: puzzle economy measure <settings.json>.");
        }

        var levels = new SimulatedLevels(measurement, rungs, starts, count);
        levels.RequireOneContentPerLevel();
        levels.RequireReach(settings.Player.InLevelBoosters.LargestAssumedWorth);
        return levels;
    }

    /// <summary>The games of a level, rung and board start.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The level, rung or start is not one a run plays.</exception>
    public MeasuredLine Line(LevelId level, SkillRung rung, BoardStart start) => level.Value <= Count && _measurement.Find(level, rung, start) is { } line
        ? line
        : throw new ArgumentOutOfRangeException(nameof(level), $"A run has no games of {LineKey.Of(level, rung, start)}.");

    /// <summary>Every line a run can read.</summary>
    public IEnumerable<MeasuredLine> LinesRead() =>
        from level in Levels
        from rung in Rungs
        from start in Starts
        select Line(level, rung, start);

    /// <summary>The levels a rung cannot pass: no stored plain game of it is won, however many moves a player can add.</summary>
    public IReadOnlyList<Wall> Walls()
    {
        var walls = new List<Wall>();
        foreach (LevelId level in Levels)
        {
            foreach (SkillRung rung in Rungs)
            {
                MeasuredLine line = Line(level, rung, BoardStart.Plain);
                MoveCount furthest = line.MoveLimit + Spec.StartBoosterExtraMoves + Spec.ExtraMovesOfferMoves;
                if (!line.Seeds.Any(seed => seed.Games.Any(game => game.WonWithin(furthest))))
                {
                    walls.Add(new Wall(level, rung));
                }
            }
        }

        return walls;
    }

    private static BoardStart[] StartsARuleCanUse(StartBoosterHabit habit)
    {
        bool startsWithBoosters = habit.UseAfterLossesOnLevel is not null || habit.BuyAfterLossesOnLevel is not null;
        IEnumerable<BoardStart> ofBoosters = startsWithBoosters ? habit.Preference.Select(BoosterKinds.BoardStart) : [];
        return ofBoosters.Append(BoardStart.Plain).Distinct().Order().ToArray();
    }

    private static void RequireMarks(PlayerRules rules, MeasurementSpec spec)
    {
        foreach (Percent mark in new[] { rules.ExtraMoves.TakeWhenGoalsDone, rules.InLevelBoosters.UseWhenGoalsDone })
        {
            if (mark.Value != 0 && !spec.ProgressMarks.Contains(mark))
            {
                throw new MeasurementMismatchException(
                    $"A player rule waits for {mark} % of the goals, and the measurement keeps only the marks {string.Join(", ", spec.ProgressMarks)}; measure again with that mark, or change the rule.");
            }
        }
    }

    /// <summary>A run reads the even and the odd games of a seed apart, so every seed needs both.</summary>
    private static void RequireHalves(MeasurementSpec spec)
    {
        if (spec.PlainGamesBySeedPosition.Append(spec.BoostedGamesPerSeed).Any(games => games < 2 || games % 2 != 0))
        {
            throw new MeasurementMismatchException("The measurement stores an odd number of games for a seed; a run needs an even number, to read each half on its own.");
        }
    }

    private void RequireOneContentPerLevel()
    {
        foreach (LevelId level in Levels)
        {
            MeasuredLine first = Line(level, Rungs[0], BoardStart.Plain);
            bool sameContent = LinesOf(level).All(line => line.Fingerprint == first.Fingerprint && line.MoveLimit == first.MoveLimit && line.Pool.SequenceEqual(first.Pool));
            if (!sameContent)
            {
                throw new MeasurementMismatchException($"Level {level} was measured in parts, on different contents of the level; measure it again (puzzle economy measure --from {level} --to {level}).");
            }
        }
    }

    /// <summary>No stored game is read past the move cap: beyond it nobody knows how a game went.</summary>
    private void RequireReach(MoveCount largestAssumedWorth)
    {
        MoveCount added = Spec.StartBoosterExtraMoves + Spec.ExtraMovesOfferMoves + largestAssumedWorth;
        foreach (LevelId level in Levels)
        {
            MoveCount limit = Line(level, Rungs[0], BoardStart.Plain).MoveLimit;
            if (limit + added > Spec.MoveCap)
            {
                throw new MeasurementMismatchException(
                    $"Level {level} has {limit} moves and a player can add {added}, past the {Spec.MoveCap} moves the measurement followed its games to; measure again with a larger measurement.moveCap.");
            }
        }
    }

    private IEnumerable<MeasuredLine> LinesOf(LevelId level) =>
        from rung in Rungs
        from start in Starts
        select Line(level, rung, start);
}
