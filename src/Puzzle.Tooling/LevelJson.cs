using System.Security.Cryptography;
using System.Text.Json;
using Puzzle.Core.Levels;

namespace Puzzle.Tooling;

/// <summary>Reads and writes level files with System.Text.Json (Unity reads the same files with its own serializer).</summary>
public static class LevelJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Unindented, so the bytes do not depend on the platform's line ending.</summary>
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        IncludeFields = true,
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

        return Parse(json, $"'{path}'");
    }

    /// <summary>The level a level file's text holds.</summary>
    /// <param name="source">Where the text comes from, for the message of a failure: a quoted path, or what holds it.</param>
    /// <exception cref="LevelFormatException">The text is not JSON, or not a valid level.</exception>
    public static LevelDefinition Parse(string json, string source)
    {
        LevelFile file;
        try
        {
            file = JsonSerializer.Deserialize<LevelFile>(json, Options)
                ?? throw new LevelFormatException($"{source} contains no level.");
        }
        catch (JsonException ex)
        {
            throw new LevelFormatException($"{source} is not valid level JSON: {ex.Message}", ex);
        }

        return LevelFileMapper.ToDefinition(file);
    }

    public static void Save(LevelDefinition level, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(LevelFileMapper.ToFile(level), Options));

    /// <summary>
    /// The level as one line of level-file JSON, the same on every platform: what <see cref="Fingerprint"/> hashes, and
    /// what a tool writes when it keeps a level inside a record of its own. <see cref="Parse"/> reads it back.
    /// </summary>
    public static string ToCanonicalJson(LevelDefinition level) =>
        JsonSerializer.Serialize(LevelFileMapper.ToFile(level), CanonicalOptions);

    /// <summary>
    /// A short id of the level's exact content: 16 hex digits of a SHA-256 over its canonical file form. Attempt records
    /// carry it, so a comparison never mixes attempts of a sample level, or of an older version of a level, with the
    /// current file of the same id. Any change to the level file format changes every fingerprint.
    /// </summary>
    public static string Fingerprint(LevelDefinition level)
    {
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(LevelFileMapper.ToFile(level), CanonicalOptions);
        return Convert.ToHexStringLower(SHA256.HashData(canonical))[..16];
    }
}
