#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The permissions a built package asks for, read from what Android's own tool says of it (<c>aapt dump
    /// permissions puzzle.apk</c>), and held against the permissions the build means it to ask for. The manifest
    /// step (<see cref="ManifestPermissions"/>) writes only the Unity library's manifest; the package's final list is
    /// the merged manifest, where Unity's own lines and every library's meet, so it is read from the package itself:
    /// a permission a library brings in unseen, or one that an earlier build's manifest still withdraws, stops the
    /// build. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class PackagePermissions
    {
        /// <summary>
        /// The permission Unity's own library declares for itself and asks for, under the package's id, so that what it
        /// registers to hear stays the game's own (Android 13 and later): "com.saturnwave.puzzle.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION".
        /// </summary>
        public const string UnitysReceiverSuffix = ".DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION";

        /// <summary>One line of the dump that names a permission the package asks for: <c>uses-permission: name='android.permission.VIBRATE'</c>.</summary>
        private static readonly Regex AskedLine = new Regex(@"^uses-permission(?:-sdk-23)?:\s*name='([^']+)'", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>
        /// The permissions the dump says the package asks for, each once, in the dump's order. The permission a package
        /// only declares (<c>permission: …</c>) is not one it asks for, and is not among them.
        /// </summary>
        /// <exception cref="InvalidOperationException">The text is not such a dump: it names no package.</exception>
        public static IReadOnlyList<string> AskedIn(string dump)
        {
            if (dump == null || !dump.Contains("package:"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's permissions: it names no package.");
            }

            return AskedLine.Matches(dump).Cast<Match>().Select(match => match.Groups[1].Value).Distinct().ToArray();
        }

        /// <summary>Stops a package that asks for other permissions than these, or lacks one of them.</summary>
        /// <exception cref="InvalidOperationException">What the package asks for beyond them, and what of them it does not ask for.</exception>
        public static void Require(IReadOnlyCollection<string> asked, IReadOnlyCollection<string> meant)
        {
            string[] beyond = asked.Except(meant).ToArray();
            string[] lacking = meant.Except(asked).ToArray();
            if (beyond.Length == 0 && lacking.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "The package's merged manifest does not ask for the build's permissions:"
                + (beyond.Length > 0 ? $" it also asks for {string.Join(", ", beyond)}" : string.Empty)
                + (beyond.Length > 0 && lacking.Length > 0 ? ";" : string.Empty)
                + (lacking.Length > 0 ? $" it does not ask for {string.Join(", ", lacking)}" : string.Empty)
                + ". A permission changed in the build's list needs a manifest written anew (the build takes the old one away first);"
                + " one a library brought in is the library's to explain before it ships.");
        }
    }
}
