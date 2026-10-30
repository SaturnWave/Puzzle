using System.Text.Json;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;

namespace Puzzle.Bank.Storage;

/// <summary>What makes a folder this bank and no other: its id, and the seed all of its draws come from.</summary>
public sealed record BankIdentity(string BankId, ulong BankSeed, DateTime CreatedUtc);

/// <summary>Another process is growing the bank; the message names it.</summary>
public sealed class BankBusyException(string message) : Exception(message);

/// <summary>A whole read of the bank: its content, and what every shard file was like.</summary>
/// <param name="GamesWithoutCandidate">Batches of games whose candidate is in no shard (a shard file is missing).</param>
public sealed record BankScan(BankCatalog Catalog, IReadOnlyList<ShardScan> Shards, int GamesWithoutCandidate)
{
    public long Bytes => Shards.Sum(shard => shard.Length);

    public int Records => Shards.Sum(shard => shard.Records.Count);

    /// <summary>Records whose writing was cut short at the end of a file: a stopped grower leaves at most one, and it costs nothing.</summary>
    public int TornTails => Shards.Count(shard => shard.TornTailBytes > 0);

    /// <summary>No shard has a damaged stretch and every batch of games has its candidate.</summary>
    public bool IsSound => GamesWithoutCandidate == 0 && Shards.All(shard => shard.IsSound);
}

/// <summary>
/// The bank on disk: a folder outside the repository with a small manifest (<c>bank.json</c>) and append-only shard
/// files under <c>shards/</c>. The shards are the bank; the manifest only repeats what every shard's header says and
/// is written again from them when it is lost. Nothing here is ever rewritten in place.
/// </summary>
public sealed class BankStore
{
    /// <summary>The environment variable that names the bank's folder when no option does.</summary>
    public const string RootVariable = "PUZZLE_BANK";

    private const string ManifestName = "bank.json";
    private const string ShardFolder = "shards";
    private const string LockName = "grow.lock";

    private static readonly JsonSerializerOptions ManifestJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private readonly HeaderRecord _header;

    private BankStore(string root, HeaderRecord header)
    {
        Root = root;
        _header = header;
    }

    public string Root { get; }

    public BankIdentity Identity => new(_header.BankId, _header.BankSeed, _header.CreatedUtc);

    private string ShardDirectory => Path.Combine(Root, ShardFolder);

    /// <summary>Whether <paramref name="root"/> holds a bank: its manifest, or shards to write one from.</summary>
    public static bool Exists(string root) =>
        File.Exists(Path.Combine(root, ManifestName)) || ShardFilesUnder(Path.Combine(root, ShardFolder)).Length > 0;

    /// <summary>Makes an empty bank in <paramref name="root"/>, creating the folder.</summary>
    /// <exception cref="BankFormatException">The folder already holds a bank.</exception>
    /// <exception cref="BankWriteException">The folder cannot be made or written.</exception>
    public static BankStore Create(string root, ulong bankSeed)
    {
        if (Exists(root))
        {
            throw new BankFormatException($"'{root}' already holds a bank.");
        }

        var header = new HeaderRecord(BankFormat.FormatVersion, BankFormat.ReaderVersion, Guid.NewGuid().ToString("N"), bankSeed, DateTime.UtcNow);
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ShardFolder));
            WriteManifest(root, header);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BankWriteException($"Could not make a bank in '{root}': {ex.Message}", ex);
        }

        return new BankStore(root, header);
    }

    /// <exception cref="BankFormatException">The folder holds no bank, or one this build may not read.</exception>
    /// <exception cref="BankReadException">The bank's files cannot be read.</exception>
    public static BankStore Open(string root)
    {
        HeaderRecord header = Reading(root, () => ReadManifest(root) ?? HeaderOfFirstShard(root))
            ?? throw new BankFormatException($"'{root}' holds no level bank (no {ManifestName} and no shard).");
        RequireReadable(header, Path.Combine(root, ManifestName));
        var store = new BankStore(root, header);
        if (!File.Exists(Path.Combine(root, ManifestName)))
        {
            WriteManifest(root, header);
        }

        return store;
    }

    /// <summary>Reads every shard. Damage is reported in the scan, not thrown: the sound records around it are the bank.</summary>
    /// <exception cref="BankFormatException">A shard belongs to another bank or needs a newer reader, or a sound record does not hold what its type says.</exception>
    /// <exception cref="BankReadException">A shard file cannot be read.</exception>
    public BankScan Read()
    {
        var catalog = new BankCatalog();
        var shards = new List<ShardScan>();
        foreach (string path in Reading(Root, () => ShardFilesUnder(ShardDirectory)))
        {
            ShardScan shard = Reading(Root, () => ShardReader.Scan(path));
            RequireOwnShard(shard);
            foreach (StoredRecord record in shard.Records)
            {
                AddTo(catalog, record, shard.File);
            }

            shards.Add(shard);
        }

        return new BankScan(catalog, shards, catalog.AdoptWaitingGames());
    }

    /// <summary>
    /// Runs a read of the bank's files. A disk that went away fails every read alike, and a caller can do one thing
    /// about it: stop, say so, and be started again when the disk is back.
    /// </summary>
    /// <exception cref="BankReadException">The read failed.</exception>
    private static T Reading<T>(string root, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BankReadException($"Could not read the bank in '{root}': {ex.Message}", ex);
        }
    }

    /// <summary>A writer for this process's own shard files, keeping <paramref name="catalog"/> in step with what it writes.</summary>
    public BankWriter OpenWriter(BankCatalog catalog)
    {
        Directory.CreateDirectory(ShardDirectory);
        return new BankWriter(new ShardWriter(ShardDirectory, RecordCodec.Frame(_header)), catalog);
    }

    /// <summary>
    /// Takes the grower's place: one process grows a bank at a time, because two would draw the same draw numbers.
    /// The lock is the open file handle, so it ends with its process however that ends; a lock file left behind by a
    /// crash or a pulled cable holds nobody back. Readers and tools that only add games need no lock.
    /// </summary>
    /// <exception cref="BankBusyException">Another process holds it.</exception>
    /// <exception cref="BankWriteException">The bank's folder cannot be written.</exception>
    public GrowLock LockForGrowth() => GrowLock.Acquire(Path.Combine(Root, LockName));

    /// <summary>The full record of a batch of games, read again from its shard.</summary>
    /// <exception cref="BankFormatException">No sound games record lies there any more.</exception>
    /// <exception cref="BankReadException">The shard cannot be read.</exception>
    public RungGames ReadGames(RecordLocation location)
    {
        byte[] bytes = Reading(Root, () =>
        {
            using var stream = new FileStream(Path.Combine(ShardDirectory, location.File), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadRecordAt(stream, location.Offset);
        });
        if (!RecordFrame.TryRead(bytes, out byte typeCode, out int payloadLength) || typeCode != (byte)RecordType.Games)
        {
            throw new BankFormatException($"No games record lies at offset {location.Offset} of '{location.File}'.");
        }

        return RecordCodec.ReadGames(new StoredRecord(typeCode, bytes.AsMemory(RecordFrame.HeaderLength, payloadLength), location.Offset));
    }

    /// <summary>The bytes of the one record that starts at <paramref name="offset"/>; fewer when the file ends first, none when no header lies there.</summary>
    private static byte[] ReadRecordAt(FileStream stream, long offset)
    {
        var header = new byte[RecordFrame.HeaderLength];
        stream.Position = offset;
        if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length || RecordFrame.WholeLength(header) is not int length)
        {
            return [];
        }

        var record = new byte[length];
        header.CopyTo(record, 0);
        int read = stream.ReadAtLeast(record.AsSpan(header.Length), length - header.Length, throwOnEndOfStream: false);
        return record[..(header.Length + read)];
    }

    private static void AddTo(BankCatalog catalog, StoredRecord record, string file)
    {
        switch (record.Type)
        {
            case RecordType.Epoch:
                catalog.Add(RecordCodec.ReadJson<EpochRecord>(record));
                break;
            case RecordType.Board:
                catalog.Add(RecordMapping.ToCandidate(RecordCodec.ReadJson<BoardRecord>(record)));
                break;
            case RecordType.Games:
                RungGames games = RecordCodec.ReadGames(record);
                catalog.Add(new StoredGames(games.Header, games.Finishes(), new RecordLocation(file, record.Offset)));
                break;
            case RecordType.Draw:
                catalog.Add(RecordCodec.ReadJson<DrawRecord>(record));
                break;
            case RecordType.Retract:
                foreach (string id in RecordCodec.ReadJson<RetractRecord>(record).Candidates)
                {
                    catalog.Retract(CandidateId.Parse(id));
                }

                break;
            case RecordType.Header:
            case null: // a record of a newer tool that this reader is allowed to pass over (see StoredRecord.Type)
                break;
            default:
                throw new InvalidOperationException($"Unhandled record type {record.Type}.");
        }
    }

    /// <exception cref="BankFormatException">The shard does not start with this bank's header, or needs a newer reader.</exception>
    private void RequireOwnShard(ShardScan shard)
    {
        if (shard.Records.Count == 0)
        {
            return; // nothing sound in it; its damage is in the scan
        }

        if (shard.Records[0].Type != RecordType.Header)
        {
            throw new BankFormatException($"Shard '{shard.File}' does not start with a header record.");
        }

        HeaderRecord header = RecordCodec.ReadJson<HeaderRecord>(shard.Records[0]);
        RequireReadable(header, shard.File);
        if (header.BankId != _header.BankId)
        {
            throw new BankFormatException($"Shard '{shard.File}' belongs to bank {header.BankId}, not to {_header.BankId}.");
        }
    }

    private static void RequireReadable(HeaderRecord header, string source)
    {
        if (header.MinReaderVersion > BankFormat.ReaderVersion)
        {
            throw new BankFormatException(
                $"'{source}' needs a reader of version {header.MinReaderVersion}; this build reads up to {BankFormat.ReaderVersion}. Build the tools from a newer source.");
        }
    }

    private static HeaderRecord? ReadManifest(string root)
    {
        string path = Path.Combine(root, ManifestName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HeaderRecord>(File.ReadAllText(path), ManifestJson)
                ?? throw new BankFormatException($"'{path}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new BankFormatException($"'{path}' is not a bank manifest: {ex.Message}", ex);
        }
    }

    private static HeaderRecord? HeaderOfFirstShard(string root)
    {
        foreach (string path in ShardFilesUnder(Path.Combine(root, ShardFolder)))
        {
            if (ShardReader.Scan(path).Records is [{ Type: RecordType.Header } header, ..])
            {
                return RecordCodec.ReadJson<HeaderRecord>(header);
            }
        }

        return null;
    }

    /// <summary>Through a temporary file and a rename, so a manifest is whole or absent.</summary>
    private static void WriteManifest(string root, HeaderRecord header)
    {
        string path = Path.Combine(root, ManifestName);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(header, ManifestJson));
        File.Move(temporary, path, overwrite: true);
    }

    private static string[] ShardFilesUnder(string directory) => Directory.Exists(directory)
        ? Directory.GetFiles(directory, "*" + ShardWriter.Extension).Order(StringComparer.Ordinal).ToArray()
        : [];
}

/// <summary>
/// Writes what a tool adds to the bank and tells the catalog as soon as it is on disk, in that order: the catalog
/// never knows of a record the disk does not have.
/// </summary>
public sealed class BankWriter(ShardWriter shards, BankCatalog catalog) : IDisposable
{
    public long BytesWritten => shards.BytesWritten;

    /// <exception cref="BankWriteException">The record could not be written.</exception>
    public void Open(EpochRecord epoch)
    {
        shards.Append([RecordCodec.Frame(epoch)]);
        catalog.Add(epoch);
    }

    /// <summary>A measured candidate with its games and the draw that made it: all of it in one write, or none.</summary>
    /// <exception cref="BankWriteException">The records could not be written.</exception>
    public void Store(BankCandidate candidate, IReadOnlyList<RungGames> games, DrawRecord draw)
    {
        byte[][] frames = [RecordCodec.Frame(RecordMapping.ToRecord(candidate)), .. games.Select(RecordCodec.Frame), RecordCodec.Frame(draw)];
        IReadOnlyList<RecordLocation> locations = shards.Append(frames);
        catalog.Add(candidate);
        for (int i = 0; i < games.Count; i++)
        {
            catalog.Add(new StoredGames(games[i].Header, games[i].Finishes(), locations[i + 1]));
        }

        catalog.Add(draw);
    }

    /// <summary>A measured candidate that no draw made (an imported level) with its games: all of it in one write, or none.</summary>
    /// <exception cref="BankWriteException">The records could not be written.</exception>
    public void Adopt(BankCandidate candidate, IReadOnlyList<RungGames> games)
    {
        byte[][] frames = [RecordCodec.Frame(RecordMapping.ToRecord(candidate)), .. games.Select(RecordCodec.Frame)];
        IReadOnlyList<RecordLocation> locations = shards.Append(frames);
        catalog.Add(candidate);
        for (int i = 0; i < games.Count; i++)
        {
            catalog.Add(new StoredGames(games[i].Header, games[i].Finishes(), locations[i + 1]));
        }
    }

    /// <summary>Further games of candidates the bank holds (the confirm and audit games of an assembly), in one write.</summary>
    /// <returns>The games as the catalog now holds them, in the order given.</returns>
    /// <exception cref="BankWriteException">The records could not be written.</exception>
    public IReadOnlyList<StoredGames> Add(IReadOnlyList<RungGames> games)
    {
        IReadOnlyList<RecordLocation> locations = shards.Append(games.Select(RecordCodec.Frame).ToArray());
        StoredGames[] stored = games.Select((batch, i) => new StoredGames(batch.Header, batch.Finishes(), locations[i])).ToArray();
        foreach (StoredGames batch in stored)
        {
            catalog.Add(batch);
        }

        return stored;
    }

    /// <summary>A draw that stored nothing.</summary>
    /// <exception cref="BankWriteException">The record could not be written.</exception>
    public void Note(DrawRecord draw)
    {
        shards.Append([RecordCodec.Frame(draw)]);
        catalog.Add(draw);
    }

    /// <summary>Withdraws candidates: their records stay on disk and are no longer read.</summary>
    /// <exception cref="BankWriteException">The record could not be written.</exception>
    public void Retract(IReadOnlyList<CandidateId> candidates, string reason)
    {
        shards.Append([RecordCodec.Frame(new RetractRecord(candidates.Select(id => id.ToString()).ToArray(), reason, DateTime.UtcNow))]);
        foreach (CandidateId id in candidates)
        {
            catalog.Retract(id);
        }
    }

    public void Dispose() => shards.Dispose();
}

/// <summary>
/// The grower's place in a bank, held for as long as this object lives. The place is an open handle on the lock file
/// that shares nothing: the one way of opening a file that keeps a second opener out on every platform (on Unix,
/// .NET turns every other share mode into a shared lock, which two growers would both be given). Because nobody else
/// can then read the lock file, the holder says who it is in a note beside it.
/// </summary>
public sealed class GrowLock : IDisposable
{
    private const string HolderNoteSuffix = ".holder";

    private readonly FileStream _file;
    private readonly string _holderNote;

    private GrowLock(FileStream file, string holderNote)
    {
        _file = file;
        _holderNote = holderNote;
    }

    internal static GrowLock Acquire(string path)
    {
        string holderNote = path + HolderNoteSuffix;
        FileStream file;
        try
        {
            file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        }
        catch (IOException ex) when (HolderOf(holderNote) is string holder)
        {
            throw new BankBusyException($"The bank is being grown by {holder}; one grower at a time ({ex.Message})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BankWriteException($"Could not take the grower's lock '{path}': {ex.Message}", ex);
        }

        try
        {
            File.WriteAllText(holderNote, $"process {Environment.ProcessId} on {Environment.MachineName}, since {DateTime.UtcNow:u}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            file.Dispose();
            throw new BankWriteException($"Could not write the grower's note '{holderNote}': {ex.Message}", ex);
        }

        return new GrowLock(file, holderNote);
    }

    /// <summary>
    /// Leaves the place. The note goes first, while the handle still keeps a newcomer out, so nobody reads the note
    /// of a holder that has left. A note that cannot be removed (the disk was pulled) is a stale line of text and
    /// holds nobody back: the lock is the handle, and the next grower writes the note anew.
    /// </summary>
    public void Dispose()
    {
        try
        {
            File.Delete(_holderNote);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Deliberately not rethrown: see the summary. Leaving the place must not fail on a note.
        }

        _file.Dispose();
    }

    /// <summary>What the holder wrote into its note; absent when there is no note to read (then the disk is the trouble, not a holder).</summary>
    private static string? HolderOf(string holderNote)
    {
        try
        {
            return File.ReadAllText(holderNote);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
