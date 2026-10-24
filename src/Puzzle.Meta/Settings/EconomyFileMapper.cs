using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Meta.Settings
{
    /// <summary>The packed economy settings are malformed or contradict themselves; the message names the field and the problem.</summary>
    public sealed class EconomyFormatException : Exception
    {
        public EconomyFormatException(string message)
            : base(message)
        {
        }

        public EconomyFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Turns the raw <see cref="EconomyFile"/> into checked <see cref="EconomyRules"/>. No number has a default
    /// here: what the file does not state in a usable way is refused, by the name the settings file gives the field.
    /// </summary>
    public static class EconomyFileMapper
    {
        /// <summary>The settings' word for an ad that is only simulated.</summary>
        public const string SimulatedAdsWord = "simulated";

        /// <summary>The settings' word for a build that sells nothing for money.</summary>
        public const string NoRealMoneyWord = "notAvailable";

        /// <summary>The settings' word for a build that sells the store's products through the platform's store.</summary>
        public const string StoreWord = "store";

        /// <summary>The starter pack's name, as the economy simulator's reports call it.</summary>
        public const string StarterPackName = "starter-pack";

        /// <summary>The most a product may cost in the US store, in cents: the settings file's own bound.</summary>
        public const long LargestPriceCents = 99_999;

        /// <summary>
        /// No amount of coins in the settings may be above this, the same bound the economy simulator holds its
        /// settings to: far above any price a game has, far below what whole-number arithmetic holds.
        /// </summary>
        public const long LargestAmount = 1_000_000;

        /// <exception cref="EconomyFormatException">The file is not a set of economy settings this code can play by.</exception>
        public static EconomyRules ToRules(EconomyFile file)
        {
            if (file.formatVersion != EconomyFile.CurrentFormatVersion)
            {
                throw new EconomyFormatException($"The economy settings are in format version {file.formatVersion}; this code reads version {EconomyFile.CurrentFormatVersion}.");
            }

            WinReward win = new WinReward(
                CoinsOf(file.coins.perMoveLeftInFinale, "coins.perMoveLeftInFinale"),
                CoinsOf(file.coins.perLevelWon, "coins.perLevelWon"));
            RealMoneySales realMoney = RealMoneyOf(file.build);
            return new EconomyRules(
                lives: new LifeRules(
                    AtLeast(file.lives.max, 1, "lives.max"),
                    TimeSpan.FromMinutes(AtLeast(file.lives.minutesPerLife, 1, "lives.minutesPerLife"))),
                win: win,
                paintingCompleted: new PaintingReward(RotationOf(
                    file.rewards.paintingCompleted.coins, file.rewards.paintingCompleted.boostersEachTime, file.rewards.paintingCompleted.boosterRotation, "rewards.paintingCompleted")),
                daily: new DailyReward(
                    RotationOf(file.rewards.daily.coins, file.rewards.daily.boostersEachTime, file.rewards.daily.boosterRotation, "rewards.daily"),
                    AtLeast(file.rewards.daily.everyDays, 1, "rewards.daily.everyDays")),
                extraMoves: ExtraMovesOf(file.extraMovesOffer, win),
                startBoosters: StartBoostersOf(file.startBoosters),
                hints: HintsOf(file.hints),
                shop: ShopOf(file, win, realMoney),
                startingWallet: StartingWalletOf(file.start),
                unlocks: UnlocksOf(file.boosterUnlocks),
                ads: AdsOf(file.build));
        }

        private static RewardRotation RotationOf(long coins, int boostersEachTime, string[]? rotation, string field)
        {
            BoosterKind[] boosters = ListOrNone(rotation).Select(name => BoosterOf(name, $"{field}.boosterRotation")).ToArray();
            int each = AtLeast(boostersEachTime, 0, $"{field}.boostersEachTime");
            if (each > 0 && boosters.Length == 0)
            {
                throw Invalid($"{field}.boosterRotation is empty, so {field}.boostersEachTime must be 0");
            }

            return new RewardRotation(CoinsOf(coins, $"{field}.coins"), each, boosters);
        }

        /// <summary>A price the finale pays back is no price: the moves an offer adds would return more coins than they cost.</summary>
        private static ExtraMovesRules ExtraMovesOf(ExtraMovesOfferFile file, WinReward win)
        {
            Coins price = CoinsOf(file.priceCoins, "extraMovesOffer.priceCoins");
            int movesLeftAtBest = ExtraMovesOffer.Moves - 1;
            Coins paidBack = win.PerMoveLeft * movesLeftAtBest;
            if (price <= paidBack)
            {
                throw Invalid(
                    $"extraMovesOffer.priceCoins {price} must be above {paidBack}: a win on the first of the {ExtraMovesOffer.Moves} bought moves leaves {movesLeftAtBest}, and the finale would pay the purchase back");
            }

            return new ExtraMovesRules(AtLeast(file.freeAdsPerDay, 0, "extraMovesOffer.freeAdsPerDay"), price);
        }

        private static StartBoosterRules StartBoostersOf(StartBoostersFile file)
        {
            if (file.perAttempt < 1 || file.perAttempt > StartBoosterRules.KindsOfStartBooster)
            {
                throw Invalid($"startBoosters.perAttempt {file.perAttempt} must be 1 to {StartBoosterRules.KindsOfStartBooster}: each kind of start booster is taken once");
            }

            return new StartBoosterRules(file.perAttempt);
        }

        /// <summary>A hint waits a second or more, an attempt shows none or more, and the bot it shows the move of is named plainly.</summary>
        private static HintRules HintsOf(HintsFile file)
        {
            if (!IsPlainName(file.bot))
            {
                throw Invalid($"hints.bot '{file.bot}' names no bot; a player model is named like \"casual\" or \"skill-60\"");
            }

            return new HintRules(
                TimeSpan.FromSeconds(AtLeast(file.idleSeconds, 1, "hints.idleSeconds")),
                AtLeast(file.perAttempt, 0, "hints.perAttempt"),
                file.bot);
        }

        private static Shop ShopOf(EconomyFile file, WinReward win, RealMoneySales realMoney)
        {
            ShopFile shop = file.shop;
            return new Shop(
                BoosterPricesOf(file.prices, win),
                Named(ListOrNone(shop.boosterSets), set => set.name, "shop.boosterSets").Select(SetOf).ToArray(),
                StoreOf(file.store),
                new AdForCoins(CoinsOf(shop.adForCoins.coins, "shop.adForCoins.coins"), AtLeast(shop.adForCoins.perDay, 0, "shop.adForCoins.perDay")),
                realMoney);
        }

        private static Dictionary<BoosterKind, Coins> BoosterPricesOf(PricesFile file, WinReward win)
        {
            const string Field = "prices.boosters";
            var prices = new Dictionary<BoosterKind, Coins>();
            foreach (BoosterPriceFile entry in ListOrNone(file.boosters))
            {
                BoosterKind kind = BoosterOf(entry.booster, Field);
                if (prices.ContainsKey(kind))
                {
                    throw Invalid($"{Field} states '{entry.booster}' twice");
                }

                prices[kind] = CoinsOf(entry.coins, $"{Field}.{entry.booster}");
            }

            RequireEveryBooster(prices.Keys, Field);
            int addedMoves = RuleSet.Default.StartBoosterExtraMoves;
            Coins paidBack = win.PerMoveLeft * addedMoves;
            Coins extraMoves = prices[BoosterKind.StartExtraMoves];
            if (extraMoves <= paidBack)
            {
                throw Invalid(
                    $"{Field}.{BoosterKind.StartExtraMoves.Name()} {extraMoves} must be above {paidBack}: a win leaves the {addedMoves} added moves, and the finale would pay the purchase back");
            }

            return prices;
        }

        private static BoosterSet SetOf(BoosterSetFile file)
        {
            string field = $"shop.boosterSets.{file.name}";
            Dictionary<BoosterKind, int> boosters = CountsOf(file.boosters, $"{field}.boosters", leastCount: 1);
            if (boosters.Count == 0)
            {
                throw Invalid($"{field}.boosters is empty: a set gives at least one booster");
            }

            return new BoosterSet(file.name, CoinsOf(AtLeast(file.priceCoins, 1L, $"{field}.priceCoins"), $"{field}.priceCoins"), ShelfOf(boosters));
        }

        /// <summary>
        /// What the game sells for real money: at least one coin pack, and the starter pack, each with a price, coins
        /// and an id of its own that both stores take. The pricing rules between them are the economy simulator's
        /// balance policy, not the format's.
        /// </summary>
        private static StoreCatalogue StoreOf(StoreFile? file)
        {
            StoreFile store = file ?? new StoreFile();
            StoreProduct[] packs = Named(ListOrNone(store.coinPacks), pack => pack.name, "store.coinPacks")
                .Select(pack => ProductOf(
                    pack.name, StoreProductKind.CoinPack, pack.productIdSuffix, pack.priceUsdCents, pack.coins, BoosterShelf.Empty, $"store.coinPacks.{pack.name}"))
                .ToArray();
            if (packs.Length == 0)
            {
                throw Invalid("store.coinPacks is empty: the store sells at least one pack of coins");
            }

            const string Starter = "store.starterPack";
            StarterPackFile starter = store.starterPack ?? new StarterPackFile();
            Dictionary<BoosterKind, int> boosters = CountsOf(starter.boosters, $"{Starter}.boosters", leastCount: 1);
            StoreProduct starterProduct = ProductOf(
                StarterPackName, StoreProductKind.StarterPack, starter.productIdSuffix, starter.priceUsdCents, starter.coins, ShelfOf(boosters), Starter);
            var starterPack = new StarterPack(
                starterProduct, AtLeast(starter.timesPerPlayer, 1, $"{Starter}.timesPerPlayer"), AtLeast(starter.fromLevel, 1, $"{Starter}.fromLevel"));

            string? repeated = packs.Append(starterProduct).GroupBy(product => product.IdSuffix).FirstOrDefault(sameId => sameId.Count() > 1)?.Key;
            if (repeated != null)
            {
                throw Invalid($"store: two products have the product id suffix '{repeated}'; a store knows a product by its id alone");
            }

            return new StoreCatalogue(packs, starterPack);
        }

        private static StoreProduct ProductOf(string name, StoreProductKind kind, string? idSuffix, long priceUsdCents, long coins, BoosterShelf boosters, string field)
        {
            if (!IsProductIdSuffix(idSuffix))
            {
                throw Invalid(
                    $"{field}.productIdSuffix '{idSuffix}' must be lowercase letters, digits, '_' and '.', begin and end with a letter or a digit and hold no two dots together: what both stores take in a product id");
            }

            if (priceUsdCents < 1 || priceUsdCents > LargestPriceCents)
            {
                throw Invalid($"{field}.priceUsdCents {priceUsdCents} must be a price in US cents from 1 to {LargestPriceCents}");
            }

            return new StoreProduct(name, idSuffix!, kind, new UsCents(priceUsdCents), CoinsOf(AtLeast(coins, 1L, $"{field}.coins"), $"{field}.coins"), boosters);
        }

        /// <summary>The end of a product id both stores take: lowercase letters, digits, '_' and '.', a letter or a digit at each end, no two dots together.</summary>
        public static bool IsProductIdSuffix(string? suffix) =>
            !string.IsNullOrEmpty(suffix)
            && suffix.All(letter => IsLowerLetterOrDigit(letter) || letter == '_' || letter == '.')
            && IsLowerLetterOrDigit(suffix[0])
            && IsLowerLetterOrDigit(suffix[suffix.Length - 1])
            && !suffix.Contains("..");

        private static bool IsLowerLetterOrDigit(char letter) => (letter >= 'a' && letter <= 'z') || (letter >= '0' && letter <= '9');

        private static Wallet StartingWalletOf(StartFile file)
        {
            const string Field = "start.boosters";
            Dictionary<BoosterKind, int> boosters = CountsOf(file.boosters, Field, leastCount: 0);
            RequireEveryBooster(boosters.Keys, Field);
            return Wallet.Empty.WithCoins(CoinsOf(file.coins, "start.coins")).WithShelf(ShelfOf(boosters));
        }

        /// <summary>
        /// The levels the boosters used during play open on and their gifts: each of the four stated once in both, on a
        /// level from 1 and with none or more of its own kind; a start booster, open from the first level on, in neither.
        /// </summary>
        private static BoosterUnlocks UnlocksOf(BoosterUnlocksFile file)
        {
            const string LevelsField = "boosterUnlocks.levels";
            const string GiftField = "boosterUnlocks.gift";
            var levels = new Dictionary<BoosterKind, int>();
            foreach (BoosterLevelFile entry in ListOrNone(file.levels))
            {
                BoosterKind kind = InLevelBoosterOf(entry.booster, LevelsField);
                if (levels.ContainsKey(kind))
                {
                    throw Invalid($"{LevelsField} states '{entry.booster}' twice");
                }

                levels[kind] = AtLeast(entry.level, 1, $"{LevelsField}.{entry.booster}");
            }

            Dictionary<BoosterKind, int> gift = CountsOf(file.gift, GiftField, leastCount: 0);
            foreach (BoosterKind kind in gift.Keys)
            {
                InLevelBoosterOf(kind.Name(), GiftField);
            }

            RequireEveryInLevelBooster(levels.Keys, LevelsField);
            RequireEveryInLevelBooster(gift.Keys, GiftField);
            return new BoosterUnlocks(levels, ShelfOf(gift));
        }

        private static BoosterKind InLevelBoosterOf(string name, string field)
        {
            BoosterKind kind = BoosterOf(name, field);
            return kind.IsInLevel()
                ? kind
                : throw Invalid($"{field} names '{name}', a start booster, which is open from the first level on ({BoosterNamesListed(BoosterKinds.InLevel)} open on a level)");
        }

        private static void RequireEveryInLevelBooster(IEnumerable<BoosterKind> stated, string field)
        {
            BoosterKind[] missing = BoosterKinds.InLevel.Except(stated).ToArray();
            if (missing.Length > 0)
            {
                throw Invalid($"{field} must state every booster used during play; it lacks {BoosterNamesListed(missing)}");
            }
        }

        /// <summary>How many of each booster a list of names and counts states: no kind twice, no count below <paramref name="leastCount"/>.</summary>
        private static Dictionary<BoosterKind, int> CountsOf(BoosterCountFile[]? stated, string field, int leastCount)
        {
            var counts = new Dictionary<BoosterKind, int>();
            foreach (BoosterCountFile entry in ListOrNone(stated))
            {
                BoosterKind kind = BoosterOf(entry.booster, field);
                if (counts.ContainsKey(kind))
                {
                    throw Invalid($"{field} states '{entry.booster}' twice");
                }

                counts[kind] = AtLeast(entry.count, leastCount, $"{field}.{entry.booster}");
            }

            return counts;
        }

        private static BoosterShelf ShelfOf(Dictionary<BoosterKind, int> counts)
        {
            BoosterShelf shelf = BoosterShelf.Empty;
            foreach (KeyValuePair<BoosterKind, int> held in counts)
            {
                shelf = shelf.With(held.Key, held.Value);
            }

            return shelf;
        }

        private static AdSource AdsOf(BuildFile build)
        {
            switch (build.ads)
            {
                case SimulatedAdsWord:
                    return AdSource.Simulated;
                default:
                    throw Invalid($"build.ads is \"{build.ads}\"; this build can only show an ad that is \"{SimulatedAdsWord}\"");
            }
        }

        private static RealMoneySales RealMoneyOf(BuildFile build)
        {
            switch (build.realMoneyPurchases)
            {
                case NoRealMoneyWord:
                    return RealMoneySales.NotAvailable;
                case StoreWord:
                    return RealMoneySales.ThroughTheStore;
                default:
                    throw Invalid(
                        $"build.realMoneyPurchases is \"{build.realMoneyPurchases}\"; a build sells nothing for money (\"{NoRealMoneyWord}\") or sells the store's products through the platform's store (\"{StoreWord}\")");
            }
        }

        /// <summary>The entries of a list that stands for an object keyed by name: every name is there, plain and used once.</summary>
        private static T[] Named<T>(T[] entries, Func<T, string> nameOf, string field)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (T entry in entries)
            {
                string name = nameOf(entry);
                if (!IsPlainName(name))
                {
                    throw Invalid($"{field} has an entry named '{name}'; a name is letters, digits and '-'");
                }

                if (!names.Add(name))
                {
                    throw Invalid($"{field} states '{name}' twice");
                }
            }

            return entries;
        }

        /// <summary>A name the settings can use as a field: it is written into paths, where a dot would split it.</summary>
        private static bool IsPlainName(string? name) =>
            !string.IsNullOrEmpty(name) && name.All(letter => (letter >= 'a' && letter <= 'z') || (letter >= 'A' && letter <= 'Z') || (letter >= '0' && letter <= '9') || letter == '-');

        private static BoosterKind BoosterOf(string name, string field) =>
            BoosterKinds.Find(name) ?? throw Invalid($"{field} names '{name}', which is no booster ({BoosterNamesListed(BoosterKinds.All)})");

        private static void RequireEveryBooster(IEnumerable<BoosterKind> stated, string field)
        {
            BoosterKind[] missing = BoosterKinds.All.Except(stated).ToArray();
            if (missing.Length > 0)
            {
                throw Invalid($"{field} must state every booster; it lacks {BoosterNamesListed(missing)}");
            }
        }

        private static Coins CoinsOf(long amount, string field)
        {
            if (amount < 0 || amount > LargestAmount)
            {
                throw Invalid($"{field} {amount} must be an amount of coins from 0 to {LargestAmount}");
            }

            return new Coins(amount);
        }

        private static T AtLeast<T>(T value, T least, string field)
            where T : IComparable<T>
        {
            if (value.CompareTo(least) < 0)
            {
                throw Invalid($"{field} {value} must be at least {least}");
            }

            return value;
        }

        /// <summary>A JSON reader hands over nothing where the document writes null for a list; that reads as an empty list.</summary>
        private static T[] ListOrNone<T>(T[]? list) => list ?? Array.Empty<T>();

        private static string BoosterNamesListed(IEnumerable<BoosterKind> boosters) => string.Join(", ", boosters.Select(BoosterKinds.Name));

        private static EconomyFormatException Invalid(string message) => new EconomyFormatException($"Invalid economy settings: {message}.");
    }
}
