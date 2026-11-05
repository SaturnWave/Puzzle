using System.Globalization;
using Puzzle.Bank.Measuring;

namespace Puzzle.Bank.Storage;

/// <summary>A sound record as it lies in a shard file.</summary>
/// <param name="TypeCode">The stored type; a code this build does not know is a record of a newer tool (see <see cref="Type"/>).</param>
/// <param name="Offset">Where the record's frame starts in the file.</param>
public sealed record StoredRecord(byte TypeCode, ReadOnlyMemory<byte> Payload, long Offset)
{
    /// <summary>
    /// The record's type; absent for a code this build does not know. Such a record is passed over: a shard's header
    /// names the oldest reader that may read it, so a type that an older reader must not miss raises that number
    /// and the older reader refuses the whole bank instead.
    /// </summary>
    public RecordType? Type => Enum.IsDefined((RecordType)TypeCode) ? (RecordType)TypeCode : null;
}

/// <summary>Bytes inside a shard file that are no record: damage, with records after it.</summary>
public sealed record UnreadableRange(long Offset, long Length);

/// <summary>What one shard file holds.</summary>
/// <param name="Unreadable">Damaged stretches; every record around them was still read.</param>
/// <param name="TornTailBytes">Bytes of a record whose writing was cut short at the end of the file; 0 for a file that ends cleanly.</param>
public sealed record ShardScan(string File, IReadOnlyList<StoredRecord> Records, IReadOnlyList<UnreadableRange> Unreadable, long TornTailBytes, long Length)
{
    public bool IsSound => Unreadable.Count == 0;
}

/// <summary>
/// Reads a shard file record by record. Nothing in a shard is trusted beyond its checksums: after a stretch that is
/// no record the reader looks for the next sound one, so a damaged record costs that record and no other.
/// </summary>
public static class ShardReader
{
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static ShardScan Scan(string path)
    {
        // A grower may be appending to this very file; what it has not finished writing is a torn tail to this reader.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return Scan(bytes, Path.GetFileName(path));
    }

    public static ShardScan Scan(byte[] bytes, string file)
    {
        var records = new List<StoredRecord>();
        var unreadable = new List<UnreadableRange>();
        long tornTail = 0;
        int position = 0;
        while (position < bytes.Length)
        {
            if (RecordFrame.TryRead(bytes.AsSpan(position), out byte typeCode, out int payloadLength))
            {
                records.Add(new StoredRecord(typeCode, bytes.AsMemory(position + RecordFrame.HeaderLength, payloadLength), position));
                position += RecordFrame.HeaderLength + payloadLength + RecordFrame.ChecksumLength;
                continue;
            }

            int next = NextRecordAfter(bytes, position);
            if (next < 0 && RecordFrame.IsCutOffRecord(bytes.AsSpan(position)))
            {
                tornTail = bytes.Length - position;
                break;
            }

            int end = next < 0 ? bytes.Length : next;
            unreadable.Add(new UnreadableRange(position, end - position));
            position = end;
        }

        return new ShardScan(file, records, unreadable, tornTail, bytes.Length);
    }

    /// <summary>The offset of the first sound record that starts after <paramref name="position"/>; -1 when there is none.</summary>
    private static int NextRecordAfter(byte[] bytes, int position)
    {
        for (int from = position + 1; from < bytes.Length;)
        {
            int found = bytes.AsSpan(from).IndexOf(RecordFrame.Magic);
            if (found < 0)
            {
                return -1;
            }

            if (RecordFrame.TryRead(bytes.AsSpan(from + found), out _, out _))
            {
                return from + found;
            }

            from += found + 1;
        }

        return -1;
    }
}

/// <summary>
/// The bank's files could not be read: the disk went away, or a shard that was there is gone. Nothing was changed.
/// </summary>
public sealed class BankReadException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>The bank's disk went away or filled up while a record was written. The record is not in the bank; everything written before it is.</summary>
public sealed class BankWriteException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>
/// Appends records to shard files that belong to this writer alone: named after its start time and process id, never
/// reopened, never rewritten. All frames of one call go out in a single write and are flushed to the disk before the
/// call returns, so what belongs together (a candidate and its games) is in the bank whole or not at all.
/// </summary>
public sealed class ShardWriter : IDisposable
{
    public const string Extension = ".pzb";

    private const long DefaultRollOverBytes = 64L * 1024 * 1024;

    /// <summary>Writers this process has opened: a second writer within the same second still gets files of its own.</summary>
    private static int _writersOpened;

    private readonly string _directory;
    private readonly string _runStamp;
    private readonly byte[] _headerFrame;
    private readonly long _rollOverBytes;
    private FileStream? _file;
    private int _fileNumber;

    /// <param name="headerFrame">The framed header record every new file starts with.</param>
    public ShardWriter(string directory, byte[] headerFrame, long rollOverBytes = DefaultRollOverBytes)
    {
        _directory = directory;
        _headerFrame = headerFrame;
        _rollOverBytes = rollOverBytes;
        _runStamp = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}-{Environment.ProcessId}-{Interlocked.Increment(ref _writersOpened):00}");
    }

    public long BytesWritten { get; private set; }

    /// <returns>Where each frame now lies, in the order given.</returns>
    /// <exception cref="BankWriteException">The frames could not be written and flushed.</exception>
    public IReadOnlyList<RecordLocation> Append(IReadOnlyList<byte[]> frames)
    {
        byte[] block = frames.SelectMany(frame => frame).ToArray();
        try
        {
            FileStream file = FileFor(block.Length);
            long offset = file.Position;
            file.Write(block);
            file.Flush(flushToDisk: true);
            BytesWritten += block.Length;
            return LocationsOf(frames, Path.GetFileName(file.Name), offset);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BankWriteException($"Could not write to the bank in '{_directory}': {ex.Message}", ex);
        }
    }

    private static RecordLocation[] LocationsOf(IReadOnlyList<byte[]> frames, string file, long offset)
    {
        var locations = new RecordLocation[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            locations[i] = new RecordLocation(file, offset);
            offset += frames[i].Length;
        }

        return locations;
    }

    public void Dispose() => _file?.Dispose();

    /// <summary>The open shard, or a new one when there is none yet or the open one would grow past the roll-over size.</summary>
    private FileStream FileFor(int blockLength)
    {
        if (_file is not null && _file.Length + blockLength <= _rollOverBytes)
        {
            return _file;
        }

        _file?.Dispose();
        _fileNumber++;
        string path = Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{_runStamp}-{_fileNumber:0000}{Extension}"));
        // Unbuffered: every block goes to the disk as one write, and closing a file whose disk went away has nothing left to flush.
        _file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 0);
        _file.Write(_headerFrame);
        BytesWritten += _headerFrame.Length;
        return _file;
    }
}
