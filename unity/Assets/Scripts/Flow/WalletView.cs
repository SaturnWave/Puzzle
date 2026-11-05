#nullable enable
using System;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the museum shows of a player's wallet at one instant, on its row and on its cards: the coins, the
    /// lives that are there with every life that has come back by then, while a life is missing how long until
    /// the next one is back and until all are, and the boosters on the shelf (the card before a level counts them).
    /// The meter is settled at the instant it is seen (a life is stored as missing until a loss settles it), so the
    /// same wallet looked at later shows more lives and shorter waits. In the tester mode (<see cref="WalletHandling.Unlimited"/>)
    /// the view still holds the coins and the meter as they are, and says that neither has an end: every price is
    /// paid and a level is open without a life.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// A phone clock set back puts the next life further away than a life ever takes: the meter brings no life back
    /// for it (<see cref="LifeMeter.SettledAt"/>), and the view shows no wait longer than one life takes, so a pill
    /// made for "12:34" never has to say "2880:00". The wait then stands still until the clock has caught up.
    /// </remarks>
    public readonly struct WalletView : IEquatable<WalletView>
    {
        /// <summary>Nothing only in the struct's default, which holds no wallet.</summary>
        private readonly BoosterShelf? _shelf;

        private WalletView(Coins coins, int lives, int mostLives, TimeSpan? untilNextLife, TimeSpan? untilFull, BoosterShelf shelf, WalletHandling handling)
        {
            Coins = coins;
            Lives = lives;
            MostLives = mostLives;
            UntilNextLife = untilNextLife;
            UntilFull = untilFull;
            _shelf = shelf;
            Handling = handling;
        }

        public Coins Coins { get; }

        /// <summary>The boosters the player holds; an empty shelf for a view made without a wallet (the struct's default).</summary>
        public BoosterShelf Shelf => _shelf ?? BoosterShelf.Empty;

        /// <summary>How many lives are there at the instant.</summary>
        public int Lives { get; }

        /// <summary>How many lives a full meter holds.</summary>
        public int MostLives { get; }

        /// <summary>How long until the next life is back; absent while every life is there.</summary>
        public TimeSpan? UntilNextLife { get; }

        /// <summary>How long until every life is back; absent while every life is there.</summary>
        public TimeSpan? UntilFull { get; }

        public bool IsFull => UntilNextLife == null;

        /// <summary>How the client holds the wallet to the rules: as they have it, or the tester mode's.</summary>
        public WalletHandling Handling { get; }

        /// <summary>True in the tester mode: the coins and the lives have no end.</summary>
        public bool IsUnlimited => Handling == WalletHandling.Unlimited;

        /// <summary>True when a level may be started: a life is there to lose, or the lives have no end.</summary>
        public bool HasALife => IsUnlimited || Lives > 0;

        /// <summary>True when the wallet pays a price: its coins cover it, or the coins have no end.</summary>
        public bool CanPay(Coins price) => IsUnlimited || Coins.Covers(price);

        /// <summary>The wallet as it stands at <paramref name="now"/> by the rules of lives, held as the rules have it.</summary>
        /// <exception cref="ArgumentNullException">No wallet or no rules.</exception>
        public static WalletView Of(Wallet wallet, LifeRules rules, DateTimeOffset now) => Of(wallet, rules, now, WalletHandling.Normal);

        /// <summary>The wallet as it stands at <paramref name="now"/> by the rules of lives, held as the client holds it.</summary>
        /// <exception cref="ArgumentNullException">No wallet or no rules.</exception>
        public static WalletView Of(Wallet wallet, LifeRules rules, DateTimeOffset now, WalletHandling handling)
        {
            Coins coins = (wallet ?? throw new ArgumentNullException(nameof(wallet))).Coins;
            LifeRules lives = rules ?? throw new ArgumentNullException(nameof(rules));
            LifeMeter settled = wallet.Lives.SettledAt(now, lives);
            if (!(settled.UntilNextLife(now) is TimeSpan wait))
            {
                return new WalletView(coins, lives.Max, lives.Max, untilNextLife: null, untilFull: null, wallet.Shelf, handling);
            }

            TimeSpan untilNext = wait < lives.PerLife ? wait : lives.PerLife;
            TimeSpan untilFull = untilNext + TimeSpan.FromTicks(lives.PerLife.Ticks * (settled.ShortOfFull - 1));
            return new WalletView(coins, settled.LivesUnder(lives), lives.Max, untilNext, untilFull, wallet.Shelf, handling);
        }

        /// <summary>The same view with another amount of coins: what the row shows while an amount counts up to this one's.</summary>
        public WalletView WithCoins(Coins coins) => new WalletView(coins, Lives, MostLives, UntilNextLife, UntilFull, Shelf, Handling);

        public bool Equals(WalletView other) =>
            Coins == other.Coins && Lives == other.Lives && MostLives == other.MostLives
            && UntilNextLife == other.UntilNextLife && UntilFull == other.UntilFull && Shelf.Equals(other.Shelf) && Handling == other.Handling;

        public override bool Equals(object? obj) => obj is WalletView other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Coins, Lives, MostLives, UntilNextLife, UntilFull, Shelf, Handling);

        public override string ToString() => (IsFull
            ? $"{Coins} coins, {Lives} lives (full)"
            : $"{Coins} coins, {Lives} of {MostLives} lives, the next in {UntilNextLife}") + (IsUnlimited ? ", neither with an end (tester mode)" : string.Empty);

        public static bool operator ==(WalletView left, WalletView right) => left.Equals(right);

        public static bool operator !=(WalletView left, WalletView right) => !left.Equals(right);
    }
}
