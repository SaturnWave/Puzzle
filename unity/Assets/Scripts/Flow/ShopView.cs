#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>What the hall's gift frame shows of the daily reward at one instant. A closed set (private constructor).</summary>
    public abstract class GiftView
    {
        private GiftView()
        {
        }

        /// <summary>A reward waits to be taken, and holds this.</summary>
        public sealed class Due : GiftView
        {
            public Due(Reward next)
            {
                Next = next ?? throw new ArgumentNullException(nameof(next));
            }

            public Reward Next { get; }

            public override string ToString() => $"due: {Next}";
        }

        /// <summary>Today's reward was taken; the next is due this long from now, at the player's midnight.</summary>
        public sealed class Waiting : GiftView
        {
            public Waiting(TimeSpan left)
            {
                Left = left;
            }

            public TimeSpan Left { get; }

            public override string ToString() => $"the next in {Left}";
        }
    }

    /// <summary>A set of boosters as the shop offers it to a player: whether their coins cover it, and if not, by how much they fall short.</summary>
    public sealed class SetOffer
    {
        public SetOffer(BoosterSet set, Coins coins)
        {
            Set = set ?? throw new ArgumentNullException(nameof(set));
            Missing = coins.ShortOf(set.Price);
        }

        public BoosterSet Set { get; }

        /// <summary>What the player lacks for the set; none when their coins cover it.</summary>
        public Coins Missing { get; }

        public bool IsCovered => Missing.IsNone;

        /// <summary>What the set holds, by kind, in the engine's order: the discs of its row.</summary>
        public IReadOnlyList<(BoosterKind Kind, int Count)> Boosters =>
            BoosterKinds.All.Where(Set.Boosters.Has).Select(kind => (kind, Set.Boosters.Count(kind))).ToArray();
    }

    /// <summary>
    /// What the shop and the hall's gift frame show at one instant, by the economy's rules: the packs of coins (which
    /// a test build shows and does not sell), the sets with whether the player's coins cover each, the ad for coins
    /// with how many are left on the player's calendar day, and whether the daily reward waits or when the next one
    /// comes. Every number comes from the settings through the rules; nothing here is a price of its own. Whoever
    /// owns the wallet makes it anew at every step, so a new day opens the ads and the gift as the clock passes
    /// midnight. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class ShopView
    {
        private ShopView(
            IReadOnlyList<CoinPack> coinPacks, IReadOnlyList<SetOffer> sets, Coins coinsForAnAd, int adsLeftToday, int adsPerDay, AdSource ads,
            RealMoneySales realMoney, GiftView gift, int giftEveryDays)
        {
            GiftEveryDays = giftEveryDays;
            CoinPacks = coinPacks;
            Sets = sets;
            CoinsForAnAd = coinsForAnAd;
            AdsLeftToday = adsLeftToday;
            AdsPerDay = adsPerDay;
            Ads = ads;
            RealMoney = realMoney;
            Gift = gift;
        }

        /// <summary>The packs of coins, in the order the settings list them.</summary>
        public IReadOnlyList<CoinPack> CoinPacks { get; }

        /// <summary>Whether a pack of coins can be bought at all in this build.</summary>
        public RealMoneySales RealMoney { get; }

        /// <summary>The sets, in the order the settings list them.</summary>
        public IReadOnlyList<SetOffer> Sets { get; }

        /// <summary>The coins one watched ad gives.</summary>
        public Coins CoinsForAnAd { get; }

        /// <summary>How many more ads give coins on the player's calendar day.</summary>
        public int AdsLeftToday { get; }

        public int AdsPerDay { get; }

        public bool CanWatchAnAd => AdsLeftToday > 0;

        /// <summary>Where this build's ads come from: the simulated one of a test build.</summary>
        public AdSource Ads { get; }

        public GiftView Gift { get; }

        /// <summary>A daily reward is due this many days after the one before it.</summary>
        public int GiftEveryDays { get; }

        /// <summary>The shop and the gift as the rules have them for a wallet at an instant of the player's clock.</summary>
        /// <param name="now">The player's time, with the offset of their time zone: a calendar day is theirs.</param>
        /// <exception cref="ArgumentNullException">No rules or no wallet.</exception>
        public static ShopView Of(EconomyRules rules, Wallet wallet, DateTimeOffset now)
        {
            Shop shop = (rules ?? throw new ArgumentNullException(nameof(rules))).Shop;
            Wallet held = wallet ?? throw new ArgumentNullException(nameof(wallet));
            DayStamp today = DayStamp.Of(now);
            SetOffer[] sets = shop.BoosterSets.Select(set => new SetOffer(set, held.Coins)).ToArray();
            return new ShopView(
                shop.CoinPacks, sets, shop.AdForCoins.Coins, shop.AdsForCoinsLeft(held, today), shop.AdForCoins.PerDay, rules.Ads, shop.RealMoney,
                GiftOf(rules.Daily, held, now), rules.Daily.EveryDays);
        }

        /// <summary>
        /// The daily reward as the frame shows it: due, or waiting until the player's midnight of the day it is due.
        /// A date set back is waited out to the day of the latest reward and one more: the rules open no day twice.
        /// </summary>
        private static GiftView GiftOf(DailyReward daily, Wallet wallet, DateTimeOffset now)
        {
            if (!daily.IsDueOn(wallet, DayStamp.Of(now)) && daily.DueOn(wallet) is DayStamp due)
            {
                var dueMidnight = new DateTimeOffset(new DateTime(due.Number * TimeSpan.TicksPerDay), now.Offset);
                return new GiftView.Waiting(dueMidnight - now);
            }

            return new GiftView.Due(daily.NextFor(wallet));
        }
    }
}
