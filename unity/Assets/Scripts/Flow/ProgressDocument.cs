#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// The saved progress as the file writes it:
    /// <c>{"version":6,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":4,"pieces":[0,1,2]}],"wallet":{...},"tutorial":["goal","match"],"purchases":{...}}</c>
    /// (the wallet's block is <see cref="WalletDocument"/>, the purchases' <see cref="PurchasesDocument"/>; the
    /// tutorial's steps seen are named as <see cref="TutorialSteps.NameOf"/> names them, in the order of the
    /// enumeration). The fields are public and named as the document names them, because that is how Unity's JSON
    /// reader maps a document. A line of the purchases' block that no game could have written is set aside on its
    /// own and the rest of the progress is read (<see cref="PurchasesDocument.Read"/>); anything else no game could
    /// have written makes the whole document damaged. Five earlier layouts are still read. Layout 1,
    /// written before the game had a museum, has no <c>placed</c>: it reads as progress with no jigsaw piece in place
    /// yet, so the pieces of every level won before then wait on their trays. Layouts 1 and 2 have no wallet: their
    /// player holds what the rules give a new player, with every life; the gifts of the boosters used during play
    /// that are open where the player stands are given to them the first time the game opens (Puzzle.Meta's
    /// BoosterUnlocks). Layout 3 keeps a wallet but not which of those gifts were given: its player was given all of
    /// them in the welcome gift before the first level, as the game did then, so all count as given and none comes a
    /// second time. Layouts 1 to 4 keep no tutorial: their player has seen every step of a level they have won (an
    /// obstacle's level being the first that has it), the jigsaw's first piece once a piece is in place and the first
    /// painting once one is whole, and nothing else (<see cref="TutorialPlan.OfAnOlderSave"/>). Layouts 1 to 5 keep no
    /// purchases: they were written before the game sold anything for money, so their player has bought nothing.
    /// Unity's reader makes 0 of a number a document does not have, and an empty list of a list it does not have, so
    /// whether the wallet, the gifts, the tutorial and the purchases are there is decided by the layout's number,
    /// never by a zero or an empty list.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class ProgressDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 6;

        /// <summary>The oldest layout a player can have on a device; this code reads it as it reads its own.</summary>
        private const int FirstVersion = 1;

        /// <summary>The first layout that keeps the player's wallet.</summary>
        private const int FirstVersionWithAWallet = 3;

        /// <summary>The first layout that keeps which boosters used during play have had their opening gift.</summary>
        private const int FirstVersionWithUnlockGifts = 4;

        /// <summary>The first layout that keeps which steps of the tutorial the player has seen.</summary>
        private const int FirstVersionWithTheTutorial = 5;

        /// <summary>The first layout that keeps what the player bought for real money.</summary>
        private const int FirstVersionWithPurchases = 6;

        public int version;
        public int levelsWon;
        public int attemptsOnLevel;

        /// <summary>The jigsaw pieces in place, one entry for every painting that has any; in the file since layout 2.</summary>
        public PlacedDocument[] placed = Array.Empty<PlacedDocument>();

        /// <summary>What the player owns around the levels; in the file since layout 3.</summary>
        public WalletDocument wallet = new WalletDocument();

        /// <summary>The steps of the tutorial the player has seen, by their names; in the file since layout 5.</summary>
        public string[] tutorial = Array.Empty<string>();

        /// <summary>What the player bought for real money and was given; in the file since layout 6.</summary>
        public PurchasesDocument purchases = new PurchasesDocument();

        /// <summary>The document that keeps a progress, in the current layout.</summary>
        public static ProgressDocument Of(PlayerProgress progress)
        {
            var placed = new List<PlacedDocument>();
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                IReadOnlyList<int> pieces = progress.Museum.Placed.In(painting);
                if (pieces.Count > 0)
                {
                    placed.Add(new PlacedDocument { painting = painting.Value, pieces = pieces.ToArray() });
                }
            }

            return new ProgressDocument
            {
                version = CurrentVersion,
                levelsWon = progress.LevelsWon,
                attemptsOnLevel = progress.AttemptsOnLevel,
                placed = placed.ToArray(),
                wallet = WalletDocument.Of(progress.Wallet),
                tutorial = progress.Tutorial.Steps.Select(TutorialSteps.NameOf).ToArray(),
                purchases = PurchasesDocument.Of(progress.Purchases),
            };
        }

        /// <summary>What a document that a JSON reader filled is, as far as this version of the game can tell.</summary>
        /// <param name="document">Nothing when the file's text held no document.</param>
        /// <param name="startingWallet">
        /// What the rules give a new player (Puzzle.Meta.EconomyRules.StartingWallet): the wallet of a player whose
        /// file is of a layout before the wallet.
        /// </param>
        public static ProgressReading Read(ProgressDocument? document, Wallet startingWallet)
        {
            if (document == null || document.version < FirstVersion)
            {
                return new ProgressReading.Damaged();
            }

            bool hasCounts = document.levelsWon >= 0 && document.attemptsOnLevel >= 0;
            PlacedPieces? placed = hasCounts ? PossiblePlacedPiecesOf(document) : null;
            Wallet? wallet = document.version < FirstVersionWithAWallet
                ? startingWallet
                : document.wallet?.PossibleWallet(givenAllInTheWelcomeGift: document.version < FirstVersionWithUnlockGifts);
            PurchasesReading? purchases = document.version < FirstVersionWithPurchases ? PurchasesReading.Nothing : document.purchases?.Read();
            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one, so what this game knows is looked for in them too:
                // the pieces, the wallet, the tutorial and the purchases if they still read as such, else at least
                // where the player stands. A step this game does not have is passed over: a later game may have more.
                PlayerProgress? understood = !hasCounts ? null
                    : new PlayerProgress(
                        document.levelsWon, document.attemptsOnLevel, placed ?? PlacedPieces.None, wallet ?? startingWallet, TutorialOfALaterGame(document),
                        purchases?.Made ?? PurchasesMade.None);
                return new ProgressReading.OfALaterGame(document.version, understood);
            }

            TutorialSeen? tutorial = placed == null ? null
                : document.version < FirstVersionWithTheTutorial ? TutorialPlan.OfAnOlderSave(new MuseumProgress(document.levelsWon, placed))
                : PossibleTutorialOf(document);
            return placed != null && wallet != null && tutorial != null && purchases != null
                ? new ProgressReading.Sound(new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed, wallet, tutorial, purchases.Made), purchases.SetAside)
                : (ProgressReading)new ProgressReading.Damaged();
        }

        /// <summary>The steps the document names as seen, each a step of the tutorial and named once; nothing otherwise.</summary>
        private static TutorialSeen? PossibleTutorialOf(ProgressDocument document)
        {
            TutorialSeen seen = TutorialSeen.None;
            foreach (string name in ListOrNone(document.tutorial))
            {
                if (!(TutorialSteps.Find(name ?? string.Empty) is TutorialStep step) || seen.Has(step))
                {
                    return null;
                }

                seen = seen.With(step);
            }

            return seen;
        }

        /// <summary>The steps of this game that a document of a later game names as seen.</summary>
        private static TutorialSeen TutorialOfALaterGame(ProgressDocument document) =>
            TutorialSeen.Of(ListOrNone(document.tutorial).Select(name => TutorialSteps.Find(name ?? string.Empty)).OfType<TutorialStep>());

        /// <summary>The pieces the document says are in place; nothing when it names a painting or a piece the museum does not have, a piece twice, or one the levels won have not given.</summary>
        private static PlacedPieces? PossiblePlacedPiecesOf(ProgressDocument document)
        {
            PlacedPieces placed = PlacedPieces.None;
            foreach (PlacedDocument entry in ListOrNone(document.placed))
            {
                if (entry.painting < 1 || entry.painting > MuseumPlan.PaintingCount)
                {
                    return null;
                }

                var painting = new PaintingNumber(entry.painting);
                foreach (int piece in ListOrNone(entry.pieces))
                {
                    bool isAPieceNotYetNamed = piece >= 0 && piece < painting.Size.PieceCount && !placed.IsPlaced(painting, piece);
                    if (!isAPieceNotYetNamed)
                    {
                        return null;
                    }

                    placed = placed.With(painting, piece);
                }
            }

            return MuseumProgress.IsPossible(document.levelsWon, placed) ? placed : null;
        }

        /// <summary>A JSON reader may hand over nothing for a list the document does not have, as layout 1 has no placed pieces.</summary>
        internal static T[] ListOrNone<T>(T[]? list) => list ?? Array.Empty<T>();
    }

    /// <summary>The pieces of one painting that are in place, as the file writes them.</summary>
    [Serializable]
    public sealed class PlacedDocument
    {
        /// <summary>The painting's number in the museum, from 1.</summary>
        public int painting;

        /// <summary>The numbers of its pieces that are in place, from 0.</summary>
        public int[] pieces = Array.Empty<int>();
    }

    /// <summary>
    /// The player's wallet as the file writes it (Puzzle.Meta.Wallet, field by field):
    /// <c>{"coins":1250,"livesShort":2,"nextLifeAtUnixSeconds":1792000800,"boosters":[{"name":"hit-row","count":3},...],
    /// "day":739537,"adsForMovesToday":1,"adsForCoinsToday":0,"paintingRewards":3,"dailyRewards":0,"lastDailyDay":0,
    /// "unlockGifts":["hit-cell","hit-row"]}</c>.
    /// Lives are kept as how many are missing, so a full meter is 0 and the time of the next life is 0 with it. An
    /// instant is kept in whole seconds since 1970 (UTC), which is what every clock of the game hands out
    /// (<see cref="IWallClock"/>); one between two seconds is written as the later second, so a wait is never cut
    /// short. A calendar day is kept as its number (Puzzle.Meta.DayStamp); the day of the latest daily reward is 0
    /// while none was given. Every booster of the game is written, by the name its records give it; the boosters used
    /// during play whose opening gift was given are named in <c>unlockGifts</c>, in the engine's order.
    /// </summary>
    [Serializable]
    public sealed class WalletDocument
    {
        public long coins;
        public int livesShort;
        public long nextLifeAtUnixSeconds;
        public BoosterCountDocument[] boosters = Array.Empty<BoosterCountDocument>();
        public int day;
        public int adsForMovesToday;
        public int adsForCoinsToday;
        public int paintingRewards;
        public int dailyRewards;
        public int lastDailyDay;

        /// <summary>The boosters used during play whose opening gift was given, by their record names; in the file since layout 4.</summary>
        public string[] unlockGifts = Array.Empty<string>();

        /// <summary>The block that keeps a wallet.</summary>
        public static WalletDocument Of(Wallet wallet) => new WalletDocument
        {
            coins = wallet.Coins.Amount,
            livesShort = wallet.Lives.ShortOfFull,
            nextLifeAtUnixSeconds = wallet.Lives.NextLifeAt is DateTimeOffset nextLifeAt ? UnixSecondsNoEarlierThan(nextLifeAt) : 0,
            boosters = BoosterKinds.All.Select(kind => new BoosterCountDocument { name = kind.Name(), count = wallet.Shelf.Count(kind) }).ToArray(),
            day = wallet.Ads.Day.Number,
            adsForMovesToday = wallet.Ads.ForExtraMoves,
            adsForCoinsToday = wallet.Ads.ForCoins,
            paintingRewards = wallet.Rewards.ForPaintings,
            dailyRewards = wallet.Rewards.Daily,
            lastDailyDay = wallet.Rewards.LastDailyOn?.Number ?? 0,
            unlockGifts = wallet.Rewards.UnlockGifts.Kinds.Select(BoosterKinds.Name).ToArray(),
        };

        /// <summary>
        /// The wallet the block holds; nothing when no wallet can be so: a negative amount or count, a time for a
        /// full meter or a day for a daily reward never given, a day or an instant outside the calendar, a booster
        /// this game does not have, one named twice, or one not named at all; an opening gift of a booster this game
        /// does not have, of a start booster, or named twice.
        /// </summary>
        /// <param name="givenAllInTheWelcomeGift">
        /// True for a block of a layout that kept no opening gifts (layout 3): its player was given every one of them
        /// in the welcome gift, and the block's <c>unlockGifts</c> are not looked at.
        /// </param>
        public Wallet? PossibleWallet(bool givenAllInTheWelcomeGift)
        {
            BoosterShelf? shelf = PossibleShelf();
            BoosterKindSet? gifts = givenAllInTheWelcomeGift ? BoosterKindSet.Of(BoosterKinds.InLevel) : PossibleUnlockGifts();
            bool statesNoTimeForAFullMeter = livesShort > 0 || nextLifeAtUnixSeconds == 0;
            bool statesNoDayWithoutADailyReward = dailyRewards > 0 || lastDailyDay == 0;
            if (shelf == null || !(gifts is BoosterKindSet given) || coins < 0 || !statesNoTimeForAFullMeter || !statesNoDayWithoutADailyReward)
            {
                return null;
            }

            try
            {
                // The wallet's own parts refuse every other thing the game cannot have written.
                return new Wallet(
                    new Coins(coins),
                    new LifeMeter(livesShort, livesShort > 0 ? DateTimeOffset.FromUnixTimeSeconds(nextLifeAtUnixSeconds) : (DateTimeOffset?)null),
                    shelf,
                    new AdsWatched(new DayStamp(day), adsForMovesToday, adsForCoinsToday),
                    new RewardsGiven(paintingRewards, dailyRewards, dailyRewards > 0 ? new DayStamp(lastDailyDay) : (DayStamp?)null, given));
            }
            catch (ArgumentException)
            {
                return null; // a negative count, or a day or an instant the calendar does not have
            }
        }

        /// <summary>The boosters the block names, each of the game's exactly once with a count of none or more; nothing otherwise.</summary>
        private BoosterShelf? PossibleShelf()
        {
            BoosterShelf shelf = BoosterShelf.Empty;
            var named = new HashSet<BoosterKind>();
            foreach (BoosterCountDocument entry in ProgressDocument.ListOrNone(boosters))
            {
                if (entry == null || !(BoosterKinds.Find(entry.name ?? string.Empty) is BoosterKind kind) || !named.Add(kind) || entry.count < 0)
                {
                    return null;
                }

                shelf = shelf.With(kind, entry.count);
            }

            return named.Count == BoosterKinds.All.Count ? shelf : null;
        }

        /// <summary>The boosters the block names as having had their opening gift: each a booster used during play, once; nothing otherwise.</summary>
        private BoosterKindSet? PossibleUnlockGifts()
        {
            BoosterKindSet given = BoosterKindSet.None;
            foreach (string name in ProgressDocument.ListOrNone(unlockGifts))
            {
                if (!(BoosterKinds.Find(name ?? string.Empty) is BoosterKind kind) || !kind.IsInLevel() || given.Contains(kind))
                {
                    return null;
                }

                given = given.With(kind);
            }

            return given;
        }

        /// <summary>An instant as whole seconds since 1970 (UTC); one between two seconds as the later of them.</summary>
        private static long UnixSecondsNoEarlierThan(DateTimeOffset instant)
        {
            long seconds = instant.ToUnixTimeSeconds();
            return DateTimeOffset.FromUnixTimeSeconds(seconds) < instant ? seconds + 1 : seconds;
        }
    }

    /// <summary>
    /// What the player bought for real money, as the file writes it (<see cref="PurchasesMade"/>):
    /// <c>{"bought":[{"product":"coins.pouch","times":2},{"product":"starter.apprentice_kit","times":1}],"transactions":["GPA.3301-...","GPA.3302-..."]}</c>.
    /// A product is named by the end of its id in the stores; the payments' ids are the latest delivered, the oldest
    /// first.
    /// </summary>
    [Serializable]
    public sealed class PurchasesDocument
    {
        public BoughtDocument[] bought = Array.Empty<BoughtDocument>();
        public string[] transactions = Array.Empty<string>();

        /// <summary>The block that keeps what was bought.</summary>
        public static PurchasesDocument Of(PurchasesMade purchases) => new PurchasesDocument
        {
            bought = purchases.Bought.Select(entry => new BoughtDocument { product = entry.Product, times = entry.Times }).ToArray(),
            transactions = purchases.Transactions.ToArray(),
        };

        /// <summary>
        /// What the block holds, line by line. A line no game could have written is set aside on its own, with a
        /// word on why, and the rest is read, so that one bad line (written by hand, or come back from a backup) does
        /// not cost the player their levels, pieces and wallet. What a line surely says is kept even then, because
        /// money hangs on it:
        /// <list type="bullet">
        /// <item>Every payment's id that is there is kept: an id the record holds is what keeps a payment the store
        /// tells of again from being given twice. An empty id names no payment, and is set aside; an id named twice is
        /// kept once; of more ids than the record keeps, the oldest are set aside, as the record lets them go itself.</item>
        /// <item>A product's line is written only once the product was given, so a line whose count is under 1 still
        /// says the product was bought once at least, and counts as once: the starter pack it names stays sold out,
        /// never sold a second time on a guess. A product named twice counts as the more of its two counts, for the
        /// same reason. A line with no product (or no line at all) says nothing that can be kept.</item>
        /// </list>
        /// </summary>
        public PurchasesReading Read()
        {
            var setAside = new List<string>();
            var made = new PurchasesMade(TimesBought(setAside), PaymentsDelivered(setAside));
            return new PurchasesReading(made, setAside);
        }

        /// <summary>How many times each product was bought, as far as the lines say it surely; the lines set aside go to the list.</summary>
        private IEnumerable<(string Product, int Times)> TimesBought(List<string> setAside)
        {
            var times = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (BoughtDocument? entry in ProgressDocument.ListOrNone(bought))
            {
                if (entry == null || string.IsNullOrEmpty(entry.product))
                {
                    setAside.Add($"a bought line with no product ({(entry == null ? "no line" : $"×{entry.times}")})");
                    continue;
                }

                int count = entry.times;
                if (count < 1)
                {
                    setAside.Add($"{entry.product} ×{entry.times}: a count under 1, counted as bought once");
                    count = 1;
                }

                if (times.TryGetValue(entry.product, out int before))
                {
                    setAside.Add($"{entry.product} named again (×{before} and ×{count}): counted as ×{Math.Max(before, count)}");
                    count = Math.Max(before, count);
                }

                times[entry.product] = count;
            }

            return times.Select(entry => (entry.Key, entry.Value));
        }

        /// <summary>The payments' ids, each once, the latest that the record keeps; the ids set aside go to the list.</summary>
        private IEnumerable<string> PaymentsDelivered(List<string> setAside)
        {
            var ids = new List<string>();
            foreach (string? id in ProgressDocument.ListOrNone(transactions))
            {
                if (string.IsNullOrEmpty(id))
                {
                    setAside.Add("an empty payment's id");
                }
                else if (ids.Contains(id, StringComparer.Ordinal))
                {
                    setAside.Add($"the payment {id} named again: kept once");
                }
                else
                {
                    ids.Add(id);
                }
            }

            int beyondTheKept = Math.Max(0, ids.Count - PurchasesMade.TransactionsKept);
            setAside.AddRange(ids.Take(beyondTheKept).Select(id => $"the payment {id}, older than the {PurchasesMade.TransactionsKept} kept"));
            return ids.Skip(beyondTheKept);
        }
    }

    /// <summary>What a purchases' block was read as: the record it holds, and the lines set aside on the way, each with a word on why (none for a block a game wrote).</summary>
    public sealed class PurchasesReading
    {
        public PurchasesReading(PurchasesMade made, IReadOnlyList<string> setAside)
        {
            Made = made ?? throw new ArgumentNullException(nameof(made));
            SetAside = setAside ?? throw new ArgumentNullException(nameof(setAside));
        }

        /// <summary>The reading of a layout that kept no purchases: nothing bought, nothing set aside.</summary>
        public static PurchasesReading Nothing { get; } = new PurchasesReading(PurchasesMade.None, Array.Empty<string>());

        public PurchasesMade Made { get; }

        public IReadOnlyList<string> SetAside { get; }
    }

    /// <summary>How many times one product was bought, as the file writes it.</summary>
    [Serializable]
    public sealed class BoughtDocument
    {
        /// <summary>The end of the product's id in the stores: "coins.pouch".</summary>
        public string product = string.Empty;

        public int times;
    }

    /// <summary>How many of one booster the player holds, as the file writes it.</summary>
    [Serializable]
    public sealed class BoosterCountDocument
    {
        /// <summary>The booster's name in attempt records, as Puzzle.Meta.BoosterKinds.Name gives it.</summary>
        public string name = string.Empty;

        public int count;
    }

    /// <summary>What reading a progress document came to. A closed set: the private constructor keeps every kind inside this class.</summary>
    public abstract class ProgressReading
    {
        private ProgressReading()
        {
        }

        /// <summary>A progress document of a layout this game reads as its own.</summary>
        public sealed class Sound : ProgressReading
        {
            /// <param name="setAside">The lines of its purchases' block that were set aside, each with a word on why; none for a document a game wrote.</param>
            public Sound(PlayerProgress progress, IReadOnlyList<string> setAside)
            {
                Progress = progress;
                SetAside = setAside;
            }

            public PlayerProgress Progress { get; }

            /// <summary>The lines of the purchases' block no game could have written, set aside while the rest was read.</summary>
            public IReadOnlyList<string> SetAside { get; }
        }

        /// <summary>A progress document of a later layout, with what this game can read of it, if anything.</summary>
        public sealed class OfALaterGame : ProgressReading
        {
            public OfALaterGame(int version, PlayerProgress? understood)
            {
                Version = version;
                Understood = understood;
            }

            public int Version { get; }

            /// <summary>Where the player stands, with the pieces and the wallet as far as they read; a wallet that does not read is the rules' starting one. Nothing when not even the player's place reads.</summary>
            public PlayerProgress? Understood { get; }
        }

        /// <summary>Not a progress document: no JSON, half a save, numbers no progress can have.</summary>
        public sealed class Damaged : ProgressReading
        {
        }
    }
}
