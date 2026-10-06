using System.Text.Json.Nodes;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Generator;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>
/// The move margin (<c>calibration.moveMargin</c>): a level gets its calibrated move limit plus a share of it, and its
/// seed pool and reported win rates are measured at that final limit.
/// </summary>
public class MoveMarginTests
{
    public static TheoryData<decimal, int, int> FinalLimits => new()
    {
        { 0.25m, 20, 25 },
        { 0.25m, 16, 20 },
        { 0.25m, 21, 27 },   // 26.25
        { 0.25m, 17, 22 },   // 21.25
        { 0.25m, 1, 2 },     // 1.25
        { 0.1m, 50, 55 },    // exactly 55; binary 50 * 1.1 is 55.00000000000001 and would give 56
        { 0.12m, 25, 28 },   // exactly 28; binary 25 * 1.12 is 28.000000000000004 and would give 29
        { 0.1m, 21, 24 },    // 23.1
        { 1m, 36, 72 },
    };

    [Theory]
    [MemberData(nameof(FinalLimits))]
    public void A_margin_adds_its_share_of_the_calibrated_moves_rounded_up_to_a_whole_move(decimal share, int calibratedMoves, int finalMoves)
    {
        Assert.Equal(finalMoves, TestProfiles.Margin(share).Soften(calibratedMoves));
    }

    [Fact]
    public void No_margin_leaves_every_calibrated_limit_as_it_is()
    {
        Assert.True(MoveMargin.None.IsNone);
        Assert.Equal(MoveMargin.None, TestProfiles.Margin(0));
        Assert.False(TestProfiles.Margin(0.25m).IsNone);
        Assert.All(Enumerable.Range(1, 150), moves => Assert.Equal(moves, MoveMargin.None.Soften(moves)));
    }

    [Fact]
    public void A_margin_is_a_share_from_0_to_1()
    {
        Assert.True(MoveMargin.TryCreate(0m, out _));
        Assert.True(MoveMargin.TryCreate(1m, out MoveMargin whole));
        Assert.False(MoveMargin.TryCreate(-0.01m, out MoveMargin negative));
        Assert.False(MoveMargin.TryCreate(1.01m, out MoveMargin aboveOne));

        Assert.Equal(1m, whole.Share);
        Assert.Equal((MoveMargin.None, MoveMargin.None), (negative, aboveOne));
    }

    [Fact]
    public void A_margin_written_with_trailing_zeros_is_the_same_margin()
    {
        Assert.Equal(TestProfiles.Margin(0.25m), TestProfiles.Margin(0.250m));
        Assert.Equal("0.25", TestProfiles.Margin(0.25m).ToString());
    }

    [Fact]
    public void A_profile_without_a_move_margin_has_none()
    {
        Assert.Equal(MoveMargin.None, RepoFiles.ProfileBeforeOnboarding.Calibration.MoveMargin);
    }

    [Fact]
    public void A_profile_reads_its_move_margin_exactly_as_written()
    {
        GenerationProfile profile = LoadWithMoveMargin(JsonValue.Create(0.1m));

        Assert.Equal(0.1m, profile.Calibration.MoveMargin.Share);
        Assert.Equal(55, profile.Calibration.MoveMargin.Soften(50));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(25)]   // a percentage instead of a share
    public void A_move_margin_outside_0_to_1_is_a_profile_error(double written)
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadWithMoveMargin(JsonValue.Create(written)));

        Assert.Contains("calibration.moveMargin must be between 0 and 1", rejected.Message);
    }

    [Fact]
    public void A_move_margin_of_0_or_1_is_accepted()
    {
        Assert.True(LoadWithMoveMargin(JsonValue.Create(0)).Calibration.MoveMargin.IsNone);
        Assert.Equal(1m, LoadWithMoveMargin(JsonValue.Create(1)).Calibration.MoveMargin.Share);
    }

    [Fact]
    public void The_margin_raises_the_limit_calibration_found_and_changes_nothing_else_about_the_level()
    {
        // Level 9 is Hard: the average bot is to win 60 % at the calibrated limit.
        GeneratedLevel calibrated = Generate(TestProfiles.Tiny, 9);
        GeneratedLevel softened = Generate(TestProfiles.Tiny.WithMoveMargin(0.25m), 9);

        int calibratedMoves = calibrated.Metrics.MoveLimit;
        Assert.Equal((calibratedMoves, MoveMargin.None), (calibrated.Metrics.CalibratedMoves, calibrated.Metrics.MoveMargin));
        Assert.Equal((calibratedMoves, TestProfiles.Margin(0.25m)), (softened.Metrics.CalibratedMoves, softened.Metrics.MoveMargin));
        Assert.Equal(TestProfiles.Margin(0.25m).Soften(calibratedMoves), softened.Metrics.MoveLimit);
        Assert.Equal(softened.Metrics.MoveLimit, softened.Level.MoveLimit);
        Assert.Equal(
            (calibrated.Metrics.Style, calibrated.Metrics.Goals, calibrated.Metrics.MoveRange, calibrated.Metrics.TargetWinRate, calibrated.Metrics.CandidatesTried, calibrated.Metrics.Relaxed),
            (softened.Metrics.Style, softened.Metrics.Goals, softened.Metrics.MoveRange, softened.Metrics.TargetWinRate, softened.Metrics.CandidatesTried, softened.Metrics.Relaxed));
        Assert.Equal(LevelFileMapper.ToFile(calibrated.Level).cells, LevelFileMapper.ToFile(softened.Level).cells);
    }

    [Fact]
    public void Win_rates_are_reported_at_the_final_limit_against_the_target_of_the_calibrated_one()
    {
        // Level 13 is Normal: 85 % for the average bot and a 60 % floor for the casual bot at the calibrated limit. Every
        // win rate comes from the same games with and without the margin, so more moves can only raise it.
        GenerationProfile profile = TestProfiles.Tiny with { Calibration = TestProfiles.Tiny.Calibration with { ReportRuns = 40 } };
        LevelMetrics calibrated = Generate(profile, 13).Metrics;
        LevelMetrics softened = Generate(profile.WithMoveMargin(0.25m), 13).Metrics;

        WinRateCheck calibratedFloor = Assert.IsType<FloorCheck>(calibrated.Floor).WinRate;
        WinRateCheck softenedFloor = Assert.IsType<FloorCheck>(softened.Floor).WinRate;
        Assert.Equal((0.85, 0.85), (calibrated.TargetWinRate, softened.TargetWinRate));
        Assert.Equal((0.60, 0.60), (calibratedFloor.Required, softenedFloor.Required));
        Assert.True(softened.WinRate > calibrated.WinRate, $"win {softened.WinRate:P0} at {softened.MoveLimit} moves, {calibrated.WinRate:P0} at {calibrated.MoveLimit}");
        Assert.True(softenedFloor.Measured > calibratedFloor.Measured, $"floor win {softenedFloor.Measured:P0} softened, {calibratedFloor.Measured:P0} calibrated");
        Assert.True(softened.BotWinRates["casual"] > calibrated.BotWinRates["casual"], $"casual {softened.BotWinRates["casual"]:P0} softened, {calibrated.BotWinRates["casual"]:P0} calibrated");
        Assert.True(softened.BotWinRates["expert"] > calibrated.BotWinRates["expert"], $"expert {softened.BotWinRates["expert"]:P0} softened, {calibrated.BotWinRates["expert"]:P0} calibrated");
        Assert.InRange(Assert.NotNull(softened.WinRateWithExtraMoves), softened.WinRate, 1.0);
        // A near miss is a game lost at the limit it is read at, so the final limit leaves room for fewer of them than
        // the calibrated limit had.
        Assert.True(calibrated.NearMiss > 1 - softened.WinRate, $"{calibrated.NearMiss:P0} near misses at the calibrated limit would fit under the final one too; this level cannot tell where the rate is read");
        Assert.InRange(softened.NearMiss, 0, 1 - softened.WinRate);
    }

    [Fact]
    public void The_seed_pool_is_balanced_around_the_win_rate_at_the_final_limit()
    {
        // Level 10 is SuperHard: 45 % for the average bot at the calibrated limit, far more with a quarter more moves.
        // Each seed plays 20 games and joins the pool within 15 points of the level's verified win rate.
        GenerationProfile profile = TestProfiles.Tiny.WithMoveMargin(0.25m) with
        {
            SeedPool = new SeedPoolSettings { Candidates = 16, GamesPerSeed = 20, PoolSize = 4, TolerancePoints = 15 },
            MinPoolSize = 4,
        };
        const double Tolerance = 0.15;

        LevelMetrics softened = Generate(profile, 10).Metrics;

        Assert.True(softened.WinRate > softened.TargetWinRate + 2 * Tolerance, $"win {softened.WinRate:P0} at the final limit is too close to the {softened.TargetWinRate:P0} target to tell the two pools apart");
        Assert.True(softened.PoolMinWinRate >= softened.WinRate - Tolerance - 1e-9, $"pool {softened.PoolMinWinRate:P0}-{softened.PoolMaxWinRate:P0} around {softened.WinRate:P0}");
        Assert.True(softened.PoolMaxWinRate <= softened.WinRate + Tolerance + 1e-9, $"pool {softened.PoolMinWinRate:P0}-{softened.PoolMaxWinRate:P0} around {softened.WinRate:P0}");
    }

    [Fact]
    public void A_move_margin_that_takes_a_move_range_past_the_move_cap_is_a_profile_error()
    {
        // The longest range of the profile is SuperHard 28-45 from level 301; a quarter more is 57 moves.
        static GenerationProfile LoadWithMarginUnderCap(decimal margin, int moveCap) => TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json =>
        {
            json["calibration"]!["moveMargin"] = margin;
            json["calibration"]!["moveCap"] = moveCap;
        });

        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadWithMarginUnderCap(0.25m, moveCap: 56));
        GenerationProfile fitting = LoadWithMarginUnderCap(0.25m, moveCap: 57);
        GenerationProfile withoutMargin = LoadWithMarginUnderCap(0m, moveCap: 40);

        Assert.Contains("band fromLevel 301: calibration.moveMargin 0.25 takes the top of the SuperHard move range, 45 moves, to 57, past calibration.moveCap 56", rejected.Message);
        Assert.Equal((57, 57), (fitting.Calibration.MoveCap, fitting.Calibration.MoveMargin.Soften(45)));
        Assert.Equal(40, withoutMargin.Calibration.MoveCap);   // no margin, nothing asked: the profile loads as it did before
    }

    [Fact]
    public void A_final_limit_stops_at_the_move_cap_games_are_followed_to()
    {
        // The loader refuses a margin that takes a move range past the cap, so only a relaxed level, calibrated above
        // its range, can come this far. A profile built in memory shows the stop itself: level 7 is Normal and
        // calibrates to 18-28 moves; doubled, that passes a cap of 36 moves.
        GenerationProfile capped = TestProfiles.Tiny.WithoutFloors() with
        {
            Calibration = TestProfiles.Tiny.Calibration with { MoveCap = 36, MoveMargin = TestProfiles.Margin(1m) },
        };

        LevelMetrics cut = Generate(capped, 7).Metrics;
        LevelMetrics whole = Generate(TestProfiles.Tiny.WithoutFloors().WithMoveMargin(1m), 7).Metrics;

        Assert.InRange(cut.CalibratedMoves, 19, 28);
        Assert.Equal((36, true), (cut.MoveLimit, cut.MarginCutByMoveCap));
        Assert.Null(cut.WinRateWithExtraMoves);   // five more moves are past the cap, where no game was followed
        Assert.Equal((2 * whole.CalibratedMoves, false), (whole.MoveLimit, whole.MarginCutByMoveCap));
    }

    [Fact]
    public void A_band_names_its_own_move_margin_and_later_bands_inherit_it()
    {
        GenerationProfile profile = TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json =>
        {
            json["calibration"]!["moveMargin"] = 0.25m;
            BandFrom(json, 51)["moveMargin"] = 0;
        });
        var planner = new LevelPlanner(profile);

        Assert.Equal(TestProfiles.Margin(0.25m), planner.Plan(50).MoveMargin);   // before that band: the profile's margin
        Assert.All(new[] { 51, 121, 301, 1000 }, level => Assert.True(planner.Plan(level).MoveMargin.IsNone));
    }

    [Fact]
    public void A_band_move_margin_that_is_not_a_share_is_a_profile_error_naming_the_band()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() =>
            TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json => BandFrom(json, 51)["moveMargin"] = 1.5m));

        Assert.Contains("band fromLevel 51: moveMargin must be between 0 and 1", rejected.Message);
    }

    [Fact]
    public void A_level_is_softened_by_the_margin_of_its_own_band()
    {
        GenerationProfile profile = TestProfiles.Tiny.WithoutFloors().WithMoveMargin(0.25m) with
        {
            Bands = TestProfiles.Tiny.WithoutFloors().Bands.Select(band => band with { MoveMargin = MoveMargin.None }).ToArray(),
        };

        LevelMetrics metrics = Generate(profile, 9).Metrics;

        Assert.Equal((metrics.CalibratedMoves, MoveMargin.None), (metrics.MoveLimit, metrics.MoveMargin));
    }

    private static JsonNode BandFrom(JsonNode profile, int fromLevel) =>
        profile["bands"]!.AsArray().First(band => (int)band!["fromLevel"]! == fromLevel)!;

    private static GeneratedLevel Generate(GenerationProfile profile, int number) => new LevelGenerator(profile).Generate(number, []);

    /// <summary>The profile before onboarding with <paramref name="moveMargin"/> written into its calibration settings.</summary>
    private static GenerationProfile LoadWithMoveMargin(JsonNode moveMargin) =>
        TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json => json["calibration"]!["moveMargin"] = moveMargin);
}
