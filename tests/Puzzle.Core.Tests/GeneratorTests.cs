using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Generator;
using Puzzle.Generator.Layout;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class GeneratorTests
{
    private static readonly Lazy<GenerationProfile> DefaultProfile = new(() => ProfileLoader.Load(FindRepoFile("profiles", "default.json")));

    [Fact]
    public void Bands_inherit_what_they_do_not_override()
    {
        GenerationProfile profile = DefaultProfile.Value;

        ProfileBand early = profile.BandFor(6);
        ProfileBand late = profile.BandFor(350);

        Assert.Equal(5, early.Colors.Normal);
        Assert.Equal(0.85, early.WinRate.Normal);   // inherited from the band at level 1
        Assert.Equal(new MoveRange(18, 28), early.Moves.Normal);
        Assert.Equal(0.40, late.WinRate.Hard);
        Assert.Equal(3, late.MaxObstacleTypes);       // inherited from the band at level 201
    }

    [Fact]
    public void Planner_follows_tutorial_cycle_sizes_hooks_and_palettes()
    {
        var planner = new LevelPlanner(DefaultProfile.Value);

        Assert.All(Enumerable.Range(1, 5), n => Assert.Equal(DifficultyTier.Easy, planner.Plan(n).Tier));
        Assert.Equal(DifficultyTier.SuperHard, planner.Plan(20).Tier);
        Assert.Equal(new BoardSize(10, 10), planner.Plan(25).Size);
        Assert.DoesNotContain(Mechanic.Box1, planner.Plan(10).Unlocked);
        Assert.Contains(Mechanic.Box1, planner.Plan(11).Featured);
        Assert.Contains(Mechanic.Box1, planner.Plan(13).Featured);
        Assert.DoesNotContain(Mechanic.Box1, planner.Plan(14).Featured);
        Assert.Equal(GemColors.First(6), planner.Plan(100).Palette);
        Assert.Equal(planner.Plan(400).Palette.Count, DefaultProfile.Value.BandFor(400).Colors.For(planner.Plan(400).Tier));
        LevelPlan once = planner.Plan(77);
        LevelPlan again = planner.Plan(77);
        Assert.Equal((once.Seed, once.Size, once.Tier), (again.Seed, again.Size, again.Tier));
        Assert.Equal(once.Palette, again.Palette);
    }

    [Fact]
    public void Mechanics_open_on_the_first_level_of_a_painting()
    {
        GenerationProfile profile = DefaultProfile.Value;
        int paintingLevels = profile.Cycle.Count;          // one painting = one saw-tooth cycle (GAME_DESIGN §18)
        int roomLevels = profile.Palette.ChapterSize;      // one museum room = one palette chapter

        Assert.Equal(
            new[]
            {
                new MechanicUnlock(11, Mechanic.Box1),
                new MechanicUnlock(21, Mechanic.Jelly1),
                new MechanicUnlock(31, Mechanic.Box2),
                new MechanicUnlock(41, Mechanic.Chain1),
                new MechanicUnlock(51, Mechanic.Items),
                new MechanicUnlock(71, Mechanic.Jelly2),
                new MechanicUnlock(91, Mechanic.Box3),
                new MechanicUnlock(111, Mechanic.Chain2),
            },
            profile.Mechanics);
        Assert.All(profile.Mechanics, unlock => Assert.Equal(1, unlock.FromLevel % paintingLevels));
        Assert.Equal(1, profile.Mechanics.Single(u => u.Mechanic == Mechanic.Items).FromLevel % roomLevels); // opens the second museum room
    }

    [Fact]
    public void Shapes_are_mirrored()
    {
        var size = new BoardSize(11, 12);
        foreach (ShapeStyle style in Enum.GetValues<ShapeStyle>())
        {
            for (ulong seed = 0; seed < 20; seed++)
            {
                bool[] mask = ShapeBuilder.Build(style, size, new Rng(seed));
                for (int i = 0; i < mask.Length; i++)
                {
                    int x = i % size.Width;
                    Assert.Equal(mask[i], mask[i - x + (size.Width - 1 - x)]);
                }
            }
        }
    }

    [Fact]
    public void A_row_of_boxes_seals_the_cells_below_but_pairs_do_not()
    {
        var size = new BoardSize(7, 6);
        var sealedRow = new LayoutDraft(size, Enumerable.Repeat(true, 42).ToArray());
        sealedRow.SetTileMirrored(2, 2, '1');
        sealedRow.SetTileMirrored(3, 2, '1');
        var pairs = new LayoutDraft(size, Enumerable.Repeat(true, 42).ToArray());
        pairs.SetTileMirrored(1, 2, '1'); // a pair touching the edge would seal the corner cell below it
        pairs.SetTileMirrored(2, 2, '1');

        Assert.False(Feedability.AllCellsFed(sealedRow)); // boxes at x=2,3,4: cell (3,3) has no open diagonal
        Assert.True(Feedability.AllCellsFed(pairs));
    }

    [Fact]
    public void Placed_boxes_keep_every_cell_fed_and_symmetric()
    {
        var size = new BoardSize(10, 11);
        for (ulong seed = 0; seed < 10; seed++)
        {
            var draft = new LayoutDraft(size, Enumerable.Repeat(true, 110).ToArray());

            ObstaclePainter.PlaceBoxes(draft, BoxPattern.Clusters, target: 20, maxHp: 2, new Rng(seed));

            Assert.True(Feedability.AllCellsFed(draft));
            Assert.Equal(draft.Tiles, draft.Tiles.Select((_, i) => draft.Tiles[draft.Index(draft.MirrorX(i % 10), i / 10)]).ToArray());
        }
    }

    [Fact]
    public void Generated_level_features_its_hook_and_is_reproducible()
    {
        GenerationProfile tiny = DefaultProfile.Value with
        {
            Calibration = DefaultProfile.Value.Calibration with { Runs = 30, VerifyRuns = 30, ReportRuns = 10, MaxCandidates = 4, MaxScaleIterations = 3 },
            SeedPool = new SeedPoolSettings { Candidates = 6, GamesPerSeed = 6, PoolSize = 4, TolerancePoints = 20 },
        };

        GeneratedLevel first = new LevelGenerator(tiny).Generate(13, []);
        GeneratedLevel second = new LevelGenerator(tiny).Generate(13, []);

        Assert.Contains(first.Level.Goals, g => g is GoalSpec.BreakBoxes);
        Assert.Equal(DifficultyTier.Normal, first.Level.Difficulty);
        Assert.NotEmpty(first.Level.Attempts.SeedPool);
        Assert.Equal(LevelFileMapper.ToFile(first.Level).cells, LevelFileMapper.ToFile(second.Level).cells);
        Assert.Equal(first.Level.MoveLimit, second.Level.MoveLimit);
        Assert.Equal(first.Level.Attempts.SeedPool, second.Level.Attempts.SeedPool);
        Assert.InRange(Assert.NotNull(first.Metrics.WinRateWithExtraMoves), first.Metrics.WinRate, 1.0); // five more moves never lose a won game
    }

    [Fact]
    public void Seed_pool_never_drops_below_the_minimum()
    {
        GenerationProfile strict = DefaultProfile.Value with
        {
            Calibration = DefaultProfile.Value.Calibration with { Runs = 30, VerifyRuns = 30, ReportRuns = 10, MaxCandidates = 3, MaxScaleIterations = 2 },
            SeedPool = new SeedPoolSettings { Candidates = 6, GamesPerSeed = 6, PoolSize = 8, TolerancePoints = 1 },
            MinPoolSize = 5,
        };

        GeneratedLevel level = new LevelGenerator(strict).Generate(7, []);

        Assert.True(level.Level.Attempts.SeedPool.Count >= 5, $"pool of {level.Level.Attempts.SeedPool.Count}");
        Assert.Equal(level.Level.Attempts.SeedPool.Count, level.Metrics.PoolSize);
    }

    [Fact]
    public void A_measurement_bot_may_report_on_levels_but_never_calibrate_them()
    {
        GenerationProfile reporting = LoadDefaultProfileWith(calibrationBot: "average", "expert", "usta-48");
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadDefaultProfileWith(calibrationBot: "usta", "expert"));
        ProfileFormatException rejectedBudget = Assert.Throws<ProfileFormatException>(() => LoadDefaultProfileWith(calibrationBot: "USTA-12", "expert"));

        Assert.Equal(new[] { "expert", "usta-48" }, reporting.ReportBots);
        Assert.Contains("calibrationBot 'usta' is a measurement bot", rejected.Message);
        Assert.Contains("calibrationBot 'USTA-12' is a measurement bot", rejectedBudget.Message);
    }

    [Fact]
    public void A_null_report_bot_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadDefaultProfileWith(calibrationBot: "average", "expert", null));

        Assert.Contains("reportBots has a null entry", rejected.Message);
    }

    /// <summary>Loads the default profile with other bots, through a temporary file that is removed afterwards.</summary>
    private static GenerationProfile LoadDefaultProfileWith(string calibrationBot, params string?[] reportBots)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        JsonObject profile = JsonNode.Parse(File.ReadAllText(FindRepoFile("profiles", "default.json")), documentOptions: options)!.AsObject();
        profile["calibrationBot"] = calibrationBot;
        profile["reportBots"] = new JsonArray(reportBots.Select(bot => JsonValue.Create(bot)).ToArray<JsonNode?>());
        string path = Path.Combine(Path.GetTempPath(), $"puzzle-profile-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, profile.ToJsonString());
        try
        {
            return ProfileLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FindRepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"{Path.Combine(parts)} not found above {AppContext.BaseDirectory}.");
    }
}
