using System.Text;
using System.Text.Json;
using Puzzle.Bank.Measuring;
using Puzzle.Core.Levels;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>What a measurement file holds: its spec and its lines, in file order.</summary>
public sealed record StoredMeasurement(MeasurementSpec Spec, IReadOnlyList<MeasuredLine> Lines);

/// <summary>
/// The measurement file: UTF-8 without a byte order mark, one JSON object per line ended by a line feed, numbers in
/// the invariant form, properties always in the same order. The first line is the <see cref="MeasurementSpec"/>, every
/// other line one <see cref="MeasuredLine"/>. The same lines therefore always give the same bytes.
/// </summary>
public static class MeasurementFile
{
    private const char LineEnd = '\n';

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Where a measurement that is still being played keeps its finished lines.</summary>
    public static string PartPathOf(string path) => path + ".part";

    /// <summary>The whole file. A line that does not read is an error wherever it stands.</summary>
    /// <returns>Absent when no file is there.</returns>
    /// <exception cref="MeasurementFormatException">The file is not a measurement, or a line of it is damaged.</exception>
    public static StoredMeasurement? Read(string path) => ReadLines(path, tornLastLineIsDropped: false);

    /// <summary>
    /// A file that was being appended to when the measurement stopped. Its last line may be torn (it does not end with
    /// a line feed) and is then left out; every line before it must read.
    /// </summary>
    /// <returns>Absent when no file is there, or not even its first line was finished.</returns>
    /// <exception cref="MeasurementFormatException">A finished line of the file is damaged.</exception>
    public static StoredMeasurement? ReadPart(string path) => ReadLines(path, tornLastLineIsDropped: true);

    /// <summary>
    /// Writes the spec and the lines, sorted by what they are for, beside <paramref name="path"/> and then moves the
    /// finished file onto it, so a failed write loses neither the old file nor the new.
    /// </summary>
    /// <exception cref="IOException">The file could not be written or put in place; the message says where the new one is.</exception>
    public static void Write(string path, MeasurementSpec spec, IEnumerable<MeasuredLine> lines)
    {
        string pending = path + ".new";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        using (StreamWriter writer = Create(pending))
        {
            writer.Write(HeaderText(spec));
            writer.Write(LineEnd);
            foreach (MeasuredLine line in lines.OrderBy(line => line.Key))
            {
                writer.Write(LineText(line));
                writer.Write(LineEnd);
            }
        }

        PutInPlace(pending, path);
    }

    /// <summary>Opens a file that takes finished lines one at a time (<see cref="Append"/>); what was there is replaced.</summary>
    public static StreamWriter Create(string path) => new(path, append: false, Utf8) { NewLine = LineEnd.ToString() };

    /// <summary>Writes one finished line and hands it to the disk before returning: a line is either whole in the file or torn at its end.</summary>
    public static void Append(StreamWriter file, string lineText)
    {
        file.Write(lineText);
        file.Write(LineEnd);
        file.Flush();
    }

    public static string HeaderText(MeasurementSpec spec) => Json(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("format", MeasurementSpec.Format);
        writer.WriteString("engine", spec.Engine);
        writer.WriteNumber("moveCap", spec.MoveCap.Value);
        writer.WriteNumber("firstRepeat", spec.FirstRepeat);
        WriteNumbers(writer, "progressMarks", spec.ProgressMarks.Select(mark => mark.Value));
        WriteNumbers(writer, "plainGamesBySeedPosition", spec.PlainGamesBySeedPosition);
        writer.WriteNumber("boostedGamesPerSeed", spec.BoostedGamesPerSeed);
        writer.WriteNumber("startBoosterExtraMoves", spec.StartBoosterExtraMoves.Value);
        writer.WriteNumber("extraMovesOfferMoves", spec.ExtraMovesOfferMoves.Value);
        writer.WriteEndObject();
    });

    public static string LineText(MeasuredLine line) => Json(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("level", line.Level.Value);
        writer.WriteString("fingerprint", line.Fingerprint);
        writer.WriteString("tier", line.Tier.ToString());
        writer.WriteNumber("moveLimit", line.MoveLimit.Value);
        writer.WriteStartArray("pool");
        foreach (uint seed in line.Pool)
        {
            writer.WriteNumberValue(seed);
        }

        writer.WriteEndArray();
        writer.WriteStartObject("player");
        writer.WriteString("name", line.Player.Name);
        writer.WriteNumber("notice", line.Player.NoticePercent);
        writer.WriteNumber("temperature", line.Player.Temperature);
        writer.WriteEndObject();
        writer.WriteString("start", line.Start.FileName());
        writer.WriteStartArray("seeds");
        foreach (SeedGames seed in line.Seeds)
        {
            WriteSeed(writer, seed);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static void WriteSeed(Utf8JsonWriter writer, SeedGames seed)
    {
        MeasuredGame[] games = seed.Games.ToArray();
        writer.WriteStartObject();
        WriteNumbers(writer, "ends", games.Select(game => game.End.ToStored()));
        writer.WriteStartArray("reached");
        int marks = games.Length == 0 ? 0 : games[0].ReachedOn.Count;
        for (int mark = 0; mark < marks; mark++)
        {
            writer.WriteStartArray();
            foreach (MeasuredGame game in games)
            {
                if (game.ReachedOn[mark] is MoveCount move)
                {
                    writer.WriteNumberValue(move.Value);
                }
                else
                {
                    writer.WriteNullValue();
                }
            }

            writer.WriteEndArray();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNumbers(Utf8JsonWriter writer, string name, IEnumerable<int> numbers)
    {
        writer.WriteStartArray(name);
        foreach (int number in numbers)
        {
            writer.WriteNumberValue(number);
        }

        writer.WriteEndArray();
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Utf8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static StoredMeasurement? ReadLines(string path, bool tornLastLineIsDropped)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string text = ReadText(path);
        string[] lines = text.Split(LineEnd);
        int finished = lines.Length - 1; // what follows the last line feed is no finished line
        if (lines[finished].Length > 0 && !tornLastLineIsDropped)
        {
            throw new MeasurementFormatException($"'{path}' is not a whole measurement: its last line is cut off.");
        }

        if (finished == 0)
        {
            return tornLastLineIsDropped ? null : throw new MeasurementFormatException($"'{path}' is not a measurement: it is empty.");
        }

        MeasurementSpec spec = Parsed(path, 1, () => ParseHeader(lines[0]));
        var measured = new List<MeasuredLine>(finished - 1);
        for (int index = 1; index < finished; index++)
        {
            int lineNumber = index + 1;
            measured.Add(Parsed(path, lineNumber, () => ParseLine(lines[index], spec)));
        }

        return new StoredMeasurement(spec, measured);
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path, Utf8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder where the file is expected, or a file this user may not read.
            throw new MeasurementFormatException($"Cannot read the measurement '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Reads one line, turning what a damaged line fails with into an error that names the file and the line.</summary>
    private static T Parsed<T>(string path, int lineNumber, Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            throw new MeasurementFormatException($"'{path}' is not a measurement, or is damaged: line {lineNumber} does not read ({ex.Message}).", ex);
        }
    }

    internal static MeasurementSpec ParseHeader(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement header = document.RootElement;
        int format = header.GetProperty("format").GetInt32();
        if (format != MeasurementSpec.Format)
        {
            throw new FormatException($"format {format}, where this build reads format {MeasurementSpec.Format}");
        }

        return new MeasurementSpec
        {
            Engine = header.GetProperty("engine").GetString() ?? throw new FormatException("no engine fingerprint"),
            MoveCap = new MoveCount(header.GetProperty("moveCap").GetInt32()),
            FirstRepeat = header.GetProperty("firstRepeat").GetInt32(),
            ProgressMarks = Numbers(header.GetProperty("progressMarks")).Select(mark => new Percent(mark)).ToArray(),
            PlainGamesBySeedPosition = Numbers(header.GetProperty("plainGamesBySeedPosition")),
            BoostedGamesPerSeed = header.GetProperty("boostedGamesPerSeed").GetInt32(),
            StartBoosterExtraMoves = new MoveCount(header.GetProperty("startBoosterExtraMoves").GetInt32()),
            ExtraMovesOfferMoves = new MoveCount(header.GetProperty("extraMovesOfferMoves").GetInt32()),
        };
    }

    internal static MeasuredLine ParseLine(string text, MeasurementSpec spec)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        JsonElement line = document.RootElement;
        JsonElement player = line.GetProperty("player");
        string startName = line.GetProperty("start").GetString() ?? string.Empty;
        BoardStart start = BoardStarts.Find(startName) ?? throw new FormatException($"'{startName}' is no board start");
        uint[] pool = line.GetProperty("pool").EnumerateArray().Select(seed => seed.GetUInt32()).ToArray();
        JsonElement[] seeds = line.GetProperty("seeds").EnumerateArray().ToArray();
        if (seeds.Length != pool.Length || pool.Length == 0)
        {
            throw new FormatException($"{seeds.Length} seeds of games for a pool of {pool.Length}");
        }

        return new MeasuredLine
        {
            Level = new LevelId(line.GetProperty("level").GetInt32()),
            Fingerprint = line.GetProperty("fingerprint").GetString() ?? throw new FormatException("no level fingerprint"),
            Tier = Enum.Parse<DifficultyTier>(line.GetProperty("tier").GetString() ?? string.Empty),
            MoveLimit = new MoveCount(line.GetProperty("moveLimit").GetInt32()),
            Pool = pool,
            Player = new PlayerModel(
                player.GetProperty("name").GetString() ?? throw new FormatException("no player name"),
                player.GetProperty("notice").GetInt32(),
                player.GetProperty("temperature").GetDouble()),
            Start = start,
            Seeds = seeds.Select((seed, position) => ParseSeed(seed, spec, spec.GamesOn(start, new SeedPosition(position)))).ToArray(),
        };
    }

    private static SeedGames ParseSeed(JsonElement seed, MeasurementSpec spec, int expectedGames)
    {
        int[] ends = Numbers(seed.GetProperty("ends"));
        JsonElement[] reached = seed.GetProperty("reached").EnumerateArray().ToArray();
        if (ends.Length != expectedGames || reached.Length != spec.ProgressMarks.Count || reached.Any(ofMark => ofMark.GetArrayLength() != ends.Length))
        {
            throw new FormatException($"a seed with {ends.Length} games and {reached.Length} marks, where the file's first line asks for {expectedGames} and {spec.ProgressMarks.Count}");
        }

        if (ends.Any(end => Math.Abs(end) > spec.MoveCap.Value))
        {
            throw new FormatException($"a game that ended past the move cap of {spec.MoveCap}");
        }

        MoveCount?[][] reachedOn = reached
            .Select(ofMark => ofMark.EnumerateArray().Select(move => move.ValueKind == JsonValueKind.Null ? (MoveCount?)null : new MoveCount(move.GetInt32())).ToArray())
            .ToArray();
        var games = new MeasuredGame[ends.Length];
        for (int game = 0; game < games.Length; game++)
        {
            games[game] = new MeasuredGame(GameEnd.FromStored(ends[game]), spec.ProgressMarks, reachedOn.Select(ofMark => ofMark[game]).ToArray());
        }

        return new SeedGames(games, spec.ProgressMarks);
    }

    private static int[] Numbers(JsonElement array) => array.EnumerateArray().Select(number => number.GetInt32()).ToArray();

    /// <summary>Moves the finished new file onto the old one. A failure leaves both and says where the new one is.</summary>
    private static void PutInPlace(string pendingPath, string path)
    {
        try
        {
            File.Move(pendingPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The new measurement is complete at '{pendingPath}', but it could not take the place of '{path}': {ex.Message}", ex);
        }
    }
}
