#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Whether the game's native libraries load on a phone whose memory pages are 16 KB: Google Play takes an app that
    /// targets Android 15 or later only when every 64-bit library it carries is built so (since 1 November 2025). A
    /// library is when each of its loadable segments (an ELF program header of type PT_LOAD) is aligned to 16 KB or more:
    /// the loader maps the segments page by page, and a segment aligned to 4 KB cannot start on a 16 KB page. Unity 6
    /// builds its own libraries so; a plugin's library might not be, and the bundle build reads every one
    /// (unity/Assets/Editor/PlayBundle.cs). How the libraries lie in the zip of an installed APK is bundletool's concern (Play's
    /// own bundletool aligns what it leaves uncompressed), not the library's. This file has no Unity dependency, so the
    /// repository's tests compile it too.
    /// </summary>
    public static class NativeLibraryAlignment
    {
        /// <summary>The page size the loadable segments must allow for, in bytes.</summary>
        public const long SixteenKilobytes = 16 * 1024;

        private const int HeaderLength = 64;
        private const int ProgramHeaderLength = 56;
        private const uint LoadableSegment = 1; // PT_LOAD

        /// <summary>
        /// The smallest alignment, in bytes, of the library's loadable segments, read from its ELF header and program
        /// headers. The library is read from its start onwards and no further than the program headers, so a stream
        /// that cannot seek (a zip entry's) will do.
        /// </summary>
        /// <exception cref="InvalidDataException">The stream is not a 64-bit little-endian ELF file, ends early, or has no loadable segment.</exception>
        public static long SmallestLoadAlignment(Stream library)
        {
            byte[] header = ReadExactly(library, HeaderLength, "its ELF header");
            if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
            {
                throw new InvalidDataException("the file is not an ELF library: it does not start with 0x7F 'ELF'");
            }

            if (header[4] != 2 || header[5] != 1)
            {
                throw new InvalidDataException("the library is not 64-bit little-endian ELF, the only kind an arm64-v8a package carries");
            }

            long programHeaders = (long)ReadUInt64(header, 0x20);
            int entryLength = ReadUInt16(header, 0x36);
            int entries = ReadUInt16(header, 0x38);
            if (entryLength < ProgramHeaderLength || programHeaders < HeaderLength)
            {
                throw new InvalidDataException($"the library's program headers are not where a 64-bit ELF file has them (at {programHeaders}, {entryLength} bytes each)");
            }

            Skip(library, programHeaders - HeaderLength);
            byte[] table = ReadExactly(library, entries * entryLength, "its program headers");
            long[] loadable = Enumerable.Range(0, entries)
                .Where(entry => ReadUInt32(table, entry * entryLength) == LoadableSegment)
                .Select(entry => (long)ReadUInt64(table, (entry * entryLength) + 48))
                .ToArray();
            return loadable.Length > 0 ? loadable.Min() : throw new InvalidDataException("the library has no loadable segment");
        }

        /// <summary>Whether a library whose loadable segments are aligned so loads on a phone with 16 KB pages.</summary>
        public static bool LoadsOnSixteenKilobytePages(long smallestLoadAlignment) => smallestLoadAlignment >= SixteenKilobytes;

        /// <summary>Stops a bundle that carries a library aligned for smaller pages.</summary>
        /// <param name="alignments">Every native library of the bundle by its path in the bundle, with its smallest load alignment.</param>
        /// <exception cref="InvalidOperationException">The bundle carries no library at all, or one aligned to less than 16 KB.</exception>
        public static void Require(IReadOnlyDictionary<string, long> alignments)
        {
            if (alignments.Count == 0)
            {
                throw new InvalidOperationException("the bundle carries no native library, where a Unity game carries at least libunity.so and libil2cpp.so");
            }

            string[] misaligned = alignments.Where(library => !LoadsOnSixteenKilobytePages(library.Value)).Select(library => $"{library.Key} ({library.Value} bytes)").ToArray();
            if (misaligned.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{string.Join(", ", misaligned)} {(misaligned.Length == 1 ? "is" : "are")} aligned to less than 16 KB: Google Play refuses an app for Android 15 and later "
                    + "whose 64-bit libraries do not load on 16 KB pages. The library is its maker's to rebuild (a newer version of the plugin that brings it).");
            }
        }

        private static byte[] ReadExactly(Stream stream, int count, string what)
        {
            var bytes = new byte[count];
            int read = 0;
            while (read < count)
            {
                int got = stream.Read(bytes, read, count - read);
                if (got == 0)
                {
                    throw new InvalidDataException($"the library ends before {what}");
                }

                read += got;
            }

            return bytes;
        }

        private static void Skip(Stream stream, long count)
        {
            var buffer = new byte[4096];
            while (count > 0)
            {
                int got = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (got == 0)
                {
                    throw new InvalidDataException("the library ends before its program headers");
                }

                count -= got;
            }
        }

        private static int ReadUInt16(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

        private static uint ReadUInt32(byte[] bytes, int at) => (uint)(bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24));

        private static ulong ReadUInt64(byte[] bytes, int at) => ReadUInt32(bytes, at) | ((ulong)ReadUInt32(bytes, at + 4) << 32);
    }
}
