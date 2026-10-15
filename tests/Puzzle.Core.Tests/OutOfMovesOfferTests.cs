using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// The offer of five more moves to a game that ran out of them, as the Unity game makes it: once in an attempt, never
/// after a win or with no move possible; the engine asked first, and the payment kept by the attempt's ledger only
/// when it gave the moves (900 coins, or one of three ads a day in the tests' economy, <see cref="MetaSupport"/>);
/// a board no shuffle brings back refused with nothing paid; giving up a loss for good for a life; what its card
/// says in each state; and an attempt with the moves taken or refused replayed from its record to the same end.
/// <c>OutOfMovesOffer</c>, <c>LevelResult</c>, <c>Ads</c> and <c>ResultCardLayout</c> are the Unity project's
/// source files, compiled into these tests.
/// </summary>
public class OutOfMovesOfferTests
{
    private const int LevelsInThePack = 1000;

    private static readonly EconomyRules TheRules = Rules();

    private static readonly AttemptLedger Ledger = new(TheRules, LevelsInThePack);

    private static readonly GameOptions Recording = new() { RecordActions = true };

    /// <summary>Three purple gems in the fourth row once the one above the third column is pulled down.</summary>
    private static readonly Move TheMatchingMove = Move.Swap(P(2, 2), P(2, 3));

    private static readonly AttemptBrief FirstTry = AttemptBrief.FirstTry(TheRules.Win);

    /// <summary>A player on level 40 who has tried it twice and holds so many coins, every life and no ad watched today.</summary>
    private static PlayerProgress HoldingCoins(long coins) => new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithWallet(WalletWith(coins));

    private static ExtraMovesTerms TermsOf(PlayerProgress progress) => TheRules.ExtraMoves.TermsFor(progress.Wallet, Today);

    /// <summary>A game whose one move collects the purple goal and leaves the red one open: its moves have run out.</summary>
    private static Game OutOfMoves(int moves = 1) => OutOfMovesWith(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3), new GoalSpec.CollectColor(GemColor.Red, 999) }, moves);

    private static Game OutOfMovesWith(GoalSpec[] goals, int moves)
    {
        Game game = Game.Start(Level(Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p'), goals: goals, moves: moves), AttemptSeeds.From(1), Recording);
        Assert.True(game.TryPlay(TheMatchingMove));
        return game;
    }

    /// <summary>The same move on jellies under the three purple gems it matches, with a goal of three jellies and one of so many purple gems.</summary>
    private static Game OutOfMovesOnJelly(int purpleWanted)
    {
        string[] jelly = Blank(6, 6).With(0, 3, '1').With(1, 3, '1').With(2, 3, '1');
        var goals = new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, purpleWanted), new GoalSpec.ClearJelly(3) };
        Game game = Game.Start(Level(Filler(6, 6).With(0, 3, 'p').With(1, 3, 'p').With(2, 2, 'p'), jelly: jelly, goals: goals, moves: 1), AttemptSeeds.From(1), Recording);
        Assert.True(game.TryPlay(TheMatchingMove));
        return game;
    }

    /// <summary>Boxes all around one rocket: once it has fired there is nothing to move and nothing a shuffle could bring back.</summary>
    private static Game OutOfMovesOnADeadBoard()
    {
        Game game = Game.Start(Level(Blank(3, 3, '3').With(1, 1, 'H'), moves: 1), AttemptSeeds.From(1), Recording);
        Assert.True(game.TryPlay(Move.Tap(P(1, 1))));
        return game;
    }

    private static PlayerProgress Keep(PlayerProgress progress, params AttemptEvent[] happened) =>
        happened.Aggregate(progress, (kept, each) => Ledger.Apply(kept, each, Evening));

    [Fact]
    public void A_game_whose_moves_ran_out_is_offered_five_more_once_and_never_after_a_win()
    {
        Game outOfMoves = OutOfMoves();
        Game won = OutOfMovesWith(new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Purple, 3) }, moves: 1);

        Assert.Equal(GameStatus.LostOutOfMoves, outOfMoves.Status);
        Assert.True(OutOfMovesOffer.IsMadeIn(outOfMoves));
        Assert.Equal(GameStatus.Won, won.Status);
        Assert.False(OutOfMovesOffer.IsMadeIn(won));
        Assert.Throws<InvalidOperationException>(() => LevelResult.OfferTo(won, FirstTry));

        Assert.IsType<OfferAnswer.MovesGiven>(OutOfMovesOffer.Take(outOfMoves, ExtraMovesPayment.ByCoins, TermsOf(HoldingCoins(900))));
        AttemptActionTests.PlayRandomly(outOfMoves, new Rng(5), moves: ExtraMovesOffer.Moves);

        // The five were played and the moves ran out again: this time the loss is final, and nothing is offered.
        Assert.Equal(GameStatus.LostOutOfMoves, outOfMoves.Status);
        Assert.False(OutOfMovesOffer.IsMadeIn(outOfMoves));
        Assert.Throws<InvalidOperationException>(() => OutOfMovesOffer.Take(outOfMoves, ExtraMovesPayment.ByCoins, TermsOf(HoldingCoins(900))));
        Assert.Throws<InvalidOperationException>(() => OutOfMovesOffer.GiveUp(outOfMoves));
        Assert.Equal("Hamle bitti!", ResultWords.Title(LevelResult.Of(outOfMoves, FirstTry)));
    }

    [Fact]
    public void Paying_with_coins_asks_the_engine_first_gives_five_more_moves_and_the_ledger_charges_exactly_the_price()
    {
        Game game = OutOfMoves();
        PlayerProgress before = HoldingCoins(1250);

        var given = Assert.IsType<OfferAnswer.MovesGiven>(OutOfMovesOffer.Take(game, ExtraMovesPayment.ByCoins, TermsOf(before)));
        PlayerProgress after = Keep(before, given.Payment);

        Assert.Equal((GameStatus.Playing, ExtraMovesOffer.Moves), (game.Status, game.MovesLeft));
        Assert.True(game.ExtraMovesTaken);
        Assert.Equal(new Coins(1250 - 900), after.Wallet.Coins);
        Assert.Equal(before.Wallet.Ads, after.Wallet.Ads);
        Assert.Equal(before.Wallet.Lives, after.Wallet.Lives);
        Assert.Equal(before.AttemptsOnLevel, after.AttemptsOnLevel);
    }

    [Fact]
    public void Paying_with_an_ad_spends_one_of_the_days_free_ads_and_a_new_day_brings_them_back()
    {
        PlayerProgress progress = HoldingCoins(0);
        for (int ad = 1; ad <= 3; ad++)
        {
            var given = Assert.IsType<OfferAnswer.MovesGiven>(OutOfMovesOffer.Take(OutOfMoves(), ExtraMovesPayment.ByAd, TermsOf(progress)));
            progress = Keep(progress, given.Payment);
            Assert.Equal(3 - ad, TermsOf(progress).FreeAdsLeftToday);
        }

        Game afterTheThird = OutOfMoves();
        Assert.IsType<OfferAnswer.CannotPay>(OutOfMovesOffer.Take(afterTheThird, ExtraMovesPayment.ByAd, TermsOf(progress)));
        Assert.Equal(new Coins(0), progress.Wallet.Coins);

        // The engine was not asked: the game is still out of moves, and still offered them.
        Assert.True(OutOfMovesOffer.IsMadeIn(afterTheThird));
        Assert.Equal(3, TheRules.ExtraMoves.TermsFor(progress.Wallet, Today.Plus(1)).FreeAdsLeftToday);
    }

    [Fact]
    public void Coins_short_of_the_price_ask_nothing_of_the_engine_and_charge_nothing()
    {
        Game game = OutOfMoves();
        PlayerProgress poor = HoldingCoins(899);

        Assert.IsType<OfferAnswer.CannotPay>(OutOfMovesOffer.Take(game, ExtraMovesPayment.ByCoins, TermsOf(poor)));

        Assert.Equal((GameStatus.LostOutOfMoves, false), (game.Status, game.ExtraMovesTaken));
        Assert.Empty(game.Actions.OfType<AttemptAction.ExtraMovesRefused>());
        Assert.Equal(new Coins(899), poor.Wallet.Coins);
    }

    [Theory]
    [InlineData(ExtraMovesPayment.ByCoins)]
    [InlineData(ExtraMovesPayment.ByAd)]
    public void A_board_no_shuffle_brings_back_is_refused_the_moves_with_nothing_paid_and_its_loss_is_final_for_a_life(ExtraMovesPayment paidBy)
    {
        Game game = OutOfMovesOnADeadBoard();
        PlayerProgress before = HoldingCoins(1250);

        var refused = Assert.IsType<OfferAnswer.Refused>(OutOfMovesOffer.Take(game, paidBy, TermsOf(before)));
        PlayerProgress after = Keep(before, refused.Loss);

        Assert.Equal(LossReason.ExtraMovesRefused, refused.Loss.Reason);
        Assert.True(refused.Loss.EndsTheAttempt);
        Assert.Equal((new Coins(1250), before.Wallet.Ads), (after.Wallet.Coins, after.Wallet.Ads));
        Assert.Equal(TheRules.Lives.Max - 1, after.Wallet.Lives.SettledAt(Evening, TheRules.Lives).LivesUnder(TheRules.Lives));

        LevelResult card = LevelResult.LostForGood(game, FirstTry, refused.Loss.Reason);
        Assert.Equal("Oynanacak hamle kalmadı!", ResultWords.Title(card));
        Assert.Equal("+5 hamle verilemedi, bir şey harcanmadı", ResultWords.Footnote(card));
        Assert.Equal("1 can gitti", ResultWords.Lives(card));
    }

    [Fact]
    public void Giving_up_loses_the_game_for_good_for_one_life_and_its_card_tries_again_through_the_levels_card()
    {
        Game game = OutOfMoves();
        PlayerProgress before = HoldingCoins(1250);

        AttemptEvent.LostForGood gaveUp = OutOfMovesOffer.GiveUp(game);
        PlayerProgress after = Keep(before, gaveUp);

        Assert.Equal(LossReason.GaveUp, gaveUp.Reason);
        Assert.True(gaveUp.EndsTheAttempt);
        Assert.Equal(new Coins(1250), after.Wallet.Coins);
        Assert.Equal(TheRules.Lives.Max - 1, after.Wallet.Lives.SettledAt(Evening, TheRules.Lives).LivesUnder(TheRules.Lives));

        LevelResult card = LevelResult.LostForGood(game, FirstTry, gaveUp.Reason);
        Assert.Equal(("Hamle bitti!", "Bölüm 1 · 1. deneme", "Hedefe bu kadar kaldı", "1 can gitti"), (ResultWords.Title(card), ResultWords.Headline(card), ResultWords.Footnote(card), ResultWords.Lives(card)));
        Assert.Equal("Tekrar dene", ResultWords.GoOn(card));
        Assert.True(card.GoingOnLeadsTo().HangsTheStartCard);
        Assert.True(ResultWords.CostsALife(card));
    }

    [Fact]
    public void The_offers_card_says_what_is_left_its_price_and_what_each_way_of_paying_allows()
    {
        Game game = OutOfMoves();
        LevelResult offer = LevelResult.OfferTo(game, new AttemptBrief(attemptNumber: 3, isOnLastLevel: false, TheRules.Win));
        ExtraMovesTerms rich = TermsOf(HoldingCoins(1250));
        ExtraMovesTerms poorWithNoAdLeft = TheRules.ExtraMoves.TermsFor(WalletWith(100).WithAds(new AdsWatched(Today, forExtraMoves: 3, forCoins: 0)), Today);

        var outOfMoves = Assert.IsType<LevelResult.OutOfMoves>(offer);
        GoalFigure open = Assert.Single(outOfMoves.OpenGoals);
        Assert.Equal(PieceArt.GoalRed, open.Icon);
        Assert.Equal(3, outOfMoves.AttemptNumber);
        Assert.Equal("Hamle bitti!", ResultWords.Title(offer));
        Assert.Equal(("+5 hamle", "900", "Reklam izle"), (ResultWords.TakeTheMovesForCoins(rich), ResultWords.PriceOfTheMoves(rich), ResultWords.TakeTheMovesForAnAd(rich)));
        Assert.Equal(("Altın yetmiyor", "900", "Bugünlük bitti"), (ResultWords.TakeTheMovesForCoins(poorWithNoAdLeft), ResultWords.PriceOfTheMoves(poorWithNoAdLeft), ResultWords.TakeTheMovesForAnAd(poorWithNoAdLeft)));
        Assert.Equal(("Vazgeç", "1 can gider"), (ResultWords.GiveUp, ResultWords.Lives(offer)));
        Assert.True(ResultWords.CostsALife(offer));

        // The offer decides the game first: its card has no way on into the museum, and no footnote or green "go on".
        Assert.Throws<InvalidOperationException>(() => offer.GoingOnLeadsTo());
        Assert.Throws<InvalidOperationException>(() => ResultWords.Footnote(offer));
        Assert.Throws<InvalidOperationException>(() => ResultWords.GoOn(offer));
    }

    [Fact]
    public void The_offer_says_it_was_close_only_from_eighty_percent_of_the_goals_done()
    {
        // The one move collects three purple gems and clears the three jellies under them: with five purple wanted the
        // goals are (3/5 + 1) / 2, 80 percent done; with six, (3/6 + 1) / 2, 75 percent.
        Game justOver = OutOfMovesOnJelly(purpleWanted: 5);
        Game justUnder = OutOfMovesOnJelly(purpleWanted: 6);
        LevelResult close = LevelResult.OfferTo(justOver, FirstTry);
        LevelResult notClose = LevelResult.OfferTo(justUnder, FirstTry);

        Assert.Equal(0.8, justOver.Goals.OverallProgress, 6);
        Assert.Equal(0.75, justUnder.Goals.OverallProgress, 6);
        Assert.True(Assert.IsType<LevelResult.OutOfMoves>(close).IsClose);
        Assert.Equal("Az kaldı!", ResultWords.Headline(close));
        Assert.False(Assert.IsType<LevelResult.OutOfMoves>(notClose).IsClose);
        Assert.Equal("Kalan hedefler", ResultWords.Headline(notClose));
        Assert.False(Assert.IsType<LevelResult.OutOfMoves>(LevelResult.OfferTo(OutOfMoves(), FirstTry)).IsClose);
        Assert.Equal(80, LevelResult.CloseFromGoalsDonePercent);
    }

    [Fact]
    public void An_attempt_with_the_extra_moves_taken_is_replayed_from_its_record_to_the_same_end()
    {
        Game game = OutOfMoves();
        Assert.IsType<OfferAnswer.MovesGiven>(OutOfMovesOffer.Take(game, ExtraMovesPayment.ByAd, TermsOf(HoldingCoins(0))));
        AttemptActionTests.PlayRandomly(game, new Rng(8), moves: ExtraMovesOffer.Moves);

        AttemptRecord record = AttemptActionTests.RecordOf(game, AttemptSeeds.From(1));

        Assert.True(record.Outcome.ExtraMovesBought);
        Assert.Contains(new AttemptAction.ExtraMovesTaken(), game.Actions);
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(record, game.Level));
    }

    [Fact]
    public void An_attempt_whose_extra_moves_were_refused_is_replayed_from_its_record_to_the_same_end()
    {
        Game game = OutOfMovesOnADeadBoard();
        Assert.IsType<OfferAnswer.Refused>(OutOfMovesOffer.Take(game, ExtraMovesPayment.ByCoins, TermsOf(HoldingCoins(1250))));

        AttemptRecord record = AttemptActionTests.RecordOf(game, AttemptSeeds.From(1));

        Assert.False(record.Outcome.ExtraMovesBought);
        Assert.Equal(new AttemptAction.ExtraMovesRefused(), game.Actions[^1]);
        AttemptActionTests.AssertTheSameEnd(game, AttemptReplay.Of(record, game.Level));
    }

    [Fact]
    public void The_medallion_counts_the_extra_moves_up_to_the_games_own_number_without_warning_of_few_on_the_way()
    {
        Game game = OutOfMoves();
        HudFigures before = HudFigures.Of(game);
        Assert.IsType<OfferAnswer.MovesGiven>(OutOfMovesOffer.Take(game, ExtraMovesPayment.ByCoins, TermsOf(HoldingCoins(900))));
        HudFigures given = HudFigures.Of(game);

        HudFigures[] counted = Enumerable.Range(1, ExtraMovesOffer.Moves).Select(given.WithMovesShown).ToArray();

        Assert.Equal((0, false), (before.MovesLeft, before.MovesAreFew));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, counted.Select(figures => figures.MovesLeft));
        Assert.All(counted, figures => Assert.False(figures.MovesAreFew));
        Assert.Single(counted[2].DifferencesFrom(given));
        Assert.Empty(counted[^1].DifferencesFrom(given));
        Assert.Throws<ArgumentOutOfRangeException>(() => given.WithMovesShown(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => given.WithMovesShown(-1));
    }

    [Fact]
    public void The_offers_card_has_the_canvas_rows_and_its_goals_and_giving_up_are_centred_on_it()
    {
        // LoseTR: the niche 38 under the card's top edge; 15 under it "Az kaldı!", the goals 2 under that, the
        // buttons 13 further, the rule 15 under their lip and giving up under the rule.
        Assert.Equal(132f, ResultCardLayout.RowOf(ResultCardKind.OutOfMoves, CardRowContent.Alcove).Middle);
        Assert.Equal((241f, 265f), Span(CardRowContent.Headline));
        Assert.Equal((267f, 307f), Span(CardRowContent.GoalsLeft));
        Assert.Equal((320f, 399f), Span(CardRowContent.Buttons));
        Assert.Equal((409f, 419f), Span(CardRowContent.Rule));
        Assert.Equal(437f, ResultCardLayout.RowOf(ResultCardKind.OutOfMoves, CardRowContent.GiveUp).Middle);
        Assert.Equal(ResultCardLayout.ButtonsWidth, ResultCardLayout.TakeForCoinsWidth + ResultCardLayout.GapBetweenButtons + ResultCardLayout.TakeForAnAdWidth);
        Assert.Equal(164f, ResultCardLayout.TakeForAnAdWidth);

        IReadOnlyList<(float Picture, float NumberLeft)> three = ResultCardLayout.GoalsLeftRow(new[] { 18f, 36f, 18f });
        float left = three[0].Picture - ResultCardLayout.GoalLeftSize / 2f;
        float right = three[2].NumberLeft + 18f;
        Assert.Equal(0f, (left + right) / 2f, 0.001f);
        Assert.Equal(three[0].NumberLeft + 18f + ResultCardLayout.GapBetweenGoalsLeft, three[1].Picture - ResultCardLayout.GoalLeftSize / 2f, 0.001f);
        Assert.Equal(three[1].Picture + ResultCardLayout.GoalLeftSize / 2f + ResultCardLayout.GapGoalToNumber, three[1].NumberLeft, 0.001f);
        Assert.Empty(ResultCardLayout.GoalsLeftRow(Array.Empty<float>()));

        (float giveUpLeft, float heart, float costLeft) = ResultCardLayout.GiveUpRow(giveUpWidth: 60f, costWidth: 70f);
        Assert.Equal(0f, (giveUpLeft + costLeft + 70f) / 2f, 0.001f);
        Assert.Equal(giveUpLeft + 60f + ResultCardLayout.GapGiveUpToHeart + ResultCardLayout.GiveUpHeartSize / 2f, heart, 0.001f);
    }

    [Fact]
    public void A_simulated_ad_runs_three_seconds_of_the_stages_clock_and_this_build_shows_no_other()
    {
        IAdShowing ad = Ads.Show(AdSource.Simulated);

        Assert.IsType<SimulatedAd>(ad);
        Assert.Equal((0f, 3f, false), (ad.Progress, ad.SecondsLeft, ad.IsOver));
        Assert.Equal("3 sn", AdWords.SecondsLeft(ad.SecondsLeft));
        ad.Advance(1.2f);
        Assert.Equal(0.4f, ad.Progress, 3);
        Assert.Equal("2 sn", AdWords.SecondsLeft(ad.SecondsLeft));
        ad.Advance(1.79f);
        Assert.False(ad.IsOver);
        ad.Advance(0.5f);
        Assert.Equal((1f, 0f, true), (ad.Progress, ad.SecondsLeft, ad.IsOver));
        Assert.Equal("0 sn", AdWords.SecondsLeft(ad.SecondsLeft));
        Assert.Throws<ArgumentOutOfRangeException>(() => ad.Advance(-0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ads.Show((AdSource)7));
        Assert.Equal("Ödülün: +5 hamle", AdWords.ItPaysFor);
    }

    private static (float Top, float Bottom) Span(CardRowContent content)
    {
        CardRow row = ResultCardLayout.RowOf(ResultCardKind.OutOfMoves, content);
        return (row.Top, row.Bottom);
    }
}
