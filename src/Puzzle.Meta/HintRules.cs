using System;
using System.Globalization;

namespace Puzzle.Meta
{
    /// <summary>
    /// When the game shows a hint by itself, how many an attempt, and whose move it shows (the settings' hints). When
    /// is the client's to tell (a board at rest, untouched for <see cref="Idle"/>), and the move is Puzzle.Bot's
    /// HintAdvisor's, which this assembly does not reference: <see cref="Bot"/> is the name the advisor is made by, and
    /// the economy simulator's loader is what checks that it names a player model.
    /// </summary>
    public sealed class HintRules : IEquatable<HintRules>
    {
        /// <exception cref="ArgumentOutOfRangeException">A wait under a second, or fewer than no hints.</exception>
        /// <exception cref="ArgumentException">No bot is named.</exception>
        public HintRules(TimeSpan idle, int perAttempt, string bot)
        {
            if (idle < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(idle), idle, "A hint waits at least a second for the player's own move.");
            }

            if (perAttempt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perAttempt), perAttempt, "An attempt shows none or more hints.");
            }

            Idle = idle;
            PerAttempt = perAttempt;
            Bot = string.IsNullOrWhiteSpace(bot) ? throw new ArgumentException("A hint shows the move of a bot, which must be named.", nameof(bot)) : bot;
        }

        /// <summary>How long a board at rest lies untouched before a hint shows itself; every move and every touch starts it again.</summary>
        public TimeSpan Idle { get; }

        /// <summary>Hints an attempt shows at most; 0 shows none.</summary>
        public int PerAttempt { get; }

        /// <summary>The name of the player model of the bot family whose move a hint shows: "casual", "skill-60".</summary>
        public string Bot { get; }

        public bool Equals(HintRules? other) => other is not null && Idle == other.Idle && PerAttempt == other.PerAttempt && Bot == other.Bot;

        public override bool Equals(object? obj) => Equals(obj as HintRules);

        public override int GetHashCode() => HashCode.Combine(Idle, PerAttempt, Bot);

        public override string ToString() =>
            $"up to {PerAttempt} hints an attempt by {Bot}, after {Idle.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s untouched";
    }
}
