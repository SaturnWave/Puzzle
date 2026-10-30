using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;
using Puzzle.Generator.Calibration;

namespace Puzzle.Core.Tests;

/// <summary>The casual guard: how a move limit is read from two bots' games, and when a checked limit stands.</summary>
public class LimitRuleTests
{
    private const double Tolerance = 0.07;

    // Ten games each. The calibration bot reaches 80% at move 17; the floor bot reaches 60% at move 22 and 90% never.
    private static readonly LevelReport CalibrationGames = Games(10, 11, 12, 13, 14, 15, 16, 17, 18, 19);
    private static readonly LevelReport FloorGames = Games(12, 14, 16, 18, 20, 22, 24, 26, 28, null);

    [Fact]
    public void Without_a_floor_the_limit_is_what_the_target_needs()
    {
        var evidence = new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), Floor: null);

        Assert.Equal(new LimitChoice(17, FloorSetLimit: false), evidence.ReadLimit());
    }

    [Fact]
    public void A_floor_that_needs_more_moves_than_the_target_sets_the_limit()
    {
        var evidence = new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.6, FloorGames));

        Assert.Equal(new LimitChoice(22, FloorSetLimit: true), evidence.ReadLimit());
    }

    [Fact]
    public void A_floor_that_needs_no_more_moves_than_the_target_leaves_the_target_limit()
    {
        var easierFloor = new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.3, FloorGames));
        var equalFloor = new LimitEvidence(new MeasuredShare(0.7, CalibrationGames), new MeasuredShare(0.3, FloorGames));

        Assert.Equal(new LimitChoice(17, FloorSetLimit: false), easierFloor.ReadLimit());
        Assert.Equal(new LimitChoice(16, FloorSetLimit: false), equalFloor.ReadLimit()); // both need 16: the target decides
    }

    [Fact]
    public void A_floor_the_floor_bot_never_reaches_within_the_cap_leaves_no_limit_like_an_unreachable_target()
    {
        var unreachableFloor = new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.95, FloorGames));
        var unreachableTarget = new LimitEvidence(new MeasuredShare(0.95, FloorGames), new MeasuredShare(0.3, CalibrationGames));

        Assert.Null(unreachableFloor.ReadLimit());
        Assert.Null(unreachableTarget.ReadLimit());
    }

    [Fact]
    public void Without_a_floor_a_limit_stands_only_within_the_tolerance_of_the_target_on_both_sides()
    {
        Assert.True(LimitVerification.Stands(new WinRateCheck(0.85, 0.80), floor: null, Tolerance));
        Assert.True(LimitVerification.Stands(new WinRateCheck(0.85, 0.91), floor: null, Tolerance));
        Assert.False(LimitVerification.Stands(new WinRateCheck(0.85, 0.77), floor: null, Tolerance));
        Assert.False(LimitVerification.Stands(new WinRateCheck(0.85, 0.94), floor: null, Tolerance));
    }

    [Fact]
    public void With_a_floor_the_calibration_bot_may_sit_far_above_the_target_while_the_floor_holds()
    {
        Assert.True(LimitVerification.Stands(new WinRateCheck(0.85, 0.97), new WinRateCheck(0.60, 0.62), Tolerance));
        Assert.True(LimitVerification.Stands(new WinRateCheck(0.85, 0.97), new WinRateCheck(0.60, 0.55), Tolerance));
    }

    [Fact]
    public void With_a_floor_each_bot_must_come_within_the_tolerance_of_its_share()
    {
        Assert.False(LimitVerification.Stands(new WinRateCheck(0.85, 0.97), new WinRateCheck(0.60, 0.50), Tolerance));
        Assert.False(LimitVerification.Stands(new WinRateCheck(0.85, 0.75), new WinRateCheck(0.60, 0.65), Tolerance));
    }

    [Fact]
    public void With_a_floor_a_limit_both_bots_overshoot_is_too_generous_and_gets_re_read()
    {
        Assert.False(LimitVerification.Stands(new WinRateCheck(0.85, 0.97), new WinRateCheck(0.60, 0.70), Tolerance));
        Assert.True(LimitVerification.Stands(new WinRateCheck(0.85, 0.89), new WinRateCheck(0.60, 0.80), Tolerance)); // the target decides and holds
    }

    [Fact]
    public void A_final_limit_with_a_move_margin_stands_however_far_it_overshoots_but_not_on_a_shortfall()
    {
        Assert.True(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.50, 0.95), floor: null, Tolerance));
        Assert.True(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.85, 1.00), new WinRateCheck(0.60, 0.95), Tolerance));
        Assert.True(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.85, 0.80), new WinRateCheck(0.60, 0.55), Tolerance));    // short within the tolerance
        Assert.False(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.50, 0.40), floor: null, Tolerance));
        Assert.False(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.85, 1.00), new WinRateCheck(0.60, 0.50), Tolerance));   // the floor bot falls short
        Assert.False(LimitVerification.StandsWithMoveMargin(new WinRateCheck(0.85, 0.75), new WinRateCheck(0.60, 0.95), Tolerance));   // the calibration bot falls short
    }

    [Fact]
    public void Evidence_checks_a_limit_against_both_bots_games()
    {
        var withFloor = new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.6, FloorGames));
        var withoutFloor = withFloor with { Floor = null };

        Assert.True(withFloor.Confirms(22, Tolerance));      // calibration bot 100%, floor bot exactly 60%
        Assert.False(withFloor.Confirms(18, Tolerance));     // floor bot 40%
        Assert.False(withoutFloor.Confirms(22, Tolerance));  // alone, 100% overshoots the 80% target
    }

    [Fact]
    public void A_stuck_game_of_either_bot_marks_the_level_broken()
    {
        LevelReport stuck = LevelReport.From([Run(10), new RunResult(0, GameStatus.LostNoMovesPossible, 5, null, new float[6], PlayStats.None)], moveCap: 150);

        Assert.False(new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.6, FloorGames)).AnyRunStuck);
        Assert.True(new LimitEvidence(new MeasuredShare(0.8, CalibrationGames), new MeasuredShare(0.6, stuck)).AnyRunStuck);
    }

    private static LevelReport Games(params int?[] completedAt) => LevelReport.From(completedAt.Select(Run).ToArray(), moveCap: 150);

    private static RunResult Run(int? completedAt)
    {
        int moves = completedAt ?? 150;
        float[] progress = Enumerable.Range(0, moves + 1).Select(i => completedAt is null ? 0.5f : i / (float)moves).ToArray();
        return new RunResult(0, completedAt is null ? GameStatus.LostOutOfMoves : GameStatus.Won, moves, completedAt, progress, PlayStats.None);
    }
}
