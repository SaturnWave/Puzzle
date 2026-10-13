using Puzzle.Meta;
using Puzzle.Meta.Settings;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the tests of the game's meta rules start from: a pack of economy settings of the tests' own, not the shipped
/// one, whose prices and amounts are placeholders their owner changes. A test of a rule (lives, offers, rewards, the
/// shop) must not move when a price does; only the tests of the pinned pack read the shipped numbers.
/// </summary>
internal static class MetaSupport
{
    /// <summary>The engine's order: what a rotation of every booster gives in turn.</summary>
    public static readonly string[] EveryBooster = BoosterKinds.All.Select(BoosterKinds.Name).ToArray();

    /// <summary>A Tuesday evening where the player is three hours ahead of Greenwich: late enough for a test to cross midnight.</summary>
    public static readonly DateTimeOffset Evening = new(2026, 10, 13, 21, 0, 0, TimeSpan.FromHours(3));

    public static DayStamp Today { get; } = DayStamp.Of(Evening);

    public static DateTimeOffset MinutesLater(double minutes) => Evening.AddMinutes(minutes);

    /// <summary>
    /// The tests' pack: five lives and thirty minutes as decided; 10 coins a move left and 25 a level; the +5 for
    /// 900 coins or one of 3 ads a day; a new player with 100 coins, one rocket-and-bomb and two shuffles; two start
    /// boosters an attempt; a starter set for 2 100 and a master set for 6 300; 50 coins for one of 2 ads a day.
    /// </summary>
    public static EconomyFile Pack() => new()
    {
        formatVersion = EconomyFile.CurrentFormatVersion,
        lives = { max = 5, minutesPerLife = 30 },
        coins = { perMoveLeftInFinale = 10, perLevelWon = 25 },
        rewards =
        {
            paintingCompleted = { coins = 80, boostersEachTime = 1, boosterRotation = EveryBooster },
            daily = { coins = 5, everyDays = 1, boostersEachTime = 1, boosterRotation = EveryBooster },
        },
        extraMovesOffer = { freeAdsPerDay = 3, priceCoins = 900 },
        prices =
        {
            boosters =
            [
                Price("start-rocket-and-bomb", 600), Price("start-color-bomb", 1000), Price("start-extra-moves", 500),
                Price("hit-cell", 400), Price("hit-row", 700), Price("hit-column", 700), Price("shuffle", 300),
            ],
        },
        start =
        {
            coins = 100,
            boosters =
            [
                Count("start-rocket-and-bomb", 1), Count("start-color-bomb", 0), Count("start-extra-moves", 0),
                Count("hit-cell", 0), Count("hit-row", 0), Count("hit-column", 0), Count("shuffle", 2),
            ],
        },
        startBoosters = { perAttempt = 2 },
        shop =
        {
            coinPacks = [new CoinPackFile { name = "small", coins = 500 }, new CoinPackFile { name = "large", coins = 2500 }],
            boosterSets =
            [
                new BoosterSetFile
                {
                    name = "starter",
                    priceCoins = 2100,
                    boosters = [Count("start-rocket-and-bomb", 1), Count("start-color-bomb", 1), Count("start-extra-moves", 1)],
                },
                new BoosterSetFile
                {
                    name = "master",
                    priceCoins = 6300,
                    boosters = [Count("hit-cell", 3), Count("hit-row", 3), Count("hit-column", 3), Count("shuffle", 3)],
                },
            ],
            adForCoins = { coins = 50, perDay = 2 },
        },
        build = { ads = EconomyFileMapper.SimulatedAdsWord, realMoneyPurchases = EconomyFileMapper.NoRealMoneyWord },
    };

    /// <summary>The rules of the tests' pack, with <paramref name="change"/> made to the pack first.</summary>
    public static EconomyRules Rules(Action<EconomyFile>? change = null)
    {
        EconomyFile pack = Pack();
        change?.Invoke(pack);
        return EconomyFileMapper.ToRules(pack);
    }

    /// <summary>A wallet with these coins and nothing else: every life, no booster, nothing used.</summary>
    public static Wallet WalletWith(long coins) => Wallet.Empty.WithCoins(new Coins(coins));

    public static BoosterPriceFile Price(string booster, long coins) => new() { booster = booster, coins = coins };

    public static BoosterCountFile Count(string booster, int count) => new() { booster = booster, count = count };
}
