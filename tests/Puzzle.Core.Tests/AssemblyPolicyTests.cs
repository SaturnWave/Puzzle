using Puzzle.Bank.Assembling;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Growing;
using Puzzle.Core.Levels;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class AssemblyPolicyTests
{
    /// <summary>A policy of one band like the default's; a test replaces the part it is about.</summary>
    private const string OneBand =
        """{ "plan": "PLAN", "pinned": ["1-50"], "bands": [ { "fromLevel": 51, "player": "skill-55", "moveMargin": 0, "winShare": { "Easy": 0.9, "Normal": 0.75, "Hard": 0.55, "SuperHard": 0.4 } } ] }""";

    [Fact]
    public void The_default_policy_pins_the_first_room_and_gives_every_later_level_rung_55_at_the_approved_shares()
    {
        DifficultyPolicy policy = AssemblySupport.DefaultPolicy;

        Assert.All(Enumerable.Range(1, 50), level => Assert.True(policy.Pins(level)));
        Assert.False(policy.Pins(51));
        PolicyBand band = Assert.Single(policy.Bands);
        Assert.Equal((51, "skill-55", true), (band.FromLevel, band.Player.Label, band.MoveMargin.IsNone));
        Assert.Equal(new TierValues<double>(0.90, 0.75, 0.55, 0.40), band.WinShare);
        Assert.Null(band.Moves);
        Assert.Equal("default", policy.Plan.Name);
    }

    [Fact]
    public void A_level_takes_its_player_share_and_margin_from_the_policy_and_its_move_range_from_the_plan()
    {
        LevelSlot hard = AssemblySupport.Slot(55);

        Assert.Equal(DifficultyTier.Hard, hard.Tier);
        Assert.Equal(("skill-55", 0.55, new MoveRange(24, 36)), (hard.Difficulty!.Player.Label, hard.Difficulty.Share, hard.Difficulty.Moves));
        Assert.True(hard.Difficulty.Margin.IsNone);
        Assert.False(hard.IsPinned);
    }

    [Fact]
    public void A_policy_that_states_move_ranges_overrides_the_plans()
    {
        DifficultyPolicy policy = AssemblySupport.PolicyFrom(OneBand.Replace("\"moveMargin\": 0,", "\"moveMargin\": 0, \"moves\": { \"Easy\": [10, 20], \"Normal\": [12, 22], \"Hard\": [14, 24], \"SuperHard\": [16, 26] },", StringComparison.Ordinal));

        Assert.Equal(new MoveRange(14, 24), AssemblySupport.Slot(55, policy).Difficulty!.Moves);
    }

    [Fact]
    public void A_band_inherits_what_it_leaves_out_from_the_band_before_it()
    {
        const string SecondBand = """, { "fromLevel": 301, "player": "skill-45*0.5+skill-65*0.5", "winShare": { "Hard": 0.5 } } ] }""";
        DifficultyPolicy policy = AssemblySupport.PolicyFrom(OneBand.Replace(" ] }", SecondBand, StringComparison.Ordinal));

        PolicyBand later = policy.BandFor(400)!;
        Assert.Equal(2, policy.Bands.Count);
        Assert.Equal("skill-45*0.5+skill-65*0.5", later.Player.Label);
        Assert.Equal(new TierValues<double>(0.9, 0.75, 0.5, 0.4), later.WinShare);
        Assert.True(later.MoveMargin.IsNone);
        Assert.Equal("skill-55", policy.BandFor(300)!.Player.Label);
    }

    [Theory]
    [InlineData("\"pinned\"", "\"pined\"", "pined")]
    [InlineData("\"moveMargin\"", "\"moveMargn\"", "moveMargn")]
    [InlineData("\"Hard\"", "\"Hrad\"", "Hrad")]
    public void A_field_the_policy_format_does_not_have_is_refused_by_name(string field, string misspelt, string named)
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace(field, misspelt, StringComparison.Ordinal)));

        Assert.Contains(named, refused.Message);
    }

    [Theory]
    [InlineData("\"player\": \"skill-55\",", "", "player")]
    [InlineData("\"moveMargin\": 0,", "", "moveMargin")]
    [InlineData("\"Normal\": 0.75,", "", "winShare.Normal")]
    public void The_first_band_must_say_who_plays_what_that_player_wins_and_the_margin(string stated, string removed, string missing)
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace(stated, removed, StringComparison.Ordinal)));

        Assert.Contains(missing, refused.Message);
    }

    [Theory]
    [InlineData("\"skill-55\"", "\"skill-52\"", "skill-52")]
    [InlineData("\"skill-55\"", "\"usta\"", "usta")]
    [InlineData("\"Easy\": 0.9", "\"Easy\": 1.2", "winShare.Easy")]
    [InlineData("\"moveMargin\": 0", "\"moveMargin\": 3", "moveMargin 3")]
    [InlineData("[\"1-50\"]", "[\"50-1\"]", "50-1")]
    public void A_value_that_means_nothing_is_refused_with_the_field_it_stands_in(string stated, string wrong, string named)
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace(stated, wrong, StringComparison.Ordinal)));

        Assert.Contains(named, refused.Message);
    }

    [Fact]
    public void A_kind_that_is_no_kind_of_level_is_refused_with_the_kinds_that_exist()
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace("\"pinned\"", "\"variety\": { \"boxez\": 0.2 }, \"pinned\"", StringComparison.Ordinal)));

        Assert.Contains("boxez", refused.Message);
        Assert.Contains("CollectOnly, Boxes, Jelly, Chains, Items", refused.Message);
    }

    [Fact]
    public void A_margin_that_would_carry_a_limit_past_the_stored_games_is_refused()
    {
        string longRange = OneBand.Replace("\"moveMargin\": 0,", "\"moveMargin\": 0.5, \"moves\": { \"Easy\": [10, 20], \"Normal\": [12, 22], \"Hard\": [14, 24], \"SuperHard\": [90, 110] },", StringComparison.Ordinal);

        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(longRange));

        Assert.Contains("90-110", refused.Message);
        Assert.Contains("150", refused.Message);
    }

    [Fact]
    public void Audit_rounds_must_fit_the_repeats_no_other_game_uses()
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace("\"pinned\"", "\"checks\": { \"auditRounds\": 5 }, \"pinned\"", StringComparison.Ordinal)));

        Assert.Contains("checks.auditRounds", refused.Message);
    }

    [Fact]
    public void A_level_that_is_neither_pinned_nor_covered_by_a_band_is_refused()
    {
        DifficultyPolicy policy = AssemblySupport.PolicyFrom(OneBand.Replace("\"1-50\"", "\"1-40\"", StringComparison.Ordinal));

        var refused = Assert.Throws<PolicyFormatException>(() => LevelSlots.Of(policy, Enumerable.Range(38, 6), RecordedLevels.None));

        Assert.Contains("Level 41 ", refused.Message);
        Assert.Contains("51", refused.Message);
    }

    [Fact]
    public void A_level_people_have_recorded_attempts_on_keeps_its_board_is_frozen_or_is_free_as_the_policy_says()
    {
        DifficultyPolicy policy = AssemblySupport.DefaultPolicy;
        var recorded = new RecordedLevels(new Dictionary<int, IReadOnlyList<uint>> { [57] = [11u, 12u] }, onOtherVersions: []);

        IReadOnlyList<LevelSlot> keepingBoards = LevelSlots.Of(policy, [56, 57], recorded);
        IReadOnlyList<LevelSlot> freezing = LevelSlots.Of(policy with { Recorded = RecordedProtection.Freeze }, [56, 57], recorded);
        IReadOnlyList<LevelSlot> free = LevelSlots.Of(policy with { Recorded = RecordedProtection.None }, [56, 57], recorded);

        Assert.Equal(RecordedProtection.KeepBoard, policy.Recorded);
        Assert.Equal([(false, false), (false, true)], keepingBoards.Select(slot => (slot.IsPinned, slot.KeepsBoard)));
        Assert.Equal([11u, 12u], keepingBoards[1].RecordedSeeds);
        Assert.Equal([(false, false), (true, false)], freezing.Select(slot => (slot.IsPinned, slot.KeepsBoard)));
        Assert.Contains("recorded", freezing[1].PinnedBecause);
        Assert.Equal([(false, false), (false, false)], free.Select(slot => (slot.IsPinned, slot.KeepsBoard)));
    }

    [Fact]
    public void A_pinned_level_is_frozen_whatever_recordings_would_protect_of_it()
    {
        var recorded = new RecordedLevels(new Dictionary<int, IReadOnlyList<uint>> { [7] = [3u] }, onOtherVersions: []);

        LevelSlot slot = Assert.Single(LevelSlots.Of(AssemblySupport.DefaultPolicy, [7], recorded));

        Assert.True(slot.IsPinned);
        Assert.False(slot.KeepsBoard);
    }

    [Theory]
    [InlineData("\"moveMargin\": 0,", "\"moveMargin\": 0, \"moveMargin\": 0.5,", "moveMargin")]
    [InlineData("\"Easy\": 0.9,", "\"Easy\": 0.9, \"easy\": 0.5,", "easy")]
    [InlineData("\"pinned\": [\"1-50\"],", "\"pinned\": [\"1-50\"], \"pinned\": [\"1-40\"],", "pinned")]
    public void A_field_stated_twice_is_refused_instead_of_the_second_replacing_the_first(string stated, string twice, string named)
    {
        var refused = Assert.Throws<PolicyFormatException>(() => AssemblySupport.PolicyFrom(OneBand.Replace(stated, twice, StringComparison.Ordinal)));

        Assert.Contains($"'{named}' twice", refused.Message);
    }

    [Fact]
    public void A_kind_can_have_a_move_range_of_its_own_and_only_a_board_of_that_kind_is_asked_for_it()
    {
        DifficultyPolicy policy = AssemblySupport.PolicyFrom(OneBand.Replace("\"moveMargin\": 0,", "\"moveMargin\": 0, \"kindMoves\": { \"items\": { \"Normal\": [24, 42] } },", StringComparison.Ordinal));
        LevelSlot normal = AssemblySupport.Slot(57, policy);
        LevelSlot easy = Enumerable.Range(51, 30).Select(number => AssemblySupport.Slot(number, policy)).First(slot => slot.Tier == DifficultyTier.Easy);
        CandidateFeatures items = AssemblySupport.CandidateFor(normal, 1, families: ObstacleKind.Items).Features;
        CandidateFeatures boxes = AssemblySupport.CandidateFor(normal, 2, families: ObstacleKind.Boxes).Features;
        CandidateFeatures both = AssemblySupport.CandidateFor(normal, 3, families: [ObstacleKind.Boxes, ObstacleKind.Items]).Features;

        Assert.Equal(DifficultyTier.Normal, normal.Tier);
        Assert.Equal(new MoveRange(24, 42), normal.Difficulty!.For(items).Moves);
        Assert.Equal(new MoveRange(24, 42), normal.Difficulty.For(both).Moves);
        Assert.Equal(normal.Difficulty.Moves, normal.Difficulty.For(boxes).Moves);
        Assert.Equal(easy.Difficulty!.Moves, easy.Difficulty.For(items).Moves); // the policy names no range for items in Easy
        Assert.NotEqual(
            DecisionKey.Of(normal, normal.Difficulty.For(items), new CandidateId(1)),
            DecisionKey.Of(normal, normal.Difficulty, new CandidateId(1)));
    }

    [Fact]
    public void A_kinds_own_range_reaches_the_bank_a_class_asks_item_boards_for_it_and_records_their_draws_under_it()
    {
        DifficultyPolicy policy = AssemblySupport.PolicyFrom(OneBand.Replace("\"moveMargin\": 0,", "\"moveMargin\": 0, \"kindMoves\": { \"items\": { \"Normal\": [24, 42] } },", StringComparison.Ordinal));

        PlanClass normal = Demand.FromPolicy(policy, Enumerable.Range(51, 200), []).Classes.First(planClass => planClass.Tier == DifficultyTier.Normal);

        Assert.Equal(new MoveRange(24, 42), normal.MovesFor([ObstacleKind.Items]));
        Assert.Equal(normal.Moves, normal.MovesFor([ObstacleKind.Chains]));
        Assert.EndsWith("in 24-42", normal.LabelFor([ObstacleKind.Items]));
        Assert.Equal(normal.Label, normal.LabelFor([ObstacleKind.Chains]));
        Assert.NotEqual(PlanClass.AskOf(normal.Label), PlanClass.AskOf(normal.LabelFor([ObstacleKind.Items])));
    }

    [Fact]
    public void The_policys_kind_targets_steer_the_mix_the_bank_is_grown_toward()
    {
        StructureNeeds structure = AssemblySupport.Slot(300).Structure;
        var targets = new Dictionary<LevelKind, double> { [LevelKind.CollectOnly] = 0.5, [LevelKind.Boxes] = 0.3, [LevelKind.Chains] = 0.3, [LevelKind.Jelly] = 0.3, [LevelKind.Items] = 0.03 };
        IReadOnlyList<IReadOnlyList<ObstacleKind>> sets = KindMix.SetsFor(structure);

        Assert.Equal(1.0, sets.Sum(set => KindMix.TargetShare(structure, set, targets)), precision: 9);
        Assert.Equal(0.5, KindMix.TargetShare(structure, [], targets), precision: 9);
        Assert.Equal(10, KindMix.TargetShare(structure, [ObstacleKind.Boxes], targets) / KindMix.TargetShare(structure, [ObstacleKind.Items], targets), precision: 6);
        Assert.Equal(0.35, KindMix.TargetShare(structure, []), precision: 9); // without targets: the grower's own shares
        Assert.Equal(KindMix.TargetShare(structure, [ObstacleKind.Boxes]), KindMix.TargetShare(structure, [ObstacleKind.Items]), precision: 9);
    }

    [Fact]
    public void The_bank_grows_for_the_levels_the_policy_decides_and_for_its_player()
    {
        Demand demand = Demand.FromPolicy(AssemblySupport.DefaultPolicy, Enumerable.Range(41, 30), [BankSupport.Player("skill-45")]);

        Assert.Equal(Enumerable.Range(51, 20), demand.Classes.SelectMany(planClass => planClass.Levels).Order());
        Assert.Equal(["skill-55", "skill-45"], demand.Aims.Select(aim => aim.Label));
    }

    [Fact]
    public void A_decision_key_changes_with_every_input_of_the_decision_and_with_nothing_else()
    {
        DifficultyPolicy policy = AssemblySupport.DefaultPolicy;
        LevelSlot slot = AssemblySupport.Slot(55);
        var board = new Puzzle.Bank.Candidates.CandidateId(42);
        string key = DecisionKey.Of(slot, slot.Difficulty!, board);
        Assert.Equal(key, DecisionKey.Of(AssemblySupport.Slot(55, policy with { KindTolerance = 0.2, FailOnKindMiss = true, Recorded = RecordedProtection.None }), slot.Difficulty!, board));

        Assert.Equal(key, DecisionKey.Of(AssemblySupport.Slot(55, policy with { Name = "renamed", KindShares = new Dictionary<LevelKind, double>() }), slot.Difficulty!, board));
        Assert.NotEqual(key, DecisionKey.Of(slot, slot.Difficulty!, new Puzzle.Bank.Candidates.CandidateId(43)));
        Assert.NotEqual(key, DecisionKey.Of(slot, slot.Difficulty! with { Share = 0.6 }, board));
        Assert.NotEqual(key, DecisionKey.Of(slot, slot.Difficulty! with { Player = BankSupport.Player("skill-60") }, board));
        Assert.NotEqual(key, DecisionKey.Of(slot, slot.Difficulty! with { Moves = new MoveRange(24, 37) }, board));
        Assert.NotEqual(key, DecisionKey.Of(slot, slot.Difficulty! with { Margin = TestProfiles.Margin(0.25m) }, board));
    }

    [Fact]
    public void A_report_row_and_its_file_give_the_key_the_level_was_decided_under()
    {
        var live = new LevelFolder(RepoFiles.LiveLevelsFolder);
        FolderLevel level = live.Find(55)!;
        LevelSlot slot = AssemblySupport.Slot(55);

        Assert.Equal(DecisionKey.Of(slot, slot.Difficulty!, level.Content!.Id), level.DecisionKey);
        Assert.NotEqual(level.DecisionKey, live.Find(56)!.DecisionKey);
    }
}
