#nullable enable
using System;
using Puzzle.Client.Statistics;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The one error line Aptabase's package writes at every start of a build whose settings file holds no App Key:
    /// "[AptabaseAnalytics] The Aptabase App Key  is invalid. Tracking will be disabled". It is the package saying that
    /// it set nothing up and sends nothing, which is what a build without the owner's key is to be; the package offers
    /// no way to start without the line. The checks that run the game in play mode (PlayModeCheck, MusicCheck) do not
    /// count it as the game's error while the settings file holds no key; with a key in the file, the line counts as any
    /// other error.
    /// </summary>
    internal static class KeylessStatisticsNotice
    {
        private const string Beginning = "[AptabaseAnalytics] The Aptabase App Key ";
        private const string Ending = " is invalid. Tracking will be disabled";

        /// <summary>True for the package's line of a missing key, in a project whose settings file holds no key.</summary>
        public static bool Is(string message) =>
            message.StartsWith(Beginning, StringComparison.Ordinal)
            && message.EndsWith(Ending, StringComparison.Ordinal)
            && AptabaseNotes.KeyOfThisBuild() is StatisticsKey.None;
    }
}
