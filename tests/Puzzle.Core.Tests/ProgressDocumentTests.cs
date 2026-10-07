using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// The saved progress across versions of the game: the layout written before the museum existed still reads, and
/// gives its player the pieces of the levels they had won; the current layout keeps the placed pieces; a later
/// layout and a damaged document are told apart. <c>ProgressDocument</c> is the Unity project's source file,
/// compiled into these tests; here System.Text.Json fills it, in the game Unity's reader does
/// (unity/Assets/Editor/ProgressFileCheck.cs checks that one against a real file).
/// </summary>
public class ProgressDocumentTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly PaintingNumber FourthPainting = new(4);

    private static ProgressReading Read(string json) =>
        ProgressDocument.Read(JsonSerializer.Deserialize<ProgressDocument>(json, AsUnityMapsIt));

    private static PlayerProgress SoundProgressOf(string json) => Assert.IsType<ProgressReading.Sound>(Read(json)).Progress;

    [Fact]
    public void A_file_written_before_the_museum_gives_its_player_the_pieces_of_every_level_they_won()
    {
        // Exactly what the first Android build wrote.
        PlayerProgress read = SoundProgressOf("""{"version":1,"levelsWon":39,"attemptsOnLevel":2}""");

        Assert.Equal(new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2), read);
        Assert.Equal((39, 0, 39), (read.Museum.PiecesWon, read.Museum.PiecesPlaced, read.Museum.PiecesWaiting));
        Assert.Equal(Enumerable.Range(0, 10), read.Museum.WaitingIn(new PaintingNumber(1)));
        Assert.Equal(Enumerable.Range(0, 9), read.Museum.WaitingIn(FourthPainting));
    }

    [Fact]
    public void Progress_is_written_in_the_current_layout_with_the_placed_pieces_painting_by_painting()
    {
        PlayerProgress progress = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2)
            .WithTrayPlaced(new PaintingNumber(1))
            .WithPiecePlaced(FourthPainting, 2)
            .WithPiecePlaced(FourthPainting, 0);

        string written = JsonSerializer.Serialize(ProgressDocument.Of(progress), AsUnityMapsIt);

        Assert.Equal(
            """{"version":2,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":1,"pieces":[0,1,2,3,4,5,6,7,8,9]},{"painting":4,"pieces":[0,2]}]}""",
            written);
        Assert.Equal(progress, SoundProgressOf(written));
    }

    [Fact]
    public void What_is_written_reads_back_the_same_all_the_way_through_the_museum()
    {
        PlayerProgress everything = PaintingNumber.All.Aggregate(
            new PlayerProgress(levelsWon: 1000, attemptsOnLevel: 3),
            (progress, painting) => painting.Value % 3 == 0 ? progress : progress.WithTrayPlaced(painting));

        string written = JsonSerializer.Serialize(ProgressDocument.Of(everything), AsUnityMapsIt);

        Assert.Equal(everything, SoundProgressOf(written));
        Assert.Equal(67, SoundProgressOf(written).Museum.PaintingsCompleted);
    }

    [Fact]
    public void Winning_and_trying_keep_the_placed_pieces()
    {
        PlayerProgress placed = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 0).WithPiecePlaced(FourthPainting, 8);

        PlayerProgress later = placed.WithAttemptStarted().WithLevelWon(levelCount: 1000);

        Assert.Equal(new[] { 8 }, later.Museum.Placed.In(FourthPainting));
        Assert.Equal(40, later.Museum.PiecesWon);
        Assert.NotEqual(new PlayerProgress(levelsWon: 40, attemptsOnLevel: 0), later); // the same place in the levels, another museum
    }

    [Fact]
    public void A_later_games_document_is_told_apart_and_what_this_game_knows_of_it_is_read()
    {
        var later = Assert.IsType<ProgressReading.OfALaterGame>(
            Read("""{"version":3,"levelsWon":12,"attemptsOnLevel":1,"placed":[{"painting":1,"pieces":[5]}],"coins":250}"""));

        Assert.Equal(3, later.Version);
        Assert.Equal(new PlayerProgress(levelsWon: 12, attemptsOnLevel: 1).WithPiecePlaced(new PaintingNumber(1), 5), later.Understood);
    }

    [Fact]
    public void Of_a_later_games_document_whose_pieces_this_game_cannot_read_the_players_place_is_still_known()
    {
        var later = Assert.IsType<ProgressReading.OfALaterGame>(
            Read("""{"version":3,"levelsWon":12,"attemptsOnLevel":1,"placed":[{"painting":140,"pieces":[5]}]}"""));
        Assert.Equal(new PlayerProgress(levelsWon: 12, attemptsOnLevel: 1), later.Understood);

        var unreadable = Assert.IsType<ProgressReading.OfALaterGame>(Read("""{"version":3,"levelsWon":-12,"attemptsOnLevel":1}"""));
        Assert.Null(unreadable.Understood);
    }

    [Theory]
    [InlineData("""{"levelsWon":3,"attemptsOnLevel":1}""")] // no layout named
    [InlineData("""{"version":0,"levelsWon":3,"attemptsOnLevel":1}""")]
    [InlineData("""{"version":2,"levelsWon":-4,"attemptsOnLevel":0}""")]
    [InlineData("""{"version":1,"levelsWon":3,"attemptsOnLevel":-1}""")]
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":2,"pieces":[2]}]}""")] // twelve levels give pieces 0 and 1 of the second painting
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":1,"pieces":[3,3]}]}""")] // a piece twice
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":1,"pieces":[10]}]}""")] // ten pieces: 0 to 9
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":1,"pieces":[-1]}]}""")]
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":0,"pieces":[0]}]}""")]
    [InlineData("""{"version":2,"levelsWon":12,"attemptsOnLevel":0,"placed":[{"painting":101,"pieces":[0]}]}""")]
    public void A_document_that_says_what_no_progress_can_be_is_damaged(string json)
    {
        Assert.IsType<ProgressReading.Damaged>(Read(json));
    }

    [Fact]
    public void No_document_and_a_document_without_its_lists_are_handled()
    {
        Assert.IsType<ProgressReading.Damaged>(ProgressDocument.Read(null));
        Assert.Equal(
            new PlayerProgress(levelsWon: 5, attemptsOnLevel: 0),
            SoundProgressOf("""{"version":2,"levelsWon":5,"attemptsOnLevel":0,"placed":null}"""));
        Assert.Equal(
            new PlayerProgress(levelsWon: 5, attemptsOnLevel: 0),
            SoundProgressOf("""{"version":2,"levelsWon":5,"attemptsOnLevel":0,"placed":[{"painting":1,"pieces":null}]}"""));
    }
}
