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
    /// <c>{"version":3,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":4,"pieces":[0,1,2]}],"wallet":{...}}</c>
    /// (the wallet's block is <see cref="WalletDocument"/>). The fields are public and named as the document names
    /// them, because that is how Unity's JSON reader maps a document. Two earlier layouts are still read. Layout 1,
    /// written before the game had a museum, has no <c>placed</c>: it reads as progress with no jigsaw piece in place
    /// yet, so the pieces of every level won before then wait on their trays. Layouts 1 and 2 have no wallet: their
    /// player holds what the rules give a new player, with every life. Unity's reader makes 0 of a number a document
    /// does not have, so whether the wallet is there is decided by the layout's number, never by a zero.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    [Serializable]
    public sealed class ProgressDocument
    {
        /// <summary>The layout this code writes. A file of a later layout is not guessed at.</summary>
        public const int CurrentVersion = 3;

        /// <summary>The oldest layout a player can have on a device; this code reads it as it reads its own.</summary>
        private const int FirstVersion = 1;

        /// <summary>The first layout that keeps the player's wallet.</summary>
        private const int FirstVersionWithAWallet = 3;

        public int version;
        public int levelsWon;
        public int attemptsOnLevel;

        /// <summary>The jigsaw pieces in place, one entry for every painting that has any; in the file since layout 2.</summary>
        public PlacedDocument[] placed = Array.Empty<PlacedDocument>();

        /// <summary>What the player owns around the levels; in the file since layout 3.</summary>
        public WalletDocument wallet = new WalletDocument();

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
            Wallet? wallet = document.version < FirstVersionWithAWallet ? startingWallet : document.wallet?.PossibleWallet();
            if (document.version > CurrentVersion)
            {
                // Later layouts are expected to add to this one, so what this game knows is looked for in them too:
                // the pieces and the wallet if they still read as such, else at least where the player stands.
                PlayerProgress? understood = !hasCounts ? null
                    : new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed ?? PlacedPieces.None, wallet ?? startingWallet);
                return new ProgressReading.OfALaterGame(document.version, understood);
            }

            return placed != null && wallet != null
                ? new ProgressReading.Sound(new PlayerProgress(document.levelsWon, document.attemptsOnLevel, placed, wallet))
                : (ProgressReading)new ProgressReading.Damaged();
        }

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
    /// "day":739537,"adsForMovesToday":1,"adsForCoinsToday":0,"paintingRewards":3,"dailyRewards":0,"lastDailyDay":0}</c>.
    /// Lives are kept as how many are missing, so a full meter is 0 and the time of the next life is 0 with it. An
    /// instant is kept in whole seconds since 1970 (UTC), which is what every clock of the game hands out
    /// (<see cref="IWallClock"/>); one between two seconds is written as the later second, so a wait is never cut
    /// short. A calendar day is kept as its number (Puzzle.Meta.DayStamp); the day of the latest daily reward is 0
    /// while none was given. Every booster of the game is written, by the name its records give it.
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
        };

        /// <summary>
        /// The wallet the block holds; nothing when no wallet can be so: a negative amount or count, a time for a
        /// full meter or a day for a daily reward never given, a day or an instant outside the calendar, a booster
        /// this game does not have, one named twice, or one not named at all.
        /// </summary>
        public Wallet? PossibleWallet()
        {
            BoosterShelf? shelf = PossibleShelf();
            bool statesNoTimeForAFullMeter = livesShort > 0 || nextLifeAtUnixSeconds == 0;
            bool statesNoDayWithoutADailyReward = dailyRewards > 0 || lastDailyDay == 0;
            if (shelf == null || coins < 0 || !statesNoTimeForAFullMeter || !statesNoDayWithoutADailyReward)
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
                    new RewardsGiven(paintingRewards, dailyRewards, dailyRewards > 0 ? new DayStamp(lastDailyDay) : (DayStamp?)null));
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
