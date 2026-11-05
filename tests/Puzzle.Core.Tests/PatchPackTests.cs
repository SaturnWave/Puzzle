using System.IO;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Recording;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// Content versions and patch packs (docs/GAME_DESIGN.md §20): how a released game, which carries its levels with it,
/// is told that some of them changed, and how an attempt record says which version of a level was played.
/// </summary>
public class PatchPackTests
{
    /// <summary>Level <paramref name="number"/> on an open board; levels of one number differ by their move limit here.</summary>
    private static LevelDefinition Numbered(int number, int moves, int contentVersion = ContentVersion.FirstValue) => new(
        new LevelId(number),
        DifficultyTier.Normal,
        LevelLayout.Parse(new LayoutLayers { Cells = Blank(8, 8, '#') }),
        new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 30) },
        moves,
        new SpawnRules { Palette = GemColors.First(5) },
        new AttemptPolicy { SeedPool = new uint[] { 11, 22, 33 } },
        new ContentVersion(contentVersion));

    private static PatchPack Pack(int sequence, params LevelDefinition[] corrections) => new(sequence, corrections);

    private static int[] Numbers(IEnumerable<LevelId> levels) => levels.Select(level => level.Value).ToArray();

    private static (int Number, int Moves, int Version)[] Summary(IEnumerable<LevelDefinition> levels) =>
        levels.Select(level => (level.Id.Value, level.MoveLimit, level.ContentVersion.Value)).ToArray();

    [Fact]
    public void A_level_is_at_its_first_content_version_unless_its_file_says_otherwise()
    {
        LevelFile file = LevelFileMapper.ToFile(GameTests.OpenLevel());
        Assert.Equal(ContentVersion.First, LevelFileMapper.ToDefinition(file).ContentVersion);
        Assert.Equal(default, ContentVersion.First);

        file.contentVersion = 3;
        LevelDefinition third = LevelFileMapper.ToDefinition(file);
        Assert.Equal(new ContentVersion(3), third.ContentVersion);
        Assert.Equal(3, LevelFileMapper.ToFile(third).contentVersion);
        Assert.Equal(new ContentVersion(3), third.WithMoveLimit(12).WithAttempts(AttemptPolicy.Default).ContentVersion);

        file.id = 7;
        file.contentVersion = 0;
        var error = Assert.Throws<LevelFormatException>(() => LevelFileMapper.ToDefinition(file));
        Assert.Contains("Level 7", error.Message);
    }

    [Fact]
    public void Content_versions_of_a_level_are_ordered_and_the_next_one_is_one_higher()
    {
        var second = new ContentVersion(2);

        Assert.True(second > ContentVersion.First);
        Assert.True(ContentVersion.First < second);
        Assert.Equal(second, ContentVersion.First.Next());
        Assert.True(ContentVersion.First.IsFirst);
        Assert.False(second.IsFirst);
        Assert.Equal("2", second.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContentVersion(0));
    }

    [Fact]
    public void The_first_content_version_stays_out_of_the_file_so_an_untouched_level_keeps_its_file_and_fingerprint()
    {
        LevelDefinition level = Numbered(12, moves: 25);
        LevelDefinition patched = level.WithContentVersion(new ContentVersion(2));

        string untouched = LevelJson.ToCanonicalJson(level);
        string corrected = LevelJson.ToCanonicalJson(patched);

        Assert.DoesNotContain("contentVersion", untouched);
        Assert.Contains("\"contentVersion\":2", corrected);
        Assert.Equal(untouched, corrected.Replace("\"contentVersion\":2,", string.Empty));
        Assert.NotEqual(LevelJson.Fingerprint(level), LevelJson.Fingerprint(patched));
        Assert.Equal(new ContentVersion(2), LevelJson.Parse(corrected, "the corrected level").ContentVersion);
        Assert.Equal(ContentVersion.First, LevelJson.Parse(untouched, "the untouched level").ContentVersion);
        Assert.True(LevelJson.HaveSameContent(level, patched));
        Assert.False(LevelJson.HaveSameContent(level, level.WithMoveLimit(26)));
    }

    [Fact]
    public void The_level_files_of_the_game_name_no_content_version()
    {
        // The working levels are never stamped: a version in one of these files would change its fingerprint and
        // orphan the recordings made on it. Versions live in a release's folder (patch apply) and in packs.
        string[] files = Directory.GetFiles(RepoFiles.LiveLevelsFolder, "level_*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.DoesNotContain("contentVersion", File.ReadAllText(file)));
    }

    [Fact]
    public void A_pack_replaces_only_the_levels_it_has_a_later_version_of()
    {
        LevelDefinition[] shipped = { Numbered(1, moves: 20), Numbered(2, moves: 21), Numbered(3, moves: 22, contentVersion: 3) };
        PatchPack pack = Pack(
            1,
            Numbered(9, moves: 40, contentVersion: 2),
            Numbered(3, moves: 99, contentVersion: 2),
            Numbered(1, moves: 24, contentVersion: 2));

        PatchedLevels patched = pack.ApplyTo(shipped);

        Assert.Equal(new[] { (1, 24, 2), (2, 21, 1), (3, 22, 3) }, Summary(patched.Levels));
        Assert.Equal(new[] { 1 }, Numbers(patched.Replaced));
        Assert.Equal(new[] { 3 }, Numbers(patched.AlreadyCurrent));
        Assert.Equal(new[] { 9 }, Numbers(patched.Unknown));
        Assert.Same(shipped[1], patched.Levels[1]);
    }

    [Fact]
    public void A_device_without_a_pack_plays_the_levels_its_build_carries_and_takes_any_pack_over_none()
    {
        LevelDefinition[] shipped = { Numbered(1, moves: 20), Numbered(2, moves: 21) };

        PatchedLevels patched = PatchPack.None.ApplyTo(shipped);

        Assert.Equal(shipped, patched.Levels);
        Assert.Empty(patched.Replaced);
        Assert.Equal(0, PatchPack.None.Sequence);
        Assert.True(Pack(1).IsNewerThan(PatchPack.None));
    }

    [Fact]
    public void A_device_keeps_the_pack_with_the_highest_number_so_a_stale_one_takes_nothing_back()
    {
        PatchPack third = Pack(3, Numbered(1, moves: 30, contentVersion: 3));
        PatchPack first = Pack(1, Numbered(1, moves: 24, contentVersion: 2));

        Assert.True(third.IsNewerThan(first));
        Assert.False(first.IsNewerThan(third));
        Assert.False(third.IsNewerThan(third));
    }

    [Fact]
    public void A_pack_corrects_a_level_once_never_at_its_first_version_and_has_a_number()
    {
        var twice = Assert.Throws<LevelFormatException>(() => Pack(1, Numbered(4, moves: 20, contentVersion: 2), Numbered(4, moves: 21, contentVersion: 3)));
        Assert.Contains("level 4", twice.Message);

        var first = Assert.Throws<LevelFormatException>(() => Pack(1, Numbered(5, moves: 20)));
        Assert.Contains("level 5", first.Message);

        Assert.Contains("sequence 0", Assert.Throws<LevelFormatException>(() => Pack(0)).Message);
    }

    [Fact]
    public void A_pack_survives_its_file()
    {
        PatchPack pack = Pack(4, Numbered(7, moves: 31, contentVersion: 4), Numbered(2, moves: 18, contentVersion: 2));
        using var folder = new TempFolder();
        string path = Path.Combine(folder.Path, "pack.json");

        LevelJson.SavePatchPack(pack, path);
        PatchPack read = LevelJson.LoadPatchPack(path);

        Assert.Equal(4, read.Sequence);
        Assert.Equal(new[] { (2, 18, 2), (7, 31, 4) }, Summary(read.Levels));
        Assert.Equal(new uint[] { 11, 22, 33 }, read.CorrectionOf(new LevelId(7))!.Attempts.SeedPool);
        Assert.Null(read.CorrectionOf(new LevelId(3)));
    }

    [Theory]
    [InlineData("{}", "sequence 0")]
    [InlineData("{\"formatVersion\":1,\"sequence\":1,\"levels\":null}", "list of levels")]
    [InlineData("{\"formatVersion\":1,\"sequence\":1,\"levels\":[null]}", "list of levels")]
    [InlineData("{\"formatVersion\":2,\"sequence\":1,\"levels\":[]}", "format version 2")]
    [InlineData("{\"formatVersion\":1,\"sequence\":1,\"levels\":[", "not valid patch pack JSON")]
    public void A_file_that_is_not_a_pack_is_refused_as_one(string json, string reason)
    {
        using var folder = new TempFolder();
        string path = Path.Combine(folder.Path, "pack.json");
        File.WriteAllText(path, json);

        Assert.Contains(reason, Assert.Throws<LevelFormatException>(() => LevelJson.LoadPatchPack(path)).Message);
    }

    [Fact]
    public void A_level_file_or_a_missing_file_is_no_pack()
    {
        using var folder = new TempFolder();
        string levelPath = Path.Combine(folder.Path, "level_0001.json");
        LevelJson.Save(Numbered(1, moves: 20), levelPath);
        string missing = Path.Combine(folder.Path, "nothing.json");

        Assert.Throws<LevelFormatException>(() => LevelJson.LoadPatchPack(levelPath));
        Assert.Contains(missing, Assert.Throws<LevelFormatException>(() => LevelJson.LoadPatchPack(missing)).Message);
    }

    [Fact]
    public void The_first_pack_of_a_release_holds_every_changed_level_whole_at_its_second_version()
    {
        LevelDefinition[] release = { Numbered(1, moves: 20), Numbered(2, moves: 21), Numbered(3, moves: 22) };
        LevelDefinition[] current = { Numbered(1, moves: 20), Numbered(2, moves: 25), Numbered(3, moves: 22), Numbered(4, moves: 30) };

        PatchPackDraft draft = PatchPackBuilder.Build(release, current, PatchPack.None);

        Assert.Equal(1, draft.Pack.Sequence);
        Assert.Equal(new[] { (2, 25, 2) }, Summary(draft.Pack.Levels));
        Assert.Equal(new[] { 2 }, Numbers(draft.Changed));
        Assert.Empty(draft.Kept);
        Assert.Empty(draft.MissingNow);
        Assert.Equal(new[] { 4 }, Numbers(draft.NotPublished));
        Assert.Equal(new[] { (1, 20, 1), (2, 25, 2), (3, 22, 1) }, Summary(draft.Pack.ApplyTo(release).Levels));
    }

    [Fact]
    public void A_later_pack_has_the_next_number_keeps_standing_corrections_and_raises_a_level_changed_again()
    {
        LevelDefinition[] release = { Numbered(1, moves: 20), Numbered(2, moves: 21), Numbered(3, moves: 22) };
        PatchPack previous = Pack(1, Numbered(2, moves: 25, contentVersion: 2), Numbered(3, moves: 28, contentVersion: 2));
        LevelDefinition[] current = { Numbered(1, moves: 19), Numbered(2, moves: 25), Numbered(3, moves: 30) };

        PatchPackDraft draft = PatchPackBuilder.Build(release, current, previous);

        Assert.Equal(2, draft.Pack.Sequence);
        Assert.Equal(new[] { (1, 19, 2), (2, 25, 2), (3, 30, 3) }, Summary(draft.Pack.Levels));
        Assert.Equal(new[] { 1, 3 }, Numbers(draft.Changed));
        Assert.Equal(new[] { 2 }, Numbers(draft.Kept));
    }

    [Fact]
    public void A_level_taken_back_to_what_the_release_shipped_still_gets_a_new_version()
    {
        // Players have version 2 now. Dropping the correction would make a later change of the level version 2
        // again: one number for two contents.
        LevelDefinition[] release = { Numbered(1, moves: 20) };
        PatchPack previous = Pack(1, Numbered(1, moves: 25, contentVersion: 2));
        LevelDefinition[] current = { Numbered(1, moves: 20) };

        PatchPackDraft draft = PatchPackBuilder.Build(release, current, previous);

        Assert.Equal(new[] { (1, 20, 3) }, Summary(draft.Pack.Levels));
        Assert.Equal(new[] { 1 }, Numbers(draft.Changed));
    }

    [Fact]
    public void A_pack_made_against_a_later_release_still_serves_the_builds_before_it()
    {
        // The first release shipped level 1 with 20 moves; pack 1 made it 25. The second release carries that.
        LevelDefinition[] firstRelease = { Numbered(1, moves: 20), Numbered(2, moves: 21) };
        PatchPack packOne = Pack(1, Numbered(1, moves: 25, contentVersion: 2));
        IReadOnlyList<LevelDefinition> secondRelease = packOne.ApplyTo(firstRelease).Levels;
        LevelDefinition[] current = { Numbered(1, moves: 25), Numbered(2, moves: 23) };

        PatchPackDraft withThePack = PatchPackBuilder.Build(secondRelease, current, packOne);
        PatchPackDraft packForgotten = PatchPackBuilder.Build(secondRelease, current, PatchPack.None);

        Assert.Equal(new[] { (1, 25, 2), (2, 23, 2) }, Summary(withThePack.Pack.Levels));
        Assert.Equal(new[] { 1 }, Numbers(withThePack.Kept));
        Assert.Equal(new[] { (1, 25, 2), (2, 23, 2) }, Summary(withThePack.Pack.ApplyTo(firstRelease).Levels));
        Assert.Equal(new[] { 2 }, Numbers(withThePack.Pack.ApplyTo(secondRelease).Replaced));

        // The release folder carries its versions, so even without the pack no version is handed out twice.
        Assert.Equal(Summary(withThePack.Pack.Levels), Summary(packForgotten.Pack.Levels));
    }

    [Fact]
    public void A_correction_of_a_level_the_release_folder_lacks_is_carried_on_and_a_level_missing_now_stays_as_published()
    {
        LevelDefinition[] release = { Numbered(1, moves: 20), Numbered(2, moves: 21) };
        PatchPack previous = Pack(5, Numbered(2, moves: 23, contentVersion: 2), Numbered(8, moves: 40, contentVersion: 4));
        LevelDefinition[] current = { Numbered(1, moves: 20), Numbered(8, moves: 41) };

        PatchPackDraft draft = PatchPackBuilder.Build(release, current, previous);

        Assert.Equal(6, draft.Pack.Sequence);
        Assert.Equal(new[] { (2, 23, 2), (8, 41, 5) }, Summary(draft.Pack.Levels));
        Assert.Equal(new[] { 8 }, Numbers(draft.Changed));
        Assert.Equal(new[] { 2 }, Numbers(draft.Kept));
        Assert.Equal(new[] { 2 }, Numbers(draft.MissingNow));
        Assert.Empty(draft.NotPublished);
    }

    [Fact]
    public void One_content_version_of_a_level_with_two_contents_stops_the_pack()
    {
        LevelDefinition[] release = { Numbered(1, moves: 25, contentVersion: 2) };
        PatchPack previous = Pack(1, Numbered(1, moves: 27, contentVersion: 2));

        var error = Assert.Throws<LevelFormatException>(() => PatchPackBuilder.Build(release, release, previous));

        Assert.Contains("Level 1", error.Message);
        Assert.Contains("content version 2", error.Message);
    }

    [Fact]
    public void A_record_names_the_content_version_played_and_leaves_the_first_one_out()
    {
        LevelDefinition level = Numbered(12, moves: 25);
        AttemptRecord onFirst = TestRecords.Of(level, AttemptResult.Won, movesUsed: 20);
        AttemptRecord onThird = TestRecords.Of(level.WithContentVersion(new ContentVersion(3)), AttemptResult.Won, movesUsed: 20);

        string firstLine = AttemptRecordJson.Serialize(onFirst);
        string thirdLine = AttemptRecordJson.Serialize(onThird);

        Assert.DoesNotContain("contentVersion", firstLine);
        Assert.Contains("\"level\":12,\"contentVersion\":3,", thirdLine);
        Assert.Equal(ContentVersion.First, AttemptRecordJson.Parse(firstLine).Start.ContentVersion);
        Assert.Equal(new ContentVersion(3), AttemptRecordJson.Parse(thirdLine).Start.ContentVersion);
    }

    [Fact]
    public void Records_of_the_released_game_are_told_apart_by_the_content_version_they_were_played_on()
    {
        // The released game's records carry no level fingerprint: before and after a patch they differ by version only.
        LevelDefinition shipped = Numbered(12, moves: 25);
        LevelDefinition patched = shipped.WithContentVersion(new ContentVersion(2));
        AttemptRecord[] records =
        {
            TestRecords.Of(shipped, AttemptResult.OutOfMoves, movesUsed: 25, lab: null),
            TestRecords.Of(shipped, AttemptResult.OutOfMoves, movesUsed: 25, lab: null),
            TestRecords.Of(patched, AttemptResult.Won, movesUsed: 20, lab: null),
        };

        AttemptSummary summary = AttemptSummary.Of(records);
        HumanBotReport againstThePatchedLevel = HumanBotComparison.Compare(records, new[] { patched }, botRuns: 1);

        Assert.Equal(
            new[] { (1, 2), (2, 0) },
            summary.Levels.OrderBy(level => level.ContentVersion.Value).Select(level => (level.ContentVersion.Value, level.Play.Finished - level.Play.Wins)).ToArray());
        Assert.Equal(new RecordTally(Compared: 1, Assisted: 0, ChosenSeeds: 0, UnknownLevel: 2, TesterMode: 0), againstThePatchedLevel.Records);
    }
}
