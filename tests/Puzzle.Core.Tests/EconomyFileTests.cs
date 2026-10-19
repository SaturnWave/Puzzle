using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Puzzle.Bot;
using Puzzle.Economy.Settings;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using SimulatedBoosters = Puzzle.Economy.Units.BoosterKinds;

namespace Puzzle.Core.Tests;

/// <summary>
/// The economy settings as the game reads them: the pinned pack of economy/default.json
/// (tests/fixtures/economy-pack.json, which tools/unity/economy-pack.test.mjs holds to the pack script's output)
/// gives the game the numbers the simulator reads from the settings file itself, and a pack the game cannot play by
/// is refused by the name of the field.
/// </summary>
public class EconomyFileTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static string PinnedPackText => File.ReadAllText(RepoFiles.EconomyPackPath);

    private static EconomyFile PinnedPack() => JsonSerializer.Deserialize<EconomyFile>(PinnedPackText, AsUnityMapsIt)!;

    private static EconomyFormatException Refused(Action<EconomyFile> change) =>
        Assert.Throws<EconomyFormatException>(() => MetaSupport.Rules(change));

    [Fact]
    public void The_pinned_pack_gives_the_game_the_simulators_reading_of_the_shipped_settings_number_for_number()
    {
        EconomyRules game = EconomyFileMapper.ToRules(PinnedPack());
        EconomySettings simulated = SettingsLoader.Load(RepoFiles.EconomySettingsPath("default.json"));

        // What both play by.
        Assert.Equal(simulated.Lives.Max.Value, game.Lives.Max);
        Assert.Equal(simulated.Lives.PerLife, game.Lives.PerLife);
        Assert.Equal(simulated.Coins.PerMoveLeftInFinale.Amount, game.Win.PerMoveLeft.Amount);
        Assert.Equal(simulated.Coins.PerLevelWon.Amount, game.Win.PerLevelWon.Amount);
        AssertSameReward(simulated.Rewards.PaintingCompleted, game.PaintingCompleted.Rotation);
        AssertSameReward(simulated.Rewards.Daily, game.Daily.Rotation);
        Assert.Equal(simulated.Rewards.DailyEveryDays, game.Daily.EveryDays);
        Assert.Equal(simulated.ExtraMovesOffer.FreeAdsPerDay, game.ExtraMoves.FreeAdsPerDay);
        Assert.Equal(simulated.ExtraMovesOffer.Price.Amount, game.ExtraMoves.Price.Amount);
        Assert.Equal(
            simulated.Prices.Boosters.ToDictionary(price => SimulatedBoosters.Name(price.Key), price => price.Value.Amount),
            BoosterKinds.All.ToDictionary(BoosterKinds.Name, kind => game.Shop.PriceOf(kind).Amount));

        // The welcome gift, which both play by.
        Assert.Equal(simulated.Start.Coins.Amount, game.StartingWallet.Coins.Amount);
        Assert.Equal(
            simulated.Start.Boosters.ToDictionary(held => SimulatedBoosters.Name(held.Key), held => held.Value),
            BoosterKinds.All.ToDictionary(BoosterKinds.Name, game.StartingWallet.Shelf.Count));

        // What the game alone plays by, and the simulator only reads.
        GameOnlySettings stated = simulated.Game;
        Assert.Equal(stated.StartBoostersPerAttempt, game.StartBoosters.PerAttempt);
        Assert.Equal(new HintRules(stated.Hints.Idle, stated.Hints.PerAttempt, stated.Hints.Bot), game.Hints);
        Assert.True(HintAdvisor.TryCreate(game.Hints.Bot, out _), $"The shipped hints show the moves of '{game.Hints.Bot}', which gives no hints.");
        Assert.Equal(
            stated.CoinPacks.ToDictionary(pack => pack.Key, pack => pack.Value.Amount),
            game.Shop.CoinPacks.ToDictionary(pack => pack.Name, pack => pack.Coins.Amount));
        Assert.Equal(stated.BoosterSets.Keys.Order(), game.Shop.BoosterSets.Select(set => set.Name).Order());
        Assert.All(game.Shop.BoosterSets, set =>
        {
            BoosterSetSettings same = stated.BoosterSets[set.Name];
            Assert.Equal(same.Price.Amount, set.Price.Amount);
            Assert.Equal(
                same.Boosters.ToDictionary(held => SimulatedBoosters.Name(held.Key), held => held.Value),
                BoosterKinds.All.Where(set.Boosters.Has).ToDictionary(BoosterKinds.Name, set.Boosters.Count));
        });
        Assert.Equal(stated.CoinsForAnAd.Amount, game.Shop.AdForCoins.Coins.Amount);
        Assert.Equal(stated.AdsForCoinsPerDay, game.Shop.AdForCoins.PerDay);
        Assert.Equal((AdsInTheBuild.Simulated, RealMoneyInTheBuild.NotAvailable), (stated.Ads, stated.RealMoneyPurchases));
        Assert.Equal((AdSource.Simulated, RealMoneySales.NotAvailable), (game.Ads, game.Shop.RealMoney));
    }

    [Fact]
    public void The_pinned_pack_holds_what_is_decided_and_starts_a_new_player_whole()
    {
        EconomyRules game = EconomyFileMapper.ToRules(PinnedPack());

        // Decided (GAME_DESIGN section 19): five lives, one back every thirty minutes. Every other number of the pack
        // is an amount its owner changes: no test holds it.
        Assert.Equal(new LifeRules(max: 5, perLife: TimeSpan.FromMinutes(30)), game.Lives);

        // A new player has every life, has used nothing and has been given nothing yet.
        Wallet start = game.StartingWallet;
        Assert.True(start.Lives.IsFull);
        Assert.Equal(AdsWatched.None, start.Ads);
        Assert.Equal(RewardsGiven.None, start.Rewards);
    }

    [Fact]
    public void The_file_model_holds_the_whole_pack_field_for_field_and_in_its_order()
    {
        string pinned = JsonNode.Parse(PinnedPackText)!.ToJsonString();

        string heldAndWrittenAgain = JsonSerializer.Serialize(PinnedPack(), AsUnityMapsIt);

        // Nothing of the pack is unknown to the model and nothing of the model is missing from the pack: either
        // would be read as nothing or as 0 by Unity's reader, without a word. The Editor's EconomyCheck asks the
        // same of Unity's own reader and writer.
        Assert.Equal(pinned, heldAndWrittenAgain);
        Assert.Equal(EconomyFile.CurrentFormatVersion, PinnedPack().formatVersion);
    }

    [Fact]
    public void The_tests_own_pack_is_one_the_game_can_play_by()
    {
        EconomyRules rules = MetaSupport.Rules();

        Assert.Equal(new Coins(100), rules.StartingWallet.Coins);
        Assert.Equal(BoosterShelf.Empty.WithOneMore(BoosterKind.StartRocketAndBomb).With(BoosterKind.Shuffle, 2), rules.StartingWallet.Shelf);
        Assert.Equal(2, rules.StartBoosters.PerAttempt);
        Assert.Equal(new HintRules(TimeSpan.FromSeconds(8), perAttempt: 2, bot: "novice"), rules.Hints);
    }

    [Fact]
    public void A_document_that_is_no_pack_or_a_pack_of_another_format_is_refused()
    {
        // What Unity's reader makes of an empty document or of one about something else: every number 0.
        var noPack = new EconomyFile();

        Assert.Contains("format version 0", Assert.Throws<EconomyFormatException>(() => EconomyFileMapper.ToRules(noPack)).Message);
        Assert.Contains("format version 2", Refused(pack => pack.formatVersion = 2).Message);
        Assert.Contains("format version 4", Refused(pack => pack.formatVersion = 4).Message);
    }

    [Fact]
    public void The_boosters_used_during_play_open_on_their_levels_with_their_gift_and_no_start_booster_has_either()
    {
        EconomyRules rules = MetaSupport.Rules();

        Assert.Equal(new[] { 7, 12, 16, 20 }, BoosterKinds.InLevel.Select(rules.Unlocks.LevelOf));
        Assert.Equal((3, 2, 1, 0), (rules.Unlocks.Gift.Count(BoosterKind.HitCell), rules.Unlocks.Gift.Count(BoosterKind.HitRow), rules.Unlocks.Gift.Count(BoosterKind.HitColumn), rules.Unlocks.Gift.Count(BoosterKind.Shuffle)));
        Assert.Contains("lacks shuffle", Refused(pack => pack.boosterUnlocks.levels = pack.boosterUnlocks.levels.Where(opens => opens.booster != "shuffle").ToArray()).Message);
        Assert.Contains("lacks hit-row", Refused(pack => pack.boosterUnlocks.gift = pack.boosterUnlocks.gift.Where(given => given.booster != "hit-row").ToArray()).Message);
        Assert.Contains("a start booster", Refused(pack => pack.boosterUnlocks.levels = [.. pack.boosterUnlocks.levels, MetaSupport.Opens("start-color-bomb", 3)]).Message);
        Assert.Contains("a start booster", Refused(pack => pack.boosterUnlocks.gift = [.. pack.boosterUnlocks.gift, MetaSupport.Count("start-color-bomb", 1)]).Message);
        Assert.Contains("boosterUnlocks.levels.hit-cell 0 must be at least 1", Refused(pack => pack.boosterUnlocks.levels[0].level = 0).Message);
        Assert.Contains("boosterUnlocks.gift.hit-cell -1 must be at least 0", Refused(pack => pack.boosterUnlocks.gift[0].count = -1).Message);
        Assert.Contains("states 'hit-row' twice", Refused(pack => pack.boosterUnlocks.levels = [.. pack.boosterUnlocks.levels, MetaSupport.Opens("hit-row", 3)]).Message);
    }

    [Fact]
    public void A_missing_booster_price_is_refused_by_the_boosters_name()
    {
        EconomyFormatException missing = Refused(pack => pack.prices.boosters = pack.prices.boosters.Where(price => price.booster != "hit-column").ToArray());
        EconomyFormatException none = Refused(pack => pack.prices.boosters = []);
        EconomyFormatException twice = Refused(pack => pack.prices.boosters = [.. pack.prices.boosters, MetaSupport.Price("shuffle", 300)]);
        EconomyFormatException startNotStated = Refused(pack => pack.start.boosters = pack.start.boosters.Where(held => held.booster != "hit-cell").ToArray());

        Assert.Contains("prices.boosters must state every booster; it lacks hit-column", missing.Message);
        Assert.Contains("it lacks start-rocket-and-bomb, start-color-bomb, start-extra-moves, hit-cell, hit-row, hit-column, shuffle", none.Message);
        Assert.Contains("prices.boosters states 'shuffle' twice", twice.Message);
        Assert.Contains("start.boosters must state every booster; it lacks hit-cell", startNotStated.Message);
    }

    [Fact]
    public void An_unknown_booster_name_is_refused_wherever_it_stands()
    {
        Assert.Contains("prices.boosters names 'hit-diagonal', which is no booster (start-rocket-and-bomb, ", Refused(pack => pack.prices.boosters[3].booster = "hit-diagonal").Message);
        Assert.Contains("rewards.paintingCompleted.boosterRotation names 'Shuffle'", Refused(pack => pack.rewards.paintingCompleted.boosterRotation = ["shuffle", "Shuffle"]).Message);
        Assert.Contains("rewards.daily.boosterRotation names ''", Refused(pack => pack.rewards.daily.boosterRotation = [""]).Message);
        Assert.Contains("start.boosters names 'bomb'", Refused(pack => pack.start.boosters[0].booster = "bomb").Message);
        Assert.Contains("shop.boosterSets.master.boosters names 'rocket'", Refused(pack => pack.shop.boosterSets[1].boosters[0].booster = "rocket").Message);
    }

    [Theory]
    [InlineData("coins.perMoveLeftInFinale")]
    [InlineData("coins.perLevelWon")]
    [InlineData("rewards.paintingCompleted.coins")]
    [InlineData("rewards.daily.coins")]
    [InlineData("extraMovesOffer.priceCoins")]
    [InlineData("prices.boosters.hit-row")]
    [InlineData("start.coins")]
    [InlineData("shop.boosterSets.starter.priceCoins")]
    [InlineData("shop.coinPacks.small.coins")]
    [InlineData("shop.adForCoins.coins")]
    public void A_negative_amount_and_one_no_sum_could_hold_are_refused_by_their_field(string field)
    {
        EconomyFormatException negative = Refused(pack => SetAmount(pack, field, -1));
        EconomyFormatException tooLarge = Refused(pack => SetAmount(pack, field, EconomyFileMapper.LargestAmount + 1));

        Assert.Contains($"{field} -1 must be", negative.Message);
        Assert.Contains($"{field} 1000001 must be an amount of coins from 0 to 1000000", tooLarge.Message);
    }

    [Fact]
    public void A_negative_count_is_refused_by_its_field()
    {
        Assert.Contains("lives.max 0 must be at least 1", Refused(pack => pack.lives.max = 0).Message);
        Assert.Contains("lives.minutesPerLife 0 must be at least 1", Refused(pack => pack.lives.minutesPerLife = 0).Message);
        Assert.Contains("rewards.daily.everyDays 0 must be at least 1", Refused(pack => pack.rewards.daily.everyDays = 0).Message);
        Assert.Contains("rewards.daily.boostersEachTime -1 must be at least 0", Refused(pack => pack.rewards.daily.boostersEachTime = -1).Message);
        Assert.Contains("extraMovesOffer.freeAdsPerDay -1 must be at least 0", Refused(pack => pack.extraMovesOffer.freeAdsPerDay = -1).Message);
        Assert.Contains("shop.adForCoins.perDay -2 must be at least 0", Refused(pack => pack.shop.adForCoins.perDay = -2).Message);
        Assert.Contains("start.boosters.shuffle -1 must be at least 0", Refused(pack => pack.start.boosters[6].count = -1).Message);
        Assert.Contains("shop.boosterSets.starter.boosters.start-color-bomb 0 must be at least 1", Refused(pack => pack.shop.boosterSets[0].boosters[1].count = 0).Message);
        Assert.Contains("startBoosters.perAttempt 0 must be 1 to 3", Refused(pack => pack.startBoosters.perAttempt = 0).Message);
        Assert.Contains("startBoosters.perAttempt 4 must be 1 to 3", Refused(pack => pack.startBoosters.perAttempt = 4).Message);
        Assert.Contains("hints.idleSeconds 0 must be at least 1", Refused(pack => pack.hints.idleSeconds = 0).Message);
        Assert.Contains("hints.perAttempt -1 must be at least 0", Refused(pack => pack.hints.perAttempt = -1).Message);
        Assert.Contains("hints.bot '' names no bot", Refused(pack => pack.hints.bot = string.Empty).Message);
        Assert.Contains("hints.bot 'skill 60' names no bot", Refused(pack => pack.hints.bot = "skill 60").Message);

        // None a day, none each time and no pack at all are amounts like any other.
        EconomyRules bare = MetaSupport.Rules(pack =>
        {
            pack.extraMovesOffer.freeAdsPerDay = 0;
            pack.shop.adForCoins.perDay = 0;
            pack.rewards.daily.boostersEachTime = 0;
            pack.rewards.daily.boosterRotation = [];
            pack.shop.coinPacks = [];
            pack.shop.boosterSets = [];
        });
        Assert.Equal(0, bare.ExtraMoves.FreeAdsPerDay);
        Assert.Empty(bare.Shop.BoosterSets);
    }

    [Fact]
    public void A_build_that_claims_real_ads_or_real_purchases_is_refused()
    {
        Assert.Contains("build.ads is \"live\"; this build can only show an ad that is \"simulated\"", Refused(pack => pack.build.ads = "live").Message);
        Assert.Contains("build.ads is \"\"", Refused(pack => pack.build.ads = string.Empty).Message);
        Assert.Contains("build.realMoneyPurchases is \"store\"; this build sells nothing for money, which is \"notAvailable\"", Refused(pack => pack.build.realMoneyPurchases = "store").Message);
        Assert.Contains("build.realMoneyPurchases is \"Simulated\"", Refused(pack => pack.build.realMoneyPurchases = "Simulated").Message);
    }

    [Fact]
    public void A_price_the_finale_pays_back_is_refused_as_the_simulator_refuses_it()
    {
        // 10 coins a move left: a win on the first of the five bought moves leaves four, 40 coins; the +3 booster's moves pay 30.
        Assert.Contains("extraMovesOffer.priceCoins 40 must be above 40", Refused(pack => pack.extraMovesOffer.priceCoins = 40).Message);
        Assert.Contains("prices.boosters.start-extra-moves 30 must be above 30", Refused(pack => pack.prices.boosters[2].coins = 30).Message);
        Assert.Equal(new Coins(41), MetaSupport.Rules(pack => pack.extraMovesOffer.priceCoins = 41).ExtraMoves.Price);

        // The simulator's loader draws the same two lines, with the fixture's same 10 coins a move.
        Assert.Throws<EconomySettingsFormatException>(() => EconomySupport.Settings("""{ "extraMovesOffer": { "priceCoins": 40 } }"""));
        Assert.Throws<EconomySettingsFormatException>(() => EconomySupport.Settings("""{ "prices": { "boosters": { "start-extra-moves": 30 } } }"""));
        Assert.Equal(41, EconomySupport.Settings("""{ "extraMovesOffer": { "priceCoins": 41 } }""").ExtraMovesOffer.Price.Amount);
    }

    [Fact]
    public void Goods_that_contradict_themselves_are_refused()
    {
        Assert.Contains("rewards.paintingCompleted.boosterRotation is empty, so rewards.paintingCompleted.boostersEachTime must be 0", Refused(pack => pack.rewards.paintingCompleted.boosterRotation = []).Message);
        Assert.Contains("shop.boosterSets.starter.boosters is empty: a set gives at least one booster", Refused(pack => pack.shop.boosterSets[0].boosters = []).Message);
        Assert.Contains("shop.boosterSets.starter.boosters states 'shuffle' twice", Refused(pack => pack.shop.boosterSets[0].boosters = [MetaSupport.Count("shuffle", 1), MetaSupport.Count("shuffle", 2)]).Message);
        Assert.Contains("shop.boosterSets.starter.priceCoins 0 must be at least 1", Refused(pack => pack.shop.boosterSets[0].priceCoins = 0).Message);
        Assert.Contains("shop.coinPacks.small.coins 0 must be at least 1", Refused(pack => pack.shop.coinPacks[0].coins = 0).Message);
        Assert.Contains("shop.boosterSets states 'starter' twice", Refused(pack => pack.shop.boosterSets[1].name = "starter").Message);
        Assert.Contains("shop.coinPacks has an entry named 'big.one'", Refused(pack => pack.shop.coinPacks[1].name = "big.one").Message);
        Assert.Contains("shop.coinPacks has an entry named ''", Refused(pack => pack.shop.coinPacks[1].name = string.Empty).Message);
    }

    [Fact]
    public void A_list_the_document_writes_as_nothing_reads_as_an_empty_list()
    {
        // A JSON reader hands over null where the document says null; the mapper reads that as stated and empty, and judges it so.
        Assert.Contains("prices.boosters must state every booster", Refused(pack => pack.prices.boosters = null!).Message);
        Assert.Empty(MetaSupport.Rules(pack => pack.shop.coinPacks = null!).Shop.CoinPacks);
    }

    private static void AssertSameReward(RewardRule simulated, RewardRotation game)
    {
        Assert.Equal(simulated.Coins.Amount, game.Coins.Amount);
        Assert.Equal(simulated.BoostersEachTime, game.BoostersEachTime);
        Assert.Equal(simulated.BoosterRotation.Select(SimulatedBoosters.Name), game.Rotation.Select(BoosterKinds.Name));
    }

    /// <summary>Puts <paramref name="amount"/> where the settings' path <paramref name="field"/> leads in the tests' pack.</summary>
    private static void SetAmount(EconomyFile pack, string field, long amount)
    {
        switch (field)
        {
            case "coins.perMoveLeftInFinale":
                pack.coins.perMoveLeftInFinale = amount;
                break;
            case "coins.perLevelWon":
                pack.coins.perLevelWon = amount;
                break;
            case "rewards.paintingCompleted.coins":
                pack.rewards.paintingCompleted.coins = amount;
                break;
            case "rewards.daily.coins":
                pack.rewards.daily.coins = amount;
                break;
            case "extraMovesOffer.priceCoins":
                pack.extraMovesOffer.priceCoins = amount;
                break;
            case "prices.boosters.hit-row":
                pack.prices.boosters.Single(price => price.booster == "hit-row").coins = amount;
                break;
            case "start.coins":
                pack.start.coins = amount;
                break;
            case "shop.boosterSets.starter.priceCoins":
                pack.shop.boosterSets.Single(set => set.name == "starter").priceCoins = amount;
                break;
            case "shop.coinPacks.small.coins":
                pack.shop.coinPacks.Single(coinPack => coinPack.name == "small").coins = amount;
                break;
            case "shop.adForCoins.coins":
                pack.shop.adForCoins.coins = amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "The test knows no such amount.");
        }
    }
}
