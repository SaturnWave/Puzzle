using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Puzzle.Bot.Simulation;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

/// <summary>Generation profiles shaped for a test.</summary>
internal static class TestProfiles
{
    /// <summary>The profile before onboarding with few games everywhere, so a test can generate whole levels quickly.</summary>
    public static GenerationProfile Tiny => RepoFiles.ProfileBeforeOnboarding with
    {
        Calibration = RepoFiles.ProfileBeforeOnboarding.Calibration with { Runs = 30, VerifyRuns = 30, ReportRuns = 10, MaxCandidates = 4, MaxScaleIterations = 3 },
        SeedPool = new SeedPoolSettings { Candidates = 6, GamesPerSeed = 6, PoolSize = 4, TolerancePoints = 20 },
    };

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="share"/> is outside 0..1.</exception>
    public static MoveMargin Margin(decimal share) => MoveMargin.TryCreate(share, out MoveMargin margin)
        ? margin
        : throw new ArgumentOutOfRangeException(nameof(share), share, "A move margin is a share between 0 and 1.");

    public static GenerationProfile WithMoveMargin(this GenerationProfile profile, decimal share) =>
        profile with { Calibration = profile.Calibration with { MoveMargin = Margin(share) } };

    /// <summary>The same rules without any casual floor, so the calibration bot alone decides every limit.</summary>
    public static GenerationProfile WithoutFloors(this GenerationProfile profile) => profile with
    {
        Bands = profile.Bands.Select(band => band with { CasualFloor = new TierValues<double?>(null, null, null, null) }).ToArray(),
    };

    /// <summary>The same rules with <paramref name="bot"/> calibrating every band that starts at <paramref name="fromLevel"/> or later.</summary>
    public static GenerationProfile CalibratedBy(this GenerationProfile profile, string bot, int fromLevel = 1) => profile with
    {
        Bands = profile.Bands.Select(band => band.FromLevel >= fromLevel ? band with { CalibrationBot = bot } : band).ToArray(),
    };

    /// <summary>
    /// Loads the profile at <paramref name="path"/> after <paramref name="edit"/> changed its JSON, through a temporary
    /// file that is removed afterwards.
    /// </summary>
    public static GenerationProfile LoadEdited(string path, Action<JsonObject> edit)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonObject profile = JsonNode.Parse(File.ReadAllText(path), documentOptions: options)!.AsObject();
        edit(profile);
        string editedPath = Path.Combine(Path.GetTempPath(), $"puzzle-profile-{Guid.NewGuid():N}.json");
        File.WriteAllText(editedPath, profile.ToJsonString());
        try
        {
            return ProfileLoader.Load(editedPath);
        }
        finally
        {
            File.Delete(editedPath);
        }
    }
}
