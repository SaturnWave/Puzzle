using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// The saved progress across versions of the game: the layout written before the museum existed still reads, and
/// gives its player the pieces of the levels they had won; the layouts written before the wallet existed give their
/// player the rules' starting wallet with every life; the layouts written before the tutorial existed give their
/// player the steps of the levels they had won; the current layout keeps the placed pieces, the wallet, its instants
/// to the second, and the steps of the tutorial seen; a later layout and a damaged document are told apart. <c>ProgressDocument</c> is the Unity
/// project's source file, compiled into these tests; here System.Text.Json fills it, in the game Unity's reader does
/// (unity/Assets/Editor/ProgressFileCheck.cs checks that one against a real file).
/// </summary>
public class ProgressDocumentTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly PaintingNumber FourthPainting = new(4);

    /// <summary>What the tests' economy gives a new player: 100 coins, one rocket-and-bomb and two shuffles, every life.</summary>
    private static readonly Wallet StartingWallet = MetaSupport.Rules().StartingWallet;

    /// <summary>What the game before the wallet wrote for a player who had won 39 levels and placed a painting and a half (layout 2).</summary>
    private const string DocumentBeforeTheWallet =
        """{"version":2,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":1,"pieces":[0,1,2,3,4,5,6,7,8,9]},{"painting":4,"pieces":[0,2]}]}""";

    /// <summary>The same player with <see cref="WalletInUse"/> but for its opening gifts, as the game before them wrote it (layout 3).</summary>
    private const string DocumentOfLayoutThree =
        """{"version":3,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":1,"pieces":[0,1,2,3,4,5,6,7,8,9]},{"painting":4,"pieces":[0,2]}],"""
        + "\"wallet\":{\"coins\":1234,\"livesShort\":2,\"nextLifeAtUnixSeconds\":1791970200,"
        + "\"boosters\":[{\"name\":\"start-rocket-and-bomb\",\"count\":0},{\"name\":\"start-color-bomb\",\"count\":2},{\"name\":\"start-extra-moves\",\"count\":0},"
        + """{"name":"hit-cell","count":0},{"name":"hit-row","count":1},{"name":"hit-column","count":0},{"name":"shuffle","count":5}],"""
        + "\"day\":739902,\"adsForMovesToday\":1,\"adsForCoinsToday\":2,\"paintingRewards\":3,\"dailyRewards\":4,\"lastDailyDay\":739901}}";

    /// <summary>The same player with <see cref="WalletInUse"/>, as this game writes it (layout 4): the spatula's and the row brush's opening gifts given.</summary>
    private const string DocumentWithAWallet =
        """{"version":4,"levelsWon":39,"attemptsOnLevel":2,"placed":[{"painting":1,"pieces":[0,1,2,3,4,5,6,7,8,9]},{"painting":4,"pieces":[0,2]}],"""
        + "\"wallet\":{\"coins\":1234,\"livesShort\":2,\"nextLifeAtUnixSeconds\":1791970200,"
        + "\"boosters\":[{\"name\":\"start-rocket-and-bomb\",\"count\":0},{\"name\":\"start-color-bomb\",\"count\":2},{\"name\":\"start-extra-moves\",\"count\":0},"
        + """{"name":"hit-cell","count":0},{"name":"hit-row","count":1},{"name":"hit-column","count":0},{"name":"shuffle","count":5}],"""
        + "\"day\":739902,\"adsForMovesToday\":1,\"adsForCoinsToday\":2,\"paintingRewards\":3,\"dailyRewards\":4,\"lastDailyDay\":739901,"
        + "\"unlockGifts\":[\"hit-cell\",\"hit-row\"]}}";

    /// <summary>The same player with the steps of the tutorial they have seen (<see cref="SeenOnLevelForty"/>), as this game writes it (layout 5).</summary>
    private static readonly string DocumentWithTheTutorial =
        DocumentWithAWallet.Replace("""{"version":4,""", """{"version":5,""", StringComparison.Ordinal)[..^1]
        + ""","tutorial":["goal","match","moves","four-in-a-line","five-in-a-line","box","jelly","first-piece","first-painting"]}""";

    /// <summary>
    /// What a player on level 40 with the first painting whole had seen of the tutorial before the booster intro (#7):
    /// all but the steps of the tape and the frame corners, whose levels come later. Named one by one, so that the
    /// documents pinned here stay what they are as steps are added.
    /// </summary>
    private static readonly TutorialSeen SeenOnLevelForty = TutorialSeen.Of(new[]
    {
        TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves, TutorialStep.FourInALine, TutorialStep.FiveInALine, TutorialStep.Box, TutorialStep.Jelly,
        TutorialStep.FirstPiece, TutorialStep.FirstPainting,
    });

    /// <summary>Noon on 14 October 2026 in Istanbul: 09:00 at Greenwich, day 739 902 of the calendar.</summary>
    private static readonly DateTimeOffset Noon = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3));

    private static ProgressReading Read(string json) => Read(json, StartingWallet);

    private static ProgressReading Read(string json, Wallet startingWallet) =>
        ProgressDocument.Read(JsonSerializer.Deserialize<ProgressDocument>(json, AsUnityMapsIt), startingWallet);

    private static PlayerProgress SoundProgressOf(string json) => Assert.IsType<ProgressReading.Sound>(Read(json)).Progress;

    private static string Written(PlayerProgress progress) => JsonSerializer.Serialize(ProgressDocument.Of(progress), AsUnityMapsIt);

    /// <summary>A progress as a save of a layout before the tutorial reads it: with the steps of the levels it has won and of what it has done in the museum.</summary>
    private static PlayerProgress AsAnOlderSaveReads(PlayerProgress progress) =>
        new(progress.LevelsWon, progress.AttemptsOnLevel, progress.Museum.Placed, progress.Wallet, TutorialPlan.OfAnOlderSave(progress.Museum));

    /// <summary><see cref="PlacedPlayer"/> with <see cref="WalletInUse"/> and the tutorial seen so far (<see cref="SeenOnLevelForty"/>).</summary>
    private static PlayerProgress TutoredPlayer()
    {
        PlayerProgress placed = PlacedPlayer();
        return new PlayerProgress(placed.LevelsWon, placed.AttemptsOnLevel, placed.Museum.Placed, WalletInUse(), SeenOnLevelForty);
    }

    private static PlayerProgress PlacedPlayer() =>
        new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithTrayPlaced(new PaintingNumber(1)).WithPiecePlaced(FourthPainting, 2).WithPiecePlaced(FourthPainting, 0);

    /// <summary>Something in every field: 1 234 coins, two lives short with the next due at 12:30, a few boosters, the day's ads, rewards up to yesterday.</summary>
    private static Wallet WalletInUse() => new(
        new Coins(1234),
        new LifeMeter(shortOfFull: 2, Noon.AddMinutes(30)),
        BoosterShelf.Empty.With(BoosterKind.StartColorBomb, 2).With(BoosterKind.HitRow, 1).With(BoosterKind.Shuffle, 5),
        new AdsWatched(DayStamp.Of(Noon), forExtraMoves: 1, forCoins: 2),
        new RewardsGiven(forPaintings: 3, daily: 4, lastDailyOn: new DayStamp(DayStamp.Of(Noon).Number - 1), BoosterKindSet.Of(new[] { BoosterKind.HitCell, BoosterKind.HitRow })));

    [Fact]
    public void A_file_written_before_the_museum_gives_its_player_the_pieces_of_every_level_they_won_and_a_new_players_wallet()
    {
        // Exactly what the first Android build wrote.
        PlayerProgress read = SoundProgressOf("""{"version":1,"levelsWon":39,"attemptsOnLevel":2}""");

        Assert.Equal(AsAnOlderSaveReads(new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithWallet(StartingWallet)), read);
        Assert.Equal((39, 0, 39), (read.Museum.PiecesWon, read.Museum.PiecesPlaced, read.Museum.PiecesWaiting));
        Assert.Equal(Enumerable.Range(0, 10), read.Museum.WaitingIn(new PaintingNumber(1)));
        Assert.Equal(Enumerable.Range(0, 9), read.Museum.WaitingIn(FourthPainting));
        Assert.True(read.Wallet.Lives.IsFull);
    }

    [Fact]
    public void A_file_written_before_the_wallet_keeps_its_pieces_and_gives_its_player_the_starting_wallet_with_every_life()
    {
        PlayerProgress read = SoundProgressOf(DocumentBeforeTheWallet);

        Assert.Equal(AsAnOlderSaveReads(PlacedPlayer().WithWallet(StartingWallet)), read);
        Assert.True(read.Wallet.Lives.IsFull);
        Assert.Equal((new Coins(100), 1, 2), (read.Wallet.Coins, read.Wallet.Shelf.Count(BoosterKind.StartRocketAndBomb), read.Wallet.Shelf.Count(BoosterKind.Shuffle)));
    }

    [Fact]
    public void A_real_file_of_the_game_before_the_wallet_read_by_the_shipped_rules_gives_the_welcome_gift_and_every_life()
    {
        EconomyRules shipped = EconomyFileMapper.ToRules(JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), AsUnityMapsIt)!);

        PlayerProgress read = Assert.IsType<ProgressReading.Sound>(Read(DocumentBeforeTheWallet, shipped.StartingWallet)).Progress;

        // The welcome gift is what economy/default.json's start section says; no number of it is held here.
        Assert.Equal(shipped.StartingWallet, read.Wallet);
        Assert.Equal(shipped.Lives.Max, read.Wallet.Lives.LivesUnder(shipped.Lives));
        Assert.Equal((AdsWatched.None, RewardsGiven.None), (read.Wallet.Ads, read.Wallet.Rewards));
        Assert.Equal(PlacedPlayer().Museum.Placed, read.Museum.Placed);

        // Saved again, it is in the current layout and reads back the same.
        string written = Written(read);
        Assert.StartsWith("""{"version":5,""", written, StringComparison.Ordinal);
        Assert.Equal(read, Assert.IsType<ProgressReading.Sound>(Read(written, shipped.StartingWallet)).Progress);
    }

    [Fact]
    public void Progress_is_written_in_the_current_layout_with_the_placed_pieces_painting_by_painting_the_wallet_field_by_field_and_the_tutorial_step_by_step()
    {
        PlayerProgress progress = TutoredPlayer();

        string written = Written(progress);

        Assert.Equal(DocumentWithTheTutorial, written);
        Assert.Equal(progress, SoundProgressOf(written));
    }

    [Fact]
    public void The_instant_of_the_next_life_reads_back_to_the_second_and_one_between_two_seconds_never_comes_early()
    {
        // A loss on a clock of the game: the instant is a whole second, and so is every instant the meter counts on from it.
        DateTimeOffset lostAt = new FixedWallClock(Noon.AddSeconds(17)).Now;
        LifeMeter oneLost = LifeMeter.Full.WithOneLost(lostAt, MetaSupport.Rules().Lives);
        PlayerProgress saved = PlacedPlayer().WithWallet(StartingWallet.WithLives(oneLost));

        Assert.Equal(saved, SoundProgressOf(Written(saved)));
        Assert.Equal(lostAt.AddMinutes(30), SoundProgressOf(Written(saved)).Wallet.Lives.NextLifeAt);

        // An instant no clock of the game hands out is written as the second after it.
        DateTimeOffset betweenTwoSeconds = Noon.AddMinutes(30).AddMilliseconds(250);
        PlayerProgress odd = saved.WithWallet(StartingWallet.WithLives(new LifeMeter(1, betweenTwoSeconds)));
        Assert.Equal(Noon.AddMinutes(30).AddSeconds(1), SoundProgressOf(Written(odd)).Wallet.Lives.NextLifeAt);
    }

    [Fact]
    public void What_is_written_reads_back_the_same_all_the_way_through_the_museum()
    {
        PlayerProgress everything = PaintingNumber.All.Aggregate(
            new PlayerProgress(levelsWon: 1000, attemptsOnLevel: 3),
            (progress, painting) => painting.Value % 3 == 0 ? progress : progress.WithTrayPlaced(painting));

        string written = Written(everything);

        Assert.Equal(everything, SoundProgressOf(written));
        Assert.Equal(67, SoundProgressOf(written).Museum.PaintingsCompleted);
    }

    [Fact]
    public void Winning_and_trying_keep_the_placed_pieces_and_the_wallet()
    {
        PlayerProgress placed = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 0).WithPiecePlaced(FourthPainting, 8).WithWallet(WalletInUse());

        PlayerProgress later = placed.WithAttemptStarted().WithLevelWon(levelCount: 1000);

        Assert.Equal(new[] { 8 }, later.Museum.Placed.In(FourthPainting));
        Assert.Equal(40, later.Museum.PiecesWon);
        Assert.Equal(WalletInUse(), later.Wallet);
        Assert.NotEqual(new PlayerProgress(levelsWon: 40, attemptsOnLevel: 0).WithWallet(WalletInUse()), later); // the same place in the levels, another museum
    }

    [Fact]
    public void Whether_a_wallet_is_there_is_decided_by_the_layout_never_by_a_zero()
    {
        // Layout 3 with no coin and every life: no coin is no coin, and not what a new player holds.
        string nothing = DocumentOfLayoutThree
            .Replace("\"coins\":1234", "\"coins\":0", StringComparison.Ordinal)
            .Replace("\"livesShort\":2,\"nextLifeAtUnixSeconds\":1791970200", "\"livesShort\":0,\"nextLifeAtUnixSeconds\":0", StringComparison.Ordinal);
        Assert.Equal(Coins.None, SoundProgressOf(nothing).Wallet.Coins);
        Assert.True(SoundProgressOf(nothing).Wallet.Lives.IsFull);

        // Layout 3 without its wallet is not a wallet of zeros: it is damaged.
        Assert.IsType<ProgressReading.Damaged>(Read("""{"version":3,"levelsWon":39,"attemptsOnLevel":2,"placed":[]}"""));
        Assert.IsType<ProgressReading.Damaged>(Read("""{"version":3,"levelsWon":39,"attemptsOnLevel":2,"placed":[],"wallet":null}"""));

        // Layout 2 with numbers where a later wallet would be: the layout says there is none, and they are not read.
        PlayerProgress older = SoundProgressOf(DocumentOfLayoutThree.Replace("""{"version":3,""", """{"version":2,""", StringComparison.Ordinal));
        Assert.Equal(StartingWallet, older.Wallet);
    }

    [Fact]
    public void A_wallet_of_layout_three_had_every_opening_gift_in_its_welcome_gift_and_is_given_none_again()
    {
        PlayerProgress read = SoundProgressOf(DocumentOfLayoutThree);

        // The game that wrote layout 3 gave three of each booster used during play before the first level.
        Assert.Equal(BoosterKindSet.Of(BoosterKinds.InLevel), read.Wallet.Rewards.UnlockGifts);
        Assert.Equal(WalletInUse().Shelf, read.Wallet.Shelf);
        Assert.Same(read.Wallet, MetaSupport.Rules().Unlocks.GivenTo(read.Wallet, level: 40));
    }

    [Fact]
    public void A_file_before_the_wallet_gives_no_opening_gift_itself_so_the_rule_gives_each_once()
    {
        PlayerProgress read = SoundProgressOf(DocumentBeforeTheWallet);
        EconomyRules rules = MetaSupport.Rules();

        Wallet given = rules.Unlocks.GivenTo(read.Wallet, level: 40);

        Assert.Equal(BoosterKindSet.None, read.Wallet.Rewards.UnlockGifts);
        Assert.Equal(BoosterKindSet.Of(BoosterKinds.InLevel), given.Rewards.UnlockGifts);
        Assert.Equal((3, 2, 1), (given.Shelf.Count(BoosterKind.HitCell), given.Shelf.Count(BoosterKind.HitRow), given.Shelf.Count(BoosterKind.HitColumn)));
        Assert.Equal(given, SoundProgressOf(Written(read.WithWallet(given))).Wallet);
    }

    [Theory]
    [InlineData("[\"hit-cell\",\"hit-cell\"]")] // a gift twice
    [InlineData("[\"start-color-bomb\"]")] // a start booster has no opening gift
    [InlineData("[\"hit-diagonal\"]")] // no booster of this game
    [InlineData("[null]")]
    public void Opening_gifts_no_game_could_have_given_make_the_wallet_damaged(string gifts)
    {
        string json = DocumentWithAWallet.Replace("[\"hit-cell\",\"hit-row\"]", gifts, StringComparison.Ordinal);

        Assert.NotEqual(DocumentWithAWallet, json);
        Assert.IsType<ProgressReading.Damaged>(Read(json));
    }

    [Fact]
    public void A_later_games_document_is_told_apart_and_what_this_game_knows_of_it_is_read()
    {
        string laterGames = DocumentWithTheTutorial.Replace("""{"version":5,""", """{"version":6,""", StringComparison.Ordinal)
            .Replace("\"goal\",", "\"goal\",\"booster-intro\",", StringComparison.Ordinal)[..^1]
            + ""","device":"phone"}""";

        var later = Assert.IsType<ProgressReading.OfALaterGame>(Read(laterGames));

        Assert.Equal(6, later.Version);
        Assert.Equal(TutoredPlayer(), later.Understood); // a step of the later game's is passed over
    }

    [Fact]
    public void Of_a_later_games_document_whose_pieces_or_wallet_this_game_cannot_read_the_players_place_is_still_known()
    {
        var later = Assert.IsType<ProgressReading.OfALaterGame>(
            Read("""{"version":6,"levelsWon":12,"attemptsOnLevel":1,"placed":[{"painting":140,"pieces":[5]}]}"""));
        Assert.Equal(new PlayerProgress(levelsWon: 12, attemptsOnLevel: 1, PlacedPieces.None, StartingWallet, TutorialSeen.None), later.Understood);

        // A booster this game does not know: the wallet is the later game's, and this game plays on the starting one.
        var unknownBooster = Assert.IsType<ProgressReading.OfALaterGame>(
            Read(DocumentWithTheTutorial.Replace("""{"version":5,""", """{"version":6,""", StringComparison.Ordinal).Replace("hit-cell", "hit-diagonal", StringComparison.Ordinal)));
        Assert.Equal(TutoredPlayer().WithWallet(StartingWallet), unknownBooster.Understood);

        var unreadable = Assert.IsType<ProgressReading.OfALaterGame>(Read("""{"version":6,"levelsWon":-12,"attemptsOnLevel":1}"""));
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

    [Theory]
    [InlineData("\"coins\":1234", "\"coins\":-1234")]
    [InlineData("\"livesShort\":2", "\"livesShort\":-2")]
    [InlineData("\"livesShort\":2", "\"livesShort\":0")] // a time for a full meter
    [InlineData("\"nextLifeAtUnixSeconds\":1791970200", "\"nextLifeAtUnixSeconds\":999999999999")] // after the calendar's last day
    [InlineData("\"hit-cell\"", "\"hit-everything\"")]
    [InlineData("\"hit-cell\"", "\"hit-row\"")] // one booster twice and one not at all
    [InlineData("{\"name\":\"hit-cell\",\"count\":0},", "")] // one booster not at all
    [InlineData("\"count\":5", "\"count\":-5")]
    [InlineData("\"day\":739902", "\"day\":-1")]
    [InlineData("\"day\":739902", "\"day\":9999999")] // after the calendar's last day
    [InlineData("\"adsForCoinsToday\":2", "\"adsForCoinsToday\":-2")]
    [InlineData("\"paintingRewards\":3", "\"paintingRewards\":-3")]
    [InlineData("\"dailyRewards\":4", "\"dailyRewards\":0")] // a day for a daily reward never given
    [InlineData("\"boosters\":[", "\"boosters\":[null,")]
    public void A_wallet_that_cannot_be_is_damaged(string held, string instead)
    {
        string json = DocumentWithAWallet.Replace(held, instead, StringComparison.Ordinal);
        Assert.NotEqual(DocumentWithAWallet, json);

        Assert.IsType<ProgressReading.Damaged>(Read(json));
    }

    [Theory]
    [InlineData("""["goal","goal"]""")] // a step twice
    [InlineData("""["booster-intro"]""")] // no step of this game
    [InlineData("""["Goal"]""")] // names are written in small letters
    [InlineData("[null]")]
    public void Steps_of_the_tutorial_no_game_could_have_written_make_the_document_damaged(string steps)
    {
        Assert.IsType<ProgressReading.Damaged>(Read(WithTheTutorial(steps)));
    }

    [Fact]
    public void Whether_a_tutorial_is_there_is_decided_by_the_layout_never_by_an_empty_list()
    {
        // Layout 4 with a list where a later layout keeps the tutorial: the layout says there is none, and it is not read.
        PlayerProgress older = SoundProgressOf(DocumentWithAWallet[..^1] + ""","tutorial":["goal"]}""");

        Assert.Equal(TutorialPlan.OfAnOlderSave(older.Museum), older.Tutorial);
        Assert.Equal(TutorialSeen.None, SoundProgressOf(WithTheTutorial("[]")).Tutorial);
    }

    /// <summary><see cref="DocumentWithTheTutorial"/> with another list of the steps seen.</summary>
    private static string WithTheTutorial(string steps) =>
        DocumentWithTheTutorial[..DocumentWithTheTutorial.IndexOf(""","tutorial":""", StringComparison.Ordinal)] + ",\"tutorial\":" + steps + "}";

    [Fact]
    public void No_document_and_a_document_without_its_lists_are_handled()
    {
        Assert.IsType<ProgressReading.Damaged>(ProgressDocument.Read(null, StartingWallet));
        Assert.Equal(
            AsAnOlderSaveReads(new PlayerProgress(levelsWon: 5, attemptsOnLevel: 0).WithWallet(StartingWallet)),
            SoundProgressOf("""{"version":2,"levelsWon":5,"attemptsOnLevel":0,"placed":null}"""));
        Assert.Equal(
            AsAnOlderSaveReads(new PlayerProgress(levelsWon: 5, attemptsOnLevel: 0).WithWallet(StartingWallet)),
            SoundProgressOf("""{"version":2,"levelsWon":5,"attemptsOnLevel":0,"placed":[{"painting":1,"pieces":null}]}"""));
        Assert.Equal(
            TutoredPlayer().WithTutorialStepSeen(TutorialStep.Chain),
            SoundProgressOf(DocumentWithTheTutorial.Replace("\"jelly\"", "\"jelly\",\"chain\"", StringComparison.Ordinal)));
        Assert.Equal(
            new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2, PlacedPlayer().Museum.Placed, WalletInUse(), TutorialSeen.None),
            SoundProgressOf(DocumentWithTheTutorial.Replace(""","tutorial":[""", ""","was":[""", StringComparison.Ordinal)));
        Assert.IsType<ProgressReading.Damaged>(Read(DocumentWithAWallet.Replace("\"boosters\":[", "\"boosters\":null,\"was\":[", StringComparison.Ordinal)));
    }
}
