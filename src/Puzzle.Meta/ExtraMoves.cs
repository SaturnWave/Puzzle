using System;

namespace Puzzle.Meta
{
    /// <summary>
    /// What the extra moves would cost a player right now, by each way of paying: what the card of a level that ran
    /// out of moves shows on its two buttons. It is a reading of the wallet and promises nothing; paying asks the
    /// rule again (<see cref="ExtraMovesRules"/>).
    /// </summary>
    public sealed class ExtraMovesTerms : IEquatable<ExtraMovesTerms>
    {
        internal ExtraMovesTerms(Coins price, Coins missing, int freeAdsLeftToday)
        {
            Price = price;
            Missing = missing;
            FreeAdsLeftToday = freeAdsLeftToday;
        }

        /// <summary>Coins the extra moves cost.</summary>
        public Coins Price { get; }

        /// <summary>What the player lacks for the price; none when the coins cover it.</summary>
        public Coins Missing { get; }

        public bool CoinsCoverIt => Missing.IsNone;

        /// <summary>How many more times today an ad pays for the extra moves.</summary>
        public int FreeAdsLeftToday { get; }

        public bool AnAdPaysToday => FreeAdsLeftToday > 0;

        public bool Equals(ExtraMovesTerms? other) =>
            other is not null && Price == other.Price && Missing == other.Missing && FreeAdsLeftToday == other.FreeAdsLeftToday;

        public override bool Equals(object? obj) => Equals(obj as ExtraMovesTerms);

        public override int GetHashCode() => HashCode.Combine(Price, Missing, FreeAdsLeftToday);

        public override string ToString() => $"{Price} coins ({Missing} missing) or an ad ({FreeAdsLeftToday} left today)";
    }

    /// <summary>
    /// What the extra-moves offer costs (the settings' extraMovesOffer): an ad a few times a day, coins at any time.
    /// How many moves it adds is the engine's to say, and whether a game may take it too: the client asks the engine
    /// first and charges the wallet only when the moves were given.
    /// </summary>
    public sealed class ExtraMovesRules
    {
        /// <exception cref="ArgumentOutOfRangeException">A negative number of ads a day.</exception>
        public ExtraMovesRules(int freeAdsPerDay, Coins price)
        {
            if (freeAdsPerDay < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(freeAdsPerDay), freeAdsPerDay, "A day has none or more free ads.");
            }

            FreeAdsPerDay = freeAdsPerDay;
            Price = price;
        }

        /// <summary>The offer costs an ad this many times a calendar day.</summary>
        public int FreeAdsPerDay { get; }

        public Coins Price { get; }

        public ExtraMovesTerms TermsFor(Wallet wallet, DayStamp today) =>
            new ExtraMovesTerms(Price, wallet.Coins.ShortOf(Price), FreeAdsLeft(wallet, today));

        /// <summary>The extra moves paid with coins: <see cref="Purchase.Bought"/> or <see cref="Purchase.NotEnoughCoins"/>.</summary>
        public Purchase PaidWithCoins(Wallet wallet) => Purchase.For(Price, wallet, paid => paid);

        /// <summary>The extra moves paid with an ad that was watched on <paramref name="today"/>.</summary>
        public AdOutcome PaidWithAnAd(Wallet wallet, DayStamp today)
        {
            if (FreeAdsLeft(wallet, today) == 0)
            {
                return new AdOutcome.NoneLeftToday();
            }

            return new AdOutcome.Rewarded(wallet.WithAds(wallet.Ads.WithOneForExtraMoves(today)));
        }

        private int FreeAdsLeft(Wallet wallet, DayStamp today) => Math.Max(0, FreeAdsPerDay - wallet.Ads.On(today).ForExtraMoves);
    }
}
