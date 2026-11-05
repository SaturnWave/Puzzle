#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>Google Play's tracks a bundle can be released on. A closed set.</summary>
    public enum PlayTrack
    {
        /// <summary>Internal testing: up to a hundred testers, no review.</summary>
        Internal,

        /// <summary>Closed testing: the testers on the track's lists (a personal account's twelve testers for fourteen days, before production).</summary>
        Closed,

        /// <summary>Open testing: anyone who joins from the store page.</summary>
        Open,

        /// <summary>Production: every player of the store.</summary>
        Production,
    }

    /// <summary>One bundle Google Play has taken: its versionCode, the versionName players see, the day it was uploaded and the track it went to.</summary>
    public sealed class PlayUpload
    {
        /// <summary>The highest versionCode Google Play takes.</summary>
        public const int HighestVersionCode = 2100000000;

        public PlayUpload(int versionCode, string versionName, DateTime day, PlayTrack track)
        {
            VersionCode = versionCode >= 1 && versionCode <= HighestVersionCode
                ? versionCode
                : throw new ArgumentOutOfRangeException(nameof(versionCode), versionCode, $"Google Play takes a versionCode from 1 to {HighestVersionCode}.");
            VersionName = string.IsNullOrWhiteSpace(versionName) ? throw new ArgumentException("An upload has a versionName.", nameof(versionName)) : versionName;
            Day = day.Date;
            Track = track;
        }

        public int VersionCode { get; }

        /// <summary>What the store and the game's entry page show ("1.0"): Unity's bundleVersion when the bundle was built.</summary>
        public string VersionName { get; }

        public DateTime Day { get; }

        public PlayTrack Track { get; }

        public override string ToString() => $"versionCode {VersionCode} ({VersionName}), uploaded {Day:yyyy-MM-dd} to the {Track.ToString().ToLowerInvariant()} track";
    }

    /// <summary>
    /// The record of every bundle uploaded to Google Play (release/google-play-uploads.txt, tracked in the repository),
    /// and the versionCode the next bundle is built with. Google Play takes a versionCode once, for good, and after it
    /// only a higher one: a code is used up as soon as Play Console has taken the file, even when the release it went
    /// into is then thrown away. So the bundle build (unity/Assets/Editor/PlayBundle.cs) never takes a code from anyone: it builds
    /// one more than the last line of the record, and the first bundle, with no line yet, is versionCode 1. A bundle
    /// built again before it is uploaded gets the same code, which is as it should be; the line is added, and committed,
    /// once Play Console has taken the upload (docs/PLAY_RELEASE.md). A record whose codes do not rise line by line is
    /// wrong somewhere and stops the build, as does a line that is not "versionCode versionName day track".
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class PlayUploads
    {
        /// <summary>The record, from the Unity project's folder, where the Editor's tools run.</summary>
        public const string RecordPath = "../release/google-play-uploads.txt";

        private const string DayFormat = "yyyy-MM-dd";

        /// <summary>The uploads the record names, oldest first. A line that starts with # and an empty line are not uploads.</summary>
        /// <exception cref="FormatException">A line is not an upload, or its code does not rise above the line before.</exception>
        public static IReadOnlyList<PlayUpload> Read(string record)
        {
            var uploads = new List<PlayUpload>();
            string[] lines = (record ?? throw new ArgumentNullException(nameof(record))).Split('\n');
            for (int at = 0; at < lines.Length; at++)
            {
                string line = lines[at].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                PlayUpload upload = UploadOn(line, at + 1);
                if (uploads.Count > 0 && upload.VersionCode <= uploads[uploads.Count - 1].VersionCode)
                {
                    throw new FormatException(
                        $"Line {at + 1} of the record of uploads names versionCode {upload.VersionCode}, which does not rise above the line before ({uploads[uploads.Count - 1].VersionCode}): "
                        + "Google Play takes only a higher code after one, so the record is wrong somewhere.");
                }

                uploads.Add(upload);
            }

            return uploads;
        }

        /// <summary>The versionCode of the next bundle: one more than the last upload's, 1 when nothing has been uploaded.</summary>
        /// <exception cref="InvalidOperationException">The last upload took Google Play's highest code.</exception>
        public static int NextVersionCode(IReadOnlyList<PlayUpload> uploads)
        {
            if (uploads.Count == 0)
            {
                return 1;
            }

            int last = uploads[uploads.Count - 1].VersionCode;
            return last < PlayUpload.HighestVersionCode
                ? last + 1
                : throw new InvalidOperationException($"The last upload took versionCode {last}, the highest Google Play takes: no bundle can follow it.");
        }

        /// <summary>The record's line for an upload, as it is to be added: "2 1.0 2026-11-02 closed".</summary>
        public static string LineOf(PlayUpload upload) =>
            $"{upload.VersionCode} {upload.VersionName} {upload.Day.ToString(DayFormat, CultureInfo.InvariantCulture)} {upload.Track.ToString().ToLowerInvariant()}";

        private static PlayUpload UploadOn(string line, int number)
        {
            string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 4
                || !int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out int code)
                || code < 1 || code > PlayUpload.HighestVersionCode
                || !DateTime.TryParseExact(words[2], DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day)
                || !(TrackNamed(words[3]) is PlayTrack track))
            {
                throw new FormatException(
                    $"Line {number} of the record of uploads (\"{line}\") is not \"versionCode versionName day track\", such as \"1 1.0 2026-10-28 closed\": "
                    + $"a code from 1 to {PlayUpload.HighestVersionCode}, a day written {DayFormat}, and one of {string.Join(", ", AllTracks.Select(Lower))}.");
            }

            return new PlayUpload(code, words[1], day, track);
        }

        private static IEnumerable<PlayTrack> AllTracks => (PlayTrack[])Enum.GetValues(typeof(PlayTrack));

        private static string Lower(PlayTrack track) => track.ToString().ToLowerInvariant();

        /// <summary>The track a word of the record names; none when it names none.</summary>
        private static PlayTrack? TrackNamed(string word)
        {
            foreach (PlayTrack track in AllTracks)
            {
                if (string.Equals(Lower(track), word, StringComparison.OrdinalIgnoreCase))
                {
                    return track;
                }
            }

            return null;
        }
    }
}
