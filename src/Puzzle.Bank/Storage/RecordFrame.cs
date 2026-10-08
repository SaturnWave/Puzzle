using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Puzzle.Bank.Storage;

/// <summary>What a record of the bank holds. Closed set; a number is never reused for another meaning.</summary>
public enum RecordType : byte
{
    /// <summary>First record of every shard file: which bank it belongs to and which tools may read it.</summary>
    Header = 1,

    /// <summary>An epoch of the measurement: the fingerprint of the rules and the bot code its games were played with.</summary>
    Epoch = 2,

    /// <summary>A candidate: its content, its play seeds, its place on the amount grid and where it came from.</summary>
    Board = 3,

    /// <summary>A batch of games one player played on one candidate.</summary>
    Games = 4,

    /// <summary>What one draw of the grower came to, stored or not: the resume cursor and the throughput statistics.</summary>
    Draw = 5,

    /// <summary>Candidates withdrawn from the bank; their records stay on disk and are no longer read.</summary>
    Retract = 6,
}

/// <summary>
/// The frame around every record: the magic <c>PZB1</c>, the type, a reserved 24 bits, the payload length, the
/// payload, and the first eight bytes of a SHA-256 over all of that. A record counts only when it is whole and its
/// checksum holds, so a write cut short by a pulled cable leaves nothing a reader could mistake for a record.
/// </summary>
public static class RecordFrame
{
    public const int HeaderLength = 12;

    public const int ChecksumLength = 8;

    /// <summary>The largest payload a reader accepts; a length beyond it is damage, not a record.</summary>
    public const int LargestPayload = 16 * 1024 * 1024;

    private const int TypeOffset = 4;
    private const int LengthOffset = 8;

    public static ReadOnlySpan<byte> Magic => "PZB1"u8;

    public static byte[] Encode(RecordType type, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > LargestPayload)
        {
            throw new ArgumentException($"A record payload holds at most {LargestPayload} bytes, not {payload.Length}.", nameof(payload));
        }

        var frame = new byte[HeaderLength + payload.Length + ChecksumLength];
        Magic.CopyTo(frame);
        frame[TypeOffset] = (byte)type;
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(LengthOffset), (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderLength));
        int checkedLength = HeaderLength + payload.Length;
        ChecksumOf(frame.AsSpan(0, checkedLength)).CopyTo(frame.AsSpan(checkedLength));
        return frame;
    }

    /// <summary>
    /// Whether a whole, sound record starts at the beginning of <paramref name="bytes"/>; its type code and the
    /// length of its payload if so.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> bytes, out byte typeCode, out int payloadLength)
    {
        typeCode = 0;
        payloadLength = 0;
        if (DeclaredLength(bytes) is not int length || bytes.Length < HeaderLength + length + ChecksumLength)
        {
            return false;
        }

        int checkedLength = HeaderLength + length;
        if (!ChecksumOf(bytes[..checkedLength]).SequenceEqual(bytes.Slice(checkedLength, ChecksumLength)))
        {
            return false;
        }

        typeCode = bytes[TypeOffset];
        payloadLength = length;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="bytes"/>, the end of a file, look like a record whose writing was cut short: the
    /// beginning of the magic, or a header that announces more bytes than the file has.
    /// </summary>
    public static bool IsCutOffRecord(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Magic.Length)
        {
            return Magic.StartsWith(bytes);
        }

        if (!bytes.StartsWith(Magic))
        {
            return false;
        }

        return bytes.Length < HeaderLength || (DeclaredLength(bytes) is int length && bytes.Length < HeaderLength + length + ChecksumLength);
    }

    /// <summary>
    /// How many bytes the record that starts with <paramref name="header"/> takes in all, so one record can be read
    /// out of a large file; absent when those <see cref="HeaderLength"/> bytes are no plausible header.
    /// </summary>
    public static int? WholeLength(ReadOnlySpan<byte> header) =>
        DeclaredLength(header) is int payload ? HeaderLength + payload + ChecksumLength : null;

    /// <summary>The payload length a header at the start of <paramref name="bytes"/> announces; absent without a plausible header.</summary>
    private static int? DeclaredLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength || !bytes.StartsWith(Magic))
        {
            return null;
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[LengthOffset..]);
        return length <= LargestPayload ? (int)length : null;
    }

    private static byte[] ChecksumOf(ReadOnlySpan<byte> headerAndPayload) => SHA256.HashData(headerAndPayload)[..ChecksumLength];
}
