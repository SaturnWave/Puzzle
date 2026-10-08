using System.Globalization;
using Puzzle.Bank.Candidates;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Tooling;

namespace Puzzle.Bank.Assembling;

/// <summary>One level's row of a report, as its line of text and by column name.</summary>
public sealed class ReportLine(string text, string[] columns)
{
    private readonly string[] _cells = text.Split(',');

    /// <summary>The row exactly as the file has it.</summary>
    public string Text { get; } = text;

    /// <summary>The cell under <paramref name="column"/>; absent when the report has no such column or the row is shorter than its header.</summary>
    public string? Cell(string column)
    {
        int at = Array.IndexOf(columns, column);
        return at >= 0 && at < _cells.Length ? _cells[at] : null;
    }

    /// <summary>The row's text with the cell under <paramref name="column"/> replaced; the row as it is when it has no such cell.</summary>
    public string With(string column, string value)
    {
        int at = Array.IndexOf(columns, column);
        if (at < 0 || at >= _cells.Length)
        {
            return Text;
        }

        string[] cells = [.. _cells];
        cells[at] = value;
        return string.Join(',', cells);
    }

    /// <summary>A cell that holds a number (a share, a move count); absent when it is missing, empty or no number.</summary>
    public double? Number(string column) =>
        double.TryParse(Cell(column), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
}

/// <summary>
/// A table with one row per level number, read as text: its header, its line ending, and its rows by level. Both
/// tables of a level folder are of this kind: the generation report (report.csv) and an assembly's own table
/// (assembly.csv).
/// </summary>
public sealed class ReportTable
{
    private readonly Dictionary<int, ReportLine> _rows;

    private ReportTable(string[] columns, string newline, Dictionary<int, ReportLine> rows)
    {
        Columns = columns;
        Newline = newline;
        _rows = rows;
    }

    /// <summary>Empty when the folder has no report.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>The line ending the file was written with, so a copy of its rows is the same bytes.</summary>
    public string Newline { get; }

    public ReportLine? Row(int level) => _rows.GetValueOrDefault(level);

    /// <summary>The report at <paramref name="path"/>; a missing file reads as a report without rows.</summary>
    public static ReportTable Read(string path)
    {
        if (!File.Exists(path))
        {
            return new ReportTable([], Environment.NewLine, []);
        }

        string text = File.ReadAllText(path);
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] lines = text.Split(newline, StringSplitOptions.RemoveEmptyEntries);
        string[] columns = lines.Length > 0 ? lines[0].Split(',') : [];
        var rows = new Dictionary<int, ReportLine>();
        foreach (string line in lines.Skip(1))
        {
            if (int.TryParse(line.AsSpan(0, Math.Max(0, line.IndexOf(','))), NumberStyles.None, CultureInfo.InvariantCulture, out int level))
            {
                rows[level] = new ReportLine(line, columns);
            }
        }

        return new ReportTable(columns, newline, rows);
    }
}

/// <summary>A level as a folder holds it: its file, the level in it, the candidate behind it, and its rows in the folder's two tables.</summary>
/// <param name="Content">Absent when the level cannot be a bank candidate (a fixed start board, a palette out of colour order).</param>
/// <param name="Row">Absent when the folder's report has no row for the level.</param>
/// <param name="Provenance">What the assembly that wrote the folder noted of the level (assembly.csv); absent in a folder no assembly wrote.</param>
public sealed record FolderLevel(int Number, string Path, LevelDefinition Level, CandidateContent? Content, ReportLine? Row, ReportLine? Provenance)
{
    /// <summary>What the report's relaxed column says of a level that stands in a set without meeting its policy.</summary>
    public const string StaleMark = "stale";

    private const string MeetsItsRange = "no";

    /// <summary>
    /// The level does not meet what it was made for, by its own report row: the generator gave up its move range
    /// (relaxed), or an assembly found no board for the number and left the level it had (stale).
    /// </summary>
    public bool IsRelaxedOrStale => Row?.Cell("relaxed") is string relaxed && relaxed != MeetsItsRange;

    /// <summary>The limit before the move margin, which is what a move range bounds; a row older than margins has only the limit itself.</summary>
    public int CalibratedMoves => Row?.Number("calibratedMoves") is double calibrated ? (int)calibrated : Level.MoveLimit;

    /// <summary>The stamp an assembly wrote beside the level; absent for a level no assembly has taken over yet.</summary>
    public DecisionStamp? Stamp =>
        Provenance?.Cell("rules") is { Length: > 0 } rules && Provenance.Cell("checks") is { Length: > 0 } checks
            ? new DecisionStamp(rules, checks, Provenance.Cell("epoch") ?? string.Empty)
            : null;

    /// <summary>
    /// The key this level was decided under (<see cref="DecisionKey"/>), read from its row and its file; absent when
    /// the row does not say (no row, or one written before the columns existed) or the level is no candidate.
    /// </summary>
    public string? DecisionKey
    {
        get
        {
            if (Content is null || Row?.Cell("calibrationBot") is not string player || Row.Cell("targetWin") is not string share || Row.Cell("moveRange") is not string moves)
            {
                return null;
            }

            return Assembling.DecisionKey.Of(
                player,
                share,
                Row.Cell("moveMargin") ?? string.Empty,
                moves,
                Level.Difficulty,
                Level.Layout.Shape.Width,
                Level.Layout.Shape.Height,
                Level.Spawn.Palette,
                Content.Id);
        }
    }
}

/// <summary>
/// A folder of level files with its report: the live levels, an earlier assembly, or a staging folder. Files are
/// named <c>level_0057.json</c> and loaded when first asked for.
/// </summary>
public sealed class LevelFolder(string root)
{
    private readonly Dictionary<int, FolderLevel?> _levels = [];

    public string Root { get; } = root;

    public ReportTable Report { get; } = ReportTable.Read(Path.Combine(root, GenerationReport.FileName));

    /// <summary>The table of the assembly that wrote the folder; without rows for a folder no assembly wrote.</summary>
    public ReportTable Provenance { get; } = ReportTable.Read(Path.Combine(root, AssemblyWriter.TableName));

    public static string FileNameOf(int number) => string.Create(CultureInfo.InvariantCulture, $"level_{number:0000}.json");

    /// <summary>The level numbers the folder has a file for, ascending.</summary>
    public IReadOnlyList<int> Numbers() => Directory.Exists(Root)
        ? Directory.GetFiles(Root, "level_*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path)["level_".Length..])
            .Where(digits => int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(digits => int.Parse(digits, CultureInfo.InvariantCulture))
            .Order()
            .ToArray()
        : [];

    /// <summary>The level under <paramref name="number"/>; absent when the folder has no file for it.</summary>
    /// <exception cref="LevelFormatException">The file is not a valid level.</exception>
    public FolderLevel? Find(int number)
    {
        if (!_levels.TryGetValue(number, out FolderLevel? level))
        {
            string path = Path.Combine(Root, FileNameOf(number));
            level = File.Exists(path) ? Read(number, path) : null;
            _levels[number] = level;
        }

        return level;
    }

    private FolderLevel Read(int number, string path)
    {
        LevelDefinition level = LevelJson.Load(path);
        return new FolderLevel(number, path, level, ContentOf(level), Report.Row(number), Provenance.Row(number));
    }

    private static CandidateContent? ContentOf(LevelDefinition level)
    {
        try
        {
            return CandidateContent.Of(level);
        }
        catch (CandidateFormatException)
        {
            // Such a level can only be pinned; why it is no candidate is CandidateContent.Of's to say when someone asks.
            return null;
        }
    }
}
