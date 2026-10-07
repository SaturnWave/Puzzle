#nullable enable
using System;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// How far a player has come: the levels are played in the pack's order, a win opens the next one, a loss means
    /// the same level again. It is the web test server's rule (Puzzle.Tooling.Players.PlayerProgress), so a level's
    /// retries meet the same boards here as there: the player stands on the level after the ones they won, and
    /// <see cref="AttemptsOnLevel"/> counts their attempts on that level only. An attempt counts once a move is
    /// played in it; a board that was only looked at uses up no retry number. This file has no Unity dependency, so
    /// the repository's tests compile it too.
    /// </summary>
    public sealed class PlayerProgress : IEquatable<PlayerProgress>
    {
        /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
        public PlayerProgress(int levelsWon, int attemptsOnLevel)
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
        }

        /// <summary>Where every player starts: the first level, never tried.</summary>
        public static PlayerProgress None { get; } = new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0);

        /// <summary>How many levels of the pack, from its first on, the player has won.</summary>
        public int LevelsWon { get; }

        /// <summary>
        /// The attempts on the level the player stands on in which a move was played. It is also the number, counted
        /// from 0, of their next attempt there, which picks that attempt's seed from the level's calibrated pool.
        /// </summary>
        public int AttemptsOnLevel { get; }

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
        public PlayerProgress WithAttemptStarted() => new PlayerProgress(LevelsWon, AttemptsOnLevel + 1);

        /// <summary>
        /// The level the player stands on is won: they stand on the next one, never tried. Winning the last level of
        /// the pack again does not count it twice.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The pack has no level.</exception>
        public PlayerProgress WithLevelWon(int levelCount) =>
            new PlayerProgress(Math.Min(LevelIndexIn(levelCount) + 1, levelCount), attemptsOnLevel: 0);

        public bool Equals(PlayerProgress? other) =>
            other is not null && LevelsWon == other.LevelsWon && AttemptsOnLevel == other.AttemptsOnLevel;

        public override bool Equals(object? obj) => Equals(obj as PlayerProgress);

        public override int GetHashCode() => HashCode.Combine(LevelsWon, AttemptsOnLevel);

        public override string ToString() => $"{LevelsWon} levels won, {AttemptsOnLevel} attempts on the next";
    }

    /// <summary>Where a player's progress is kept between two runs of the game.</summary>
    public interface IProgressStore
    {
        /// <summary>The progress that was saved last; <see cref="PlayerProgress.None"/> when none ever was.</summary>
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
