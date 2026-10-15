using Puzzle.Client.Flow;
using Puzzle.Client.Screens;
using Puzzle.Meta;
using Puzzle.Museum;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the shop's, the gift frame's and the board's requests and a completed painting do to the player's wallet
/// (<c>WalletLedger</c>, the Unity project's source file, compiled into these tests): every purchase through the
/// rules, a refusal that changes nothing and says why, the ads for coins and the daily reward counted by the player's
/// calendar day, and each reward given once.
/// </summary>
public class WalletLedgerTests
{
    private static readonly EconomyRules Rules = MetaSupport.Rules();
    private static readonly WalletLedger Ledger = new(Rules);

    private static BoosterSet Set(string name) => Rules.Shop.BoosterSets.Single(set => set.Name == name);

    private static WalletAnswer.Received Received(WalletAnswer answer) => Assert.IsType<WalletAnswer.Received>(answer);

    private static WalletRefusal Refused(WalletAnswer answer) => Assert.IsType<WalletAnswer.Refused>(answer).Why;

    [Fact]
    public void A_set_is_bought_for_its_price_from_the_settings_and_its_boosters_are_received()
    {
        Wallet wallet = WalletWith(7000);

        WalletAnswer.Received bought = Received(Ledger.Answer(new WalletRequest.BuyASet(Set("master")), wallet, Evening));

        Assert.Equal(new Coins(700), bought.Wallet.Coins);
        Assert.Equal(12, bought.Wallet.Shelf.Total);
        Assert.Equal(3, bought.Wallet.Shelf.Count(BoosterKind.Shuffle));
        Assert.Equal(12, bought.What.Boosters.Count);
        Assert.Equal(Coins.None, bought.What.Coins);
    }

    [Fact]
    public void A_set_the_coins_do_not_cover_is_refused_with_what_is_missing_and_nothing_changes()
    {
        Wallet wallet = WalletWith(2000);

        WalletRefusal why = Refused(Ledger.Answer(new WalletRequest.BuyASet(Set("starter")), wallet, Evening));

        Assert.Equal(new Coins(100), Assert.IsType<WalletRefusal.CoinsShort>(why).Missing);
        Assert.Equal(WalletWith(2000), wallet);
    }

    [Fact]
    public void A_single_booster_is_bought_through_the_same_rules()
    {
        WalletAnswer.Received bought = Received(Ledger.Answer(new WalletRequest.BuyABooster(BoosterKind.HitRow), WalletWith(1000), Evening));

        Assert.Equal(new Coins(300), bought.Wallet.Coins);
        Assert.Equal([BoosterKind.HitRow], bought.What.Boosters);
        Assert.IsType<WalletRefusal.CoinsShort>(Refused(Ledger.Answer(new WalletRequest.BuyABooster(BoosterKind.HitRow), WalletWith(699), Evening)));
    }

    [Fact]
    public void A_pack_of_coins_is_not_on_sale_in_this_build_and_one_the_shop_does_not_show_is_no_request()
    {
        CoinPack shown = Rules.Shop.CoinPacks[0];

        Assert.IsType<WalletRefusal.NotOnSale>(Refused(Ledger.Answer(new WalletRequest.BuyACoinPack(shown), WalletWith(0), Evening)));
        Assert.Throws<ArgumentException>(() => Ledger.Answer(new WalletRequest.BuyACoinPack(new CoinPack("huge", new Coins(99_999))), WalletWith(0), Evening));
        Assert.Throws<ArgumentException>(() => Ledger.Answer(new WalletRequest.BuyASet(new BoosterSet("free", Coins.None, BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle))), WalletWith(0), Evening));
    }

    [Fact]
    public void Ads_give_their_coins_as_often_a_day_as_the_settings_say_and_a_new_day_opens_them_again()
    {
        var watch = new WalletRequest.CoinsForAnAd();
        Wallet first = Received(Ledger.Answer(watch, WalletWith(0), Evening)).Wallet;
        Wallet second = Received(Ledger.Answer(watch, first, Evening.AddMinutes(1))).Wallet;

        // The tests' settings: 50 coins for each of two ads a day.
        Assert.Equal(new Coins(100), second.Coins);
        Assert.IsType<WalletRefusal.NoAdLeftToday>(Refused(Ledger.Answer(watch, second, Evening.AddMinutes(2))));

        // The phone's clock set back opens no day twice; the next calendar day opens the ads again.
        Assert.IsType<WalletRefusal.NoAdLeftToday>(Refused(Ledger.Answer(watch, second, Evening.AddDays(-1))));
        WalletAnswer.Received tomorrow = Received(Ledger.Answer(watch, second, Evening.AddHours(4)));
        Assert.Equal(new Coins(150), tomorrow.Wallet.Coins);
        Assert.Equal(new Reward(new Coins(50), []), tomorrow.What);
    }

    [Fact]
    public void The_daily_gift_is_given_once_a_calendar_day_by_the_rotation_and_a_clock_set_back_opens_none()
    {
        var take = new WalletRequest.TakeTheDailyGift();
        WalletAnswer.Received first = Received(Ledger.Answer(take, WalletWith(0), Evening));

        Assert.Equal([BoosterKind.StartRocketAndBomb], first.What.Boosters);
        Assert.Equal(Today.Plus(1), Assert.IsType<WalletRefusal.GiftNotDueYet>(Refused(Ledger.Answer(take, first.Wallet, Evening.AddMinutes(30)))).DueOn);
        Assert.Equal(Today.Plus(1), Assert.IsType<WalletRefusal.GiftNotDueYet>(Refused(Ledger.Answer(take, first.Wallet, Evening.AddDays(-30)))).DueOn);

        WalletAnswer.Received second = Received(Ledger.Answer(take, first.Wallet, Evening.AddHours(3)));
        Assert.Equal([BoosterKind.StartColorBomb], second.What.Boosters);
        Assert.Equal(2, second.Wallet.Rewards.Daily);
    }

    [Fact]
    public void The_piece_that_completes_a_painting_gives_its_reward_once_with_the_restoration()
    {
        var painting = new PaintingNumber(1);
        var progress = new PlayerProgress(levelsWon: painting.LastLevel.Value, attemptsOnLevel: 0).WithWallet(WalletWith(100));
        int[] pieces = progress.Museum.WaitingIn(painting).ToArray();

        Assert.Equal(new Reward(new Coins(80), [BoosterKind.StartRocketAndBomb]), Ledger.RewardForCompleting(progress, painting));

        // All but the last piece: no reward yet.
        PlayerProgress almost = Ledger.WithPiecesInPlace(progress, painting, pieces.Take(pieces.Length - 1));
        Assert.Equal(progress.Wallet, almost.Wallet);

        // The last piece: the painting is whole and its reward is in the same progress, counted.
        PlayerProgress whole = Ledger.WithPiecesInPlace(almost, painting, pieces);
        Assert.True(whole.Museum.IsComplete(painting));
        Assert.Equal(new Coins(180), whole.Wallet.Coins);
        Assert.Equal(1, whole.Wallet.Shelf.Count(BoosterKind.StartRocketAndBomb));
        Assert.Equal(1, whole.Wallet.Rewards.ForPaintings);

        // Kept again, or the painting opened again once whole: nothing more is given, and nothing is shown as to come.
        Assert.Equal(whole, Ledger.WithPiecesInPlace(whole, painting, pieces));
        Assert.Equal(whole, Ledger.WithPiecesInPlace(whole, painting, []));
        Assert.Null(Ledger.RewardForCompleting(whole, painting));
    }

    [Fact]
    public void The_next_painting_completed_gives_the_next_booster_of_the_rotation()
    {
        var first = new PaintingNumber(1);
        var second = new PaintingNumber(2);
        var progress = new PlayerProgress(levelsWon: second.LastLevel.Value, attemptsOnLevel: 0);

        PlayerProgress oneWhole = Ledger.WithPiecesInPlace(progress, first, progress.Museum.WaitingIn(first));
        Assert.Equal(new Reward(new Coins(80), [BoosterKind.StartColorBomb]), Ledger.RewardForCompleting(oneWhole, second));

        PlayerProgress bothWhole = Ledger.WithPiecesInPlace(oneWhole, second, oneWhole.Museum.WaitingIn(second));
        Assert.Equal(new Coins(160), bothWhole.Wallet.Coins);
        Assert.Equal(2, bothWhole.Wallet.Rewards.ForPaintings);
        Assert.Equal(1, bothWhole.Wallet.Shelf.Count(BoosterKind.StartColorBomb));
    }
}
