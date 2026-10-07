using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Unity game's way from one level to the next: how a player's progress counts wins and attempts, what a decided
/// game tells its result panel, and where that panel hangs on a screen. <c>PlayerProgress</c>, <c>LevelResult</c>,
/// <c>AttemptWords</c> and <c>ScreenLayout</c> are the Unity project's source files, compiled into these tests:
/// they have no Unity dependency.
/// </summary>
public class GameFlowTests
{
    /// <summary>The room the Unity game's result card asks of a screen (ResultPanel.Width and Height), in points.</summary>
    private const float CardWidth = 332f;
    private const float CardHeight = 449f;

    private const int LevelsInThePack = 1000;

    /// <summary>Three purple gems in the fourth row once the one above the third column is pulled down.</summary>
    private static readonly Move TheMatchingMove = Move.Swap(P(2, 2), P(2, 3));

    private static string[] BoardWithAMatchInReach() =>
        Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p');

    private static Game PlayedOut(GoalSpec[] goals, int moves)
    {
        Game game = Start(Level(BoardWithAMatchInReach(), goals: goals, moves: moves));
        Assert.True(game.TryPlay(TheMatchingMove));
        return game;
    }

    [Fact]
    public void A_new_player_stands_on_the_first_level_and_has_not_tried_it()
    {
        Assert.Equal(0, PlayerProgress.None.LevelIndexIn(LevelsInThePack));
        Assert.Equal(0, PlayerProgress.None.AttemptsOnLevel);
        Assert.False(PlayerProgress.None.IsOnLastLevelOf(LevelsInThePack));
    }

    [Fact]
    public void Every_attempt_with_a_move_in_it_takes_the_next_seed_of_the_levels_pool()
    {
        // The seed of an attempt is picked by the count of attempts before it, as the web test server picks it.
        var pool = new uint[] { 11, 22, 33 };
        AttemptPolicy policy = new AttemptPolicy { SeedPool = pool };
        PlayerProgress progress = PlayerProgress.None;

        var dealt = new List<AttemptSeeds>();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            dealt.Add(policy.SeedsFor(progress.AttemptsOnLevel, unpooledSeed: 0));
            progress = progress.WithAttemptStarted();
        }

        Assert.Equal(new[] { AttemptSeeds.From(11), AttemptSeeds.From(22), AttemptSeeds.From(33), AttemptSeeds.From(11) }, dealt);
        Assert.Equal(0, progress.LevelsWon);
    }

    [Fact]
    public void A_win_moves_the_player_to_the_next_level_which_is_untried()
    {
        PlayerProgress afterThreeTries = new PlayerProgress(levelsWon: 39, attemptsOnLevel: 3);

        PlayerProgress won = afterThreeTries.WithLevelWon(LevelsInThePack);

        Assert.Equal(new PlayerProgress(levelsWon: 40, attemptsOnLevel: 0), won);
        Assert.Equal(40, won.LevelIndexIn(LevelsInThePack));
    }

    [Fact]
    public void A_player_who_won_every_level_stands_on_the_last_one_and_winning_it_again_counts_nothing_new()
    {
        PlayerProgress onTheLast = new PlayerProgress(levelsWon: LevelsInThePack - 1, attemptsOnLevel: 1);
        Assert.True(onTheLast.IsOnLastLevelOf(LevelsInThePack));

        PlayerProgress wonAll = onTheLast.WithLevelWon(LevelsInThePack);
        Assert.Equal(LevelsInThePack, wonAll.LevelsWon);
        Assert.Equal(LevelsInThePack - 1, wonAll.LevelIndexIn(LevelsInThePack));
        Assert.True(wonAll.IsOnLastLevelOf(LevelsInThePack));

        Assert.Equal(wonAll, wonAll.WithAttemptStarted().WithLevelWon(LevelsInThePack));
    }

    [Fact]
    public void Progress_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProgress(levelsWon: -1, attemptsOnLevel: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProgress(levelsWon: 0, attemptsOnLevel: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerProgress.None.LevelIndexIn(0));
    }

    [Fact]
    public void A_store_in_memory_gives_back_what_it_was_given_last()
    {
        var store = new ProgressInMemory(PlayerProgress.None);
        var later = new PlayerProgress(levelsWon: 7, attemptsOnLevel: 2);

        store.Save(later);

        Assert.Equal(later, store.Load());
        Assert.Equal(1, store.TimesSaved);
    }

    [Fact]
    public void A_won_game_tells_its_moves_and_the_moves_it_left_over()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        Assert.Equal(GameStatus.Won, game.Status);

        LevelResult result = LevelResult.Of(game, AttemptBrief.FirstTry);

        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Equal(1, won.MovesUsed);
        Assert.Equal(4, won.MovesLeft);
        Assert.Equal("Bölüm tamamlandı!", ResultWords.Title(result));
        Assert.Equal("Bölüm 1 · 1 hamlede", ResultWords.Headline(result));
        Assert.Equal("4 hamle arttı", ResultWords.Footnote(result));
        Assert.Equal("Sonraki bölüm", ResultWords.GoOn(result));
    }

    [Fact]
    public void A_level_won_with_its_last_move_and_the_last_level_of_all_have_words_of_their_own()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 1);

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 2, isOnLastLevel: true));

        Assert.Equal("Son hamlede tamamlandı", ResultWords.Footnote(result));
        Assert.Equal("Tekrar oyna", ResultWords.GoOn(result));
    }

    [Fact]
    public void A_lost_game_tells_which_try_it_was_and_what_was_missing()
    {
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) };
        Game game = PlayedOut(goals, moves: 1);
        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 3, isOnLastLevel: false));

        LevelResult.Lost lost = Assert.IsType<LevelResult.Lost>(result);
        Assert.Equal(LossReason.OutOfMoves, lost.Reason);
        Assert.Equal(3, lost.AttemptNumber);

        // The purple goal was met by the one move; only the red one is still open, with what is left of it.
        var open = Assert.Single(lost.OpenGoals);
        Assert.Equal(PieceArt.GoalRed, open.Icon);
        Assert.Equal(game.Goals[1].Remaining, open.Remaining);
        Assert.Equal("Hamle bitti!", ResultWords.Title(result));
        Assert.Equal("Bölüm 1 · 3. deneme", ResultWords.Headline(result));
        Assert.Equal("Tekrar dene", ResultWords.GoOn(result));
    }

    [Fact]
    public void A_game_that_is_still_played_has_no_result()
    {
        Game game = Start(Level(BoardWithAMatchInReach()));

        Assert.Throws<InvalidOperationException>(() => LevelResult.Of(game, AttemptBrief.FirstTry));
    }

    [Fact]
    public void The_shelf_tells_a_first_try_how_to_play_and_a_later_one_which_try_it_is()
    {
        Assert.Equal(AttemptWords.HowToPlay, AttemptWords.OnTheShelf(AttemptBrief.FirstTry));
        Assert.Equal("3. deneme · taşlar yeniden dizildi", AttemptWords.OnTheShelf(new AttemptBrief(attemptNumber: 3, isOnLastLevel: false)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptBrief(attemptNumber: 0, isOnLastLevel: false));
    }

    public static TheoryData<int, int, int, int> Screens() => new TheoryData<int, int, int, int>
    {
        { 1080, 2340, 0, 0 },
        { 1080, 2340, 110, 60 },
        { 1080, 1920, 0, 0 },
        { 2076, 2152, 0, 0 },
        { 720, 1280, 0, 0 },
        { 1280, 720, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void A_result_card_hangs_between_the_plaques_and_the_shelf_and_inside_the_screen(int width, int height, int topInset, int bottomInset)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height, topInset, bottomInset), new BoardFootprint(9, 9, hasExits: false));

        CardPlace place = layout.PlaceOfACard(CardWidth, CardHeight);

        float top = place.Middle + place.Scale * CardHeight / 2;
        float bottom = place.Middle - place.Scale * CardHeight / 2;
        Assert.InRange(place.Scale, 0.1f, 1f);
        Assert.True(top <= layout.HudBottom, "the card runs under the plaques");
        Assert.True(bottom >= layout.ShelfTop, "the card runs over the shelf");
        Assert.True(place.Scale * CardWidth <= layout.ViewWidth - 2 * ScreenLayout.CardSideMargin + 0.001f, "the card is cut off at the sides");
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(1080, 1920)]
    [InlineData(2076, 2152)]
    public void On_the_screens_the_game_is_made_for_a_result_card_has_its_full_size(int width, int height)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(9, 9, hasExits: false));

        Assert.Equal(1f, layout.PlaceOfACard(CardWidth, CardHeight).Scale);
    }

    [Fact]
    public void A_point_of_the_layout_and_its_pixel_turn_into_each_other()
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(1080, 2340), new BoardFootprint(9, 9, hasExits: false));

        Assert.Equal((0f, 0f), layout.PointAt(540f, 1170f));
        (float pixelX, float pixelY) = layout.PixelOfPoint(-100f, 250f);
        (float x, float y) = layout.PointAt(pixelX, pixelY);
        Assert.Equal(-100f, x, 0.001f);
        Assert.Equal(250f, y, 0.001f);
    }

    [Fact]
    public void What_settles_ends_in_its_place_after_giving_a_little_past_it()
    {
        Assert.Equal(0f, Easing.Settle(0f), 0.0001f);
        Assert.Equal(1f, Easing.Settle(1f), 0.0001f);

        float furthest = Enumerable.Range(0, 101).Max(step => Easing.Settle(step / 100f));
        Assert.InRange(furthest, 1.005f, 1.04f);
    }
}
