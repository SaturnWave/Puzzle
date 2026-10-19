#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
#pragma warning disable IDE1006, CA1051 // public camelCase fields are the file's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// The saved progress as the file writes it:
    /// <c>{"version":5,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":4,"pieces":[0,1,2]}],"wallet":{...},"tutorial":["goal","match"]}</c>
    /// (the wallet's block is <see cref="WalletDocument"/>; the tutorial's steps seen are named as
    /// <see cref="TutorialSteps.NameOf"/> names them, in the order of the enumeration). The fields are public and named
    /// as the document names them, because that is how Unity's JSON reader maps a document. Four earlier layouts are
    /// still read. Layout 1,
    /// written before the game had a museum, has no <c>placed</c>: it reads as progress with no jigsaw piece in place
    /// yet, so the pieces of every level won before then wait on their trays. Layouts 1 and 2 have no wallet: their
    /// player holds what the rules give a new player, with every life; the gifts of the boosters used during play
    /// that are open where the player stands are given to them the first time the game opens (Puzzle.Meta's
    /// BoosterUnlocks). Layout 3 keeps a wallet but not which of those gifts were given: its player was given all of
    /// them in the welcome gift before the first level, as the game did then, so all count as given and none comes a
    /// second time. Layouts 1 to 4 keep no tutorial: their player has seen every step of a level they have won (an
    /// obstacle's level being the first that has it), the jigsaw's first piece once a piece is in place and the first
    /// painting once one is whole, and nothing else (<see cref="TutorialPlan.OfAnOlderSave"/>). Unity's reader makes 0
    /// of a number a document does not have, and an empty list of a list it does not have, so whether the wallet, the
    /// gifts and the tutorial are there is decided by the layout's number, never by a zero or an empty list.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class ProgressDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 5;

        /// <summary>The oldest layout a player can have on a device; this code reads it as it reads its own.</summary>
        private const int FirstVersion = 1;

        /// <summary>The first layout that keeps the player's wallet.</summary>
        private const int FirstVersionWithAWallet = 3;

        /// <summary>The first layout that keeps which boosters used during play have had their opening gift.</summary>
        private const int FirstVersionWithUnlockGifts = 4;

        /// <summary>The first layout that keeps which steps of the tutorial the player has seen.</summary>
        private const int FirstVersionWithTheTutorial = 5;

        public int version;
        public int levelsWon;
        public int attemptsOnLevel;

        /// <summary>The jigsaw pieces in place, one entry for every painting that has any; in the file since layout 2.</summary>
        public PlacedDocument[] placed = Array.Empty<PlacedDocument>();

        /// <summary>What the player owns around the levels; in the file since layout 3.</summary>
        public WalletDocument wallet = new WalletDocument();

        /// <summary>The steps of the tutorial the player has seen, by their names; in the file since layout 5.</summary>
        public string[] tutorial = Array.Empty<string>();

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
            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one, so what this game knows is looked for in them too:
                // the pieces, the wallet and the tutorial if they still read as such, else at least where the player
                // stands. A step this game does not have is passed over: a later game may have more.
                PlayerProgress? understood = !hasCounts ? null
                    : new PlayerProgress(
                        document.levelsWon, document.attemptsOnLevel, placed ?? PlacedPieces.None, wallet ?? startingWallet, TutorialOfALaterGame(document));
                return new ProgressReading.OfALaterGame(document.version, understood);
            }

            TutorialSeen? tutorial = placed == null ? null
                : document.version < FirstVersionWithTheTutorial ? TutorialPlan.OfAnOlderSave(new MuseumProgress(document.levelsWon, placed))
                : PossibleTutorialOf(document);
            return placed != null && wallet != null && tutorial != null
                ? new ProgressReading.Sound(new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed, wallet, tutorial))
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
            public Sound(PlayerProgress progress)
            {
                Progress = progress;
            }

            public PlayerProgress Progress { get; }
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
