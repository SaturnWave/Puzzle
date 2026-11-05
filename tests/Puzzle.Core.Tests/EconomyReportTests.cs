using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>The economy simulator's reports: what a number means when few players get somewhere, how noise is kept out of a ranking, and what a target and a comparison are.</summary>
public class EconomyReportTests
{
    private static readonly SkillRung Weak = SkillRung.Parse("skill-45");
    private static readonly SkillRung Strong = SkillRung.Parse("skill-65");

    private static string[] Cells(string csv, Func<string[], bool> row) =>
        csv.Split('\n').Select(line => line.Split(',')).Single(cells => cells.Length > 1 && row(cells));

    [Fact]
    public void A_quantile_fewer_players_reached_is_left_empty()
    {
        Assert.Equal(3, Statistics.QuantileOrLater([4, 3], outOf: 10, new Percent(10)));
        Assert.Equal(4, Statistics.QuantileOrLater([4, 3], outOf: 10, new Percent(20)));
        Assert.Null(Statistics.QuantileOrLater([4, 3], outOf: 10, Statistics.Median));
        Assert.Null(Statistics.QuantileOrLater(Array.Empty<int>(), outOf: 0, Statistics.Median));

        // A tally counts the same way: of ten players two have a value, so the median falls among those who have none.
        var attemptsNeeded = new Histogram(bound: 100);
        attemptsNeeded.Add(4);
        attemptsNeeded.Add(3);
        Assert.Equal(3, attemptsNeeded.QuantileOrLater(outOf: 10, new Percent(10)));
        Assert.Equal(4, attemptsNeeded.QuantileOrLater(outOf: 10, new Percent(20)));
        Assert.Null(attemptsNeeded.QuantileOrLater(outOf: 10, Statistics.Median));
        Assert.Equal(3, attemptsNeeded.Quantile(Statistics.Median));

        // The second level is one only the strong rung passes: a quarter of the mix.
        EconomySettings settings = SmallWorld.Settings(
            """{ "report": { "milestones": [1, 2], "paceTarget": { "level": 2 } } }""",
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": null, "buyAfterLossesOnLevel": null } } }""");
        HandMadeLevels walled = new HandMadeLevels(Weak, Average, Strong)
            .Level(20, Always(Won(15)))
            .Level(20, rung => rung == Strong ? [Always(Won(15))] : [Always(Unfinished())]);

        EconomyReport report = SmallWorld.Report(settings, walled);

        PaceRow ofTheMix = report.Pace.Single(row => row.Population.Name == Population.MixName && row.Level.Value == 2);
        Assert.Equal(0.25, ofTheMix.ReachedShare);
        Assert.Equal(1, ofTheMix.DaysP10);
        Assert.Null(ofTheMix.DaysP50);
        Assert.Null(ofTheMix.DaysP90);
        Assert.Equal(PaceVerdict.NotReached, report.Pace.Single(row => row.Population.IsAverage && row.Level.Value == 2).Verdict);
        Assert.Equal(1, report.Pace.Single(row => row.Population.Name == Strong.Name && row.Level.Value == 2).DaysP90);

        // In the file an unknown value is an empty cell, never a 0.
        string[] cells = Cells(report.Files()["pace.csv"], row => row[1] == Population.MixName && row[4] == "2");
        Assert.Equal(["0.250", "1", "", ""], cells[6..10]);

        // The level is named as a wall for the rungs it stops, and those players are still on it.
        Assert.Equal([new Wall(new LevelId(2), Weak), new Wall(new LevelId(2), Average)], report.Walls);
        Assert.Equal(30, report.Stalls.Single(row => row.Population.Name == Population.MixName && row.Level.Value == 2).StillOnIt);
        Assert.Contains("WALLS: no stored game is won", report.Files()[EconomyReport.SummaryFile]);
    }

    [Fact]
    public void The_stall_list_is_ranked_on_one_half_of_the_games_and_read_from_the_other()
    {
        // A level's first seed holds an even game (stored first) and an odd one. The second level is lost on its even
        // game and won on its odd one, the third the other way round; the first is won on both.
        MeasuredGame[] wonOnBoth = [Won(15), Won(15)];
        HandMadeLevels levels = new HandMadeLevels()
            .Level(20, wonOnBoth)
            .Level(20, [Unfinished(), Won(15)], wonOnBoth)
            .Level(20, [Won(15), Unfinished()], wonOnBoth);
        EconomySettings settings = OnePlayer("""{ "players": { "count": 20 }, "report": { "milestones": [3], "paceTarget": { "level": 3 } } }""");

        EconomyReport report = SmallWorld.Report(settings, levels);

        StallRow second = report.Stalls.Single(row => row.Level.Value == 2);
        StallRow third = report.Stalls.Single(row => row.Level.Value == 3);
        Assert.Equal(1, second.RankOnEvenGames);
        Assert.Equal(2.0, second.AttemptsMeanOnEvenGames);
        Assert.Equal(1.0, second.AttemptsMeanOnOddGames);
        Assert.Equal(3, third.RankOnEvenGames);
        Assert.Equal(1.0, third.AttemptsMeanOnEvenGames);
        Assert.Equal(2.0, third.AttemptsMeanOnOddGames);
        Assert.InRange(second.AttemptsMean!.Value, 1.0, 2.0);

        Population average = report.Populations.Single(population => population.IsAverage);
        Assert.Equal([2, 1, 3], StallReport.Worst(report.Stalls, average, 3).Select(row => row.Level.Value));
        Assert.Equal(0.5, second.FirstTryWinShare);
        Assert.Equal(2, second.FirstSeedGames);
        Assert.True(second.FirstTryBand!.Value.Low < 0.5 && second.FirstTryBand.Value.High > 0.5);

        // The summary lists the level that tops the even games with the attempts of the odd games beside it.
        string listed = report.Files()[EconomyReport.SummaryFile].Split('\n').First(line => line.TrimStart().StartsWith("2 ", StringComparison.Ordinal) && line.Contains("Normal"));
        Assert.Contains("1.00", listed);
        Assert.Contains("2.00", listed);
        Assert.True(listed.IndexOf("1.00", StringComparison.Ordinal) < listed.IndexOf("2.00", StringComparison.Ordinal));
    }

    [Fact]
    public void The_pace_verdict_is_read_against_the_target_and_forces_nothing()
    {
        var target = new PaceTarget(new LevelId(100), From: new DayNumber(10), To: new DayNumber(14));
        Assert.Equal(PaceVerdict.Faster, PaceReport.VerdictOn(9, target));
        Assert.Equal(PaceVerdict.Within, PaceReport.VerdictOn(10, target));
        Assert.Equal(PaceVerdict.Within, PaceReport.VerdictOn(14, target));
        Assert.Equal(PaceVerdict.Slower, PaceReport.VerdictOn(15, target));
        Assert.Equal(PaceVerdict.NotReached, PaceReport.VerdictOn(null, target));

        // Another target is another verdict on the same journeys: no other report moves.
        EconomyReport early = SmallWorld.Report(SmallWorld.Settings("""{ "report": { "paceTarget": { "fromDays": 1, "toDays": 1 } } }"""));
        EconomyReport late = SmallWorld.Report(SmallWorld.Settings("""{ "report": { "paceTarget": { "fromDays": 20, "toDays": 30 } } }"""));

        PaceRow earlyRow = early.Pace.Single(row => row.Population.IsAverage && row.Verdict is not null);
        PaceRow lateRow = late.Pace.Single(row => row.Population.IsAverage && row.Verdict is not null);
        Assert.Equal(earlyRow.DaysP50, lateRow.DaysP50);
        Assert.Equal(PaceVerdict.Faster, lateRow.Verdict);
        Assert.NotEqual(PaceVerdict.Faster, earlyRow.Verdict);
        Assert.Equal(
            ["pace.csv", "settings.csv", EconomyReport.SummaryFile],
            early.Files().Keys.Where(file => early.Files()[file] != late.Files()[file]));
    }

    [Fact]
    public void Two_files_with_other_players_are_compared_without_per_player_differences()
    {
        EconomySettings a = SmallWorld.Settings();
        EconomySettings sameUnderShorterSessions = SmallWorld.Settings("""{ "name": "short", "habit": { "sessionCeilingMinutes": 5 } }""");
        EconomySettings otherPlayers = SmallWorld.Settings("""{ "name": "others", "players": { "seed": 9 } }""");

        Comparison samePlayers = Compared(a, sameUnderShorterSessions);
        Comparison others = Compared(a, otherPlayers);

        Assert.True(samePlayers.HaveTheSamePlayers);
        Assert.Equal(["habit.sessionCeilingMinutes", "name"], samePlayers.Fields.Select(field => field.Field).Order());
        Assert.Equal(new FieldDifference("habit.sessionCeilingMinutes", "20", "5"), samePlayers.Fields.Single(field => field.Field.StartsWith("habit", StringComparison.Ordinal)));
        ComparisonRow days = samePlayers.Rows.Single(row => row.Population.Name == Average.Name && row.Figure == ComparedFigure.DaysP50 && row.Level.Value == 30);
        Assert.True(days.B > days.A);
        Assert.Equal(days.B - days.A, days.Change);
        Assert.True(days.ChangeP10 >= 0 && days.ChangeP90 >= days.ChangeP10);

        // Every figure a single player has comes with the spread of the players' own changes.
        Assert.All(samePlayers.Rows.Where(row => row.Figure != ComparedFigure.ReachedShare), row => Assert.NotNull(row.ChangeP10));

        Assert.False(others.HaveTheSamePlayers);
        Assert.Equal(["name", "players.seed"], others.Fields.Select(field => field.Field).Order());
        Assert.All(others.Rows, row => Assert.Null(row.ChangeP10));
        Assert.All(others.Rows, row => Assert.Null(row.ChangeP90));
        Assert.Contains(others.Rows, row => row.A is not null && row.B is not null);
        Assert.Contains("do not simulate the same players", others.DifferencesText());

        // The share that reached a level is no single player's figure: it has no change of a player's own.
        Assert.All(samePlayers.Rows.Where(row => row.Figure == ComparedFigure.ReachedShare), row => Assert.Null(row.ChangeP10));
        string[] cells = Cells(samePlayers.TableText(), row => row[0] == Average.Name && row[1] == "reachedShare" && row[2] == "30");
        Assert.Equal(["1.000", "1.000", "0.000", "", ""], cells[4..9]);
    }

    private static Comparison Compared(EconomySettings a, EconomySettings b)
    {
        Measurement measurement = SmallWorld.Levels().Build();
        SimulatedLevels levelsOfA = SimulatedLevels.Of(a, measurement);
        SimulatedLevels levelsOfB = SimulatedLevels.Of(b, measurement);
        return Comparison.Of(
            new Comparison.Simulated(a, PopulationRun.PlayPass(a, levelsOfA, MeasuredHalf.All, dayEndsKept: []), levelsOfA.Count),
            new Comparison.Simulated(b, PopulationRun.PlayPass(b, levelsOfB, MeasuredHalf.All, dayEndsKept: []), levelsOfB.Count),
            measurement.Spec);
    }
}
