using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// What a player meets where the museum's paintings end, when a room becomes whole, when they want to leave a
/// level, and where the controls of all that lie on a screen. The catalogue the repository ships has paintings for
/// its first room only, so a tester reaches the end of them after fifty levels: from there on the game hands out
/// no piece of a blank canvas, shows no blank painting as restored, and says that more is being prepared. These
/// are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class MuseumEndsTests
{
    private const int LevelCount = 1000;

    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 0, 0), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
        (720, 1280, 0, 0), (1200, 1920, 0, 0), (2076, 2152, 0, 0), (2076, 2152, 80, 60), (1600, 2560, 0, 0),
        (2048, 2732, 48, 40), (1640, 2360, 48, 40), (1488, 2266, 48, 40),
        (2732, 2048, 0, 40), (2732, 2048, 48, 40), (2360, 1640, 0, 40), (2152, 2076, 0, 0), (2560, 1600, 0, 0),
    };

    /// <summary>The proportions of the first room's paintings, of a blank canvas, and of a very wide and a very upright picture.</summary>
    private static readonly float[] PictureAspects = { 1.36f, 0.81f, 1.29f, 1.04f, 1.69f, 4f / 3f, 2.2f, 0.6f };

    private static PlayerProgress Player(int levelsWon, int piecesPlaced) =>
        new PlayerProgress(levelsWon, attemptsOnLevel: 0).WithWaitingPiecesPlaced(piecesPlaced);

    [Theory]
    [InlineData(0, 0, 1, HallTask.Restoring)]
    [InlineData(47, 46, 5, HallTask.Restoring)]
    [InlineData(50, 49, 5, HallTask.Restoring)]              // the last piece of the room still waits
    [InlineData(50, 50, 5, HallTask.WaitingForPaintings)]    // the room is whole and the next has no painting: its last one stays in the frame
    [InlineData(51, 50, 5, HallTask.WaitingForPaintings)]    // a level of a painting that is not there gives the hall nothing to restore
    [InlineData(640, 50, 5, HallTask.WaitingForPaintings)]
    [InlineData(640, 20, 3, HallTask.Restoring)]             // however far the levels are, a painting that is there and not whole comes first
    [InlineData(1000, 50, 5, HallTask.WaitingForPaintings)]  // every level won, but most of the museum is still to come: not "restored"
    public void Where_the_paintings_end_the_hall_keeps_the_last_one_restored_and_says_that_more_are_coming(int levelsWon, int placed, int painting, HallTask task)
    {
        MuseumStanding standing = MuseumStanding.Of(Player(levelsWon, placed).Museum, TestMuseums.EndingAfterRoomOne);

        Assert.Equal((new PaintingNumber(painting), task), (standing.Painting, standing.Task));
        Assert.NotNull(TestMuseums.EndingAfterRoomOne.ArtworkAt(standing.Painting));
    }

    [Fact]
    public void The_museum_is_restored_only_when_every_one_of_its_hundred_paintings_hangs_whole()
    {
        Assert.Equal(HallTask.MuseumRestored, MuseumStanding.Of(Player(1000, 3000).Museum, TestMuseums.Full).Task);
        Assert.Equal(new PaintingNumber(100), MuseumStanding.Of(Player(1000, 3000).Museum, TestMuseums.Full).Painting);
        Assert.Equal(HallTask.Restoring, MuseumStanding.Of(Player(1000, 2999).Museum, TestMuseums.Full).Task);
        Assert.Equal(HallTask.WaitingForPaintings, MuseumStanding.Of(Player(1000, 3000).Museum, TestMuseums.EndingAfterRoomOne).Task);
    }

    [Fact]
    public void A_visit_with_nothing_to_restore_rings_no_painting_and_knows_when_every_level_is_won()
    {
        var resting = new MuseumVisit(Player(60, 50).Museum, TestMuseums.EndingAfterRoomOne, levelToPlay: 61);
        var atTheEnd = new MuseumVisit(Player(1000, 50).Museum, TestMuseums.EndingAfterRoomOne, levelToPlay: 1000, hasWonEveryLevel: true);

        Assert.Null(resting.BeingRestored);
        Assert.Equal((new PaintingNumber(5), new RoomNumber(1)), (resting.Painting, resting.Room));
        Assert.False(resting.HasWonEveryLevel);
        Assert.True(atTheEnd.HasWonEveryLevel);
    }

    [Theory]
    [InlineData(49, true)]    // the last level of the first room still gives its piece
    [InlineData(50, false)]   // level 51 belongs to a painting that is not there
    [InlineData(150, false)]
    [InlineData(640, false)]
    public void A_level_of_a_painting_that_is_not_there_gives_no_piece_to_hold(int levelsWon, bool givesAPiece)
    {
        PieceReward? reward = PieceReward.ForWinning(new PlayerProgress(levelsWon, attemptsOnLevel: 0), LevelCount, TestMuseums.EndingAfterRoomOne);

        Assert.Equal(givesAPiece, reward != null);
    }

    [Fact]
    public void The_levels_won_keep_the_pieces_of_a_painting_that_is_not_there_yet()
    {
        MuseumProgress museum = Player(levelsWon: 60, piecesPlaced: 50).Museum;

        // The rule of the museum itself has not changed: the day painting 6 is chosen, its ten pieces wait on its tray.
        Assert.Equal(10, museum.WaitingIn(new PaintingNumber(6)).Count);
        Assert.Equal(Enumerable.Range(0, 10), RoomWall.Of(new RoomNumber(2), museum, TestMuseums.Full).Paintings[0].PiecesInPlace.Concat(museum.WaitingIn(new PaintingNumber(6))));
    }

    [Fact]
    public void A_reward_knows_every_piece_that_waits_on_its_painting_once_it_is_won()
    {
        PieceReward first = PieceReward.ForWinning(new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0), LevelCount, TestMuseums.EndingAfterRoomOne)!;
        PieceReward tenth = PieceReward.ForWinning(new PlayerProgress(levelsWon: 9, attemptsOnLevel: 0), LevelCount, TestMuseums.EndingAfterRoomOne)!;
        PieceReward afterPlacing = PieceReward.ForWinning(Player(levelsWon: 9, piecesPlaced: 9), LevelCount, TestMuseums.EndingAfterRoomOne)!;

        Assert.Equal((1, 10, 1), (first.WaitingWithThem, tenth.WaitingWithThem, afterPlacing.WaitingWithThem));
        Assert.Equal("Parçayı\nyerleştir", MuseumWords.PlaceWhatWasWon(first.WaitingWithThem));
        Assert.Equal("Parçaları\nyerleştir", MuseumWords.PlaceWhatWasWon(tenth.WaitingWithThem));
    }

    [Fact]
    public void A_place_without_a_work_is_shown_as_to_come_whatever_the_progress_says()
    {
        // A player far beyond the first room, with pieces said to be in place where no painting hangs.
        MuseumProgress museum = Player(levelsWon: 640, piecesPlaced: 1500).Museum;

        RoomWall second = RoomWall.Of(new RoomNumber(2), museum, TestMuseums.EndingAfterRoomOne);
        RoomWall first = RoomWall.Of(new RoomNumber(1), museum, TestMuseums.EndingAfterRoomOne);

        Assert.True(second.IsToCome);
        Assert.False(second.IsRestored);
        Assert.All(second.Paintings, painting =>
        {
            Assert.Equal(PaintingStanding.ToCome, painting.Standing);
            Assert.Equal((0, 0, 0), (painting.PiecesInPlace.Count, painting.PiecesWaiting, painting.PiecesWon));
        });
        Assert.Equal(0, second.PaintingsWhole);
        Assert.True(first.IsRestored);
        Assert.False(first.IsToCome);
        Assert.Equal(5, CollectionRooms.PaintingsWhole(museum, TestMuseums.EndingAfterRoomOne));
        Assert.Equal(62, CollectionRooms.PaintingsWhole(museum, TestMuseums.Full));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(640)]
    [InlineData(1000)]
    public void The_collection_turns_no_further_than_the_first_room_that_is_still_to_come(int levelsWon)
    {
        MuseumProgress museum = Player(levelsWon, piecesPlaced: 0).Museum;

        Assert.Equal(new RoomNumber(2), CollectionRooms.LastToVisit(museum, TestMuseums.EndingAfterRoomOne));
        Assert.True(CollectionRooms.Shows(new RoomNumber(2), museum, TestMuseums.EndingAfterRoomOne));
        Assert.False(CollectionRooms.Shows(new RoomNumber(3), museum, TestMuseums.EndingAfterRoomOne));
    }

    [Fact]
    public void A_room_whose_name_is_only_pencilled_in_is_not_named_to_the_player()
    {
        MuseumCatalogue shipped = TestMuseums.EndingAfterRoomOne;

        Assert.Equal("Empresyonizm", MuseumWords.NameOf(shipped.Room(new RoomNumber(1))));
        Assert.Equal("Post-empresyonizm", MuseumWords.NameOf(shipped.Room(new RoomNumber(2))));
        Assert.All(shipped.Rooms.Skip(2), room => Assert.Equal("Yeni oda", MuseumWords.NameOf(room)));
        Assert.Equal("Yeni oda", RoomWall.Of(new RoomNumber(7), MuseumProgress.None, shipped).Name);
    }

    [Fact]
    public void A_restored_room_says_what_comes_after_it()
    {
        Assert.Equal("Oda onarıldı!", MuseumWords.RoomRestored);
        Assert.Equal("Empresyonizm · 5 / 5 tablo", MuseumWords.RoomAndItsPaintings("Empresyonizm"));
        Assert.Equal("Yeni tablolar hazırlanıyor", MuseumWords.AfterTheRoom(new RoomNumber(1), TestMuseums.EndingAfterRoomOne));
        Assert.Equal("Sıradaki oda: Akım 2", MuseumWords.AfterTheRoom(new RoomNumber(1), TestMuseums.Full));
        Assert.Equal("Müzenin tamamı onarıldı!", MuseumWords.AfterTheRoom(new RoomNumber(20), TestMuseums.Full));
    }

    [Fact]
    public void A_room_is_restored_when_the_last_piece_of_its_fifth_painting_is_placed()
    {
        PlayerProgress before = Player(levelsWon: 50, piecesPlaced: 49);

        PlayerProgress after = before.WithPiecePlaced(new PaintingNumber(5), piece: 9);

        Assert.False(RoomWall.Of(new RoomNumber(1), before.Museum, TestMuseums.EndingAfterRoomOne).IsRestored);
        Assert.True(RoomWall.Of(new RoomNumber(1), after.Museum, TestMuseums.EndingAfterRoomOne).IsRestored);
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
    public void The_pause_disc_stands_on_the_shelfs_left_end_and_is_large_enough_for_a_finger(int width, int height, int top, int bottom)
    {
        foreach ((int columns, int rows) in new[] { (9, 10), (11, 13), (7, 7) })
        {
            ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height, top, bottom), new BoardFootprint(columns, rows, hasExits: true));
            Box button = layout.PauseButton;
            float boardBottom = layout.BoardTop - rows * layout.CellSize;

            Assert.True(button.Width * layout.PointInTheHand >= ScreenLayout.SmallestDiscInTheHand - 0.001f, $"the pause disc is {button.Width * layout.PointInTheHand} points across in the hand");
            Assert.True(button.Height <= ScreenLayout.ShelfHeight * layout.ShelfScale, "the pause disc is higher than the plank it stands on");
            Assert.Equal(button.Width, button.Height, precision: 3);
            Assert.Equal(layout.ShelfMiddle + ScreenLayout.DiscsAboveMiddle * layout.ShelfScale, button.MiddleY, precision: 3);
            Assert.True(button.Left > -layout.ShelfWidth / 2 && button.Right < 0f, "the button does not stand on the left half of the shelf");
            Assert.True(button.Left >= layout.View.Left + 4f && button.Bottom >= layout.View.UsableBottom, "the button leaves the screen");
            Assert.True(button.Top < boardBottom, "the button reaches the board's cells");

            // On a phone the shelf's line still has room beside the button for its lettering at a readable size.
            float lineRoom = layout.ShelfWidth / 2 - 20f - (button.Right + 8f);
            Assert.True(lineRoom >= 200f, $"the shelf's line has only {lineRoom} points beside the button");
        }
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
    public void The_banner_of_a_restored_room_and_the_halls_note_fit_between_the_painting_and_the_bar(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        Box banner = hall.Banner;
        float pinsTop = banner.Top + (HallLayout.BannerNameplateRise + HallLayout.BannerPinsRise) * hall.BannerScale;

        // The plate's shadow and the button's gilt rim reach a few points past their boxes: the banner keeps clear of both.
        Assert.InRange(hall.BannerScale, 0.55f, 1f);
        Assert.True(pinsTop <= hall.Hanging.PlateBottom - 8f, "the pins of the banner's nameplate reach the painting's brass plate");
        Assert.True(banner.Bottom >= hall.Button.Top + 8f, "the banner reaches the button's rim");
        Assert.True(banner.Left >= hall.Page.View.Left + 8f && banner.Right <= hall.Page.View.Right - 8f, "the banner is cut off at a side");

        // The note stands over the button and under the painting's plate, clear of both.
        Assert.True(hall.NoteMiddle - HallLayout.NoteHeight / 2 > hall.Button.Top, "the note lies on the button");
        Assert.True(hall.NoteMiddle + HallLayout.NoteHeight / 2 < hall.Hanging.PlateBottom, "the note reaches the painting's brass plate");
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void The_badge_of_waiting_pieces_lies_wholly_on_the_screen_and_clear_of_the_painting_beside_it(int width, int height, int top, int bottom)
    {
        // Every order of the first room's proportions on the wall, so each shape is tried in each place of each row.
        float[] aspects = { 1.36f, 0.81f, 1.29f, 1.04f, 1.69f };
        for (int turn = 0; turn < aspects.Length; turn++)
        {
            float[] turned = aspects.Skip(turn).Concat(aspects.Take(turn)).ToArray();
            CollectionLayout collection = CollectionLayout.Of(new ScreenShape(width, height, top, bottom), turned);
            ScreenView view = collection.Page.View;
            for (int painting = 0; painting < collection.Exhibits.Count; painting++)
            {
                ExhibitPlace exhibit = collection.Exhibits[painting];
                float left = exhibit.BadgeMiddleX - CollectionLayout.BadgeReach;
                float right = exhibit.BadgeMiddleX + CollectionLayout.BadgeReach;
                float badgeTop = exhibit.BadgeMiddleY + CollectionLayout.BadgeReach;

                Assert.True(right <= view.Right - 2f, $"the badge of painting {painting + 1} ends {view.Right - right} points from the screen's side");
                Assert.True(badgeTop < collection.Page.RailBottom, $"the badge of painting {painting + 1} reaches the picture rail");
                Assert.True(exhibit.BadgeMiddleX <= exhibit.Frame.Right && exhibit.BadgeMiddleX >= exhibit.Frame.MiddleX, "the badge does not sit on the frame's right half");
                Assert.True(exhibit.BadgeMiddleY <= exhibit.Frame.Top && exhibit.BadgeMiddleY >= exhibit.Frame.MiddleY, "the badge does not sit on the frame's upper half");
                foreach (ExhibitPlace other in collection.Exhibits.Where((_, index) => index != painting))
                {
                    bool isBesideIt = other.Frame.Top > exhibit.BadgeMiddleY - CollectionLayout.BadgeReach && other.Frame.Bottom < badgeTop;
                    bool isClear = !isBesideIt || right <= other.Frame.Left || left >= other.Frame.Right;
                    Assert.True(isClear, $"the badge of painting {painting + 1} lies on the frame of another painting");
                }
            }
        }
    }
}
