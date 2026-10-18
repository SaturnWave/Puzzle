#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the wallet row and the lives card say, in the language the game speaks (<see cref="Speech"/>). The row's
    /// Turkish words are the design canvas' (HomeTR: "1.250", "Dolu", the count on the heart); the lives card has no
    /// drawing, and its words are written in the canvas' manner. In the tester mode the amount and the count on the
    /// heart are "∞", the lives pill says "Test", why, and the lives card "Sınırsız". No Unity dependency.
    /// </summary>
    public static class WalletWords
    {
        /// <summary>The lives pill while every life is there.</summary>
        public static string Full => Speech.Say(WordId.LivesFull);

        /// <summary>The lives card's nameplate, and its line while every life is there.</summary>
        public static string LivesTitle => Speech.Say(WordId.LivesTitle);
        public static string AllLivesThere => Speech.Say(WordId.LivesAllThere);

        /// <summary>The lives card's line for a player without a life, who may have pressed the play button.</summary>
        public static string PlayWhenALifeComes => Speech.Say(WordId.LivesPlayWhenALifeComes);

        /// <summary>What the row and the lives card show of coins and lives that have no end, in the tester mode: a sign, the same in every language.</summary>
        public const string Unlimited = "∞";

        /// <summary>The lives pill in the tester mode, the lives card's figures under its heart there, and its lines.</summary>
        public static string TesterModePill => Speech.Say(WordId.TesterPill);
        public static string UnlimitedLives => Speech.Say(WordId.TesterUnlimitedLives);
        public static string TesterModeOn => Speech.Say(WordId.TesterModeIsOn);
        public static string NoLifeIsLost => Speech.Say(WordId.TesterNoLifeIsLost);

        /// <summary>The amount of coins, with its thousands set apart: "1.250".</summary>
        public static string Amount(Coins coins) => NumberWords.Amount(coins.Amount);

        /// <summary>The row's amount: where its count of the coins has got to, or "∞" in the tester mode.</summary>
        public static string AmountOnTheRow(WalletView wallet, Coins counted) => wallet.IsUnlimited ? Unlimited : Amount(counted);

        /// <summary>The count on the heart: the lives that are there, or "∞" in the tester mode.</summary>
        public static string OnTheHeart(WalletView wallet) => wallet.IsUnlimited ? Unlimited : Number(wallet.Lives);

        /// <summary>The lives pill: "Dolu" while every life is there, otherwise the wait for the next one, "12:34"; "Test" in the tester mode.</summary>
        public static string LivesPill(WalletView wallet) =>
            wallet.IsUnlimited ? TesterModePill : wallet.UntilNextLife is TimeSpan wait ? NumberWords.Countdown(wait) : Full;

        /// <summary>The lives there of the lives a full meter holds: "3 / 5"; "Sınırsız" in the tester mode, under a heart that says "∞".</summary>
        public static string LivesOfMost(WalletView wallet) => wallet.IsUnlimited ? UnlimitedLives : $"{Number(wallet.Lives)} / {Number(wallet.MostLives)}";

        /// <summary>When the next life is back: "Sonraki can 12:34".</summary>
        public static string NextLife(TimeSpan wait) => Speech.Say(WordId.LivesNextLife, ("wait", NumberWords.Countdown(wait)));

        /// <summary>When every life is back: "Canların 1 sa 42 dk sonra dolar".</summary>
        public static string FullAfter(TimeSpan wait) => Speech.Say(WordId.LivesFullAfter, ("wait", NumberWords.HoursAndMinutes(wait)));

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
