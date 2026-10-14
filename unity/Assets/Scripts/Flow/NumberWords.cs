#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// Numbers as the game's screens write them, in the player's language: an amount of coins with its thousands
    /// set apart ("1.250", as the design canvas writes the wallet on HomeTR), and how long something is still
    /// away, short ("12:34") or long ("1 sa 42 dk"). No Unity dependency.
    /// </summary>
    public static class NumberWords
    {
        private const char ThousandsMark = '.';
        private const int DigitsInAGroup = 3;
        private const string Hours = "sa";
        private const string Minutes = "dk";

        /// <summary>An amount with a point between its thousands, as Turkish writes them: "950", "1.250", "1.000.000".</summary>
        /// <exception cref="ArgumentOutOfRangeException">The amount is less than nothing: the game counts no debt.</exception>
        public static string Amount(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount on a screen is nothing or more.");
            }

            string digits = amount.ToString(CultureInfo.InvariantCulture);
            var grouped = new StringBuilder(digits.Length + digits.Length / DigitsInAGroup);
            for (int digit = 0; digit < digits.Length; digit++)
            {
                bool beginsAGroup = digit > 0 && (digits.Length - digit) % DigitsInAGroup == 0;
                if (beginsAGroup)
                {
                    grouped.Append(ThousandsMark);
                }

                grouped.Append(digits[digit]);
            }

            return grouped.ToString();
        }

        /// <summary>
        /// A short wait as minutes and seconds, each with two figures: "12:34", "00:07". A second that has begun
        /// counts whole, so the wait reads "00:00" only when nothing of it is left; a wait of an hour or more goes
        /// on counting minutes ("75:00"), and one that is over stays at "00:00".
        /// </summary>
        public static string Countdown(TimeSpan left)
        {
            long seconds = WholeUnitsBegun(left, TimeSpan.TicksPerSecond);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", seconds / 60, seconds % 60);
        }

        /// <summary>
        /// A long wait in hours and minutes: "1 sa 42 dk", "2 sa" on the hour, "42 dk" under an hour. A minute
        /// that has begun counts whole; a wait that is over reads "0 dk".
        /// </summary>
        public static string HoursAndMinutes(TimeSpan left)
        {
            long minutes = WholeUnitsBegun(left, TimeSpan.TicksPerMinute);
            long hours = minutes / 60;
            long minutesOverTheHour = minutes % 60;
            if (hours == 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} {1}", minutesOverTheHour, Minutes);
            }

            return minutesOverTheHour == 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} {1}", hours, Hours)
                : string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", hours, Hours, minutesOverTheHour, Minutes);
        }

        /// <summary>How many units of a length a time span holds when one that has begun counts whole; none for a span that is over.</summary>
        private static long WholeUnitsBegun(TimeSpan span, long ticksInAUnit) =>
            span <= TimeSpan.Zero ? 0 : (span.Ticks + ticksInAUnit - 1) / ticksInAUnit;
    }
}
