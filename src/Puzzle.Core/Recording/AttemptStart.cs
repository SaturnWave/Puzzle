using System;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Core.Recording
{
    /// <summary>What is known when an attempt starts: the level, its randomness, who plays it on which client, and when.</summary>
    public sealed class AttemptStart
    {
        public const int MaxClientLength = 32;

        /// <param name="level">The level played.</param>
        /// <param name="moveLimit">The level's move limit, without any "+5 moves" purchase.</param>
        /// <param name="seeds">The attempt's seeds; with the level and the moves they reproduce the game exactly.</param>
        /// <param name="attempt">
        /// The 0-based retry number when the seeds came from the level's <see cref="AttemptPolicy"/> as the device's
        /// next retry; absent when they were chosen by hand or the number is no real retry count (a web lab
        /// experiment, possibly a replay of a game already seen).
        /// </param>
        /// <param name="device">The anonymous device that played.</param>
        /// <param name="client">The program that played, e.g. "web-lab": lowercase letters, digits and '-'.</param>
        /// <param name="startedUtc">When the attempt started; stored in UTC.</param>
        /// <exception cref="ArgumentException">A value is out of range or malformed.</exception>
        public AttemptStart(LevelId level, int moveLimit, AttemptSeeds seeds, int? attempt, DeviceId device, string client, DateTimeOffset startedUtc)
        {
            Level = level;
            MoveLimit = moveLimit >= 1 && moveLimit <= LevelDefinition.MaxMoveLimit
                ? moveLimit
                : throw new ArgumentOutOfRangeException(nameof(moveLimit), moveLimit, $"A move limit is 1..{LevelDefinition.MaxMoveLimit}.");
            Seeds = seeds;
            Attempt = attempt is null or >= 0
                ? attempt
                : throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempts are counted from 0.");
            Device = device ?? throw new ArgumentNullException(nameof(device));
            Client = IsClientName(client)
                ? client
                : throw new ArgumentException($"A client name is 1..{MaxClientLength} lowercase letters, digits or '-'.", nameof(client));
            StartedUtc = startedUtc.ToUniversalTime();
        }

        public LevelId Level { get; }

        /// <summary>The level's move limit, without any "+5 moves" purchase.</summary>
        public int MoveLimit { get; }

        public AttemptSeeds Seeds { get; }

        /// <summary>The 0-based retry number when the seeds were the device's next retry under the level's policy; absent otherwise.</summary>
        public int? Attempt { get; }

        public DeviceId Device { get; }

        /// <summary>The program that played, e.g. "web-lab".</summary>
        public string Client { get; }

        public DateTimeOffset StartedUtc { get; }

        private static bool IsClientName(string? name)
        {
            if (name is null || name.Length == 0 || name.Length > MaxClientLength)
            {
                return false;
            }

            foreach (char c in name)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
