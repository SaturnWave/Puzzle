using System.Text.Json.Nodes;
using Puzzle.Generator;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>
/// A generation profile may name a rung of the skill ladder wherever it names a player model: as the calibration bot of
/// the profile or of a band, and as the floor bot, which must be the weaker of the two whichever of them is a rung.
/// </summary>
public class LadderBotProfileTests
{
    // In the profile before onboarding the average bot calibrates every band and the casual bot guards every band's
    // floors; the bands start at levels 1, 6, 31, 51, 121, 201, 301 and 601.
    private const int BandAt6 = 1;
    private const int BandAt51 = 3;

    [Fact]
    public void A_rung_may_calibrate_the_profile_or_a_band_and_be_the_floor_bot()
    {
        GenerationProfile profile = LoadEdited(json =>
        {
            json["calibrationBot"] = "skill-60";
            json["bands"]![BandAt51]!["calibrationBot"] = "Skill-90";
            json["floorBot"] = "skill-45";
            json["reportBots"] = new JsonArray("skill-30", "expert");
        });

        Assert.Equal("skill-45", profile.FloorBot);
        Assert.Equal(new[] { "skill-30", "expert" }, profile.ReportBots);
        Assert.Equal(
            ("skill-60", "skill-60", "Skill-90", "Skill-90"),
            (profile.BandFor(1).CalibrationBot, profile.BandFor(50).CalibrationBot, profile.BandFor(51).CalibrationBot, profile.BandFor(1000).CalibrationBot));
    }

    [Theory]
    [InlineData("skill-80")]    // just under the average bot, which is skill-85
    [InlineData("skill-30")]
    [InlineData("casual")]
    [InlineData("random")]
    public void A_floor_bot_weaker_than_every_calibration_bot_is_accepted(string floorBot)
    {
        Assert.Equal(floorBot, LoadEdited(json => json["floorBot"] = floorBot).FloorBot);
    }

    [Theory]
    [InlineData("skill-85")]    // as strong as the average bot
    [InlineData("skill-90")]
    [InlineData("skill-100")]
    public void A_rung_as_strong_as_the_calibration_bot_or_stronger_is_no_floor_bot(string floorBot)
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["floorBot"] = floorBot));

        Assert.Contains($"band fromLevel 1: floorBot '{floorBot}' is not weaker than calibrationBot 'average'", rejected.Message);
        Assert.Contains(
            "random < novice < casual < average < expert, with a skill-<n> bot at its n (novice = skill-50, casual = skill-70, average = skill-85, expert = skill-100)",
            rejected.Message);
    }

    [Theory]
    [InlineData("skill-70")]    // as strong as the casual floor bot
    [InlineData("skill-65")]
    [InlineData("skill-30")]
    public void A_band_calibrated_by_a_rung_no_stronger_than_the_floor_bot_is_rejected(string calibrationBot)
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["bands"]![BandAt6]!["calibrationBot"] = calibrationBot));

        Assert.Contains($"band fromLevel 6: floorBot 'casual' is not weaker than calibrationBot '{calibrationBot}'", rejected.Message);
    }

    [Fact]
    public void A_band_calibrated_by_a_rung_stronger_than_the_floor_bot_is_accepted()
    {
        GenerationProfile profile = LoadEdited(json => json["bands"]![BandAt6]!["calibrationBot"] = "skill-75");

        Assert.Equal(("casual", "average", "skill-75"), (profile.FloorBot, profile.BandFor(5).CalibrationBot, profile.BandFor(6).CalibrationBot));
    }

    [Theory]
    [InlineData("skill-52")]
    [InlineData("skill-25")]
    [InlineData("skill-105")]
    [InlineData("skill-x")]
    public void A_name_off_the_ladder_is_an_unknown_bot_wherever_a_profile_names_it(string name)
    {
        ProfileFormatException asCalibrationBot = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["calibrationBot"] = name));
        ProfileFormatException asBandsBot = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["bands"]![BandAt6]!["calibrationBot"] = name));
        ProfileFormatException asFloorBot = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["floorBot"] = name));

        Assert.StartsWith($"unknown bot '{name}' (use random, novice, casual, average, expert, skill-<n> (n = 30, 35 .. 100), usta or usta-<n>)", asCalibrationBot.Message);
        Assert.StartsWith($"band fromLevel 6: unknown bot '{name}'", asBandsBot.Message);
        Assert.StartsWith($"unknown bot '{name}'", asFloorBot.Message);
    }

    [Fact]
    public void A_measurement_bot_is_told_that_the_rungs_are_player_models_too()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadEdited(json => json["floorBot"] = "usta"));

        Assert.Contains("(random, novice, casual, average, expert, skill-<n> (n = 30, 35 .. 100))", rejected.Message);
    }

    [Fact]
    public void A_level_is_calibrated_with_the_rung_its_band_names()
    {
        // Level 7 is Normal with an 85 % target. Its limit is read from the games of skill-60, so at that limit the
        // weakest rung wins no more often than the expert.
        GenerationProfile profile = TestProfiles.Tiny.WithoutFloors().CalibratedBy("skill-60") with { ReportBots = ["skill-30", "expert"] };

        LevelMetrics metrics = new LevelGenerator(profile).Generate(7, []).Metrics;

        Assert.Equal(("skill-60", 0.85), (metrics.CalibrationBot, metrics.TargetWinRate));
        Assert.True(metrics.WinRate >= 0.85 - profile.Calibration.WinRateTolerance, $"skill-60 won {metrics.WinRate:P0} at {metrics.MoveLimit} moves");
        Assert.True(
            metrics.BotWinRates["skill-30"] <= metrics.BotWinRates["expert"],
            $"skill-30 won {metrics.BotWinRates["skill-30"]:P0}, the expert {metrics.BotWinRates["expert"]:P0}");
    }

    /// <summary>Loads the profile before onboarding after <paramref name="edit"/> changed its JSON.</summary>
    private static GenerationProfile LoadEdited(Action<JsonObject> edit) => TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, edit);
}
