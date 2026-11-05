#nullable enable
using System;
using System.IO;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using Puzzle.Museum;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the progress file does what the game relies on, with Unity's own JSON reader
    /// and file calls: nothing saved reads as a new player with the rules' starting wallet, what is saved is read
    /// back (also when a file is already there to be replaced, with the jigsaw pieces the player has put in place,
    /// and with a wallet whose next life is due at an exact second), the file of the game before the museum and the
    /// file of the game before the wallet still read (their player gets the pieces of the levels they had won and the
    /// rules' starting wallet with every life, and has seen the steps of the tutorial of the levels they had won), the
    /// steps of the tutorial a player has seen are written and read back, such a file is written in the current layout at the next save and
    /// reads back after a restart, what was bought with the server's ledger numbers of it (layout 7) is written as
    /// pinned and read back, a file that is not a progress document is moved aside and never written over,
    /// and a file that a later version of the game wrote is left exactly as it is while the game is told where the
    /// player stands. It works in a folder of its own (out/unity/progress-check), not in a player's.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ProgressFileCheck.Run
    /// </summary>
    public static class ProgressFileCheck
    {
        private const string Folder = "../out/unity/progress-check";

        /// <summary>What a damaged file may hold: nothing, half a save, no JSON at all, a document without a layout, numbers no progress has.</summary>
        private static readonly string[] DamagedDocuments =
        {
            string.Empty,
            "{\"version\":1,\"levelsWon\":3",
            "not a progress file",
            "{\"levelsWon\":3,\"attemptsOnLevel\":1}",
            "{\"version\":1,\"levelsWon\":-4,\"attemptsOnLevel\":0}",
            "{\"version\":2,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[{\"painting\":1,\"pieces\":[3]}]}", // three levels give the pieces 0 to 2
            "{\"version\":3,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[]}", // a layout with a wallet, without it, is not a wallet of zeros
            "{\"version\":4,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[]}", // nor the layout of the gifts without it
            "{\"version\":5,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[]}", // nor the layout of the tutorial
            "{\"version\":6,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[]}", // nor the layout of the purchases
            "{\"version\":7,\"levelsWon\":3,\"attemptsOnLevel\":0,\"placed\":[]}", // nor the current layout
        };

        /// <summary>What the first Android build wrote: the layout before the museum.</summary>
        private const string DocumentBeforeTheMuseum = "{\"version\":1,\"levelsWon\":39,\"attemptsOnLevel\":2}";

        /// <summary>The same player after placing the first painting's pieces and two of the fourth's, as the game before the wallet wrote it (layout 2).</summary>
        private const string DocumentBeforeTheWallet =
            "{\"version\":2,\"levelsWon\":39,\"attemptsOnLevel\":2,\"placed\":[{\"painting\":1,\"pieces\":[0,1,2,3,4,5,6,7,8,9]},{\"painting\":4,\"pieces\":[0,2]}]}";

        /// <summary>
        /// The same player with <see cref="WalletInUse"/>, as this game writes it (layout 4): the spatula's and the row
        /// brush's opening gifts given. The pinned text lives twice, here and in ProgressDocumentTests: change both together.
        /// </summary>
        private const string DocumentWithAWallet =
            "{\"version\":4,\"levelsWon\":39,\"attemptsOnLevel\":2,\"placed\":[{\"painting\":1,\"pieces\":[0,1,2,3,4,5,6,7,8,9]},{\"painting\":4,\"pieces\":[0,2]}],"
            + "\"wallet\":{\"coins\":1234,\"livesShort\":2,\"nextLifeAtUnixSeconds\":1791970200,"
            + "\"boosters\":[{\"name\":\"start-rocket-and-bomb\",\"count\":0},{\"name\":\"start-color-bomb\",\"count\":2},{\"name\":\"start-extra-moves\",\"count\":0},"
            + "{\"name\":\"hit-cell\",\"count\":0},{\"name\":\"hit-row\",\"count\":1},{\"name\":\"hit-column\",\"count\":0},{\"name\":\"shuffle\",\"count\":5}],"
            + "\"day\":739902,\"adsForMovesToday\":1,\"adsForCoinsToday\":2,\"paintingRewards\":3,\"dailyRewards\":4,\"lastDailyDay\":739901,"
            + "\"unlockGifts\":[\"hit-cell\",\"hit-row\"]}}";

        /// <summary>
        /// The same player with the steps of the tutorial seen so far (<see cref="SeenOnLevelForty"/>), as this game writes
        /// it (layout 5). The pinned text lives twice, here and in ProgressDocumentTests: change both together.
        /// </summary>
        private static readonly string DocumentWithTheTutorial = DocumentWithAWallet.Replace("{\"version\":4,", "{\"version\":5,").Substring(0, DocumentWithAWallet.Length - 1)
            + ",\"tutorial\":[\"goal\",\"match\",\"moves\",\"four-in-a-line\",\"five-in-a-line\",\"box\",\"jelly\",\"first-piece\",\"first-painting\"]}";

        /// <summary>
        /// What a player on level 40 with the first painting whole had seen of the tutorial before the booster intro
        /// (#7): all but the steps of the tape and the frame corners. Named one by one, so that the pinned file stays
        /// what it is as steps are added.
        /// </summary>
        private static readonly TutorialSeen SeenOnLevelForty = TutorialSeen.Of(new[]
        {
            TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves, TutorialStep.FourInALine, TutorialStep.FiveInALine, TutorialStep.Box, TutorialStep.Jelly,
            TutorialStep.FirstPiece, TutorialStep.FirstPainting,
        });

        /// <summary>
        /// The same player with two payments delivered, the second numbered by the server's ledger, as this game writes it
        /// (layout 7). The pinned text lives twice, here and in ProgressDocumentTests: change both together.
        /// </summary>
        private static readonly string DocumentWithTheLedger = DocumentWithTheTutorial.Replace("{\"version\":5,", "{\"version\":7,").Substring(0, DocumentWithTheTutorial.Length - 1)
            + ",\"purchases\":{\"bought\":[{\"product\":\"coins.pouch\",\"times\":2}],\"transactions\":[\"GPA.1\",\"GPA.2\"],\"ledgerDelivered\":\"2\"}}";

        /// <summary>The document of a later game: what this game writes, and something it does not know.</summary>
        private static readonly string LaterDocument =
            DocumentWithTheLedger.Replace("{\"version\":7,", "{\"version\":8,").Substring(0, DocumentWithTheLedger.Length - 1) + ",\"device\":\"phone\"}";

        /// <summary>Noon on 14 October 2026 in Istanbul, a whole second as every clock of the game tells: when the check loses a life.</summary>
        private static readonly IWallClock Clock = new FixedWallClock(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3)));

        public static void Run()
        {
            try
            {
                EconomyRules rules = EconomyPack.LoadPacked();
                StartWithAnEmptyFolder();
                var file = new ProgressFile(Folder, rules.StartingWallet);
                Expect(PlayerProgress.NewPlayer(rules.StartingWallet), file.Load(), "with nothing saved");

                var first = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2);
                file.Save(first);
                Expect(first, Restarted(rules).Load(), "after a first save");

                var second = first.WithLevelWon(levelCount: 1000);
                file.Save(second);
                Expect(second, Restarted(rules).Load(), "after a save over an older one");

                string path = Path.Combine(Folder, ProgressFile.FileName);
                ExpectThePiecesOfTheLevelsWonBeforeTheMuseum(file, path, rules.StartingWallet);
                PlayerProgress turned = ExpectTheStartingWalletBeforeTheWallet(file, path, rules);
                ExpectALifeLostReadBackToTheSecond(file, turned, rules);
                ExpectAWalletInUseWrittenAsPinned(file, path, rules);

                string[] damagedDocuments = DamagedDocuments.Concat(DamagedWallets())
                    .Append(DocumentWithTheTutorial.Replace("\"goal\",", "\"booster-intro\",")) // a step this game does not have
                    .Append(DocumentWithTheTutorial.Replace("\"match\",", "\"goal\",")) // a step twice
                    .ToArray();
                foreach (string damaged in damagedDocuments)
                {
                    File.WriteAllText(path, damaged);
                    Expect(PlayerProgress.NewPlayer(rules.StartingWallet), file.Load(), $"with the file holding \"{damaged}\"");
                    ExpectMovedAside(path, damaged);
                }

                File.WriteAllText(path, LaterDocument);
                ExpectLeftAlone(file, path, TutoredPlayer());

                Debug.Log($"Progress file check passed: saved progress is read back with its placed pieces, its wallet and the tutorial's steps seen, the files of the game before the museum "
                    + $"and before the wallet give their player their pieces and the rules' starting wallet ({rules.StartingWallet}), such a file is written in layout "
                    + $"{ProgressDocument.CurrentVersion} at the next save and reads back after a restart, a life lost reads back to the second, "
                    + $"{damagedDocuments.Length} damaged files were moved aside, and a later game's file was left as it was.");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidOperationException || failure is EconomyFormatException)
            {
                Debug.LogError($"Progress file check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The file of the game before the museum is read as it stands; every piece its levels gave waits on a tray,
        /// and the player holds the rules' starting wallet. Reading it changes nothing in it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The old file was not read so, or was changed by reading it.</exception>
        private static void ExpectThePiecesOfTheLevelsWonBeforeTheMuseum(ProgressFile file, string path, Wallet startingWallet)
        {
            File.WriteAllText(path, DocumentBeforeTheMuseum);
            PlayerProgress read = file.Load();
            Expect(AsAnOlderSaveReads(new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithWallet(startingWallet)), read, "with the file of the game before the museum");
            if (read.Museum.PiecesWaiting != 39 || File.ReadAllText(path) != DocumentBeforeTheMuseum)
            {
                throw new InvalidOperationException($"The file of the game before the museum gave {read.Museum.PiecesWaiting} waiting pieces instead of 39, or was changed by reading it.");
            }
        }

        /// <summary>
        /// A real file of the game before the wallet (layout 2) is read with its pieces and the rules' starting wallet,
        /// every life there; reading it changes nothing in it. The next save writes it in the current layout, and a
        /// restart reads the same back.
        /// </summary>
        /// <returns>The progress as it was read and saved again.</returns>
        /// <exception cref="InvalidOperationException">The file was not read, written or read back so.</exception>
        private static PlayerProgress ExpectTheStartingWalletBeforeTheWallet(ProgressFile file, string path, EconomyRules rules)
        {
            File.WriteAllText(path, DocumentBeforeTheWallet);
            PlayerProgress read = file.Load();
            Expect(AsAnOlderSaveReads(PlacedPlayer().WithWallet(rules.StartingWallet)), read, "with the file of the game before the wallet");
            if (!read.Wallet.Lives.IsFull || File.ReadAllText(path) != DocumentBeforeTheWallet)
            {
                throw new InvalidOperationException($"The file of the game before the wallet gave lives \"{read.Wallet.Lives}\", or was changed by reading it.");
            }

            file.Save(read);
            ProgressDocument written = JsonUtility.FromJson<ProgressDocument>(File.ReadAllText(path));
            if (written.version != ProgressDocument.CurrentVersion)
            {
                throw new InvalidOperationException($"The file of the game before the wallet was saved again in layout {written.version}, not {ProgressDocument.CurrentVersion}.");
            }

            Expect(read, Restarted(rules).Load(), "after the file of the game before the wallet was saved in the current layout and the game was restarted");
            Debug.Log($"Progress file check: a layout-2 file read as \"{read}\", written as {File.ReadAllText(path)}");
            return read;
        }

        /// <summary>
        /// A life lost on a clock of the game (the rules' own step, as a lost level takes it) is saved and read back
        /// after a restart exactly: one life fewer, the next due one life's time after the loss, to the second.
        /// </summary>
        /// <exception cref="InvalidOperationException">The meter was not read back as it was saved.</exception>
        private static void ExpectALifeLostReadBackToTheSecond(ProgressFile file, PlayerProgress before, EconomyRules rules)
        {
            PlayerProgress lost = new AttemptLedger(rules, levelCount: 1000).Apply(before, new AttemptEvent.LostForGood(LossReason.OutOfMoves), Clock.Now);
            var oneLost = new LifeMeter(shortOfFull: 1, Clock.Now + rules.Lives.PerLife);
            if (lost.Wallet.Lives != oneLost)
            {
                throw new InvalidOperationException($"A lost level left lives \"{lost.Wallet.Lives}\", not \"{oneLost}\".");
            }

            file.Save(lost);
            Expect(lost, Restarted(rules).Load(), "after a life was lost and the game was restarted");
        }

        /// <summary>A wallet with something in every field, the tutorial seen so far and two payments, one numbered by the server's ledger, are written exactly as pinned, and read back after a restart.</summary>
        /// <exception cref="InvalidOperationException">The wallet, the tutorial or the purchases were not written or read back so.</exception>
        private static void ExpectAWalletInUseWrittenAsPinned(ProgressFile file, string path, EconomyRules rules)
        {
            PlayerProgress inUse = TutoredPlayer();
            file.Save(inUse);
            if (File.ReadAllText(path) != DocumentWithTheLedger)
            {
                throw new InvalidOperationException($"The progress with a wallet in use was written as {File.ReadAllText(path)}; {DocumentWithTheLedger} was expected.");
            }

            Expect(inUse, Restarted(rules).Load(), "after a wallet in use was saved and the game was restarted");
        }

        /// <summary>The pinned document with one thing in its wallet that no wallet can have.</summary>
        private static string[] DamagedWallets() => new[]
        {
            DocumentWithAWallet.Replace("\"coins\":1234", "\"coins\":-1234"),
            DocumentWithAWallet.Replace("\"livesShort\":2", "\"livesShort\":-2"),
            DocumentWithAWallet.Replace("\"livesShort\":2", "\"livesShort\":0"), // a time for a full meter
            DocumentWithAWallet.Replace("\"hit-cell\"", "\"hit-everything\""),
            DocumentWithAWallet.Replace("\"hit-cell\"", "\"hit-row\""), // one booster twice and one not at all
            DocumentWithAWallet.Replace("{\"name\":\"shuffle\",\"count\":5}", "{\"name\":\"shuffle\",\"count\":-5}"),
            DocumentWithAWallet.Replace("\"dailyRewards\":4", "\"dailyRewards\":0"), // a day for a daily reward never given
            DocumentWithAWallet.Replace("\"day\":739902", "\"day\":-1"),
            DocumentWithAWallet.Replace("\"nextLifeAtUnixSeconds\":1791970200", "\"nextLifeAtUnixSeconds\":999999999999"), // after the calendar's last day
            DocumentWithAWallet.Replace("[\"hit-cell\",\"hit-row\"]", "[\"start-color-bomb\"]"), // a start booster has no opening gift
            DocumentWithAWallet.Replace("[\"hit-cell\",\"hit-row\"]", "[\"hit-cell\",\"hit-cell\"]"), // a gift twice
        };

        /// <summary><see cref="PlacedPlayer"/> with <see cref="WalletInUse"/>, the tutorial seen so far, and two pouches bought, the second with the ledger's number 2.</summary>
        private static PlayerProgress TutoredPlayer()
        {
            PlayerProgress placed = PlacedPlayer();
            var bought = new PurchasesMade(new[] { ("coins.pouch", 2) }, new[] { "GPA.1", "GPA.2" }, LedgerNumbers.Of(new[] { new LedgerNumber(2) }));
            return new PlayerProgress(placed.LevelsWon, placed.AttemptsOnLevel, placed.Museum.Placed, WalletInUse(), SeenOnLevelForty, bought);
        }

        /// <summary>A progress as a file of a layout before the tutorial reads: with the steps of the levels won and of what was done in the museum.</summary>
        private static PlayerProgress AsAnOlderSaveReads(PlayerProgress progress) =>
            new PlayerProgress(progress.LevelsWon, progress.AttemptsOnLevel, progress.Museum.Placed, progress.Wallet, TutorialPlan.OfAnOlderSave(progress.Museum));

        /// <summary>A player who has won 39 levels and placed the first painting's pieces and two of the fourth's; an empty wallet.</summary>
        private static PlayerProgress PlacedPlayer() =>
            new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithTrayPlaced(new PaintingNumber(1)).WithPiecePlaced(new PaintingNumber(4), 2).WithPiecePlaced(new PaintingNumber(4), 0);

        /// <summary>
        /// A wallet with something in every field: 1 234 coins, two lives short with the next due at 12:30 in
        /// Istanbul, a few boosters, ads and rewards of the day before and the day of the check. Its numbers are this
        /// check's own, not the economy's.
        /// </summary>
        private static Wallet WalletInUse()
        {
            DayStamp day = DayStamp.Of(Clock.Now);
            return new Wallet(
                new Coins(1234),
                new LifeMeter(shortOfFull: 2, Clock.Now.AddMinutes(30)),
                BoosterShelf.Empty.With(BoosterKind.StartColorBomb, 2).With(BoosterKind.HitRow, 1).With(BoosterKind.Shuffle, 5),
                new AdsWatched(day, forExtraMoves: 1, forCoins: 2),
                new RewardsGiven(forPaintings: 3, daily: 4, lastDailyOn: new DayStamp(day.Number - 1), BoosterKindSet.Of(new[] { BoosterKind.HitCell, BoosterKind.HitRow })));
        }

        /// <summary>The file as a game that was closed and opened again finds it.</summary>
        private static ProgressFile Restarted(EconomyRules rules) => new ProgressFile(Folder, rules.StartingWallet);

        private static void StartWithAnEmptyFolder()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }

        /// <exception cref="InvalidOperationException">The progress that was read is not the expected one.</exception>
        private static void Expect(PlayerProgress expected, PlayerProgress read, string when)
        {
            if (!read.Equals(expected))
            {
                throw new InvalidOperationException($"The file gave \"{read}\" {when}; \"{expected}\" was expected.");
            }
        }

        /// <exception cref="InvalidOperationException">The later game's file was read as this game's, moved or changed, or the player's place was not told.</exception>
        private static void ExpectLeftAlone(ProgressFile file, string path, PlayerProgress understood)
        {
            try
            {
                PlayerProgress read = file.Load();
                throw new InvalidOperationException($"A later game's file was read as \"{read}\" instead of being refused.");
            }
            catch (ProgressOfALaterGameException later)
            {
                bool isLeft = File.Exists(path) && File.ReadAllText(path) == LaterDocument;
                if (!isLeft || later.Version != 8 || !understood.Equals(later.Understood))
                {
                    throw new InvalidOperationException(
                        $"A later game's file was not left as it was (still there unchanged: {isLeft}), or the game was told layout {later.Version} and \"{later.Understood}\".");
                }
            }
        }

        /// <exception cref="InvalidOperationException">The damaged file is still in the progress file's place, or was not kept.</exception>
        private static void ExpectMovedAside(string path, string damaged)
        {
            string[] others = Directory.GetFiles(Folder);
            bool isKept = others.Length == 1 && others[0] != path && File.ReadAllText(others[0]) == damaged;
            if (File.Exists(path) || !isKept)
            {
                throw new InvalidOperationException($"A file holding \"{damaged}\" was not moved aside as it was; the folder holds {string.Join(", ", others)}.");
            }
        }
    }
}
