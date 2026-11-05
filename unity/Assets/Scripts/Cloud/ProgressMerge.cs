#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Cloud
{
    /// <summary>What merging a phone's progress with the cloud's came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class MergeOutcome
    {
        private MergeOutcome()
        {
        }

        /// <summary>The two made one.</summary>
        public sealed class Merged : MergeOutcome
        {
            /// <param name="countedOnce">What both sides had been given since the base and is counted once, a line each, for the log; none for most merges.</param>
            public Merged(PlayerProgress progress, IReadOnlyList<string> countedOnce)
            {
                Progress = progress ?? throw new ArgumentNullException(nameof(progress));
                CountedOnce = countedOnce ?? throw new ArgumentNullException(nameof(countedOnce));
            }

            public PlayerProgress Progress { get; }

            public IReadOnlyList<string> CountedOnce { get; }
        }

        /// <summary>
        /// Both sides delivered payments that the ledger or the store's catalogue cannot name, so what to count once
        /// is not known: nothing is merged and nothing written until they can be (the ledger read whole, or a game
        /// that sells the product). Guessing would make or lose a player's coins.
        /// </summary>
        public sealed class Unaccounted : MergeOutcome
        {
            public Unaccounted(IReadOnlyList<LedgerNumber> payments)
            {
                Payments = payments ?? throw new ArgumentNullException(nameof(payments));
            }

            /// <summary>The ledger numbers in question, at most a few.</summary>
            public IReadOnlyList<LedgerNumber> Payments { get; }
        }
    }

    /// <summary>
    /// The three-way merge of a player's progress (docs/research/server-v1.md §3.5): the <b>base</b> is the cloud's
    /// save as this phone last synced it, the <b>local</b> progress is the phone's now, the <b>cloud</b> is the save
    /// just read. When the cloud is still the base (or the very local progress), the local one stands; when the local
    /// one is still the base, the cloud's stands. When both moved on, they are merged field by field:
    /// <list type="bullet">
    /// <item>Levels won: the more. Attempts on the level: the more on the same level, else those of the side further on
    /// (the level's seed pool must not hand out a seed again, §17).</item>
    /// <item>Pieces in place, steps of the tutorial seen, the boosters' opening gifts: every one either side has.</item>
    /// <item>Coins and each booster: the cloud's, plus what the phone gained or spent since the base, never below none,
    /// so what two phones earned adds up. A gain the rules give once and both phones were given since the base is
    /// counted once, its contents taken back out of the phone's side: a payment both delivered (the same number in the
    /// ledger), a booster's opening gift, a day's daily reward, a painting's reward. The welcome gift needs no such
    /// care: a new installation's base is a new player, who holds it already.</item>
    /// <item>Lives: the phone's, short-lived as they are, so two phones never make more of them.</item>
    /// <item>The day's ads: the more of each on the same day, else the later day's counts.</item>
    /// <item>Rewards counted: as many as were given, a painting or a day both phones were rewarded for counted once,
    /// never fewer than either side counts; the latest daily reward's day is the later.</item>
    /// <item>Purchases: each product's count as the coins (the cloud's, plus the phone's since the base, a payment both
    /// delivered counted once), never fewer than either side's nor than the ledger's numbers of it; the stores'
    /// payment ids of both (the latest 64); the ledger numbers of both, never cut short.</item>
    /// </list>
    /// The rules are the economy's (what a gift, a reward and a product hold); the ledger names what a payment
    /// delivered on both phones held. Nothing here reads a clock or the network.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class ProgressMerge
    {
        /// <summary>How many unaccounted payments a merge names at most: enough for the log.</summary>
        private const int UnaccountedNamed = 8;

        private readonly EconomyRules _rules;

        public ProgressMerge(EconomyRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <param name="baseProgress">The cloud's progress as this phone last synced it; a new player's when it never did (a new installation, another account).</param>
        /// <param name="local">The phone's progress now.</param>
        /// <param name="cloud">The cloud's progress now.</param>
        /// <param name="ledger">The player's ledger of verified payments as read with the cloud's save.</param>
        public MergeOutcome Merge(PlayerProgress baseProgress, PlayerProgress local, PlayerProgress cloud, PurchaseLedger ledger)
        {
            if (cloud.Equals(baseProgress) || cloud.Equals(local))
            {
                return new MergeOutcome.Merged(local, Array.Empty<string>());
            }

            if (local.Equals(baseProgress))
            {
                return new MergeOutcome.Merged(cloud, Array.Empty<string>());
            }

            var sides = new Sides(baseProgress, local, cloud);
            LedgerNumbers deliveredOnBoth = sides.NewOnBoth(progress => progress.Purchases.LedgerDelivered);
            var payments = new List<StoreProduct>();
            var unaccounted = new List<LedgerNumber>();
            foreach (LedgerNumber number in deliveredOnBoth.Numbers.Take(ledger.Entries.Count + 1))
            {
                if (ledger.Find(number) is LedgerEntry entry && _rules.Shop.Store.Find(entry.Product) is StoreProduct product)
                {
                    payments.Add(product);
                }
                else
                {
                    unaccounted.Add(number);
                }
            }

            if (unaccounted.Count > 0 || deliveredOnBoth.Count > ledger.Entries.Count)
            {
                return new MergeOutcome.Unaccounted(unaccounted.Take(UnaccountedNamed).ToArray());
            }

            var countedOnce = new List<string>();
            var twice = new Gains();
            foreach (StoreProduct product in payments)
            {
                twice.Add(product.Coins, product.Boosters);
                countedOnce.Add($"a payment for {product.IdSuffix} delivered on both sides");
            }

            foreach (BoosterKind kind in sides.NewOnBoth(progress => progress.Wallet.Rewards.UnlockGifts).Kinds)
            {
                twice.Add(Coins.None, BoosterShelf.Empty.With(kind, _rules.Unlocks.Gift.Count(kind)));
                countedOnce.Add($"the opening gift of {kind.Name()} given on both sides");
            }

            RewardsGiven rewards = MergedRewards(sides, twice, countedOnce);
            PlayerProgress merged = new PlayerProgress(
                Math.Max(local.LevelsWon, cloud.LevelsWon),
                MergedAttempts(local, cloud),
                MergedPlaced(local, cloud),
                new Wallet(
                    MergedCoins(sides, twice),
                    local.Wallet.Lives,
                    MergedShelf(sides, twice),
                    MergedAds(local.Wallet.Ads, cloud.Wallet.Ads),
                    rewards),
                TutorialSeen.Of(local.Tutorial.Steps.Concat(cloud.Tutorial.Steps)),
                MergedPurchases(sides, payments, ledger));
            return new MergeOutcome.Merged(merged, countedOnce);
        }

        /// <summary>The attempts on the level the merged progress stands on: the more on the same level, else those of the side further on.</summary>
        private static int MergedAttempts(PlayerProgress local, PlayerProgress cloud) =>
            local.LevelsWon == cloud.LevelsWon ? Math.Max(local.AttemptsOnLevel, cloud.AttemptsOnLevel)
            : local.LevelsWon > cloud.LevelsWon ? local.AttemptsOnLevel
            : cloud.AttemptsOnLevel;

        /// <summary>Every piece either side has in place; a piece the levels won do not give (no game writes one) is left out.</summary>
        private static PlacedPieces MergedPlaced(PlayerProgress local, PlayerProgress cloud)
        {
            var won = new MuseumProgress(Math.Max(local.LevelsWon, cloud.LevelsWon), PlacedPieces.None);
            PlacedPieces placed = PlacedPieces.None;
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                foreach (int piece in local.Museum.Placed.In(painting).Union(cloud.Museum.Placed.In(painting)).Where(piece => piece < won.PiecesWonIn(painting)))
                {
                    placed = placed.With(painting, piece);
                }
            }

            return placed;
        }

        private static Coins MergedCoins(Sides sides, Gains twice) =>
            new Coins(Math.Max(0, sides.Cloud.Wallet.Coins.Amount + (sides.Local.Wallet.Coins.Amount - sides.Base.Wallet.Coins.Amount) - twice.Coins));

        private static BoosterShelf MergedShelf(Sides sides, Gains twice)
        {
            BoosterShelf shelf = BoosterShelf.Empty;
            foreach (BoosterKind kind in BoosterKinds.All)
            {
                int count = sides.Cloud.Wallet.Shelf.Count(kind) + (sides.Local.Wallet.Shelf.Count(kind) - sides.Base.Wallet.Shelf.Count(kind)) - twice.Boosters(kind);
                shelf = shelf.With(kind, Math.Max(0, count));
            }

            return shelf;
        }

        /// <summary>The day's ads: the more of each on the same day, else the later day's counts.</summary>
        private static AdsWatched MergedAds(AdsWatched local, AdsWatched cloud) =>
            local.Day == cloud.Day ? new AdsWatched(local.Day, Math.Max(local.ForExtraMoves, cloud.ForExtraMoves), Math.Max(local.ForCoins, cloud.ForCoins))
            : local.Day > cloud.Day ? local
            : cloud;

        /// <summary>
        /// The rewards counted: every reward given on either side since the base, a painting or a day both were rewarded
        /// for counted once (its contents, as the phone was given them, go into <paramref name="twice"/>), never fewer
        /// than either side counts; the boosters' opening gifts of both.
        /// </summary>
        private RewardsGiven MergedRewards(Sides sides, Gains twice, List<string> countedOnce)
        {
            RewardsGiven local = sides.Local.Wallet.Rewards;
            RewardsGiven cloud = sides.Cloud.Wallet.Rewards;
            RewardsGiven before = sides.Base.Wallet.Rewards;

            int paintingsSinceTheBase = Math.Min(Since(local.ForPaintings, before.ForPaintings), Since(cloud.ForPaintings, before.ForPaintings));
            int paintingsOnBoth = Math.Min(sides.PaintingsCompletedOnBoth(), paintingsSinceTheBase);
            for (int latest = 1; latest <= paintingsOnBoth; latest++)
            {
                Reward reward = _rules.PaintingCompleted.Rotation.Of(local.ForPaintings - latest);
                twice.Add(reward);
                countedOnce.Add($"a painting's reward ({reward}) given on both sides");
            }

            bool sameDailyRewardOnBoth = local.LastDailyOn is DayStamp day && cloud.LastDailyOn == day && before.LastDailyOn != day
                && local.Daily > before.Daily && cloud.Daily > before.Daily;
            if (sameDailyRewardOnBoth)
            {
                Reward reward = _rules.Daily.Rotation.Of(local.Daily - 1);
                twice.Add(reward);
                countedOnce.Add($"the daily reward of {local.LastDailyOn} ({reward}) given on both sides");
            }

            int forPaintings = Math.Max(
                Math.Max(local.ForPaintings, cloud.ForPaintings),
                before.ForPaintings + Since(local.ForPaintings, before.ForPaintings) + Since(cloud.ForPaintings, before.ForPaintings) - paintingsOnBoth);
            int daily = Math.Max(
                Math.Max(local.Daily, cloud.Daily),
                before.Daily + Since(local.Daily, before.Daily) + Since(cloud.Daily, before.Daily) - (sameDailyRewardOnBoth ? 1 : 0));
            DayStamp? lastDaily = Later(local.LastDailyOn, cloud.LastDailyOn);
            BoosterKindSet gifts = BoosterKindSet.Of(local.UnlockGifts.Kinds.Concat(cloud.UnlockGifts.Kinds));
            return new RewardsGiven(forPaintings, daily, lastDaily, gifts);
        }

        /// <summary>
        /// What was bought: each product's count as the coins count it, never fewer than either side's nor than the
        /// ledger's numbers of it the merged progress holds; the stores' payment ids of both, the cloud's first, the
        /// latest kept; the ledger numbers of both.
        /// </summary>
        private static PurchasesMade MergedPurchases(Sides sides, IReadOnlyList<StoreProduct> deliveredOnBoth, PurchaseLedger ledger)
        {
            PurchasesMade local = sides.Local.Purchases;
            PurchasesMade cloud = sides.Cloud.Purchases;
            PurchasesMade before = sides.Base.Purchases;
            LedgerNumbers numbers = local.LedgerDelivered.Union(cloud.LedgerDelivered);

            var bought = new List<(string Product, int Times)>();
            IEnumerable<string> products = local.Bought.Concat(cloud.Bought).Concat(before.Bought).Select(entry => entry.Product)
                .Concat(ledger.Entries.Where(entry => numbers.Contains(entry.Number)).Select(entry => entry.Product))
                .Distinct(StringComparer.Ordinal);
            foreach (string product in products.OrderBy(product => product, StringComparer.Ordinal))
            {
                int counted = cloud.TimesBought(product) + (local.TimesBought(product) - before.TimesBought(product))
                    - deliveredOnBoth.Count(payment => payment.IdSuffix == product);
                int inTheLedger = ledger.Entries.Count(entry => entry.Product == product && numbers.Contains(entry.Number));
                int times = Math.Max(Math.Max(counted, inTheLedger), Math.Max(local.TimesBought(product), cloud.TimesBought(product)));
                if (times > 0)
                {
                    bought.Add((product, times));
                }
            }

            string[] payments = cloud.Transactions.Concat(local.Transactions.Where(id => !cloud.HasSeen(id))).ToArray();
            return new PurchasesMade(bought, payments.Skip(Math.Max(0, payments.Length - PurchasesMade.TransactionsKept)), numbers);
        }

        /// <summary>How many a count grew by since the base; none when it is below it (a side that is no successor of the base, which no game writes).</summary>
        private static int Since(int now, int before) => Math.Max(0, now - before);

        private static DayStamp? Later(DayStamp? one, DayStamp? other) =>
            one is DayStamp a && other is DayStamp b ? (a > b ? a : b) : one ?? other;

        /// <summary>The three progresses a merge looks at.</summary>
        private sealed class Sides
        {
            public Sides(PlayerProgress baseProgress, PlayerProgress local, PlayerProgress cloud)
            {
                Base = baseProgress;
                Local = local;
                Cloud = cloud;
            }

            public PlayerProgress Base { get; }

            public PlayerProgress Local { get; }

            public PlayerProgress Cloud { get; }

            /// <summary>The ledger numbers both sides gained since the base.</summary>
            public LedgerNumbers NewOnBoth(Func<PlayerProgress, LedgerNumbers> numbers) =>
                numbers(Local).Except(numbers(Base)).Intersect(numbers(Cloud).Except(numbers(Base)));

            /// <summary>The boosters whose opening gift both sides were given since the base.</summary>
            public BoosterKindSet NewOnBoth(Func<PlayerProgress, BoosterKindSet> gifts) =>
                BoosterKindSet.Of(gifts(Local).Kinds.Where(kind => gifts(Cloud).Contains(kind) && !gifts(Base).Contains(kind)));

            /// <summary>How many paintings both sides completed since the base.</summary>
            public int PaintingsCompletedOnBoth() =>
                PaintingNumber.All.Count(painting => Local.Museum.IsComplete(painting) && Cloud.Museum.IsComplete(painting) && !Base.Museum.IsComplete(painting));
        }

        /// <summary>The contents of gains both sides were given, summed: what the merge takes back out of the phone's side.</summary>
        private sealed class Gains
        {
            private readonly int[] _boosters = new int[BoosterKinds.All.Count];

            public long Coins { get; private set; }

            public int Boosters(BoosterKind kind) => _boosters[(int)kind];

            public void Add(Reward reward)
            {
                Coins += reward.Coins.Amount;
                foreach (BoosterKind booster in reward.Boosters)
                {
                    _boosters[(int)booster]++;
                }
            }

            public void Add(Coins coins, BoosterShelf boosters)
            {
                Coins += coins.Amount;
                foreach (BoosterKind kind in BoosterKinds.All)
                {
                    _boosters[(int)kind] += boosters.Count(kind);
                }
            }
        }
    }
}
