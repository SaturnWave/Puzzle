using System;
using System.Globalization;

namespace Puzzle.Meta
{
    /// <summary>
    /// A calendar day where the player is: what "today" means for an allowance that is counted by the day (the free
    /// ads, the daily reward). It is a day of the calendar and not a stretch of 24 hours, so a new day opens at the
    /// player's midnight. Days are compared, never trusted: a rule gives a new allowance only on a later day, so a
    /// day that goes backwards (the phone's date set back) opens nothing.
    /// </summary>
    public readonly struct DayStamp : IEquatable<DayStamp>, IComparable<DayStamp>
    {
        /// <summary>The calendar's last day (31 December 9999), counted like every other.</summary>
        private static readonly int LastNumber = (int)(DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay);

        /// <param name="number">The day counted from the first day of the calendar (1 January of the year 1 is day 0).</param>
        /// <exception cref="ArgumentOutOfRangeException">The calendar has no day of that number.</exception>
        public DayStamp(int number)
        {
            if (number < 0 || number > LastNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, $"The calendar's days are numbered 0 to {LastNumber}.");
            }

            Number = number;
        }

        public int Number { get; }

        /// <summary>The day it is on a clock that shows the player's own time (the phone's local time, with its offset).</summary>
        public static DayStamp Of(DateTimeOffset playersTime) => new DayStamp((int)(playersTime.Date.Ticks / TimeSpan.TicksPerDay));

        /// <summary>The day <paramref name="days"/> later.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is negative, or leads past the calendar's last day.</exception>
        public DayStamp Plus(int days)
        {
            if (days < 0 || days > LastNumber - Number)
            {
                throw new ArgumentOutOfRangeException(nameof(days), days, "A later day is none or more days on, and within the calendar.");
            }

            return new DayStamp(Number + days);
        }

        public int CompareTo(DayStamp other) => Number.CompareTo(other.Number);

        public bool Equals(DayStamp other) => Number == other.Number;

        public override bool Equals(object? obj) => obj is DayStamp other && Equals(other);

        public override int GetHashCode() => Number;

        public override string ToString() => new DateTime(Number * TimeSpan.TicksPerDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static bool operator ==(DayStamp left, DayStamp right) => left.Equals(right);

        public static bool operator !=(DayStamp left, DayStamp right) => !left.Equals(right);

        public static bool operator <(DayStamp left, DayStamp right) => left.Number < right.Number;

        public static bool operator >(DayStamp left, DayStamp right) => left.Number > right.Number;

        public static bool operator <=(DayStamp left, DayStamp right) => left.Number <= right.Number;

        public static bool operator >=(DayStamp left, DayStamp right) => left.Number >= right.Number;
    }
}
