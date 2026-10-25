using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Puzzle.Client.Avatar;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// The painter, the game's avatar (docs/GAME_DESIGN.md §22), in what can be said of him without the Editor: that the
/// game carries exactly the pictures art/avatar records, that what the game knows of a standing picture is what its
/// render wrote, that every step of the tutorial gives him a look, and where he stands in the hall, in a restored
/// room's hall and beside the jigsaw's tray on every screen the game is made for. <c>PainterPicture</c>,
/// <c>FigurePlace</c>, <c>HallLayout</c> and <c>JigsawLayout</c> are the Unity project's source files, compiled
/// into these tests.
/// </summary>
public class PainterTests
{
    private static readonly float[] PictureAspects = { 1.69f, 1.25f, 0.81f, 4f / 3f, 2.2f, 0.6f };

    /// <summary>The museum's screens (MuseumLayoutTests): tall phones, the 9:16 phone, the foldable, tablets both ways, with cutouts and bars.</summary>
    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 0, 0), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
        (720, 1280, 0, 0), (1200, 1920, 0, 0), (2076, 2152, 0, 0), (2076, 2152, 80, 60), (1600, 2560, 0, 0),
        (2048, 2732, 48, 40), (1640, 2360, 48, 40), (1488, 2266, 48, 40),
        (2732, 2048, 0, 40), (2732, 2048, 48, 40), (2360, 1640, 0, 40), (2152, 2076, 0, 0), (2560, 1600, 0, 0),
    };

    public static TheoryData<int, int, int, int, float> EveryScreenAndPicture()
    {
        var cases = new TheoryData<int, int, int, int, float>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            foreach (float aspect in PictureAspects)
            {
                cases.Add(width, height, top, bottom, aspect);
            }
        }

        return cases;
    }

    public static TheoryData<PainterPicture> StandingPictures() => new TheoryData<PainterPicture>
    {
        PainterPicture.StandingWithPalette, PainterPicture.StandingWaving, PainterPicture.StandingCheering,
    };

    [Fact]
    public void Every_picture_of_the_painter_is_recorded_and_the_game_carries_the_recorded_bytes_under_its_english_name()
    {
        IReadOnlyDictionary<string, (string Source, string Sha256)> recorded = RecordedPictures();

        Assert.Equal(PainterPictures.All.Length, recorded.Count);
        foreach (PainterPicture picture in PainterPictures.All)
        {
            string carried = PainterPictures.FileNameOf(picture) + ".png";
            Assert.True(recorded.ContainsKey(carried), $"art/avatar/licences.json does not record {carried}");
            (string source, string sha256) = recorded[carried];
            Assert.Equal(sha256, Sha256Of(RepoFiles.RepositoryFile($"art/avatar/{source}")));
            Assert.Equal(sha256, Sha256Of(Path.Combine(RepoFiles.ClientProjectFolder, "Assets", "Art", "Avatar", "Resources", PainterPictures.ResourceFolder, carried)));
            Assert.Equal($"{PainterPictures.ResourceFolder}/{PainterPictures.FileNameOf(picture)}", PainterPictures.ResourceOf(picture));
        }
    }

    [Theory]
    [MemberData(nameof(StandingPictures))]
    public void A_standing_pictures_feet_scale_and_height_are_those_its_render_wrote(PainterPicture standing)
    {
        string source = RecordedPictures()[PainterPictures.FileNameOf(standing) + ".png"].Source;
        using JsonDocument render = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile($"art/avatar/{Path.ChangeExtension(source, ".json")}")));
        JsonElement size = render.RootElement.GetProperty("size");
        JsonElement feet = render.RootElement.GetProperty("feet_pivot_px");
        float width = size[0].GetSingle();
        float height = size[1].GetSingle();
        float pixelsPerMetre = render.RootElement.GetProperty("px_per_metre").GetSingle();

        (float fromLeft, float fromFoot) = PainterPictures.FeetOf(standing);
        StandingMeasure measure = PainterPictures.MeasureOf(standing);

        Assert.Equal(PainterPictures.PixelsPerMetre, pixelsPerMetre);
        Assert.Equal(feet[0].GetSingle() / width, fromLeft, precision: 4);
        Assert.Equal((height - feet[1].GetSingle()) / height, fromFoot, precision: 4);
        Assert.InRange(measure.Top, render.RootElement.GetProperty("figure_height_px").GetSingle() / pixelsPerMetre - 0.01f, (height - (height - feet[1].GetSingle())) / pixelsPerMetre);
        Assert.InRange(measure.Left, measure.LowLeft, feet[0].GetSingle() / pixelsPerMetre);
        Assert.InRange(measure.Right, measure.LowRight, (width - feet[0].GetSingle()) / pixelsPerMetre);
    }

    [Fact]
    public void A_bust_stands_on_no_feet()
    {
        Assert.False(PainterPictures.IsStanding(PainterPicture.BustJoyful));
        Assert.Throws<ArgumentOutOfRangeException>(() => PainterPictures.MeasureOf(PainterPicture.BustGuiding));
        Assert.Throws<ArgumentOutOfRangeException>(() => PainterPictures.FeetOf(PainterPicture.BustThinking));
    }

    [Fact]
    public void Every_step_of_the_tutorial_gives_the_painter_a_look_and_every_look_has_a_bust_of_its_own()
    {
        PainterMood[] moods = Enum.GetValues<TutorialStep>().Select(PainterPictures.MoodOf).ToArray();
        PainterPicture[] busts = Enum.GetValues<PainterMood>().Select(PainterPictures.BustOf).ToArray();

        Assert.Equal(Enum.GetValues<PainterMood>().Order(), moods.Distinct().Order());
        Assert.Equal(PainterMood.Joyful, PainterPictures.MoodOf(TutorialStep.FirstPainting));
        Assert.All(new[] { TutorialStep.Box, TutorialStep.Jelly, TutorialStep.Chain, TutorialStep.Item }, step => Assert.Equal(PainterMood.Thinking, PainterPictures.MoodOf(step)));
        Assert.Equal(busts.Length, busts.Distinct().Count());
        Assert.All(busts, bust => Assert.False(PainterPictures.IsStanding(bust)));
    }

    [Fact]
    public void On_the_design_phone_the_painter_with_his_palette_is_about_450_pixels_tall()
    {
        var screen = new ScreenShape(1080, 2424);
        HallLayout hall = HallLayout.Of(screen, 1.69f);

        float points = hall.PainterBesideTheBench(PainterPicture.StandingWithPalette).Bounds(PainterPictures.MeasureOf(PainterPicture.StandingWithPalette)).Height;

        Assert.InRange(points * hall.Page.View.PixelsPerPoint, 440f, 460f);
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void The_painter_stands_at_the_benchs_right_on_its_floor_inside_the_screen_under_the_plate_and_clear_of_the_gift(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        float tableRight = 100f * hall.BenchScale;

        foreach (PainterPicture pose in new[] { PainterPicture.StandingWithPalette, PainterPicture.StandingWaving })
        {
            FigurePlace place = hall.PainterBesideTheBench(pose);
            Box figure = place.Bounds(PainterPictures.MeasureOf(pose));

            Assert.Equal(hall.BenchFeet, place.FeetY, precision: 3);
            Assert.True(place.FeetY > hall.Button.Top, $"the painter ({pose}) stands on the play button");
            Assert.True(figure.Right <= hall.Page.View.UsableRight - 0.01f, $"the painter ({pose}) is cut off at the side: {figure.Right:0.#}");
            Assert.True(figure.Top <= hall.Hanging.PlateBottom - 0.01f, $"the painter ({pose}) reaches the painting's plate");
            Assert.True(figure.Left >= tableRight - 16f, $"the painter ({pose}) stands in front of the workbench: {figure.Left:0.#} against its table's end {tableRight:0.#}");
            Assert.True(figure.Left > hall.GiftButton.Right, $"the painter ({pose}) stands under the gift frame");
            Assert.True(figure.Height >= 60f, $"the painter ({pose}) is only {figure.Height:0.#} points tall");
        }
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void In_the_hall_of_a_restored_room_the_painter_cheers_beside_the_banner_inside_the_screen_and_under_the_plate(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        StandingMeasure cheering = PainterPictures.MeasureOf(PainterPicture.StandingCheering);
        FigurePlace place = hall.CheeringPainter;
        Box figure = place.Bounds(cheering);

        Assert.Equal(hall.PainterBesideTheBench(PainterPicture.StandingWithPalette).PointsPerMetre, place.PointsPerMetre, precision: 4);
        Assert.True(figure.Right <= hall.Page.View.UsableRight - 0.01f, $"the cheering painter is cut off at the side: {figure.Right:0.#}");
        Assert.True(figure.Top <= hall.Hanging.PlateBottom - 0.01f, "the cheering painter's hands reach the painting's plate");
        Assert.True(place.FeetY > hall.Button.Top, "the cheering painter stands on the button");
        Assert.True(hall.Banner.Right <= place.LowBounds(cheering).Left - 0.01f, $"the banner ({hall.Banner.Right:0.#}) runs into the painter ({place.LowBounds(cheering).Left:0.#})");
        Assert.True(hall.Banner.Left >= hall.Page.View.UsableLeft, "the banner is cut off at the left");

        // It leaves the middle only as far as he needs: never into a wing's paintings on a wide screen.
        bool isInTheMiddle = Math.Abs(hall.Banner.MiddleX) < 0.01f;
        bool isAgainstThePainter = hall.Banner.Right >= place.LowBounds(cheering).Left - 6.5f;
        Assert.True(isInTheMiddle || isAgainstThePainter, $"the banner stands at {hall.Banner.MiddleX:0.#}, neither in the middle nor beside the painter");
        if (hall.Page.View.Width >= 600f)
        {
            Assert.True(isInTheMiddle, $"on a screen {hall.Page.View.Width:0} points wide the banner stands at {hall.Banner.MiddleX:0.#}, out of the middle");
        }
    }

    public static TheoryData<int, int, int, int, float> EveryJigsawScreen()
    {
        var cases = new TheoryData<int, int, int, int, float>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            foreach (float aspect in new[] { 1.69f, 1.25f, 0.81f, 0.6f })
            {
                cases.Add(width, height, top, bottom, aspect);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryJigsawScreen))]
    public void When_a_painting_is_whole_the_painter_cheers_in_front_of_the_trays_right_end_and_leaves_its_well_to_the_reward(int width, int height, int top, int bottom, float aspect)
    {
        PaintingOrientation orientation = aspect >= 1f ? PaintingOrientation.Landscape : PaintingOrientation.Portrait;
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height, top, bottom), aspect, JigsawSize.Ten, orientation);
        StandingMeasure cheering = PainterPictures.MeasureOf(PainterPicture.StandingCheering);
        FigurePlace place = jigsaw.CheeringPainter;
        Box figure = place.Bounds(cheering);

        Assert.True(figure.Left >= jigsaw.Tray.Left && figure.Right <= jigsaw.Tray.Right, $"the painter ({figure.Left:0.#}..{figure.Right:0.#}) stands out of the tray ({jigsaw.Tray.Left:0.#}..{jigsaw.Tray.Right:0.#})");
        Assert.True(place.FeetY >= jigsaw.Tray.Bottom && place.FeetY < jigsaw.Well.Bottom, "the painter's feet are not on the tray's lower rim");
        Assert.True(figure.Top <= jigsaw.Hint.Bottom - 0.01f, "the painter's hands reach the label over the tray");
        Assert.True(figure.Height >= 75f, $"the painter is only {figure.Height:0.#} points tall");
        Assert.True(jigsaw.WellBesideThePainter.Right <= place.LowBounds(cheering).Left, "the reward's room runs into the painter");
        Assert.True(jigsaw.WellBesideThePainter.Width >= 150f, $"the reward has only {jigsaw.WellBesideThePainter.Width:0.#} points beside the painter");
    }

    private static string Sha256Of(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    /// <summary>The pictures art/avatar/licences.json records, by the name the game carries each under: its source's name there, and its bytes' hash.</summary>
    private static IReadOnlyDictionary<string, (string Source, string Sha256)> RecordedPictures()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("art/avatar/licences.json")));
        return record.RootElement.GetProperty("pictures").EnumerateArray().ToDictionary(
            picture => picture.GetProperty("inGame").GetString()!,
            picture => (picture.GetProperty("file").GetString()!, picture.GetProperty("sha256").GetString()!));
    }
}
