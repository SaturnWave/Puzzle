using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The jigsaw a painting is restored with and the collection, in the Unity game: what lies on a jigsaw's table and in
/// its tray, when a piece that is let go drops into its place, what the collection shows of a painting, the Turkish
/// words of both screens, and where both put their things on a screen. These are the Unity project's source files,
/// compiled into these tests: they have no Unity dependency.
/// </summary>
public class JigsawScreenTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    /// <summary>The proportions of the first room's paintings as the game carries them, of their full-size pictures, and of a blank canvas.</summary>
    private static readonly float[] PictureAspects = { 1.69f, 1.25f, 0.81f, 4f / 3f, 1.04f, 1.29f, 2.2f, 0.6f };

    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 0, 0), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
        (720, 1280, 0, 0), (1200, 1920, 0, 0), (2076, 2152, 0, 0), (2076, 2152, 80, 60), (1600, 2560, 0, 0),
        (2048, 2732, 48, 40), (1640, 2360, 48, 40), (1488, 2266, 48, 40),
        (2732, 2048, 0, 40), (2732, 2048, 48, 40), (2360, 1640, 0, 40), (2152, 2076, 0, 0), (2560, 1600, 0, 0),
    };

    /// <summary>A player who has won so many levels and put so many of the pieces they gave in place, in the museum's order.</summary>
    private static PlayerProgress Player(int levelsWon, int piecesPlaced) =>
        new PlayerProgress(levelsWon, attemptsOnLevel: 0).WithWaitingPiecesPlaced(piecesPlaced);

    [Fact]
    public void A_jigsaws_table_begins_as_the_progress_has_the_painting()
    {
        var table = new JigsawTable(new PaintingNumber(5), Player(levelsWon: 47, piecesPlaced: 42).Museum);

        Assert.Equal(new[] { 0, 1 }, table.InPlace);
        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, table.Waiting);
        Assert.Equal(10, table.PieceCount);
        Assert.False(table.IsWhole);
    }

    [Fact]
    public void A_piece_goes_from_the_tray_to_its_place_once_and_only_if_it_waits()
    {
        var table = new JigsawTable(new PaintingNumber(5), Player(levelsWon: 47, piecesPlaced: 42).Museum);

        table.Place(4);

        Assert.Equal(new[] { 0, 1, 4 }, table.InPlace);
        Assert.Equal(new[] { 2, 3, 5, 6 }, table.Waiting);
        Assert.Throws<InvalidOperationException>(() => table.Place(4));  // in place already
        Assert.Throws<InvalidOperationException>(() => table.Place(7));  // not won yet
        Assert.Throws<InvalidOperationException>(() => table.Place(40)); // no such piece
    }

    [Fact]
    public void A_painting_is_whole_when_its_last_piece_is_placed_and_the_progress_agrees_with_the_table()
    {
        PlayerProgress player = Player(levelsWon: 50, piecesPlaced: 47);
        var painting = new PaintingNumber(5);
        var table = new JigsawTable(painting, player.Museum);

        foreach (int piece in table.Waiting.ToArray())
        {
            table.Place(piece);
            player = player.WithPiecePlaced(painting, piece);
        }

        Assert.True(table.IsWhole);
        Assert.Empty(table.Waiting);
        Assert.True(player.Museum.IsComplete(painting));
        Assert.Equal(table.InPlace, player.Museum.Placed.In(painting));
        Assert.True(player.Museum.IsRestored(new RoomNumber(1)));
        Assert.Equal(new PaintingNumber(6), MuseumStanding.Of(player.Museum, TestMuseums.Full).Painting);
    }

    [Fact]
    public void A_tray_shows_its_first_pieces_and_counts_the_ones_behind()
    {
        var row = new TrayRow(new[] { 2, 3, 4, 5, 6 }, placeCount: 3);

        Assert.Equal(new int?[] { 2, 3, 4 }, row.Places);
        Assert.Equal(new[] { 2, 3, 4 }, row.Shown);
        Assert.Equal(2, row.Behind);
        Assert.Equal(1, row.PlaceOf(3));
        Assert.Null(row.PlaceOf(5));
    }

    [Fact]
    public void A_piece_that_leaves_the_tray_is_followed_into_its_place_by_the_next_one_behind()
    {
        var row = new TrayRow(new[] { 2, 3, 4, 5, 6 }, placeCount: 3);

        Assert.Equal(5, row.Take(3));
        Assert.Equal(new int?[] { 2, 5, 4 }, row.Places);
        Assert.Equal(6, row.Take(2));
        Assert.Null(row.Take(4));

        Assert.Equal(new int?[] { 6, 5, null }, row.Places);
        Assert.Equal(new[] { 6, 5 }, row.Shown);
        Assert.Equal(0, row.Behind);
        Assert.Throws<InvalidOperationException>(() => row.Take(4));
    }

    [Fact]
    public void A_tray_has_no_more_places_than_pieces_and_at_least_room_for_one()
    {
        Assert.Equal(new int?[] { 7 }, new TrayRow(new[] { 7 }, placeCount: 4).Places);
        Assert.Empty(new TrayRow(Array.Empty<int>(), placeCount: 4).Places);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrayRow(new[] { 1 }, placeCount: 0));
    }

    [Theory]
    [InlineData(0f, 0f, true)]       // exactly on its place
    [InlineData(50f, 0f, true)]      // half a piece to the side: 0.55 of 100 reaches
    [InlineData(60f, 0f, false)]
    [InlineData(0f, 30f, true)]      // 0.55 of 60 reaches 33 upright
    [InlineData(0f, 36f, false)]
    [InlineData(45f, 25f, false)]    // near both limits at once: outside the ellipse
    [InlineData(-30f, -15f, true)]
    public void A_piece_let_go_within_reach_of_its_place_drops_in(float awayX, float awayY, bool dropsIn)
    {
        Assert.Equal(dropsIn, JigsawSnap.Catches(awayX, awayY, placeWidth: 100f, placeHeight: 60f));
    }

    [Fact]
    public void The_small_pieces_of_a_large_jigsaw_are_caught_from_half_a_fingertip_away()
    {
        // A forty-piece painting on a phone has places of about 36 by 34 points: 0.55 of that would be under 20.
        Assert.True(JigsawSnap.Catches(21f, 0f, placeWidth: 36f, placeHeight: 34f));
        Assert.True(JigsawSnap.Catches(0f, -21f, placeWidth: 36f, placeHeight: 34f));
        Assert.False(JigsawSnap.Catches(23f, 0f, placeWidth: 36f, placeHeight: 34f));
    }

    [Fact]
    public void A_painting_is_covered_being_restored_or_whole()
    {
        RoomWall room = RoomWall.Of(new RoomNumber(1), Player(levelsWon: 47, piecesPlaced: 42).Museum, Shipped.Value);

        Assert.Equal(
            new[] { PaintingStanding.Whole, PaintingStanding.Whole, PaintingStanding.Whole, PaintingStanding.Whole, PaintingStanding.BeingRestored },
            room.Paintings.Select(painting => painting.Standing));
        Assert.Equal((2, 5, 7), (room.Paintings[4].PiecesInPlace.Count, room.Paintings[4].PiecesWaiting, room.Paintings[4].PiecesWon));

        RoomWall ahead = RoomWall.Of(new RoomNumber(1), Player(levelsWon: 12, piecesPlaced: 0).Museum, Shipped.Value);
        Assert.Equal(
            new[] { PaintingStanding.BeingRestored, PaintingStanding.BeingRestored, PaintingStanding.NotReached, PaintingStanding.NotReached, PaintingStanding.NotReached },
            ahead.Paintings.Select(painting => painting.Standing));
    }

    [Theory]
    [InlineData(0, 2)]      // a new player sees the first room, and the second waiting
    [InlineData(49, 2)]
    [InlineData(50, 3)]     // fifty levels open the second room: the third is seen waiting
    [InlineData(640, 14)]
    [InlineData(1000, 20)]  // the museum has no room after the twentieth
    public void The_collection_shows_the_rooms_that_are_open_and_the_one_after(int levelsWon, int lastRoom)
    {
        MuseumProgress museum = Player(levelsWon, piecesPlaced: 0).Museum;

        Assert.Equal(new RoomNumber(lastRoom), CollectionRooms.LastToVisit(museum, TestMuseums.Full));
        Assert.True(CollectionRooms.Shows(new RoomNumber(lastRoom), museum, TestMuseums.Full));
        if (lastRoom < MuseumPlan.RoomCount)
        {
            Assert.False(CollectionRooms.Shows(new RoomNumber(lastRoom + 1), museum, TestMuseums.Full));
        }
    }

    [Fact]
    public void The_jigsaw_and_the_collection_speak_in_the_designs_words()
    {
        Assert.Equal("Koleksiyon", MuseumWords.CollectionTab);
        Assert.Equal("4 / 100 TABLO", MuseumWords.CollectionCount(4));
        Assert.Equal("7 / 10", MuseumWords.PiecesOf(7, 10));
        Assert.Equal("6 / 10 parça", MuseumWords.PiecesInPlace(6, 10));
        Assert.Equal("BÖLÜM 21–30", MuseumWords.LevelsOf(new PaintingNumber(3)));
        Assert.Equal("Parçayı yerine sürükle", MuseumWords.DragHint(1));
        Assert.Equal("Parçaları yerine sürükle", MuseumWords.DragHint(4));
        Assert.Equal("Parçayı\nyerleştir", MuseumWords.PlaceWhatWasWon(1));
        Assert.Equal("Parçaları\nyerleştir", MuseumWords.PlaceWhatWasWon(4));
        Assert.Equal("+6", MuseumWords.MoreBehind(6));
        Assert.Equal("Otomatik tamamla", MuseumWords.PlaceThemAll);
    }

    /// <summary>Every screen on which the jigsaw stacks its parts upright, with every picture and every cut.</summary>
    public static TheoryData<int, int, int, int, float, int, bool> EveryScreenPictureAndCut() => PicturesAndCutsOn(sideways: false);

    /// <summary>Every screen held sideways, on which the jigsaw stands its parts in two columns, with every picture and every cut.</summary>
    public static TheoryData<int, int, int, int, float, int, bool> EverySidewaysScreenPictureAndCut() => PicturesAndCutsOn(sideways: true);

    private static TheoryData<int, int, int, int, float, int, bool> PicturesAndCutsOn(bool sideways)
    {
        var cases = new TheoryData<int, int, int, int, float, int, bool>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            if (PageLayout.Of(new ScreenShape(width, height, top, bottom)).IsSideways != sideways)
            {
                continue;
            }

            foreach (float aspect in PictureAspects)
            {
                foreach (JigsawSize size in JigsawSize.All)
                {
                    cases.Add(width, height, top, bottom, aspect, size.PieceCount, aspect < 1f);
                }
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryScreenPictureAndCut))]
    public void The_jigsaw_stacks_its_plaque_painting_pips_tray_and_button_without_any_two_meeting(
        int width, int height, int top, int bottom, float aspect, int pieceCount, bool isUpright)
    {
        JigsawSize size = JigsawSize.All.Single(cut => cut.PieceCount == pieceCount);
        PaintingOrientation orientation = isUpright ? PaintingOrientation.Portrait : PaintingOrientation.Landscape;
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height, top, bottom), aspect, size, orientation);
        PageLayout page = jigsaw.Page;
        ScreenView view = page.View;
        FramePlace hanging = jigsaw.Hanging;

        Assert.True(jigsaw.Plaque.Top < page.CorniceBottom, "the counter's plaque runs into the cornice");
        Assert.True(jigsaw.Back.Left >= view.Left && jigsaw.Back.Right < jigsaw.Plaque.Left, "the back button is cut off, or lies on the plaque");
        Assert.True(jigsaw.Back.Top < page.CorniceBottom && jigsaw.Back.Bottom > page.RailTop, "the back button leaves its strip of wall");

        Assert.True(hanging.Frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(hanging.Frame.Left >= view.Left && hanging.Frame.Right <= view.Right, "the frame is cut off at a side");
        Assert.True(jigsaw.Pips.Top < hanging.PlateBottom, "the pips run into the painting's plate");
        Assert.True(jigsaw.Pips.Left >= view.Left && jigsaw.Pips.Right <= view.Right, "the pips are cut off at a side");
        Assert.Equal(pieceCount > 20 ? 2 : 1, jigsaw.PipRows);

        float hintTop = jigsaw.Hint.MiddleY + JigsawLayout.HintHeight / 2;
        Assert.True(hintTop < jigsaw.Pips.Bottom, "the tray's hint runs into the pips");
        Assert.True(jigsaw.Tray.Left >= view.Left && jigsaw.Tray.Right <= view.Right, "the tray is cut off at a side");
        Assert.True(jigsaw.Button.Top < jigsaw.Tray.Bottom, "the button runs into the tray");
        Assert.True(jigsaw.Button.Bottom - JigsawLayout.ButtonLip * jigsaw.ButtonScale > view.UsableBottom, "the button's lip is under the system bar");
        Assert.True(jigsaw.Button.Left >= view.Left && jigsaw.Button.Right <= view.Right, "the button is cut off at a side");

        float onePixel = 1f / view.PixelsPerPoint;
        Box picture = hanging.Picture;
        Assert.InRange(picture.Width / picture.Height, aspect * (1 - 2 * onePixel / picture.Height) - 0.01f, aspect * (1 + 2 * onePixel / picture.Height) + 0.01f);
        Assert.True(picture.Width <= JigsawLayout.WidestPicture + onePixel);
        Assert.True(picture.Height >= 95f, $"the picture is only {picture.Height} points high");
    }

    [Theory]
    [MemberData(nameof(EveryScreenPictureAndCut))]
    public void The_pieces_of_the_tray_lie_side_by_side_in_its_well(int width, int height, int top, int bottom, float aspect, int pieceCount, bool isUpright)
    {
        JigsawSize size = JigsawSize.All.Single(cut => cut.PieceCount == pieceCount);
        PaintingOrientation orientation = isUpright ? PaintingOrientation.Portrait : PaintingOrientation.Landscape;
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height, top, bottom), aspect, size, orientation);
        Box well = jigsaw.Well;

        Assert.InRange(jigsaw.TrayPlaces, 1, JigsawLayout.MostTrayPlaces);
        Assert.InRange(jigsaw.TrayPieceScale, 0.2f, 1f);
        Assert.True(well.Left > jigsaw.Tray.Left && well.Right < jigsaw.Tray.Right && well.Bottom > jigsaw.Tray.Bottom && well.Top < jigsaw.Tray.Top);

        // A piece with a knob at the top and one at the foot is half as high again as its cell; it still lies in the well.
        float pieceHeight = 1.5f * jigsaw.LargestCell.Height * jigsaw.TrayPieceScale;
        float pieceWidth = 1.3f * jigsaw.LargestCell.Width * jigsaw.TrayPieceScale;
        Assert.True(pieceHeight <= well.Height, $"a piece {pieceHeight} points high lies in a well of {well.Height}");
        Assert.True(pieceWidth <= well.Width / jigsaw.TrayPlaces, $"a piece {pieceWidth} points wide lies in a place of {well.Width / jigsaw.TrayPlaces}");

        // The places of a full row and of a row of one are centred in the well and lie inside it.
        float placeWidth = well.Width / jigsaw.TrayPlaces;
        (float first, float y) = jigsaw.TrayPlaceMiddle(0, jigsaw.TrayPlaces);
        (float last, _) = jigsaw.TrayPlaceMiddle(jigsaw.TrayPlaces - 1, jigsaw.TrayPlaces);
        Assert.Equal(well.MiddleY, y, precision: 3);
        Assert.Equal(well.Left + placeWidth / 2, first, precision: 2);
        Assert.Equal(well.Right - placeWidth / 2, last, precision: 2);
        Assert.Equal(well.MiddleX, jigsaw.TrayPlaceMiddle(0, 1).X, precision: 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => jigsaw.TrayPlaceMiddle(jigsaw.TrayPlaces, jigsaw.TrayPlaces));
        Assert.Throws<ArgumentOutOfRangeException>(() => jigsaw.TrayPlaceMiddle(0, jigsaw.TrayPlaces + 1));
    }

    [Theory]
    [MemberData(nameof(EverySidewaysScreenPictureAndCut))]
    public void Held_sideways_the_jigsaw_hangs_its_painting_at_the_left_and_its_tray_at_the_right_without_any_two_meeting(
        int width, int height, int top, int bottom, float aspect, int pieceCount, bool isUpright)
    {
        JigsawSize size = JigsawSize.All.Single(cut => cut.PieceCount == pieceCount);
        PaintingOrientation orientation = isUpright ? PaintingOrientation.Portrait : PaintingOrientation.Landscape;
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height, top, bottom), aspect, size, orientation);
        PageLayout page = jigsaw.Page;
        ScreenView view = page.View;
        Box frame = jigsaw.Hanging.Frame;
        float hintTop = jigsaw.Hint.MiddleY + JigsawLayout.HintHeight / 2;

        Assert.Equal(JigsawArrangement.SideBySide, jigsaw.Arrangement);
        Assert.True(jigsaw.Plaque.Top < page.CorniceBottom && jigsaw.Plaque.Bottom > page.RailTop, "the counter's plaque leaves its strip of wall");
        Assert.InRange(jigsaw.Plaque.MiddleX, frame.Left, frame.Right);
        Assert.True(jigsaw.Back.Left >= view.UsableLeft && jigsaw.Back.Right < jigsaw.Plaque.Left, "the back button is cut off, or lies on the plaque");

        Assert.True(frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(frame.Left >= view.UsableLeft, "the frame is cut off at the left");
        Assert.True(jigsaw.Hanging.PlateBottom > view.UsableBottom, "the painting's plate is under the system bar");

        // The column at the right: pips, hint, tray and button from the rail down, clear of the frame and of each other.
        foreach (Box part in new[] { jigsaw.Pips, jigsaw.Tray, jigsaw.Button })
        {
            Assert.True(part.Left > frame.Right && part.Right <= view.UsableRight, $"a part of the column at {part} meets the frame or leaves the screen");
        }

        Assert.True(jigsaw.Pips.Top < page.RailBottom, "the pips run into the picture rail");
        Assert.Equal(page.RailBottom, jigsaw.PipsHangFrom);
        Assert.True(hintTop < jigsaw.Pips.Bottom, "the tray's hint runs into the pips");
        Assert.True(jigsaw.Button.Top < jigsaw.Tray.Bottom, "the button runs into the tray");
        Assert.True(jigsaw.Button.Bottom - JigsawLayout.ButtonLip * jigsaw.ButtonScale > view.UsableBottom, "the button's lip is under the system bar");

        float onePixel = 1f / view.PixelsPerPoint;
        Box picture = jigsaw.Hanging.Picture;
        Assert.InRange(picture.Width / picture.Height, aspect * (1 - 2 * onePixel / picture.Height) - 0.01f, aspect * (1 + 2 * onePixel / picture.Height) + 0.01f);
        Assert.True(picture.Height >= 190f, $"the picture held sideways is only {picture.Height} points high");
    }

    [Theory]
    [MemberData(nameof(EverySidewaysScreenPictureAndCut))]
    public void Held_sideways_the_tray_lays_its_pieces_in_rows_and_columns_each_in_a_place_of_its_own(
        int width, int height, int top, int bottom, float aspect, int pieceCount, bool isUpright)
    {
        JigsawSize size = JigsawSize.All.Single(cut => cut.PieceCount == pieceCount);
        PaintingOrientation orientation = isUpright ? PaintingOrientation.Portrait : PaintingOrientation.Landscape;
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height, top, bottom), aspect, size, orientation);
        Box well = jigsaw.Well;
        (float placeWidth, float placeHeight) = jigsaw.TrayPlaceSize;

        Assert.Equal(JigsawLayout.MostTrayPlaces, jigsaw.TrayPlaces);
        Assert.Contains((jigsaw.TrayColumns, jigsaw.TrayRows), new[] { (2, 3), (3, 2) });
        Assert.InRange(jigsaw.TrayPieceScale, 0.2f, 1f);

        // A piece with its knobs lies in its place.
        Assert.True(1.3f * jigsaw.LargestCell.Width * jigsaw.TrayPieceScale <= placeWidth);
        Assert.True(1.5f * jigsaw.LargestCell.Height * jigsaw.TrayPieceScale <= placeHeight);

        // Whatever number of places is used, each lies in the well, a place's width or height away from every other.
        for (int used = 1; used <= jigsaw.TrayPlaces; used++)
        {
            var middles = Enumerable.Range(0, used).Select(place => jigsaw.TrayPlaceMiddle(place, used)).ToArray();
            foreach ((float x, float y) in middles)
            {
                Assert.True(well.Contains(x, y), $"a place of {used} lies outside the well");
            }

            for (int place = 0; place < used; place++)
            {
                for (int other = place + 1; other < used; other++)
                {
                    bool apart = Math.Abs(middles[place].X - middles[other].X) >= placeWidth - 0.01f || Math.Abs(middles[place].Y - middles[other].Y) >= placeHeight - 0.01f;
                    Assert.True(apart, $"places {place} and {other} of {used} overlap");
                }
            }
        }

        Assert.Equal(well.MiddleX, jigsaw.TrayPlaceMiddle(0, 1).X, precision: 3);
        Assert.Equal(well.MiddleY, jigsaw.TrayPlaceMiddle(0, 1).Y, precision: 3);
    }

    [Theory]
    [InlineData(2732, 2048, 2048, 2732)]
    [InlineData(2360, 1640, 1640, 2360)]
    [InlineData(2560, 1600, 1600, 2560)]
    public void A_tablet_held_sideways_draws_an_upright_painting_larger_than_held_upright(int width, int height, int uprightWidth, int uprightHeight)
    {
        JigsawLayout sideways = JigsawLayout.Of(new ScreenShape(width, height), 0.81f, JigsawSize.Forty, PaintingOrientation.Portrait);
        JigsawLayout upright = JigsawLayout.Of(new ScreenShape(uprightWidth, uprightHeight), 0.81f, JigsawSize.Forty, PaintingOrientation.Portrait);

        Assert.Equal(JigsawArrangement.SideBySide, sideways.Arrangement);
        Assert.Equal(JigsawArrangement.Stacked, upright.Arrangement);
        Assert.True(sideways.Hanging.Picture.Height > 1.3f * upright.Hanging.Picture.Height, $"{sideways.Hanging.Picture.Height} points sideways, {upright.Hanging.Picture.Height} upright");
    }

    [Theory]
    [InlineData(2152, 2076)]
    [InlineData(2076, 2152)]
    [InlineData(1080, 2424)]
    public void The_open_foldable_held_either_way_and_a_phone_keep_the_upright_jigsaw(int width, int height)
    {
        JigsawLayout jigsaw = JigsawLayout.Of(new ScreenShape(width, height), 1.25f, JigsawSize.Twenty, PaintingOrientation.Landscape);

        Assert.Equal(JigsawArrangement.Stacked, jigsaw.Arrangement);
        Assert.Equal(1, jigsaw.TrayRows);
        Assert.Equal(jigsaw.Hanging.PlateBottom, jigsaw.PipsHangFrom);
    }

    [Fact]
    public void A_ten_piece_landscape_painting_shows_two_large_pieces_on_a_phone_and_a_forty_piece_one_five()
    {
        var phone = new ScreenShape(1080, 2424);

        JigsawLayout ten = JigsawLayout.Of(phone, 1.69f, JigsawSize.Ten, PaintingOrientation.Landscape);
        JigsawLayout forty = JigsawLayout.Of(phone, 1.69f, JigsawSize.Forty, PaintingOrientation.Landscape);

        Assert.Equal(2, ten.TrayPlaces);
        Assert.True(ten.TrayPieceScale > 0.8f, $"a ten-piece painting's pieces lie in the tray at {ten.TrayPieceScale} of their size");
        Assert.Equal(5, forty.TrayPlaces);
        Assert.Equal(1f, forty.TrayPieceScale);
        Assert.Throws<ArgumentOutOfRangeException>(() => JigsawLayout.Of(phone, 0f, JigsawSize.Ten, PaintingOrientation.Landscape));
    }

    [Fact]
    public void An_upright_paintings_cut_runs_in_columns_so_its_largest_piece_is_tall()
    {
        JigsawLayout upright = JigsawLayout.Of(new ScreenShape(1080, 2424), 0.81f, JigsawSize.Ten, PaintingOrientation.Portrait);
        Box picture = upright.Hanging.Picture;

        Assert.Equal(picture.Width / 3, upright.LargestCell.Width, precision: 3);
        Assert.Equal(picture.Height / 3, upright.LargestCell.Height, precision: 3);
    }

    public static TheoryData<int, int, int, int> EveryScreen()
    {
        var cases = new TheoryData<int, int, int, int>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            cases.Add(width, height, top, bottom);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void The_collection_hangs_a_rooms_five_paintings_between_the_rail_and_the_bar_without_any_two_meeting(int width, int height, int top, int bottom)
    {
        float[] aspects = { 1.25f, 0.81f, 1.29f, 1.04f, 1.69f };
        CollectionLayout collection = CollectionLayout.Of(new ScreenShape(width, height, top, bottom), aspects);
        PageLayout page = collection.Page;

        Assert.Equal(5, collection.Exhibits.Count);
        for (int painting = 0; painting < collection.Exhibits.Count; painting++)
        {
            ExhibitPlace exhibit = collection.Exhibits[painting];
            Box touch = exhibit.Touch;
            Assert.True(exhibit.Frame.Top < page.RailBottom, $"painting {painting + 1} runs into the picture rail");
            Assert.True(touch.Bottom > page.BarReach, $"painting {painting + 1} reaches the bar");
            Assert.True(touch.Left >= page.View.Left && touch.Right <= page.View.Right, $"painting {painting + 1} is cut off at a side");
            Assert.True(exhibit.Picture.Height >= 50f, $"painting {painting + 1} is only {exhibit.Picture.Height} points high");
            Assert.InRange(exhibit.Picture.Width / exhibit.Picture.Height, aspects[painting] - 0.05f, aspects[painting] + 0.05f);
            Assert.Equal(painting < (page.View.Width >= 410f ? 3 : 2), exhibit.HangsFromTheRail);

            for (int other = painting + 1; other < collection.Exhibits.Count; other++)
            {
                Box otherTouch = collection.Exhibits[other].Touch;
                bool areApart = touch.Right <= otherTouch.Left || otherTouch.Right <= touch.Left || touch.Top <= otherTouch.Bottom || otherTouch.Top <= touch.Bottom;
                Assert.True(areApart, $"paintings {painting + 1} and {other + 1} lie on each other");
            }
        }

        // The two round buttons stand on the plaque, the one for the room before left of the one for the room after.
        Assert.True(collection.PreviousRoom.Left > page.Plaque.Left && collection.NextRoom.Right < page.Plaque.Right);
        Assert.True(collection.PreviousRoom.Right < collection.NextRoom.Left);
        Assert.True(collection.NextRoom.Top < page.Plaque.Top && collection.NextRoom.Bottom > page.Plaque.Bottom);
    }

    [Theory]
    [InlineData(2048, 2732, 48, 40, 3)]
    [InlineData(1640, 2360, 48, 40, 3)]
    [InlineData(1488, 2266, 48, 40, 3)]
    [InlineData(2076, 2152, 0, 0, 3)]
    [InlineData(1080, 2424, 0, 0, 2)]
    [InlineData(1080, 1920, 0, 0, 2)]
    [InlineData(1600, 2560, 0, 0, 2)]
    public void An_ipad_upright_and_the_open_fold_hang_a_rooms_paintings_three_to_a_row_and_a_phone_two(int width, int height, int top, int bottom, int inTheTopRow)
    {
        float[] aspects = { 1.25f, 0.81f, 1.29f, 1.04f, 1.69f };
        CollectionLayout collection = CollectionLayout.Of(new ScreenShape(width, height, top, bottom), aspects);

        Assert.Equal(inTheTopRow, collection.Exhibits.Count(exhibit => exhibit.HangsFromTheRail));
    }

    [Fact]
    public void On_the_largest_ipad_every_painting_of_a_room_is_larger_than_three_rows_of_two_hung_it()
    {
        // Three rows of two gave every painting of this room 67 points of height on the 12.9" iPad Pro.
        float[] aspects = { 1.25f, 0.81f, 1.29f, 1.04f, 1.69f };
        CollectionLayout collection = CollectionLayout.Of(new ScreenShape(2048, 2732, 48, 40), aspects);

        Assert.All(collection.Exhibits, exhibit => Assert.True(exhibit.Picture.Height > 70f, $"a painting is only {exhibit.Picture.Height} points high"));
    }

    [Fact]
    public void A_wall_of_the_collection_needs_paintings_with_proportions()
    {
        var phone = new ScreenShape(1080, 2424);

        Assert.Throws<ArgumentException>(() => CollectionLayout.Of(phone, Array.Empty<float>()));
        Assert.Throws<ArgumentException>(() => CollectionLayout.Of(phone, new[] { 1.2f, 0f }));
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
    public void A_painting_that_is_looked_at_by_itself_hangs_large_inside_the_usable_screen(int width, int height, int top, int bottom, float aspect)
    {
        ViewerLayout viewer = ViewerLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        ScreenView view = viewer.View;
        Box frame = viewer.Hanging.Frame;

        Assert.True(frame.Top < view.UsableTop && frame.Left >= view.Left && frame.Right <= view.Right, "the frame is cut off");
        Assert.True(viewer.LineMiddle < viewer.Hanging.PlateBottom, "the line lies on the plate");
        Assert.True(viewer.LineMiddle - 7f > view.UsableBottom, "the line is under the system bar");
        Assert.Equal(view.Top, viewer.Hanging.WiresFrom);

        // It is larger than the same painting in the hall wherever the screen has the room.
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        Assert.True(viewer.Hanging.Picture.Height >= hall.Picture.Height - 0.5f, $"{viewer.Hanging.Picture.Height} points high in front of the dark, {hall.Picture.Height} in the hall");
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void While_pieces_wait_the_hall_has_the_jigsaws_button_beside_the_play_button(int width, int height, int top, int bottom, float aspect)
    {
        var screen = new ScreenShape(width, height, top, bottom);
        HallLayout alone = HallLayout.Of(screen, aspect);
        HallLayout beside = HallLayout.Of(screen, aspect, hasJigsawButton: true);

        Assert.Null(alone.JigsawButton);
        Box jigsaw = Assert.IsType<Box>(beside.JigsawButton);
        Assert.True(beside.Button.Right < jigsaw.Left, "the two buttons lie on each other");
        Assert.True(beside.Button.Left >= beside.Page.View.Left && jigsaw.Right <= beside.Page.View.Right, "a button is cut off at a side");
        Assert.Equal((beside.Button.Bottom, beside.Button.Height), (jigsaw.Bottom, jigsaw.Height));
        Assert.Equal(alone.Button.Bottom, beside.Button.Bottom);
        Assert.Equal(alone.Picture.Height, beside.Picture.Height);
    }

    [Fact]
    public void A_frame_hangs_from_above_itself_and_carries_its_plate_on_its_lower_bar()
    {
        var place = new FramePlace(new Box(-100f, 0f, 200f, 120f), wiresFrom: 200f);

        Assert.Equal((-123f, 123f, -23f, 143f), (place.Frame.Left, place.Frame.Right, place.Frame.Bottom, place.Frame.Top));
        Assert.Equal(-27.5f, place.PlateMiddle);
        Assert.Equal(-45f, place.PlateBottom);
        Assert.Equal(200f, place.WidestPlate);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FramePlace(new Box(-100f, 0f, 200f, 120f), wiresFrom: 140f));
    }
}
