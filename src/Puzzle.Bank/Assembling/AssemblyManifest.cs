using System.Security.Cryptography;
using System.Text.Json;
using Puzzle.Generator;

namespace Puzzle.Bank.Assembling;

/// <summary>The plan an assembly was made on: the file's name and the SHA-256 of its bytes.</summary>
public sealed record ManifestPlan(string File, string Sha256);

/// <summary>The bank an assembly chose from.</summary>
/// <param name="Cut">Every shard file with its length when the assembly read the bank: the content the choice was made from.</param>
public sealed record ManifestBank(string Id, string Epoch, int Candidates, IReadOnlyList<ShardLength> Cut);

public sealed record ManifestKind(string Kind, int Levels, double Share, double Target, bool Missed);

public sealed record ManifestShortfall(int Level, string Kind, string Detail);

/// <param name="Candidate">Absent for a number without a level and for a level that is no bank candidate.</param>
/// <param name="Key">The decision key of a level the assembly decided or kept.</param>
/// <param name="Sha256">The SHA-256 of the level file as written; absent for a number without a level.</param>
public sealed record ManifestLevel(int Level, string Decision, string? Candidate, string? Key, string? Sha256);

/// <summary>
/// What an assembly was made with and what it came to, written beside its levels: the policy in full, the plan, the
/// stamp its levels stand under, the bank and how much of it there was, the decisions, the mix of kinds against the
/// targets, every level that does not meet the policy, and a digest of every file. The digests are what makes a
/// folder checkable: a set that is taken live is first shown to be the set that was assembled.
/// </summary>
/// <param name="Policy">The policy file as it was read.</param>
/// <param name="Files">The SHA-256 of the two tables, by file name.</param>
public sealed record AssemblyManifest(
    int Format,
    JsonElement Policy,
    ManifestPlan Plan,
    string StartedFrom,
    DecisionStamp Stamp,
    ManifestBank? Bank,
    IReadOnlyDictionary<string, int> Decisions,
    IReadOnlyList<ManifestKind> Kinds,
    IReadOnlyList<ManifestShortfall> Shortfalls,
    IReadOnlyDictionary<string, string> Files,
    IReadOnlyList<ManifestLevel> Levels)
{
    /// <summary>JSON inside, but not named *.json: every tool that reads a folder of levels takes each *.json in it for a level.</summary>
    public const string FileName = "assembly.manifest";

    /// <summary>2: file digests, the stamp, the bank's cut, the kinds and the shortfalls. A manifest of format 1 has none of them and cannot be checked.</summary>
    public const int CurrentFormat = 2;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonDocumentOptions PolicyJson = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>The manifest of <paramref name="assembly"/> as its level files and tables lie in <paramref name="folder"/>.</summary>
    public static AssemblyManifest Of(Assembly assembly, string folder)
    {
        using JsonDocument policy = JsonDocument.Parse(assembly.Policy.SourceText, PolicyJson);
        return new AssemblyManifest(
            CurrentFormat,
            policy.RootElement.Clone(),
            new ManifestPlan(Path.GetFileName(assembly.Policy.PlanPath), Sha256Of(assembly.Policy.PlanPath)),
            Path.GetFileName(Path.TrimEndingDirectorySeparator(assembly.Base.Root)),
            assembly.Stamp,
            assembly.Bank is BankAccess bank ? new ManifestBank(bank.BankId, bank.Epoch.ToString(), bank.Catalog.Candidates.Count, bank.Cut ?? []) : null,
            assembly.Decisions.GroupBy(AssemblyWriter.DecisionName).OrderBy(group => group.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count()),
            assembly.KindMix.Select(kind => new ManifestKind(kind.Kind.ToString(), kind.Levels, Math.Round(kind.Share, 4), kind.Target, assembly.MissedKinds.Contains(kind))).ToArray(),
            assembly.Shortfalls.Select(shortfall => new ManifestShortfall(shortfall.Level, shortfall.Kind.ToString(), shortfall.Detail)).ToArray(),
            new[] { GenerationReport.FileName, AssemblyWriter.TableName }.ToDictionary(name => name, name => Sha256Of(Path.Combine(folder, name))),
            assembly.Decisions.Select(decision => LevelOf(decision, folder)).ToArray());
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The manifest of an assembled folder; absent when the folder has none.</summary>
    /// <exception cref="AssemblyException">The file is no manifest this build reads (an older format, or not JSON).</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static AssemblyManifest? Read(string folder)
    {
        string path = Path.Combine(folder, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            AssemblyManifest? manifest = JsonSerializer.Deserialize<AssemblyManifest>(File.ReadAllText(path), Json);
            return manifest is { Format: CurrentFormat, Levels: not null, Files: not null, Shortfalls: not null, Stamp: not null }
                ? manifest
                : throw new AssemblyException($"'{path}' was written by an older assembler (format {manifest?.Format}): it has no file digests to check the folder against. Assemble the set again.");
        }
        catch (JsonException ex)
        {
            throw new AssemblyException($"'{path}' is not an assembly manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// What in <paramref name="folder"/> is not what this manifest describes: a file that is missing, a file whose
    /// bytes changed, a level file the manifest does not know. Empty for the folder as the assembly wrote it.
    /// </summary>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public IReadOnlyList<string> DifferencesFrom(string folder)
    {
        var expected = Files.ToDictionary(file => file.Key, file => file.Value);
        foreach (ManifestLevel level in Levels.Where(level => level.Sha256 is not null))
        {
            expected[LevelFolder.FileNameOf(level.Level)] = level.Sha256!;
        }

        var differences = new List<string>();
        foreach ((string name, string sha256) in expected.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            string path = Path.Combine(folder, name);
            if (!File.Exists(path))
            {
                differences.Add($"{name} is missing");
            }
            else if (Sha256Of(path) != sha256)
            {
                differences.Add($"{name} is not the file the assembly wrote");
            }
        }

        differences.AddRange(new LevelFolder(folder).Numbers()
            .Select(LevelFolder.FileNameOf)
            .Where(name => !expected.ContainsKey(name))
            .Select(name => $"{name} is not a file of the assembly"));
        return differences;
    }

    private static ManifestLevel LevelOf(LevelDecision decision, string folder)
    {
        string path = Path.Combine(folder, LevelFolder.FileNameOf(decision.Number));
        return new ManifestLevel(
            decision.Number,
            AssemblyWriter.DecisionName(decision),
            AssemblyWriter.ContentOf(decision)?.Id.ToString(),
            AssemblyWriter.KeyOf(decision) is { Length: > 0 } key ? key : null,
            File.Exists(path) ? Sha256Of(path) : null);
    }

    private static string Sha256Of(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
