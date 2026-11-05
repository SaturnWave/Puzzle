#nullable enable
using System;
using System.Globalization;
using System.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What a phone downloads of one part of a Google Play bundle, as bundletool reckons it (<c>bundletool get-size
    /// total</c>): the compressed size of the APKs Play would send for that part, the smallest over every kind of
    /// phone and the largest. Google Play takes a bundle whose base module (the code, Unity's libraries and the first
    /// scene) downloads in at most <see cref="PlaysBaseLimitBytes"/>; the game's paintings and the rest of Unity's data
    /// go in the install-time asset pack, which Play installs with the base and limits far higher. This file has no
    /// Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class BundleDownloadSize
    {
        /// <summary>
        /// Google Play's limit on the base module's compressed download, 200 MB, read as 200 million bytes: the stricter
        /// of the two ways "MB" is read, so a base under it is under Play's limit either way.
        /// </summary>
        public const long PlaysBaseLimitBytes = 200L * 1000 * 1000;

        public BundleDownloadSize(long smallestBytes, long largestBytes)
        {
            SmallestBytes = smallestBytes >= 0 ? smallestBytes : throw new ArgumentOutOfRangeException(nameof(smallestBytes), smallestBytes, "A size is not negative.");
            LargestBytes = largestBytes >= smallestBytes ? largestBytes : throw new ArgumentOutOfRangeException(nameof(largestBytes), largestBytes, "The largest size is not below the smallest.");
        }

        /// <summary>The download on the kind of phone that downloads the least.</summary>
        public long SmallestBytes { get; }

        /// <summary>The download on the kind of phone that downloads the most: the one held against Play's limit.</summary>
        public long LargestBytes { get; }

        /// <summary>Whether a base module this size is within Google Play's limit on every kind of phone.</summary>
        public bool IsWithinPlaysBaseLimit => LargestBytes <= PlaysBaseLimitBytes;

        /// <summary>The size as bundletool says it: a header line "MIN,MAX" and a line of two numbers of bytes.</summary>
        /// <exception cref="FormatException">The text is not that.</exception>
        public static BundleDownloadSize Parse(string said)
        {
            string[] lines = (said ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
            string[] sizes = lines.Length == 2 && lines[0] == "MIN,MAX" ? lines[1].Split(',') : Array.Empty<string>();
            if (sizes.Length != 2
                || !long.TryParse(sizes[0], NumberStyles.None, CultureInfo.InvariantCulture, out long smallest)
                || !long.TryParse(sizes[1], NumberStyles.None, CultureInfo.InvariantCulture, out long largest)
                || largest < smallest)
            {
                throw new FormatException($"bundletool's size is not \"MIN,MAX\" and a line of two sizes in bytes: \"{said?.Trim()}\"");
            }

            return new BundleDownloadSize(smallest, largest);
        }

        /// <summary>The size in megabytes of a million bytes, with one decimal: "38.2 MB" or "38.2–41.0 MB".</summary>
        public override string ToString() =>
            SmallestBytes == LargestBytes ? $"{Megabytes(LargestBytes)} MB" : $"{Megabytes(SmallestBytes)}–{Megabytes(LargestBytes)} MB";

        private static string Megabytes(long bytes) => (bytes / 1e6).ToString("F1", CultureInfo.InvariantCulture);
    }
}
