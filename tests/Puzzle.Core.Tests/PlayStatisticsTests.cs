using Puzzle.Client.Flow;
using Puzzle.Client.Statistics;

namespace Puzzle.Core.Tests;

/// <summary>
/// The play statistics' gate, its notes and the key it sends through: nothing is handed over before the player's
/// "yes", nothing after it is taken back, the game's start goes first once a run, the notes hold no id, and only a key
/// of the European region is sent through. <c>PlayStatistics</c>, <c>PlayNote</c> and <c>StatisticsKey</c> are the
/// Unity project's source files, compiled into these tests; the gate inside a running session is checked by
/// unity/Assets/Editor/ConsentCheck.cs, and the service's package itself only on a phone (docs/PHONE_TESTS.md F25).
/// </summary>
public class PlayStatisticsTests
{
    private static readonly DeviceSettings NotAsked = DeviceSettings.Default;
    private static readonly DeviceSettings Allowed = DeviceSettings.Default.With(DeviceSetting.Statistics, isOn: true);
    private static readonly DeviceSettings Declined = DeviceSettings.Default.With(DeviceSetting.Statistics, isOn: false);

    /// <summary>What a sink was handed and how it was turned, in order: "on", "off", or a note's name.</summary>
    private sealed class RecordedNotes : IStatisticsSink
    {
        public List<string> Said { get; } = new();

        public List<PlayNote> Notes { get; } = new();

        public void Send(PlayNote note)
        {
            Notes.Add(note);
            Said.Add(note.Name);
        }

        public void Turn(bool isOn) => Said.Add(isOn ? "on" : "off");
    }

    [Fact]
    public void Before_the_question_is_answered_the_sink_is_turned_off_and_handed_nothing()
    {
        var sink = new RecordedNotes();
        PlayStatistics statistics = PlayStatistics.Through(sink);

        statistics.Follow(NotAsked);
        statistics.Note(new PlayNote.LevelStarted(1), NotAsked);
        statistics.Note(new PlayNote.ShopOpened(), NotAsked);

        Assert.Equal(new[] { "off" }, sink.Said);
    }

    [Fact]
    public void Once_allowed_the_start_goes_first_and_then_every_note_in_the_order_it_came()
    {
        var sink = new RecordedNotes();
        PlayStatistics statistics = PlayStatistics.Through(sink);
        statistics.Follow(NotAsked);

        statistics.Follow(Allowed);
        statistics.Note(new PlayNote.LevelStarted(3), Allowed);
        statistics.Note(new PlayNote.LevelEnded(3, isWon: true, movesLeft: 4), Allowed);

        Assert.Equal(new[] { "off", "on", "app_start", "level_start", "level_win" }, sink.Said);
    }

    [Fact]
    public void Taken_back_the_sink_is_turned_off_and_nothing_more_is_handed_over_and_the_start_is_not_noted_twice_in_a_run()
    {
        var sink = new RecordedNotes();
        PlayStatistics statistics = PlayStatistics.Through(sink);
        statistics.Follow(Allowed);

        statistics.Follow(Declined);
        statistics.Note(new PlayNote.LevelStarted(4), Declined);
        statistics.Follow(Allowed);
        statistics.Note(new PlayNote.LevelStarted(5), Allowed);

        Assert.Equal(new[] { "on", "app_start", "off", "on", "level_start" }, sink.Said);
        Assert.Equal(5, Assert.IsType<PlayNote.LevelStarted>(sink.Notes[^1]).Level);
    }

    [Fact]
    public void A_note_follows_the_answer_it_is_given_even_without_a_follow_before_it()
    {
        var sink = new RecordedNotes();
        PlayStatistics statistics = PlayStatistics.Through(sink);

        statistics.Note(new PlayNote.ShopOpened(), Allowed);

        Assert.Equal(new[] { "on", "app_start", "shop_open" }, sink.Said);
    }

    [Fact]
    public void The_game_asks_while_the_question_is_not_answered_and_the_tools_never_ask()
    {
        PlayStatistics game = PlayStatistics.Through(new RecordedNotes());

        Assert.True(game.AsksOn(NotAsked));
        Assert.False(game.AsksOn(Allowed));
        Assert.False(game.AsksOn(Declined));
        Assert.False(PlayStatistics.Nowhere.AsksOn(NotAsked));
    }

    [Fact]
    public void The_notes_carry_their_numbers_and_the_product_alone_and_never_an_id()
    {
        PlayNote[] every =
        {
            new PlayNote.GameStarted(),
            new PlayNote.LevelStarted(12),
            new PlayNote.LevelEnded(12, isWon: true, movesLeft: 3),
            new PlayNote.LevelEnded(12, isWon: false, movesLeft: 0),
            new PlayNote.PaintingCompleted(painting: 7, room: 2),
            new PlayNote.RoomRestored(2),
            new PlayNote.ShopOpened(),
            new PlayNote.PurchaseCompleted("coins.pouch"),
        };

        Assert.Equal(
            new[]
            {
                "app_start", "level_start (level=12)", "level_win (level=12, moves_left=3)", "level_lose (level=12, moves_left=0)",
                "painting_complete (painting=7, room=2)", "room_complete (room=2)", "shop_open", "purchase_complete (product=coins.pouch)",
            },
            every.Select(note => note.ToString()));
        string[] keys = every.SelectMany(note => note.Properties.Keys).Distinct().ToArray();
        Assert.Equal(new[] { "level", "moves_left", "painting", "room", "product" }, keys);
        Assert.All(every.SelectMany(note => note.Properties.Values), value => Assert.True(value is int || value is string));
    }

    [Fact]
    public void A_note_out_of_its_range_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayNote.LevelStarted(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayNote.LevelEnded(1, isWon: false, movesLeft: -1));
        Assert.Throws<ArgumentException>(() => new PlayNote.PurchaseCompleted(string.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A-EU-0000000000")]
    public void An_empty_settings_file_or_the_packages_own_placeholder_is_no_key(string? text)
    {
        Assert.IsType<StatisticsKey.None>(StatisticsKey.Read(text));
    }

    [Fact]
    public void A_key_of_the_european_region_is_sent_through()
    {
        var key = Assert.IsType<StatisticsKey.OfTheEu>(StatisticsKey.Read(" A-EU-1234567890 "));

        Assert.Equal("A-EU-1234567890", key.Value);
    }

    [Theory]
    [InlineData("A-US-1234567890")]
    [InlineData("A-SH-1234567890")]
    [InlineData("A-DEV-1234567890")]
    [InlineData("EU-1234567890")]
    [InlineData("A-EU-")]
    [InlineData("B-EU-1234567890")]
    public void A_key_of_another_region_or_another_form_is_refused_because_the_game_promises_the_european_union(string text)
    {
        Assert.IsType<StatisticsKey.Refused>(StatisticsKey.Read(text));
    }
}
