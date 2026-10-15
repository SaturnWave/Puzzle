#nullable enable
using System;
using Puzzle.Core.Levels;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// An ad being shown, as the stage's clock moves it on: how far it has come and whether it is over. The stage
    /// shows it and waits for its end; what it pays for is given once it is over. No Unity dependency.
    /// </summary>
    public interface IAdShowing
    {
        /// <summary>How far the ad has come: 0 as it begins, 1 once it is over.</summary>
        float Progress { get; }

        /// <summary>How long the ad still takes, in seconds of the stage's clock.</summary>
        float SecondsLeft { get; }

        bool IsOver { get; }

        /// <summary>Moves the ad on by so many seconds of the stage's clock.</summary>
        void Advance(float seconds);
    }

    /// <summary>
    /// The ad of a test build (the settings' build.ads "simulated"): a card that stands in for one for three seconds,
    /// marked as the test build's stand-in. Nothing is advertised, nothing is loaded, nobody is paid, and it always
    /// runs to its end. Three seconds and not the thirty a real one takes, because nothing is being shown.
    /// </summary>
    public sealed class SimulatedAd : IAdShowing
    {
        public const float Seconds = 3f;

        private float _shown;

        public float Progress => Math.Min(1f, _shown / Seconds);

        public float SecondsLeft => Math.Max(0f, Seconds - _shown);

        public bool IsOver => _shown >= Seconds;

        /// <exception cref="ArgumentOutOfRangeException">The clock is moved backwards.</exception>
        public void Advance(float seconds)
        {
            if (seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "An ad's clock moves forward only.");
            }

            _shown = Math.Min(Seconds, _shown + seconds);
        }
    }

    /// <summary>How this build shows an ad: the way the economy's rules name (EconomyRules.Ads), which in a test build is the simulated one alone.</summary>
    public static class Ads
    {
        /// <summary>A new ad, shown as the build's rules say.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The rules name a way of showing ads this build does not have: it refuses to pretend it has one.</exception>
        public static IAdShowing Show(AdSource source) => source switch
        {
            AdSource.Simulated => new SimulatedAd(),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "This build shows an ad only as a simulated one."),
        };
    }

    /// <summary>What an ad pays for once it is over. A closed set (private constructor).</summary>
    public abstract class AdPrize
    {
        private AdPrize()
        {
        }

        /// <summary>The five more moves of a game that ran out of them.</summary>
        public sealed class ExtraMoves : AdPrize
        {
        }

        /// <summary>Coins in the shop (the settings' shop.adForCoins).</summary>
        public sealed class CoinsInTheShop : AdPrize
        {
            public CoinsInTheShop(Coins amount)
            {
                Amount = amount;
            }

            public Coins Amount { get; }
        }
    }

    /// <summary>What the card that stands in for an ad says, in the player's language.</summary>
    public static class AdWords
    {
        /// <summary>The card's nameplate, on two lines: what it is, and that it is the test build's.</summary>
        public const string Title = "Reklam canlandırması";
        public const string TestBuild = "TEST SÜRÜMÜ";

        /// <summary>Under its alcove: that no ad is shown, and what it pays for.</summary>
        public const string NothingIsShown = "Bu test sürümünde reklam gösterilmez.";

        public static string ItPaysFor(AdPrize prize) => prize switch
        {
            AdPrize.ExtraMoves _ => $"Ödülün: +{ExtraMovesOffer.Moves} hamle",
            AdPrize.CoinsInTheShop coins => $"Ödülün: +{NumberWords.Amount(coins.Amount.Amount)} altın",
            _ => throw new ArgumentOutOfRangeException(nameof(prize), prize, "An ad's prize the card has no words for."),
        };

        /// <summary>Beside its bar: how many seconds are left, a part of one counted as a whole.</summary>
        public static string SecondsLeft(float seconds) => $"{(int)Math.Ceiling(Math.Max(0f, seconds))} sn";
    }
}
