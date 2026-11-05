using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>Boosters sold together for coins.</summary>
    public sealed class BoosterSet
    {
        /// <exception cref="ArgumentException">The set holds no booster.</exception>
        public BoosterSet(string name, Coins price, BoosterShelf boosters)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Boosters = boosters ?? throw new ArgumentNullException(nameof(boosters));
            if (boosters.IsEmpty)
            {
                throw new ArgumentException($"The set '{name}' holds no booster.", nameof(boosters));
            }

            Price = price;
        }

        /// <summary>The settings' name for the set; what a player sees it called is the client's business.</summary>
        public string Name { get; }

        public Coins Price { get; }

        /// <summary>What the set gives, by kind.</summary>
        public BoosterShelf Boosters { get; }

        public override string ToString() => $"set '{Name}' for {Price}: {Boosters}";
    }

    /// <summary>Coins for watching an ad, a few times a day (the settings' shop.adForCoins).</summary>
    public sealed class AdForCoins
    {
        /// <exception cref="ArgumentOutOfRangeException">A negative number of ads a day.</exception>
        public AdForCoins(Coins coins, int perDay)
        {
            if (perDay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perDay), perDay, "A day has none or more ads for coins.");
            }

            Coins = coins;
            PerDay = perDay;
        }

        /// <summary>Coins one watched ad gives.</summary>
        public Coins Coins { get; }

        /// <summary>How many times a calendar day an ad gives coins.</summary>
        public int PerDay { get; }
    }

    /// <summary>
    /// What a player can get beyond playing (the settings' prices, shop and store): one booster or a set of them for
    /// coins, coins for an ad, and what the store sells for real money (<see cref="Store"/>), which a test build shows
    /// and does not sell (<see cref="RealMoney"/>). A purchase for coins that is refused gives no wallet back, so
    /// nothing can have changed. What is bought for money is paid in the platform's store and put into the wallet by
    /// the client, once per payment; no rule here takes money.
    /// </summary>
    public sealed class Shop
    {
        private readonly Dictionary<BoosterKind, Coins> _boosterPrices;

        /// <exception cref="ArgumentException">A booster has no price.</exception>
        public Shop(
            IReadOnlyDictionary<BoosterKind, Coins> boosterPrices,
            IReadOnlyList<BoosterSet> boosterSets,
            StoreCatalogue store,
            AdForCoins adForCoins,
            RealMoneySales realMoney)
        {
            _boosterPrices = new Dictionary<BoosterKind, Coins>();
            foreach (BoosterKind kind in BoosterKinds.All)
            {
                _boosterPrices[kind] = boosterPrices.TryGetValue(kind, out Coins price)
                    ? price
                    : throw new ArgumentException($"{kind.Name()} has no price.", nameof(boosterPrices));
            }

            BoosterSets = (boosterSets ?? throw new ArgumentNullException(nameof(boosterSets))).ToArray();
            Store = store ?? throw new ArgumentNullException(nameof(store));
            AdForCoins = adForCoins ?? throw new ArgumentNullException(nameof(adForCoins));
            RealMoney = realMoney;
        }

        /// <summary>The sets, in the order the settings list them.</summary>
        public IReadOnlyList<BoosterSet> BoosterSets { get; }

        /// <summary>What the game sells for real money: the coin packs and the starter pack.</summary>
        public StoreCatalogue Store { get; }

        /// <summary>The packs of coins, in the order the settings list them.</summary>
        public IReadOnlyList<StoreProduct> CoinPacks => Store.CoinPacks;

        public AdForCoins AdForCoins { get; }

        /// <summary>Whether this build sells the store's products for money at all.</summary>
        public RealMoneySales RealMoney { get; }

        /// <summary>Coins one booster of the kind costs.</summary>
        public Coins PriceOf(BoosterKind kind) => _boosterPrices[kind];

        /// <summary>One booster for its single price: <see cref="Purchase.Bought"/> or <see cref="Purchase.NotEnoughCoins"/>.</summary>
        public Purchase BuyOne(BoosterKind kind, Wallet wallet) =>
            Purchase.For(PriceOf(kind), wallet, paid => paid.WithShelf(paid.Shelf.WithOneMore(kind)));

        /// <summary>A set for its price: <see cref="Purchase.Bought"/> or <see cref="Purchase.NotEnoughCoins"/>.</summary>
        /// <exception cref="ArgumentException">The set is not one of <see cref="BoosterSets"/>: a price comes from the settings, never from a caller.</exception>
        public Purchase Buy(BoosterSet set, Wallet wallet)
        {
            if (!BoosterSets.Contains(set))
            {
                throw new ArgumentException($"The shop does not sell the {set}.", nameof(set));
            }

            return Purchase.For(set.Price, wallet, paid => paid.WithShelf(paid.Shelf.Plus(set.Boosters)));
        }

        /// <summary>How many more times on <paramref name="today"/> an ad gives the player coins.</summary>
        public int AdsForCoinsLeft(Wallet wallet, DayStamp today) => Math.Max(0, AdForCoins.PerDay - wallet.Ads.On(today).ForCoins);

        /// <summary>The coins of an ad that was watched on <paramref name="today"/>.</summary>
        public AdOutcome CoinsForAnAd(Wallet wallet, DayStamp today)
        {
            if (AdsForCoinsLeft(wallet, today) == 0)
            {
                return new AdOutcome.NoneLeftToday();
            }

            return new AdOutcome.Rewarded(wallet.WithCoins(wallet.Coins + AdForCoins.Coins).WithAds(wallet.Ads.WithOneForCoins(today)));
        }
    }
}
