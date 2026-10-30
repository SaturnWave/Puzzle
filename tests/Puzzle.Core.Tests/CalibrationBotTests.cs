using System.Text.Json.Nodes;
using Puzzle.Bot.Simulation;
using Puzzle.Generator;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>A band names the player model its levels are calibrated with (<c>bands[].calibrationBot</c>).</summary>
public class CalibrationBotTests
{
    // In the profile before onboarding the bands start at levels 1, 6, 31, 51, 121, 201, 301 and 601.
    private const int BandAt6 = 1;
    private const int BandAt51 = 3;

    [Fact]
    public void A_band_names_its_calibration_bot_and_later_bands_inherit_it()
    {
        GenerationProfile profile = LoadWithoutFloors(json =>
        {
            json["bands"]![BandAt6]!["calibrationBot"] = "novice";
            json["bands"]![BandAt51]!["calibrationBot"] = "expert";
        });
        var planner = new LevelPlanner(profile);

        Assert.Equal(
            new[] { "average", "novice", "novice", "expert", "expert", "expert", "expert", "expert" },
            profile.Bands.Select(band => band.CalibrationBot));
        Assert.Equal(
            ("average", "novice", "novice", "expert", "expert"),
            (planner.Plan(5).CalibrationBot, planner.Plan(6).CalibrationBot, planner.Plan(50).CalibrationBot, planner.Plan(51).CalibrationBot, planner.Plan(1000).CalibrationBot));
    }

    [Fact]
    public void The_top_level_calibration_bot_is_the_default_of_the_first_band()
    {
        GenerationProfile named = LoadWithoutFloors(json => json["calibrationBot"] = "novice");
        GenerationProfile unnamed = LoadWithoutFloors(json => json.Remove("calibrationBot"));
        GenerationProfile overridden = LoadWithoutFloors(json =>
        {
            json["calibrationBot"] = "novice";
            json["bands"]![0]!["calibrationBot"] = "random";
        });

        Assert.All(named.Bands, band => Assert.Equal("novice", band.CalibrationBot));
        Assert.All(unnamed.Bands, band => Assert.Equal("average", band.CalibrationBot));
        Assert.All(overridden.Bands, band => Assert.Equal("random", band.CalibrationBot));
    }

    [Fact]
    public void A_band_calibration_bot_must_be_a_player_model()
    {
        ProfileFormatException measurement = Assert.Throws<ProfileFormatException>(() => LoadWithoutFloors(json => json["bands"]![BandAt6]!["calibrationBot"] = "usta"));
        ProfileFormatException measurementBudget = Assert.Throws<ProfileFormatException>(() => LoadWithoutFloors(json => json["bands"]![BandAt51]!["calibrationBot"] = "usta-48"));
        ProfileFormatException unknown = Assert.Throws<ProfileFormatException>(() => LoadWithoutFloors(json => json["bands"]![BandAt6]!["calibrationBot"] = "beginner"));

        Assert.Contains("band fromLevel 6: calibrationBot 'usta' is a measurement bot; move limits are set against a player model", measurement.Message);
        Assert.Contains("band fromLevel 51: calibrationBot 'usta-48' is a measurement bot", measurementBudget.Message);
        Assert.Contains("band fromLevel 6: unknown bot 'beginner'", unknown.Message);
    }

    [Fact]
    public void A_misspelled_top_level_calibration_bot_is_rejected_even_when_the_first_band_names_its_own()
    {
        static void NameTheFirstBandsBot(JsonObject json) => json["bands"]![0]!["calibrationBot"] = "random";

        ProfileFormatException misspelled = Assert.Throws<ProfileFormatException>(() => LoadWithoutFloors(json =>
        {
            json["calibrationBot"] = "avarage";
            NameTheFirstBandsBot(json);
        }));
        ProfileFormatException measurement = Assert.Throws<ProfileFormatException>(() => LoadWithoutFloors(json =>
        {
            json["calibrationBot"] = "usta";
            NameTheFirstBandsBot(json);
        }));

        Assert.StartsWith("unknown bot 'avarage'", misspelled.Message);                        // no band to blame: the field of the profile itself
        Assert.StartsWith("calibrationBot 'usta' is a measurement bot", measurement.Message);
    }

    [Fact]
    public void A_band_with_a_floor_needs_a_floor_bot_weaker_than_its_own_calibration_bot()
    {
        // Every band of the profile has casual floors, checked with the casual bot.
        static GenerationProfile LoadWithBandAt6CalibratedBy(string bot) =>
            TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json => json["bands"]![BandAt6]!["calibrationBot"] = bot);

        ProfileFormatException weakerBand = Assert.Throws<ProfileFormatException>(() => LoadWithBandAt6CalibratedBy("novice"));
        ProfileFormatException equalBand = Assert.Throws<ProfileFormatException>(() => LoadWithBandAt6CalibratedBy("casual"));
        GenerationProfile strongerBand = LoadWithBandAt6CalibratedBy("expert");

        Assert.Contains("band fromLevel 6: floorBot 'casual' is not weaker than calibrationBot 'novice'", weakerBand.Message);
        Assert.Contains("band fromLevel 6: floorBot 'casual' is not weaker than calibrationBot 'casual'", equalBand.Message);
        Assert.Equal("expert", strongerBand.BandFor(6).CalibrationBot);
    }

    [Fact]
    public void A_band_without_a_floor_may_be_calibrated_by_a_bot_no_stronger_than_the_floor_bot()
    {
        GenerationProfile profile = TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json =>
        {
            JsonArray bands = json["bands"]!.AsArray();
            bands[0]!.AsObject().Remove("casualFloor");   // floors start at level 51, where the average bot takes over
            bands[0]!["calibrationBot"] = "novice";
            bands[BandAt51]!["calibrationBot"] = "average";
        });

        Assert.Equal(("casual", "novice", "average"), (profile.FloorBot, profile.BandFor(50).CalibrationBot, profile.BandFor(51).CalibrationBot));
    }

    /// <summary>
    /// One set of board rules, two bands, two bots: the random bot calibrates the levels up to 30 and the expert bot the
    /// ones from 31. Levels 10 and 40 are both SuperHard with a 45 % target and no floor. Each limit is read from 80
    /// games, so a single game moves a win rate by little more than a point.
    /// </summary>
    private static GenerationProfile RandomThenExpert => TestProfiles.Tiny.WithoutFloors().CalibratedBy("random").CalibratedBy("expert", fromLevel: 31) with
    {
        Calibration = TestProfiles.Tiny.Calibration with { Runs = 80, VerifyRuns = 80, ReportRuns = 30 },
        ReportBots = ["random", "expert"],
    };

    [Fact]
    public void The_move_limit_is_read_from_the_plans_calibration_bot()
    {
        var generator = new LevelGenerator(RandomThenExpert);
        double tolerance = RandomThenExpert.Calibration.WinRateTolerance;

        LevelMetrics setByRandom = generator.Generate(10, []).Metrics;
        LevelMetrics setByExpert = generator.Generate(40, []).Metrics;

        Assert.Equal(("random", "expert"), (setByRandom.CalibrationBot, setByExpert.CalibrationBot));
        Assert.Equal((0.45, 0.45), (setByRandom.TargetWinRate, setByExpert.TargetWinRate));
        Assert.InRange(setByRandom.WinRate, 0.45 - tolerance, 0.45 + tolerance);
        Assert.InRange(setByExpert.WinRate, 0.45 - tolerance, 0.45 + tolerance);
        // Each limit sits on the target for its band's own bot only: the expert sails through the random bot's limit and
        // the random bot fails at the expert's.
        Assert.True(setByRandom.BotWinRates["expert"] > 0.90, $"the expert bot won {setByRandom.BotWinRates["expert"]:P0} at the random bot's limit");
        Assert.True(setByExpert.BotWinRates["random"] < 0.10, $"the random bot won {setByExpert.BotWinRates["random"]:P0} at the expert bot's limit");
    }

    [Fact]
    public void The_seed_pool_is_built_with_the_plans_calibration_bot()
    {
        // Each seed plays 20 games and joins the pool within 15 points of the level's verified win rate.
        GenerationProfile profile = RandomThenExpert with
        {
            SeedPool = new SeedPoolSettings { Candidates = 12, GamesPerSeed = 20, PoolSize = 4, TolerancePoints = 15 },
            MinPoolSize = 4,
        };
        const double Tolerance = 0.15;

        LevelMetrics setByRandom = new LevelGenerator(profile).Generate(10, []).Metrics;

        // A stronger bot wins nearly every game at the random bot's limit, on every seed: a pool measured with one
        // would sit near 100 %, far above the random bot's 45 %.
        Assert.Equal("random", setByRandom.CalibrationBot);
        Assert.InRange(setByRandom.WinRate, 0.38, 0.52);
        Assert.True(setByRandom.PoolMinWinRate >= setByRandom.WinRate - Tolerance - 1e-9, $"pool {setByRandom.PoolMinWinRate:P0}-{setByRandom.PoolMaxWinRate:P0} around {setByRandom.WinRate:P0}");
        Assert.True(setByRandom.PoolMaxWinRate <= setByRandom.WinRate + Tolerance + 1e-9, $"pool {setByRandom.PoolMinWinRate:P0}-{setByRandom.PoolMaxWinRate:P0} around {setByRandom.WinRate:P0}");
    }

    /// <summary>The profile before onboarding without its casual floors (so any player model may calibrate), then edited.</summary>
    private static GenerationProfile LoadWithoutFloors(Action<JsonObject> edit) => TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, json =>
    {
        foreach (JsonNode? band in json["bands"]!.AsArray())
        {
            band!.AsObject().Remove("casualFloor");
        }

        edit(json);
    });
}
