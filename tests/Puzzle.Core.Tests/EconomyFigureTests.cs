using Puzzle.Core.Levels;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The figures of the economy's reports, each counted by hand on a game small enough to follow: what an offer is
/// worth, what one player's lives, coins and boosters come to, and what a row says when it is not about everyone.
/// </summary>
public class EconomyFigureTests
{
    private const int Limit = 20;

    private static readonly SkillRung Weak = SkillRung.Parse("skill-45");
    private static readonly SkillRung Strong = SkillRung.Parse("skill-65");

    /// <summary>Figures side by side, an unknown one among them as it is: two lists of them are equal figure by figure.</summary>
    private static double?[] Figures(params double?[] figures) => figures;

    private static string[] Cells(string csv, Func<string[], bool> row) =>
        csv.Split('\n').Select(line => line.Split(',')).Single(cells => cells.Length > 1 && row(cells));

    /// <summary>
    /// One player on two levels. The first is won at once with five moves left. The second is lost five times, each
    /// time out of moves with the goals nearly done and too few coins for the offer, and won on the sixth seed.
    /// </summary>
    private static EconomyReport OnePlayersTwoLevels()
    {
        EconomySettings settings = OnePlayer(
            """{ "coins": { "perLevelWon": 100 } }""",
            """{ "extraMovesOffer": { "freeAdsPerDay": 0 }, "player": { "extraMoves": { "paysCoins": true } } }""",
            """{ "rewards": { "daily": { "boostersEachTime": 1 } } }""",
            """{ "report": { "milestones": [2], "paceTarget": { "level": 2 }, "balanceAtDays": [1] } }""");
        MeasuredGame[] nearMiss = Always(Unfinished(nearlyDoneOn: 10));
        HandMadeLevels levels = new HandMadeLevels()
            .Level(Limit, Always(Won(15)))
            .Level(Limit, nearMiss, nearMiss, nearMiss, nearMiss, nearMiss, Always(Won(15)));
        return SmallWorld.Report(settings, levels);
    }

    [Fact]
    public void An_offer_is_worth_the_wins_it_adds_on_the_stored_games()
    {
        // Two stored games of one seed: one is won on move 15 of 20, the other needs 22 and has 80 % of the goals at the limit.
        Measurement measurement = new HandMadeLevels().Level(Limit, [Won(15), Won(22, nearlyDoneOn: 10)]).Build();

        OfferRow[] rows = OfferReport.Rows(OnePlayer(), measurement, lastLevel: 1).Where(row => row.Levels.Name == "all levels").ToArray();

        // The offer's five moves win the second game with three left; without it half the attempts are won, with five left.
        OfferRow offer = rows.Single(row => row.Offer is Offered.ExtraMoves && row.Condition is OfferCondition.Any);
        Assert.Equal(Figures(2, 0.5, 1.0, 5.0, 4.0), Figures(offer.Games, offer.WinWithout, offer.WinWith, offer.MovesLeftWithout, offer.MovesLeftWith));
        Assert.Equal(50.0, offer.GainPoints);
        Assert.Equal(new Coins(900), offer.Price);
        Assert.Equal(new Coins(18), offer.CoinsPerGainPoint);

        // Of the attempts that ran out of moves, the one there is had between 80 and 90 % of the goals done, and the offer wins it.
        OfferRow nearlyDone = rows.Single(row => row.Condition == new OfferCondition.OutOfMovesWithGoals(new Percent(80), new Percent(90)));
        Assert.Equal(Figures(1, 0.0, 1.0, 3.0), Figures(nearlyDone.Games, nearlyDone.WinWithout, nearlyDone.WinWith, nearlyDone.MovesLeftWith));
        Assert.Null(nearlyDone.MovesLeftWithout);
        Assert.Equal("goals 80 to 90 %", nearlyDone.Condition.Name);
        Assert.All(
            rows.Where(row => row.Condition is OfferCondition.OutOfMovesWithGoals && row != nearlyDone),
            row => Assert.Equal(Figures(0, null, null), Figures(row.Games, row.WinWithout, row.WinWith)));
        Assert.Equal(
            ["goals below 50 %", "goals 50 to 80 %", "goals 80 to 90 %", "goals 90 % or more"],
            rows.Where(row => row.Condition is OfferCondition.OutOfMovesWithGoals).Select(row => row.Condition.Name));

        // The booster's three moves win the second game too, with one left; the first then has eight.
        OfferRow booster = rows.Single(row => row.Offer == new Offered.StartBooster(BoosterKind.StartExtraMoves));
        Assert.Equal(Figures(0.5, 1.0, 4.5), Figures(booster.WinWithout, booster.WinWith, booster.MovesLeftWith));
        Assert.Equal(new Coins(10), booster.CoinsPerGainPoint);

        // No board booster was measured, so none has a row; and the table writes the rows as they are.
        Assert.Equal(6, rows.Length);
        string table = OfferReport.Table(rows).ToText();
        Assert.Contains("skill-55,all levels,extra-moves-offer,any,2,0.500,1.000,50.0,5.0,4.0,900,18\n", table);
        Assert.Contains("skill-55,all levels,start-extra-moves,any,2,0.500,1.000,50.0,5.0,4.5,500,10\n", table);
        Assert.Contains("skill-55,all levels,extra-moves-offer,goals below 50 %,0,,,,,,900,\n", table);
    }

    [Fact]
    public void What_lives_cost_one_player_is_counted_to_the_milestone_and_the_timer_is_told_apart()
    {
        EconomyReport report = OnePlayersTwoLevels();

        // The first session: a win, then five losses from 08:35:50 on, over by 08:49:10 with 50 seconds of its 20 minutes
        // left. The life the first loss costs is due at 09:05:50. The second level is won in the second session.
        LivesRow lives = report.Lives.Single();
        Assert.Equal(Figures(1, 1.0, 1.0, 5.0, 1.0), Figures(lives.Players, lives.ReachedShare, lives.SessionsMean, lives.LivesLostMean, lives.RefillMomentsMean));
        Assert.Equal(Figures(1.0, 0.0, 0.0), Figures(lives.EndedByLivesShare, lives.EndedAtCeilingShare, lives.StartedWithoutALifeShare));
        Assert.Equal(50 / 3600.0, lives.HoursDeniedMean!.Value, precision: 9);
        Assert.Equal(Figures(0.0, 0.0), Figures(lives.StartedBelowFullShare, lives.LivesBackInSessionsMean));
        Assert.Equal(1000 / 60.0, lives.MinutesToNextLifeMean!.Value, precision: 9);

        string[] cells = Cells(report.Files()["lives.csv"], row => row[0] == "skill-55");
        Assert.Equal(["skill-55", "2", "1", "1.000", "1.0", "1.000", "0.000", "0.000", "5.0", "1.0", "0.0", "0.0", "0.0", "0.000", "0.00", "16.7"], cells);

        // The summary says in words that the timer did nothing in this run.
        Assert.Contains("THE TIMER DID NOTHING HERE", report.Files()[EconomyReport.SummaryFile]);

        // The whole journey: two sessions, one ended by lives and one by the last level; 1150 and 150 seconds long.
        HabitRow habit = report.Habit.Single();
        Assert.Equal(Figures(0.5, 0.0, 0.0, 0.5), Figures(habit.EndedByLivesShare, habit.EndedAtCeilingShare, habit.StartedWithoutALifeShare, habit.EndedWithTheLastLevelShare));
        Assert.Equal(1300 / 60.0 / 2, habit.SessionMinutesMean!.Value, precision: 9);
        Assert.Equal(1300 / 60.0, habit.MinutesADayMean!.Value, precision: 9);
    }

    [Fact]
    public void A_timer_that_gives_lives_back_during_sessions_is_said_to_have_acted()
    {
        // Two minutes a life and a level nobody wins: lives come back while the sessions run.
        EconomySettings settings = OnePlayer(
            """{ "lives": { "minutesPerLife": 2 } }""",
            """{ "report": { "milestones": [1], "paceTarget": { "level": 1 }, "balanceAtDays": [1] } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(6, Always(Unfinished()), Always(Unfinished()), Always(Won(4))).Level(6, Always(Unfinished()));

        EconomyReport report = SmallWorld.Report(settings, levels);

        // Up to the first level, won on the third attempt of the first session, nothing came back yet; the summary reads every row.
        Assert.Equal(0.0, report.Lives.Single().LivesBackInSessionsMean);
        Assert.Contains("THE TIMER DID NOTHING HERE", report.Files()[EconomyReport.SummaryFile]);

        EconomySettings toTheSecond = OnePlayer(
            """{ "lives": { "minutesPerLife": 2 } }""",
            """{ "report": { "milestones": [1, 2], "paceTarget": { "level": 1 }, "balanceAtDays": [1] } }""");
        HandMadeLevels wonLater = new HandMadeLevels()
            .Level(6, Always(Won(4)))
            .Level(6, Enumerable.Repeat(Always(Unfinished()), 9).Append(Always(Won(4))).ToArray());

        EconomyReport acted = SmallWorld.Report(toTheSecond, wonLater);

        // The second level takes a second session: the first ended by lives after lives had come back during it.
        LivesRow atTheSecond = acted.Lives.Single(row => row.Level.Value == 2);
        Assert.True(atTheSecond.LivesBackInSessionsMean > 0);
        Assert.Equal(1.0, atTheSecond.RefillMomentsMean);
        Assert.DoesNotContain("THE TIMER DID NOTHING HERE", acted.Files()[EconomyReport.SummaryFile]);
        Assert.Contains("The 2-minute timer acted in this run", acted.Files()[EconomyReport.SummaryFile]);
    }

    [Fact]
    public void What_one_player_earns_spends_and_goes_without_is_counted_to_the_milestone()
    {
        EconomyReport report = OnePlayersTwoLevels();

        // Two wins of 50 coins for the moves left and 100 flat. Five times the offer was wanted with 150 coins in hand.
        CoinsRow atTheMilestone = report.Coins.Single(row => row.At == BalanceMoment.Milestone);
        Assert.Equal(Figures(2, 1, 1.0), Figures(atTheMilestone.Value, atTheMilestone.Players, atTheMilestone.ReachedShare));
        Assert.Equal(new Coins(300), atTheMilestone.BalanceP50);
        Assert.Equal(new Coins(100), atTheMilestone.EarnedFinale);
        Assert.Equal(new Coins(200), atTheMilestone.EarnedLevelWon);
        Assert.Equal(Coins.None, atTheMilestone.SpentExtraMoves);
        Assert.Equal(Coins.None, atTheMilestone.SpentStartBoosters);
        Assert.Equal(new Coins(5 * 750), atTheMilestone.GapsAtUnpaidWants);
        Assert.Equal(new Coins(5 * 900), atTheMilestone.PriceOfUnpaidWants);
        Assert.Equal(Figures(0, 1), Figures(atTheMilestone.BalanceBuysExtraMoves, atTheMilestone.BalanceBuysSidePieces));

        // The end of the first day finds the journey over: the player stands as they ended.
        CoinsRow atTheEndOfDayOne = report.Coins.Single(row => row.At == BalanceMoment.EndOfDay);
        Assert.Equal(Figures(1, 1.0, 0, 2), Figures(atTheEndOfDayOne.Players, atTheEndOfDayOne.ReachedShare, atTheEndOfDayOne.PlayersStillPlaying, atTheEndOfDayOne.LevelP50));
        Assert.Equal(new Coins(300), atTheEndOfDayOne.BalanceP50);

        string[] coins = Cells(report.Files()["coins.csv"], row => row[1] == "level");
        Assert.Equal(["skill-55", "level", "2", "1", "1.000", "0", "2", "300", "300", "300", "100", "200", "0", "0", "0", "0", "3750", "4500", "0", "1"], coins);

        SpendingRow spending = report.Spending.Single();
        Assert.Equal(Figures(5.0, 5.0, 5.0), Figures(spending.TimesOutOfMoves, spending.ExtraMovesWanted, spending.ExtraMovesWantedADay));
        Assert.Equal(Figures(0.0, 0.0, 5.0), Figures(spending.ExtraMovesByAd, spending.ExtraMovesBought, spending.ExtraMovesWantedUnpaid));
        Assert.Equal(new Coins(3750), spending.GapsAtUnpaidExtraMoves);
        Assert.Equal(new Coins(4500), spending.PriceOfUnpaidExtraMoves);
        Assert.Null(spending.ShareOfTakenExtraMovesThatWon);
        Assert.Equal(0.0, spending.StartBoostersWantedUnpaid);
        Assert.Equal(Coins.None, spending.GapsAtUnpaidStartBoosters);
        Assert.Equal(Coins.None, spending.PriceOfUnpaidStartBoosters);

        string[] spent = Cells(report.Files()["spending.csv"], row => row[0] == "skill-55");
        Assert.Equal(["skill-55", "2", "1", "1.000", "5.00", "5.00", "5.00", "0.00", "0.00", "5.00", "3750", "4500", "", "0.00", "0.00", "0", "0"], spent);

        // The day's reward is the first booster of the rotation; this player never starts with one, so it is held.
        BoosterRow rewarded = report.Boosters.Single(row => row.Booster == BoosterKind.StartRocketAndBomb);
        Assert.Equal(Figures(1.0, 0.0, 0.0, 1.0), Figures(rewarded.Received, rewarded.Used, rewarded.Bought, rewarded.Held));
        Assert.All(report.Boosters.Where(row => row != rewarded), row => Assert.Equal(Figures(0.0, 0.0), Figures(row.Received, row.Held)));
        Assert.Contains("skill-55,2,start-rocket-and-bomb,1,1.000,1.00,0.00,0.00,1.00\n", report.Files()["boosters.csv"]);

        // The summary names the two sums for what they are and claims neither as the size of a purchase.
        string summary = report.Files()[EconomyReport.SummaryFile];
        Assert.Contains("unpaid wants: gaps", summary);
        Assert.Contains("their full price", summary);
        Assert.Contains("neither is the size of a purchase", summary);
        Assert.Contains("wants the +5 5.00 times a day, and 0 a day cost an ad", summary);
    }

    [Fact]
    public void A_player_who_arrives_at_a_level_and_plays_nothing_of_it_is_not_one_of_its_players()
    {
        // One day of three sessions of eight wins: the day ends on a win, with the 25th level reached and not started.
        EconomySettings oneDay = OnePlayer("""{ "players": { "horizonDays": 1 }, "report": { "milestones": [10], "paceTarget": { "level": 10 }, "balanceAtDays": [1] } }""");
        EconomyReport wonThroughout = SmallWorld.Report(oneDay, new HandMadeLevels().EasyLevels(40));

        StallRow lastPlayed = wonThroughout.Stalls.Single(row => row.Level.Value == 24);
        StallRow arrivedAt = wonThroughout.Stalls.Single(row => row.Level.Value == 25);
        Assert.Equal(Figures(1, 0, 1.0), Figures(lastPlayed.Players, lastPlayed.StillOnIt, lastPlayed.AttemptsMean));
        Assert.Equal((0, 0), (arrivedAt.Players, arrivedAt.StillOnIt));
        Assert.Null(arrivedAt.AttemptsMean);
        Assert.Null(arrivedAt.AttemptsP50);

        // A mean of attempts per player who played a level is never below one.
        Assert.All(wonThroughout.Stalls.Where(row => row.AttemptsMean is not null), row => Assert.True(row.AttemptsMean >= 1));
        Assert.All(wonThroughout.Stalls.Where(row => row.AttemptsMeanOnEvenGames is not null), row => Assert.True(row.AttemptsMeanOnEvenGames >= 1));

        // A player who did start a level and never won it is still on it, with every attempt they made.
        EconomyReport stuck = SmallWorld.Report(oneDay, new HandMadeLevels().EasyLevels(3).Level(6, Always(Unfinished())).EasyLevels(6));
        StallRow wall = stuck.Stalls.Single(row => row.Level.Value == 4);
        Assert.Equal(Figures(1, 1, 15.0), Figures(wall.Players, wall.StillOnIt, wall.AttemptsMean));
        Assert.Equal(0, stuck.Stalls.Single(row => row.Level.Value == 5).Players);
    }

    [Fact]
    public void A_row_about_the_players_who_got_there_says_how_many_they_were()
    {
        // The second level is one only the strong rung passes: a quarter of the mix.
        EconomySettings settings = SmallWorld.Settings(
            """{ "report": { "milestones": [1, 2], "paceTarget": { "level": 2 } } }""",
            """{ "player": { "startBoosters": { "useAfterLossesOnLevel": null, "buyAfterLossesOnLevel": null } } }""");
        HandMadeLevels walled = new HandMadeLevels(Weak, Average, Strong)
            .Level(Limit, Always(Won(15)))
            .Level(Limit, rung => rung == Strong ? [Always(Won(15))] : [Always(Unfinished())]);

        EconomyReport report = SmallWorld.Report(settings, walled);

        LivesRow ofTheMix = report.Lives.Single(row => row.Population.Name == Population.MixName && row.Level.Value == 2);
        LivesRow ofTheWeak = report.Lives.Single(row => row.Population.Name == Weak.Name && row.Level.Value == 2);
        Assert.Equal(Figures(10, 0.25), Figures(ofTheMix.Players, ofTheMix.ReachedShare));
        Assert.Equal(Figures(0, 0.0), Figures(ofTheWeak.Players, ofTheWeak.ReachedShare));
        Assert.Null(ofTheWeak.SessionsMean);
        Assert.Equal(0.25, report.Coins.Single(row => row.Population.Name == Population.MixName && row.At == BalanceMoment.Milestone && row.Value == 2).ReachedShare);
        Assert.Equal(0.25, report.Spending.Single(row => row.Population.Name == Population.MixName && row.Level.Value == 2).ReachedShare);
        Assert.All(report.Boosters.Where(row => row.Population.Name == Population.MixName && row.Level.Value == 2), row => Assert.Equal(0.25, row.ReachedShare));
        Assert.All(report.Lives.Where(row => row.Level.Value == 1), row => Assert.Equal(1.0, row.ReachedShare));

        // In the files the share stands beside the number of players, and a population nobody of which arrived has empty cells.
        Assert.Equal(["mix", "2", "10", "0.250"], Cells(report.Files()["lives.csv"], row => row[0] == Population.MixName && row[1] == "2")[..4]);
        Assert.Equal(["skill-45", "2", "0", "0.000", "", ""], Cells(report.Files()["lives.csv"], row => row[0] == Weak.Name && row[1] == "2")[..6]);

        // And the summary says under each table that such a row is about the faster players only.
        string summary = report.Files()[EconomyReport.SummaryFile];
        Assert.Equal(3, summary.Split("NOTE: where 'reached' is below 100 %").Length - 1);
        Assert.DoesNotContain("NOTE: where 'reached' is below 100 %", SmallWorld.Report(SmallWorld.Settings()).Files()[EconomyReport.SummaryFile]);
    }

    [Fact]
    public void Two_files_are_compared_over_the_players_who_reached_a_level_under_both()
    {
        // The same players and the same games under both files; the second follows them for one day only, so only the
        // quickest of them have reached the later milestones under it.
        EconomySettings whole = SmallWorld.Settings("""{ "report": { "milestones": [10, 20, 30] } }""");
        EconomySettings oneDay = SmallWorld.Settings("""{ "name": "one-day", "players": { "horizonDays": 1 }, "report": { "milestones": [10, 20, 30], "balanceAtDays": [1] } }""");
        Measurement measurement = SmallWorld.Levels().Build();
        PassResult passOfWhole = PopulationRun.PlayPass(whole, SimulatedLevels.Of(whole, measurement), MeasuredHalf.All, dayEndsKept: []);
        PassResult passOfOneDay = PopulationRun.PlayPass(oneDay, SimulatedLevels.Of(oneDay, measurement), MeasuredHalf.All, dayEndsKept: []);

        Comparison comparison = Comparison.Of(
            new Comparison.Simulated(whole, passOfWhole, SmallWorld.LevelCount),
            new Comparison.Simulated(oneDay, passOfOneDay, SmallWorld.LevelCount),
            measurement.Spec);

        Assert.True(comparison.HaveTheSamePlayers);
        ComparisonRow reached = comparison.Rows.First(row => row.Population.Name == Population.MixName && row.Figure == ComparedFigure.ReachedShare && row.B is > 0 and < 1);
        Assert.Equal(1.0, reached.A);
        int milestone = whole.Report.Milestones.ToList().IndexOf(reached.Level);

        // Up to their milestone the players who got there in a day went the same way under both files: nothing changes
        // for them, and the figures of the first file are theirs too, not those of all its players.
        Checkpoint[] quickestUnderWhole = passOfWhole.Players.Zip(passOfOneDay.Players)
            .Where(player => player.Second.AtMilestones[milestone] is not null)
            .Select(player => player.First.AtMilestones[milestone]!)
            .ToArray();
        double adsOfTheQuickest = quickestUnderWhole.Average(checkpoint => checkpoint.Totals.ExtraMovesByAd);
        double adsOfEveryone = passOfWhole.Players.Average(player => player.AtMilestones[milestone]!.Totals.ExtraMovesByAd);
        ComparisonRow ads = comparison.Rows.Single(row => row.Population.Name == Population.MixName && row.Figure == ComparedFigure.ExtraMovesByAd && row.Level == reached.Level);
        Assert.NotEqual(adsOfEveryone, adsOfTheQuickest);
        Assert.Equal(Figures(adsOfTheQuickest, adsOfTheQuickest, 0.0), Figures(ads.A, ads.B, ads.Change));
        Assert.Equal(Figures(0.0, 0.0), Figures(ads.ChangeP10, ads.ChangeP90));
        ComparedFigure[] ofThoseWhoReached = [ComparedFigure.CoinsBalanceP50, ComparedFigure.GapsAtUnpaidWants, ComparedFigure.PriceOfUnpaidWants, ComparedFigure.StartBoostersUsed];
        Assert.All(
            comparison.Rows.Where(row => row.Population.Name == Population.MixName && row.Level == reached.Level && ofThoseWhoReached.Contains(row.Figure)),
            row => Assert.Equal(0.0, row.Change));

        // The days are a median over every player with those who never arrive counted as later: each file's own.
        ComparisonRow days = comparison.Rows.Single(row => row.Population.Name == Population.MixName && row.Figure == ComparedFigure.DaysP50 && row.Level == reached.Level);
        Assert.NotNull(days.A);
        Assert.Contains("over the players who reached the level under both files", comparison.DifferencesText());
        Assert.Contains("Where reachedShare is below 1", comparison.DifferencesText());
    }

    [Fact]
    public void A_figure_reads_the_same_in_a_comparison_as_in_the_report_it_comes_from()
    {
        // 8810 extra moves bought by 2000 players: the same mean, written by the same rule in both files.
        double mean = 8810 / 2000.0;
        Assert.Equal("4.41", Cell.Fine(mean));
        Assert.Equal(Cell.Fine(mean), FigureUnit.TimesPerPlayer.Written(mean));
        Assert.Equal(Cell.Fine(mean), FigureUnit.Hours.Written(mean));
        Assert.Equal(Cell.Share(0.1235), FigureUnit.ShareOfThePlayers.Written(0.1235));

        // Coins, days and attempts are whole; a change that rounds to nothing has no sign.
        Assert.Equal("11289", FigureUnit.Coins.Written(11289.4));
        Assert.Equal("-13", FigureUnit.Days.Written(-12.6));
        Assert.Equal("0", FigureUnit.Coins.Written(-0.4));
        Assert.Equal("0.00", FigureUnit.TimesPerPlayer.Written(-0.001));
        Assert.Equal("-0.01", FigureUnit.TimesPerPlayer.Written(-0.006));
        Assert.Equal("0.000", FigureUnit.ShareOfAPlayersSessions.Written(-0.0001));
    }

    [Fact]
    public void A_report_says_which_measurement_it_read_whatever_the_settings_ask_for()
    {
        // The fixture's measurement block asks for 64, 32, 16 and 8 plain games and 4 boosted; the hand-made measurement has two of each.
        EconomyReport report = SmallWorld.Report(SmallWorld.Settings());
        string settingsList = report.Files()["settings.csv"];
        string summary = report.Files()[EconomyReport.SummaryFile];

        Assert.Equal(
            ["plainGamesBySeedPosition 64/32/16/8 instead of 2", "boostedGamesPerSeed 4 instead of 2"],
            report.SettingsAskForAnotherMeasurement);
        Assert.Contains("measurement.plainGamesBySeedPosition,64 32 16 8,games,Tool\n", settingsList);
        Assert.Contains("measurementRead.plainGamesBySeedPosition,2,games,Tool\n", settingsList);
        Assert.Contains("measurementRead.boostedGamesPerSeed,2,games,Tool\n", settingsList);
        Assert.Contains("measurementRead.moveCap,150,moves,Tool\n", settingsList);
        Assert.Contains("measurementRead.extraMovesOfferMoves,5,moves,Tool\n", settingsList);
        Assert.Contains("its games: followed to move 150, bot repeats from 10000, goals marked at 50, 80, 90 %, 2 plain games by pool position, 2 on a board that starts with a booster", summary);
        Assert.Contains("NOTE: the measurement block of the settings asks for another measurement than the one that was read (plainGamesBySeedPosition 64/32/16/8 instead of 2; boostedGamesPerSeed 4 instead of 2)", summary);

        // Settings that ask for the measurement that was read have nothing to note; no figure moves either way.
        EconomyReport agreeing = SmallWorld.Report(SmallWorld.Settings("""{ "measurement": { "plainGamesBySeedPosition": [2], "boostedGamesPerSeed": 2 } }"""));
        Assert.Empty(agreeing.SettingsAskForAnotherMeasurement);
        Assert.DoesNotContain("NOTE: the measurement block", agreeing.Files()[EconomyReport.SummaryFile]);
        Assert.Equal(report.Files()["pace.csv"], agreeing.Files()["pace.csv"]);
        Assert.Equal(report.Files()["coins.csv"], agreeing.Files()["coins.csv"]);

        // A comparison lists the block among the settings that differ, and says that it changes no figure.
        Measurement measurement = SmallWorld.Levels().Build();
        EconomySettings a = SmallWorld.Settings();
        EconomySettings b = SmallWorld.Settings("""{ "name": "agreeing", "measurement": { "plainGamesBySeedPosition": [2], "boostedGamesPerSeed": 2 } }""");
        Comparison comparison = Comparison.Of(
            new Comparison.Simulated(a, PopulationRun.PlayPass(a, SimulatedLevels.Of(a, measurement), MeasuredHalf.All, dayEndsKept: []), SmallWorld.LevelCount),
            new Comparison.Simulated(b, PopulationRun.PlayPass(b, SimulatedLevels.Of(b, measurement), MeasuredHalf.All, dayEndsKept: []), SmallWorld.LevelCount),
            measurement.Spec);
        Assert.NotEmpty(comparison.AAsksForAnotherMeasurement);
        Assert.Empty(comparison.BAsksForAnotherMeasurement);
        Assert.Contains("NOTE: the measurement block of a asks for another measurement than the one both were read from", comparison.DifferencesText());
        Assert.DoesNotContain("NOTE: the measurement block of b", comparison.DifferencesText());
        Assert.All(comparison.Rows.Where(row => row.Change is not null), row => Assert.Equal(0.0, row.Change));
    }

    [Fact]
    public void A_level_on_which_the_player_never_wants_the_extra_moves_is_marked_and_the_groups_set_it_apart()
    {
        // Each level's first seed holds an even game (stored first) and an odd one; its second seed is always won.
        // Level 2 is lost on its even game with nothing of the goals to show; level 3 on its odd game with 80 % done.
        MeasuredGame[] wonOnBoth = [Won(15), Won(15)];
        HandMadeLevels levels = new HandMadeLevels()
            .Level(Limit, wonOnBoth)
            .Level(Limit, [Unfinished(), Won(15)], wonOnBoth)
            .Level(Limit, [Won(15), Unfinished(nearlyDoneOn: 10)], wonOnBoth);
        EconomySettings settings = OnePlayer("""{ "players": { "count": 20 }, "report": { "milestones": [3], "paceTarget": { "level": 3 } } }""");

        EconomyReport report = SmallWorld.Report(settings, levels);

        StallRow first = report.Stalls.Single(row => row.Level.Value == 1);
        StallRow second = report.Stalls.Single(row => row.Level.Value == 2);
        StallRow third = report.Stalls.Single(row => row.Level.Value == 3);
        Assert.Null(first.ExtraMovesWantableShare);
        Assert.False(first.ExtraMovesNeverWanted);
        Assert.Equal(0.0, second.ExtraMovesWantableShare);
        Assert.True(second.ExtraMovesNeverWanted);
        Assert.Equal(1.0, third.ExtraMovesWantableShare);
        Assert.False(third.ExtraMovesNeverWanted);

        // The second level tops the list on the even games and is last on the odd ones; the third the other way round.
        Assert.Equal((1, 3), (second.RankOnEvenGames, second.RankOnOddGames));
        Assert.Equal((3, 1), (third.RankOnEvenGames, third.RankOnOddGames));
        Assert.Equal((2, 2), (first.RankOnEvenGames, first.RankOnOddGames));

        // By group the two halves agree: four attempts on three levels in each. The level of the blind spot stands apart.
        Population average = report.Populations.Single(population => population.IsAverage);
        StallGroupRow[] groups = StallReport.ByGroup(report.Stalls, average).ToArray();
        Assert.Equal(["first room", "all levels"], groups.Select(group => group.Group.Name));
        StallGroupRow all = groups[1];
        Assert.Equal((3, 1), (all.Levels, all.NeverWantingExtraMoves));
        Assert.Equal(4 / 3.0, all.AttemptsMeanOnEvenGames!.Value, precision: 9);
        Assert.Equal(4 / 3.0, all.AttemptsMeanOnOddGames!.Value, precision: 9);
        Assert.Equal(second.AttemptsMean, all.AttemptsMeanThere);
        Assert.Equal((first.AttemptsMean + third.AttemptsMean) / 2, all.AttemptsMeanOnTheOthers);

        string[] cells = Cells(report.Files()["stalls.csv"], row => row[0] == "2");
        Assert.Equal(["0.000", "0.000"], cells[18..20]);
        Assert.Equal(["1", "3"], cells[22..24]);

        string summary = report.Files()[EconomyReport.SummaryFile];
        string listedSecond = summary.Split('\n').First(line => line.TrimStart().StartsWith("2 ", StringComparison.Ordinal) && line.Contains("Normal"));
        string listedThird = summary.Split('\n').First(line => line.TrimStart().StartsWith("3 ", StringComparison.Ordinal) && line.Contains("Normal"));
        Assert.EndsWith("NEVER", listedSecond);
        Assert.EndsWith("yes", listedThird);
        Assert.Contains("3 of these 3 are also among the 10 worst on the odd games", summary);
        Assert.Contains("by group of levels", summary);
    }
}
