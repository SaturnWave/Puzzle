using Puzzle.Client;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the Unity game draws over what (<c>DrawOrder</c>, the Unity project's source file, compiled into these
/// tests: it has no Unity dependency). Everything is flat and drawn by one camera, so the list decides every
/// overlap; pinned here are the places a won game's finale takes in it, which a mistake has shown to matter.
/// </summary>
public class DrawOrderTests
{
    private static readonly DrawOrder[] Places = Enum.GetValues<DrawOrder>();

    [Fact]
    public void No_two_things_have_one_place_in_the_draw_order()
    {
        // Two sprites at one place are drawn in whatever order the device takes them.
        Assert.Equal(Places.Length, Places.Select(place => (int)place).Distinct().Count());
    }

    [Fact]
    public void The_light_behind_a_plaque_lies_on_the_wall_under_everything_that_hangs_or_stands_in_front_of_it()
    {
        // On a screen where the plaques hang close over the board, light drawn over the frame lay on the board's first row.
        Assert.True(DrawOrder.PlaqueHalo > DrawOrder.WallLight);
        Assert.All(
            new[] { DrawOrder.Sconce, DrawOrder.Wire, DrawOrder.FrameShadow, DrawOrder.Floor, DrawOrder.Frame, DrawOrder.Piece, DrawOrder.Effect, DrawOrder.Shelf, DrawOrder.Plaque },
            inFront => Assert.True(DrawOrder.PlaqueHalo < inFront, $"the light behind a plaque is drawn over {inFront}"));
    }

    [Fact]
    public void Every_picture_a_thrown_move_is_made_of_has_a_place_of_its_own_over_the_plaques_and_under_the_dark()
    {
        // All of them at one place made the same move a crisp star in one game and a blot of light in the next.
        ThrownMovePart[] parts = Enum.GetValues<ThrownMovePart>();
        int[] places = parts.Select(part => (int)DrawOrder.ThrownMove + (int)part).ToArray();

        Assert.Equal(new[] { ThrownMovePart.Tail, ThrownMovePart.GiltLight, ThrownMovePart.PaleLight, ThrownMovePart.Star, ThrownMovePart.Core }, parts);
        Assert.Equal(places.OrderBy(place => place), places);
        Assert.Equal(places.Length, places.Distinct().Count());
        Assert.Equal((int)DrawOrder.ThrownMove, places[0]);

        // The places above the first are the move's alone: nothing else of the list lies among them.
        Assert.DoesNotContain(Places, place => (int)place > places[0] && (int)place <= places[^1]);
        Assert.All(places, place =>
        {
            Assert.True(place > (int)DrawOrder.PlaquePictureMark, "a thrown move is drawn under a plaque it leaves");
            Assert.True(place > (int)DrawOrder.PlaqueRing, "a thrown move is drawn under the ring of light it comes out of");
            Assert.True(place < (int)DrawOrder.PageButton && place < (int)DrawOrder.Veil, "a thrown move is drawn over what it must lie under");
        });
        Assert.True(DrawOrder.PlaqueRing > DrawOrder.PlaquePictureMark);
    }

    [Theory]
    [InlineData(DrawOrder.LessonPortrait, DrawOrder.LessonPillLettering, DrawOrder.Carried)]
    [InlineData(DrawOrder.CardLessonPortrait, DrawOrder.CardLessonPillLettering, DrawOrder.SecondVeil)]
    [InlineData(DrawOrder.CardPortrait, DrawOrder.CardBadgeLettering, DrawOrder.CardLettering)]
    public void Every_part_of_the_painters_framed_bust_has_a_place_of_its_own_between_what_it_lies_over_and_under(DrawOrder portrait, DrawOrder over, DrawOrder under)
    {
        int[] places = Enum.GetValues<PortraitPart>().Select(part => (int)portrait + (int)part).ToArray();

        Assert.Equal(new[] { PortraitPart.Velvet, PortraitPart.Bust, PortraitPart.Frame }, Enum.GetValues<PortraitPart>());
        Assert.All(places, place => Assert.InRange(place, (int)over + 1, (int)under - 1));
        Assert.DoesNotContain(Places, place => place != portrait && (int)place >= places[0] && (int)place <= places[^1]);
    }

    [Fact]
    public void The_standing_painter_is_drawn_over_the_workbench_and_its_piece_and_over_the_trays_reward_under_its_label()
    {
        Assert.True(DrawOrder.HallPainter > DrawOrder.BenchSparkle && DrawOrder.HallPainter < DrawOrder.Floor);
        Assert.True(DrawOrder.TrayPainter > DrawOrder.TrayRewardLettering && DrawOrder.TrayPainter < DrawOrder.ShelfPill);
    }
}
