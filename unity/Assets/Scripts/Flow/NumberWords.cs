#nullable enable
using System;
using System.Globalization;
using System.Text;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// Numbers as the game's screens write them, in the language the game speaks (<see cref="Speech"/>): an amount of
    /// coins with its thousands set apart as the language sets them ("1.250" in Turkish, as the design canvas writes
    /// the wallet on HomeTR; "1,250" in English and Korean), and how long something is still away, short ("12:34")
    /// or long ("1 sa 42 dk", "1h 42m", "1시간 42분"). No Unity dependency.
    /// </summary>
    public static class NumberWords
    {
        private const int DigitsInAGroup = 3;

        /// <summary>An amount with the language's mark between its thousands: "950", "1.250", "1.000.000" in Turkish.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The amount is less than nothing: the game counts no debt.</exception>
        public static string Amount(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount on a screen is nothing or more.");
            }

            char thousandsMark = Languages.ThousandsMarkOf(Speech.Language);
            string digits = amount.ToString(CultureInfo.InvariantCulture);
            var grouped = new StringBuilder(digits.Length + digits.Length / DigitsInAGroup);
            for (int digit = 0; digit < digits.Length; digit++)
            {
                bool beginsAGroup = digit > 0 && (digits.Length - digit) % DigitsInAGroup == 0;
                if (beginsAGroup)
                {
                    grouped.Append(thousandsMark);
                }

                grouped.Append(digits[digit]);
            }

            return grouped.ToString();
        }

        /// <summary>
        /// A short wait as minutes and seconds, each with two figures: "12:34", "00:07", the same in every language.
        /// A second that has begun counts whole, so the wait reads "00:00" only when nothing of it is left; a wait of
        /// an hour or more goes on counting minutes ("75:00"), and one that is over stays at "00:00".
        /// </summary>
        public static string Countdown(TimeSpan left)
        {
            long seconds = WholeUnitsBegun(left, TimeSpan.TicksPerSecond);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", seconds / 60, seconds % 60);
        }

        /// <summary>
        /// A long wait in hours and minutes, as the language shortens them: "1 sa 42 dk", "2 sa" on the hour, "42 dk"
        /// under an hour. A minute that has begun counts whole; a wait that is over reads "0 dk".
        /// </summary>
        public static string HoursAndMinutes(TimeSpan left)
        {
            long minutes = WholeUnitsBegun(left, TimeSpan.TicksPerMinute);
            string hours = Number(minutes / 60);
            string minutesOverTheHour = Number(minutes % 60);
            if (minutes / 60 == 0)
            {
                return Speech.Say(WordId.DurationMinutes, ("minutes", minutesOverTheHour));
            }

            return minutes % 60 == 0
                ? Speech.Say(WordId.DurationHours, ("hours", hours))
                : Speech.Say(WordId.DurationHoursAndMinutes, ("hours", hours), ("minutes", minutesOverTheHour));
        }

        /// <summary>A number of seconds as the language shortens them: "7 sn", "7s", "7초".</summary>
        public static string Seconds(int seconds) => Speech.Say(WordId.DurationSeconds, ("seconds", Number(seconds)));

        /// <summary>How many units of a length a time span holds when one that has begun counts whole; none for a span that is over.</summary>
        private static long WholeUnitsBegun(TimeSpan span, long ticksInAUnit) =>
            span <= TimeSpan.Zero ? 0 : (span.Ticks + ticksInAUnit - 1) / ticksInAUnit;

        private static string Number(long number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
