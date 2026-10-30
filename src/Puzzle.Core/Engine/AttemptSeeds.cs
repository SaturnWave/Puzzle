using System;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// The randomness of one attempt, split in two so a level can keep its starting board while the falling
    /// tiles change from attempt to attempt.
    /// </summary>
    public readonly struct AttemptSeeds : IEquatable<AttemptSeeds>
    {
        private AttemptSeeds(ulong board, ulong play)
        {
            Board = board;
            Play = play;
        }

        /// <summary>Seeds the starting board.</summary>
        public ulong Board { get; }

        /// <summary>Seeds everything during play: refills, effect choices, shuffles.</summary>
        public ulong Play { get; }

        /// <summary>One seed for both board and play: every attempt looks and plays differently.</summary>
        public static AttemptSeeds From(ulong seed) => new AttemptSeeds(seed, seed);

        /// <summary>The same starting board every attempt, different falling tiles.</summary>
        public static AttemptSeeds WithFixedBoard(ulong boardSeed, ulong playSeed) => new AttemptSeeds(boardSeed, playSeed);

        public bool Equals(AttemptSeeds other) => Board == other.Board && Play == other.Play;

        public override bool Equals(object? obj) => obj is AttemptSeeds other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Board, Play);

        public override string ToString() => Board == Play ? $"seed {Play}" : $"board {Board} / play {Play}";

        public static bool operator ==(AttemptSeeds left, AttemptSeeds right) => left.Equals(right);

        public static bool operator !=(AttemptSeeds left, AttemptSeeds right) => !left.Equals(right);
    }
}
