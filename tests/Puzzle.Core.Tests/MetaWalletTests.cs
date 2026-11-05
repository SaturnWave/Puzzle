using Puzzle.Core.Engine;
using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a player holds in the game: whole coins that are never owed, boosters by kind, and a wallet that never
/// changes, so that a rule which refuses can hand nothing back and still have changed nothing.
/// </summary>
public class MetaWalletTests
{
    [Fact]
    public void Coins_are_whole_and_never_negative()
    {
        var purse = new Coins(250);

        Assert.Equal(new Coins(1150), purse + new Coins(900));
        Assert.Equal(new Coins(1000), purse * 4);
        Assert.Equal(new Coins(50), purse.Less(new Coins(200)));
        Assert.True(purse.Covers(new Coins(250)));
        Assert.False(purse.Covers(new Coins(251)));
        Assert.Equal(new Coins(650), purse.ShortOf(new Coins(900)));
        Assert.Equal(Coins.None, purse.ShortOf(new Coins(100)));
        Assert.True(Coins.None.IsNone);
        Assert.Equal("250", purse.ToString());

        // Nobody owes coins: an amount below zero cannot be made, by any way.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Coins(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.Less(new Coins(251)));
        Assert.Throws<ArgumentOutOfRangeException>(() => purse * -1);
        Assert.Throws<OverflowException>(() => new Coins(long.MaxValue) + new Coins(1));
    }

    [Fact]
    public void Every_booster_of_the_engine_has_a_kind_that_goes_by_the_engines_name()
    {
        string[] startBoosters = Enum.GetValues<StartBooster>().Select(BoosterNames.Of).ToArray();
        BoosterUse[] uses = typeof(BoosterUse).GetNestedTypes()
            .Where(type => type.IsSubclassOf(typeof(BoosterUse)))
            .Select(type => (BoosterUse)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type))
            .ToArray();

        Assert.Equal(7, BoosterKinds.All.Count);
        Assert.Equal(startBoosters, BoosterKinds.All.Where(BoosterKinds.IsStart).Select(BoosterKinds.Name));
        Assert.Equal(uses.Select(BoosterNames.Of).Order(), BoosterKinds.All.Where(BoosterKinds.IsInLevel).Select(BoosterKinds.Name).Order());

        // Both ways between the engine's boosters and the kinds a player holds, and back from a name in a file.
        Assert.All(Enum.GetValues<StartBooster>(), booster => Assert.Equal(booster, BoosterKinds.Of(booster).AsStartBooster()));
        Assert.All(uses, use => Assert.Equal(BoosterNames.Of(use), BoosterKinds.Of(use).Name()));
        Assert.All(BoosterKinds.All, kind => Assert.Equal(kind, BoosterKinds.Find(kind.Name())));
        Assert.Null(BoosterKinds.Find("start-nothing"));
        Assert.Throws<ArgumentException>(() => BoosterKind.Shuffle.AsStartBooster());
    }

    [Fact]
    public void A_shelf_counts_boosters_by_kind_and_never_changes()
    {
        BoosterShelf shelf = BoosterShelf.Empty.WithOneMore(BoosterKind.HitRow).With(BoosterKind.Shuffle, more: 3);

        Assert.Equal(1, shelf.Count(BoosterKind.HitRow));
        Assert.Equal(3, shelf.Count(BoosterKind.Shuffle));
        Assert.Equal(4, shelf.Total);
        Assert.True(shelf.Has(BoosterKind.Shuffle));
        Assert.False(shelf.Has(BoosterKind.StartColorBomb));

        // Taking one gives another shelf; the one that was read stays what it was.
        BoosterShelf afterOneUsed = shelf.WithOneFewer(BoosterKind.HitRow);
        Assert.False(afterOneUsed.Has(BoosterKind.HitRow));
        Assert.True(shelf.Has(BoosterKind.HitRow));
        Assert.True(BoosterShelf.Empty.IsEmpty);

        Assert.Throws<InvalidOperationException>(() => afterOneUsed.WithOneFewer(BoosterKind.HitRow));
        Assert.Throws<ArgumentOutOfRangeException>(() => shelf.With(BoosterKind.Shuffle, more: -1));

        // Two shelves that hold the same are equal, however they came to hold it.
        Assert.Equal(shelf, BoosterShelf.Empty.With(BoosterKind.Shuffle, 2).Plus(BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle).WithOneMore(BoosterKind.HitRow)));
        Assert.NotEqual(shelf, afterOneUsed);
        Assert.Equal("1 hit-row, 3 shuffle", shelf.ToString());
    }

    [Fact]
    public void A_wallet_is_equal_by_what_it_holds_and_every_step_gives_a_new_one()
    {
        Wallet wallet = Wallet.Empty;

        Wallet richer = wallet.WithCoins(new Coins(300)).WithShelf(BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle));

        // The wallet that was read is untouched.
        Assert.Equal(Coins.None, wallet.Coins);
        Assert.True(wallet.Shelf.IsEmpty);
        Assert.True(wallet.Lives.IsFull);
        Assert.Equal(AdsWatched.None, wallet.Ads);
        Assert.Equal(RewardsGiven.None, wallet.Rewards);

        // Equal by value, part by part.
        var same = new Wallet(new Coins(300), LifeMeter.Full, BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle), AdsWatched.None, RewardsGiven.None);
        Assert.Equal(same, richer);
        Assert.Equal(same.GetHashCode(), richer.GetHashCode());
        Assert.NotEqual(richer, richer.WithCoins(new Coins(301)));
        Assert.NotEqual(richer, richer.WithLives(new LifeMeter(1, Evening)));
        Assert.NotEqual(richer, richer.WithShelf(BoosterShelf.Empty));
        Assert.NotEqual(richer, richer.WithAds(AdsWatched.None.WithOneForCoins(Today)));
        Assert.NotEqual(richer, richer.WithRewards(RewardsGiven.None.WithOneForAPainting()));
    }

    [Fact]
    public void A_day_is_the_players_calendar_day_and_not_a_stretch_of_hours()
    {
        var justBeforeMidnight = new DateTimeOffset(2026, 10, 13, 23, 59, 0, TimeSpan.FromHours(3));
        DateTimeOffset justAfter = justBeforeMidnight.AddMinutes(2);

        Assert.Equal(DayStamp.Of(Evening), DayStamp.Of(justBeforeMidnight));
        Assert.Equal(DayStamp.Of(justBeforeMidnight).Plus(1), DayStamp.Of(justAfter));
        Assert.True(DayStamp.Of(justAfter) > DayStamp.Of(justBeforeMidnight));
        Assert.Equal("2026-10-13", DayStamp.Of(justBeforeMidnight).ToString());
        Assert.Equal("2026-10-14", DayStamp.Of(justAfter).ToString());

        // The day is read off the player's own clock: the same instant is another day three zones further west.
        Assert.Equal("2026-10-13", DayStamp.Of(justAfter.ToOffset(TimeSpan.Zero)).ToString());

        Assert.Equal(DayStamp.Of(Evening), new DayStamp(DayStamp.Of(Evening).Number));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DayStamp(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Today.Plus(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Today.Plus(int.MaxValue));
    }

    [Fact]
    public void The_ads_of_a_day_start_from_none_on_a_later_day_and_never_on_an_earlier_one()
    {
        AdsWatched today = AdsWatched.None.WithOneForExtraMoves(Today).WithOneForExtraMoves(Today).WithOneForCoins(Today);

        Assert.Equal(new AdsWatched(Today, forExtraMoves: 2, forCoins: 1), today);
        Assert.Equal(today, today.On(Today));

        // A later day opens a new count.
        DayStamp tomorrow = Today.Plus(1);
        Assert.Equal(new AdsWatched(tomorrow, 0, 0), today.On(tomorrow));
        Assert.Equal(new AdsWatched(tomorrow, 0, 1), today.WithOneForCoins(tomorrow));

        // The phone's date set back: yesterday comes after a day that is already counted, and opens nothing. An ad
        // watched then counts on the day the wallet knows.
        DayStamp yesterday = new(Today.Number - 1);
        Assert.Equal(today, today.On(yesterday));
        Assert.Equal(new AdsWatched(Today, forExtraMoves: 3, forCoins: 1), today.WithOneForExtraMoves(yesterday));

        Assert.Throws<ArgumentOutOfRangeException>(() => new AdsWatched(Today, forExtraMoves: -1, forCoins: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdsWatched(Today, forExtraMoves: 0, forCoins: -1));
    }

    [Fact]
    public void Rewards_are_counted_and_the_daily_one_with_its_day()
    {
        RewardsGiven rewards = RewardsGiven.None.WithOneForAPainting().WithTheDailyOf(Today).WithOneForAPainting();

        Assert.Equal(new RewardsGiven(forPaintings: 2, daily: 1, lastDailyOn: Today), rewards);
        Assert.Null(RewardsGiven.None.LastDailyOn);
        Assert.Equal(Today.Plus(3), rewards.WithTheDailyOf(Today.Plus(3)).LastDailyOn);

        // A day without a reward, or a reward without its day, was not written by the game.
        Assert.Throws<ArgumentException>(() => new RewardsGiven(forPaintings: 0, daily: 0, lastDailyOn: Today));
        Assert.Throws<ArgumentException>(() => new RewardsGiven(forPaintings: 0, daily: 1, lastDailyOn: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RewardsGiven(forPaintings: -1, daily: 0, lastDailyOn: null));
    }
}
