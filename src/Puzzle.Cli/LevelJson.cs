using System.Text.Json;
using Puzzle.Core.Levels;

namespace Puzzle.Cli;

/// <summary>Reads and writes level files with System.Text.Json (Unity reads the same files with its own serializer).</summary>
internal static class LevelJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <exception cref="LevelFormatException">The file is missing, not JSON, or not a valid level.</exception>
    public static LevelDefinition Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new LevelFormatException($"Cannot read '{path}': {ex.Message}", ex);
        }

        LevelFile file;
        try
        {
            file = JsonSerializer.Deserialize<LevelFile>(json, Options)
                ?? throw new LevelFormatException($"'{path}' contains no level.");
        }
        catch (JsonException ex)
        {
            throw new LevelFormatException($"'{path}' is not valid level JSON: {ex.Message}", ex);
        }

        return LevelFileMapper.ToDefinition(file);
    }

    public static void Save(LevelDefinition level, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(LevelFileMapper.ToFile(level), Options));
}
