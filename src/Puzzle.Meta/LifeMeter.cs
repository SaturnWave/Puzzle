using System;
using System.Globalization;

namespace Puzzle.Meta
{
    /// <summary>How many lives a player can hold and how long one takes to come back (the settings' lives).</summary>
    public sealed class LifeRules : IEquatable<LifeRules>
    {
        /// <exception cref="ArgumentOutOfRangeException">Fewer than one life, or a life that takes no time to come back.</exception>
        public LifeRules(int max, TimeSpan perLife)
        {
            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "A player can hold at least one life.");
            }

            if (perLife <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(perLife), perLife, "A life takes some time to come back.");
            }

            Max = max;
            PerLife = perLife;
        }

        public int Max { get; }

        /// <summary>One life comes back after this long.</summary>
        public TimeSpan PerLife { get; }

        public bool Equals(LifeRules? other) => other is not null && Max == other.Max && PerLife == other.PerLife;

        public override bool Equals(object? obj) => Equals(obj as LifeRules);

        public override int GetHashCode() => HashCode.Combine(Max, PerLife);

        public override string ToString() => $"{Max} lives, one back every {PerLife.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} minutes";
    }

    /// <summary>
    /// A player's lives (docs/GAME_DESIGN.md section 19): full at the start, one lost with every lost attempt, one
    /// back after a fixed time while below the maximum. The clock of the next life starts with the loss that takes
    /// the meter below full and is not started again by a later loss.
    /// </summary>
    /// <remarks>
    /// The meter holds how many lives are missing, not how many are there, so the default value and a wallet saved
    /// before lives existed are both full, and a full meter stays full when the settings give more lives. It knows no
    /// clock: the time is handed to each question, and a time earlier than one it has seen (the phone's clock set
    /// back) brings no life back.
    /// </remarks>
    public readonly struct LifeMeter : IEquatable<LifeMeter>
    {
        /// <summary>When the next life comes back; meaningful only while a life is missing.</summary>
        private readonly DateTimeOffset _nextLifeAt;

        /// <param name="shortOfFull">How many lives are missing.</param>
        /// <param name="nextLifeAt">When the next one comes back: stated exactly when a life is missing.</param>
        /// <exception cref="ArgumentOutOfRangeException">The number of missing lives is negative.</exception>
        /// <exception cref="ArgumentException">A life is missing and no time is given for it, or none is missing and a time is.</exception>
        public LifeMeter(int shortOfFull, DateTimeOffset? nextLifeAt)
        {
            if (shortOfFull < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shortOfFull), shortOfFull, "A meter cannot hold more lives than a full one.");
            }

            if (nextLifeAt.HasValue != shortOfFull > 0)
            {
                throw new ArgumentException("The time of the next life is stated exactly when a life is missing.", nameof(nextLifeAt));
            }

            ShortOfFull = shortOfFull;
            _nextLifeAt = nextLifeAt ?? default;
        }

        /// <summary>Every life there: a new player.</summary>
        public static LifeMeter Full => default;

        public int ShortOfFull { get; }

        public bool IsFull => ShortOfFull == 0;

        /// <summary>When the next life comes back; absent while the meter is full.</summary>
        public DateTimeOffset? NextLifeAt => IsFull ? (DateTimeOffset?)null : _nextLifeAt;

        /// <summary>How many lives are there, as of the last time the meter was settled.</summary>
        public int LivesUnder(LifeRules rules) => Math.Max(0, rules.Max - ShortOfFull);

        public bool HasOneUnder(LifeRules rules) => LivesUnder(rules) > 0;

        /// <summary>How long from <paramref name="now"/> until the next life comes back; absent while the meter is full.</summary>
        public TimeSpan? UntilNextLife(DateTimeOffset now)
        {
            if (IsFull)
            {
                return null;
            }

            return _nextLifeAt > now ? _nextLifeAt - now : TimeSpan.Zero;
        }

        /// <summary>
        /// The meter with every life that has come back by <paramref name="now"/>. A time before the next life is
        /// due changes nothing, which is also what a clock that was set back gets.
        /// </summary>
        public LifeMeter SettledAt(DateTimeOffset now, LifeRules rules)
        {
            // Saved under settings with more lives: a meter is never emptier than empty.
            int missing = Math.Min(ShortOfFull, rules.Max);
            if (missing == 0)
            {
                return Full;
            }

            if (now < _nextLifeAt)
            {
                return new LifeMeter(missing, _nextLifeAt);
            }

            long cameBack = 1 + ((now - _nextLifeAt).Ticks / rules.PerLife.Ticks);
            return cameBack >= missing
                ? Full
                : new LifeMeter(missing - (int)cameBack, _nextLifeAt + TimeSpan.FromTicks(cameBack * rules.PerLife.Ticks));
        }

        /// <summary>
        /// The meter after a lost attempt took a life at <paramref name="now"/>. A life that fell due while the
        /// attempt was played is back first: a meter that was full by then had no clock, and this loss starts one.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no life to lose: the attempt should not have started.</exception>
        public LifeMeter WithOneLost(DateTimeOffset now, LifeRules rules)
        {
            LifeMeter settled = SettledAt(now, rules);
            if (!settled.HasOneUnder(rules))
            {
                throw new InvalidOperationException("An attempt was lost without a life to lose.");
            }

            return new LifeMeter(settled.ShortOfFull + 1, settled.IsFull ? now + rules.PerLife : settled._nextLifeAt);
        }

        public bool Equals(LifeMeter other) => ShortOfFull == other.ShortOfFull && _nextLifeAt == other._nextLifeAt;

        public override bool Equals(object? obj) => obj is LifeMeter other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ShortOfFull, _nextLifeAt);

        public override string ToString() => IsFull
            ? "full"
            : $"{ShortOfFull} short, next at {_nextLifeAt.ToString("u", CultureInfo.InvariantCulture)}";

        public static bool operator ==(LifeMeter left, LifeMeter right) => left.Equals(right);

        public static bool operator !=(LifeMeter left, LifeMeter right) => !left.Equals(right);
    }
}
