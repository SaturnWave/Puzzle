using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
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
        Assert.Equal("Devam", ResultWords.GoOn(result));
    }

    [Fact]
    public void A_won_games_card_tells_its_moves_and_the_moves_it_left_over_also_after_its_finale()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);

        FinaleOutcome finale = game.PlayFinale();

        // The finale turns the four moves left into rockets; the card still says what the player left over.
        Assert.Equal(4, finale.RocketsFired);
        LevelResult result = LevelResult.Of(game, AttemptBrief.FirstTry);
        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Equal((1, 4), (won.MovesUsed, won.MovesLeft));
        Assert.Equal("Bölüm 1 · 1 hamlede", ResultWords.Headline(result));
        Assert.Equal("4 hamle arttı", ResultWords.Footnote(result));
    }

    [Fact]
    public void The_plaque_counts_one_move_down_for_each_rocket_and_never_warns_of_few_moves_in_the_finale()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 6);
        HudFigures won = HudFigures.Of(game);
        Assert.Equal((5, false), (won.MovesLeft, won.MovesAreFew));

        HudFigures figures = won;
        var shown = new List<int>();
        for (int rocket = 0; rocket < 7; rocket++)
        {
            figures = figures.WithMovesTurnedIntoRockets(1);
            shown.Add(figures.MovesLeft);

            // A move counted down while the game is played turns the number red at three; a decided game has nothing to warn of.
            Assert.False(figures.MovesAreFew);
        }

        Assert.Equal(new[] { 4, 3, 2, 1, 0, 0, 0 }, shown);
        Assert.Same(won.Goals, figures.Goals);
        Assert.Throws<ArgumentOutOfRangeException>(() => figures.WithMovesTurnedIntoRockets(-1));
    }

    [Fact]
    public void After_a_finale_that_made_a_rocket_of_every_move_the_plaque_shows_none_left_and_every_goal_still_met()
    {
        // Purple keeps falling in and the rockets keep bursting it: the goal must stay where the winning move left it.
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 21);
        HudFigures won = HudFigures.Of(game);

        FinaleOutcome finale = game.PlayFinale();

        // The game goes on saying what was left; the plaque is what the player saw counted down, one move at a time.
        HudFigures ofTheGame = HudFigures.Of(game).WithMovesTurnedIntoRockets(finale.RocketsFired);
        HudFigures countedDown = Enumerable.Range(0, finale.RocketsFired).Aggregate(won, (figures, _) => figures.WithMovesTurnedIntoRockets(1));
        Assert.Equal(20, HudFigures.Of(game).MovesLeft);
        Assert.Equal(20, finale.RocketsFired);
        Assert.Equal(0, ofTheGame.MovesLeft);
        Assert.Equal(won.Goals.Select(goal => goal.Remaining), ofTheGame.Goals.Select(goal => goal.Remaining));
        Assert.All(ofTheGame.Goals, goal => Assert.Equal(0, goal.Remaining));
        Assert.Empty(countedDown.DifferencesFrom(ofTheGame));
    }

    [Fact]
    public void A_board_that_ran_out_of_gems_leaves_the_moves_no_rocket_was_made_of_on_the_plaque_without_a_warning()
    {
        // The engine stops when a board has no free gem left: it then made fewer rockets than moves were left.
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 21);
        HudFigures won = HudFigures.Of(game);

        HudFigures ofTheGame = won.WithMovesTurnedIntoRockets(18);
        HudFigures countedDown = Enumerable.Range(0, 18).Aggregate(won, (figures, _) => figures.WithMovesTurnedIntoRockets(1));

        Assert.Equal((2, false, 18), (ofTheGame.MovesLeft, ofTheGame.MovesAreFew, ofTheGame.MovesTurnedIntoRockets));
        Assert.Empty(countedDown.DifferencesFrom(ofTheGame));
        Assert.Equal(
            new[] { "plaques: 0 moves left, the engine has 2", "plaques: 20 moves were counted down into rockets, the engine made 18" },
            countedDown.WithMovesTurnedIntoRockets(2).DifferencesFrom(ofTheGame));
    }

    [Fact]
    public void A_move_counted_down_once_too_often_or_too_seldom_in_the_finale_is_told_although_the_number_on_the_plaque_stops_at_none()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 6);
        HudFigures won = HudFigures.Of(game);
        HudFigures ofTheGame = won.WithMovesTurnedIntoRockets(5);
        HudFigures CountedDown(int times) => Enumerable.Range(0, times).Aggregate(won, (figures, _) => figures.WithMovesTurnedIntoRockets(1));

        // Six moves thrown where five were left: the number reads 0 either way, and only the count tells.
        Assert.Equal(ofTheGame.MovesLeft, CountedDown(6).MovesLeft);
        Assert.Equal(new[] { "plaques: 6 moves were counted down into rockets, the engine made 5" }, CountedDown(6).DifferencesFrom(ofTheGame));
        Assert.Equal(
            new[] { "plaques: 1 moves left, the engine has 0", "plaques: 4 moves were counted down into rockets, the engine made 5" },
            CountedDown(4).DifferencesFrom(ofTheGame));
        Assert.Empty(CountedDown(5).DifferencesFrom(ofTheGame));
    }

    [Fact]
    public void The_plaques_are_compared_with_the_game_by_its_moves_and_by_what_is_left_of_every_goal()
    {
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 9), new GoalSpec.CollectColor(GemColor.Red, 2) };
        Game game = PlayedOut(goals, moves: 6);
        HudFigures ofTheGame = HudFigures.Of(game);
        HudFigures before = new HudFigures(6, movesAreFew: false, new[] { new GoalFigure(ofTheGame.Goals[0].Icon, 9), ofTheGame.Goals[1] });

        // The move's three purple gems, counted one by one as they burst, and the move itself.
        HudFigures counted = before.AfterAMove().WithOneCounted(0).WithOneCounted(0).WithOneCounted(0);

        Assert.Equal(6, ofTheGame.Goals[0].Remaining);
        Assert.Empty(counted.DifferencesFrom(ofTheGame));
        Assert.Equal(new[] { "plaques: 6 moves left, the engine has 5" }, before.WithOneCounted(0).WithOneCounted(0).WithOneCounted(0).DifferencesFrom(ofTheGame));
        Assert.Equal(new[] { "plaques: 7 left of goal 1, the engine has 6" }, before.AfterAMove().WithOneCounted(0).WithOneCounted(0).DifferencesFrom(ofTheGame));
        Assert.Throws<ArgumentException>(() => counted.DifferencesFrom(new HudFigures(5, movesAreFew: false, Array.Empty<GoalFigure>())));
    }

    [Fact]
    public void A_won_game_that_gave_a_jigsaw_piece_says_so_first_and_its_moves_under_it()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        PieceReward? reward = PieceReward.ForWinning(new PlayerProgress(levelsWon: 46, attemptsOnLevel: 0), levelCount: 1000, TestMuseums.EndingAfterRoomOne);

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, reward));

        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Same(reward, won.Reward);
        Assert.Equal("Yeni parça: 7 / 10", ResultWords.Headline(result));
        Assert.Equal("Bölüm 1 · 1 hamlede", ResultWords.Footnote(result));
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
    public void A_player_who_asks_to_leave_is_told_what_is_left_to_do_and_what_leaving_costs()
    {
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) };
        Game untouched = Start(Level(BoardWithAMatchInReach(), goals: goals, moves: 5));
        Game played = PlayedOut(goals, moves: 5);
        Assert.Equal(GameStatus.Playing, played.Status);

        LevelResult first = LevelResult.ForLeaving(untouched, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false));
        LevelResult later = LevelResult.ForLeaving(played, new AttemptBrief(attemptNumber: 3, isOnLastLevel: false));

        LevelResult.Leaving leaving = Assert.IsType<LevelResult.Leaving>(later);
        Assert.True(leaving.HasPlayed);
        Assert.False(Assert.IsType<LevelResult.Leaving>(first).HasPlayed);

        // The purple goal was met by the one move; the red one is what is left to do.
        var open = Assert.Single(leaving.OpenGoals);
        Assert.Equal(PieceArt.GoalRed, open.Icon);
        Assert.Equal("Bölümden çık?", ResultWords.Title(later));
        Assert.Equal("Bölüm 1 · 3. deneme", ResultWords.Headline(later));
        Assert.Equal("Çıkarsan bu deneme sayılır", ResultWords.Footnote(later));
        Assert.Equal("Henüz hamle yapmadın", ResultWords.Footnote(first));
        Assert.Equal("Oyna", ResultWords.GoOn(later));
    }

    [Fact]
    public void On_the_narrow_button_beside_the_jigsaw_button_no_line_of_the_green_label_is_longer_than_a_short_word()
    {
        Game won = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        LevelResult onTheLastLevel = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: true));
        LevelResult onAnyOther = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false));

        Assert.Equal("Tekrar\noyna", ResultWords.GoOnOnANarrowButton(onTheLastLevel));
        Assert.Equal("Devam", ResultWords.GoOnOnANarrowButton(onAnyOther));

        // The blue button's own lines are "Parçayı" and "yerleştir": the green one's are no longer, so both are lettered alike.
        Assert.All(ResultWords.GoOnOnANarrowButton(onTheLastLevel).Split('\n'), line => Assert.True(line.Length <= 6));
    }

    [Fact]
    public void Only_a_game_that_is_still_played_can_be_left()
    {
        Game decided = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        Assert.Equal(GameStatus.Won, decided.Status);

        Assert.Throws<InvalidOperationException>(() => LevelResult.ForLeaving(decided, AttemptBrief.FirstTry));
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
