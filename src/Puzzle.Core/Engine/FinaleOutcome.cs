using System;

namespace Puzzle.Core.Engine
{
    /// <summary>What the celebration after a win did (<see cref="Game.PlayFinale"/>).</summary>
    public sealed class FinaleOutcome
    {
        internal FinaleOutcome(int rocketsFired, PlayStats stats)
        {
            RocketsFired = rocketsFired;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        /// <summary>
        /// One per move that was left; fewer only when the board ran out of free gems to turn into rockets. What a
        /// move left is worth in coins is the game's economy, not the engine's.
        /// </summary>
        public int RocketsFired { get; }

        /// <summary>What the rockets did to the board. Not part of <see cref="Game.TotalStats"/>, which describe the play.</summary>
        public PlayStats Stats { get; }
    }
}
