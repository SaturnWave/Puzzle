using System.IO;
using System.IO.Compression;
using System.Text;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Purchases;
using Puzzle.Meta;
using Puzzle.Museum;
using Xunit.Abstractions;
using static Puzzle.Core.Tests.CloudSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The save's envelope in the cloud (docs/research/server-v1.md §3.2): what it says besides the progress, the
/// progress packed from 16 KB of JSON on (measured here with a player at the end of the museum), a later game's save
/// told apart and never written over, and a damaged one told apart from it.
/// </summary>
public class CloudEnvelopeTests
{
    private readonly ITestOutputHelper _output;

    public CloudEnvelopeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static CloudSaveReading Read(string text) => CloudEnvelope.Read(text, Json, Rules.StartingWallet);

    /// <summary>
    /// A player who has won all 1 000 levels and placed every piece, with a full wallet, the 64 payments' ids the
    /// record keeps (each as long as a Google Play purchase token, the longest a store gives) and 200 ledger numbers.
    /// </summary>
    private static PlayerProgress AtTheEndOfTheMuseum()
    {
        PlayerProgress won = new PlayerProgress(levelsWon: 1000, attemptsOnLevel: 3, PlacedPieces.None, Rules.StartingWallet, TutorialSeen.All);
        PlayerProgress placed = won.WithWaitingPiecesPlaced(won.Museum.PiecesWaiting);
        PurchasesMade made = PurchasesMade.None;
        var random = new Random(1000);
        const string TokenLetters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.";
        for (int payment = 1; payment <= 200; payment++)
        {
            string token = new(Enumerable.Range(0, 150).Select(_ => TokenLetters[random.Next(TokenLetters.Length)]).ToArray());
            made = made.WithDelivered("coins.small", token).WithLedgerNumber(new LedgerNumber(payment));
        }

        var wallet = new Wallet(
            new Coins(123_456_789),
            new LifeMeter(shortOfFull: 3, Noon.AddMinutes(17)),
            BoosterKinds.All.Aggregate(BoosterShelf.Empty, (shelf, kind) => shelf.With(kind, 999)),
            new AdsWatched(DayStamp.Of(Noon), forExtraMoves: 3, forCoins: 2),
            new RewardsGiven(forPaintings: 100, daily: 365, lastDailyOn: DayStamp.Of(Noon), BoosterKindSet.Of(BoosterKinds.InLevel)));
        return placed.WithWallet(wallet).WithPurchases(made);
    }

    [Fact]
    public void A_save_reads_back_with_its_progress_and_what_it_says_besides()
    {
        PlayerProgress progress = NewPlayer().Won(12).Bought(Small, "GPA.1", number: 1);
        var save = new CloudSave(progress, "1.1.0", revision: 42, Noon, "writer-1", new[] { "w-40", "w-41", "w-42" });

        string text = CloudEnvelope.Write(save, Json);
        CloudSave read = Assert.IsType<CloudSaveReading.Sound>(Read(text)).Save;

        Assert.StartsWith("""{"envelope":1,"progressLayout":7,"appVersion":"1.1.0","revision":42,"writtenAtUtc":"2026-11-03T09:00:00Z","writer":"writer-1","writes":["w-40","w-41","w-42"],"progress":{"version":7,""", text, StringComparison.Ordinal);
        Assert.Equal(progress, read.Progress);
        Assert.Equal((42L, Noon, "writer-1", "w-42"), (read.Revision, read.WrittenAt!.Value, read.Writer, read.LastWrite));
    }

    [Fact]
    public void A_save_names_only_its_latest_writes()
    {
        var save = new CloudSave(NewPlayer(), "1.1.0", revision: 30, Noon, "w", Enumerable.Range(1, 30).Select(n => $"write-{n}"));

        Assert.Equal(CloudSave.WritesKept, save.Writes.Count);
        Assert.Equal("write-15", save.Writes[0]);
        Assert.Equal("write-30", save.LastWrite);
    }

    /// <summary>
    /// The plan asked the first stage to measure a real player at the end of the museum: their progress is over 16 KB
    /// of JSON (about 21 KB, half of it the payments' tokens) and is packed to well under its size; the tokens, random
    /// as the store's are, pack least.
    /// </summary>
    [Fact]
    public void A_player_at_the_end_of_the_museum_is_packed_and_reads_back_the_same()
    {
        PlayerProgress progress = AtTheEndOfTheMuseum();
        int plain = Encoding.UTF8.GetByteCount(Json.Write(ProgressDocument.Of(progress)));

        string text = CloudEnvelope.Write(new CloudSave(progress, "1.1.0", 7, Noon, "w", new[] { "w-7" }), Json);

        _output.WriteLine($"The progress of a player at level 1000: {plain} bytes of JSON; the save in the cloud: {Encoding.UTF8.GetByteCount(text)} bytes.");
        Assert.True(plain >= CloudEnvelope.PackFromBytes, $"{plain} bytes");
        Assert.Contains("\"progressGz\":\"H4sI", text, StringComparison.Ordinal); // gzip's mark, in base64
        Assert.InRange(Encoding.UTF8.GetByteCount(text), 1, plain * 3 / 4);
        Assert.Equal(progress, Assert.IsType<CloudSaveReading.Sound>(Read(text)).Save.Progress);
    }

    [Fact]
    public void A_progress_under_16_KB_is_not_packed()
    {
        string text = CloudEnvelope.Write(new CloudSave(NewPlayer().Won(300), "1.1.0", 1, Noon, "w", new[] { "w-1" }), Json);

        Assert.Contains("\"progressGz\":\"\"", text, StringComparison.Ordinal);
        Assert.Contains("\"progress\":{\"version\":7", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_later_games_save_is_told_apart_with_what_this_game_can_read_of_it()
    {
        string ours = SaveText(NewPlayer().Won(312));
        string laterProgress = ours.Replace("\"progress\":{\"version\":7", "\"progress\":{\"version\":8", StringComparison.Ordinal).Replace("\"progressLayout\":7", "\"progressLayout\":8", StringComparison.Ordinal);
        string laterEnvelope = ours.Replace("\"envelope\":1", "\"envelope\":2", StringComparison.Ordinal);

        var later = Assert.IsType<CloudSaveReading.OfALaterGame>(Read(laterProgress));
        var envelope = Assert.IsType<CloudSaveReading.OfALaterGame>(Read(laterEnvelope));

        Assert.Equal((8, 312), (later.ProgressLayout, later.Understood!.LevelsWon));
        Assert.Equal(2, envelope.Envelope);
        Assert.Null(envelope.Understood); // a later envelope is not opened
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a save")]
    [InlineData("""{"progress":{"version":7,"levelsWon":3,"attemptsOnLevel":0}}""")] // no envelope
    [InlineData("""{"envelope":1,"revision":1,"progress":{"version":2,"levelsWon":-4,"attemptsOnLevel":0}}""")] // a progress no game writes
    [InlineData("""{"envelope":1,"revision":1,"progressGz":"not base64!"}""")]
    [InlineData("""{"envelope":1,"revision":1,"progressGz":"bm90IGd6aXA="}""")] // base64 of "not gzip"
    [InlineData("""{"envelope":1,"revision":-1,"progress":{"version":2,"levelsWon":4,"attemptsOnLevel":0}}""")]
    public void A_text_no_game_writes_is_damaged(string text)
    {
        Assert.IsType<CloudSaveReading.Damaged>(Read(text));
    }

    [Fact]
    public void A_packed_progress_that_unpacks_to_more_than_a_players_data_can_be_is_damaged_and_never_read_on()
    {
        using var packed = new MemoryStream();
        using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] spaces = Encoding.UTF8.GetBytes(new string(' ', 1024 * 1024));
            for (int megabyte = 0; megabyte < 6; megabyte++)
            {
                gzip.Write(spaces, 0, spaces.Length);
            }
        }

        string text = $$"""{"envelope":1,"revision":1,"progressGz":"{{Convert.ToBase64String(packed.ToArray())}}"}""";

        Assert.IsType<CloudSaveReading.Damaged>(Read(text));
    }

    [Fact]
    public void A_save_whose_time_or_version_does_not_read_is_still_read_they_are_for_support()
    {
        string text = SaveText(NewPlayer().Won(3)).Replace("\"writtenAtUtc\":\"2026-11-03T09:00:00Z\"", "\"writtenAtUtc\":\"yesterday\"", StringComparison.Ordinal)
            .Replace("\"appVersion\":\"1.1.0\"", "\"appVersion\":\"\"", StringComparison.Ordinal);

        CloudSave read = Assert.IsType<CloudSaveReading.Sound>(Read(text)).Save;

        Assert.Null(read.WrittenAt);
        Assert.Equal("unknown", read.AppVersion);
        Assert.Equal(3, read.Progress.LevelsWon);
    }
}
