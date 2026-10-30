using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>
    /// A price point of the US store, in whole cents ($1.99 is 199): the settings' price of a product. The game never
    /// shows it to a player, who sees the price the store words for them in their own currency; it is what the
    /// pricing rules and an Editor's stand-in store go by. Money is never a fraction here.
    /// </summary>
    public readonly struct UsCents : IEquatable<UsCents>
    {
        /// <exception cref="ArgumentOutOfRangeException">The price is not above nothing.</exception>
        public UsCents(long cents)
        {
            if (cents < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cents), cents, "A price is at least one cent.");
            }

            Cents = cents;
        }

        public long Cents { get; }

        public bool Equals(UsCents other) => Cents == other.Cents;

        public override bool Equals(object? obj) => obj is UsCents other && Equals(other);

        public override int GetHashCode() => Cents.GetHashCode();

        /// <summary>The price as the US store writes it: "$1.99".</summary>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "${0}.{1:00}", Cents / 100, Cents % 100);

        public static bool operator ==(UsCents left, UsCents right) => left.Equals(right);

        public static bool operator !=(UsCents left, UsCents right) => !left.Equals(right);
    }

    /// <summary>What a product of the store is to a player. A closed set: the stores sell the two kinds differently.</summary>
    public enum StoreProductKind
    {
        /// <summary>A pack of coins, bought as often as the player likes; the store forgets it once the game has it (a consumable).</summary>
        CoinPack,

        /// <summary>
        /// The starter pack, sold a limited number of times to a player: the store keeps it as the player's for good
        /// (a non-consumable), refuses to sell it to them again, and gives it back to a game that asks for what the
        /// player owns.
        /// </summary>
        StarterPack,
    }

    /// <summary>
    /// Something the game sells for real money (the settings' store section): what it gives, its price in the US
    /// store, and the end of its id in both stores. Its contents are listed, so nothing of it is random, and nothing
    /// of it reaches a level.
    /// </summary>
    public sealed class StoreProduct
    {
        /// <param name="idSuffix">
        /// What follows the game's application id and a dot in the product's id in both stores ("coins.pouch"): the
        /// client puts the two together, so the ids follow the application id when that is decided.
        /// </param>
        /// <exception cref="ArgumentException">A name or an id suffix that is empty.</exception>
        public StoreProduct(string name, string idSuffix, StoreProductKind kind, UsCents usPrice, Coins coins, BoosterShelf boosters)
        {
            Name = string.IsNullOrEmpty(name) ? throw new ArgumentException("A product has a name.", nameof(name)) : name;
            IdSuffix = string.IsNullOrEmpty(idSuffix) ? throw new ArgumentException("A product has an id.", nameof(idSuffix)) : idSuffix;
            Kind = kind;
            UsPrice = usPrice;
            Coins = coins;
            Boosters = boosters ?? throw new ArgumentNullException(nameof(boosters));
        }

        /// <summary>The settings' name for it ("pouch", "starter-pack"); what a player sees it called is the client's business.</summary>
        public string Name { get; }

        /// <summary>The end of its id in both stores, after the application id and a dot.</summary>
        public string IdSuffix { get; }

        public StoreProductKind Kind { get; }

        /// <summary>Its price point in the US store; never shown to a player.</summary>
        public UsCents UsPrice { get; }

        public Coins Coins { get; }

        /// <summary>The boosters it gives besides the coins; none for a coin pack.</summary>
        public BoosterShelf Boosters { get; }

        /// <summary>What it puts into the wallet: its coins, and its boosters one by one in the engine's order.</summary>
        public Reward Contents => new Reward(Coins, BoosterKinds.All.SelectMany(kind => Enumerable.Repeat(kind, Boosters.Count(kind))).ToArray());

        public override string ToString() => $"{Kind} '{Name}' ({IdSuffix}, {UsPrice}): {Contents}";
    }

    /// <summary>The starter pack: a product sold a limited number of times to a player, and offered only from a level on.</summary>
    public sealed class StarterPack
    {
        /// <exception cref="ArgumentException">The product is not of the starter pack's kind.</exception>
        /// <exception cref="ArgumentOutOfRangeException">It is sold fewer than once, or offered before the first level.</exception>
        public StarterPack(StoreProduct product, int timesPerPlayer, int fromLevel)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            if (product.Kind != StoreProductKind.StarterPack)
            {
                throw new ArgumentException($"The {product} is not a starter pack.", nameof(product));
            }

            TimesPerPlayer = timesPerPlayer >= 1
                ? timesPerPlayer
                : throw new ArgumentOutOfRangeException(nameof(timesPerPlayer), timesPerPlayer, "A starter pack is sold at least once.");
            FromLevel = fromLevel >= 1
                ? fromLevel
                : throw new ArgumentOutOfRangeException(nameof(fromLevel), fromLevel, "Levels are counted from 1.");
        }

        public StoreProduct Product { get; }

        /// <summary>How many times one player can buy it.</summary>
        public int TimesPerPlayer { get; }

        /// <summary>The shop offers it once the player has come to this level (counted from 1), until it is bought.</summary>
        public int FromLevel { get; }

        /// <summary>True when a player who stands on <paramref name="levelNumber"/> (counted from 1) and bought it so many times is offered it.</summary>
        public bool IsOfferedTo(int levelNumber, int timesBought) => levelNumber >= FromLevel && timesBought < TimesPerPlayer;

        public override string ToString() => $"{Product}, {TimesPerPlayer} per player from level {FromLevel}";
    }

    /// <summary>What the game sells for real money: the coin packs, in the order the shop shows them, and the starter pack.</summary>
    public sealed class StoreCatalogue
    {
        /// <exception cref="ArgumentException">No coin pack, a coin pack of another kind, or two products with one id.</exception>
        public StoreCatalogue(IReadOnlyList<StoreProduct> coinPacks, StarterPack starterPack)
        {
            CoinPacks = (coinPacks ?? throw new ArgumentNullException(nameof(coinPacks))).ToArray();
            StarterPack = starterPack ?? throw new ArgumentNullException(nameof(starterPack));
            if (CoinPacks.Count == 0)
            {
                throw new ArgumentException("The store sells at least one pack of coins.", nameof(coinPacks));
            }

            if (CoinPacks.FirstOrDefault(pack => pack.Kind != StoreProductKind.CoinPack) is StoreProduct notAPack)
            {
                throw new ArgumentException($"The {notAPack} is not a pack of coins.", nameof(coinPacks));
            }

            Products = CoinPacks.Append(starterPack.Product).ToArray();
            if (Products.GroupBy(product => product.IdSuffix).FirstOrDefault(sameId => sameId.Count() > 1) is IGrouping<string, StoreProduct> twice)
            {
                throw new ArgumentException($"Two products have the id '{twice.Key}'; a store knows a product by its id alone.", nameof(coinPacks));
            }
        }

        /// <summary>The packs of coins, in the order the shop shows them.</summary>
        public IReadOnlyList<StoreProduct> CoinPacks { get; }

        public StarterPack StarterPack { get; }

        /// <summary>Every product, the coin packs first.</summary>
        public IReadOnlyList<StoreProduct> Products { get; }

        /// <summary>The product with this end of its id; nothing when the game sells no such product.</summary>
        public StoreProduct? Find(string idSuffix) => Products.FirstOrDefault(product => product.IdSuffix == idSuffix);
    }
}
