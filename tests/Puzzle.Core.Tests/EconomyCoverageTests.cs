using Puzzle.Economy.Measuring;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a run asks of a measurement before it reads one: how far the levels go without a gap, and that every rule of
/// the settings can be read from the stored games. A measurement that cannot carry the run is refused with what is
/// missing, and a level beyond the measurement is reported as not measured, never as a number.
/// </summary>
public class EconomyCoverageTests
{
    private const string UsesBoosters = """{ "player": { "startBoosters": { "useAfterLossesOnLevel": 1 } } }""";

    private static MeasuredGame[][] OneSeed => [Always(Won(15))];

    private static MeasurementMismatchException Refused(EconomySettings settings, Measurement measurement) =>
        Assert.Throws<MeasurementMismatchException>(() => SimulatedLevels.Of(settings, measurement));

    [Fact]
    public void Players_are_simulated_to_the_last_level_measured_without_a_gap_and_no_further()
    {
        // The third level has no games on a board that starts with a colour bomb, which this player can start with.
        HandMadeLevels levels = new HandMadeLevels()
            .Level(20, OneSeed).Starting(BoardStart.RocketAndBomb, OneSeed).Starting(BoardStart.ColorBomb, OneSeed)
            .Level(20, OneSeed).Starting(BoardStart.RocketAndBomb, OneSeed).Starting(BoardStart.ColorBomb, OneSeed)
            .Level(20, OneSeed).Starting(BoardStart.RocketAndBomb, OneSeed)
            .Level(20, OneSeed).Starting(BoardStart.RocketAndBomb, OneSeed).Starting(BoardStart.ColorBomb, OneSeed);
        EconomySettings withBoosters = OnePlayer(UsesBoosters, """{ "report": { "milestones": [2, 3], "paceTarget": { "level": 2 } } }""");

        Assert.Equal(2, levels.For(withBoosters).Count);
        Assert.Equal([BoardStart.Plain, BoardStart.RocketAndBomb, BoardStart.ColorBomb], levels.For(withBoosters).Starts);

        // A player who starts with no booster reads the plain games alone, and those go on to the fourth level.
        Assert.Equal(4, levels.For(OnePlayer()).Count);
        Assert.Equal([BoardStart.Plain], levels.For(OnePlayer()).Starts);

        // The milestone beyond the measurement is said to be not measured: it has no figure in any report.
        EconomyReport report = SmallWorld.Report(withBoosters, levels);
        PaceRow beyond = report.Pace.Single(row => row.Level.Value == 3);
        Assert.False(beyond.IsMeasured);
        Assert.Null(beyond.ReachedShare);
        Assert.Null(beyond.DaysP50);
        Assert.Null(beyond.Verdict);
        Assert.True(report.Pace.Single(row => row.Level.Value == 2).IsMeasured);
        Assert.DoesNotContain(report.Lives, row => row.Level.Value == 3);
        Assert.DoesNotContain(report.Coins, row => row.At == BalanceMoment.Milestone && row.Value == 3);
        Assert.DoesNotContain(report.Spending, row => row.Level.Value == 3);
        Assert.Contains("not measured", report.Files()[EconomyReport.SummaryFile]);
        Assert.Contains("fixture,skill-55,yes,1,3,no,,,,,,,,,,,", report.Files()["pace.csv"]);
        Assert.Equal(2, report.Measurement.LastLevel);
    }

    [Fact]
    public void A_measurement_without_the_first_level_for_a_board_start_the_player_uses_is_refused()
    {
        HandMadeLevels plainOnly = new HandMadeLevels().Level(20, OneSeed).Level(20, OneSeed);

        MeasurementMismatchException refused = Refused(OnePlayer(UsesBoosters), plainOnly.Build());

        Assert.Contains("no games of level 1", refused.Message);
        Assert.Contains("start-color-bomb", refused.Message);
    }

    [Fact]
    public void A_level_whose_limit_and_added_moves_pass_the_move_cap_is_refused()
    {
        // The cap is 150: a limit of 142 and the booster's 3 and the offer's 5 just fit, 143 does not.
        Assert.Equal(1, new HandMadeLevels().Level(142, OneSeed).For(OnePlayer()).Count);
        MeasurementMismatchException tooLong = Refused(OnePlayer(), new HandMadeLevels().Level(20, OneSeed).Level(143, OneSeed).Build());
        Assert.Contains("Level 2 has 143 moves and a player can add 8", tooLong.Message);
        Assert.Contains("measurement.moveCap", tooLong.Message);

        // An in-level booster that is assumed to be worth moves reads the game that much further still.
        EconomySettings assumesAWorth = OnePlayer("""{ "player": { "inLevelBoosters": { "assumedMovesWorth": { "shuffle": 4 } } } }""");
        Assert.Equal(1, new HandMadeLevels().Level(138, OneSeed).For(assumesAWorth).Count);
        Assert.Contains("a player can add 12", Refused(assumesAWorth, new HandMadeLevels().Level(139, OneSeed).Build()).Message);
    }

    [Fact]
    public void A_measurement_that_keeps_no_mark_a_rule_waits_for_is_refused()
    {
        Measurement measured = new HandMadeLevels().Level(20, OneSeed).Build();
        var withoutTheEightyMark = new Measurement(Spec with { ProgressMarks = [new Percent(50), new Percent(90)] }, measured.Lines);

        MeasurementMismatchException refused = Refused(OnePlayer(), withoutTheEightyMark);

        Assert.Contains("waits for 80 % of the goals", refused.Message);
        Assert.Contains("50, 90", refused.Message);
    }

    [Fact]
    public void A_measurement_with_an_odd_number_of_games_for_a_seed_is_refused()
    {
        Measurement measured = new HandMadeLevels().Level(20, OneSeed).Build();
        var oddGames = new Measurement(Spec with { PlainGamesBySeedPosition = [3] }, measured.Lines);

        Assert.Contains("odd number of games", Refused(OnePlayer(), oddGames).Message);
    }

    [Fact]
    public void A_level_measured_in_parts_on_different_contents_is_refused()
    {
        SkillRung weak = SkillRung.Parse("skill-45");
        Measurement measured = new HandMadeLevels(weak, Average).Level(20, OneSeed).Level(20, OneSeed).Build();
        MeasuredLine[] inParts = measured.Lines
            .Select(line => line.Level.Value == 2 && line.Player.Name == weak.Name ? line with { Fingerprint = "an earlier content" } : line)
            .ToArray();
        EconomySettings twoRungs = OnePlayer("""{ "players": { "count": 2, "skillMixPercent": { "skill-45": 50, "skill-55": 50, "skill-65": 0 } } }""");

        MeasurementMismatchException refused = Refused(twoRungs, new Measurement(Spec, inParts));

        Assert.Contains("Level 2 was measured in parts", refused.Message);
        Assert.Equal(2, SimulatedLevels.Of(twoRungs, measured).Count);
    }

    [Fact]
    public void A_measurement_made_when_the_engine_added_other_moves_is_refused_checked_or_trusted()
    {
        // Nothing plays the booster's three moves or the offer's five, so no fingerprint and no replay sees them change:
        // a file that says other numbers than the engine has today is refused before any run, with the guard or without.
        Measurement measured = new HandMadeLevels().Level(20, OneSeed).Build();
        var otherMoves = new Measurement(Spec with { StartBoosterExtraMoves = new MoveCount(9), ExtraMovesOfferMoves = new MoveCount(12) }, measured.Lines);

        MeasurementMismatchException refused = Refused(OnePlayer(), otherMoves);

        Assert.Contains("the start booster's moves 9 instead of 3", refused.Message);
        Assert.Contains("the extra-moves offer's moves 12 instead of 5", refused.Message);
        Assert.Contains("--restart", refused.Message);
        Assert.Equal(1, SimulatedLevels.Of(OnePlayer(), measured).Count);
    }
}
