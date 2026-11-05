using Puzzle.Core.Levels;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// The museum's fixed structure as docs/GAME_DESIGN.md section 18 decides it: which levels belong to which painting
/// and room, when a room opens, and how finely the paintings of each stretch of levels are cut.
/// </summary>
public class MuseumPlanTests
{
    private static IEnumerable<LevelId> EveryLevel => Enumerable.Range(1, MuseumPlan.LevelCount).Select(number => new LevelId(number));

    [Fact]
    public void The_museum_is_a_thousand_levels_in_a_hundred_paintings_in_twenty_rooms()
    {
        Assert.Equal(1000, MuseumPlan.LevelCount);
        Assert.Equal(100, PaintingNumber.All.Count());
        Assert.Equal(20, RoomNumber.All.Count());
        Assert.All(RoomNumber.All, room => Assert.Equal(5, room.Paintings.Count()));
    }

    [Fact]
    public void Every_level_gives_pieces_of_exactly_one_painting_which_spans_ten_levels()
    {
        Assert.All(EveryLevel, level =>
        {
            PaintingNumber painting = PaintingNumber.OfLevel(level);
            Assert.InRange(level.Value, painting.FirstLevel.Value, painting.LastLevel.Value);
            Assert.Equal(9, painting.LastLevel.Value - painting.FirstLevel.Value);
        });
        Assert.Equal(Enumerable.Range(1, 100), EveryLevel.Select(level => PaintingNumber.OfLevel(level).Value).Distinct());
    }

    [Fact]
    public void Every_painting_hangs_in_the_room_whose_fifty_levels_hold_its_ten()
    {
        Assert.All(PaintingNumber.All, painting =>
        {
            RoomNumber room = painting.Room;
            Assert.Contains(painting, room.Paintings);
            Assert.InRange(painting.FirstLevel.Value, room.FirstLevel.Value, room.LastLevel.Value);
            Assert.InRange(painting.LastLevel.Value, room.FirstLevel.Value, room.LastLevel.Value);
            Assert.Equal(painting, room.Paintings.ElementAt(painting.PlaceInRoom - 1));
        });
    }

    [Fact]
    public void The_design_canvas_places_are_the_plans_places()
    {
        // The map canvas: "Buradasın · Bölüm 47" in room 1 with four of five paintings whole, room 2 "Bölüm 51'de açılır".
        PaintingNumber paintingOfLevel47 = PaintingNumber.OfLevel(new LevelId(47));
        Assert.Equal((5, 1, 5), (paintingOfLevel47.Value, paintingOfLevel47.Room.Value, paintingOfLevel47.PlaceInRoom));
        Assert.Equal(new LevelId(51), new RoomNumber(2).FirstLevel);
        Assert.Equal(50, new RoomNumber(2).LevelsWonToOpen);
        Assert.Equal(0, new RoomNumber(1).LevelsWonToOpen);
        Assert.Equal(new LevelId(1000), new RoomNumber(20).LastLevel);
    }

    [Theory]
    [InlineData(1, 100, 10, 1, "3-4-3")]
    [InlineData(101, 300, 20, 2, "5x4")]
    [InlineData(301, 600, 30, 3, "6x5")]
    [InlineData(601, 1000, 40, 4, "8x5")]
    public void Paintings_are_cut_finer_the_further_their_levels_lie(int firstLevel, int lastLevel, int pieces, int perLevel, string layout)
    {
        Assert.All(Enumerable.Range(firstLevel, lastLevel - firstLevel + 1), level =>
        {
            JigsawSize size = JigsawSize.OfLevel(new LevelId(level));
            Assert.Equal((pieces, perLevel, layout), (size.PieceCount, size.PiecesPerLevel, size.Name));
        });
    }

    [Fact]
    public void The_ten_levels_of_a_painting_give_all_its_pieces()
    {
        Assert.All(PaintingNumber.All, painting =>
        {
            Assert.Equal(painting.Size, JigsawSize.OfLevel(painting.LastLevel));
            Assert.Equal(painting.Size.PieceCount, painting.Size.PiecesPerLevel * MuseumPlan.LevelsPerPainting);
        });
        Assert.Equal(3000, PaintingNumber.All.Sum(painting => painting.Size.PieceCount));
    }

    [Fact]
    public void The_layouts_are_rows_of_three_four_and_three_and_three_plain_grids()
    {
        Assert.Equal(new[] { 3, 4, 3 }, JigsawSize.Ten.RowsOfALandscapePicture);
        Assert.Equal(new[] { 5, 5, 5, 5 }, JigsawSize.Twenty.RowsOfALandscapePicture);
        Assert.Equal(new[] { 6, 6, 6, 6, 6 }, JigsawSize.Thirty.RowsOfALandscapePicture);
        Assert.Equal(new[] { 8, 8, 8, 8, 8 }, JigsawSize.Forty.RowsOfALandscapePicture);
    }

    [Fact]
    public void Numbers_outside_the_museum_are_refused_and_a_number_never_set_is_the_first()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoomNumber(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RoomNumber(21));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaintingNumber(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaintingNumber(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaintingNumber.OfLevel(new LevelId(1001)));
        Assert.Throws<ArgumentOutOfRangeException>(() => JigsawSize.OfLevel(new LevelId(1001)));

        Assert.Equal(new PaintingNumber(1), default(PaintingNumber));
        Assert.Equal(new RoomNumber(1), default(RoomNumber));
    }
}
