using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Puzzle.Bank.Candidates;

/// <summary>
/// The identity of a candidate: 128 bits of a SHA-256 over its canonical text (<see cref="CanonicalText"/>). It is
/// given once, when the candidate enters the bank, and stored with it; nothing recomputes the id of a stored record.
/// </summary>
public readonly record struct CandidateId(UInt128 Value)
{
    public const int ByteLength = ContentHash.ByteLength;

    public static CandidateId Parse(string hex) => new(ContentHash.Parse(hex, nameof(CandidateId)));

    public static CandidateId Read(ReadOnlySpan<byte> bytes) => new(BinaryPrimitives.ReadUInt128BigEndian(bytes));

    public void Write(Span<byte> bytes) => BinaryPrimitives.WriteUInt128BigEndian(bytes, Value);

    /// <summary>The first 64 bits, as a seed for what is derived from the identity (a candidate's play seeds).</summary>
    public ulong Seed => (ulong)(Value >> 64);

    public override string ToString() => ContentHash.Format(Value);
}

/// <summary>
/// The identity of a board: what a player sees as the same level whatever amounts its goals ask for. Candidates with
/// one board id are siblings (<see cref="AmountGrid"/>); an assembly uses a board once.
/// </summary>
public readonly record struct BoardId(UInt128 Value)
{
    public static BoardId Parse(string hex) => new(ContentHash.Parse(hex, nameof(BoardId)));

    public override string ToString() => ContentHash.Format(Value);
}

/// <summary>128-bit content hashes, written as 32 lower-case hex digits.</summary>
internal static class ContentHash
{
    public const int ByteLength = 16;

    public static UInt128 Of(string canonicalText) =>
        BinaryPrimitives.ReadUInt128BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText)));

    public static string Format(UInt128 value) => value.ToString("x32", CultureInfo.InvariantCulture);

    /// <exception cref="FormatException">The text is not 32 hex digits.</exception>
    public static UInt128 Parse(string hex, string what) =>
        hex.Length == ByteLength * 2 && UInt128.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out UInt128 value)
            ? value
            : throw new FormatException($"'{hex}' is not a {what}: 32 hex digits are expected.");
}
