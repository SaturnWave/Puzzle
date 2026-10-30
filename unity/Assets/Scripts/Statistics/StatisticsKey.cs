#nullable enable
using System;

namespace Puzzle.Client.Statistics
{
    /// <summary>
    /// What the statistics' settings file (unity/Assets/Resources/AptabaseSettings.asset, its App Key) says about where
    /// the play notes may go. The file ships with no key: the owner's Aptabase account gives one ("A-EU-1234567890"),
    /// made in the service's European region, which is what the privacy card and the store declarations promise. A
    /// closed set (private constructor): no key, so nothing can be sent; a key of the European region, through which
    /// the notes go once the player allows them; or a key the game refuses, which sends nothing either.
    /// </summary>
    /// <remarks>
    /// The service's own package reads the same field at the start and sets itself up for any key of its form, of any
    /// region, and for its own placeholder ("A-EU-0000000000", which it writes into a new settings file). The game never
    /// hands a note to the package unless the key reads <see cref="OfTheEu"/> here: a placeholder reads as no key, and a
    /// key of another region (the United States, a server of one's own, a developer's) is refused, so the promise that
    /// the notes are counted in the European Union holds by construction. No Unity dependency.
    /// </remarks>
    public abstract class StatisticsKey
    {
        private const char Separator = '-';
        private const string KeyMark = "A";
        private const string EuRegion = "EU";

        private StatisticsKey()
        {
        }

        /// <summary>What a key's text is, as the settings file holds it; an empty or missing text is no key.</summary>
        public static StatisticsKey Read(string? text)
        {
            string key = text?.Trim() ?? string.Empty;
            if (key.Length == 0)
            {
                return new None();
            }

            string[] parts = key.Split(Separator);
            bool hasTheForm = parts.Length == 3 && parts[0] == KeyMark && parts[1].Length > 0 && parts[2].Length > 0;
            if (!hasTheForm)
            {
                return new Refused($"\"{key}\" is not an App Key of the form A-<region>-<number>");
            }

            if (IsAllZeros(parts[2]))
            {
                return new None(); // the package's own placeholder: nobody's account
            }

            return parts[1] == EuRegion
                ? new OfTheEu(key)
                : (StatisticsKey)new Refused($"the App Key is of the region {parts[1]}, and the game promises its players the European one");
        }

        private static bool IsAllZeros(string digits)
        {
            foreach (char digit in digits)
            {
                if (digit != '0')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The file holds no key, or the package's placeholder: nothing can be sent, and the notes are only written to the device's log.</summary>
        public sealed class None : StatisticsKey
        {
            public override string ToString() => "no App Key";
        }

        /// <summary>A key of the service's European region: the notes the player allows go through it.</summary>
        public sealed class OfTheEu : StatisticsKey
        {
            public OfTheEu(string value)
            {
                Value = string.IsNullOrEmpty(value) ? throw new ArgumentException("A key has a text.", nameof(value)) : value;
            }

            public string Value { get; }

            public override string ToString() => $"the App Key {Value} of the European region";
        }

        /// <summary>A key the game does not send through, and why: nothing is sent, as with no key, and the device's log says so as an error.</summary>
        public sealed class Refused : StatisticsKey
        {
            public Refused(string why)
            {
                Why = why ?? throw new ArgumentNullException(nameof(why));
            }

            public string Why { get; }

            public override string ToString() => $"a refused App Key: {Why}";
        }
    }
}
