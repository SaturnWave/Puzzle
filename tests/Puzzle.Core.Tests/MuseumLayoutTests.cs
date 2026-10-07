using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the Unity game puts the museum's pages on a screen: the hall with its painting, and the map with its floor
/// plan, between the plaque at the top and the navigation bar at the foot. The layouts are the Unity project's
/// source files, compiled into these tests. The screens are the ones the board's layout is tested on: tall phones,
/// the 9:16 phone of the design, the owner's foldable opened, each also with a cutout and a system bar.
/// </summary>
public class MuseumLayoutTests
{
    /// <summary>The proportions of the first room's paintings as the game carries them, and of a blank canvas: width over height.</summary>
    private static readonly float[] PictureAspects = { 1.69f, 1.25f, 0.81f, 4f / 3f, 2.2f, 0.6f };

    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 0, 0), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
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

    [Fact]
    public void A_box_knows_its_edges_and_what_lies_in_it()
    {
        Box box = Box.Around(middleX: 10f, middleY: -5f, width: 40f, height: 20f);

        Assert.Equal((-10f, 30f, -15f, 5f), (box.Left, box.Right, box.Bottom, box.Top));
        Assert.True(box.Contains(-10f, 5f));
        Assert.False(box.Contains(31f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box(0f, 0f, -1f, 1f));
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void A_page_keeps_its_plaque_under_the_cornice_and_its_bar_over_the_screens_foot(int width, int height, int top, int bottom)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, top, bottom));

        Assert.True(page.CorniceBottom < page.View.UsableTop, "the cornice does not show under the cutout");
        Assert.True(page.Plaque.Top < page.CorniceBottom, "the plaque runs into the cornice");
        Assert.True(page.RailTop < page.Plaque.Bottom, "the rail runs into the plaque");
        Assert.True(page.Plaque.Left >= page.View.Left + PageLayout.PlaqueSideMargin - 0.01f, "the plaque is cut off at the side");
        Assert.True(page.BarTop >= page.View.UsableBottom + PageLayout.BarHeight * PageLayout.TightBarScale - 0.01f, "the bar is hidden by the system bar");
        Assert.True(page.BarReach > page.BarTop);
        Assert.InRange(page.Roominess, 0f, 1f);
    }

    [Fact]
    public void A_tall_phone_is_roomy_and_a_wide_screen_is_as_tight_as_the_design_phone()
    {
        Assert.Equal(1f, PageLayout.Of(new ScreenShape(1080, 2424)).Roominess);
        Assert.Equal(0f, PageLayout.Of(new ScreenShape(1080, 1920)).Roominess);
        Assert.Equal(0f, PageLayout.Of(new ScreenShape(2076, 2152)).Roominess);
        Assert.Equal(PageLayout.TightBarScale, PageLayout.Of(new ScreenShape(2076, 2152)).BarScale);
    }

    [Fact]
    public void The_bars_places_stand_as_a_centred_row_inside_the_screen()
    {
        PageLayout page = PageLayout.Of(new ScreenShape(1080, 1920));

        Assert.Equal(new[] { -52f, 52f }, new[] { page.TabMiddle(0, 2), page.TabMiddle(1, 2) });
        Assert.Equal(new[] { -104f, 0f, 104f }, new[] { page.TabMiddle(0, 3), page.TabMiddle(1, 3), page.TabMiddle(2, 3) });
        Assert.Equal(72f, page.TabWidth(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.TabMiddle(2, 2));
    }

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

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void The_hall_stacks_the_painting_the_bench_and_the_button_without_any_two_meeting(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        PageLayout page = hall.Page;
        float onePixel = 1f / page.View.PixelsPerPoint;

        Assert.True(hall.Frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(hall.Frame.Left >= page.View.Left && hall.Frame.Right <= page.View.Right, "the frame is cut off at a side");
        float plateFoot = hall.PlateMiddle - HallLayout.PlateHeight / 2;
        float benchTop = hall.FloorLine + HallLayout.BenchAboveFloorLine * hall.BenchScale;
        Assert.True(plateFoot > benchTop, "the plate under the painting reaches the brushes on the bench");
        Assert.True(hall.BenchFeet > hall.Button.Top, "the bench stands behind the play button");
        Assert.True(hall.Button.Bottom - HallLayout.ButtonLip * hall.ButtonScale > page.BarReach, "the play button reaches the bar");
        Assert.True(hall.Button.Left >= page.View.Left && hall.Button.Right <= page.View.Right, "the play button is cut off at a side");

        // The picture keeps its proportions, to the pixel it is rounded to.
        Assert.InRange(hall.Picture.Width / hall.Picture.Height, aspect * (1 - 2 * onePixel / hall.Picture.Height) - 0.01f, aspect * (1 + 2 * onePixel / hall.Picture.Height) + 0.01f);
        Assert.True(hall.Picture.Width <= HallLayout.WidestPicture + onePixel);
        Assert.True(hall.Picture.Height >= 100f, $"the picture is only {hall.Picture.Height} points high");
        Assert.InRange(hall.BenchScale, 0.45f, 1f);
    }

    [Fact]
    public void A_screen_that_loses_height_to_a_cutout_draws_the_bench_smaller_and_keeps_the_picture()
    {
        HallLayout whole = HallLayout.Of(new ScreenShape(2076, 2152), 1.69f);
        HallLayout withBars = HallLayout.Of(new ScreenShape(2076, 2152, topInsetPixels: 80, bottomInsetPixels: 60), 1.69f);

        Assert.Equal(0.7f, whole.BenchScale, precision: 3);
        Assert.True(withBars.BenchScale < whole.BenchScale);
        Assert.True(withBars.Picture.Height >= 130f, $"the picture is only {withBars.Picture.Height} points high");
    }

    [Fact]
    public void The_hall_has_wall_lamps_where_the_wall_beside_the_painting_is_wide_enough()
    {
        Assert.True(HallLayout.Of(new ScreenShape(2076, 2152), 1.69f).HasLamps);
        Assert.False(HallLayout.Of(new ScreenShape(1080, 2424), 1.69f).HasLamps);
        Assert.False(HallLayout.Of(new ScreenShape(1080, 1920), 1.69f).HasLamps);

        // An upright painting is narrow: on a phone it leaves the wall a lamp needs.
        Assert.True(HallLayout.Of(new ScreenShape(1080, 1920), 0.81f).HasLamps);
        Assert.Throws<ArgumentOutOfRangeException>(() => HallLayout.Of(new ScreenShape(1080, 1920), 0f));
    }

    public static TheoryData<int, int, int, int, int> EveryScreenAndRoom()
    {
        var cases = new TheoryData<int, int, int, int, int>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            foreach (int here in new[] { 1, 2, 3, 13, 18, 19, 20 })
            {
                cases.Add(width, height, top, bottom, here);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndRoom))]
    public void The_map_hangs_between_the_rail_and_the_bar_with_every_row_on_its_sheet(int width, int height, int top, int bottom, int here)
    {
        MapLayout map = MapLayout.Of(new ScreenShape(width, height, top, bottom), new RoomNumber(here));
        PageLayout page = map.Page;

        Assert.True(map.Frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(map.Frame.Bottom > page.BarReach, "the frame runs into the bar");
        Assert.True(map.Frame.Left >= page.View.Left && map.Frame.Right <= page.View.Right, "the frame is cut off at a side");

        float above = map.Sheet.Top;
        foreach (PlanRow row in map.Rows)
        {
            Assert.True(row.Floor.Top <= above - MapLayout.WallThickness + 0.01f, $"the {row.Kind} row runs into what is above it");
            Assert.True(row.Floor.Left >= map.Sheet.Left + MapLayout.SheetSideMargin && row.Floor.Right <= map.Sheet.Right - MapLayout.SheetSideMargin);
            above = row.Floor.Bottom;
        }

        Assert.True(above - MapLayout.WallThickness - MapLayout.SheetFootMargin >= map.Sheet.Bottom - 0.01f, "the entrance does not fit under the plan");
        Assert.Equal(new RoomNumber(here), map.Plan.Here);
        Assert.NotNull(map.FloorOf(PlanRowKind.Here));
    }

    [Fact]
    public void A_tall_phone_shows_more_of_the_museum_room_by_room_than_a_short_one()
    {
        MapLayout tall = MapLayout.Of(new ScreenShape(1080, 2424), new RoomNumber(2));
        MapLayout shorter = MapLayout.Of(new ScreenShape(1080, 1920), new RoomNumber(13));

        Assert.Equal((new RoomNumber(1), 4), (tall.Plan.Before!.Value, tall.Plan.Ahead.Count));
        Assert.True(shorter.Plan.Ahead.Count < 4 || shorter.Plan.Before == null);
        Assert.NotNull(shorter.Plan.Behind);
        Assert.NotNull(shorter.Plan.Beyond);
    }

    [Fact]
    public void The_rooms_ahead_are_walked_as_a_visitor_would_and_a_last_room_alone_has_its_whole_row()
    {
        MapLayout map = MapLayout.Of(new ScreenShape(1080, 2424), new RoomNumber(1));
        Assert.Equal(4, map.Plan.Ahead.Count);

        Box third = map.FloorOfRoomAhead(0);
        Box fourth = map.FloorOfRoomAhead(1);
        Box fifth = map.FloorOfRoomAhead(2);
        Box sixth = map.FloorOfRoomAhead(3);
        Assert.True(third.Right < fourth.Left, "the first row runs from left to right");
        Assert.True(sixth.Right < fifth.Left, "the row above it runs back from right to left");
        Assert.True(fifth.Bottom > third.Top, "the second row lies above the first");
        Assert.Equal(fourth.Left, fifth.Left);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.FloorOfRoomAhead(4));

        // In room 17 the museum has three rooms left: the next one, and two ahead in one row; in room 18 one ahead, alone.
        MapLayout nearTheEnd = MapLayout.Of(new ScreenShape(1080, 2424), new RoomNumber(18));
        Assert.Equal(new[] { 20 }, nearTheEnd.Plan.Ahead.Select(room => room.Value));
        Assert.Equal(nearTheEnd.FloorOf(PlanRowKind.Here)!.Value.Width, nearTheEnd.FloorOfRoomAhead(0).Width);
    }
}
