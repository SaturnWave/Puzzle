using Puzzle.Core.Engine;
using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a player is offered around an attempt: the extra moves when a level runs out of them (for coins, or for an
/// ad a few times a day), and the start boosters they may take in. An offer that is refused changes nothing.
/// </summary>
public class MetaOfferTests
{
    private static readonly EconomyRules Rules = MetaSupport.Rules();

    private static readonly BoosterShelf OneOfEachStartBooster = BoosterShelf.Empty
        .WithOneMore(BoosterKind.StartRocketAndBomb)
        .WithOneMore(BoosterKind.StartColorBomb)
        .WithOneMore(BoosterKind.StartExtraMoves);

    private static Wallet AfterAdsForMoves(Wallet wallet, DayStamp day, int ads)
    {
        for (int ad = 0; ad < ads; ad++)
        {
            wallet = Assert.IsType<AdOutcome.Rewarded>(Rules.ExtraMoves.PaidWithAnAd(wallet, day)).Wallet;
        }

        return wallet;
    }

    private static StartChoice Picked(StartChoice choice, BoosterKind booster, BoosterShelf owned) =>
        Assert.IsType<StartToggle.Changed>(Rules.StartBoosters.Toggled(choice, booster, owned)).Choice;

    [Fact]
    public void The_terms_say_the_price_what_is_missing_for_it_and_the_ads_left_today()
    {
        ExtraMovesTerms poor = Rules.ExtraMoves.TermsFor(WalletWith(250), Today);
        ExtraMovesTerms rich = Rules.ExtraMoves.TermsFor(WalletWith(900), Today);

        Assert.Equal(new Coins(900), poor.Price);
        Assert.Equal(new Coins(650), poor.Missing);
        Assert.False(poor.CoinsCoverIt);
        Assert.Equal(3, poor.FreeAdsLeftToday);
        Assert.True(poor.AnAdPaysToday);

        Assert.True(rich.CoinsCoverIt);
        Assert.Equal(Coins.None, rich.Missing);

        // Reading the terms takes nothing: asked twice, they say the same.
        Assert.Equal(poor, Rules.ExtraMoves.TermsFor(WalletWith(250), Today));
    }

    [Fact]
    public void Extra_moves_paid_with_coins_cost_the_price_and_nothing_else()
    {
        Wallet wallet = WalletWith(1000).WithShelf(OneOfEachStartBooster);

        Purchase purchase = Rules.ExtraMoves.PaidWithCoins(wallet);

        Wallet paid = Assert.IsType<Purchase.Bought>(purchase).Wallet;
        Assert.Equal(wallet.WithCoins(new Coins(100)), paid);
        Assert.Equal(3, Rules.ExtraMoves.TermsFor(paid, Today).FreeAdsLeftToday);
    }

    [Fact]
    public void Extra_moves_the_coins_do_not_cover_are_refused_and_nothing_changes()
    {
        Wallet wallet = WalletWith(899);

        Purchase purchase = Rules.ExtraMoves.PaidWithCoins(wallet);

        // The refusal carries what is missing and no wallet: the player's is the one they had.
        Assert.Equal(new Purchase.NotEnoughCoins(new Coins(1)), purchase);
        Assert.Equal(WalletWith(899), wallet);
        Assert.IsType<Purchase.Bought>(Rules.ExtraMoves.PaidWithCoins(WalletWith(900)));
    }

    [Fact]
    public void An_ad_pays_for_the_extra_moves_as_often_a_day_as_the_settings_say()
    {
        Wallet wallet = WalletWith(250);

        Wallet afterThree = AfterAdsForMoves(wallet, Today, ads: 3);

        // Three ads cost no coin and are counted for their day; the fourth is refused and changes nothing.
        Assert.Equal(wallet.WithAds(new AdsWatched(Today, forExtraMoves: 3, forCoins: 0)), afterThree);
        Assert.Equal(0, Rules.ExtraMoves.TermsFor(afterThree, Today).FreeAdsLeftToday);
        Assert.False(Rules.ExtraMoves.TermsFor(afterThree, Today).AnAdPaysToday);
        Assert.Equal(new AdOutcome.NoneLeftToday(), Rules.ExtraMoves.PaidWithAnAd(afterThree, Today));

        // An ad watched for coins is another allowance: it takes none of these.
        Wallet afterACoinAd = Assert.IsType<AdOutcome.Rewarded>(Rules.Shop.CoinsForAnAd(wallet, Today)).Wallet;
        Assert.Equal(3, Rules.ExtraMoves.TermsFor(afterACoinAd, Today).FreeAdsLeftToday);
    }

    [Fact]
    public void A_new_day_gives_the_free_ads_again_and_a_day_set_back_does_not()
    {
        Wallet usedUp = AfterAdsForMoves(WalletWith(0), Today, ads: 3);

        // The next calendar day, however few hours later, the three are back.
        DayStamp tomorrow = Today.Plus(1);
        Assert.Equal(3, Rules.ExtraMoves.TermsFor(usedUp, tomorrow).FreeAdsLeftToday);
        Wallet nextDay = AfterAdsForMoves(usedUp, tomorrow, ads: 1);
        Assert.Equal(new AdsWatched(tomorrow, forExtraMoves: 1, forCoins: 0), nextDay.Ads);

        // The phone's date set back to a day before the counted one: no allowance opens, on that day or again today.
        var yesterday = new DayStamp(Today.Number - 1);
        Assert.Equal(0, Rules.ExtraMoves.TermsFor(usedUp, yesterday).FreeAdsLeftToday);
        Assert.Equal(new AdOutcome.NoneLeftToday(), Rules.ExtraMoves.PaidWithAnAd(usedUp, yesterday));
        Assert.Equal(2, Rules.ExtraMoves.TermsFor(nextDay, Today).FreeAdsLeftToday);
    }

    [Fact]
    public void A_start_booster_is_picked_only_when_it_is_owned_and_the_attempt_has_room()
    {
        BoosterShelf owned = OneOfEachStartBooster.WithOneFewer(BoosterKind.StartExtraMoves);

        // Not owned: refused by name, and the choice stays.
        Assert.Equal(new StartToggle.NoneOwned(BoosterKind.StartExtraMoves), Rules.StartBoosters.Toggled(StartChoice.None, BoosterKind.StartExtraMoves, owned));

        // The tests' settings allow two an attempt: the third pick is refused with the cap.
        StartChoice two = Picked(Picked(StartChoice.None, BoosterKind.StartColorBomb, OneOfEachStartBooster), BoosterKind.StartRocketAndBomb, OneOfEachStartBooster);
        Assert.Equal(2, Rules.StartBoosters.PerAttempt);
        Assert.Equal([BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb], two.Boosters);
        Assert.Equal(new StartToggle.NoMoreThisAttempt(PerAttempt: 2), Rules.StartBoosters.Toggled(two, BoosterKind.StartExtraMoves, OneOfEachStartBooster));

        // With one an attempt (one edit of the settings) the second pick is refused.
        StartBoosterRules oneAnAttempt = MetaSupport.Rules(pack => pack.startBoosters.perAttempt = 1).StartBoosters;
        StartChoice one = Assert.IsType<StartToggle.Changed>(oneAnAttempt.Toggled(StartChoice.None, BoosterKind.StartColorBomb, OneOfEachStartBooster)).Choice;
        Assert.Equal(new StartToggle.NoMoreThisAttempt(PerAttempt: 1), oneAnAttempt.Toggled(one, BoosterKind.StartRocketAndBomb, OneOfEachStartBooster));

        // A booster that is used during play is never a start booster: asking for one is a mistake of the caller.
        Assert.Throws<ArgumentException>(() => Rules.StartBoosters.Toggled(StartChoice.None, BoosterKind.Shuffle, BoosterShelf.Empty.WithOneMore(BoosterKind.Shuffle)));
    }

    [Fact]
    public void A_second_press_puts_a_booster_back_and_picking_spends_nothing_until_the_attempt_counts()
    {
        StartChoice both = Picked(Picked(StartChoice.None, BoosterKind.StartExtraMoves, OneOfEachStartBooster), BoosterKind.StartRocketAndBomb, OneOfEachStartBooster);

        // Nothing is ever picked for the player, and a pick can be undone, also when the attempt is full.
        Assert.Empty(StartChoice.None.Boosters);
        StartChoice one = Picked(both, BoosterKind.StartExtraMoves, OneOfEachStartBooster);
        Assert.Equal([BoosterKind.StartRocketAndBomb], one.Boosters);
        Assert.Equal(StartChoice.None, Picked(one, BoosterKind.StartRocketAndBomb, OneOfEachStartBooster));

        // The choice goes to the engine in the engine's own words and order.
        Assert.Equal([StartBooster.RocketAndBomb, StartBooster.ExtraMoves], both.ForTheEngine());
        var options = new GameOptions { StartBoosters = both.ForTheEngine() };
        Assert.Equal(2, options.StartBoosters.Count);

        // The shelf loses the picked boosters only when the caller says the attempt counts.
        BoosterShelf left = both.TakenFrom(OneOfEachStartBooster);
        Assert.Equal(BoosterShelf.Empty.WithOneMore(BoosterKind.StartColorBomb), left);
        Assert.Equal(3, OneOfEachStartBooster.Total);
        Assert.Throws<InvalidOperationException>(() => both.TakenFrom(BoosterShelf.Empty));
    }
}
