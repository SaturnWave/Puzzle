using System.IO;
using Puzzle.Bank.Assembling;
using Puzzle.Generator;
using Puzzle.Tooling;

namespace Puzzle.Core.Tests;

/// <summary>
/// Taking an assembled set live, on a stand-in for the live folder: a temporary copy of a few live levels with their
/// report rows. The real live folder is only ever read.
/// </summary>
public class PromotionTests
{
    /// <summary>Two pinned levels, one that meets the policy, the two the generator relaxed, and three more that meet it.</summary>
    private static readonly int[] Numbers = [49, 50, 51, 52, 53, 54, 55, 56];

    private static readonly HashSet<string> NothingRecorded = [];

    private static readonly HashSet<int> TheRelaxedTwo = [52, 53];

    private static void Assemble(DifficultyPolicy policy, LevelFolder from, BankAccess? bank, string into) =>
        AssemblyWriter.Write(new Assembler(policy, from, bank, RecordedLevels.None).Run(Numbers), into);

    [Fact]
    public void A_set_with_levels_that_do_not_meet_the_policy_goes_live_only_when_they_are_accepted_by_number()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        Assemble(AssemblySupport.DefaultPolicy, AssemblySupport.CopyOfLive(live, Numbers), bank: null, staging.Path);

        var refused = Assert.Throws<PromotionRefusedException>(() => Promotion.Plan(staging.Path, live.Path, NothingRecorded, accepted: new HashSet<int> { 52 }));
        PromotionPlan plan = Promotion.Plan(staging.Path, live.Path, NothingRecorded, TheRelaxedTwo);

        Assert.Contains("--accept 53", refused.Message);
        Assert.Equal((8, 0, 0, 0), (plan.Unchanged.Count, plan.Replaced.Count, plan.Added.Count, plan.Archived.Count));
    }

    [Fact]
    public void Going_live_replaces_levels_and_tables_together_archives_what_people_recorded_and_backs_up_what_it_replaces()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        using var elsewhere = new TemporaryFolder();
        using var bankFolder = new TemporaryBankFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [54, 55, 56]);
        LevelFolder before = AssemblySupport.CopyOfLive(live, Numbers);
        byte[] level55Before = File.ReadAllBytes(before.Find(55)!.Path);
        string row55Before = before.Find(55)!.Row!.Text;
        var recorded = new HashSet<string> { LevelJson.Fingerprint(before.Find(55)!.Level), LevelJson.Fingerprint(before.Find(51)!.Level) };
        Assemble(AssemblySupport.EasierAndLenient, before, bank.Access, staging.Path);
        (string archive, string backup) = (elsewhere.File("archive"), elsewhere.File("backup"));

        PromotionPlan plan = Promotion.Plan(staging.Path, live.Path, recorded, accepted: new HashSet<int> { 51, 52, 53 });
        Promotion.CarryOut(plan, archive, backup);

        Assert.Equal([54, 55, 56], plan.Replaced);
        Assert.Equal([55], plan.Archived); // 51 has recordings too, but its file stays: nothing to archive
        Assert.All(
            Numbers.Select(LevelFolder.FileNameOf).Append(GenerationReport.FileName).Append(AssemblyWriter.TableName).Append(AssemblyManifest.FileName),
            name => Assert.Equal(File.ReadAllBytes(staging.File(name)), File.ReadAllBytes(live.File(name))));
        Assert.Equal(level55Before, File.ReadAllBytes(Path.Combine(archive, LevelFolder.FileNameOf(55))));
        Assert.Equal([string.Join(',', before.Report.Columns), row55Before], File.ReadAllLines(Path.Combine(archive, GenerationReport.FileName)));
        Assert.Equal(level55Before, File.ReadAllBytes(Path.Combine(backup, LevelFolder.FileNameOf(55))));
        Assert.True(File.Exists(Path.Combine(backup, GenerationReport.FileName)));
        Assert.Empty(AssemblyManifest.Read(live.Path)!.DifferencesFrom(live.Path));
    }

    [Fact]
    public void A_set_assembled_from_another_folder_than_the_live_one_is_refused()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        LevelFolder liveLevels = AssemblySupport.CopyOfLive(live, Numbers);
        Assemble(AssemblySupport.DefaultPolicy, liveLevels, bank: null, staging.Path);
        LevelJson.Save(liveLevels.Find(54)!.Level.WithMoveLimit(99), live.File(LevelFolder.FileNameOf(54)));

        var refused = Assert.Throws<PromotionRefusedException>(() => Promotion.Plan(staging.Path, live.Path, NothingRecorded, TheRelaxedTwo));

        Assert.Contains("Level 54 is 'kept'", refused.Message);
        Assert.Contains("not assembled from the live folder", refused.Message);
    }

    [Fact]
    public void A_staged_set_that_was_changed_after_it_was_assembled_is_refused()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        Assemble(AssemblySupport.DefaultPolicy, AssemblySupport.CopyOfLive(live, Numbers), bank: null, staging.Path);
        File.AppendAllText(staging.File(GenerationReport.FileName), "1001,Easy\n");

        var refused = Assert.Throws<PromotionRefusedException>(() => Promotion.Plan(staging.Path, live.Path, NothingRecorded, TheRelaxedTwo));

        Assert.Contains("report.csv is not the file the assembly wrote", refused.Message);
    }

    [Fact]
    public void A_set_without_a_level_for_a_number_that_is_live_is_refused()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        LevelFolder liveLevels = AssemblySupport.CopyOfLive(live, Numbers);
        AssemblyWriter.Write(new Assembler(AssemblySupport.DefaultPolicy, liveLevels, bank: null, RecordedLevels.None).Run([49, 50, 51]), staging.Path);

        var refused = Assert.Throws<PromotionRefusedException>(() => Promotion.Plan(staging.Path, live.Path, NothingRecorded, TheRelaxedTwo));

        Assert.Contains("no level for 5 numbers that are live (first: 52)", refused.Message);
    }

    [Fact]
    public void A_folder_no_assembly_wrote_is_no_set_to_take_live()
    {
        using var live = new TemporaryFolder();
        using var staging = new TemporaryFolder();
        AssemblySupport.CopyOfLive(staging, Numbers);

        var refused = Assert.Throws<PromotionRefusedException>(() => Promotion.Plan(staging.Path, live.Path, NothingRecorded, TheRelaxedTwo));

        Assert.Contains("assembly.manifest", refused.Message);
    }
}
