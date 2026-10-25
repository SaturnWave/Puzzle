using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Core.Tests;

/// <summary>
/// The balance policy of the economy (docs/GAME_DESIGN.md section 25), as far as it is a relation between the amounts
/// of the shipped settings: whatever the amounts are, these must hold, so a test here fails when one number is changed
/// and another that follows from it is not. What a change does to a player's journey is the simulator's to say.
/// The pricing rules of what is sold for real money are rules 1, 2, 3 and 6 of the research's section 3.2
/// (docs/research/monetization-and-retention.md); rule 4, no random contents, holds by the format, which can only list
/// what a product gives.
/// </summary>
public class EconomyBalancePolicyTests
{
    private static EconomySettings Shipped => SettingsLoader.Load(RepoFiles.EconomySettingsPath("default.json"));

    /// <summary>The coin packs from the cheapest up.</summary>
    private static StoreProduct[] PacksBySize(EconomySettings settings) => settings.Store.CoinPacks.OrderBy(pack => pack.Price).ToArray();

    [Fact]
    public void The_welcome_coins_buy_a_start_booster_and_never_the_extra_moves()
    {
        EconomySettings settings = Shipped;
        Coins cheapestStartBooster = settings.Prices.Boosters.Where(price => BoosterKinds.IsStart(price.Key)).Min(price => price.Value);

        // The first visit to the shop can buy something; the first +5 for coins is earned by playing.
        Assert.True(settings.Start.Coins >= cheapestStartBooster, $"{settings.Start.Coins} buy no start booster at {cheapestStartBooster}");
        Assert.True(settings.Start.Coins < settings.ExtraMovesOffer.Price, $"{settings.Start.Coins} already buy the extra moves at {settings.ExtraMovesOffer.Price}");
    }

    [Fact]
    public void The_welcome_gift_holds_every_booster_and_favours_none()
    {
        EconomySettings settings = Shipped;

        // A booster used during play is given as it opens (its unlock gift), a start booster before the first level.
        Dictionary<BoosterKind, int> gift = BoosterKinds.All.ToDictionary(
            kind => kind,
            kind => settings.Start.Boosters[kind] + (kind.IsInLevel() ? settings.Unlocks.Gift[kind] : 0));

        // Every booster is met once without coins, and the gift does not steer the player to one of them.
        Assert.All(gift, given => Assert.True(given.Value >= 1, $"{given.Key.Name()} is missing from the welcome gift"));
        Assert.Single(gift.Values.Distinct());
    }

    [Fact]
    public void A_booster_used_during_play_is_given_as_it_opens_and_not_before_the_first_level()
    {
        EconomySettings settings = Shipped;

        // The owner's decision of 2026-10-15: the spatula on level 7, the brushes on 12 and 16, the shuffle on 20.
        Assert.All(BoosterKinds.All.Where(BoosterKinds.IsInLevel), kind => Assert.Equal(0, settings.Start.Boosters[kind]));
        Assert.Equal(
            new[] { 7, 12, 16, 20 },
            new[] { BoosterKind.HitCell, BoosterKind.HitRow, BoosterKind.HitColumn, BoosterKind.Shuffle }.Select(kind => settings.Unlocks.Levels[kind].Value));
    }

    [Fact]
    public void A_booster_set_costs_what_its_boosters_cost_one_by_one()
    {
        EconomySettings settings = Shipped;

        // No discount is invented in the shop: a set is a way to buy several at once, not a cheaper way.
        Assert.All(settings.Game.BoosterSets, set =>
        {
            Coins oneByOne = set.Value.Boosters.Aggregate(Coins.None, (sum, held) => sum + (settings.Prices.Boosters[held.Key] * held.Value));
            Assert.True(set.Value.Price == oneByOne, $"the set '{set.Key}' costs {set.Value.Price}, its boosters one by one {oneByOne}");
        });
    }

    [Fact]
    public void A_days_ads_for_coins_pay_less_than_a_tenth_of_the_extra_moves()
    {
        EconomySettings settings = Shipped;
        Coins aDay = settings.Game.CoinsForAnAd * settings.Game.AdsForCoinsPerDay;

        // Coins for ads stay a small extra: ten days of them buy no +5.
        Assert.True(aDay * 10 < settings.ExtraMovesOffer.Price, $"a day's ads pay {aDay}, ten days {aDay * 10}, the extra moves cost {settings.ExtraMovesOffer.Price}");
    }

    [Fact]
    public void A_bigger_coin_pack_never_gives_fewer_coins_a_dollar_and_the_largest_at_most_35_percent_more()
    {
        EconomySettings settings = Shipped;
        StoreProduct[] bySize = PacksBySize(settings);

        // Pricing rule 1. The shop shows the packs from the cheapest up, no two at one price.
        Assert.Equal(bySize.Select(pack => pack.Name), settings.Store.CoinPacks.Select(pack => pack.Name));
        Assert.Equal(bySize.Length, bySize.Select(pack => pack.Price).Distinct().Count());

        // Coins a dollar are compared as products of whole numbers, so no division rounds: coins / price of the
        // larger pack at least those of the smaller.
        foreach ((StoreProduct smaller, StoreProduct larger) in bySize.Zip(bySize.Skip(1)))
        {
            Assert.True(
                larger.Coins.Amount * smaller.Price.Cents >= smaller.Coins.Amount * larger.Price.Cents,
                $"the {larger.Name} gives {larger.Coins} coins for {larger.Price}, fewer a dollar than the {smaller.Name}'s {smaller.Coins} for {smaller.Price}");
        }

        // The rivals' $19.99 packs give 24 to 33 % more a dollar than their smallest; ours may give at most 35 % more.
        StoreProduct smallest = bySize[0];
        StoreProduct largest = bySize[^1];
        Assert.True(
            100 * largest.Coins.Amount * smallest.Price.Cents <= 135 * smallest.Coins.Amount * largest.Price.Cents,
            $"the {largest.Name} gives {largest.Coins} coins for {largest.Price}, more than 35 % more a dollar than the {smallest.Name}'s {smallest.Coins} for {smallest.Price}");
    }

    [Fact]
    public void Every_coin_pack_buys_a_whole_number_of_extra_moves_and_a_bigger_one_buys_more()
    {
        EconomySettings settings = Shipped;
        long price = settings.ExtraMovesOffer.Price.Amount;

        // Pricing rule 2. The shop says "about N times +5": N is the whole number a pack buys, and less than one +5
        // is left over. Even the smallest pack buys one, which is why no $0.99 pack is sold.
        long[] times = PacksBySize(settings).Select(pack => pack.Coins.Amount / price).ToArray();
        Assert.All(times, buys => Assert.True(buys >= 1, $"a pack buys {buys} times the extra moves at {price}"));
        Assert.True(times.Zip(times.Skip(1)).All(pair => pair.Second > pair.First), $"the packs buy the extra moves {string.Join(", ", times)} times");
    }

    [Fact]
    public void The_starter_pack_is_worth_at_most_two_and_a_half_times_its_price_in_coins_and_is_sold_once()
    {
        EconomySettings settings = Shipped;
        StarterPackSettings starter = settings.Store.StarterPack;
        StoreProduct product = starter.Product;

        // Pricing rule 3. Its worth is its coins and its boosters at the shop's coin prices; the coin ladder at its
        // price is what the largest pack it could buy instead gives a dollar. Compared as products of whole numbers:
        // worth / price <= 2.5 * coins / pack price.
        Coins worth = product.WorthAt(settings.Prices.Boosters);
        StoreProduct? ladder = PacksBySize(settings).LastOrDefault(pack => pack.Price <= product.Price);
        Assert.NotNull(ladder);
        Assert.True(
            2 * worth.Amount * ladder.Price.Cents <= 5 * ladder.Coins.Amount * product.Price.Cents,
            $"the starter pack is worth {worth} coins for {product.Price}; the {ladder.Name} gives {ladder.Coins} for {ladder.Price}");

        // Once per player, and only with boosters whose prices are measured (the start boosters), so its worth is known.
        Assert.Equal(1, starter.TimesPerPlayer);
        Assert.All(product.Boosters.Keys, kind => Assert.True(kind.IsStart(), $"the starter pack holds {kind.Name()}, whose price is not measured"));

        // It is offered once every booster has opened, so that each of them has been met before it is sold.
        int lastOpening = settings.Unlocks.Levels.Values.Max(level => level.Value);
        Assert.True(starter.FromLevel.Value >= lastOpening, $"the starter pack is offered from level {starter.FromLevel}, the last booster opens on {lastOpening}");
    }

    [Fact]
    public void Every_store_price_ends_in_99_cents_and_none_is_above_19_99()
    {
        // Pricing rule 6: the rivals' prices all end in .99, and no product above $19.99 is sold at launch.
        Assert.All(Shipped.Store.Products, product =>
        {
            Assert.True(product.Price.CentsAfterTheDollars == 99, $"the {product.Name} costs {product.Price}");
            Assert.True(product.Price <= new UsDollars(1999), $"the {product.Name} costs {product.Price}");
        });
    }
}
