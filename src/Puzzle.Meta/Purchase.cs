using System;

namespace Puzzle.Meta
{
    /// <summary>
    /// How an attempt to buy something ended. Closed set. Only <see cref="Bought"/> carries a wallet: after every
    /// other ending the player's wallet is the one they had.
    /// </summary>
    public abstract record Purchase
    {
        private Purchase()
        {
        }

        /// <summary>Paid for and received: the wallet after it.</summary>
        public sealed record Bought(Wallet Wallet) : Purchase;

        /// <summary>The coins do not cover the price; <paramref name="Missing"/> is what the player lacks.</summary>
        public sealed record NotEnoughCoins(Coins Missing) : Purchase;

        /// <summary>The thing is sold for real money, and this build sells nothing for money.</summary>
        public sealed record NotOnSale : Purchase;

        /// <summary>
        /// The one place coins are taken for something: when they cover <paramref name="price"/>, the price leaves
        /// the wallet and <paramref name="received"/> puts what was bought into it; otherwise nothing is touched.
        /// </summary>
        internal static Purchase For(Coins price, Wallet wallet, Func<Wallet, Wallet> received)
        {
            if (!wallet.Coins.Covers(price))
            {
                return new NotEnoughCoins(wallet.Coins.ShortOf(price));
            }

            return new Bought(received(wallet.WithCoins(wallet.Coins.Less(price))));
        }
    }

    /// <summary>
    /// How watching an ad for something ended. Closed set. The rule is asked once the ad was watched to its end;
    /// showing the ad is the client's business.
    /// </summary>
    public abstract record AdOutcome
    {
        private AdOutcome()
        {
        }

        /// <summary>The ad is counted for its day and what it paid for is given: the wallet after it.</summary>
        public sealed record Rewarded(Wallet Wallet) : AdOutcome;

        /// <summary>The day's ads for this are used up; nothing changed.</summary>
        public sealed record NoneLeftToday : AdOutcome;
    }

    /// <summary>Where an ad comes from. Closed set; this build knows one answer.</summary>
    public enum AdSource
    {
        /// <summary>A card stands in for the ad: nothing is advertised and nobody is paid. The allowances count as if it were one.</summary>
        Simulated,
    }

    /// <summary>Whether the game sells anything for real money. Closed set; this build knows one answer.</summary>
    public enum RealMoneySales
    {
        /// <summary>Nothing is sold for money: a coin pack is shown and cannot be bought.</summary>
        NotAvailable,
    }
}
