using System.Security.Cryptography;
using Puzzle.Generator;
using Puzzle.Tooling;

namespace Puzzle.Bank.Assembling;

/// <summary>A staged set may not go live as it is; the message says what stands in the way and what would lift it.</summary>
public sealed class PromotionRefusedException(string message) : Exception(message);

/// <summary>What taking a staged set live would do, worked out before anything is touched.</summary>
/// <param name="Unchanged">Numbers whose live file is already the staged file.</param>
/// <param name="Replaced">Numbers whose live file gives way to another.</param>
/// <param name="Added">Numbers the live folder has no file for yet.</param>
/// <param name="Archived">Of the replaced numbers, those whose live file people have recorded attempts on: it is copied to the archive first.</param>
public sealed record PromotionPlan(
    string Staging,
    string Live,
    AssemblyManifest Manifest,
    IReadOnlyList<int> Unchanged,
    IReadOnlyList<int> Replaced,
    IReadOnlyList<int> Added,
    IReadOnlyList<int> Archived);

/// <summary>
/// Takes an assembled set live: the level files, the report, the assembly's table and its manifest replace those of
/// the live folder together. Before anything is touched the set is shown to be what it claims:
/// <list type="bullet">
/// <item>the staging folder is the folder its manifest describes, file for file;</item>
/// <item>every level it calls pinned or kept is the very file that is live, so the set was assembled from the live
/// folder and "kept" means what a player would call it;</item>
/// <item>every level that does not meet the policy (<see cref="LevelShortfall"/>) was accepted by number;</item>
/// <item>it has a level for every number that is live.</item>
/// </list>
/// A live file that people have recorded attempts on and that gives way to another is copied to the archive first,
/// with its report row, where the comparison of people and bots finds it by its fingerprint. Every file that is
/// replaced is also copied to a backup folder, so the step can be taken back by copying the backup over the live
/// folder. The server reads the levels when it starts, so this is done with the server stopped; whoever calls this
/// sees to that.
/// </summary>
public static class Promotion
{
    private static readonly string[] Tables = [GenerationReport.FileName, AssemblyWriter.TableName, AssemblyManifest.FileName];

    /// <param name="recordedFingerprints">The fingerprints of the level files people have recorded attempts on.</param>
    /// <param name="accepted">Level numbers that may go live although they do not meet the policy.</param>
    /// <exception cref="PromotionRefusedException">One of the conditions above does not hold.</exception>
    /// <exception cref="AssemblyException">The staging folder's manifest does not read.</exception>
    /// <exception cref="IOException">A file cannot be read.</exception>
    public static PromotionPlan Plan(string staging, string live, IReadOnlySet<string> recordedFingerprints, IReadOnlySet<int> accepted)
    {
        AssemblyManifest manifest = AssemblyManifest.Read(staging)
            ?? throw new PromotionRefusedException($"'{staging}' has no {AssemblyManifest.FileName}: it is not an assembled set.");
        RequireWhole(manifest, staging);
        RequireAccepted(manifest, accepted);

        var liveLevels = new LevelFolder(live);
        var staged = manifest.Levels.Where(level => level.Sha256 is not null).ToDictionary(level => level.Level);
        RequireEveryLiveNumber(liveLevels, staged, staging);
        var (unchanged, replaced, added, archived) = (new List<int>(), new List<int>(), new List<int>(), new List<int>());
        foreach (ManifestLevel level in staged.Values.OrderBy(level => level.Level))
        {
            string livePath = Path.Combine(live, LevelFolder.FileNameOf(level.Level));
            if (!File.Exists(livePath))
            {
                added.Add(level.Level);
            }
            else if (Sha256Of(livePath) == level.Sha256)
            {
                unchanged.Add(level.Level);
            }
            else
            {
                RequireDeclaredChange(level, live);
                replaced.Add(level.Level);
                if (recordedFingerprints.Contains(LevelJson.Fingerprint(liveLevels.Find(level.Level)!.Level)))
                {
                    archived.Add(level.Level);
                }
            }
        }

        return new PromotionPlan(staging, live, manifest, unchanged, replaced, added, archived);
    }

    /// <summary>
    /// Carries the plan out: archive, backup, then the level files, and the tables and the manifest last, so a run
    /// that is cut short leaves the live manifest describing the set that was live before, and running it again
    /// finishes the step.
    /// </summary>
    /// <param name="archive">Where replaced files with recordings go (a folder of its own under levels/archive).</param>
    /// <param name="backup">Where every replaced file goes, outside the level folders.</param>
    /// <exception cref="PromotionRefusedException">The archive already holds another file under a name it needs.</exception>
    /// <exception cref="IOException">A file could not be copied; what was copied before stays.</exception>
    public static void CarryOut(PromotionPlan plan, string archive, string backup)
    {
        Archive(plan, archive);
        Directory.CreateDirectory(backup);
        foreach (string name in plan.Replaced.Select(LevelFolder.FileNameOf).Concat(Tables).Where(name => File.Exists(Path.Combine(plan.Live, name))))
        {
            File.Copy(Path.Combine(plan.Live, name), Path.Combine(backup, name), overwrite: true);
        }

        Directory.CreateDirectory(plan.Live);
        foreach (string name in plan.Replaced.Concat(plan.Added).Order().Select(LevelFolder.FileNameOf).Concat(Tables))
        {
            File.Copy(Path.Combine(plan.Staging, name), Path.Combine(plan.Live, name), overwrite: true);
        }
    }

    private static void Archive(PromotionPlan plan, string archive)
    {
        if (plan.Archived.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(archive);
        ReportTable liveReport = new LevelFolder(plan.Live).Report;
        var rows = new List<string> { string.Join(',', liveReport.Columns) };
        foreach (int number in plan.Archived)
        {
            string name = LevelFolder.FileNameOf(number);
            string kept = Path.Combine(archive, name);
            byte[] played = File.ReadAllBytes(Path.Combine(plan.Live, name));
            if (File.Exists(kept) && !File.ReadAllBytes(kept).AsSpan().SequenceEqual(played))
            {
                throw new PromotionRefusedException($"The archive '{archive}' already holds another {name}; name an archive folder of its own for this step (--archive).");
            }

            File.WriteAllBytes(kept, played);
            if (liveReport.Row(number) is ReportLine row)
            {
                rows.Add(row.Text);
            }
        }

        File.WriteAllText(Path.Combine(archive, GenerationReport.FileName), string.Concat(rows.Select(row => row + liveReport.Newline)));
    }

    private static void RequireWhole(AssemblyManifest manifest, string staging)
    {
        IReadOnlyList<string> differences = manifest.DifferencesFrom(staging);
        if (differences.Count > 0)
        {
            throw new PromotionRefusedException(
                $"'{staging}' is not the set its manifest describes ({differences.Count} files; first: {differences[0]}). Assemble it again; a staged set is never edited by hand.");
        }
    }

    private static void RequireAccepted(AssemblyManifest manifest, IReadOnlySet<int> accepted)
    {
        ManifestShortfall[] open = manifest.Shortfalls.Where(shortfall => !accepted.Contains(shortfall.Level)).ToArray();
        if (open.Length > 0)
        {
            string listed = string.Join("; ", open.Take(8).Select(shortfall => $"{shortfall.Level} ({shortfall.Kind}: {shortfall.Detail})"));
            throw new PromotionRefusedException(
                $"{open.Length} levels of the set do not meet its policy: {listed}{(open.Length > 8 ? "; ..." : string.Empty)}. "
                + $"Grow the bank and assemble again, or take them live as they are by number: --accept {string.Join(',', open.Select(shortfall => shortfall.Level))}.");
        }
    }

    private static void RequireEveryLiveNumber(LevelFolder liveLevels, Dictionary<int, ManifestLevel> staged, string staging)
    {
        int[] uncovered = liveLevels.Numbers().Where(number => !staged.ContainsKey(number)).ToArray();
        if (uncovered.Length > 0)
        {
            throw new PromotionRefusedException(
                $"'{staging}' has no level for {uncovered.Length} numbers that are live (first: {uncovered[0]}). The report and the manifest are replaced whole, so assemble every live number (--from 1 --to {liveLevels.Numbers()[^1]}).");
        }
    }

    /// <summary>
    /// A level the set calls pinned or kept is by its own word the file that was in the folder it started from. When
    /// the live file is another, the set was assembled from another folder than the live one, and nothing it says
    /// about what stays is true of the live levels.
    /// </summary>
    private static void RequireDeclaredChange(ManifestLevel level, string live)
    {
        if (level.Decision is "pinned" or "kept")
        {
            throw new PromotionRefusedException(
                $"Level {level.Level} is '{level.Decision}' in the staged set, yet it is another file than the one in '{live}': the set was not assembled from the live folder. "
                + $"Assemble it again with --base {live}.");
        }
    }

    private static string Sha256Of(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
