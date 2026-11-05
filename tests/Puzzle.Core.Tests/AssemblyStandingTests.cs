using System.IO;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>
/// When a level stays as it is and when it is decided anew: a level stands only while everything it was decided from
/// still holds and it really is a level of its number. And what recordings of people protect of a level.
/// </summary>
public class AssemblyStandingTests
{
    private static readonly int[] Numbers = [54, 55, 56];

    private static LevelFolder Live => new(RepoFiles.LiveLevelsFolder);

    /// <summary>An assembly of <see cref="Numbers"/> under an easier policy, written to <paramref name="folder"/>: three levels on their own boards with new limits.</summary>
    private static Assembly AssembledInto(TemporaryFolder folder, OpenTestBank bank)
    {
        Assembly assembly = new Assembler(AssemblySupport.EasierAndLenient, Live, bank.Access, RecordedLevels.None).Run(Numbers);
        AssemblyWriter.Write(assembly, folder.Path);
        return assembly;
    }

    [Fact]
    public void A_level_decided_under_other_rules_of_the_engine_is_decided_anew_instead_of_kept()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, Numbers);
        Assembly made = AssembledInto(first, bank);
        DecisionStamp afterARuleChange = made.Stamp with { Rules = "0123456789abcdef" };

        Assembly sameRules = new Assembler(AssemblySupport.EasierAndLenient, new LevelFolder(first.Path), bank: null, RecordedLevels.None).Run(Numbers);
        Assembly otherRules = new Assembler(AssemblySupport.EasierAndLenient, new LevelFolder(first.Path), bank: null, RecordedLevels.None, afterARuleChange).Run(Numbers);

        Assert.All(sameRules.Decisions, decision => Assert.IsType<LevelDecision.Kept>(decision));
        Assert.All(otherRules.Decisions, decision => Assert.Contains("other rules of the engine or the bot", Assert.IsType<LevelDecision.Unfilled>(decision).NotKept));
        Assert.False(otherRules.IsComplete);
    }

    [Fact]
    public void A_level_decided_on_games_of_an_earlier_epoch_of_the_bank_is_decided_anew_once_the_bank_counts_in_another()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, Numbers);
        Assembly made = AssembledInto(first, bank);
        BankAccess afterAnEpochOpenedByHand = bank.Access with { Epoch = new EpochId(0x77) }; // no board has games of it yet

        Assembly sameEpoch = new Assembler(AssemblySupport.EasierAndLenient, new LevelFolder(first.Path), bank.Access, RecordedLevels.None).Run(Numbers);
        Assembly newEpoch = new Assembler(AssemblySupport.EasierAndLenient, new LevelFolder(first.Path), afterAnEpochOpenedByHand, RecordedLevels.None).Run(Numbers);

        Assert.Equal(BankSupport.SomeEpoch.ToString(), made.Stamp.Epoch);
        Assert.All(sameEpoch.Decisions, decision => Assert.IsType<LevelDecision.Kept>(decision));
        Assert.All(newEpoch.Decisions, decision => Assert.Contains("another epoch of the bank", Assert.IsType<LevelDecision.Unfilled>(decision).NotKept));
    }

    [Fact]
    public void A_level_whose_seed_pool_was_made_by_other_rules_than_the_plan_has_now_is_decided_anew()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, Numbers);
        AssembledInto(first, bank);
        DifficultyPolicy smallerPools = AssemblySupport.EasierAndLenient;
        smallerPools = smallerPools with { Plan = smallerPools.Plan with { SeedPool = new Bot.Simulation.SeedPoolSettings { PoolSize = 4, TolerancePoints = 30 } } };

        Assembly assembly = new Assembler(smallerPools, new LevelFolder(first.Path), bank.Access, RecordedLevels.None).Run(Numbers);

        Assert.All(assembly.Decisions, decision =>
        {
            LevelDecision.Assembled again = Assert.IsType<LevelDecision.Assembled>(decision);
            Assert.True(again.SameBoard);
            Assert.Contains("seed pool and checks", again.NotKept);
            Assert.InRange(again.Finished.PoolSeeds.Count, 3, 4);
        });
    }

    [Fact]
    public void A_kept_level_whose_board_the_plan_no_longer_admits_is_decided_anew()
    {
        LevelFolder live = Live;
        FolderLevel twoFamilies = Enumerable.Range(121, 80).Select(number => live.Find(number)!).First(level => level.Content!.Features.Families.Count == 2 && !level.IsRelaxedOrStale);
        DifficultyPolicy policy = AssemblySupport.DefaultPolicy;
        DifficultyPolicy oneFamilyOnly = policy with { Plan = policy.Plan with { Bands = policy.Plan.Bands.Select(band => band with { MaxObstacleTypes = 1 }).ToArray() } };

        LevelDecision asPlanned = Assert.Single(new Assembler(policy, live, bank: null, RecordedLevels.None).Run([twoFamilies.Number]).Decisions);
        LevelDecision narrowed = Assert.Single(new Assembler(oneFamilyOnly, live, bank: null, RecordedLevels.None).Run([twoFamilies.Number]).Decisions);

        Assert.IsType<LevelDecision.Kept>(asPlanned);
        Assert.Contains("the plan no longer admits its board", Assert.IsType<LevelDecision.Unfilled>(narrowed).NotKept);
    }

    [Fact]
    public void A_kept_level_carries_where_it_came_from_and_what_its_fresh_games_said_into_the_next_assemblys_table()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, Numbers);
        AssembledInto(first, bank);

        AssemblyWriter.Write(new Assembler(AssemblySupport.EasierAndLenient, new LevelFolder(first.Path), bank: null, RecordedLevels.None).Run(Numbers), second.Path);

        var (before, after) = (new LevelFolder(first.Path), new LevelFolder(second.Path));
        Assert.All(Numbers, number =>
        {
            ReportLine decided = before.Find(number)!.Provenance!;
            ReportLine kept = after.Find(number)!.Provenance!;
            Assert.Equal(("same-board", "kept"), (decided.Cell("decision"), kept.Cell("decision")));
            Assert.StartsWith("imported live level", kept.Cell("origin"));
            Assert.All(
                new[] { "candidate", "origin", "record", "draftLimit", "confirmLimit", "limit", "audits", "auditWin", "firstTryWin", "checkWin", "poolSize", "rules", "checks", "epoch" },
                column => Assert.Equal(decided.Cell(column), kept.Cell(column)));
            Assert.NotEqual(string.Empty, kept.Cell("auditWin"));
        });
    }

    [Fact]
    public void An_assembly_replaces_the_folder_it_is_written_to_and_leaves_no_file_of_an_earlier_one()
    {
        using var staging = new TemporaryFolder();
        Assembly three = new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, RecordedLevels.None).Run(Numbers);
        Assembly one = new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, RecordedLevels.None).Run([55]);

        AssemblyWriter.Write(three, staging.Path);
        AssemblyWriter.Write(one, staging.Path);

        Assert.Equal([55], new LevelFolder(staging.Path).Numbers());
        Assert.Empty(AssemblyManifest.Read(staging.Path)!.DifferencesFrom(staging.Path));
        Assert.Equal([staging.Path], Directory.GetDirectories(Path.GetDirectoryName(staging.Path)!, Path.GetFileName(staging.Path) + "*")); // nothing left beside it
    }

    [Fact]
    public void A_manifest_tells_a_folder_that_is_the_assembly_from_one_that_was_changed_afterwards()
    {
        using var staging = new TemporaryFolder();
        AssemblyWriter.Write(new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, RecordedLevels.None).Run(Numbers), staging.Path);
        AssemblyManifest manifest = AssemblyManifest.Read(staging.Path)!;

        File.AppendAllText(staging.File(LevelFolder.FileNameOf(55)), " ");
        File.Delete(staging.File(LevelFolder.FileNameOf(56)));
        File.Copy(Live.Find(60)!.Path, staging.File(LevelFolder.FileNameOf(60)));

        Assert.Equal(
            ["level_0055.json is not the file the assembly wrote", "level_0056.json is missing", "level_0060.json is not a file of the assembly"],
            manifest.DifferencesFrom(staging.Path));
        Assert.Equal(DecisionStamp.Of(AssemblySupport.DefaultPolicy), manifest.Stamp);
        Assert.Equal(3, manifest.Decisions["kept"]);
    }

    [Fact]
    public void Kinds_that_lie_further_from_their_target_than_the_policy_tolerates_are_named_and_can_make_the_assembly_incomplete()
    {
        DifficultyPolicy onlyBoxes = AssemblySupport.DefaultPolicy with { KindShares = new Dictionary<LevelKind, double> { [LevelKind.Boxes] = 1.0, [LevelKind.CollectOnly] = 0 } };
        int[] numbers = Enumerable.Range(54, 40).ToArray();

        Assembly reported = new Assembler(onlyBoxes, Live, bank: null, RecordedLevels.None).Run(numbers);
        Assembly failing = new Assembler(onlyBoxes with { FailOnKindMiss = true }, Live, bank: null, RecordedLevels.None).Run(numbers);

        KindShare boxes = Assert.Single(reported.MissedKinds, kind => kind.Kind == LevelKind.Boxes);
        Assert.InRange(boxes.Share, 0.01, 0.9);
        Assert.Equal(1.0, boxes.Target);
        Assert.True(reported.IsComplete);
        Assert.False(failing.IsComplete);
    }

    [Fact]
    public void A_recorded_level_whose_board_cannot_meet_the_policy_keeps_the_level_it_had_and_never_gets_another_board()
    {
        using var bankFolder = new TemporaryBankFolder();
        LevelFolder live = Live;
        (LevelSlot open, int donor) = TwoLevelsOfOneStructure(live);
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [donor]);
        var recorded = new RecordedLevels(new Dictionary<int, IReadOnlyList<uint>> { [open.Number] = [live.Find(open.Number)!.Level.Attempts.SeedPool[0]] }, onOtherVersions: []);

        LevelDecision free = Assert.Single(new Assembler(AssemblySupport.EasierAndLenient, live, bank.Access, RecordedLevels.None).Run([open.Number]).Decisions);
        LevelDecision protectedByRecordings = Assert.Single(new Assembler(AssemblySupport.EasierAndLenient, live, bank.Access, recorded).Run([open.Number]).Decisions);

        Assert.False(Assert.IsType<LevelDecision.Assembled>(free).SameBoard);
        LevelDecision.Unfilled kept = Assert.IsType<LevelDecision.Unfilled>(protectedByRecordings);
        Assert.Equal(UnfilledCause.RecordedBoard, kept.Cause);
        Assert.Contains("the bank does not hold that board", kept.Reason);
        Assert.Equal(live.Find(open.Number)!.Path, kept.Stale!.Path);
    }

    [Fact]
    public void A_recorded_level_keeps_its_board_under_a_new_limit_with_the_recorded_seeds_first_in_its_pool()
    {
        using var bankFolder = new TemporaryBankFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [54]);
        IReadOnlyList<uint> livePool = Live.Find(54)!.Level.Attempts.SeedPool;
        uint[] played = [livePool[^1], livePool[^2]];
        var recorded = new RecordedLevels(new Dictionary<int, IReadOnlyList<uint>> { [54] = played }, onOtherVersions: []);

        LevelDecision.Assembled made = Assert.IsType<LevelDecision.Assembled>(
            Assert.Single(new Assembler(AssemblySupport.EasierAndLenient, Live, bank.Access, recorded).Run([54]).Decisions));

        Assert.True(made.SameBoard);
        Assert.Equal(Live.Find(54)!.Content!.Id, made.Candidate.Content.Id);
        Assert.Equal(played.Order(), made.Level.Attempts.SeedPool.Take(2).Order());
    }

    [Fact]
    public void Only_recordings_of_the_very_file_a_folder_holds_count_as_recordings_of_its_level()
    {
        LevelFolder live = Live;
        LevelDefinition level60 = live.Find(60)!.Level;
        LevelDefinition level61 = live.Find(61)!.Level;
        LevelDefinition anOlderLevel60 = level60.WithMoveLimit(level60.MoveLimit + 3);
        uint poolSeed = level60.Attempts.SeedPool[2];
        AttemptRecord[] records =
        [
            TestRecords.Of(level60, AttemptResult.Won, 20, lab: TestRecords.Lab(level60), seeds: AttemptSeeds.From(poolSeed)),
            TestRecords.Of(level60, AttemptResult.OutOfMoves, 20, lab: TestRecords.Lab(level60), seeds: AttemptSeeds.From(poolSeed)),
            TestRecords.Of(level60, AttemptResult.OutOfMoves, 20, lab: TestRecords.Lab(level60), seeds: AttemptSeeds.From(level60.Attempts.SeedPool[0])),
            TestRecords.Of(anOlderLevel60, AttemptResult.Won, 20, lab: TestRecords.Lab(anOlderLevel60)),
            TestRecords.Of(level61, AttemptResult.Won, 20, lab: TestRecords.Lab(level61.WithMoveLimit(level61.MoveLimit + 1))),
            TestRecords.Of(level61, AttemptResult.Won, 20, lab: null),
        ];

        RecordedLevels recorded = RecordedLevels.Of(records, live);

        Assert.Equal([60], recorded.Numbers);
        Assert.Equal([poolSeed, level60.Attempts.SeedPool[0]], recorded.SeedsOf(60));
        Assert.Equal([61], recorded.OnOtherVersions);
        Assert.False(recorded.Contains(61));
        Assert.Equal(3, RecordedLevels.FingerprintsIn(records).Count);
    }

    /// <summary>A level to decide and another live level whose board has the first one's structure, so it can stand in for it.</summary>
    private static (LevelSlot Open, int Donor) TwoLevelsOfOneStructure(LevelFolder live)
    {
        LevelSlot[] slots = Enumerable.Range(54, 60).Select(number => AssemblySupport.Slot(number)).ToArray();
        return (
            from open in slots
            from donor in slots
            where donor.Number != open.Number && open.Structure.Admits(live.Find(donor.Number)!.Content!.Features)
            select (open, donor.Number)).First();
    }
}
