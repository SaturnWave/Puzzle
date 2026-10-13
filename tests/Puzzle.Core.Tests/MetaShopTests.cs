using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The shop: one booster or a set of them for coins, coins for an ad a few times a day, and packs of coins that this
/// build shows and does not sell. A purchase that is refused changes nothing.
/// </summary>
public class MetaShopTests
{
    private static readonly EconomyRules Rules = MetaSupport.Rules();

    private static Shop Shop => Rules.Shop;

    private static BoosterSet Set(string name) => Shop.BoosterSets.Single(set => set.Name == name);

    [Fact]
    public void The_shop_lists_its_goods_in_the_order_of_the_settings()
    {
        Assert.Equal(["starter", "master"], Shop.BoosterSets.Select(set => set.Name));
        Assert.Equal(["small", "large"], Shop.CoinPacks.Select(pack => pack.Name));
        Assert.Equal(new Coins(2500), Shop.CoinPacks[1].Coins);
        Assert.Equal(new Coins(2100), Set("starter").Price);
        Assert.Equal(3, Set("starter").Boosters.Total);
        Assert.Equal(12, Set("master").Boosters.Total);
        Assert.Equal(new Coins(700), Shop.PriceOf(BoosterKind.HitRow));
        Assert.All(BoosterKinds.All, kind => Assert.True(Shop.PriceOf(kind) > Coins.None));
    }

    [Fact]
    public void One_booster_is_bought_for_its_single_price()
    {
        Wallet wallet = WalletWith(1000);

        Purchase purchase = Shop.BuyOne(BoosterKind.HitRow, wallet);

        Wallet paid = Assert.IsType<Purchase.Bought>(purchase).Wallet;
        Assert.Equal(new Coins(300), paid.Coins);
        Assert.Equal(BoosterShelf.Empty.WithOneMore(BoosterKind.HitRow), paid.Shelf);

        // The rest of it buys a shuffle exactly, and then nothing more.
        Wallet broke = Assert.IsType<Purchase.Bought>(Shop.BuyOne(BoosterKind.Shuffle, paid)).Wallet;
        Assert.Equal(Coins.None, broke.Coins);
        Assert.Equal(new Purchase.NotEnoughCoins(new Coins(300)), Shop.BuyOne(BoosterKind.Shuffle, broke));
    }

    [Fact]
    public void A_set_is_bought_for_its_price_and_puts_all_its_boosters_on_the_shelf()
    {
        Wallet wallet = WalletWith(7000).WithShelf(BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle));

        Wallet paid = Assert.IsType<Purchase.Bought>(Shop.Buy(Set("master"), wallet)).Wallet;

        Assert.Equal(new Coins(700), paid.Coins);
        Assert.Equal(4, paid.Shelf.Count(BoosterKind.Shuffle));
        Assert.Equal(3, paid.Shelf.Count(BoosterKind.HitCell));
        Assert.Equal(3, paid.Shelf.Count(BoosterKind.HitRow));
        Assert.Equal(3, paid.Shelf.Count(BoosterKind.HitColumn));
        Assert.Equal(13, paid.Shelf.Total);

        // Nothing but the coins and the shelf moved.
        Assert.Equal(wallet.WithCoins(paid.Coins).WithShelf(paid.Shelf), paid);
    }

    [Fact]
    public void A_purchase_the_coins_do_not_cover_is_refused_and_changes_nothing()
    {
        Wallet wallet = WalletWith(2099).WithShelf(BoosterShelf.Empty.WithOneMore(BoosterKind.HitCell));
        Wallet asItWas = WalletWith(2099).WithShelf(BoosterShelf.Empty.WithOneMore(BoosterKind.HitCell));

        Purchase set = Shop.Buy(Set("starter"), wallet);
        Purchase bigSet = Shop.Buy(Set("master"), wallet);

        // A refusal says what is missing and carries no wallet at all: there is nothing it could have changed.
        Assert.Equal(new Purchase.NotEnoughCoins(new Coins(1)), set);
        Assert.Equal(new Purchase.NotEnoughCoins(new Coins(4201)), bigSet);
        Assert.Equal(asItWas, wallet);

        // One coin more and the same set is sold.
        Assert.IsType<Purchase.Bought>(Shop.Buy(Set("starter"), WalletWith(2100)));
    }

    [Fact]
    public void A_set_that_is_not_the_shops_is_not_sold()
    {
        var invented = new BoosterSet("starter", price: Coins.None, Set("master").Boosters);

        // A price comes from the settings: a set a caller makes up is no good, whatever it is called.
        Assert.Throws<ArgumentException>(() => Shop.Buy(invented, WalletWith(9999)));
        Assert.Throws<ArgumentException>(() => new BoosterSet("empty", new Coins(100), BoosterShelf.Empty));
    }

    [Fact]
    public void A_coin_pack_is_shown_and_is_not_on_sale_in_this_build()
    {
        Assert.Equal(RealMoneySales.NotAvailable, Shop.RealMoney);
        Assert.Equal(AdSource.Simulated, Rules.Ads);

        Assert.All(Shop.CoinPacks, pack => Assert.Equal(new Purchase.NotOnSale(), Shop.Buy(pack)));
    }

    [Fact]
    public void An_ad_gives_coins_as_often_a_day_as_the_settings_say()
    {
        Wallet wallet = WalletWith(10);

        // The tests' settings: 50 coins, twice a day.
        Assert.Equal(2, Shop.AdsForCoinsLeft(wallet, Today));
        Wallet afterOne = Assert.IsType<AdOutcome.Rewarded>(Shop.CoinsForAnAd(wallet, Today)).Wallet;
        Wallet afterTwo = Assert.IsType<AdOutcome.Rewarded>(Shop.CoinsForAnAd(afterOne, Today)).Wallet;

        Assert.Equal(new Coins(60), afterOne.Coins);
        Assert.Equal(new Coins(110), afterTwo.Coins);
        Assert.Equal(new AdsWatched(Today, forExtraMoves: 0, forCoins: 2), afterTwo.Ads);
        Assert.Equal(0, Shop.AdsForCoinsLeft(afterTwo, Today));

        // The third of the day is refused and changes nothing; the next calendar day has two again; a date set back has none.
        Assert.Equal(new AdOutcome.NoneLeftToday(), Shop.CoinsForAnAd(afterTwo, Today));
        Assert.Equal(2, Shop.AdsForCoinsLeft(afterTwo, Today.Plus(1)));
        Assert.Equal(0, Shop.AdsForCoinsLeft(afterTwo, new DayStamp(Today.Number - 3)));

        // The ads for extra moves are another allowance: these took none of them.
        Assert.Equal(3, Rules.ExtraMoves.TermsFor(afterTwo, Today).FreeAdsLeftToday);
    }
}
