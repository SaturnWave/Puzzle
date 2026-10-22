using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the world map's page shows (#4, MapWorldTR): where its sheet hangs on every screen, what its camera frames,
/// how an era's pins stand, how its strip of eras scrolls, what it shows of a player's progress, and its words.
/// </summary>
public class WorldMapViewTests
{
    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
        (720, 1280, 0, 0), (1200, 1920, 0, 0), (2076, 2152, 0, 0), (2076, 2152, 80, 60), (1600, 2560, 0, 0),
    };

    public static TheoryData<int, int, int, int> EveryScreen()
    {
        var cases = new TheoryData<int, int, int, int>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            cases.Add(width, height, top, bottom);
        }

        return cases;
    }

    /// <summary>The least the camera has over the strip on the shortest supported screen (a 9:16 phone with its bars): an era's pins and the top of its card.</summary>
    private const float ShortestMap = 220f;

    private static PlayerProgress Player(int levelsWon, int piecesPlaced) =>
        new PlayerProgress(levelsWon, attemptsOnLevel: 0).WithWaitingPiecesPlaced(piecesPlaced);

    /// <summary>A player on a level with every piece won before it in place.</summary>
    private static MuseumVisit StandingOn(int level)
    {
        PlayerProgress won = new PlayerProgress(level - 1, attemptsOnLevel: 0);
        PlayerProgress placed = won.WithWaitingPiecesPlaced(won.Museum.PiecesWaiting);
        return new MuseumVisit(placed.Museum, TestMuseums.Full, level);
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void The_map_hangs_between_the_rail_and_the_bar_and_leaves_the_camera_room_over_the_strip(int width, int height, int top, int bottom)
    {
        MapLayout map = MapLayout.Of(new ScreenShape(width, height, top, bottom));
        PageLayout page = map.Page;

        Assert.True(map.Frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(map.Frame.Bottom > page.BarReach, "the frame runs into the bar");
        Assert.True(map.Frame.Left >= page.View.Left && map.Frame.Right <= page.View.Right, "the frame is cut off at a side");
        Assert.True(map.MapHeight >= ShortestMap, $"the camera has {map.MapHeight} points over the strip");
        if (map.MapHeight < MapLayout.LeastMapHeight)
        {
            Assert.True(map.Frame.Bottom - page.BarReach < 7f, "a short map did not narrow the wall under its frame");
        }
        Assert.Equal(map.Sheet.Bottom, map.Strip.Bottom);
        Assert.Equal((map.Sheet.Left + 10f, map.Sheet.Top - 20f), map.PageOf(10f, 20f));
        Assert.Equal((10f, 20f), map.SheetOf(map.Sheet.Left + 10f, map.Sheet.Top - 20f));
    }

    [Fact]
    public void A_tall_phone_gives_the_map_more_height_and_the_open_foldable_more_width()
    {
        MapLayout tall = MapLayout.Of(new ScreenShape(1080, 2424));
        MapLayout square = MapLayout.Of(new ScreenShape(2076, 2152));

        Assert.True(tall.MapHeight > square.MapHeight + 100f);
        Assert.True(square.MapWidth > tall.MapWidth);
        Assert.Equal(MapLayout.WidestFrame - 2 * MapLayout.FrameWidth, square.MapWidth, 0);
    }

    [Theory]
    [InlineData(316, 378)]
    [InlineData(338, 290)]
    [InlineData(316, 520)]
    public void The_world_view_frames_California_to_Japan_across_the_map(float width, float height)
    {
        MapFrame world = MapCamera.OfTheWorld(width, height);
        (float west, _) = world.SheetOf(NaturalEarthProjection.Project(-127, 40));
        (float east, _) = world.SheetOf(NaturalEarthProjection.Project(147, 40));
        (_, float north) = world.SheetOf(NaturalEarthProjection.Project(0, 74));
        (_, float south) = world.SheetOf(NaturalEarthProjection.Project(0, -44));

        Assert.InRange(west, 0f, 10f);
        Assert.InRange(east, width - 10f, width);
        Assert.True(north >= 0f && south <= height, $"the world runs from {north} to {south} on a map {height} high");
    }

    [Fact]
    public void The_design_frames_the_world_at_its_own_scale()
    {
        // logic.js: 306 points for the 274 degrees of longitude from California to Japan at 40 degrees north.
        MapFrame world = MapCamera.OfTheWorld(316, 378);
        float span = NaturalEarthProjection.Project(147, 40).X - NaturalEarthProjection.Project(-127, 40).X;

        Assert.Equal(306f / span, world.Scale, 4);
    }

    [Fact]
    public void An_era_is_framed_as_close_as_its_room_allows_and_never_closer_than_the_most_zoom()
    {
        MapPoint[] spread = { new(400, 100), new(460, 110), new(430, 140), new(420, 90), new(450, 120) };
        MapFrame frame = MapCamera.OfAnEra(spread, 316, 378);
        Assert.Equal(Math.Min((316 - 120) / 60f, (378 - 170) / 50f), frame.Scale, 4);
        foreach (MapPoint place in spread)
        {
            (float x, float y) = frame.SheetOf(place);
            Assert.InRange(x, 0f, 316f);
            Assert.InRange(y, 0f, 378f);
        }

        MapPoint[] oneTown = { new(500, 100), new(500.01f, 100), new(500, 100.01f), new(500.02f, 100.02f), new(500, 100) };
        Assert.Equal(MapCamera.MostZoom, MapCamera.OfAnEra(oneTown, 600, 520).Scale);
        Assert.Equal((316 - 120) / MapCamera.LeastSpanAcross, MapCamera.OfAnEra(oneTown, 316, 378).Scale, 4);
        Assert.Throws<ArgumentException>(() => MapCamera.OfAnEra(Array.Empty<MapPoint>(), 316, 378));
    }

    [Fact]
    public void The_glide_leaves_quickly_settles_slowly_and_ends_where_it_was_sent()
    {
        CubicBezier ease = CubicBezier.MapGlide;
        Assert.Equal(0f, ease.At(0f));
        Assert.Equal(1f, ease.At(1f));
        Assert.True(ease.At(0.25f) > 0.6f, "the glide leaves quickly");
        float before = 0f;
        for (int step = 1; step <= 100; step++)
        {
            float now = ease.At(step / 100f);
            Assert.True(now >= before - 1e-5f, "the glide goes back");
            before = now;
        }

        var from = new MapFrame(0f, 0f, 1f);
        var to = new MapFrame(-100f, -50f, 3f);
        var glide = new CameraGlide(from, to, beginsAt: 2f, duration: 0.85f);
        Assert.Equal(from, glide.At(2f));
        Assert.Equal(to, glide.At(2.85f));
        Assert.True(glide.IsOverAt(2.85f) && !glide.IsOverAt(2.5f));
        Assert.InRange(glide.At(2.4f).Scale, 1f, 3f);
    }

    [Fact]
    public void Pins_whose_places_lie_close_stand_in_one_row_over_them_each_with_a_line_to_its_place()
    {
        IReadOnlyList<PinPlace> pins = PinRows.Of(new[] { (0, 150f, 200f), (1, 160f, 205f), (2, 155f, 198f), (3, 40f, 300f) }, 316f, 378f);

        PinPlace[] row = pins.Where(pin => pin.Painting != 3).OrderBy(pin => pin.TipX).ToArray();
        Assert.All(row, pin => Assert.Equal(row[0].TipY, pin.TipY));
        Assert.True(row[0].TipY < 198f, "the row stands over its places");
        Assert.Equal(PinRows.Gap, row[1].TipX - row[0].TipX, 2);
        Assert.Equal(PinRows.Gap, row[2].TipX - row[1].TipX, 2);
        Assert.All(row, pin => Assert.True(pin.HasALeader));
        Assert.Equal(new[] { 0, 2, 1 }, row.Select(pin => pin.Painting));

        PinPlace alone = pins.Single(pin => pin.Painting == 3);
        // A pin alone stands a little over its place, its pointer joined to the place's dot by a short line straight down.
        Assert.Equal((40f, 294f), (alone.TipX, alone.TipY));
        Assert.True(alone.HasALeader);
    }

    [Fact]
    public void No_pin_leaves_the_map_or_stands_under_the_buttons_and_none_overlaps_another()
    {
        var places = new[] { (0, 310f, 20f), (1, 5f, 370f), (2, 300f, 300f), (3, 290f, 305f), (4, 200f, 60f) };
        IReadOnlyList<PinPlace> pins = PinRows.Of(places, 316f, 378f);

        foreach (PinPlace pin in pins)
        {
            Assert.InRange(pin.TipX, PinRows.PinWidth / 2f, 316f - PinRows.PinWidth / 2f);
            Assert.InRange(pin.TipY, PinRows.PinHeight, 378f);
            if (pin.TipY - PinRows.PinHeight < PinRows.ButtonsFoot)
            {
                Assert.True(pin.TipX + PinRows.PinWidth / 2f <= 316f - PinRows.ButtonsColumn + 0.01f, $"pin {pin.Painting} stands under the buttons");
            }
        }

        for (int i = 0; i < pins.Count; i++)
        {
            for (int j = i + 1; j < pins.Count; j++)
            {
                bool apart = Math.Abs(pins[i].TipX - pins[j].TipX) >= PinRows.Gap - 0.01f || Math.Abs(pins[i].TipY - pins[j].TipY) >= PinRows.PinHeight - 6f;
                Assert.True(apart, $"pins {pins[i].Painting} and {pins[j].Painting} overlap");
            }
        }
    }

    [Fact]
    public void The_strip_shows_an_era_a_little_in_from_the_left_and_within_its_ends()
    {
        var strip = new EraStripScroll(MuseumPlan.RoomCount, 316f);

        Assert.Equal(8f + 20 * 58f + 19 * 4f + 8f, strip.ContentWidth);
        Assert.Equal(0f, strip.OffsetShowing(1));
        Assert.Equal(5 * 62f - 120f, strip.OffsetShowing(6));
        Assert.Equal(strip.Furthest, strip.OffsetShowing(20));

        strip.ScrollTo(6, now: 1f, duration: 0.3f);
        Assert.Equal(0f, strip.At(1f));
        Assert.Equal(190f, strip.At(1.3f));
        Assert.True(strip.IsMovingAt(1.1f) && !strip.IsMovingAt(1.3f));
    }

    [Fact]
    public void A_finger_drags_the_strip_gives_a_little_past_its_end_and_a_flick_runs_on_within_it()
    {
        var strip = new EraStripScroll(MuseumPlan.RoomCount, 316f);
        strip.Hold(fingerX: 100f, now: 0f);
        strip.DragTo(200f, 0.1f);
        Assert.InRange(strip.Offset, -40f, -1f);
        strip.LetGo(0.12f, 0.3f);
        Assert.Equal(0f, strip.At(0.5f));

        strip.Hold(200f, 1f);
        strip.DragTo(150f, 1.05f);
        strip.DragTo(100f, 1.1f);
        float letGoAt = strip.Offset;
        strip.LetGo(1.11f, 0.3f);
        Assert.True(strip.At(2f) > letGoAt, "a flick to the left runs on to the left");
        Assert.InRange(strip.At(2f), 0f, strip.Furthest);

        strip.Hold(100f, 3f);
        strip.DragTo(-2000f, 3.05f);
        strip.Forget();
        Assert.Equal(strip.Furthest, strip.Offset);
    }

    [Fact]
    public void A_player_on_level_287_restores_the_fourth_painting_of_the_sixth_era()
    {
        MapStanding standing = MapStanding.Of(StandingOn(287));

        Assert.Equal(new PaintingNumber(29), standing.Here);
        Assert.Equal(new RoomNumber(6), standing.Room);
        Assert.Equal(MapMark.Restored, standing.MarkOf(new PaintingNumber(28)));
        Assert.Equal(MapMark.Here, standing.MarkOf(new PaintingNumber(29)));
        Assert.Equal(MapMark.Ahead, standing.MarkOf(new PaintingNumber(30)));
        Assert.Equal(MapMark.Restored, standing.MarkOf(new RoomNumber(5)));
        Assert.Equal(MapMark.Here, standing.MarkOf(new RoomNumber(6)));
        Assert.Equal(MapMark.Ahead, standing.MarkOf(new RoomNumber(7)));
        Assert.Equal(3, standing.RestoredIn(new RoomNumber(6)));

        // The seventh level of a painting of twenty pieces gives its 13th and 14th: the badge counts them as the board does.
        Assert.Equal((14, 20), standing.PiecesHere);
    }

    [Fact]
    public void A_player_who_played_on_without_placing_has_the_unfinished_painting_counted_by_what_they_hold()
    {
        PlayerProgress won = Player(levelsWon: 25, piecesPlaced: 12);
        var visit = new MuseumVisit(won.Museum, TestMuseums.Full, levelToPlay: 26);
        MapStanding standing = MapStanding.Of(visit);

        Assert.Equal(new PaintingNumber(2), standing.Here);
        Assert.Equal((10, 10), standing.PiecesHere);
        Assert.Equal(MapMark.Ahead, standing.MarkOf(new PaintingNumber(3)));
    }

    [Fact]
    public void The_map_says_its_plaque_card_and_pins_in_the_designs_words()
    {
        MuseumCatalogue catalogue = TestMuseums.Full;
        Assert.Equal("Dünya Haritası", MapWords.WorldTitle);
        Assert.Equal("20 DÖNEM · 100 TABLO", MapWords.WorldSummary);
        Assert.Equal("BÖLÜM 301'DE AÇILIR", MapWords.EraOpensAt(new RoomNumber(7)));
        Assert.Equal("Bölüm 261–270'te açılır", MapWords.StatusOpensAt(new PaintingNumber(27)));
        Assert.Equal("Parça 14 / 20 · Bölüm 287", MapWords.StatusPieces(14, 20, 287));
        Assert.Equal("ODA 6 · TABLO 4", MapWords.CardEyebrow(new PaintingNumber(29)));
        Assert.Equal("Bugün: Louvre", MapWords.Today("Louvre"));
        Assert.Equal("7/10", MapWords.OfAll(7, 10));
        Assert.Equal("1867–1906", MapWords.Years((1867, 1906)));
        Assert.Equal("1867", MapWords.Years((1867, 1867)));
        Assert.Equal("Monet, Giverny. Onarıldı", MapWords.PinLabel("Monet", "Giverny", MapWords.StatusRestored));
        Assert.StartsWith("ODA 1 / 20", MapWords.EraLabel(catalogue.Room(new RoomNumber(1))));
    }
}
