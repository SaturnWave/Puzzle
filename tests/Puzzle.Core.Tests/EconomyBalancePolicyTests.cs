using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Core.Tests;

/// <summary>
/// The balance policy of the economy (docs/GAME_DESIGN.md section 25), as far as it is a relation between the amounts
/// of the shipped settings: whatever the amounts are, these must hold, so a test here fails when one number is changed
/// and another that follows from it is not. What a change does to a player's journey is the simulator's to say.
/// </summary>
public class EconomyBalancePolicyTests
{
    private static EconomySettings Shipped => SettingsLoader.Load(RepoFiles.EconomySettingsPath("default.json"));

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
        IReadOnlyDictionary<BoosterKind, int> gift = Shipped.Start.Boosters;

        // Every booster is met once without coins, and the gift does not steer the player to one of them.
        Assert.Equal(BoosterKinds.All.Order(), gift.Keys.Order());
        Assert.All(gift.Values, count => Assert.True(count >= 1, "a booster is missing from the welcome gift"));
        Assert.Single(gift.Values.Distinct());
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
}
