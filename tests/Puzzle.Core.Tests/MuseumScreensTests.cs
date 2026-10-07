using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the Unity game's museum pages show and say of a player's progress: the room they stand in and its five
/// paintings, the map's floor plan, the pieces a win gives, and the Turkish words for all of it. These are the Unity
/// project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class MuseumScreensTests
{
    private const int LevelCount = 1000;

    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    /// <summary>A player who has won so many levels and put so many of the pieces they gave in place, in the museum's order.</summary>
    private static PlayerProgress Player(int levelsWon, int piecesPlaced) =>
        new PlayerProgress(levelsWon, attemptsOnLevel: 0).WithWaitingPiecesPlaced(piecesPlaced);

    [Fact]
    public void Pieces_are_placed_in_the_museums_order_and_never_more_than_are_waiting()
    {
        PlayerProgress player = Player(levelsWon: 46, piecesPlaced: 42);

        Assert.Equal((42, 4), (player.Museum.PiecesPlaced, player.Museum.PiecesWaiting));
        Assert.Equal(4, player.Museum.PaintingsCompleted);
        Assert.Equal(new[] { 0, 1 }, player.Museum.Placed.In(new PaintingNumber(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => player.WithWaitingPiecesPlaced(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => player.WithWaitingPiecesPlaced(-1));
    }

    [Theory]
    [InlineData(0, 0, 1)]     // a new player restores the first painting
    [InlineData(46, 42, 5)]   // four paintings whole: the fifth is on the wall
    [InlineData(46, 0, 1)]    // nothing placed: the first painting still waits, however far the levels are
    [InlineData(46, 12, 2)]   // the first whole, the second begun
    [InlineData(50, 50, 6)]   // the first room restored: on to the next room's first painting
    [InlineData(40, 40, 5)]   // every piece won is in place: the painting of the next level, though it has no piece yet
    [InlineData(1000, 3000, 100)] // the whole museum restored: its last painting stays in the frame
    public void The_player_restores_the_first_painting_that_is_not_whole_and_never_one_ahead_of_their_levels(int levelsWon, int placed, int painting)
    {
        MuseumProgress museum = Player(levelsWon, placed).Museum;

        MuseumStanding standing = MuseumStanding.Of(museum, TestMuseums.Full);

        Assert.Equal(new PaintingNumber(painting), standing.Painting);
        Assert.Equal(new PaintingNumber(painting).Room, standing.Room);
    }

    [Fact]
    public void A_rooms_wall_shows_its_five_paintings_as_the_progress_has_them()
    {
        PlayerProgress player = Player(levelsWon: 46, piecesPlaced: 42);

        RoomWall room = RoomWall.Of(new RoomNumber(1), player.Museum, Shipped.Value);

        Assert.Equal("Empresyonizm", room.Name);
        Assert.Equal(4, room.PaintingsWhole);
        Assert.Equal(new[] { true, true, true, true, false }, room.Paintings.Select(painting => painting.IsWhole));
        HungPainting fifth = room.Painting(new PaintingNumber(5));
        Assert.Equal("Buğday Yığınları (Yaz Sonu)", fifth.Artwork?.Caption.Title);
        Assert.Equal(new[] { 0, 1 }, fifth.PiecesInPlace);
        Assert.Equal((4, 10), (fifth.PiecesWaiting, fifth.PieceCount));
        Assert.Equal(0.2f, fifth.ShareInPlace, precision: 5);
        Assert.Throws<ArgumentException>(() => room.Painting(new PaintingNumber(6)));
    }

    [Fact]
    public void A_room_whose_paintings_are_not_chosen_yet_is_shown_with_empty_places()
    {
        RoomWall room = RoomWall.Of(new RoomNumber(2), Player(levelsWon: 50, piecesPlaced: 50).Museum, Shipped.Value);

        Assert.Equal("Post-empresyonizm", room.Name);
        Assert.All(room.Paintings, painting => Assert.Null(painting.Artwork));
        Assert.Equal("6. tablo", MuseumWords.TitleOf(room.Paintings[0].Number, room.Paintings[0].Artwork));
        Assert.Equal("HAZIRLANIYOR", MuseumWords.ByLineOf(room.Paintings[0].Artwork));
    }

    [Fact]
    public void The_plate_under_a_painting_names_it_and_its_painter_in_capitals_as_the_painter_wrote_them()
    {
        Artwork twoSisters = Shipped.Value.ArtworkAt(new PaintingNumber(2))!;

        Assert.Equal("İki Kız Kardeş (Terasta)", MuseumWords.TitleOf(new PaintingNumber(2), twoSisters));
        Assert.Equal("PIERRE-AUGUSTE RENOIR · 1881", MuseumWords.ByLineOf(twoSisters));
    }

    [Fact]
    public void The_pages_say_where_the_player_is_in_the_designs_words()
    {
        Assert.Equal("ODA 1 / 20", MuseumWords.RoomOfRooms(new RoomNumber(1)));
        Assert.Equal("4 / 5 ONARILDI", MuseumWords.Restored(4));
        Assert.Equal("4 / 5 TABLO", MuseumWords.PaintingsWhole(4));
        Assert.Equal("Bölüm 47", MuseumWords.Play(47));
        Assert.Equal("Buradasın · Bölüm 47", MuseumWords.YouAreHere(47));
        Assert.Equal("Bölüm 51'de açılır", MuseumWords.OpensAt(new RoomNumber(2)));
        Assert.Equal("20 ODA · 100 TABLO", MuseumWords.MapSummary);
        Assert.Equal("7–20", MuseumWords.Rooms(new RoomNumber(7), new RoomNumber(20)));
        Assert.Equal("20", MuseumWords.Rooms(new RoomNumber(20), new RoomNumber(20)));
    }

    [Theory]
    [InlineData(1, "1'de")]
    [InlineData(3, "3'te")]
    [InlineData(4, "4'te")]
    [InlineData(5, "5'te")]
    [InlineData(6, "6'da")]
    [InlineData(9, "9'da")]
    [InlineData(10, "10'da")]
    [InlineData(20, "20'de")]
    [InlineData(30, "30'ta")]
    [InlineData(40, "40'ta")]
    [InlineData(51, "51'de")]
    [InlineData(60, "60'ta")]
    [InlineData(70, "70'te")]
    [InlineData(90, "90'da")]
    [InlineData(100, "100'de")]
    [InlineData(101, "101'de")]
    [InlineData(951, "951'de")]
    [InlineData(1000, "1000'de")]
    public void A_number_takes_the_suffix_of_the_word_it_is_read_as(int number, string withSuffix)
    {
        Assert.Equal(withSuffix, TurkishNumbers.In(number));
    }

    [Fact]
    public void Numbers_the_game_never_counts_to_have_no_suffix()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TurkishNumbers.In(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurkishNumbers.In(1_000_000));
    }

    [Theory]
    [InlineData(46, 5, new[] { 6 }, 7, 10, "Yeni parça: 7 / 10")]
    [InlineData(100, 11, new[] { 0, 1 }, 2, 20, "2 yeni parça: 2 / 20")]
    [InlineData(640, 65, new[] { 0, 1, 2, 3 }, 4, 40, "4 yeni parça: 4 / 40")]
    [InlineData(999, 100, new[] { 36, 37, 38, 39 }, 40, 40, "4 yeni parça: 40 / 40")]
    public void Winning_the_level_a_player_stands_on_gives_that_levels_pieces(int levelsWon, int painting, int[] pieces, int wonWithThem, int pieceCount, string words)
    {
        PieceReward? reward = PieceReward.ForWinning(new PlayerProgress(levelsWon, attemptsOnLevel: 3), LevelCount, TestMuseums.Full);

        Assert.NotNull(reward);
        Assert.Equal(new PaintingNumber(painting), reward.Painting);
        Assert.Equal(pieces, reward.Pieces);
        Assert.Equal((wonWithThem, pieceCount), (reward.WonWithThem, reward.PieceCount));
        Assert.Equal(words, MuseumWords.Earned(reward));
    }

    [Fact]
    public void A_level_that_was_won_before_gives_nothing_again()
    {
        Assert.Null(PieceReward.ForWinning(new PlayerProgress(levelsWon: 1000, attemptsOnLevel: 0), LevelCount, TestMuseums.Full));
        Assert.Null(PieceReward.ForWinning(new PlayerProgress(levelsWon: 12, attemptsOnLevel: 0), levelCount: 12, TestMuseums.Full));
        Assert.Throws<ArgumentOutOfRangeException>(() => PieceReward.ForWinning(PlayerProgress.None, levelCount: 0, TestMuseums.Full));
    }

    [Fact]
    public void What_a_win_gives_is_what_the_progress_then_holds()
    {
        var before = new PlayerProgress(levelsWon: 300, attemptsOnLevel: 1);
        PieceReward reward = PieceReward.ForWinning(before, LevelCount, TestMuseums.Full)!;

        PlayerProgress after = before.WithLevelWon(LevelCount);

        Assert.Equal(before.Museum.WaitingIn(reward.Painting).Concat(reward.Pieces), after.Museum.WaitingIn(reward.Painting));
    }

    [Fact]
    public void A_visit_knows_the_level_to_play_and_where_the_player_stands()
    {
        var visit = new MuseumVisit(Player(levelsWon: 50, piecesPlaced: 50).Museum, TestMuseums.Full, levelToPlay: 51);

        Assert.Equal((51, new PaintingNumber(6), new RoomNumber(2)), (visit.LevelToPlay, visit.Painting, visit.Room));
        Assert.Equal(new PaintingNumber(6), visit.BeingRestored);
        Assert.False(visit.HasWonEveryLevel);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MuseumVisit(MuseumProgress.None, TestMuseums.Full, levelToPlay: 0));
    }

    [Fact]
    public void The_floor_plan_of_the_design_is_a_player_in_the_first_room()
    {
        FloorPlan plan = FloorPlan.Of(new RoomNumber(1), new PlanDepth(showsTheRoomBefore: true, rowsAhead: 2));

        Assert.Null(plan.Behind);
        Assert.Null(plan.Before);
        Assert.Equal(new RoomNumber(2), plan.Next);
        Assert.Equal(new[] { 3, 4, 5, 6 }, plan.Ahead.Select(room => room.Value));
        Assert.Equal(2, plan.RowsAhead);
        Assert.Equal((7, 20), (plan.Beyond!.Value.First.Value, plan.Beyond.Value.Last.Value));
    }

    public static TheoryData<int, bool, int> PlansOfEveryRoomAndDepth()
    {
        var cases = new TheoryData<int, bool, int>();
        for (int here = 1; here <= MuseumPlan.RoomCount; here++)
        {
            foreach (bool showsBefore in new[] { false, true })
            {
                for (int rowsAhead = 0; rowsAhead <= FloorPlan.MostRowsAhead; rowsAhead++)
                {
                    cases.Add(here, showsBefore, rowsAhead);
                }
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(PlansOfEveryRoomAndDepth))]
    public void Every_room_of_the_museum_is_on_the_plan_exactly_once_and_in_order(int here, bool showsBefore, int rowsAhead)
    {
        FloorPlan plan = FloorPlan.Of(new RoomNumber(here), new PlanDepth(showsBefore, rowsAhead));

        var rooms = new List<int>();
        void AddStretch(RoomStretch? stretch)
        {
            if (stretch is RoomStretch some)
            {
                rooms.AddRange(Enumerable.Range(some.First.Value, some.Last.Value - some.First.Value + 1));
            }
        }

        AddStretch(plan.Behind);
        rooms.AddRange(new[] { plan.Before, plan.Here, plan.Next }.Where(room => room.HasValue).Select(room => room!.Value.Value));
        rooms.AddRange(plan.Ahead.Select(room => room.Value));
        AddStretch(plan.Beyond);

        Assert.Equal(Enumerable.Range(1, MuseumPlan.RoomCount), rooms);
        Assert.True(plan.Ahead.Count <= rowsAhead * FloorPlan.RoomsInARow);
    }

    [Fact]
    public void A_room_is_restored_open_or_locked_and_a_stretch_is_as_far_as_its_last_room()
    {
        MuseumProgress museum = Player(levelsWon: 120, piecesPlaced: 50).Museum;

        MuseumCatalogue full = TestMuseums.Full;

        Assert.Equal(RoomAccess.Restored, FloorPlan.AccessOf(new RoomNumber(1), museum, full));
        Assert.Equal(RoomAccess.Open, FloorPlan.AccessOf(new RoomNumber(2), museum, full));
        Assert.Equal(RoomAccess.Open, FloorPlan.AccessOf(new RoomNumber(3), museum, full));
        Assert.Equal(RoomAccess.Locked, FloorPlan.AccessOf(new RoomNumber(4), museum, full));
        Assert.Equal(RoomAccess.Restored, FloorPlan.AccessOf(new RoomStretch(new RoomNumber(1), new RoomNumber(1)), museum, full));
        Assert.Equal(RoomAccess.Open, FloorPlan.AccessOf(new RoomStretch(new RoomNumber(1), new RoomNumber(3)), museum, full));
        Assert.Equal(RoomAccess.Locked, FloorPlan.AccessOf(new RoomStretch(new RoomNumber(3), new RoomNumber(20)), museum, full));
        Assert.Throws<ArgumentException>(() => new RoomStretch(new RoomNumber(3), new RoomNumber(2)));
    }
}
