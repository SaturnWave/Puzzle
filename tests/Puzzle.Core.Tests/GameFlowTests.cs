using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
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
    private const int LevelsInThePack = 1000;

    /// <summary>What a win pays in the tests' economy (<see cref="MetaSupport"/>): 10 coins a move left and 25 a level.</summary>
    private static readonly WinReward WinPays = MetaSupport.Rules().Win;

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
    public void A_new_player_stands_on_the_first_level_has_not_tried_it_and_holds_the_rules_starting_wallet()
    {
        Wallet startingWallet = MetaSupport.Rules().StartingWallet;

        PlayerProgress newPlayer = PlayerProgress.NewPlayer(startingWallet);

        Assert.Equal(0, newPlayer.LevelIndexIn(LevelsInThePack));
        Assert.Equal(0, newPlayer.AttemptsOnLevel);
        Assert.False(newPlayer.IsOnLastLevelOf(LevelsInThePack));
        Assert.Equal(0, newPlayer.Museum.PiecesPlaced);
        Assert.Equal(startingWallet, newPlayer.Wallet);
    }

    [Fact]
    public void Every_attempt_with_a_move_in_it_takes_the_next_seed_of_the_levels_pool()
    {
        // The seed of an attempt is picked by the count of attempts before it, as the web test server picks it.
        var pool = new uint[] { 11, 22, 33 };
        AttemptPolicy policy = new AttemptPolicy { SeedPool = pool };
        var progress = new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0).LevelIndexIn(0));
    }

    [Fact]
    public void A_store_in_memory_gives_back_what_it_was_given_last()
    {
        var store = new ProgressInMemory(new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0));
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

        LevelResult result = LevelResult.Of(game, AttemptBrief.FirstTry(WinPays));

        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Equal(1, won.MovesUsed);
        Assert.Equal(4, won.MovesLeft);
        Assert.Equal("Bölüm tamamlandı!", ResultWords.Title(result));
        Assert.Equal("Bölüm 1 · 1 hamlede", ResultWords.Headline(result));
        Assert.Equal("Kalan 4 hamle\nfırçaya dönüştü", ResultWords.MovesLeftOver(won));
        Assert.Equal("Devam", ResultWords.GoOn(result));
        Assert.Throws<InvalidOperationException>(() => ResultWords.Footnote(result));
    }

    [Fact]
    public void A_won_games_card_tells_its_moves_and_the_moves_it_left_over_also_after_its_finale()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);

        FinaleOutcome finale = game.PlayFinale();

        // The finale turns the four moves left into rockets; the card still says what the player left over.
        Assert.Equal(4, finale.RocketsFired);
        LevelResult result = LevelResult.Of(game, AttemptBrief.FirstTry(WinPays));
        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Equal((1, 4), (won.MovesUsed, won.MovesLeft));
        Assert.Equal("Bölüm 1 · 1 hamlede", ResultWords.Headline(result));
        Assert.Equal("Kalan 4 hamle\nfırçaya dönüştü", ResultWords.MovesLeftOver(won));
        Assert.Equal(WinPays.Of(4), won.Coins);
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

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, WinPays, reward));

        LevelResult.Won won = Assert.IsType<LevelResult.Won>(result);
        Assert.Same(reward, won.Reward);
        Assert.Equal("Yeni parça: 7 / 10", ResultWords.Headline(result));
        Assert.Equal(("Yeni parça:", "7 / 10"), (MuseumWords.EarnedLead(reward!), MuseumWords.EarnedCount(reward!)));
    }

    [Fact]
    public void A_level_won_with_its_last_move_and_the_last_level_of_all_have_words_of_their_own()
    {
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 1);

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 2, isOnLastLevel: true, WinPays));

        // No move turned into a brush: one line, and the card draws no brush beside it.
        Assert.Equal("Son hamlede tamamlandı", ResultWords.MovesLeftOver(Assert.IsType<LevelResult.Won>(result)));
        Assert.Equal("Tekrar oyna", ResultWords.GoOn(result));
    }

    [Fact]
    public void A_lost_game_tells_which_try_it_was_and_what_was_missing()
    {
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) };
        Game game = PlayedOut(goals, moves: 1);
        Assert.Equal(GameStatus.LostOutOfMoves, game.Status);

        LevelResult result = LevelResult.Of(game, new AttemptBrief(attemptNumber: 3, isOnLastLevel: false, WinPays));

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

        LevelResult first = LevelResult.ForLeaving(untouched, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, WinPays));
        LevelResult later = LevelResult.ForLeaving(played, new AttemptBrief(attemptNumber: 3, isOnLastLevel: false, WinPays));

        LevelResult.Leaving leaving = Assert.IsType<LevelResult.Leaving>(later);
        Assert.True(leaving.HasPlayed);
        Assert.False(Assert.IsType<LevelResult.Leaving>(first).HasPlayed);

        // The purple goal was met by the one move; the red one is what is left to do.
        var open = Assert.Single(leaving.OpenGoals);
        Assert.Equal(PieceArt.GoalRed, open.Icon);
        Assert.Equal("Bölümden çık?", ResultWords.Title(later));
        Assert.Equal("Bölüm 1 · 3. deneme", ResultWords.Headline(later));
        Assert.Equal("Çıkarsan 1 can gider", ResultWords.Footnote(later));
        Assert.Equal("Henüz hamle yapmadın, can gitmez", ResultWords.Footnote(first));
        Assert.Equal("Oyna", ResultWords.GoOn(later));
    }

    [Fact]
    public void On_the_narrow_button_beside_the_jigsaw_button_no_line_of_the_green_label_is_longer_than_a_short_word()
    {
        Game won = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        LevelResult onTheLastLevel = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: true, WinPays));
        LevelResult onAnyOther = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, WinPays));

        Assert.Equal("Tekrar\noyna", ResultWords.GoOnOnANarrowButton(onTheLastLevel));
        Assert.Equal("Devam", ResultWords.GoOnOnANarrowButton(onAnyOther));

        // The blue button's own lines are "Parçayı" and "yerleştir": the green one's are no longer, so both are lettered alike.
        Assert.All(ResultWords.GoOnOnANarrowButton(onTheLastLevel).Split('\n'), line => Assert.True(line.Length <= 6));
    }

    [Fact]
    public void The_card_before_a_level_is_its_one_door_a_win_goes_on_to_the_hall_and_a_retry_to_the_hall_with_the_levels_card()
    {
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) };
        Game won = PlayedOut(goals, moves: 5);
        Game lost = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) }, moves: 1);
        Game playing = Start(Level(BoardWithAMatchInReach(), goals: goals, moves: 5));

        MuseumDoor afterAWin = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, WinPays)).GoingOnLeadsTo();
        MuseumDoor afterTheLastLevelWonAgain = LevelResult.Of(won, new AttemptBrief(attemptNumber: 1, isOnLastLevel: true, WinPays)).GoingOnLeadsTo();
        MuseumDoor afterALoss = LevelResult.Of(lost, new AttemptBrief(attemptNumber: 2, isOnLastLevel: false, WinPays)).GoingOnLeadsTo();

        Assert.Equal((Destination.Home, false), (afterAWin.Page, afterAWin.HangsTheStartCard));
        Assert.Equal((Destination.Home, true), (afterALoss.Page, afterALoss.HangsTheStartCard));
        Assert.Equal((Destination.Home, true), (afterTheLastLevelWonAgain.Page, afterTheLastLevelWonAgain.HangsTheStartCard));
        Assert.Null(afterALoss.Restored);
        Assert.False(MuseumDoor.Home.HangsTheStartCard);

        // The card that asks whether to leave keeps the player in the game: its green button leads to no page.
        Assert.Throws<InvalidOperationException>(() => LevelResult.ForLeaving(playing, AttemptBrief.FirstTry(WinPays)).GoingOnLeadsTo());
    }

    [Fact]
    public void Only_a_game_that_is_still_played_can_be_left()
    {
        Game decided = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 5);
        Assert.Equal(GameStatus.Won, decided.Status);

        Assert.Throws<InvalidOperationException>(() => LevelResult.ForLeaving(decided, AttemptBrief.FirstTry(WinPays)));
    }

    [Fact]
    public void A_game_that_is_still_played_has_no_result()
    {
        Game game = Start(Level(BoardWithAMatchInReach()));

        Assert.Throws<InvalidOperationException>(() => LevelResult.Of(game, AttemptBrief.FirstTry(WinPays)));
    }

    [Fact]
    public void The_shelf_tells_a_first_try_how_to_play_and_a_later_one_which_try_it_is()
    {
        Assert.Equal(AttemptWords.HowToPlay, AttemptWords.OnTheShelf(AttemptBrief.FirstTry(WinPays)));
        Assert.Equal("3. deneme · taşlar yeniden dizildi", AttemptWords.OnTheShelf(new AttemptBrief(attemptNumber: 3, isOnLastLevel: false, WinPays)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptBrief(attemptNumber: 0, isOnLastLevel: false, WinPays));
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

        CardPlace place = layout.PlaceOfACard(ResultCardLayout.BodyWidth, ResultCardLayout.Height);

        float top = place.Middle + place.Scale * ResultCardLayout.Height / 2;
        float bottom = place.Middle - place.Scale * ResultCardLayout.Height / 2;
        Assert.InRange(place.Scale, 0.1f, 1f);
        Assert.True(top <= layout.HudBottom, "the card runs under the plaques");
        Assert.True(bottom >= layout.ShelfTop, "the card runs over the shelf");
        Assert.True(place.Scale * ResultCardLayout.BodyWidth <= layout.ViewWidth - 2 * ScreenLayout.CardSideMargin + 0.001f, "the card is cut off at the sides");
    }

    [Theory]
    [InlineData(1080, 2340, 332f / 362f)]
    [InlineData(1080, 1920, 332f / 362f)]
    [InlineData(2076, 2152, 1f)]
    public void On_the_screens_the_game_is_made_for_a_result_card_is_as_wide_as_the_phone_allows_and_the_canvas_draws_it_on_the_foldable(int width, int height, float scale)
    {
        // A phone of 360 points draws the canvas' card 332 wide, as the game drew it before it had the canvas' rows;
        // the height never holds it back there, even with the taller shelf of the 9:16 phone.
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(9, 9, hasExits: false));

        Assert.Equal(scale, layout.PlaceOfACard(ResultCardLayout.BodyWidth, ResultCardLayout.Height).Scale, 0.0001f);
    }

    public static TheoryData<ResultCardKind> Cards() => new TheoryData<ResultCardKind>
    {
        ResultCardKind.WonWithAPiece,
        ResultCardKind.WonWithoutAPiece,
        ResultCardKind.WhatIsLeft,
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void The_rows_of_a_result_card_follow_one_another_down_its_body_and_none_runs_into_the_next(ResultCardKind kind)
    {
        IReadOnlyList<CardRow> rows = ResultCardLayout.RowsOf(kind);

        // The rows scale with the card as one, so rows that keep apart at its full size keep apart at every size a screen draws it at.
        Assert.Equal(rows.OrderBy(row => row.Top), rows);
        Assert.All(rows.Zip(rows.Skip(1)), pair => Assert.True(pair.First.Bottom <= pair.Second.Top, $"{pair.First} runs into {pair.Second}"));
        Assert.InRange(rows[0].Top, ResultCardLayout.RidesAbove, ResultCardLayout.BodyHeight);
        Assert.InRange(rows[^1].Bottom, rows[0].Top, ResultCardLayout.BodyHeight);
        Assert.Equal(CardRowContent.Alcove, rows[0].Content);
        Assert.Equal(CardRowContent.Buttons, rows[^1].Content);
        Assert.Equal(rows.Count, rows.Select(row => row.Content).Distinct().Count());
    }

    [Fact]
    public void A_won_cards_rows_are_the_canvas_own_and_only_a_piece_brings_the_pips()
    {
        // WinTR: the niche 38 under the card's top edge, "Yeni parça" 17 under it, the pips 8 under that, the coins
        // 19 further with the rule over them, and the buttons 15 under the coins; the card is 468 high.
        Assert.Equal(468f, ResultCardLayout.BodyHeight);
        Assert.Equal(132f, ResultCardLayout.RowOf(ResultCardKind.WonWithAPiece, CardRowContent.Alcove).Middle);
        Assert.Equal((243f, 267f), Span(ResultCardKind.WonWithAPiece, CardRowContent.Headline));
        Assert.Equal((275f, 291f), Span(ResultCardKind.WonWithAPiece, CardRowContent.Pips));
        Assert.Equal((299f, 309f), Span(ResultCardKind.WonWithAPiece, CardRowContent.Rule));
        Assert.Equal((310f, 356f), Span(ResultCardKind.WonWithAPiece, CardRowContent.Coins));
        Assert.Equal(371f, ResultCardLayout.RowOf(ResultCardKind.WonWithAPiece, CardRowContent.Buttons).Top);

        Assert.False(ResultCardLayout.Has(ResultCardKind.WonWithoutAPiece, CardRowContent.Pips));
        Assert.True(ResultCardLayout.Has(ResultCardKind.WonWithoutAPiece, CardRowContent.Coins));
        Assert.False(ResultCardLayout.Has(ResultCardKind.WhatIsLeft, CardRowContent.Coins));
        Assert.Throws<ArgumentException>(() => ResultCardLayout.RowOf(ResultCardKind.WhatIsLeft, CardRowContent.Pips));
        Assert.Equal((AlcoveFrame.Wide, 1f), (ResultCardLayout.FrameOf(ResultCardKind.WonWithAPiece), ResultCardLayout.AlcoveScale(ResultCardKind.WonWithAPiece)));
        Assert.Equal(AlcoveFrame.WebPage, ResultCardLayout.FrameOf(ResultCardKind.WhatIsLeft));
    }

    [Fact]
    public void Beside_the_place_button_the_green_one_is_150_wide_and_the_two_fill_the_canvas_row()
    {
        (float goOn, float place) = ResultCardLayout.ButtonMiddles(ResultCardLayout.GoOnBesidePlaceWidth);
        (float goOnAlone, float museum) = ResultCardLayout.ButtonMiddles(ResultCardLayout.GoOnBesideMuseumWidth);

        // WinTR's row is 328 wide: the green button at its left end, the blue one of 166 at its right, 12 between.
        Assert.Equal(-164f + 75f, goOn);
        Assert.Equal(164f - 83f, place);
        Assert.Equal(164f - 59f, museum);
        Assert.Equal(ResultCardLayout.ButtonsWidth, ResultCardLayout.GoOnBesidePlaceWidth + ResultCardLayout.GapBetweenButtons + ResultCardLayout.PlaceWidth);
        Assert.True(goOnAlone < museum);
    }

    [Theory]
    [InlineData(40f, 90f, true)]
    [InlineData(40f, 140f, false)]
    [InlineData(80f, 90f, true)]
    public void The_coins_row_is_centred_on_the_card_with_its_pill_as_wide_as_its_amount_needs(float amountWidth, float wordsWidth, bool hasABrush)
    {
        float pill = ResultCardLayout.PillWidth(amountWidth);
        CoinRowPlaces places = ResultCardLayout.CoinRow(pill, wordsWidth, hasABrush);

        CoinsOnTheirPill coins = Assert.NotNull(places.Coins);
        float left = coins.Coin - ResultCardLayout.CoinSize / 2f;
        Assert.Equal(0f, (left + places.WordsRight) / 2f, 0.001f);
        Assert.Equal(Math.Max(ResultCardLayout.NarrowestPill, ResultCardLayout.AmountRoomLeft + amountWidth + ResultCardLayout.AmountRoomRight), pill);
        Assert.Equal(left + ResultCardLayout.PillInFromCoin + pill / 2f, coins.Pill, 0.001f);
        Assert.True(coins.Amount - amountWidth / 2f >= coins.Pill - pill / 2f + ResultCardLayout.AmountRoomLeft - 0.001f, "the amount runs over the coin");
        Assert.Equal(coins.Pill + pill / 2f + ResultCardLayout.GapAfterPill + (hasABrush ? ResultCardLayout.BrushSize + ResultCardLayout.GapAfterBrush : 0f), places.WordsLeft, 0.001f);
        Assert.True(places.WordsRight - left <= ResultCardLayout.ButtonsWidth, "the coins' row is wider than the card's row of buttons");
    }

    [Fact]
    public void A_win_that_paid_no_coin_has_no_pill_and_its_words_alone_are_centred_on_the_card()
    {
        // The economy pays a win by the moves it left over: one won with its last move may pay nothing, and "+0" says nothing worth a pill.
        CoinRowPlaces places = ResultCardLayout.CoinRow(pillWidth: null, wordsWidth: 140f, hasABrush: false);

        Assert.Null(places.Coins);
        Assert.Equal((-70f, 70f), (places.WordsLeft, places.WordsRight));
    }

    [Theory]
    [InlineData(4, 3, 55)]
    [InlineData(1, 0, 25)]
    public void A_won_card_shows_the_coins_the_ledger_pays_for_the_win_whether_its_finale_is_watched_or_not(int moves, int movesLeft, long coins)
    {
        // The tests' economy pays 10 coins a move left and 25 a level: the card says what the ledger adds to the wallet.
        EconomyRules rules = MetaSupport.Rules();
        Game game = PlayedOut(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves);
        PlayerProgress before = new PlayerProgress(levelsWon: 0, attemptsOnLevel: 1).WithWallet(rules.StartingWallet);
        PlayerProgress after = new AttemptLedger(rules, LevelsInThePack).Apply(before, new AttemptEvent.Won(game.MovesLeft), MetaSupport.Evening);

        LevelResult.Won beforeTheFinale = Assert.IsType<LevelResult.Won>(LevelResult.Of(game, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, rules.Win)));
        if (movesLeft > 0)
        {
            game.PlayFinale();
        }

        LevelResult.Won afterTheFinale = Assert.IsType<LevelResult.Won>(LevelResult.Of(game, new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, rules.Win)));

        Assert.Equal(movesLeft, beforeTheFinale.MovesLeft);
        Assert.Equal(new Coins(coins), beforeTheFinale.Coins);
        Assert.Equal(after.Wallet.Coins.Amount - before.Wallet.Coins.Amount, beforeTheFinale.Coins.Amount);
        Assert.Equal(beforeTheFinale.Coins, afterTheFinale.Coins);
        Assert.Equal($"+{coins}", ResultWords.CoinsWon(afterTheFinale.Coins));
    }

    [Fact]
    public void The_coins_on_a_card_are_written_as_the_wallet_writes_an_amount()
    {
        Assert.Equal("+0", ResultWords.CoinsWon(Coins.None));
        Assert.Equal("+1.250", ResultWords.CoinsWon(new Coins(1250)));
    }

    [Fact]
    public void A_level_that_gives_a_piece_hangs_itself_over_its_goals_and_the_painting_beside_and_one_that_gives_none_keeps_the_web_pages_plaques()
    {
        PieceReward? reward = PieceReward.ForWinning(new PlayerProgress(levelsWon: 46, attemptsOnLevel: 0), LevelsInThePack, TestMuseums.EndingAfterRoomOne);

        BoardPlaques withThePiece = BoardPlaques.Of(47, reward);
        BoardPlaques without = BoardPlaques.Of(101, reward: null);

        var piece = Assert.IsType<BoardPlaques.LevelOverGoalsBesideThePiece>(withThePiece);
        Assert.Equal(("BÖLÜM 47", "PARÇA 7 / 10"), (piece.GoalsLabel, piece.PieceLabel));
        Assert.Same(reward, piece.Reward);
        var plain = Assert.IsType<BoardPlaques.LevelBesideGoals>(without);
        Assert.Equal(("BÖLÜM", "101", "HEDEF"), (plain.LevelLabel, plain.LevelFigure, plain.GoalsLabel));
        Assert.Equal(101, without.LevelNumber);
    }

    private static (float Top, float Bottom) Span(ResultCardKind kind, CardRowContent content)
    {
        CardRow row = ResultCardLayout.RowOf(kind, content);
        return (row.Top, row.Bottom);
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
