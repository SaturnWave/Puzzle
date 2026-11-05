#nullable enable
using System;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// How pieces fall: they start with a little speed and are pulled faster and faster, like things that fall. One
    /// law for every piece means that a piece that starts later above another never catches up with it, however far
    /// each of them falls, so falling pieces need no other rule to keep them apart. Lengths are in cells.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class FallLaw
    {
        /// <summary>A piece leaves its cell at this speed, in cells per second, so the first frames of a fall already move.</summary>
        public const float StartSpeed = 3f;

        /// <summary>The pull, in cells per second per second: one row takes about a tenth of a second, eight rows a third.</summary>
        public const float Pull = 120f;

        /// <summary>How far a piece has fallen so long after it let go.</summary>
        public static float CellsAfter(float seconds) => StartSpeed * seconds + Pull * seconds * seconds / 2f;

        /// <summary>How long a fall of so many cells takes.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The distance is negative.</exception>
        public static float SecondsFor(float cells)
        {
            if (cells < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cells), cells, "Pieces do not fall upwards.");
            }

            return (float)((Math.Sqrt(StartSpeed * StartSpeed + 2.0 * Pull * cells) - StartSpeed) / Pull);
        }
    }
}
