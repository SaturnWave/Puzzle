using System.Text.Json.Nodes;
using Puzzle.Economy.Reporting;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Simulating;
using Puzzle.Economy.Units;
using static Puzzle.Core.Tests.EconomySupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// Paying players in the simulator: the store's products (the settings' store section) and the rules by which a
/// simulated player buys them (player.purchases). A purchase gives coins and boosters and costs money; it never
/// changes a game the player meets. The levels here have 20 moves, and the fixture's +5 costs 900 coins.
/// </summary>
public class EconomyPurchaseTests
{
    private const int Limit = 20;

    private const string BuysWhenShort =
        """{ "extraMovesOffer": { "freeAdsPerDay": 0 }, "player": { "extraMoves": { "paysCoins": true }, "purchases": { "packWhenShortForExtraMoves": "pouch" } } }""";

    private const string BuysTheStarterPack = """{ "player": { "purchases": { "buysTheStarterPack": true } } }""";

    private static EconomySettingsFormatException Refused(params string[] changes) =>
        Assert.Throws<EconomySettingsFormatException>(() => EconomySupport.Settings(changes));

    [Fact]
    public void The_store_is_read_whole_and_a_free_player_buys_nothing_from_it()
    {
        EconomySettings settings = EconomySupport.Settings();
        StoreSettings store = settings.Store;

        Assert.Equal(["pouch", "bag", "chest", "treasure"], store.CoinPacks.Select(pack => pack.Name));
        StoreProduct pouch = store.CoinPacks[0];
        Assert.Equal(("pouch", "coins.pouch", new UsDollars(199), new Coins(1000)), (pouch.Name, pouch.ProductIdSuffix, pouch.Price, pouch.Coins));
        Assert.Empty(pouch.Boosters);
        Assert.Equal("$19.99", store.CoinPacks[^1].Price.ToString());

        StarterPackSettings starter = store.StarterPack;
        Assert.Equal((StarterPackSettings.Name, "starter.apprentice_kit", new UsDollars(199), new Coins(1500)), (starter.Product.Name, starter.Product.ProductIdSuffix, starter.Product.Price, starter.Product.Coins));
        Assert.Equal([BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb, BoosterKind.StartExtraMoves], starter.Product.BoostersOneByOne);
        Assert.Equal((1, 20), (starter.TimesPerPlayer, starter.FromLevel.Value));
        Assert.Equal(store.CoinPacks.Append(starter.Product), store.Products);

        Assert.False(settings.Player.Purchases.PaysMoney);
        Assert.True(EconomySupport.Settings(BuysTheStarterPack).Player.Purchases.PaysMoney);
    }

    [Theory]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "productIdSuffix": "Coins.Pouch" } } } }""", "store.coinPacks.pouch.productIdSuffix 'Coins.Pouch' must be lowercase letters")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "productIdSuffix": "coins-pouch" } } } }""", "store.coinPacks.pouch.productIdSuffix 'coins-pouch'")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "productIdSuffix": ".pouch" } } } }""", "store.coinPacks.pouch.productIdSuffix '.pouch'")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "productIdSuffix": "coins..pouch" } } } }""", "store.coinPacks.pouch.productIdSuffix 'coins..pouch'")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "productIdSuffix": "" } } } }""", "store.coinPacks.pouch.productIdSuffix ''")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "priceUsdCents": 0 } } } }""", "store.coinPacks.pouch.priceUsdCents 0 must be a price in US cents from 1 to 99999")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "priceUsdCents": 100000 } } } }""", "store.coinPacks.pouch.priceUsdCents 100000")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "coins": 0 } } } }""", "store.coinPacks.pouch.coins 0 must be at least 1")]
    [InlineData("""{ "store": { "coinPacks": { "big.one": { "productIdSuffix": "coins.big", "priceUsdCents": 2999, "coins": 20000 } } } }""", "store.coinPacks has an entry named 'big.one'")]
    [InlineData("""{ "store": { "starterPack": { "productIdSuffix": "coins.pouch" } } }""", "two products have the product id suffix 'coins.pouch'")]
    [InlineData("""{ "store": { "starterPack": { "boosters": { "start-rainbow": 1 } } } }""", "store.starterPack.boosters names 'start-rainbow', which is no booster")]
    [InlineData("""{ "store": { "starterPack": { "boosters": { "start-color-bomb": 0 } } } }""", "store.starterPack.boosters.start-color-bomb 0 must be at least 1")]
    [InlineData("""{ "store": { "starterPack": { "timesPerPlayer": 0 } } }""", "store.starterPack.timesPerPlayer 0 must be at least 1")]
    [InlineData("""{ "store": { "starterPack": { "fromLevel": 0 } } }""", "store.starterPack.fromLevel 0 must be at least 1")]
    [InlineData("""{ "store": { "starterPack": { "priceUsdCents": 1.99 } } }""", "is not a valid economy settings file")]
    public void A_store_product_with_a_malformed_id_price_or_contents_is_refused(string change, string message) =>
        Assert.Contains(message, Refused(change).Message);

    [Fact]
    public void A_store_without_a_coin_pack_is_refused()
    {
        JsonObject document = FixtureDocument();
        document["store"]!["coinPacks"] = new JsonObject();

        var refused = Assert.Throws<EconomySettingsFormatException>(() => SettingsLoader.Read(document, "the test's settings"));

        Assert.Contains("store.coinPacks is empty", refused.Message);
    }

    [Theory]
    [InlineData("""{ "player": { "purchases": { "packWhenShortForExtraMoves": "crate" } } }""", "player.purchases.packWhenShortForExtraMoves names 'crate', which is no pack of store.coinPacks (pouch, bag, chest, treasure)")]
    [InlineData("""{ "player": { "extraMoves": { "paysCoins": false }, "purchases": { "packWhenShortForExtraMoves": "pouch" } } }""", "player.purchases.packWhenShortForExtraMoves needs player.extraMoves.paysCoins")]
    [InlineData("""{ "store": { "coinPacks": { "pouch": { "coins": 800 } } }, "player": { "purchases": { "packWhenShortForExtraMoves": "pouch" } } }""", "'pouch' gives 800 coins and must pay for the extra moves at 900 by itself")]
    [InlineData("""{ "player": { "purchases": { "scheduledPack": "crate" } } }""", "player.purchases.scheduledPack names 'crate'")]
    [InlineData("""{ "player": { "purchases": { "scheduledEveryDays": 0 } } }""", "player.purchases.scheduledEveryDays 0 must be at least 1")]
    public void A_purchase_habit_names_a_pack_of_the_store_and_buys_only_what_it_can_use(string change, string message) =>
        Assert.Contains(message, Refused(change).Message);

    [Fact]
    public void A_player_short_of_coins_for_the_extra_moves_buys_their_pack_and_pays_with_it()
    {
        // Won on move 23 of 20: the +5 wins it with 2 moves left. The first time the player has no coin; the second
        // time the flat reward of the first win pays, and nothing is bought.
        EconomySettings settings = OnePlayer(BuysWhenShort, """{ "coins": { "perLevelWon": 1000 } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(23, nearlyDoneOn: 10))).Level(Limit, Always(Won(23, nearlyDoneOn: 10)));

        JourneyList journey = Journey(settings, levels);
        AttemptPlayed[] attempts = journey.Attempts.ToArray();
        PurchaseMade purchase = Assert.Single(journey.Purchases);

        StoreProduct pouch = settings.Store.CoinPacks[0];
        Assert.Equal(new ExtraMovesDecision.TakenForCoins(new Coins(900), PackBoughtFirst: pouch), attempts[0].Reading.ExtraMoves);
        Assert.Equal((pouch, PurchaseOccasion.ShortForExtraMoves, attempts[0].DecidedAt), (purchase.Product, purchase.Occasion, purchase.At));
        Assert.Equal(new Coins(1000), purchase.After.Balance);
        Assert.True(journey.Events.IndexOf(purchase) < journey.Events.IndexOf(attempts[0]), "the purchase is told before the attempt it paid for");
        Assert.Equal(new Coins(1000 - 900 + 20 + 1000), attempts[0].After.Balance);
        Assert.Equal(new ExtraMovesDecision.TakenForCoins(new Coins(900)), attempts[1].Reading.ExtraMoves);
        Assert.All(attempts, attempt => Assert.IsType<AttemptEnding.Won>(attempt.Reading.Ending));

        JourneyTotals totals = PopulationRun.PlayPass(settings, levels.For(settings), MeasuredHalf.All, dayEndsKept: []).Players.Single().AtEnd.Totals;
        Assert.Equal((1, new UsDollars(199), new Coins(1000)), (totals.Purchases, totals.MoneySpent, totals.Earned(CoinSource.BoughtForMoney)));
        Assert.Equal((2, 0), (totals.ExtraMovesBought, totals.ExtraMovesWantedUnpaid));
    }

    [Fact]
    public void A_free_ad_comes_before_a_purchase()
    {
        EconomySettings settings = OnePlayer(BuysWhenShort, """{ "extraMovesOffer": { "freeAdsPerDay": 1 }, "player": { "extraMoves": { "watchesAds": true } } }""");
        HandMadeLevels levels = new HandMadeLevels().Level(Limit, Always(Won(23, nearlyDoneOn: 10))).Level(Limit, Always(Won(23, nearlyDoneOn: 10)));

        AttemptPlayed[] attempts = Journey(settings, levels).Attempts.ToArray();

        Assert.IsType<ExtraMovesDecision.TakenForAd>(attempts[0].Reading.ExtraMoves);
        Assert.Equal("pouch", Assert.IsType<ExtraMovesDecision.TakenForCoins>(attempts[1].Reading.ExtraMoves).PackBoughtFirst?.Name);
    }

    [Fact]
    public void The_starter_pack_is_bought_once_on_coming_to_its_level()
    {
        EconomySettings settings = OnePlayer(BuysTheStarterPack);

        JourneyList journey = Journey(settings, new HandMadeLevels().EasyLevels(25));
        PurchaseMade purchase = Assert.Single(journey.Purchases);

        // Nineteen wins of 5 moves left at 10 coins a move, then the pack's 1 500 coins and three start boosters.
        Assert.Equal((StarterPackSettings.Name, PurchaseOccasion.StarterPackOffered), (purchase.Product.Name, purchase.Occasion));
        Assert.Equal(19, purchase.After.LevelsWon);
        Assert.Equal(new Coins((19 * 50) + 1500), purchase.After.Balance);
        Assert.All(BoosterKinds.All.Where(BoosterKinds.IsStart), kind => Assert.Equal(1, purchase.After.Held.Count(kind)));
        Assert.Equal(journey.Attempts.Single(attempt => attempt.Level.Value == 19).DecidedAt, purchase.At);

        // From level 1 it is bought before the first attempt; a journey over before its level never meets it.
        PurchaseMade first = Assert.Single(Journey(OnePlayer(BuysTheStarterPack, """{ "store": { "starterPack": { "fromLevel": 1 } } }"""), new HandMadeLevels().EasyLevels(3)).Purchases);
        Assert.Equal(0, first.After.LevelsWon);
        Assert.Empty(Journey(settings, new HandMadeLevels().EasyLevels(19)).Purchases);
    }

    [Fact]
    public void A_scheduled_pack_is_bought_on_its_days_whatever_the_purse_holds()
    {
        // A level nobody wins keeps the journey going to the horizon.
        EconomySettings settings = OnePlayer("""{ "players": { "horizonDays": 70 }, "player": { "purchases": { "scheduledPack": "chest", "scheduledEveryDays": 30 } } }""");

        PurchaseMade[] purchases = Journey(settings, new HandMadeLevels().Level(Limit, Always(Unfinished()))).Purchases.ToArray();

        Assert.Equal([1, 31, 61], purchases.Select(purchase => purchase.At.Day.Value));
        Assert.All(purchases, purchase => Assert.Equal(PlayerTime.At(purchase.At.Day, settings.Habit.SessionStarts[0]), purchase.At));
        Assert.All(purchases, purchase => Assert.Equal(("chest", PurchaseOccasion.Scheduled), (purchase.Product.Name, purchase.Occasion)));
        Assert.Equal([new Coins(6000), new Coins(12000), new Coins(18000)], purchases.Select(purchase => purchase.After.Balance));
    }

    [Fact]
    public void Money_a_player_spends_never_changes_the_game_they_meet()
    {
        const string BuysEverything =
            """{ "player": { "extraMoves": { "paysCoins": true }, "purchases": { "packWhenShortForExtraMoves": "pouch", "buysTheStarterPack": true, "scheduledPack": "bag" } }, "extraMovesOffer": { "freeAdsPerDay": 0 }, "store": { "starterPack": { "fromLevel": 5 } } }""";
        int purchases = 0;
        foreach (SimulatedPlayer player in Enumerable.Range(1, 12).Select(seed => Player(seed: (ulong)seed)))
        {
            JourneyList free = Journey(OnePlayer(), SmallWorld.Levels(), player);
            JourneyList paying = Journey(OnePlayer(BuysEverything), SmallWorld.Levels(), player);
            Dictionary<(int Level, int Attempt), GameMet> metFree = free.Attempts.ToDictionary(attempt => (attempt.Level.Value, attempt.Attempt.Value), attempt => attempt.Met);
            Dictionary<(int Level, int Attempt), GameMet> metPaying = paying.Attempts.ToDictionary(attempt => (attempt.Level.Value, attempt.Attempt.Value), attempt => attempt.Met);
            (int Level, int Attempt)[] inBoth = metFree.Keys.Intersect(metPaying.Keys).ToArray();

            Assert.True(inBoth.Length >= SmallWorld.LevelCount);
            Assert.All(inBoth, attempt => Assert.Equal(metFree[attempt], metPaying[attempt]));
            purchases += paying.Purchases.Count();
        }

        // The paying journeys really bought: the schedule's first pack and the starter pack at least, for each player.
        Assert.True(purchases >= 2 * 12, $"{purchases} purchases in twelve paying journeys");
    }

    [Fact]
    public void A_paying_run_reports_what_was_bought_and_a_free_run_only_noughts()
    {
        EconomyReport paying = SmallWorld.Report(SmallWorld.Settings(BuysTheStarterPack));
        EconomyReport free = SmallWorld.Report(SmallWorld.Settings());

        PurchaseRow atTheEnd = paying.Purchases.Single(row => row.Population.Name == Population.MixName && row.Level.Value == 30);
        Assert.Equal((1.0, 1.0), (atTheEnd.PurchasesMean, atTheEnd.PayingShare));
        Assert.Equal((new UsDollars(199), new UsDollars(199), new Coins(1500)), (atTheEnd.SpentMean, atTheEnd.SpentP50, atTheEnd.CoinsBoughtMean));
        Assert.Equal(new Coins(1500), paying.Coins.Single(row => row.Population.Name == Population.MixName && row.At == BalanceMoment.Milestone && row.Value == 30).EarnedBoughtForMoney);
        Assert.Contains("mix,30,40,1.000,1.00,1.000,1.99,1.99,1.99,1500\n", paying.Files()["purchases.csv"]);

        string summary = paying.Files()[EconomyReport.SummaryFile];
        Assert.Contains("40 paying players", summary);
        Assert.Contains("PURCHASES FOR REAL MONEY", summary);
        Assert.Contains("the player buys the starter pack ($1.99: 1500 coins and 1 start-rocket-and-bomb, 1 start-color-bomb, 1 start-extra-moves) when the shop first offers it, on coming to level 20", summary);

        Assert.All(free.Purchases, row => Assert.Equal((0.0, 0.0, new UsDollars(0)), (row.PurchasesMean, row.PayingShare, row.SpentMean)));
        Assert.DoesNotContain("PURCHASES FOR REAL MONEY", free.Files()[EconomyReport.SummaryFile]);
        Assert.Contains("40 free players", free.Files()[EconomyReport.SummaryFile]);
    }
}
