using System;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>
    /// The ads a player has watched on one calendar day, by what each paid for. The count belongs to its day: on a
    /// later day it starts from none, and on an earlier one (the phone's date set back) it stays as it is, so no
    /// day's allowance opens twice.
    /// </summary>
    public readonly struct AdsWatched : IEquatable<AdsWatched>
    {
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        public AdsWatched(DayStamp day, int forExtraMoves, int forCoins)
        {
            if (forExtraMoves < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(forExtraMoves), forExtraMoves, "A number of ads is never negative.");
            }

            if (forCoins < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(forCoins), forCoins, "A number of ads is never negative.");
            }

            Day = day;
            ForExtraMoves = forExtraMoves;
            ForCoins = forCoins;
        }

        /// <summary>No ad watched on any day: a new player.</summary>
        public static AdsWatched None => default;

        /// <summary>The day the counts are of.</summary>
        public DayStamp Day { get; }

        public int ForExtraMoves { get; }

        public int ForCoins { get; }

        /// <summary>The counts as they stand on <paramref name="today"/>: none on a later day than theirs, unchanged otherwise.</summary>
        public AdsWatched On(DayStamp today) => today > Day ? new AdsWatched(today, forExtraMoves: 0, forCoins: 0) : this;

        public AdsWatched WithOneForExtraMoves(DayStamp today)
        {
            AdsWatched counted = On(today);
            return new AdsWatched(counted.Day, counted.ForExtraMoves + 1, counted.ForCoins);
        }

        public AdsWatched WithOneForCoins(DayStamp today)
        {
            AdsWatched counted = On(today);
            return new AdsWatched(counted.Day, counted.ForExtraMoves, counted.ForCoins + 1);
        }

        public bool Equals(AdsWatched other) => Day == other.Day && ForExtraMoves == other.ForExtraMoves && ForCoins == other.ForCoins;

        public override bool Equals(object? obj) => obj is AdsWatched other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Day, ForExtraMoves, ForCoins);

        public override string ToString() => $"{ForExtraMoves} ads for moves and {ForCoins} for coins on {Day}";

        public static bool operator ==(AdsWatched left, AdsWatched right) => left.Equals(right);

        public static bool operator !=(AdsWatched left, AdsWatched right) => !left.Equals(right);
    }

    /// <summary>
    /// How many rewards a player has been given, by kind, and the boosters whose opening gift was given
    /// (<see cref="BoosterUnlocks"/>). A reward's number decides what it holds (the rotation), and a gift is given
    /// once for good, so both are part of what a player owns and are saved with it.
    /// </summary>
    public readonly struct RewardsGiven : IEquatable<RewardsGiven>
    {
        /// <summary>Rewards counted with no booster's opening gift given yet.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        /// <exception cref="ArgumentException">A day is stated for a daily reward that was never given, or none for one that was.</exception>
        public RewardsGiven(int forPaintings, int daily, DayStamp? lastDailyOn)
            : this(forPaintings, daily, lastDailyOn, BoosterKindSet.None)
        {
        }

        /// <param name="lastDailyOn">The day the latest daily reward was given; absent when none was.</param>
        /// <param name="unlockGifts">The boosters used during play whose opening gift was given.</param>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        /// <exception cref="ArgumentException">
        /// A day is stated for a daily reward that was never given, or none for one that was; or a start booster is
        /// said to have had an opening gift, which only the boosters used during play have.
        /// </exception>
        public RewardsGiven(int forPaintings, int daily, DayStamp? lastDailyOn, BoosterKindSet unlockGifts)
        {
            if (forPaintings < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(forPaintings), forPaintings, "A number of rewards is never negative.");
            }

            if (daily < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(daily), daily, "A number of rewards is never negative.");
            }

            if (lastDailyOn.HasValue != daily > 0)
            {
                throw new ArgumentException("The day of the latest daily reward is stated exactly when one was given.", nameof(lastDailyOn));
            }

            if (unlockGifts.Kinds.Any(BoosterKinds.IsStart))
            {
                throw new ArgumentException("Only a booster used during play has an opening gift.", nameof(unlockGifts));
            }

            ForPaintings = forPaintings;
            Daily = daily;
            LastDailyOn = lastDailyOn;
            UnlockGifts = unlockGifts;
        }

        /// <summary>No reward yet: a new player.</summary>
        public static RewardsGiven None => default;

        /// <summary>Rewards given for completed paintings.</summary>
        public int ForPaintings { get; }

        /// <summary>Daily rewards given.</summary>
        public int Daily { get; }

        /// <summary>The day the latest daily reward was given; absent when none was.</summary>
        public DayStamp? LastDailyOn { get; }

        /// <summary>The boosters used during play whose opening gift was given (<see cref="BoosterUnlocks"/>).</summary>
        public BoosterKindSet UnlockGifts { get; }

        public RewardsGiven WithOneForAPainting() => new RewardsGiven(ForPaintings + 1, Daily, LastDailyOn, UnlockGifts);

        public RewardsGiven WithTheDailyOf(DayStamp today) => new RewardsGiven(ForPaintings, Daily + 1, today, UnlockGifts);

        /// <exception cref="ArgumentException">The booster is a start booster, which has no opening gift.</exception>
        public RewardsGiven WithTheUnlockGiftOf(BoosterKind kind) => new RewardsGiven(ForPaintings, Daily, LastDailyOn, UnlockGifts.With(kind));

        public bool Equals(RewardsGiven other) =>
            ForPaintings == other.ForPaintings && Daily == other.Daily && LastDailyOn == other.LastDailyOn && UnlockGifts == other.UnlockGifts;

        public override bool Equals(object? obj) => obj is RewardsGiven other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ForPaintings, Daily, LastDailyOn, UnlockGifts);

        public override string ToString() => $"{ForPaintings} painting rewards, {Daily} daily rewards, opening gifts of {UnlockGifts}";

        public static bool operator ==(RewardsGiven left, RewardsGiven right) => left.Equals(right);

        public static bool operator !=(RewardsGiven left, RewardsGiven right) => !left.Equals(right);
    }

    /// <summary>
    /// Everything a player owns and has used up around the levels: coins, lives, boosters, the ads of the day and
    /// the rewards counted so far. A wallet never changes; every rule that pays or charges gives a new one, so a
    /// refused purchase leaves the player's wallet the very value it was. Two wallets that hold the same are equal.
    /// </summary>
    public sealed class Wallet : IEquatable<Wallet>
    {
        public Wallet(Coins coins, LifeMeter lives, BoosterShelf shelf, AdsWatched ads, RewardsGiven rewards)
        {
            Coins = coins;
            Lives = lives;
            Shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            Ads = ads;
            Rewards = rewards;
        }

        /// <summary>Nothing owned, every life there, nothing used: what a rule adds a new player's start to.</summary>
        public static Wallet Empty { get; } = new Wallet(Coins.None, LifeMeter.Full, BoosterShelf.Empty, AdsWatched.None, RewardsGiven.None);

        public Coins Coins { get; }

        public LifeMeter Lives { get; }

        /// <summary>The boosters the player holds.</summary>
        public BoosterShelf Shelf { get; }

        public AdsWatched Ads { get; }

        public RewardsGiven Rewards { get; }

        public Wallet WithCoins(Coins coins) => new Wallet(coins, Lives, Shelf, Ads, Rewards);

        public Wallet WithLives(LifeMeter lives) => new Wallet(Coins, lives, Shelf, Ads, Rewards);

        public Wallet WithShelf(BoosterShelf shelf) => new Wallet(Coins, Lives, shelf, Ads, Rewards);

        public Wallet WithAds(AdsWatched ads) => new Wallet(Coins, Lives, Shelf, ads, Rewards);

        public Wallet WithRewards(RewardsGiven rewards) => new Wallet(Coins, Lives, Shelf, Ads, rewards);

        public bool Equals(Wallet? other) =>
            other is not null && Coins == other.Coins && Lives == other.Lives && Shelf.Equals(other.Shelf) && Ads == other.Ads && Rewards == other.Rewards;

        public override bool Equals(object? obj) => Equals(obj as Wallet);

        public override int GetHashCode() => HashCode.Combine(Coins, Lives, Shelf, Ads, Rewards);

        public override string ToString() => $"{Coins} coins, lives {Lives}, {Shelf}";
    }
}
