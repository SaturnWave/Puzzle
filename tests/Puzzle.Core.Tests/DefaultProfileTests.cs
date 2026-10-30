using Puzzle.Core.Levels;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>
/// The rules profiles/default.json ships with: the first museum room (levels 1-50) is set for weak players and gets
/// a quarter more moves than calibration found; from level 51 the average player (ladder rung 55, where the first
/// human tester sits) wins the approved share of first attempts at the limit itself.
/// </summary>
public class DefaultProfileTests
{
    private static readonly LevelPlanner Planner = new(RepoFiles.DefaultProfile);
    private static readonly LevelPlanner PlannerBeforeOnboarding = new(RepoFiles.ProfileBeforeOnboarding);

    private static IEnumerable<LevelPlan> Plans(int from, int to) => Enumerable.Range(from, to - from + 1).Select(Planner.Plan);

    [Fact]
    public void The_default_profile_sets_the_first_room_for_weak_players()
    {
        Assert.All(Plans(1, 3), plan => Assert.Equal("random", plan.CalibrationBot));
        Assert.All(Plans(4, 50), plan => Assert.Equal("novice", plan.CalibrationBot));
        Assert.All(new[] { 51, 301, 1000 }, level => Assert.Equal("skill-55", Planner.Plan(level).CalibrationBot));
    }

    [Fact]
    public void The_first_three_levels_are_set_for_the_random_bot()
    {
        Assert.All(Plans(1, 3), plan => Assert.Equal(
            (DifficultyTier.Easy, "random", 0.90, new MoveRange(14, 20)),
            (plan.Tier, plan.CalibrationBot, plan.TargetWinRate, plan.Moves)));
    }

    [Fact]
    public void Levels_4_to_20_are_easy_and_set_for_the_novice_bot()
    {
        Assert.All(Plans(4, 10), plan => Assert.Equal(
            (DifficultyTier.Easy, "novice", 0.95, new MoveRange(16, 24)),
            (plan.Tier, plan.CalibrationBot, plan.TargetWinRate, plan.Moves)));
        Assert.All(Plans(11, 20), plan => Assert.Equal(
            (DifficultyTier.Easy, "novice", 0.90, new MoveRange(16, 24)),
            (plan.Tier, plan.CalibrationBot, plan.TargetWinRate, plan.Moves)));
    }

    [Fact]
    public void The_first_two_paintings_are_easy_and_the_first_Hard_is_level_25()
    {
        Assert.All(Plans(1, 20), plan => Assert.Equal(DifficultyTier.Easy, plan.Tier));
        Assert.DoesNotContain(Plans(1, 24), plan => plan.Tier == DifficultyTier.Hard);
        Assert.DoesNotContain(Plans(1, 29), plan => plan.Tier == DifficultyTier.SuperHard);
        Assert.Equal((DifficultyTier.Hard, DifficultyTier.SuperHard), (Planner.Plan(25).Tier, Planner.Plan(30).Tier));
    }

    [Fact]
    public void The_saw_tooth_returns_at_21_with_gentle_targets()
    {
        static (DifficultyTier Tier, double Target, MoveRange Moves) Rules(int level) =>
            (Planner.Plan(level).Tier, Planner.Plan(level).TargetWinRate, Planner.Plan(level).Moves);

        Assert.Equal((DifficultyTier.Easy, 0.90, new MoveRange(16, 24)), Rules(21));
        Assert.Equal((DifficultyTier.Normal, 0.80, new MoveRange(18, 28)), Rules(22));
        Assert.Equal((DifficultyTier.Hard, 0.65, new MoveRange(20, 30)), Rules(25));
        Assert.Equal((DifficultyTier.SuperHard, 0.55, new MoveRange(22, 32)), Rules(30));
        Assert.Equal((DifficultyTier.Easy, 0.80, new MoveRange(16, 24)), Rules(31));
        Assert.Equal((DifficultyTier.Normal, 0.60, new MoveRange(18, 28)), Rules(32));
        Assert.Equal((DifficultyTier.Hard, 0.40, new MoveRange(22, 32)), Rules(35));
        Assert.Equal((DifficultyTier.SuperHard, 0.30, new MoveRange(24, 36)), Rules(40));
        Assert.Equal((DifficultyTier.SuperHard, 0.30, new MoveRange(24, 36)), Rules(50));
    }

    [Fact]
    public void No_level_of_the_first_room_has_a_casual_floor()
    {
        Assert.All(Plans(1, 50), plan => Assert.Null(plan.CasualFloor));
    }

    /// <summary>
    /// A guard of the fold of 2026-10-05, when the first room's rules moved into profiles/default.json: it ties the
    /// shipped profile to the frozen fixture. Delete it at the first deliberate change of what it compares (the first
    /// room's sizes, palettes or mechanic schedule); a failure after such a change does not mean anything broke.
    /// </summary>
    [Fact]
    public void The_first_room_keeps_the_boards_colours_and_mechanics_it_had_before_onboarding()
    {
        Assert.All(Plans(1, 50), plan =>
        {
            LevelPlan before = PlannerBeforeOnboarding.Plan(plan.Number);
            Assert.Equal((before.Seed, before.Size, before.PlainShape, before.MaxObstacleTypes), (plan.Seed, plan.Size, plan.PlainShape, plan.MaxObstacleTypes));
            AssertSameColoursAndMechanics(before, plan);
        });
    }

    /// <summary>
    /// The owner's decision of 2026-10-06: the average player is the first human tester, who sits on ladder rung 55,
    /// and wins 90 / 75 / 55 / 40 % of first attempts by tier from level 51 on. No casual floor guards a weaker player
    /// there, and the shares hold at the limit itself (no move margin).
    /// </summary>
    [Fact]
    public void From_level_51_the_average_player_wins_the_approved_share_of_first_attempts()
    {
        Assert.All(Plans(51, 1000), plan =>
        {
            double approved = plan.Tier switch
            {
                DifficultyTier.Easy => 0.90,
                DifficultyTier.Normal => 0.75,
                DifficultyTier.Hard => 0.55,
                _ => 0.40,
            };
            Assert.Equal(("skill-55", approved, MoveMargin.None), (plan.CalibrationBot, plan.TargetWinRate, plan.MoveMargin));
            Assert.Null(plan.CasualFloor);
        });
    }

    /// <summary>
    /// Retuning levels 51-1000 for the average player was to change who the limits are set for and nothing else: tiers,
    /// move ranges, boards, colours and mechanics are those of the frozen fixture. Delete it at the first deliberate
    /// change of one of these; a failure after that does not mean anything broke.
    /// </summary>
    [Fact]
    public void Levels_51_to_1000_keep_the_tiers_move_ranges_boards_colours_and_mechanics_they_had()
    {
        Assert.All(Plans(51, 1000), plan =>
        {
            LevelPlan before = PlannerBeforeOnboarding.Plan(plan.Number);
            Assert.Equal(
                (before.Tier, before.Moves, before.Size, before.PlainShape, before.MaxObstacleTypes, before.Seed),
                (plan.Tier, plan.Moves, plan.Size, plan.PlainShape, plan.MaxObstacleTypes, plan.Seed));
            AssertSameColoursAndMechanics(before, plan);
        });
    }

    /// <summary>
    /// A guard of the fold of 2026-10-05, when the first room's rules moved into profiles/default.json: delete it at
    /// the first deliberate change of a setting it compares (bots, cycle, mechanics, calibration, seed pool); a failure
    /// after that does not mean anything broke.
    /// </summary>
    [Fact]
    public void Onboarding_left_every_setting_outside_the_curve_alone()
    {
        GenerationProfile now = RepoFiles.DefaultProfile;
        GenerationProfile before = RepoFiles.ProfileBeforeOnboarding;

        Assert.Equal(
            (before.MasterSeed, before.FloorBot, before.HookLevels, before.NoveltyWindow, before.MinPoolSize, before.Palette, before.Sizes.Default, before.Sizes.FixedUntil, before.Sizes.PlainShapesUntil),
            (now.MasterSeed, now.FloorBot, now.HookLevels, now.NoveltyWindow, now.MinPoolSize, now.Palette, now.Sizes.Default, now.Sizes.FixedUntil, now.Sizes.PlainShapesUntil));
        Assert.Equal(before.ReportBots, now.ReportBots);
        Assert.Equal(before.Cycle, now.Cycle);
        Assert.Equal(before.Mechanics, now.Mechanics);
        Assert.Equal(before.Calibration, now.Calibration with { MoveMargin = MoveMargin.None });
        Assert.Equal(
            (before.SeedPool.Candidates, before.SeedPool.GamesPerSeed, before.SeedPool.PoolSize, before.SeedPool.TolerancePoints),
            (now.SeedPool.Candidates, now.SeedPool.GamesPerSeed, now.SeedPool.PoolSize, now.SeedPool.TolerancePoints));
    }

    [Fact]
    public void The_first_room_gets_a_quarter_more_moves_than_calibration_found()
    {
        Assert.All(Plans(1, 50), plan =>
        {
            Assert.Equal(0.25m, plan.MoveMargin.Share);
            Assert.Equal((20, 25, 45), (plan.MoveMargin.Soften(16), plan.MoveMargin.Soften(20), plan.MoveMargin.Soften(36)));
        });
    }

    private static void AssertSameColoursAndMechanics(LevelPlan expected, LevelPlan actual)
    {
        Assert.Equal(expected.Palette, actual.Palette);
        Assert.True(expected.Unlocked.SetEquals(actual.Unlocked), $"level {actual.Number}: unlocked mechanics differ");
        Assert.True(expected.Featured.SetEquals(actual.Featured), $"level {actual.Number}: featured mechanics differ");
    }
}
