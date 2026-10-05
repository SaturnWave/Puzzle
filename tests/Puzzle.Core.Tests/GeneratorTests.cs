using System.Text.Json.Nodes;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Generator;
using Puzzle.Generator.Calibration;
using Puzzle.Generator.Composition;
using Puzzle.Generator.Layout;
using Puzzle.Generator.Planning;
using Puzzle.Generator.Profiles;

namespace Puzzle.Core.Tests;

public class GeneratorTests
{
    [Fact]
    public void Bands_inherit_what_they_do_not_override()
    {
        GenerationProfile profile = RepoFiles.DefaultProfile;

        ProfileBand early = profile.BandFor(6);
        ProfileBand late = profile.BandFor(350);

        Assert.Equal(5, early.Colors.Normal);
        Assert.Equal(0.80, early.WinRate.Normal);   // inherited from the band at level 1
        Assert.Equal(new MoveRange(18, 28), early.Moves.Normal);
        Assert.Equal(0.40, late.WinRate.Hard);
        Assert.Equal(3, late.MaxObstacleTypes);       // inherited from the band at level 201
    }

    [Fact]
    public void Planner_follows_tutorial_cycle_sizes_hooks_and_palettes()
    {
        var planner = new LevelPlanner(RepoFiles.DefaultProfile);

        Assert.All(Enumerable.Range(1, 20), n => Assert.Equal(DifficultyTier.Easy, planner.Plan(n).Tier));
        Assert.Equal(DifficultyTier.SuperHard, planner.Plan(30).Tier);
        Assert.Equal(new BoardSize(10, 10), planner.Plan(25).Size);
        Assert.DoesNotContain(Mechanic.Box1, planner.Plan(10).Unlocked);
        Assert.Contains(Mechanic.Box1, planner.Plan(11).Featured);
        Assert.Contains(Mechanic.Box1, planner.Plan(13).Featured);
        Assert.DoesNotContain(Mechanic.Box1, planner.Plan(14).Featured);
        Assert.Equal(GemColors.First(6), planner.Plan(100).Palette);
        Assert.Equal(planner.Plan(400).Palette.Count, RepoFiles.DefaultProfile.BandFor(400).Colors.For(planner.Plan(400).Tier));
        LevelPlan once = planner.Plan(77);
        LevelPlan again = planner.Plan(77);
        Assert.Equal((once.Seed, once.Size, once.Tier), (again.Seed, again.Size, again.Tier));
        Assert.Equal(once.Palette, again.Palette);
    }

    [Fact]
    public void Mechanics_open_on_the_first_level_of_a_painting()
    {
        GenerationProfile profile = RepoFiles.DefaultProfile;
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

    // The tests below pin generator mechanics on particular levels and numbers of the profile before onboarding
    // (RepoFiles.ProfileBeforeOnboarding), not of the shipped curve.
    private static GenerationProfile TinyProfile => TestProfiles.Tiny;

    [Fact]
    public void Generated_level_features_its_hook_and_is_reproducible()
    {
        GeneratedLevel first = new LevelGenerator(TinyProfile).Generate(13, []);
        GeneratedLevel second = new LevelGenerator(TinyProfile).Generate(13, []);

        Assert.Contains(first.Level.Goals, g => g is GoalSpec.BreakBoxes);
        Assert.Equal(DifficultyTier.Normal, first.Level.Difficulty);
        Assert.NotEmpty(first.Level.Attempts.SeedPool);
        Assert.Equal(LevelFileMapper.ToFile(first.Level).cells, LevelFileMapper.ToFile(second.Level).cells);
        Assert.Equal(first.Level.MoveLimit, second.Level.MoveLimit);
        Assert.Equal(first.Level.Attempts.SeedPool, second.Level.Attempts.SeedPool);
        Assert.InRange(Assert.NotNull(first.Metrics.WinRateWithExtraMoves), first.Metrics.WinRate, 1.0); // five more moves never lose a won game
        Assert.Equal(0.60, Assert.IsType<FloorCheck>(first.Metrics.Floor).WinRate.Required);             // a Normal level of the first band
        Assert.Equal((first.Metrics.Floor, first.Metrics.FloorSetLimit), (second.Metrics.Floor, second.Metrics.FloorSetLimit));
    }

    [Fact]
    public void A_binding_casual_floor_raises_the_limit_until_the_floor_bot_reaches_it_on_fresh_seeds()
    {
        // Normal levels aim at 95 % for the calibration bot and 80 % for the random bot, which needs far more moves.
        // Above 95 % the calibration bot cannot overshoot by more than the tolerance, so whether a limit the floor set
        // stands on fresh seeds is up to the floor bot. The wide move range keeps both limits in range.
        GenerationProfile guarded = TinyProfile with
        {
            FloorBot = "random",
            Bands = TinyProfile.Bands.Select(band => band with
            {
                WinRate = band.WinRate with { Normal = 0.95 },
                CasualFloor = band.CasualFloor with { Normal = 0.80 },
                Moves = band.Moves with { Normal = new MoveRange(12, 40) },
            }).ToArray(),
        };
        GenerationProfile unguarded = guarded with { Bands = guarded.Bands.Select(band => band with { CasualFloor = band.CasualFloor with { Normal = null } }).ToArray() };

        LevelMetrics withFloor = new LevelGenerator(guarded).Generate(13, []).Metrics;
        LevelMetrics withoutFloor = new LevelGenerator(unguarded).Generate(13, []).Metrics;

        WinRateCheck floor = Assert.IsType<FloorCheck>(withFloor.Floor).WinRate;
        Assert.False(withFloor.Relaxed);
        Assert.True(withFloor.FloorSetLimit, "the floor bot needed no more moves than the calibration bot");
        Assert.True(floor.Measured >= floor.Required - guarded.Calibration.WinRateTolerance, $"the floor bot won {floor.Measured:P0} of the fresh-seed games at {withFloor.MoveLimit} moves");
        Assert.True(withFloor.MoveLimit >= withoutFloor.MoveLimit, $"{withFloor.MoveLimit} moves with the floor, {withoutFloor.MoveLimit} without");
        Assert.Null(withoutFloor.Floor);
    }

    /// <summary>The default profile with the random bot behind the casual guard: it needs far more moves than the calibration bot.</summary>
    private static GenerationProfile RandomFloorProfile => TinyProfile with { FloorBot = "random" };

    [Fact]
    public void A_floor_that_needs_more_moves_than_the_range_allows_shrinks_the_goals_until_its_limit_fits()
    {
        // Level 7 is Normal: 85 % target, 60 % floor, 18-28 moves. On its first candidate the calibration bot needs about
        // 20 moves, inside the range, and the random bot about 38. A limit read from the target alone would be checked
        // as it is and fail on the floor; read from both, it is out of range and the collect goal shrinks until it fits.
        LevelPlan plan = new LevelPlanner(RandomFloorProfile).Plan(7);
        Candidate candidate = Assert.IsType<Candidate>(CandidateFactory.TryCreate(plan, attempt: 0, density: 1.0));

        CalibrationOutcome outcome = new LevelCalibrator(RandomFloorProfile).Calibrate(candidate, plan);

        var accepted = Assert.IsType<CalibrationOutcome.Accepted>(outcome);
        Assert.True(accepted.FloorSetLimit, "the floor did not decide the limit");
        Assert.InRange(accepted.Level.MoveLimit, plan.Moves.Min, plan.Moves.Max);
        Assert.True(
            CollectTarget(accepted.Level) < CollectTarget(candidate.Build(plan, scale: 1)),
            $"collect goal {CollectTarget(accepted.Level)}, {CollectTarget(candidate.Build(plan, scale: 1))} unscaled");
    }

    [Fact]
    public void A_limit_the_floor_set_is_reported_as_set_by_the_floor_when_fresh_seeds_confirm_it()
    {
        // Level 7's second candidate: the random bot needs more moves than the calibration bot and both fit the range.
        LevelPlan plan = new LevelPlanner(RandomFloorProfile).Plan(7);
        Candidate candidate = Assert.IsType<Candidate>(CandidateFactory.TryCreate(plan, attempt: 1, density: 1.0));

        var accepted = Assert.IsType<CalibrationOutcome.Accepted>(new LevelCalibrator(RandomFloorProfile).Calibrate(candidate, plan));

        // A limit re-read from the fresh-seed games would equal theirs; this one stood as calibrated.
        Assert.NotEqual(accepted.Verification.ReadLimit()?.Moves, accepted.Level.MoveLimit);
        Assert.True(accepted.FloorSetLimit, "a limit the floor set and fresh seeds confirmed lost its mark");
    }

    [Fact]
    public void A_relaxed_level_keeps_its_casual_floor_in_the_report()
    {
        // Level 13 is Normal and features boxes. The random bot cannot break them for the 60 % floor within 5-6 moves,
        // and goals are never rescaled here, so no candidate fits and the closest is kept.
        GenerationProfile cramped = RandomFloorProfile with
        {
            Bands = RandomFloorProfile.Bands.Select(band => band with { Moves = band.Moves with { Normal = new MoveRange(5, 6) } }).ToArray(),
            Calibration = RandomFloorProfile.Calibration with { MaxCandidates = 2, MaxScaleIterations = 1 },
        };

        LevelMetrics relaxed = new LevelGenerator(cramped).Generate(13, []).Metrics;

        Assert.True(relaxed.Relaxed, $"{relaxed.MoveLimit} moves were accepted");
        FloorCheck floor = Assert.IsType<FloorCheck>(relaxed.Floor);
        Assert.Equal(("random", 0.60), (floor.Bot, floor.WinRate.Required));
    }

    private static int CollectTarget(LevelDefinition level) => level.Goals.OfType<GoalSpec.CollectColor>().Single().Target;

    [Fact]
    public void Seed_pool_never_drops_below_the_minimum()
    {
        GenerationProfile strict = RepoFiles.ProfileBeforeOnboarding with
        {
            Calibration = RepoFiles.ProfileBeforeOnboarding.Calibration with { Runs = 30, VerifyRuns = 30, ReportRuns = 10, MaxCandidates = 3, MaxScaleIterations = 2 },
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
        GenerationProfile reporting = LoadProfileWith(calibrationBot: "average", "expert", "usta-48");
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadProfileWith(calibrationBot: "usta", "expert"));
        ProfileFormatException rejectedBudget = Assert.Throws<ProfileFormatException>(() => LoadProfileWith(calibrationBot: "USTA-12", "expert"));

        Assert.Equal(new[] { "expert", "usta-48" }, reporting.ReportBots);
        Assert.Contains("calibrationBot 'usta' is a measurement bot", rejected.Message);
        Assert.Contains("calibrationBot 'USTA-12' is a measurement bot", rejectedBudget.Message);
    }

    [Fact]
    public void A_null_report_bot_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadProfileWith(calibrationBot: "average", "expert", null));

        Assert.Contains("reportBots has a null entry", rejected.Message);
    }

    [Fact]
    public void Casual_floors_are_inherited_like_win_rates_and_tiers_without_one_have_none()
    {
        GenerationProfile profile = RepoFiles.DefaultProfile;

        Assert.Equal("casual", profile.FloorBot);
        Assert.All(new[] { 1, 4, 6, 11, 31 }, level => Assert.Equal(new TierValues<double?>(null, null, null, null), profile.BandFor(level).CasualFloor)); // no band before 51 names a floor
        Assert.Equal((0.75, 0.50), (profile.BandFor(121).CasualFloor.Easy, profile.BandFor(121).CasualFloor.Normal));  // inherited from level 51
        Assert.Equal((0.70, 0.40), (profile.BandFor(700).CasualFloor.Easy, profile.BandFor(700).CasualFloor.Normal));  // inherited from level 301
        Assert.All(new[] { 51, 301, 601 }, level => Assert.Null(profile.BandFor(level).CasualFloor.Hard));
        Assert.All(new[] { 51, 301, 601 }, level => Assert.Null(profile.BandFor(level).CasualFloor.SuperHard));

        var planner = new LevelPlanner(profile);
        Assert.Null(planner.Plan(1).CasualFloor);          // the first museum room has no floor
        Assert.Equal(0.50, planner.Plan(52).CasualFloor);  // Normal
        Assert.Null(planner.Plan(55).CasualFloor);         // Hard
    }

    [Fact]
    public void A_floor_named_by_the_first_band_is_inherited_until_a_band_names_another()
    {
        GenerationProfile profile = RepoFiles.ProfileBeforeOnboarding;

        Assert.Equal((0.80, 0.60), (profile.BandFor(6).CasualFloor.Easy, profile.BandFor(6).CasualFloor.Normal));     // inherited from level 1
        Assert.Equal((0.75, 0.50), (profile.BandFor(51).CasualFloor.Easy, profile.BandFor(51).CasualFloor.Normal));   // named at level 51
        Assert.Equal(0.80, new LevelPlanner(profile).Plan(1).CasualFloor);                                             // tutorial levels are Easy
    }

    [Fact]
    public void Move_ranges_from_level_51_leave_room_for_the_casual_floor()
    {
        GenerationProfile profile = RepoFiles.DefaultProfile;

        Assert.Equal((new MoveRange(16, 24), new MoveRange(18, 28)), (profile.BandFor(50).Moves.Easy, profile.BandFor(50).Moves.Normal));
        Assert.Equal((new MoveRange(18, 29), new MoveRange(20, 33)), (profile.BandFor(51).Moves.Easy, profile.BandFor(51).Moves.Normal));
        Assert.Equal((new MoveRange(18, 31), new MoveRange(22, 35)), (profile.BandFor(301).Moves.Easy, profile.BandFor(301).Moves.Normal));
        Assert.Equal((new MoveRange(24, 36), new MoveRange(26, 38)), (profile.BandFor(51).Moves.Hard, profile.BandFor(301).Moves.Hard));
    }

    [Fact]
    public void The_floor_bot_defaults_to_casual()
    {
        GenerationProfile profile = LoadProfileEdited(json => json.Remove("floorBot"));

        Assert.Equal("casual", profile.FloorBot);
    }

    [Fact]
    public void A_floor_above_the_target_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() =>
            LoadProfileEdited(json => json["bands"]![0]!["casualFloor"]!["Easy"] = 0.96));

        Assert.Contains("band fromLevel 1: casualFloor Easy is 0.96; a floor must be above 0 and at most the Easy win rate 0.95", rejected.Message);
    }

    [Fact]
    public void An_inherited_floor_above_a_lowered_target_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json =>
        {
            JsonObject band51 = json["bands"]![3]!.AsObject();
            band51.Remove("casualFloor");                   // inherits Easy 0.80 from level 1
            band51["winRate"]!["Easy"] = 0.78;
        }));

        Assert.Contains("band fromLevel 51: casualFloor Easy is 0.8; a floor must be above 0 and at most the Easy win rate 0.78", rejected.Message);
    }

    [Fact]
    public void A_zero_floor_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() =>
            LoadProfileEdited(json => json["bands"]![0]!["casualFloor"]!["Normal"] = 0));

        Assert.Contains("casualFloor Normal is 0; a floor must be above 0", rejected.Message);
    }

    [Fact]
    public void A_floor_for_an_unknown_tier_is_a_profile_error()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() =>
            LoadProfileEdited(json => json["bands"]![0]!["casualFloor"]!["Medium"] = 0.5));

        Assert.Contains("band fromLevel 1 casualFloor: unknown tier 'Medium'", rejected.Message);
    }

    [Fact]
    public void The_floor_bot_must_be_a_known_player_model()
    {
        ProfileFormatException measurement = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json => json["floorBot"] = "usta"));
        ProfileFormatException unknown = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json => json["floorBot"] = "beginner"));

        Assert.Contains("floorBot 'usta' is a measurement bot; the casual floor is checked against a player model", measurement.Message);
        Assert.Contains("unknown bot 'beginner'", unknown.Message);
    }

    [Fact]
    public void The_floor_bot_must_not_be_the_calibration_bot()
    {
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json => json["floorBot"] = "Average"));

        Assert.Contains("floorBot 'Average' is not weaker than calibrationBot 'average'", rejected.Message);
    }

    [Fact]
    public void The_floor_bot_must_be_weaker_than_the_calibration_bot()
    {
        ProfileFormatException stronger = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json => json["floorBot"] = "expert"));

        Assert.Contains("floorBot 'expert' is not weaker than calibrationBot 'average'", stronger.Message);
        Assert.Contains("random < novice < casual < average < expert", stronger.Message);
        Assert.Equal("novice", LoadProfileEdited(json => json["floorBot"] = "novice").FloorBot);
        Assert.Equal("random", LoadProfileEdited(json => json["floorBot"] = "random").FloorBot);
    }

    [Fact]
    public void The_floor_bot_is_checked_only_when_some_band_has_a_casual_floor()
    {
        // Calibrating against the casual bot leaves the default floor bot, casual, no weaker player to model.
        static void CalibrateAgainstCasual(JsonObject json)
        {
            json["calibrationBot"] = "casual";
            json.Remove("floorBot");
        }

        GenerationProfile withoutFloors = LoadProfileEdited(json =>
        {
            CalibrateAgainstCasual(json);
            foreach (JsonNode? band in json["bands"]!.AsArray())
            {
                band!.AsObject().Remove("casualFloor");
            }
        });
        ProfileFormatException withFloors = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(CalibrateAgainstCasual));

        Assert.Equal(("casual", "casual"), (withoutFloors.BandFor(1).CalibrationBot, withoutFloors.FloorBot));
        Assert.Contains("floorBot 'casual' is not weaker than calibrationBot 'casual'", withFloors.Message);
    }

    [Fact]
    public void A_misspelled_floor_bot_is_rejected_even_before_any_band_has_a_casual_floor()
    {
        ProfileFormatException error = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json =>
        {
            json["floorBot"] = "casul";
            foreach (JsonNode? band in json["bands"]!.AsArray())
            {
                band!.AsObject().Remove("casualFloor");
            }
        }));

        Assert.Contains("casul", error.Message);
    }

    [Fact]
    public void A_casual_floor_on_any_tier_makes_the_floor_bot_prove_it_is_weaker()
    {
        // Floors only on Normal: the strength check must still run, not only when Easy has a floor.
        ProfileFormatException error = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json =>
        {
            json["floorBot"] = "expert";
            foreach (JsonNode? band in json["bands"]!.AsArray())
            {
                band!.AsObject()["casualFloor"]?.AsObject().Remove("Easy");
            }
        }));

        Assert.Contains("floorBot 'expert' is not weaker than calibrationBot 'average'", error.Message);
    }

    [Fact]
    public void Boards_stay_plain_rectangles_through_level_20_and_may_be_shaped_from_21()
    {
        var planner = new LevelPlanner(RepoFiles.DefaultProfile);
        string[] ShapesOf(int level) => Enumerable.Range(0, 8)
            .Select(attempt => CandidateFactory.TryCreate(planner.Plan(level), attempt, density: 1.0))
            .OfType<Candidate>()
            .Select(candidate => candidate.Style.Split('+')[0])
            .ToArray();

        Assert.True(planner.Plan(20).PlainShape);
        Assert.False(planner.Plan(21).PlainShape);
        Assert.NotEmpty(ShapesOf(20));
        Assert.All(ShapesOf(20), shape => Assert.Equal("Full", shape));
        Assert.Contains(ShapesOf(21), shape => shape != "Full");
    }

    [Fact]
    public void Plain_shapes_end_after_level_10_when_the_profile_does_not_say()
    {
        GenerationProfile profile = LoadProfileEdited(json => json["sizes"]!.AsObject().Remove("plainShapesUntil"));

        Assert.Equal(10, profile.Sizes.PlainShapesUntil);
        Assert.True(new LevelPlanner(profile).Plan(10).PlainShape);
        Assert.False(new LevelPlanner(profile).Plan(11).PlainShape);
    }

    [Theory]
    [InlineData("", "moveMargin", "$.moveMargin")]                         // belongs under calibration
    [InlineData("bands", "calibratonBot", "$.bands[7].calibratonBot")]    // a misspelt calibrationBot would leave the inherited bot in charge
    [InlineData("bands", "moveMargin", "$.bands[7].moveMargin")]          // the margin is the profile's, not a band's
    [InlineData("calibration", "moveMargins", "$.calibration.moveMargins")]
    [InlineData("sizes", "fixedTill", "$.sizes.fixedTill")]
    [InlineData("palette", "chapterSizes", "$.palette.chapterSizes")]
    [InlineData("mechanics", "unlocks", "$.mechanics[7].unlocks")]
    [InlineData("seedPool", "poolSizes", "$.seedPool.poolSizes")]
    public void A_field_the_profile_format_does_not_have_is_a_profile_error(string section, string field, string place)
    {
        // The field goes into the profile itself, into the section, or into the last entry of a listed section.
        ProfileFormatException rejected = Assert.Throws<ProfileFormatException>(() => LoadProfileEdited(json =>
        {
            JsonNode holder = section.Length == 0 ? json : json[section]!;
            JsonObject target = holder is JsonArray entries ? entries[^1]!.AsObject() : holder.AsObject();
            target[field] = 1;
        }));

        Assert.Contains($"is not a valid profile at {place}: ", rejected.Message);
        Assert.Contains($"'{field}'", rejected.Message);
    }

    /// <summary>Loads the profile before onboarding with other bots.</summary>
    private static GenerationProfile LoadProfileWith(string calibrationBot, params string?[] reportBots) => LoadProfileEdited(profile =>
    {
        profile["calibrationBot"] = calibrationBot;
        profile["reportBots"] = new JsonArray(reportBots.Select(bot => JsonValue.Create(bot)).ToArray<JsonNode?>());
    });

    /// <summary>Loads the profile before onboarding after <paramref name="edit"/> changed its JSON.</summary>
    private static GenerationProfile LoadProfileEdited(Action<JsonObject> edit) =>
        TestProfiles.LoadEdited(RepoFiles.ProfileBeforeOnboardingPath, edit);
}
