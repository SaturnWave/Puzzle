#nullable enable
using System;
using Puzzle.Meta;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// How far a player has come: the levels are played in the pack's order, a win opens the next one, a loss means
    /// the same level again. It is the web test server's rule (Puzzle.Tooling.Players.PlayerProgress), so a level's
    /// retries meet the same boards here as there: the player stands on the level after the ones they won, and
    /// <see cref="AttemptsOnLevel"/> counts their attempts on that level only. An attempt counts once a move is
    /// played in it; a board that was only looked at uses up no retry number. The levels won also give the player the
    /// jigsaw pieces of the museum's paintings (<see cref="Museum"/>); which of them the player has put in place is
    /// kept here with the rest, and so is what the player owns around the levels (<see cref="Wallet"/>) and which steps
    /// of the tutorial they have seen (<see cref="Tutorial"/>): it is saved in the same file, so a win and its coins
    /// are one save. This file has no Unity dependency, so the repository's
    /// tests compile it too.
    /// </summary>
    public sealed class PlayerProgress : IEquatable<PlayerProgress>
    {
        /// <summary>
        /// Progress with no jigsaw piece in place yet, an empty wallet (<see cref="Wallet.Empty"/>: no coin, no
        /// booster, every life) and the tutorial behind it (<see cref="TutorialSeen.All"/>): where the questions of
        /// the levels and the museum start, which the wallet and the tutorial play no part in. A player's own progress
        /// gets its wallet from the rules and sees the tutorial (<see cref="NewPlayer"/>), or has both from their file.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        public PlayerProgress(int levelsWon, int attemptsOnLevel)
            : this(levelsWon, attemptsOnLevel, PlacedPieces.None, Wallet.Empty)
        {
        }

        /// <summary>Progress with an empty wallet and the tutorial behind it, as above.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        /// <exception cref="ArgumentException">A piece is said to be in place that the levels won have not given yet.</exception>
        public PlayerProgress(int levelsWon, int attemptsOnLevel, PlacedPieces placed)
            : this(levelsWon, attemptsOnLevel, placed, Wallet.Empty)
        {
        }

        /// <summary>Progress with the tutorial behind it, as above.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        /// <exception cref="ArgumentException">A piece is said to be in place that the levels won have not given yet.</exception>
        public PlayerProgress(int levelsWon, int attemptsOnLevel, PlacedPieces placed, Wallet wallet)
            : this(levelsWon, attemptsOnLevel, placed, wallet, TutorialSeen.All)
        {
        }

        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        /// <exception cref="ArgumentException">A piece is said to be in place that the levels won have not given yet.</exception>
        public PlayerProgress(int levelsWon, int attemptsOnLevel, PlacedPieces placed, Wallet wallet, TutorialSeen tutorial)
        {
            if (levelsWon < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelsWon), levelsWon, "A player cannot have won fewer than no levels.");
            }

            if (attemptsOnLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptsOnLevel), attemptsOnLevel, "A level cannot have been tried fewer than no times.");
            }

            LevelsWon = levelsWon;
            AttemptsOnLevel = attemptsOnLevel;
            Museum = new MuseumProgress(levelsWon, placed);
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
        }

        /// <summary>Where every player starts: the first level, never tried, holding what the rules give a new player, with every step of the tutorial to see.</summary>
        /// <param name="startingWallet">The economy rules' starting wallet (Puzzle.Meta.EconomyRules.StartingWallet).</param>
        public static PlayerProgress NewPlayer(Wallet startingWallet) =>
            new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0, PlacedPieces.None, startingWallet, TutorialSeen.None);

        /// <summary>How many levels of the pack, from its first on, the player has won.</summary>
        public int LevelsWon { get; }

        /// <summary>
        /// The attempts on the level the player stands on in which a move was played. It is also the number, counted
        /// from 0, of their next attempt there, which picks that attempt's seed from the level's calibrated pool.
        /// </summary>
        public int AttemptsOnLevel { get; }

        /// <summary>The museum as the player has restored it: the pieces their levels gave them, and the ones they have put in place.</summary>
        public MuseumProgress Museum { get; }

        /// <summary>What the player owns and has used up around the levels: coins, lives, boosters, the day's ads and the rewards counted.</summary>
        public Wallet Wallet { get; }

        /// <summary>The steps of the tutorial the player has been shown to their end: each is shown once (docs/GAME_DESIGN.md §19).</summary>
        public TutorialSeen Tutorial { get; }

        /// <summary>
        /// Which level of a pack the player stands on, counted from 0 in the pack's order: the one after the levels
        /// they won. A player who has won them all stands on the last one, and may play it again.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The pack has no level.</exception>
        public int LevelIndexIn(int levelCount)
        {
            if (levelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "A pack without a level has no level to stand on.");
            }

            return Math.Min(LevelsWon, levelCount - 1);
        }

        /// <summary>True when the level the player stands on is the last of a pack with this many levels.</summary>
        public bool IsOnLastLevelOf(int levelCount) => LevelIndexIn(levelCount) == levelCount - 1;

        /// <summary>The first move of an attempt on the level was played: the attempt now counts.</summary>
        public PlayerProgress WithAttemptStarted() => new PlayerProgress(LevelsWon, AttemptsOnLevel + 1, Museum.Placed, Wallet, Tutorial);

        /// <summary>
        /// The level the player stands on is won: they stand on the next one, never tried, and the won level's jigsaw
        /// pieces lie on their painting's tray. Winning the last level of the pack again does not count it twice.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The pack has no level.</exception>
        public PlayerProgress WithLevelWon(int levelCount) =>
            new PlayerProgress(Math.Min(LevelIndexIn(levelCount) + 1, levelCount), attemptsOnLevel: 0, Museum.Placed, Wallet, Tutorial);

        /// <summary>The player put a jigsaw piece from a painting's tray in its place.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece of that number.</exception>
        /// <exception cref="InvalidOperationException">The piece is not on the tray: not won yet, or in place already.</exception>
        public PlayerProgress WithPiecePlaced(PaintingNumber painting, int piece) =>
            new PlayerProgress(LevelsWon, AttemptsOnLevel, Museum.WithPiecePlaced(painting, piece).Placed, Wallet, Tutorial);

        /// <summary>Every piece on a painting's tray went to its place at once.</summary>
        public PlayerProgress WithTrayPlaced(PaintingNumber painting) =>
            new PlayerProgress(LevelsWon, AttemptsOnLevel, Museum.WithTrayPlaced(painting).Placed, Wallet, Tutorial);

        /// <summary>The player's wallet changed: something was paid, earned, used or counted.</summary>
        public PlayerProgress WithWallet(Wallet wallet) => new PlayerProgress(LevelsWon, AttemptsOnLevel, Museum.Placed, wallet, Tutorial);

        /// <summary>A step of the tutorial was shown to its end; one seen before stays seen.</summary>
        public PlayerProgress WithTutorialStepSeen(TutorialStep step) => new PlayerProgress(LevelsWon, AttemptsOnLevel, Museum.Placed, Wallet, Tutorial.With(step));

        /// <summary>
        /// This progress with a number of the waiting pieces put in place, in the museum's order: painting by
        /// painting, each painting's pieces by their numbers. It is how a picture command or a test says "a player
        /// who has won so many levels and placed so many pieces".
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Fewer than no pieces, or more than are waiting.</exception>
        public PlayerProgress WithWaitingPiecesPlaced(int count)
        {
            if (count < 0 || count > Museum.PiecesWaiting)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"{Museum.PiecesWaiting} pieces are waiting to be placed.");
            }

            MuseumProgress museum = Museum;
            int left = count;
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                if (left == 0)
                {
                    break;
                }

                foreach (int piece in museum.WaitingIn(painting))
                {
                    if (left == 0)
                    {
                        break;
                    }

                    museum = museum.WithPiecePlaced(painting, piece);
                    left--;
                }
            }

            return new PlayerProgress(LevelsWon, AttemptsOnLevel, museum.Placed, Wallet, Tutorial);
        }

        public bool Equals(PlayerProgress? other) =>
            other is not null && LevelsWon == other.LevelsWon && AttemptsOnLevel == other.AttemptsOnLevel && Museum.Placed.Equals(other.Museum.Placed)
            && Wallet.Equals(other.Wallet) && Tutorial.Equals(other.Tutorial);

        public override bool Equals(object? obj) => Equals(obj as PlayerProgress);

        public override int GetHashCode() => HashCode.Combine(LevelsWon, AttemptsOnLevel, Museum.Placed, Wallet, Tutorial);

        public override string ToString() =>
            $"{LevelsWon} levels won, {AttemptsOnLevel} attempts on the next, {Museum.PiecesPlaced} of {Museum.PiecesWon} jigsaw pieces placed; {Wallet}; tutorial: {Tutorial}";
    }

    /// <summary>Where a player's progress is kept between two runs of the game.</summary>
    public interface IProgressStore
    {
        /// <summary>The progress that was saved last; a new player's (<see cref="PlayerProgress.NewPlayer"/>) when none ever was.</summary>
        /// <exception cref="System.IO.IOException">What was saved cannot be read, or is not this version's to read; the store is left as it is.</exception>
        PlayerProgress Load();

        /// <summary>Keeps the progress so that a later <see cref="Load"/> returns it, also after the game was closed.</summary>
        /// <exception cref="System.IO.IOException">The store could not be written; what it held before is still there.</exception>
        /// <exception cref="UnauthorizedAccessException">The game may not write the store; what it held before is still there.</exception>
        void Save(PlayerProgress progress);
    }

    /// <summary>A store that forgets when the game closes: for the Editor's storyboard and for tests, which must not touch a player's own progress.</summary>
    public sealed class ProgressInMemory : IProgressStore
    {
        private PlayerProgress _kept;

        public ProgressInMemory(PlayerProgress start)
        {
            _kept = start;
        }

        /// <summary>How often the progress was saved: a test's proof that a change was kept at once.</summary>
        public int TimesSaved { get; private set; }

        public PlayerProgress Load() => _kept;

        public void Save(PlayerProgress progress)
        {
            _kept = progress;
            TimesSaved++;
        }
    }
}
