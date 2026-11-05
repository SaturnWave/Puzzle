using System.IO;
using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;
using Puzzle.Tooling;

namespace Puzzle.Core.Tests;

/// <summary>Whole assemblies: the live levels as the base folder (only ever read), a small bank in a temporary folder, a staging folder beside it.</summary>
public class AssemblyTests
{
    private static readonly RecordedLevels NobodyRecorded = RecordedLevels.None;

    private static DifficultyPolicy Lenient(DifficultyPolicy policy, MoveRange? moves = null) => AssemblySupport.Lenient(policy, moves);

    private static LevelFolder Live => new(RepoFiles.LiveLevelsFolder);

    /// <summary>The live levels from 51 on for which the generator gave up the move range: its report says so, and their limits lie outside the range.</summary>
    private static readonly int[] RelaxedByTheGenerator = [52, 53];

    [Fact]
    public void Under_the_default_policy_the_live_levels_that_meet_it_come_out_byte_for_byte_and_need_no_bank()
    {
        using var staging = new TemporaryFolder();
        int[] numbers = Enumerable.Range(1, 120).ToArray();

        Assembly assembly = new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, NobodyRecorded).Run(numbers);
        AssemblyWriter.Write(assembly, staging.Path);

        Assert.Equal(50, assembly.Decisions.Count(decision => decision is LevelDecision.Pinned));
        Assert.Equal(68, assembly.Decisions.Count(decision => decision is LevelDecision.Kept));
        Assert.Equal(RelaxedByTheGenerator, assembly.Decisions.OfType<LevelDecision.Unfilled>().Select(level => level.Number));
        Assert.All(numbers, number => Assert.Equal(
            File.ReadAllBytes(Path.Combine(RepoFiles.LiveLevelsFolder, LevelFolder.FileNameOf(number))),
            File.ReadAllBytes(staging.File(LevelFolder.FileNameOf(number)))));
        string[] liveRows = File.ReadAllLines(Path.Combine(RepoFiles.LiveLevelsFolder, GenerationReport.FileName)).Take(121).ToArray();
        string[] rows = File.ReadAllLines(staging.File(GenerationReport.FileName));
        Assert.Equal(liveRows.Where((_, line) => !RelaxedByTheGenerator.Contains(line)), rows.Where((_, line) => !RelaxedByTheGenerator.Contains(line)));
    }

    [Fact]
    public void A_level_whose_limit_lies_outside_its_range_does_not_stand_and_is_named_in_the_set_it_stays_in()
    {
        using var staging = new TemporaryFolder();

        Assembly assembly = new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, NobodyRecorded).Run([51, 52, 53, 54]);
        AssemblyWriter.Write(assembly, staging.Path);

        LevelDecision.Unfilled relaxed = Assert.IsType<LevelDecision.Unfilled>(assembly.Decisions[1]);
        Assert.Contains("outside its range 20-33", relaxed.NotKept);
        Assert.Equal([(52, ShortfallKind.Stale), (53, ShortfallKind.Stale)], assembly.Shortfalls.Select(shortfall => (shortfall.Level, shortfall.Kind)));
        Assert.False(assembly.IsComplete);
        var written = new LevelFolder(staging.Path);
        Assert.Equal(["no", "stale", "stale", "no"], new[] { 51, 52, 53, 54 }.Select(number => written.Find(number)!.Row!.Cell("relaxed")));
        Assert.Equal(Live.Find(52)!.Row!.Text.Replace(",yes,", ",stale,", StringComparison.Ordinal), written.Find(52)!.Row!.Text);
        Assert.Equal([52, 53], AssemblyManifest.Read(staging.Path)!.Shortfalls.Select(shortfall => shortfall.Level));
    }

    [Fact]
    public void Every_json_file_of_an_assembly_is_a_level_so_the_tools_that_read_a_folder_of_levels_can_read_it()
    {
        using var staging = new TemporaryFolder();

        AssemblyWriter.Write(new Assembler(AssemblySupport.DefaultPolicy, Live, bank: null, NobodyRecorded).Run([49, 50, 51, 54]), staging.Path);

        LevelLibrary library = LevelLibrary.Load(staging.Path);
        Assert.Empty(library.Problems);
        Assert.Equal(4, library.Levels.Count);
        Assert.True(File.Exists(staging.File(AssemblyManifest.FileName)));
        Assert.True(File.Exists(staging.File(AssemblyWriter.TableName)));
    }

    [Fact]
    public void A_pinned_number_without_a_file_to_copy_stops_the_assembly()
    {
        using var empty = new TemporaryFolder();

        var refused = Assert.Throws<AssemblyException>(() => new Assembler(AssemblySupport.DefaultPolicy, new LevelFolder(empty.Path), bank: null, NobodyRecorded).Run([7]));

        Assert.Contains("Level 7 ", refused.Message);
    }

    [Fact]
    public void A_level_that_no_longer_stands_and_finds_no_candidate_is_reported_unfilled_and_keeps_the_level_it_had()
    {
        using var staging = new TemporaryFolder();
        DifficultyPolicy harder = AssemblySupport.DefaultPolicy.WithDifficulty("skill-55", 0.8, 0.6, 0.4, 0.3);

        Assembly assembly = new Assembler(harder, Live, bank: null, NobodyRecorded).Run([50, 51]);
        AssemblyWriter.Write(assembly, staging.Path);

        Assert.IsType<LevelDecision.Pinned>(assembly.Decisions[0]);
        LevelDecision.Unfilled unfilled = Assert.IsType<LevelDecision.Unfilled>(assembly.Decisions[1]);
        Assert.Equal(UnfilledCause.NoBank, unfilled.Cause);
        Assert.Equal(File.ReadAllBytes(Live.Find(51)!.Path), File.ReadAllBytes(staging.File(LevelFolder.FileNameOf(51))));
        string[] row = File.ReadAllLines(staging.File(AssemblyWriter.TableName))[2].Split(',');
        Assert.Equal(("51", "unfilled"), (row[0], row[2]));
        Assert.Contains("no longer meets the policy", row[^2]);
    }

    [Fact]
    public void A_changed_share_keeps_every_levels_board_and_reads_it_a_new_limit_and_pool_from_the_bank()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var staging = new TemporaryFolder();
        int[] numbers = Enumerable.Range(54, 6).ToArray();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, numbers);
        DifficultyPolicy easier = Lenient(AssemblySupport.DefaultPolicy.WithDifficulty("skill-55", 0.95, 0.85, 0.7, 0.6));

        Assembly assembly = new Assembler(easier, Live, bank.Access, NobodyRecorded).Run(numbers);
        AssemblyWriter.Write(assembly, staging.Path);

        var written = new LevelFolder(staging.Path);
        Assert.All(assembly.Decisions, decision =>
        {
            LevelDecision.Assembled made = Assert.IsType<LevelDecision.Assembled>(decision);
            FolderLevel before = Live.Find(made.Number)!;
            FolderLevel after = written.Find(made.Number)!;
            SlotDifficulty ask = made.Slot.Difficulty!;
            Assert.True(made.SameBoard);
            Assert.Equal(before.Content!.Id, after.Content!.Id);
            Assert.Equal(before.Level.Spawn.Palette, after.Level.Spawn.Palette);
            Assert.Equal(made.Finished.Limit, after.Level.MoveLimit);
            Assert.True(ask.Moves.Contains(made.Finished.CalibratedLimit));
            Assert.InRange(after.Level.Attempts.SeedPool.Count, 3, 6);
            Assert.Subset(made.Candidate.Seeds.ToHashSet(), after.Level.Attempts.SeedPool.ToHashSet());
            Assert.Equal(DecisionKey.ShareText(ask.Share), after.Row!.Cell("targetWin"));
            Assert.Equal("skill-55", after.Row.Cell("calibrationBot"));
            Assert.Equal(after.Level.MoveLimit, (int)after.Row.Number("moves")!.Value);
            Assert.Equal(after.Level.Attempts.SeedPool.Count, (int)after.Row.Number("poolSize")!.Value);
            Assert.Equal(("no", "0.0", "false"), (after.Row.Cell("relaxed"), after.Row.Cell("seconds"), after.Row.Cell("floorSetLimit")));
            Assert.Equal(made.Finished.CheckWin!.Value, after.Row.Number("win")!.Value, precision: 3);
            Assert.Equal(made.Finished.PoolSeeds.Count * AssemblySupport.QuickChecks.CheckGamesPerSeed, made.Finished.CheckGames);
            Assert.Equal(made.Key, after.DecisionKey);
            Assert.NotEqual(before.DecisionKey, after.DecisionKey);
        });
        Assert.Equal(written.Report.Columns, Live.Report.Columns);
        string[][] table = File.ReadAllLines(staging.File(AssemblyWriter.TableName)).Select(line => line.Split(',')).ToArray();
        Assert.All(table, row => Assert.Equal(table[0].Length, row.Length));
        Assert.All(table.Skip(1), row =>
        {
            Assert.Equal("same-board", row[Array.IndexOf(table[0], "decision")]);
            Assert.StartsWith("imported live level", row[Array.IndexOf(table[0], "origin")]);
            Assert.Matches(@"^\S+\.pzb@\d+$", row[Array.IndexOf(table[0], "record")]);
            Assert.Matches(@"^[01]\.\d{3}$", row[Array.IndexOf(table[0], "auditWin")]);
            Assert.Matches(@"^[01]\.\d{3}$", row[Array.IndexOf(table[0], "firstTryWin")]);
        });
    }

    [Fact]
    public void A_second_assembly_reads_its_games_from_the_bank_and_writes_the_same_bytes()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        int[] numbers = [54, 55, 56];
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, numbers);
        DifficultyPolicy easier = Lenient(AssemblySupport.DefaultPolicy.WithDifficulty("skill-55", 0.95, 0.85, 0.7, 0.6));

        AssemblyWriter.Write(new Assembler(easier, Live, bank.Access, NobodyRecorded).Run(numbers), first.Path);
        long playedFirst = bank.Games.GamesPlayed;
        AssemblyWriter.Write(new Assembler(easier, Live, bank.Access, NobodyRecorded).Run(numbers), second.Path);

        Assert.True(playedFirst > 0);
        Assert.Equal(playedFirst, bank.Games.GamesPlayed);
        Assert.All(Directory.GetFiles(first.Path), path => Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(second.File(Path.GetFileName(path)))));
    }

    [Fact]
    public void An_assembly_started_from_an_earlier_assembly_under_the_same_policy_keeps_every_level()
    {
        using var bankFolder = new TemporaryBankFolder();
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        int[] numbers = [54, 55, 56];
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, numbers);
        DifficultyPolicy easier = Lenient(AssemblySupport.DefaultPolicy.WithDifficulty("skill-55", 0.95, 0.85, 0.7, 0.6));
        AssemblyWriter.Write(new Assembler(easier, Live, bank.Access, NobodyRecorded).Run(numbers), first.Path);

        Assembly again = new Assembler(easier, new LevelFolder(first.Path), bank: null, NobodyRecorded).Run(numbers);
        AssemblyWriter.Write(again, second.Path);

        Assert.All(again.Decisions, decision => Assert.IsType<LevelDecision.Kept>(decision));
        Assert.All(numbers, number => Assert.Equal(File.ReadAllBytes(first.File(LevelFolder.FileNameOf(number))), File.ReadAllBytes(second.File(LevelFolder.FileNameOf(number)))));
        Assert.Equal(File.ReadAllBytes(first.File(GenerationReport.FileName)), File.ReadAllBytes(second.File(GenerationReport.FileName)));
    }

    [Fact]
    public void A_level_whose_board_the_bank_does_not_hold_gets_a_candidate_of_its_structure_under_its_own_number_and_colours()
    {
        using var bankFolder = new TemporaryBankFolder();
        (LevelSlot open, int donor) = TwoLevelsOfOneStructure();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [donor]);
        DifficultyPolicy easier = Lenient(AssemblySupport.DefaultPolicy.WithDifficulty("skill-55", 0.95, 0.85, 0.7, 0.6));

        Assembly assembly = new Assembler(easier, Live, bank.Access, NobodyRecorded).Run([open.Number]);

        LevelDecision.Assembled made = Assert.IsType<LevelDecision.Assembled>(Assert.Single(assembly.Decisions));
        Assert.False(made.SameBoard);
        Assert.Equal(Live.Find(donor)!.Content!.Id, made.Candidate.Content.Id);
        Assert.Equal((open.Number, open.Tier), (made.Level.Id.Value, made.Level.Difficulty));
        Assert.Equal(open.Plan.Palette, made.Level.Spawn.Palette);
        Assert.Equal(made.Candidate.Content.Id, CandidateContent.Of(made.Level).Id);
    }

    [Fact]
    public void A_board_whose_limit_left_the_move_range_stays_with_the_amount_of_its_goals_that_brings_the_limit_back()
    {
        using var bankFolder = new TemporaryBankFolder();
        FolderLevel collecting = Enumerable.Range(54, 60).Select(number => Live.Find(number)!).First(level => level.Content!.Features.Families.Count == 0);
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [collecting.Number]);
        var longer = new MoveRange(collecting.Level.MoveLimit + 6, collecting.Level.MoveLimit + 45);

        Assembly assembly = new Assembler(Lenient(AssemblySupport.DefaultPolicy, longer), Live, bank.Access, NobodyRecorded).Run([collecting.Number]);

        LevelDecision.Assembled made = Assert.IsType<LevelDecision.Assembled>(Assert.Single(assembly.Decisions));
        Assert.True(made.SameBoard);
        Assert.Equal(collecting.Content!.Board, made.Candidate.Content.Board);
        Assert.NotEqual(collecting.Content.Id, made.Candidate.Content.Id);
        Assert.True(made.Candidate.AmountStep > 0);
        Assert.True(made.Level.Goals.Sum(goal => goal.Target) > collecting.Level.Goals.Sum(goal => goal.Target));
        Assert.InRange(made.Finished.CalibratedLimit, longer.Min, longer.Max);
        Assert.Contains("another amount", made.Choice.Why);
        Assert.Equal(2, bank.Access.Catalog.Candidates.Count(candidate => candidate.Content.Board == collecting.Content.Board));
    }

    [Fact]
    public void Every_live_level_from_51_is_its_candidate_given_back_its_number_tier_limit_colours_and_pool()
    {
        using var folder = new TemporaryFolder();
        LevelFolder live = Live;

        Assert.All(Enumerable.Range(51, 150), number =>
        {
            FolderLevel level = live.Find(number)!;
            LevelDefinition back = level.Content!.ToLevel(level.Level.Id, level.Level.Difficulty, level.Level.MoveLimit, level.Level.Spawn.Palette, level.Level.Attempts.SeedPool);
            string path = folder.File(LevelFolder.FileNameOf(number));
            LevelJson.Save(back, path);
            Assert.Equal(File.ReadAllBytes(level.Path), File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void An_imported_level_is_measured_on_its_own_pool_seeds_first_and_is_not_measured_twice()
    {
        using var bankFolder = new TemporaryBankFolder();
        using OpenTestBank bank = AssemblySupport.BankOfLiveLevels(bankFolder, [54, 54]);
        FolderLevel level = Live.Find(54)!;

        BankCandidate candidate = Assert.Single(bank.Access.Catalog.Candidates);

        Assert.Equal(level.Content!.Id, candidate.Content.Id);
        Assert.Equal(level.Level.Attempts.SeedPool, candidate.Seeds.Take(level.Level.Attempts.SeedPool.Count));
        Assert.Equal(20, candidate.Seeds.Distinct().Count());
        Assert.IsType<CandidateOrigin.Imported>(candidate.Origin);
        Assert.Equal(3, candidate.Games.Count);
    }

    [Fact]
    public void Two_folders_compare_by_what_changed_for_a_player_limit_amounts_or_board()
    {
        using var first = new TemporaryFolder();
        using var second = new TemporaryFolder();
        LevelFolder live = Live;
        FolderLevel collecting = Enumerable.Range(54, 60).Select(number => live.Find(number)!).First(level => level.Content!.Features.CollectGoals > 0);
        int[] others = Enumerable.Range(54, 60).Where(number => number != collecting.Number).Take(4).ToArray();
        foreach (FolderLevel level in others.Select(number => live.Find(number)!).Append(collecting))
        {
            File.Copy(level.Path, first.File(LevelFolder.FileNameOf(level.Number)));
        }

        File.Copy(live.Find(others[0])!.Path, second.File(LevelFolder.FileNameOf(others[0])));
        LevelDefinition longer = live.Find(others[1])!.Level;
        LevelJson.Save(longer.WithMoveLimit(longer.MoveLimit + 2), second.File(LevelFolder.FileNameOf(others[1])));
        LevelDefinition donor = live.Find(others[3])!.Level;
        LevelJson.Save(new LevelDefinition(new LevelId(others[2]), donor.Difficulty, donor.Layout, donor.Goals, donor.MoveLimit, donor.Spawn, donor.Attempts), second.File(LevelFolder.FileNameOf(others[2])));
        CandidateContent moreToCollect = AmountGrid.AtStep(collecting.Content!, AmountGrid.BaseOf(collecting.Content!), step: 2);
        LevelDefinition scaled = moreToCollect.ToLevel(collecting.Level.Id, collecting.Level.Difficulty, collecting.Level.MoveLimit, collecting.Level.Spawn.Palette, collecting.Level.Attempts.SeedPool);
        LevelJson.Save(scaled, second.File(LevelFolder.FileNameOf(collecting.Number)));
        File.Copy(live.Find(200)!.Path, second.File(LevelFolder.FileNameOf(200)));

        LevelSetComparison comparison = LevelSetComparison.Of(new LevelFolder(first.Path), new LevelFolder(second.Path), others.Append(collecting.Number).Append(200));

        Assert.Equal(
            [(others[0], LevelChange.Identical), (others[1], LevelChange.SameCandidate), (others[2], LevelChange.OtherBoard), (others[3], LevelChange.OnlyInFirst), (collecting.Number, LevelChange.SameBoard), (200, LevelChange.OnlyInSecond)],
            comparison.Levels.Select(level => (level.Number, level.Change)));
        Assert.Equal((5, 5), (comparison.First.Levels, comparison.Second.Levels));
        Assert.Equal(longer.MoveLimit + 2, comparison.Levels[1].MovesSecond);
        Assert.Equal(comparison.First.Levels, comparison.First.Tiers.Sum(tier => tier.Levels));
    }

    /// <summary>A level to decide and another live level whose board has the first one's structure, so it can stand in for it.</summary>
    private static (LevelSlot Open, int Donor) TwoLevelsOfOneStructure()
    {
        LevelFolder live = Live;
        LevelSlot[] slots = Enumerable.Range(54, 60).Select(number => AssemblySupport.Slot(number)).ToArray();
        return (
            from open in slots
            from donor in slots
            where donor.Number != open.Number && open.Structure.Admits(live.Find(donor.Number)!.Content!.Features)
            select (open, donor.Number)).First();
    }
}
