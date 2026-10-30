using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The "+5 moves" offer in the simulator (docs/GAME_DESIGN.md section 19): made when the moves run out, taken once,
/// for an ad three times a day and for coins beyond that. The levels here have 20 moves.
/// </summary>
public class EconomyOfferTests
{
    private const int Limit = 20;

    private static readonly EconomySettings TakesOffers = OnePlayer("""{ "player": { "extraMoves": { "watchesAds": true, "paysCoins": true } } }""");

    private static AttemptReading Read(MeasuredGame game, EconomySettings? settings = null, int freeAds = 3, long coins = 0)
    {
        settings ??= TakesOffers;
        var reader = new AttemptReader(Spec, settings.Player.InLevelBoosters, new ExtraMovesRule(settings.Player.ExtraMoves, settings.ExtraMovesOffer.Price));
        return reader.Read(game, new AttemptTerms(new MoveCount(Limit), WithExtraMovesBooster: false, InLevelBoosterAtHand: null, LossesOnLevel: 0, new Coins(coins), freeAds));
    }

    [Fact]
    public void Extra_moves_are_offered_only_to_a_game_out_of_moves_that_can_go_on()
    {
        AttemptReading won = Read(Won(15, nearlyDoneOn: 10));
        AttemptReading stuckEarly = Read(Stuck(12, nearlyDoneOn: 10));
        AttemptReading stuckOnTheLastMove = Read(Stuck(Limit, nearlyDoneOn: 10));
        AttemptReading outOfMoves = Read(Unfinished(nearlyDoneOn: 10));

        Assert.IsType<ExtraMovesDecision.NotOffered>(won.ExtraMoves);
        Assert.False(won.RanOutOfMoves);

        Assert.IsType<ExtraMovesDecision.NotOffered>(stuckEarly.ExtraMoves);
        Assert.IsType<AttemptEnding.LostNoMovePossible>(stuckEarly.Ending);
        Assert.Equal(new MoveCount(12), stuckEarly.MovesPlayed);

        // The engine calls this game out of moves, and sells it none: nobody could play them.
        Assert.IsType<ExtraMovesDecision.NotOffered>(stuckOnTheLastMove.ExtraMoves);
        Assert.IsType<AttemptEnding.LostOutOfMoves>(stuckOnTheLastMove.Ending);

        Assert.IsType<ExtraMovesDecision.TakenForAd>(outOfMoves.ExtraMoves);
        Assert.True(outOfMoves.RanOutOfMoves);
    }

    [Fact]
    public void They_are_taken_once_per_attempt()
    {
        // Two moves short even with the five: the attempt ends at 25 moves, and no second offer is made.
        AttemptReading reading = Read(Won(27, nearlyDoneOn: 10), coins: 5000);

        Assert.IsType<ExtraMovesDecision.TakenForAd>(reading.ExtraMoves);
        Assert.IsType<AttemptEnding.LostOutOfMoves>(reading.Ending);
        Assert.Equal(new MoveCount(Limit + 5), reading.MovesPlayed);
    }

    [Fact]
    public void The_fourth_offer_of_a_day_costs_coins()
    {
        EconomySettings settings = OnePlayer(
            """{ "player": { "extraMoves": { "watchesAds": true, "paysCoins": true } } }""",
            """{ "coins": { "perLevelWon": 1000 } }""");
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(1, Limit, movesLeft: 5).Level(Limit, Always(Unfinished(nearlyDoneOn: 10)));

        AttemptPlayed[] nearMisses = Journey(settings, levels).Attempts.Where(attempt => attempt.Level.Value == 2).Take(5).ToArray();

        Assert.All(nearMisses.Take(3), attempt => Assert.IsType<ExtraMovesDecision.TakenForAd>(attempt.Reading.ExtraMoves));
        Assert.Equal(new ExtraMovesDecision.TakenForCoins(new Coins(900)), nearMisses[3].Reading.ExtraMoves);
        Assert.Equal(new Coins(1050 - 900), nearMisses[3].After.Balance);
        Assert.Equal(new ExtraMovesDecision.WantedUnpaid(new Shortfall(Price: new Coins(900), Missing: new Coins(750))), nearMisses[4].Reading.ExtraMoves);
    }

    [Fact]
    public void A_player_short_of_coins_goes_without_and_the_shortfall_is_counted()
    {
        EconomySettings paysCoins = OnePlayer("""{ "extraMovesOffer": { "freeAdsPerDay": 0 }, "player": { "extraMoves": { "paysCoins": true } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Unfinished(nearlyDoneOn: 10)));

        Assert.Equal(
            new ExtraMovesDecision.WantedUnpaid(new Shortfall(Price: new Coins(900), Missing: new Coins(900))),
            Read(Unfinished(nearlyDoneOn: 10), paysCoins, freeAds: 0).ExtraMoves);
        Assert.Equal(new ExtraMovesDecision.WantedUnpaid(Shortfall: null), Read(Unfinished(nearlyDoneOn: 10), OnePlayer(), freeAds: 0).ExtraMoves);

        PassResult pass = PopulationRun.PlayPass(paysCoins, levels.For(paysCoins), MeasuredHalf.All, dayEndsKept: [new DayNumber(1)]);
        JourneyTotals firstDay = pass.Players.Single().AtDayEnds[0].Totals;
        Assert.Equal(firstDay.Attempts, firstDay.ExtraMovesWantedUnpaid);
        Assert.Equal(new Unpaid(firstDay.Attempts, Gaps: new Coins(900L * firstDay.Attempts), Price: new Coins(900L * firstDay.Attempts)), firstDay.UnpaidExtraMoves);
        Assert.Equal(0, firstDay.ExtraMovesTaken);
    }

    [Fact]
    public void Coins_a_player_holds_are_missing_from_no_gap_and_pay_for_one_want_only()
    {
        // 500 coins and three unpaid offers at 900: each time 400 are missing, 1200 in all, and yet buying the three
        // would have taken 2200 more than the player had. The gaps and the prices are kept apart for that reason.
        EconomySettings settings = OnePlayer(
            """{ "extraMovesOffer": { "freeAdsPerDay": 0 }, "player": { "extraMoves": { "paysCoins": true } } }""",
            """{ "coins": { "perLevelWon": 450 }, "lives": { "max": 3 } }""");
        HandMadeLevels levels = new HandMadeLevels().EasyLevels(1, Limit, movesLeft: 5).Level(Limit, Always(Unfinished(nearlyDoneOn: 10)));

        PassResult pass = PopulationRun.PlayPass(settings, levels.For(settings), MeasuredHalf.All, dayEndsKept: [new DayNumber(1)]);
        JourneyList journey = Journey(settings, levels);

        SessionEnded firstSession = journey.Sessions.First();
        AttemptPlayed[] unpaid = journey.Attempts.Skip(1).Take(3).ToArray();
        Assert.Equal(4, firstSession.Attempts);
        Assert.Equal(new Coins(500), firstSession.After.Balance);
        Assert.All(unpaid, attempt => Assert.Equal(new ExtraMovesDecision.WantedUnpaid(new Shortfall(Price: new Coins(900), Missing: new Coins(400))), attempt.Reading.ExtraMoves));

        // Three sessions of three lost lives in the day: nine unpaid offers.
        JourneyTotals firstDay = pass.Players.Single().AtDayEnds[0].Totals;
        Assert.Equal(new Unpaid(Wants: 9, Gaps: new Coins(9 * 400), Price: new Coins(9 * 900)), firstDay.UnpaidExtraMoves);
        Assert.Equal(firstDay.UnpaidExtraMoves, firstDay.UnpaidInAll);
        Assert.Equal(9, firstDay.ExtraMovesWantedUnpaid);

        // A player who never pays coins has wants and no shortfall: nothing they would have bought.
        EconomySettings neverPays = OnePlayer("""{ "lives": { "max": 3 } }""");
        JourneyTotals ofNeverPaying = PopulationRun.PlayPass(neverPays, levels.For(neverPays), MeasuredHalf.All, dayEndsKept: [new DayNumber(1)]).Players.Single().AtDayEnds[0].Totals;
        Assert.Equal(9, ofNeverPaying.ExtraMovesWantedUnpaid);
        Assert.Equal(default(Unpaid), ofNeverPaying.UnpaidExtraMoves);
    }

    [Fact]
    public void The_player_decides_on_what_they_see_not_on_whether_it_would_win()
    {
        // Both are out of moves with 80 % of the goals done; only the first would win with five more.
        Assert.IsType<ExtraMovesDecision.TakenForAd>(Read(Won(Limit + 3, nearlyDoneOn: 10)).ExtraMoves);
        Assert.IsType<ExtraMovesDecision.TakenForAd>(Read(Unfinished(nearlyDoneOn: 10)).ExtraMoves);

        // This one would win on the next move, and nothing the player sees says so.
        AttemptReading farFromDone = Read(Won(Limit + 1));
        Assert.IsType<ExtraMovesDecision.NotWanted>(farFromDone.ExtraMoves);
        Assert.IsType<AttemptEnding.LostOutOfMoves>(farFromDone.Ending);

        // The rule is handed the progress, the losses, the coins and the ads left: there is no game in what it reads.
        Assert.Equal(
            ["Balance", "FreeAdsLeftToday", "GoalsDone", "LossesOnLevel"],
            typeof(OutOfMovesView).GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public void Bought_moves_left_over_are_paid_by_the_finale()
    {
        EconomySettings settings = OnePlayer("""{ "player": { "extraMoves": { "watchesAds": true } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(Limit + 2, nearlyDoneOn: 10)));

        AttemptPlayed attempt = Journey(settings, levels).Attempts.Single();

        Assert.Equal(new AttemptEnding.Won(new MoveCount(3)), attempt.Reading.Ending);
        Assert.Equal(new Coins(30), attempt.FinaleCoins);
        Assert.Equal(new Coins(30), attempt.After.Balance);
    }
}
