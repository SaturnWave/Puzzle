using System.Globalization;

namespace Puzzle.Tooling.Ladder;

/// <summary>A file that should be a ladder report is something else; the message names the file and the line.</summary>
public sealed class LadderReportFormatException : Exception
{
    public LadderReportFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// The ladder report: one CSV row per level and bot, sorted by level and then by skill. A long measurement can be done
/// in pieces: each piece replaces the rows of the levels it measured and keeps the rows of every other level.
/// </summary>
public static class LadderCsv
{
    private const string LevelColumn = "level";
    private const string SkillColumn = "skill";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly string[] Columns =
    [
        LevelColumn, "moveLimit", "bot", SkillColumn, "runs", "winAtLimit",
        .. LadderRow.MovePercents.Select(percent => string.Create(Invariant, $"movesP{percent}")),
        .. LadderRow.WinPercents.Select(percent => string.Create(Invariant, $"limitFor{percent}")),
    ];

    /// <summary>The first line of a ladder report: its column names.</summary>
    public static string Header { get; } = string.Join(',', Columns);

    /// <summary>The row as a line of the report, in the order of <see cref="Header"/>; an absent number is an empty cell.</summary>
    public static string Line(LadderRow row) => string.Join(
        ',',
        [
            row.Level.ToString(),
            row.MoveLimit.ToString(Invariant),
            row.Bot.Name,
            row.Bot.NoticePercent.ToString(Invariant),
            row.Runs.ToString(Invariant),
            row.WinAtLimit.ToString("0.000", Invariant),
            .. row.MovesNeeded.Select(Moves),
            .. row.LimitFor.Select(Moves),
        ]);

    /// <summary>Where <see cref="Upsert"/> writes the new report before it takes the place of the old one.</summary>
    public static string PendingPathOf(string path) => path + ".new";

    /// <summary>
    /// Checks that <paramref name="path"/> can take a ladder report: no file is there yet, or the one there is a ladder
    /// report. A measurement asks before it starts, so that a wrong path costs no games and overwrites no other file.
    /// </summary>
    /// <exception cref="LadderReportFormatException">The file there is not a ladder report.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static void RequireReportOrNoFile(string path) => _ = ReadLines(path);

    /// <summary>
    /// Writes <paramref name="rows"/> into the report at <paramref name="path"/>. Every row the report has for one of
    /// their levels is replaced, whichever bots it was for, so a level's rows always come from one measurement and its
    /// bots stay compared on the same games; the rows of every other level are kept. The new report is written beside
    /// the old one (<see cref="PendingPathOf"/>) and then moved onto it, so a failed write loses neither.
    /// </summary>
    /// <returns>How many rows of other levels were kept.</returns>
    /// <exception cref="ArgumentException">Two of the rows are for the same level and skill.</exception>
    /// <exception cref="LadderReportFormatException">The file there is not a ladder report.</exception>
    /// <exception cref="IOException">
    /// The report could not be read or written. When only the last step failed — the old report is open in another
    /// program, say — the message says so, and the complete new report is at <see cref="PendingPathOf"/>.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the file may not be written.</exception>
    public static int Upsert(string path, IReadOnlyCollection<LadderRow> rows)
    {
        HashSet<int> measuredLevels = rows.Select(row => row.Level.Value).ToHashSet();
        SortedDictionary<(int Level, int Skill), string> lines = ReadLines(path);
        foreach ((int Level, int Skill) replaced in lines.Keys.Where(key => measuredLevels.Contains(key.Level)).ToArray())
        {
            lines.Remove(replaced);
        }

        int kept = lines.Count;
        foreach (LadderRow row in rows)
        {
            if (!lines.TryAdd((row.Level.Value, row.Bot.NoticePercent), Line(row)))
            {
                throw new ArgumentException($"Two rows are for level {row.Level}, skill {row.Bot.NoticePercent}.", nameof(rows));
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllLines(PendingPathOf(path), lines.Values.Prepend(Header));
        PutInPlace(PendingPathOf(path), path);
        return kept;
    }

    /// <summary>Moves the finished new report onto the old one. A failure leaves both files and says where the new one is.</summary>
    /// <exception cref="IOException">The old report could not be replaced.</exception>
    private static void PutInPlace(string pendingPath, string path)
    {
        try
        {
            File.Move(pendingPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The new report is complete at '{pendingPath}', but it could not take the place of '{path}': {ex.Message}", ex);
        }
    }

    private static string Moves(int? moves) => moves?.ToString(Invariant) ?? string.Empty;

    /// <summary>
    /// The rows of the report at <paramref name="path"/> as they are written, by the level and the skill each is for,
    /// so in report order; none when no file is there.
    /// </summary>
    /// <exception cref="LadderReportFormatException">The file there is not a ladder report.</exception>
    private static SortedDictionary<(int Level, int Skill), string> ReadLines(string path)
    {
        var rows = new SortedDictionary<(int Level, int Skill), string>();
        if (!File.Exists(path))
        {
            return rows;
        }

        string[] lines = File.ReadAllLines(path);
        if (lines.Length > 0 && lines[0] != Header)
        {
            throw new LadderReportFormatException($"'{path}' is not a ladder report: its first line is not '{Header}'.");
        }

        for (int index = 1; index < lines.Length; index++)
        {
            if (KeyOf(lines[index]) is not { } key)
            {
                throw new LadderReportFormatException($"'{path}' is not a ladder report: line {index + 1} is not a row of one.");
            }

            rows[key] = lines[index];
        }

        return rows;
    }

    /// <summary>The level and the skill a line of the report is for; absent when the line is not a row of the report.</summary>
    private static (int Level, int Skill)? KeyOf(string line)
    {
        string[] cells = line.Split(',');
        return cells.Length == Columns.Length
            && int.TryParse(cells[Array.IndexOf(Columns, LevelColumn)], NumberStyles.None, Invariant, out int level)
            && int.TryParse(cells[Array.IndexOf(Columns, SkillColumn)], NumberStyles.None, Invariant, out int skill)
                ? (level, skill)
                : null;
    }
}
