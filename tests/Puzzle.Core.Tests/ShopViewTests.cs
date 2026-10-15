using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the shop and the hall's gift frame show of a wallet at an instant (<c>ShopView</c> and <c>ShopWords</c>, the
/// Unity project's source files, compiled into these tests): every number from the rules, a set dimmed by what the
/// coins lack, the ads for coins counted by the player's calendar day, and the daily reward due or waited for.
/// </summary>
public class ShopViewTests
{
    private static readonly EconomyRules Rules = MetaSupport.Rules();

    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static ShopView ViewOf(Wallet wallet, DateTimeOffset now) => ShopView.Of(Rules, wallet, now);

    [Fact]
    public void A_rich_player_is_offered_every_set_and_every_ad_of_the_day_and_sees_the_packs_not_on_sale()
    {
        ShopView view = ViewOf(WalletWith(10_000), Evening);

        Assert.All(view.Sets, offer => Assert.True(offer.IsCovered));
        Assert.Equal(["starter", "master"], view.Sets.Select(offer => offer.Set.Name));
        Assert.Equal([new Coins(500), new Coins(2500)], view.CoinPacks.Select(pack => pack.Coins));
        Assert.Equal(RealMoneySales.NotAvailable, view.RealMoney);
        Assert.Equal((new Coins(50), 2, 2, true), (view.CoinsForAnAd, view.AdsLeftToday, view.AdsPerDay, view.CanWatchAnAd));
        Assert.Equal(AdSource.Simulated, view.Ads);

        // The master set's row: three of each booster used during play, in the engine's order.
        Assert.Equal(
            [(BoosterKind.HitCell, 3), (BoosterKind.HitRow, 3), (BoosterKind.HitColumn, 3), (BoosterKind.Shuffle, 3)],
            view.Sets[1].Boosters);
    }

    [Fact]
    public void A_poor_player_sees_by_how_much_the_coins_fall_short_of_each_set()
    {
        ShopView view = ViewOf(WalletWith(2500), Evening);

        Assert.True(view.Sets[0].IsCovered);
        Assert.Equal(Coins.None, view.Sets[0].Missing);
        Assert.False(view.Sets[1].IsCovered);
        Assert.Equal(new Coins(3800), view.Sets[1].Missing);
        Assert.Equal("Usta seti için 3.800 altın daha gerek.", ShopWords.CoinsShortFor(view.Sets[1].Set, view.Sets[1].Missing));
    }

    [Fact]
    public void Once_the_days_ads_are_watched_the_free_strip_says_tomorrow_and_a_new_day_opens_them_again()
    {
        Wallet watchedBoth = WalletWith(0).WithAds(new AdsWatched(Today, forExtraMoves: 0, forCoins: 2));

        ShopView capped = ViewOf(watchedBoth, Evening);
        ShopView nextMorning = ViewOf(watchedBoth, Evening.AddHours(10));
        ShopView clockSetBack = ViewOf(watchedBoth, Evening.AddDays(-3));

        Assert.Equal((0, false), (capped.AdsLeftToday, capped.CanWatchAnAd));
        Assert.Equal("ÜCRETSİZ · BUGÜNLÜK BİTTİ", ShopWords.FreeToday(capped.AdsLeftToday));
        Assert.Equal((2, true), (nextMorning.AdsLeftToday, nextMorning.CanWatchAnAd));
        Assert.Equal("ÜCRETSİZ · BUGÜN 2 HAK", ShopWords.FreeToday(nextMorning.AdsLeftToday));
        Assert.Equal(0, clockSetBack.AdsLeftToday);
    }

    [Fact]
    public void The_gift_waits_for_a_player_who_has_not_taken_todays_and_counts_down_to_midnight_once_it_is_taken()
    {
        Wallet fresh = WalletWith(0);
        Wallet taken = Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(fresh, Today)).Wallet;

        GiftView.Due due = Assert.IsType<GiftView.Due>(ViewOf(fresh, Evening).Gift);
        Assert.Equal(new Reward(new Coins(5), [BoosterKind.StartRocketAndBomb]), due.Next);

        // Taken at nine in the evening, the next is due at the player's midnight: three hours on.
        GiftView.Waiting waiting = Assert.IsType<GiftView.Waiting>(ViewOf(taken, Evening).Gift);
        Assert.Equal(TimeSpan.FromHours(3), waiting.Left);
        Assert.Equal("3 sa", ShopWords.GiftIn(waiting.Left));
        Assert.Equal("3 sa", ShopWords.GiftIn(TimeSpan.FromHours(3)));

        // After midnight the next one waits, and it is the next of the rotation.
        GiftView.Due tomorrow = Assert.IsType<GiftView.Due>(ViewOf(taken, Evening.AddHours(3)).Gift);
        Assert.Equal([BoosterKind.StartColorBomb], tomorrow.Next.Boosters);
    }

    [Fact]
    public void A_clock_set_back_opens_no_gift_and_waits_for_the_day_after_the_latest_one()
    {
        Wallet taken = Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(WalletWith(0), Today)).Wallet;

        GiftView.Waiting waiting = Assert.IsType<GiftView.Waiting>(ViewOf(taken, Evening.AddDays(-2)).Gift);

        Assert.Equal(TimeSpan.FromHours(51), waiting.Left);
        Assert.Equal("51 sa", ShopWords.GiftIn(waiting.Left));
    }

    [Fact]
    public void Every_set_of_the_shipped_settings_has_a_name_and_costs_the_sum_of_its_singles()
    {
        EconomyRules shipped = EconomyFileMapper.ToRules(JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), AsUnityMapsIt)!);

        Assert.Equal(["Başlangıç seti", "Usta seti"], shipped.Shop.BoosterSets.Select(ShopWords.NameOf));
        Assert.All(shipped.Shop.BoosterSets, set => Assert.Equal(
            BoosterKinds.All.Sum(kind => shipped.Shop.PriceOf(kind).Amount * set.Boosters.Count(kind)),
            set.Price.Amount));
        Assert.Equal((new Coins(4), 3), (shipped.Shop.AdForCoins.Coins, shipped.Shop.AdForCoins.PerDay));
        Assert.Equal("Reklam izle, 4 altın kazan", ShopWords.WatchForCoins(shipped.Shop.AdForCoins.Coins));
        Assert.Throws<ArgumentException>(() => ShopWords.NameOf(new BoosterSet("festival", new Coins(1), BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle))));
    }

    [Fact]
    public void A_set_and_a_reward_are_said_by_their_boosters()
    {
        Assert.Equal(("×3", "+80"), (ShopWords.Times(3), ShopWords.More(80)));
        Assert.Equal("Fırça + sıçrama", ShopWords.WhatCame(new Reward(Coins.None, [BoosterKind.StartRocketAndBomb])));
        Assert.Equal("Spatula", ShopWords.WhatCame(new Reward(new Coins(80), [BoosterKind.HitCell])));
        Assert.Equal("12 güçlendirici", ShopWords.WhatCame(new Reward(Coins.None, Enumerable.Repeat(BoosterKind.Shuffle, 12).ToArray())));
        Assert.Equal(("Yarın yeni bir hediye var.", "3 gün sonra yeni bir hediye var."), (ShopWords.NextGiftAfter(1), ShopWords.NextGiftAfter(3)));
        Assert.Equal("Bölüm kartında seçip kullan.", ShopWords.HowToUse(new Reward(Coins.None, [BoosterKind.StartColorBomb, BoosterKind.StartExtraMoves])));
        Assert.Equal("Bölümde raftan seçip kullan.", ShopWords.HowToUse(new Reward(Coins.None, [BoosterKind.Shuffle, BoosterKind.HitRow])));
        Assert.Equal("2 güçlendirici", ShopWords.HowToUse(new Reward(Coins.None, [BoosterKind.Shuffle, BoosterKind.StartColorBomb])));
    }
}
