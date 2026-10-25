using System.Globalization;
using System.Text;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Reporting;

/// <summary>
/// How a value is written into a report: invariant culture whatever the machine's, and an unknown value as an empty
/// cell, never as 0. A reader must be able to tell "nothing" from "not known".
/// </summary>
public static class Cell
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Whole(long? number) => number?.ToString(Invariant) ?? string.Empty;

    public static string Whole(Coins? coins) => Whole(coins?.Amount);

    /// <summary>An amount of US money in dollars to the cent, without the sign, so that a column holds numbers: 1.99.</summary>
    public static string Money(UsDollars? amount) => amount?.Dollars.ToString("0.00", Invariant) ?? string.Empty;

    /// <summary>A share between 0 and 1, to three places.</summary>
    public static string Share(double? share) => share?.ToString("0.000", Invariant) ?? string.Empty;

    /// <summary>A number with one decimal place: hours, minutes, a mean of counts.</summary>
    public static string Number(double? number) => number?.ToString("0.0", Invariant) ?? string.Empty;

    /// <summary>A mean of small counts, to two places.</summary>
    public static string Fine(double? number) => number?.ToString("0.00", Invariant) ?? string.Empty;

    public static string YesOrNo(bool value) => value ? "yes" : "no";
}

/// <summary>A report as rows of cells under named columns.</summary>
public sealed class CsvTable
{
    private const char Separator = ',';

    private readonly string[] _columns;
    private readonly List<string[]> _rows = [];

    public CsvTable(params string[] columns) => _columns = columns;

    public int RowCount => _rows.Count;

    /// <exception cref="ArgumentException">Not one cell per column, or a cell that would break the row apart.</exception>
    public void Add(params string[] cells)
    {
        if (cells.Length != _columns.Length)
        {
            throw new ArgumentException($"A row of {cells.Length} cells for {_columns.Length} columns.", nameof(cells));
        }

        if (cells.FirstOrDefault(cell => cell.Contains(Separator) || cell.Contains('\n') || cell.Contains('"')) is { } unsafeCell)
        {
            throw new ArgumentException($"The cell '{unsafeCell}' holds a character that would break its row apart.", nameof(cells));
        }

        _rows.Add(cells);
    }

    /// <summary>The header and the rows, each ended by a line feed.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        text.Append(string.Join(Separator, _columns)).Append(ReportFiles.LineEnd);
        foreach (string[] row in _rows)
        {
            text.Append(string.Join(Separator, row)).Append(ReportFiles.LineEnd);
        }

        return text.ToString();
    }
}

/// <summary>Report files are UTF-8 without a byte order mark and end their lines with a line feed, so the same run gives the same bytes on every machine.</summary>
public static class ReportFiles
{
    public const char LineEnd = '\n';

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A text that was built line by line, with the line ends every report has whatever the machine's own are.</summary>
    public static string WithLineFeeds(StringBuilder text) => text.ToString().ReplaceLineEndings(LineEnd.ToString());

    /// <exception cref="IOException">The file could not be written.</exception>
    public static void Write(string folder, string name, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), text.ReplaceLineEndings(LineEnd.ToString()), Utf8);
    }
}
