using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;

namespace Puzzle.Bank.Storage;

/// <summary>The bank's files are not a bank this build can read; the message says which file or record and why.</summary>
public sealed class BankFormatException : Exception
{
    public BankFormatException(string message)
        : base(message)
    {
    }

    public BankFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Which bank a shard belongs to and which tools may read it; the first record of every shard file.</summary>
/// <param name="MinReaderVersion">
/// The oldest reader that understands everything in the file. A tool whose <see cref="BankFormat.ReaderVersion"/> is
/// lower refuses the bank; it must not skip what it does not know and work from a part.
/// </param>
/// <param name="BankSeed">The root of every draw seed of this bank.</param>
public sealed record HeaderRecord(int FormatVersion, int MinReaderVersion, string BankId, ulong BankSeed, DateTime CreatedUtc);

/// <summary>An epoch as stored. The build that opened it is described for a person to read; only the fingerprint and the generation identify it.</summary>
public sealed record EpochRecord(string Epoch, string Fingerprint, int Generation, DateTime OpenedUtc, string Runtime, string CoreAssembly, string BotAssembly, string Note);

/// <summary>Where a candidate stands on its board's amount grid.</summary>
public sealed record AmountsRecord(int Step, int[] Collect, int SpawnedItems);

public sealed record DrawSpecRecord(string Size, bool Plain, int Colors, string[] Families, int CollectGoals, int DensityStep, bool FullJelly);

/// <summary>Where a candidate came from: <c>drawn</c> (with the draw that made it) or <c>imported</c> (with the level file it was read from).</summary>
public sealed record OriginRecord(string Kind, long? Draw, int? Variant, ulong? Seed, DrawSpecRecord? Spec, int? DrawCode, string? ImportedFrom);

public sealed record BoardRecord(string Id, string Board, JsonElement Level, uint[] Seeds, AmountsRecord Amounts, OriginRecord Origin, DateTime CreatedUtc);

/// <summary>How one variant of a draw ended. Closed set.</summary>
public enum DrawOutcome
{
    /// <summary>Measured on the whole ladder and stored.</summary>
    Stored,

    /// <summary>The bank already holds this very candidate.</summary>
    Duplicate,

    /// <summary>The dice gave no usable board of the kind.</summary>
    Unusable,

    /// <summary>The board came out as another kind than the one aimed at (a featured strength missing, say).</summary>
    OffTarget,

    /// <summary>A few probe games put its limit outside the range aimed at; not measured, the amount was moved instead.</summary>
    ProbedOut,

    /// <summary>The level contradicts itself or cannot start: a fault of the drawing code, kept as evidence.</summary>
    Invalid,
}

/// <param name="Draw">The draw's number; the next draw of the bank is one past the largest stored.</param>
/// <param name="Variant">Which try of the draw: the amount moves between tries.</param>
/// <param name="ProbeLimit">The limit the probe games gave the aimed-at player; absent when none were played or the share was out of reach.</param>
/// <param name="Limit">The aimed-at player's draft limit on the stored games.</param>
/// <param name="Fit">A candidate was stored and that limit lies in the range aimed at: the draw gave its target a board.</param>
public sealed record DrawRecord(
    long Draw,
    int Variant,
    string Target,
    string Aim,
    DrawSpecRecord Spec,
    ulong Seed,
    int AmountStep,
    DrawOutcome Outcome,
    string? Candidate,
    int? ProbeLimit,
    int? Limit,
    bool Fit,
    double Seconds,
    string? Detail);

public sealed record RetractRecord(string[] Candidates, string Reason, DateTime CreatedUtc);

/// <summary>The versions of the bank's format this build writes and reads.</summary>
public static class BankFormat
{
    public const int FormatVersion = 1;

    /// <summary>Raised whenever a record type or field is added that an older tool must not pass over.</summary>
    public const int ReaderVersion = 1;
}

/// <summary>Turns records into framed bytes and payloads back into records.</summary>
public static class RecordCodec
{
    private const byte GamesPayloadVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Frame(HeaderRecord header) => FrameJson(RecordType.Header, header);

    public static byte[] Frame(EpochRecord epoch) => FrameJson(RecordType.Epoch, epoch);

    public static byte[] Frame(BoardRecord board) => FrameJson(RecordType.Board, board);

    public static byte[] Frame(DrawRecord draw) => FrameJson(RecordType.Draw, draw);

    public static byte[] Frame(RetractRecord retract) => FrameJson(RecordType.Retract, retract);

    public static byte[] Frame(RungGames games) => RecordFrame.Encode(RecordType.Games, EncodeGames(games));

    /// <exception cref="BankFormatException">The payload is not a record of that type.</exception>
    public static T ReadJson<T>(StoredRecord record)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(record.Payload.Span, Json)
                ?? throw new BankFormatException($"The {record.Type} record at offset {record.Offset} is empty.");
        }
        catch (JsonException ex)
        {
            throw new BankFormatException($"The {record.Type} record at offset {record.Offset} does not read: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// A games record: version, candidate, epoch, purpose, the player (notice share, temperature, name), move cap,
    /// first repeat and repeat count, the progress marks, the seed positions, then the games.
    /// </summary>
    private static byte[] EncodeGames(RungGames games)
    {
        GamesHeader header = games.Header;
        byte[] name = Encoding.UTF8.GetBytes(header.Player.Name);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Span<byte> candidate = stackalloc byte[CandidateId.ByteLength];
        header.Candidate.Write(candidate);
        writer.Write(GamesPayloadVersion);
        writer.Write(candidate);
        writer.Write(header.Epoch.Value);
        writer.Write((byte)header.Purpose);
        writer.Write((byte)header.Player.NoticePercent);
        writer.Write(header.Player.Temperature);
        writer.Write((byte)name.Length);
        writer.Write(name);
        writer.Write((ushort)header.MoveCap);
        writer.Write((ushort)header.RepeatFrom);
        writer.Write((byte)header.RepeatCount);
        writer.Write((byte)header.ProgressMarks.Count);
        foreach (int mark in header.ProgressMarks)
        {
            writer.Write((byte)mark);
        }

        writer.Write((ushort)header.SeedIndices.Count);
        foreach (int seedIndex in header.SeedIndices)
        {
            writer.Write((ushort)seedIndex);
        }

        writer.Write(games.Bytes);
        writer.Flush();
        return stream.ToArray();
    }

    /// <exception cref="BankFormatException">The payload is not a games record this build reads.</exception>
    public static RungGames ReadGames(StoredRecord record)
    {
        ReadOnlySpan<byte> payload = record.Payload.Span;
        try
        {
            int at = 0;
            if (payload[at++] != GamesPayloadVersion)
            {
                throw new BankFormatException($"The games record at offset {record.Offset} has payload version {payload[0]}; this build reads {GamesPayloadVersion}.");
            }

            CandidateId candidate = CandidateId.Read(payload.Slice(at, CandidateId.ByteLength));
            at += CandidateId.ByteLength;
            var epoch = new EpochId(BinaryPrimitives.ReadUInt64LittleEndian(payload[at..]));
            at += sizeof(ulong);
            var purpose = (GamePurpose)payload[at++];
            int notice = payload[at++];
            double temperature = BinaryPrimitives.ReadDoubleLittleEndian(payload[at..]);
            at += sizeof(double);
            int nameLength = payload[at++];
            string name = Encoding.UTF8.GetString(payload.Slice(at, nameLength));
            at += nameLength;
            int moveCap = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
            int repeatFrom = BinaryPrimitives.ReadUInt16LittleEndian(payload[(at + 2)..]);
            at += 4;
            int repeatCount = payload[at++];
            int markCount = payload[at++];
            int[] marks = ReadBytes(payload, ref at, markCount);
            int seedCount = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
            at += 2;
            var seedIndices = new int[seedCount];
            for (int i = 0; i < seedCount; i++, at += 2)
            {
                seedIndices[i] = BinaryPrimitives.ReadUInt16LittleEndian(payload[at..]);
            }

            var header = new GamesHeader
            {
                Candidate = candidate,
                Epoch = epoch,
                Player = new PlayerModel(name, notice, temperature),
                Purpose = Enum.IsDefined(purpose) ? purpose : throw new BankFormatException($"The games record at offset {record.Offset} has the unknown purpose {(byte)purpose}."),
                SeedIndices = seedIndices,
                RepeatFrom = repeatFrom,
                RepeatCount = repeatCount,
                MoveCap = moveCap,
                ProgressMarks = marks,
            };
            return new RungGames(header, payload[at..].ToArray());
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            // A sound checksum around a payload that ends early or holds the wrong number of games: written by a faulty tool.
            throw new BankFormatException($"The games record at offset {record.Offset} is cut short or inconsistent: {ex.Message}", ex);
        }
    }

    private static int[] ReadBytes(ReadOnlySpan<byte> payload, ref int at, int count)
    {
        var values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = payload[at++];
        }

        return values;
    }

    private static byte[] FrameJson<T>(RecordType type, T record) =>
        RecordFrame.Encode(type, JsonSerializer.SerializeToUtf8Bytes(record, Json));
}
