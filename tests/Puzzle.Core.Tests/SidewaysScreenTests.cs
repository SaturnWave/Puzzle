using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game on a screen held sideways, as the owner decided on 2026-10-24: which devices turn, the strips a camera
/// takes at a screen's sides, the hall's restored paintings on the wall beside its frame, the map's wide frame, and
/// what keeps clear of a camera at a side. The screens held sideways are the 12.9" iPad (2732 x 2048) and the 11"
/// one (2360 x 1640) with the home indicator (20 of their points) and the status bar (24), the iPad mini (2266 x
/// 1488), a 16:10 Android tablet (2560 x 1600), and the open foldable (2152 x 2076), which is nearly square and keeps
/// the upright pages. <c>ScreenClass</c>, <c>ScreenLayout</c>, <c>HallLayout</c>, <c>MapLayout</c>, <c>JigsawLayout</c>
/// and <c>EntryLayout</c> are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class SidewaysScreenTests
{
    /// <summary>Four restored paintings of the first room, as the game carries their proportions.</summary>
    private static readonly float[] RestoredAspects = { 1.25f, 0.81f, 1.29f, 1.04f };

    public static TheoryData<string> TabletsHeldSideways() => new()
    {
        "2732x2048+0+40", "2732x2048+48+40", "2360x1640+0+40", "2266x1488+0+40", "2560x1600", "2560x1600+0+0+80+0", "2732x2048+0+40+0+90",
    };

    public static TheoryData<string> ScreensWithoutPaintingsBeside() => new()
    {
        "2152x2076", "2152x2076+0+0+90+0", "2076x2152", "2048x2732+48+40", "1640x2360+48+40", "1080x2424", "1080x1920", "720x1280",
    };

    public static TheoryData<string> ScreensWithACameraAtASide() => new()
    {
        "2152x2076+0+0+90+0", "2152x2076+0+0+0+90", "2560x1600+0+0+80+0", "2560x1600+0+0+0+80", "2732x2048+0+40+0+120",
    };

    [Theory]
    [InlineData(320f, ScreenClass.Phone)]
    [InlineData(411f, ScreenClass.Phone)] // the Pixel Fold's cover display
    [InlineData(599.5f, ScreenClass.Phone)]
    [InlineData(600f, ScreenClass.Tablet)]
    [InlineData(673f, ScreenClass.Tablet)] // the Pixel Fold opened
    [InlineData(800f, ScreenClass.Tablet)] // a 10" tablet
    public void An_android_screen_turns_from_600_dp_on_its_shorter_side_on(float smallestSideDp, ScreenClass expected)
    {
        Assert.Equal(expected, ScreenTurns.OfAnAndroidScreen(smallestSideDp));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-411f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_side_that_is_no_length_is_refused(float smallestSideDp)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScreenTurns.OfAnAndroidScreen(smallestSideDp));
    }

    [Theory]
    [InlineData("iPad13,8", ScreenClass.Tablet)] // the owner's 12.9" iPad Pro
    [InlineData("iPad14,1", ScreenClass.Tablet)]
    [InlineData("iPhone15,2", ScreenClass.Phone)]
    [InlineData("iPod9,1", ScreenClass.Phone)]
    [InlineData("", ScreenClass.Phone)]
    public void An_ipad_turns_and_an_iphone_does_not(string model, ScreenClass expected)
    {
        Assert.Equal(expected, ScreenTurns.OfAnAppleDevice(model));
    }

    [Fact]
    public void A_phone_stands_upright_only_and_a_tablet_every_way_upside_down_too()
    {
        Assert.Equal(new[] { ScreenTurn.Portrait }, ScreenTurns.AllowedFor(ScreenClass.Phone));
        Assert.Equal(Enum.GetValues<ScreenTurn>(), ScreenTurns.AllowedFor(ScreenClass.Tablet).OrderBy(turn => turn));
    }

    [Theory]
    [InlineData("1080x2340")]
    [InlineData("1080x2340+96+48")]
    [InlineData("2152x2076+0+0+90+0")]
    [InlineData("2732x2048+0+40+0+120")]
    public void A_screen_is_written_as_the_picture_commands_take_it_and_read_back(string written)
    {
        ScreenShape screen = ScreenShape.Parse(written);

        Assert.Equal(written, screen.ToString());
        Assert.Equal(screen, ScreenShape.Parse(screen.ToString()));
    }

    [Fact]
    public void A_screen_without_side_strips_is_written_as_it_always_was()
    {
        Assert.Equal("1080x2340+96+48", new ScreenShape(1080, 2340, 96, 48).ToString());
        Assert.Equal("1080x2340", new ScreenShape(1080, 2340).ToString());
        Assert.NotEqual(new ScreenShape(2152, 2076, 0, 0, 90, 0), new ScreenShape(2152, 2076, 0, 0, 0, 90));
    }

    [Theory]
    [InlineData("1080")]
    [InlineData("1080x")]
    [InlineData("x2340")]
    [InlineData("1080x2340+96")]
    [InlineData("1080x2340+96+48+90")]
    [InlineData("1080X2340")]
    public void Text_that_is_no_screen_is_refused(string text)
    {
        Assert.Throws<FormatException>(() => ScreenShape.Parse(text));
    }

    [Fact]
    public void Side_strips_that_leave_nothing_of_the_screen_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenShape(2152, 2076, 0, 0, 1100, 1100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenShape(2152, 2076, 0, 0, -1, 0));
    }

    [Fact]
    public void Side_strips_narrow_the_usable_screen_and_leave_its_size_alone()
    {
        ScreenView bare = ScreenView.Of(new ScreenShape(2152, 2076));
        ScreenView withACamera = ScreenView.Of(new ScreenShape(2152, 2076, 0, 0, 90, 30));

        Assert.Equal(bare.Left, bare.UsableLeft);
        Assert.Equal(bare.Right, bare.UsableRight);
        Assert.Equal(bare.Width, withACamera.Width);
        Assert.Equal(bare.PixelsPerPoint, withACamera.PixelsPerPoint);
        Assert.Equal(withACamera.Left + 90 / withACamera.PixelsPerPoint, withACamera.UsableLeft, precision: 3);
        Assert.Equal(withACamera.Right - 30 / withACamera.PixelsPerPoint, withACamera.UsableRight, precision: 3);
    }

    [Theory]
    [MemberData(nameof(TabletsHeldSideways))]
    public void On_a_tablet_held_sideways_the_hall_hangs_the_rooms_restored_paintings_beside_its_frame(string written)
    {
        HallLayout hall = HallLayout.Of(ScreenShape.Parse(written), 1.25f, hasJigsawButton: true);
        ScreenView view = hall.Page.View;
        IReadOnlyList<ExhibitPlace> beside = hall.PaintingsBeside(RestoredAspects);

        Assert.True(hall.HasPaintingsBeside);
        Assert.True(hall.HasLamps);
        Assert.Equal(RestoredAspects.Length, beside.Count);
        Assert.InRange(hall.LampMiddleX, hall.Frame.Right + 20f, beside.Where(place => place.Frame.MiddleX > 0).Min(place => place.Touch.Left) - 20f);
        for (int painting = 0; painting < beside.Count; painting++)
        {
            ExhibitPlace place = beside[painting];
            bool isAtTheLeft = painting % 2 == 0;
            Assert.Equal(isAtTheLeft, place.Frame.MiddleX < 0);
            Assert.True(place.Touch.Left >= view.UsableLeft && place.Touch.Right <= view.UsableRight, $"painting {painting} leaves the usable screen at {place.Touch}");
            Assert.True(place.Frame.Top < hall.Page.RailBottom, $"painting {painting} runs into the picture rail");
            Assert.True(place.Touch.Bottom > hall.FloorLine + HallLayout.SkirtingHeight, $"painting {painting} runs into the skirting board");
            Assert.True(isAtTheLeft ? place.Touch.Right < -hall.LampMiddleX : place.Touch.Left > hall.LampMiddleX, $"painting {painting} hangs on its lamp");
            Assert.True(place.Picture.Height >= 60f, $"painting {painting} is only {place.Picture.Height} points high");
            Assert.InRange(place.Picture.Width / place.Picture.Height, RestoredAspects[painting] - 0.05f, RestoredAspects[painting] + 0.05f);
            Assert.Equal(painting < 2, place.HangsFromTheRail);
            Assert.False(Meet(place.Touch, hall.GiftButton), $"painting {painting} hangs on the gift frame");

            for (int other = painting + 1; other < beside.Count; other++)
            {
                Assert.False(Meet(place.Touch, beside[other].Touch), $"paintings {painting} and {other} meet");
            }
        }
    }

    [Fact]
    public void A_room_with_more_restored_paintings_than_places_hangs_four_of_them()
    {
        HallLayout hall = HallLayout.Of(ScreenShape.Parse("2732x2048+0+40"), 1.25f);

        Assert.Equal(HallLayout.MostPaintingsBeside, hall.PaintingsBeside(new[] { 1.25f, 0.81f, 1.29f, 1.04f, 1.69f }).Count);
        Assert.Single(hall.PaintingsBeside(new[] { 0.81f }));
        Assert.Empty(hall.PaintingsBeside(Array.Empty<float>()));
        Assert.Throws<ArgumentException>(() => hall.PaintingsBeside(new[] { 0f }));
    }

    [Theory]
    [MemberData(nameof(ScreensWithoutPaintingsBeside))]
    public void The_open_foldable_and_every_upright_screen_hang_no_paintings_beside_the_frame_and_keep_the_lamp_midway(string written)
    {
        HallLayout hall = HallLayout.Of(ScreenShape.Parse(written), 1.25f);

        Assert.False(hall.HasPaintingsBeside);
        Assert.Empty(hall.PaintingsBeside(RestoredAspects));
        Assert.Equal(hall.Frame.Right + hall.WallBesideTheFrame / 2, hall.LampMiddleX);
    }

    [Theory]
    [MemberData(nameof(TabletsHeldSideways))]
    public void Held_sideways_the_maps_frame_widens_towards_the_shape_of_the_world_and_keeps_wall_at_the_sides(string written)
    {
        MapLayout map = MapLayout.Of(ScreenShape.Parse(written));
        ScreenView view = map.Page.View;

        Assert.True(map.Page.IsSideways);
        Assert.True(map.Frame.Width > MapLayout.WidestFrame, $"the frame is {map.Frame.Width} points wide");
        Assert.True(map.Frame.Left >= view.UsableLeft + 20f && map.Frame.Right <= view.UsableRight - 20f, $"the frame at {map.Frame} keeps no wall at a side");
        Assert.True(map.MapWidth <= MapLayout.WorldShape * map.MapHeight + 1f, $"the map is {map.MapWidth} wide over {map.MapHeight}");
    }

    [Theory]
    [MemberData(nameof(ScreensWithoutPaintingsBeside))]
    public void An_upright_screen_and_the_open_foldable_keep_the_maps_frame(string written)
    {
        MapLayout map = MapLayout.Of(ScreenShape.Parse(written));

        Assert.False(map.Page.IsSideways);
        Assert.True(map.Frame.Width <= MapLayout.WidestFrame + 0.01f, $"the frame is {map.Frame.Width} points wide");
    }

    [Theory]
    [MemberData(nameof(ScreensWithACameraAtASide))]
    public void A_camera_at_a_side_of_a_screen_held_sideways_covers_no_button_and_no_painting(string written)
    {
        ScreenShape screen = ScreenShape.Parse(written);
        ScreenView view = ScreenView.Of(screen);
        bool IsUsable(Box box) => box.Left >= view.UsableLeft && box.Right <= view.UsableRight;

        JigsawLayout jigsaw = JigsawLayout.Of(screen, 0.81f, Puzzle.Museum.JigsawSize.Forty, Puzzle.Museum.PaintingOrientation.Portrait);
        Assert.True(IsUsable(jigsaw.Back), $"the jigsaw's back button at {jigsaw.Back} is under the camera");
        Assert.True(IsUsable(jigsaw.Hanging.Frame) && IsUsable(jigsaw.Tray) && IsUsable(jigsaw.Button), "a part of the jigsaw is under the camera");

        HallLayout hall = HallLayout.Of(screen, 1.69f, hasJigsawButton: true);
        Assert.True(IsUsable(hall.GiftButton) && IsUsable(hall.Frame) && IsUsable(hall.Button), "a part of the hall is under the camera");
        Assert.All(hall.PaintingsBeside(RestoredAspects), place => Assert.True(IsUsable(place.Touch), $"a painting beside the frame at {place.Touch} is under the camera"));

        MapLayout map = MapLayout.Of(screen);
        Assert.True(IsUsable(map.Frame), $"the map's frame at {map.Frame} is under the camera");

        EntryLayout entry = EntryLayout.Of(screen);
        Assert.True(entry.FooterLeft >= view.UsableLeft && entry.FooterRight <= view.UsableRight, "the entry page's footer is under the camera");
    }

    [Theory]
    [InlineData("2732x2048", true)]
    [InlineData("2360x1640", true)]
    [InlineData("2560x1600", true)]
    [InlineData("2152x2076", false)]
    [InlineData("2076x2152", false)]
    [InlineData("2048x2732", false)]
    [InlineData("1080x2424", false)]
    public void A_screen_counts_as_held_sideways_from_five_to_four_on(string written, bool isSideways)
    {
        Assert.Equal(isSideways, PageLayout.Of(ScreenShape.Parse(written)).IsSideways);
    }

    private static bool Meet(Box one, Box other) =>
        one.Left < other.Right && other.Left < one.Right && one.Bottom < other.Top && other.Bottom < one.Top;
}
