using System.Text.Json;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Client.Gallery;
using Puzzle.Client.Screens;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The tester mode (issue #10): a test build's phone that plays on with coins and lives that have no end, while the
/// economy's rules stay as they are. <c>TesterMode</c>, <c>WalletHandling</c> and the ledgers, views, words and device
/// document they touch are the Unity project's source files, compiled into these tests; the rules are the tests' pack
/// (<see cref="MetaSupport"/>): a starter set for 2 100 and a master set for 6 300, the +5 for 900 coins or one of
/// three ads a day, 50 coins for one of two ads a day, five lives of thirty minutes.
/// </summary>
public class TesterModeTests
{
    private const int LevelsInThePack = 1000;

    private static readonly EconomyRules TheRules = Rules();
    private static readonly WalletLedger Shop = new(TheRules);
    private static readonly AttemptLedger Attempts = new(TheRules, LevelsInThePack);
    private static readonly DeviceSettings TurnedOn = DeviceSettings.Default.With(DeviceSetting.TesterMode, isOn: true);

    private static BoosterSet Set(string name) => TheRules.Shop.BoosterSets.Single(set => set.Name == name);

    private static WalletAnswer.Received Received(WalletAnswer answer) => Assert.IsType<WalletAnswer.Received>(answer);

    private static PlayerProgress OnLevelForty(Wallet wallet) => new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithWallet(wallet);

    /// <summary>A wallet with these coins whose meter misses one life, which comes back half an hour after the evening.</summary>
    private static Wallet OneLifeMissing(long coins) => WalletWith(coins).WithLives(new LifeMeter(1, Evening.AddMinutes(30)));

    [Fact]
    public void Only_a_build_whose_marks_say_it_is_a_test_build_has_the_tester_mode()
    {
        Assert.True(TesterMode.IsOfferedBy(AdSource.Simulated, RealMoneySales.NotAvailable));
        Assert.False(TesterMode.IsOfferedBy((AdSource)1, RealMoneySales.NotAvailable));
        Assert.False(TesterMode.IsOfferedBy(AdSource.Simulated, (RealMoneySales)1));
    }

    [Fact]
    public void The_wallet_is_unlimited_only_while_the_mode_is_on_in_a_build_that_has_it()
    {
        Assert.Equal(WalletHandling.Normal, TesterMode.HandlingFor(DeviceSettings.Default, AdSource.Simulated, RealMoneySales.NotAvailable));
        Assert.Equal(WalletHandling.Unlimited, TesterMode.HandlingFor(TurnedOn, AdSource.Simulated, RealMoneySales.NotAvailable));
        Assert.Equal(WalletHandling.Normal, TesterMode.HandlingFor(TurnedOn, (AdSource)1, (RealMoneySales)1));
    }

    [Fact]
    public void A_first_start_has_the_tester_mode_off_and_it_turns_on_and_off_as_a_setting()
    {
        Assert.False(DeviceSettings.Default.TesterMode);
        Assert.True(TurnedOn.IsOn(DeviceSetting.TesterMode));
        Assert.Equal((true, true), (TurnedOn.Sound, TurnedOn.Vibration));
        Assert.Equal(DeviceSettings.Default, TurnedOn.With(DeviceSetting.TesterMode, isOn: false));
        Assert.NotEqual(DeviceSettings.Default, TurnedOn);
        Assert.Equal("Test modu", SettingsWords.NameOf(DeviceSetting.TesterMode));
    }

    [Fact]
    public void A_set_bought_in_the_tester_mode_gives_its_boosters_and_leaves_the_coins_even_where_they_do_not_cover_it()
    {
        Wallet wallet = WalletWith(120);

        WalletAnswer.Received bought = Received(Shop.Answer(new WalletRequest.BuyASet(Set("master")), wallet, Evening, WalletHandling.Unlimited));

        Assert.Equal(new Coins(120), bought.Wallet.Coins);
        Assert.Equal(12, bought.Wallet.Shelf.Total);
        Assert.Equal(12, bought.What.Boosters.Count);
        Assert.IsType<WalletRefusal.CoinsShort>(Assert.IsType<WalletAnswer.Refused>(Shop.Answer(new WalletRequest.BuyASet(Set("master")), wallet, Evening)).Why);
    }

    [Fact]
    public void A_booster_bought_in_the_tester_mode_is_received_and_costs_nothing()
    {
        WalletAnswer.Received bought = Received(Shop.Answer(new WalletRequest.BuyABooster(BoosterKind.StartColorBomb), WalletWith(1500), Evening, WalletHandling.Unlimited));

        Assert.Equal(new Coins(1500), bought.Wallet.Coins);
        Assert.Equal(1, bought.Wallet.Shelf.Count(BoosterKind.StartColorBomb));
    }

    [Fact]
    public void In_the_tester_mode_the_ads_and_the_gift_keep_their_rules()
    {
        Wallet wallet = WalletWith(100);

        Wallet firstAd = Received(Shop.Answer(new WalletRequest.CoinsForAnAd(), wallet, Evening, WalletHandling.Unlimited)).Wallet;
        Wallet secondAd = Received(Shop.Answer(new WalletRequest.CoinsForAnAd(), firstAd, Evening, WalletHandling.Unlimited)).Wallet;
        Assert.Equal(new Coins(200), secondAd.Coins);
        Assert.IsType<WalletRefusal.NoAdLeftToday>(
            Assert.IsType<WalletAnswer.Refused>(Shop.Answer(new WalletRequest.CoinsForAnAd(), secondAd, Evening, WalletHandling.Unlimited)).Why);
        Wallet gifted = Received(Shop.Answer(new WalletRequest.TakeTheDailyGift(), wallet, Evening, WalletHandling.Unlimited)).Wallet;
        Assert.IsType<WalletRefusal.GiftNotDueYet>(
            Assert.IsType<WalletAnswer.Refused>(Shop.Answer(new WalletRequest.TakeTheDailyGift(), gifted, Evening, WalletHandling.Unlimited)).Why);
    }

    [Fact]
    public void In_the_tester_mode_a_loss_for_good_and_leaving_after_a_move_take_no_life()
    {
        PlayerProgress before = OnLevelForty(OneLifeMissing(100));

        PlayerProgress lost = Attempts.Apply(before, new AttemptEvent.LostForGood(LossReason.GaveUp), Evening, WalletHandling.Unlimited);
        PlayerProgress left = Attempts.Apply(before, new AttemptEvent.Left(afterAMove: true), Evening, WalletHandling.Unlimited);

        Assert.Equal(before, lost);
        Assert.Equal(before, left);
        Assert.NotEqual(before.Wallet.Lives, Attempts.Apply(before, new AttemptEvent.LostForGood(LossReason.GaveUp), Evening).Wallet.Lives);
    }

    [Fact]
    public void In_the_tester_mode_the_extra_moves_for_coins_leave_the_coins_and_those_for_an_ad_count_the_ad()
    {
        PlayerProgress before = OnLevelForty(WalletWith(100));

        PlayerProgress byCoins = Attempts.Apply(before, new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByCoins), Evening, WalletHandling.Unlimited);
        PlayerProgress byAnAd = Attempts.Apply(before, new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByAd), Evening, WalletHandling.Unlimited);

        Assert.Equal(before.Wallet, byCoins.Wallet);
        Assert.Equal(2, TheRules.ExtraMoves.TermsFor(byAnAd.Wallet, Today).FreeAdsLeftToday);
        Assert.Throws<InvalidOperationException>(() => Attempts.Apply(before, new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByCoins), Evening));
    }

    [Fact]
    public void In_the_tester_mode_a_win_pays_its_coins_and_the_first_move_spends_the_start_boosters_as_ever()
    {
        PlayerProgress before = OnLevelForty(TheRules.StartingWallet);
        StartChoice rocket = Assert.IsType<StartToggle.Changed>(
            TheRules.StartBoosters.Toggled(StartChoice.None, BoosterKind.StartRocketAndBomb, before.Wallet.Shelf)).Choice;

        foreach (AttemptEvent happened in new AttemptEvent[] { new AttemptEvent.FirstMove(rocket), new AttemptEvent.Won(movesLeft: 3) })
        {
            Assert.Equal(Attempts.Apply(before, happened, Evening), Attempts.Apply(before, happened, Evening, WalletHandling.Unlimited));
        }
    }

    [Fact]
    public void In_the_tester_mode_the_offer_and_the_shop_show_every_price_as_covered()
    {
        Wallet poor = WalletWith(120);

        ExtraMovesTerms terms = TheRules.ExtraMoves.TermsFor(WalletHandling.Unlimited.ToPay(TheRules.ExtraMoves.Price, poor), Today);
        ShopView shop = ShopView.Of(TheRules, poor, Evening, WalletHandling.Unlimited);

        Assert.True(terms.CoinsCoverIt);
        Assert.Equal(3, terms.FreeAdsLeftToday);
        Assert.All(shop.Sets, set => Assert.True(set.IsCovered));
        Assert.All(ShopView.Of(TheRules, poor, Evening).Sets, set => Assert.False(set.IsCovered));
        Assert.Same(poor, WalletHandling.Normal.ToPay(TheRules.ExtraMoves.Price, poor));
    }

    [Fact]
    public void An_unlimited_wallet_view_opens_a_level_on_an_empty_meter_pays_any_price_and_says_infinity()
    {
        Wallet empty = WalletWith(0).WithLives(new LifeMeter(5, Evening.AddMinutes(10)));

        WalletView normal = WalletView.Of(empty, TheRules.Lives, Evening);
        WalletView unlimited = WalletView.Of(empty, TheRules.Lives, Evening, WalletHandling.Unlimited);

        Assert.False(normal.HasALife);
        Assert.False(normal.CanPay(new Coins(1)));
        Assert.True(unlimited.HasALife);
        Assert.True(unlimited.CanPay(new Coins(1_000_000)));
        Assert.Equal((0, Coins.None), (unlimited.Lives, unlimited.Coins));
        Assert.NotEqual(normal, unlimited);
        Assert.Equal(WalletHandling.Unlimited, unlimited.WithCoins(new Coins(5)).Handling);
        Assert.Equal(("∞", "∞", "Test", "Sınırsız"), (WalletWords.AmountOnTheRow(unlimited, new Coins(1250)), WalletWords.OnTheHeart(unlimited), WalletWords.LivesPill(unlimited), WalletWords.LivesOfMost(unlimited)));
        Assert.Equal(("1.250", "0"), (WalletWords.AmountOnTheRow(normal, new Coins(1250)), WalletWords.OnTheHeart(normal)));
    }

    [Fact]
    public void The_tester_modes_words_can_be_written_in_the_games_letters()
    {
        string[] words =
        {
            WalletWords.Unlimited, WalletWords.TesterModePill, WalletWords.UnlimitedLives, WalletWords.TesterModeOn, WalletWords.NoLifeIsLost, SettingsWords.TesterModeQuestion,
            SettingsWords.TesterModeTells, SettingsWords.TurnTesterModeOn, SettingsWords.LeaveTesterModeOff, SettingsWords.NameOf(DeviceSetting.TesterMode),
        };

        Assert.All(words, said => Assert.Equal(string.Empty, LetterSet.LettersOutside(said)));
    }

    [Fact]
    public void The_settings_card_draws_the_tester_row_under_the_others_only_while_the_mode_holds_and_the_support_card_asks_only_while_it_is_off()
    {
        DeviceView off = PhoneWith(DeviceSettings.Default, AdSource.Simulated);
        DeviceView on = PhoneWith(TurnedOn, AdSource.Simulated);
        DeviceView onInAnotherBuild = PhoneWith(TurnedOn, (AdSource)1);

        Assert.Equal(new[] { DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Vibration, DeviceSetting.Statistics }, off.SettingRows);
        Assert.Equal(new[] { DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Vibration, DeviceSetting.Statistics, DeviceSetting.TesterMode }, on.SettingRows);
        Assert.Equal(new[] { DeviceSetting.Sound, DeviceSetting.Music, DeviceSetting.Vibration, DeviceSetting.Statistics }, onInAnotherBuild.SettingRows);
        Assert.Equal((true, false, false), (off.CanTurnOnTheTesterMode, on.CanTurnOnTheTesterMode, onInAnotherBuild.CanTurnOnTheTesterMode));
        Assert.Equal((false, true, false), (off.IsInTesterMode, on.IsInTesterMode, onInAnotherBuild.IsInTesterMode));
    }

    [Fact]
    public void The_device_file_keeps_the_tester_mode_since_layout_3_and_an_older_file_reads_with_it_off_whatever_it_holds()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        const string Id = "6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a";

        string written = JsonSerializer.Serialize(DeviceDocument.Of(new DeviceId(Id), TurnedOn), options);
        DeviceSettings readBack = Assert.IsType<DeviceReading.Sound>(DeviceDocument.Read(JsonSerializer.Deserialize<DeviceDocument>(written, options))).Settings;
        string older = "{\"version\":2,\"id\":\"" + Id + "\",\"settings\":{\"sound\":true,\"vibration\":false,\"testerMode\":true}}";
        DeviceSettings readOlder = Assert.IsType<DeviceReading.Sound>(DeviceDocument.Read(JsonSerializer.Deserialize<DeviceDocument>(older, options))).Settings;

        Assert.Equal("{\"version\":6,\"id\":\"" + Id + "\",\"settings\":{\"sound\":true,\"vibration\":true,\"testerMode\":true,\"language\":\"\",\"music\":true,\"statistics\":\"\"}}", written);
        Assert.Equal(TurnedOn, readBack);
        Assert.Equal(new DeviceSettings(sound: true, vibration: false, testerMode: false), readOlder);
    }

    [Fact]
    public void An_attempt_played_in_the_tester_mode_says_so_in_its_line_and_both_writers_write_it_alike()
    {
        AttemptRecord played = TestRecords.Of(GameTests.OpenLevel(), AttemptResult.Abandoned, 3);
        AttemptRecord inTheMode = new(played.Start, played.Outcome, lab: null, played.Actions, testerMode: true);

        string marked = AttemptLine.Of(inTheMode);
        string unmarked = AttemptLine.Of(played);

        Assert.Equal(AttemptRecordJson.Serialize(inTheMode), marked);
        Assert.Equal(AttemptRecordJson.Serialize(played), unmarked);
        Assert.Contains(",\"testerMode\":true", marked);
        Assert.DoesNotContain("testerMode", unmarked);
        Assert.True(AttemptRecordJson.Parse(marked).TesterMode);
        Assert.False(AttemptRecordJson.Parse(unmarked).TesterMode);
    }

    [Fact]
    public void Attempts_of_the_tester_mode_are_left_out_of_the_comparison_and_the_summary_and_counted_apart()
    {
        LevelDefinition level = GameTests.OpenLevel();
        AttemptRecord comparable = TestRecords.Of(level, AttemptResult.Won, 20);
        AttemptRecord boosted = TestRecords.Of(level, AttemptResult.Won, 20);
        AttemptRecord inTheMode = new(boosted.Start, boosted.Outcome, lab: null, actions: null, testerMode: true);
        AttemptRecord[] records = { comparable, inTheMode, inTheMode };

        HumanBotReport report = HumanBotComparison.Compare(records, new[] { level }, botRuns: 1);
        AttemptSummary summary = AttemptSummary.Of(records);

        Assert.Equal(AttemptStanding.TesterMode, AttemptCounting.Standing(inTheMode));
        Assert.Equal(new RecordTally(Compared: 1, Assisted: 0, ChosenSeeds: 0, UnknownLevel: 0, TesterMode: 2), report.Records);
        Assert.Equal((1, 2), (summary.Overall.Finished, summary.TesterMode));
        Assert.Equal(2, Assert.Single(summary.Levels).TesterMode);
    }

    [Fact]
    public void In_the_tester_mode_the_cards_that_would_cost_a_life_say_none_goes_and_draw_no_pill()
    {
        GoalFigure[] open = Array.Empty<GoalFigure>();
        var lost = new LevelResult.Lost(levelNumber: 40, LossReason.GaveUp, attemptNumber: 2, open);
        var leavingAfterAMove = new LevelResult.Leaving(levelNumber: 40, attemptNumber: 2, hasPlayed: true, open);
        var leavingUntouched = new LevelResult.Leaving(levelNumber: 40, attemptNumber: 2, hasPlayed: false, open);
        var offer = new LevelResult.OutOfMoves(levelNumber: 40, attemptNumber: 2, open, isClose: true);

        Assert.Equal(("Test modu · can gitmez", false), (ResultWords.Lives(lost, WalletHandling.Unlimited), ResultWords.CostsALife(lost, WalletHandling.Unlimited)));
        Assert.Equal(("Test modu · can gitmez", false), (ResultWords.Lives(leavingAfterAMove, WalletHandling.Unlimited), ResultWords.CostsALife(leavingAfterAMove, WalletHandling.Unlimited)));
        Assert.Equal("Henüz hamle yapmadın, can gitmez", ResultWords.Lives(leavingUntouched, WalletHandling.Unlimited));
        Assert.Null(ResultWords.GiveUpCosts(offer, WalletHandling.Unlimited));
        Assert.Equal(string.Empty, LetterSet.LettersOutside(ResultWords.NoLifeInTheTesterMode));
    }

    [Fact]
    public void As_the_rules_have_it_the_cards_say_what_they_said_before_the_tester_mode()
    {
        GoalFigure[] open = Array.Empty<GoalFigure>();
        LevelResult[] results =
        {
            new LevelResult.Lost(levelNumber: 40, LossReason.OutOfMoves, attemptNumber: 2, open),
            new LevelResult.Leaving(levelNumber: 40, attemptNumber: 2, hasPlayed: true, open),
            new LevelResult.Leaving(levelNumber: 40, attemptNumber: 2, hasPlayed: false, open),
            new LevelResult.OutOfMoves(levelNumber: 40, attemptNumber: 2, open, isClose: false),
        };

        Assert.All(results, result => Assert.Equal(
            (ResultWords.Lives(result), ResultWords.CostsALife(result)),
            (ResultWords.Lives(result, WalletHandling.Normal), ResultWords.CostsALife(result, WalletHandling.Normal))));
        Assert.Equal("1 can gider", ResultWords.GiveUpCosts(new LevelResult.OutOfMoves(levelNumber: 40, attemptNumber: 2, open, isClose: false), WalletHandling.Normal));
    }

    private static DeviceView PhoneWith(DeviceSettings settings, AdSource ads) => new(
        settings, DeviceAbilities.OfThisBuild.Settings, new DeviceId("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a"), "1.0", ContentVersion.First,
        recordsKept: 0, recordsFile: null, ads, RealMoneySales.NotAvailable, Language.Turkish, new[] { new LanguageOffer(Language.Turkish, "Türkçe") });
}
