using System.IO;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The 16 KB check of the Google Play bundle's native libraries (2026-10-28): a library's smallest load alignment is
/// read from its ELF program headers, from a stream that cannot seek as a zip entry's, and a bundle with a library
/// aligned to less than 16 KB, or with no library at all, stops the build. <c>NativeLibraryAlignment</c> is the Unity
/// project's Editor source file, compiled into these tests; the bundle build reads every library of the bundle with it
/// (unity/Assets/Editor/PlayBundle.cs).
/// </summary>
public class NativeLibraryAlignmentTests
{
    private const uint Load = 1;
    private const uint Dynamic = 2;
    private const uint StackFlags = 0x6474e551;

    /// <summary>A 64-bit little-endian ELF file of these program headers (type, alignment), the table <paramref name="gap"/> bytes after the header.</summary>
    private static byte[] Library(int gap, params (uint Type, ulong Align)[] segments)
    {
        const int HeaderLength = 64;
        const int EntryLength = 56;
        var bytes = new byte[HeaderLength + gap + (segments.Length * EntryLength) + 32];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        bytes[4] = 2; // 64-bit
        bytes[5] = 1; // little-endian
        BitConverter.GetBytes((ulong)(HeaderLength + gap)).CopyTo(bytes, 0x20);
        BitConverter.GetBytes((ushort)EntryLength).CopyTo(bytes, 0x36);
        BitConverter.GetBytes((ushort)segments.Length).CopyTo(bytes, 0x38);
        for (int at = 0; at < segments.Length; at++)
        {
            int entry = HeaderLength + gap + (at * EntryLength);
            BitConverter.GetBytes(segments[at].Type).CopyTo(bytes, entry);
            BitConverter.GetBytes(segments[at].Align).CopyTo(bytes, entry + 48);
        }

        return bytes;
    }

    /// <summary>A stream that reads, a few bytes at a time, and cannot seek: as a zip entry's.</summary>
    private sealed class OneWay : MemoryStream
    {
        public OneWay(byte[] bytes)
            : base(bytes)
        {
        }

        public override bool CanSeek => false;

        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 7));
    }

    [Fact]
    public void The_smallest_alignment_of_the_loadable_segments_is_read()
    {
        byte[] library = Library(0, (Load, 0x4000), (Dynamic, 8), (Load, 0x10000), (StackFlags, 0));

        Assert.Equal(0x4000, NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(library)));
        Assert.Equal(0x1000, NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(Library(200, (Load, 0x1000), (Load, 0x4000)))));
    }

    [Fact]
    public void Sixteen_kilobytes_and_more_load_and_four_do_not()
    {
        Assert.True(NativeLibraryAlignment.LoadsOnSixteenKilobytePages(16384));
        Assert.True(NativeLibraryAlignment.LoadsOnSixteenKilobytePages(65536));
        Assert.False(NativeLibraryAlignment.LoadsOnSixteenKilobytePages(4096));
    }

    [Fact]
    public void A_bundle_with_a_library_for_four_kilobyte_pages_or_none_stops_the_build()
    {
        var aligned = new Dictionary<string, long> { ["base/lib/arm64-v8a/libunity.so"] = 16384, ["base/lib/arm64-v8a/libil2cpp.so"] = 65536 };
        var one = new Dictionary<string, long>(aligned) { ["base/lib/arm64-v8a/libplugin.so"] = 4096 };

        NativeLibraryAlignment.Require(aligned);
        Assert.Contains("libplugin.so (4096 bytes) is aligned to less than 16 KB", Assert.Throws<InvalidOperationException>(() => NativeLibraryAlignment.Require(one)).Message);
        Assert.Throws<InvalidOperationException>(() => NativeLibraryAlignment.Require(new Dictionary<string, long>()));
    }

    [Fact]
    public void What_is_not_a_64_bit_library_is_not_read()
    {
        byte[] thirtyTwo = Library(0, (Load, 0x4000));
        thirtyTwo[4] = 1;
        byte[] notElf = Library(0, (Load, 0x4000));
        notElf[1] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(thirtyTwo)));
        Assert.Throws<InvalidDataException>(() => NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(notElf)));
        Assert.Throws<InvalidDataException>(() => NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(Library(0, (Dynamic, 8)))));
        Assert.Throws<InvalidDataException>(() => NativeLibraryAlignment.SmallestLoadAlignment(new OneWay(Library(0, (Load, 0x4000)).Take(80).ToArray())));
    }
}
