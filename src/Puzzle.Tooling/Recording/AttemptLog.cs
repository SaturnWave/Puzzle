using System.Text;
using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Recording;

/// <summary>A line of an attempt log that is not a valid record, with the reason.</summary>
public sealed record AttemptLogProblem(int Line, string Message);

/// <summary>What an attempt log holds: the records that read, and the lines that did not.</summary>
public sealed record AttemptLogContents(IReadOnlyList<AttemptRecord> Records, IReadOnlyList<AttemptLogProblem> Problems);

/// <summary>
/// A JSON Lines file of attempt records (<see cref="AttemptRecordJson"/>): one record per line, only ever appended.
/// One instance may be shared by every thread of a process; each append writes one whole line under a lock, opening
/// and closing the file, so a crash can cut at most the last line short. The next append starts a line of its own, so
/// the cut line is then reported as unreadable and the new record is kept. Readers never lock writers out: the lab
/// keeps recording while <c>puzzle humans</c> reads the same file.
/// </summary>
public sealed class AttemptLog
{
    /// <summary>Where the lab records and <c>puzzle humans</c> reads unless told otherwise, relative to the repository root.</summary>
    public const string DefaultLocation = "recordings/attempts.jsonl";

    /// <summary>
    /// Another process (a second lab, an editor) may hold the file for a moment; an append waits for it this many times,
    /// doubling the pause from <see cref="FirstRetryPause"/> (under 0.4 s in all), before it gives up.
    /// </summary>
    private const int AppendTries = 5;

    private static readonly TimeSpan FirstRetryPause = TimeSpan.FromMilliseconds(25);

    private readonly object _appendGate = new();
    private readonly string _folder;

    /// <exception cref="ArgumentException"><paramref name="path"/> does not name a file inside a folder.</exception>
    public AttemptLog(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        _folder = System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } folder
            ? folder
            : throw new ArgumentException($"'{path}' does not name a file inside a folder.", nameof(path));
    }

    /// <summary>The full path of the file.</summary>
    public string Path { get; }

    /// <summary>
    /// The log at <see cref="DefaultLocation"/> in the repository root above <paramref name="start"/>, so every way of
    /// starting the lab feeds the same file; absent outside a repository.
    /// </summary>
    public static string? FindDefaultPath(string start) =>
        LevelLibrary.FindRepositoryRoot(start) is { } root ? System.IO.Path.GetFullPath(System.IO.Path.Combine(root, DefaultLocation)) : null;

    /// <summary>
    /// Appends <paramref name="record"/> as one line, creating the file and its folder when needed. When the file does
    /// not end in a line break (a write cut short, a hand edit), the record starts a line of its own after one, so the
    /// unfinished text never swallows it.
    /// </summary>
    /// <exception cref="IOException">The folder or the file could not be written, or another process kept the file locked.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there.</exception>
    public void Append(AttemptRecord record)
    {
        byte[] line = Encoding.UTF8.GetBytes(AttemptRecordJson.Serialize(record) + "\n");
        lock (_appendGate)
        {
            Directory.CreateDirectory(_folder);
            TimeSpan pause = FirstRetryPause;
            for (int tries = 1; ; tries++)
            {
                try
                {
                    // Read access only to look at the last byte; no other process may write while this one does.
                    using var file = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    if (!EndsWithLineBreak(file))
                    {
                        file.WriteByte((byte)'\n');
                    }

                    file.Write(line);
                    return;
                }
                catch (IOException ex) when (IsSharingViolation(ex) && tries < AppendTries)
                {
                    Thread.Sleep(pause);
                    pause *= 2;
                }
            }
        }
    }

    /// <summary>
    /// Reads every record, line by line; a line that is not a valid record is reported with its number, not thrown.
    /// Text after the last line break is an append still in progress and is left for a later read. A log that was never
    /// written holds no records.
    /// </summary>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not read the file.</exception>
    public AttemptLogContents ReadAll()
    {
        var records = new List<AttemptRecord>();
        var problems = new List<AttemptLogProblem>();
        if (!File.Exists(Path))
        {
            return new AttemptLogContents(records, problems);
        }

        // Writers (this lab or another) may keep appending, and the file may even be replaced, while it is read.
        using var file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file, Encoding.UTF8);
        int lineNumber = 0;
        foreach (string line in CompleteLines(reader))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                records.Add(AttemptRecordJson.Parse(line));
            }
            catch (AttemptRecordFormatException ex)
            {
                problems.Add(new AttemptLogProblem(lineNumber, ex.Message));
            }
        }

        return new AttemptLogContents(records, problems);
    }

    /// <summary>Whether <paramref name="file"/> is empty or ends in a line break; leaves the position at its end either way.</summary>
    private static bool EndsWithLineBreak(FileStream file)
    {
        if (file.Length == 0)
        {
            return true;
        }

        file.Seek(-1, SeekOrigin.End);
        return file.ReadByte() == '\n';
    }

    /// <summary>Windows refuses an open that another handle's sharing excludes; such a hold is brief, so it is worth waiting for.</summary>
    private static bool IsSharingViolation(IOException ex)
    {
        const int SharingViolation = 32; // ERROR_SHARING_VIOLATION
        const int LockViolation = 33; // ERROR_LOCK_VIOLATION
        return (ex.HResult & 0xFFFF) is SharingViolation or LockViolation;
    }

    /// <summary>The lines that end in a line break, without it; whatever follows the last break is not a line yet.</summary>
    private static IEnumerable<string> CompleteLines(TextReader reader)
    {
        var line = new StringBuilder();
        for (int next = reader.Read(); next >= 0; next = reader.Read())
        {
            if (next == '\n')
            {
                yield return line.ToString().TrimEnd('\r');
                line.Clear();
            }
            else
            {
                line.Append((char)next);
            }
        }
    }
}
