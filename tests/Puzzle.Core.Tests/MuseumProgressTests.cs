using Puzzle.Core.Levels;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// A player's museum: the pieces their won levels gave them, the ones they put in place, and what follows from the
/// two (paintings whole, rooms open and restored).
/// </summary>
public class MuseumProgressTests
{
    private static readonly PaintingNumber FirstPainting = new(1);

    private static MuseumProgress After(int levelsWon) => new(levelsWon, PlacedPieces.None);

    [Fact]
    public void A_new_player_has_one_open_room_and_no_piece()
    {
        MuseumProgress none = MuseumProgress.None;

        Assert.Equal((0, 0, 0), (none.PiecesWon, none.PiecesPlaced, none.PiecesWaiting));
        Assert.Equal((1, 0, 0), (none.RoomsOpened, none.RoomsRestored, none.PaintingsCompleted));
        Assert.Equal(FirstPainting, none.PaintingOfTheNextLevel);
        Assert.Empty(none.WaitingIn(FirstPainting));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(47, 47)]
    [InlineData(100, 100)]
    [InlineData(101, 102)]
    [InlineData(300, 500)]
    [InlineData(301, 503)]
    [InlineData(600, 1400)]
    [InlineData(601, 1404)]
    [InlineData(1000, 3000)]
    public void Every_won_level_gives_as_many_pieces_as_the_table_says(int levelsWon, int piecesWon)
    {
        Assert.Equal(piecesWon, After(levelsWon).PiecesWon);
    }

    [Fact]
    public void The_levels_of_a_painting_give_its_pieces_in_the_order_of_their_numbers_each_piece_once()
    {
        Assert.Equal(new[] { 0 }, MuseumProgress.PiecesGivenBy(new LevelId(1)));
        Assert.Equal(new[] { 9 }, MuseumProgress.PiecesGivenBy(new LevelId(10)));
        Assert.Equal(new[] { 0, 1 }, MuseumProgress.PiecesGivenBy(new LevelId(101)));
        Assert.Equal(new[] { 27, 28, 29 }, MuseumProgress.PiecesGivenBy(new LevelId(600)));
        Assert.Equal(new[] { 36, 37, 38, 39 }, MuseumProgress.PiecesGivenBy(new LevelId(1000)));

        Assert.All(PaintingNumber.All, painting =>
        {
            IEnumerable<int> given = Enumerable.Range(painting.FirstLevel.Value, MuseumPlan.LevelsPerPainting)
                .SelectMany(level => MuseumProgress.PiecesGivenBy(new LevelId(level)));
            Assert.Equal(Enumerable.Range(0, painting.Size.PieceCount), given);
        });
    }

    [Fact]
    public void A_won_piece_waits_on_its_paintings_tray_until_the_player_places_it()
    {
        MuseumProgress afterSeven = After(7);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, afterSeven.WaitingIn(FirstPainting));

        MuseumProgress placedTwo = afterSeven.WithPiecePlaced(FirstPainting, 4).WithPiecePlaced(FirstPainting, 0);

        Assert.Equal(new[] { 1, 2, 3, 5, 6 }, placedTwo.WaitingIn(FirstPainting));
        Assert.Equal((7, 2, 5), (placedTwo.PiecesWon, placedTwo.PiecesPlaced, placedTwo.PiecesWaiting));
        Assert.Equal(2, placedTwo.PiecesPlacedIn(FirstPainting));
        Assert.True(placedTwo.Placed.IsPlaced(FirstPainting, 4));
        Assert.False(placedTwo.Placed.IsPlaced(FirstPainting, 1));
        Assert.Equal(afterSeven, After(7)); // placing gave new values; the one before is as it was
    }

    [Fact]
    public void Only_a_piece_on_the_tray_can_be_placed()
    {
        MuseumProgress afterSeven = After(7).WithPiecePlaced(FirstPainting, 3);

        Assert.Throws<InvalidOperationException>(() => afterSeven.WithPiecePlaced(FirstPainting, 3)); // in place already
        Assert.Throws<InvalidOperationException>(() => afterSeven.WithPiecePlaced(FirstPainting, 7)); // not won yet
        Assert.Throws<InvalidOperationException>(() => afterSeven.WithPiecePlaced(new PaintingNumber(2), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => afterSeven.WithPiecePlaced(FirstPainting, 10)); // no such piece
        Assert.Throws<ArgumentOutOfRangeException>(() => afterSeven.WithPiecePlaced(FirstPainting, -1));
    }

    [Fact]
    public void Pieces_cannot_be_in_place_that_the_levels_won_have_not_given()
    {
        PlacedPieces eighthPiece = PlacedPieces.None.With(FirstPainting, 7);

        Assert.True(MuseumProgress.IsPossible(levelsWon: 8, eighthPiece));
        Assert.False(MuseumProgress.IsPossible(levelsWon: 7, eighthPiece));
        Assert.Throws<ArgumentException>(() => new MuseumProgress(levelsWon: 7, eighthPiece));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MuseumProgress(levelsWon: -1, PlacedPieces.None));
    }

    [Fact]
    public void A_painting_is_whole_when_all_its_pieces_are_in_place()
    {
        MuseumProgress allWonNonePlaced = After(10);
        Assert.False(allWonNonePlaced.IsComplete(FirstPainting));

        MuseumProgress whole = allWonNonePlaced.WithTrayPlaced(FirstPainting);

        Assert.True(whole.IsComplete(FirstPainting));
        Assert.Empty(whole.WaitingIn(FirstPainting));
        Assert.Equal(1, whole.PaintingsCompleted);
        Assert.Equal(whole, whole.WithTrayPlaced(FirstPainting)); // an empty tray places nothing
    }

    [Fact]
    public void Placing_the_tray_places_what_is_won_and_no_more()
    {
        var eleventh = new PaintingNumber(11); // the first painting of twenty pieces, two a level
        MuseumProgress afterThreeOfItsLevels = After(103).WithPiecePlaced(eleventh, 2).WithTrayPlaced(eleventh);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, afterThreeOfItsLevels.Placed.In(eleventh));
        Assert.False(afterThreeOfItsLevels.IsComplete(eleventh));
    }

    [Fact]
    public void A_room_opens_with_the_levels_and_is_restored_with_the_pieces()
    {
        var second = new RoomNumber(2);
        Assert.False(After(49).IsOpen(second));

        MuseumProgress afterFifty = After(50);
        Assert.True(afterFifty.IsOpen(second));
        Assert.Equal(2, afterFifty.RoomsOpened);
        Assert.Equal(new PaintingNumber(6), afterFifty.PaintingOfTheNextLevel);
        Assert.False(afterFifty.IsRestored(new RoomNumber(1))); // the levels never wait for the jigsaw

        MuseumProgress allPlaced = new RoomNumber(1).Paintings.Aggregate(afterFifty, (progress, painting) => progress.WithTrayPlaced(painting));
        Assert.True(allPlaced.IsRestored(new RoomNumber(1)));
        Assert.Equal((1, 5), (allPlaced.RoomsRestored, allPlaced.PaintingsCompleted));
    }

    [Fact]
    public void Levels_beyond_the_museums_thousand_give_nothing_more()
    {
        MuseumProgress beyond = After(1004);

        Assert.Equal(1000, beyond.LevelsWon);
        Assert.Equal(3000, beyond.PiecesWon);
        Assert.Equal(20, beyond.RoomsOpened);
        Assert.Equal(new PaintingNumber(100), beyond.PaintingOfTheNextLevel);
    }

    [Fact]
    public void Placed_pieces_are_equal_when_the_same_pieces_are_in_place_whatever_the_order()
    {
        PlacedPieces oneWay = PlacedPieces.None.With(FirstPainting, 1).With(new PaintingNumber(40), 29);
        PlacedPieces otherWay = PlacedPieces.None.With(new PaintingNumber(40), 29).With(FirstPainting, 1);

        Assert.Equal(oneWay, otherWay);
        Assert.Equal(oneWay.GetHashCode(), otherWay.GetHashCode());
        Assert.NotEqual(oneWay, PlacedPieces.None.With(FirstPainting, 1));
        Assert.Equal(2, oneWay.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PlacedPieces.None.With(new PaintingNumber(40), 30)); // thirty pieces: 0 to 29
        Assert.True(PlacedPieces.None.With(new PaintingNumber(100), 39).IsPlaced(new PaintingNumber(100), 39));
    }
}
