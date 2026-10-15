using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What each event of an attempt does to a player's progress by the economy's rules, as the Unity game's session
/// keeps it: a win and its coins are one change, a loss for good and leaving after a move cost one life each,
/// leaving before a move costs nothing, the first move counts the attempt and spends the start boosters taken into
/// it. <c>AttemptLedger</c> and <c>WallClock</c> are the Unity project's source files, compiled into these tests. The
/// rules are the tests' pack (<see cref="MetaSupport"/>): 10 coins a move left and 25 a level, five lives and
/// thirty minutes, the +5 for 900 coins or one of three ads a day, a new player with 100 coins, one rocket-and-bomb
/// and two shuffles, the boosters used during play opening on levels 7, 12, 16 and 20 with three, two, one and no
/// boosters of their kind.
/// </summary>
public class AttemptLedgerTests
{
    private const int LevelsInThePack = 1000;

    private static readonly EconomyRules TheRules = Rules();

    private static readonly AttemptLedger Ledger = new(TheRules, LevelsInThePack);

    /// <summary>A new player on level 40 after two attempts there, whose save has none of the opening gifts yet.</summary>
    private static PlayerProgress OnLevelForty() => new PlayerProgress(levelsWon: 39, attemptsOnLevel: 2).WithWallet(TheRules.StartingWallet);

    /// <summary>A player on a level who was given every opening gift due there, before any attempt at it.</summary>
    private static PlayerProgress OnTheLevel(int level) =>
        Ledger.WithTheOpeningGiftsDue(new PlayerProgress(levelsWon: level - 1, attemptsOnLevel: 0).WithWallet(TheRules.StartingWallet));

    private static PlayerProgress Apply(PlayerProgress progress, params AttemptEvent[] happened) =>
        happened.Aggregate(progress, (kept, each) => Ledger.Apply(kept, each, Evening));

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 35)]
    [InlineData(7, 95)]
    public void A_win_pays_the_rules_amount_for_the_moves_it_left_and_for_the_level(int movesLeft, long coins)
    {
        PlayerProgress won = Apply(OnLevelForty(), new AttemptEvent.Won(movesLeft));

        Assert.Equal(new Coins(100 + coins), won.Wallet.Coins);
        Assert.Equal(TheRules.StartingWallet.Coins + TheRules.Win.Of(movesLeft), won.Wallet.Coins);
    }

    [Fact]
    public void A_win_and_its_coins_are_one_change_that_moves_the_player_on()
    {
        PlayerProgress before = OnLevelForty();

        PlayerProgress won = Ledger.Apply(before, new AttemptEvent.Won(movesLeft: 4), Evening);

        // A new player's wallet on level 40 had none of the opening gifts yet: the win gives what is owed with its coins.
        Assert.Equal(before.WithLevelWon(LevelsInThePack).WithWallet(TheRules.Unlocks.GivenTo(before.Wallet.WithCoins(new Coins(165)), level: 41)), won);
        Assert.Equal((40, 0), (won.LevelsWon, won.AttemptsOnLevel));
        Assert.Equal(before.Wallet.Lives, won.Wallet.Lives);
    }

    [Fact]
    public void The_first_move_counts_the_attempt_and_spends_the_start_boosters_taken_into_it()
    {
        PlayerProgress before = OnLevelForty();
        var picked = Assert.IsType<StartToggle.Changed>(TheRules.StartBoosters.Toggled(StartChoice.None, BoosterKind.StartRocketAndBomb, before.Wallet.Shelf));

        PlayerProgress withNone = Apply(before, new AttemptEvent.FirstMove(StartChoice.None));
        PlayerProgress withOne = Apply(before, new AttemptEvent.FirstMove(picked.Choice));

        Assert.Equal(before.WithAttemptStarted(), withNone);
        Assert.Equal(3, withOne.AttemptsOnLevel);
        Assert.Equal(0, withOne.Wallet.Shelf.Count(BoosterKind.StartRocketAndBomb));
        Assert.Equal(2, withOne.Wallet.Shelf.Count(BoosterKind.Shuffle));
        Assert.Equal(before.Wallet.Coins, withOne.Wallet.Coins);
    }

    [Fact]
    public void Start_boosters_chosen_on_the_card_stay_on_the_shelf_when_the_level_is_left_before_its_first_move_and_go_with_it()
    {
        PlayerProgress before = OnLevelForty().WithWallet(TheRules.StartingWallet.WithShelf(TheRules.StartingWallet.Shelf.WithOneMore(BoosterKind.StartExtraMoves)));
        StartChoice chosen = new[] { BoosterKind.StartRocketAndBomb, BoosterKind.StartExtraMoves }.Aggregate(
            StartChoice.None, (choice, booster) => Assert.IsType<StartToggle.Changed>(TheRules.StartBoosters.Toggled(choice, booster, before.Wallet.Shelf)).Choice);

        PlayerProgress leftBeforeAMove = Apply(before, new AttemptEvent.Left(afterAMove: false));
        PlayerProgress leftAfterOne = Apply(before, new AttemptEvent.FirstMove(chosen), new AttemptEvent.Left(afterAMove: true));

        Assert.Same(before, leftBeforeAMove);
        Assert.Equal(before.Wallet.Shelf.WithOneFewer(BoosterKind.StartRocketAndBomb).WithOneFewer(BoosterKind.StartExtraMoves), leftAfterOne.Wallet.Shelf);
        Assert.Equal(4, leftAfterOne.Wallet.Lives.LivesUnder(TheRules.Lives));
        Assert.Equal(before.Wallet.Coins, leftAfterOne.Wallet.Coins);
    }

    [Theory]
    [InlineData(LossReason.OutOfMoves)]
    [InlineData(LossReason.NoMovePossible)]
    public void A_loss_for_good_costs_one_life_and_the_first_starts_the_clock_of_the_next(LossReason reason)
    {
        PlayerProgress lostOnce = Ledger.Apply(OnLevelForty(), new AttemptEvent.LostForGood(reason), Evening);
        PlayerProgress lostTwice = Ledger.Apply(lostOnce, new AttemptEvent.LostForGood(reason), MinutesLater(10));

        Assert.Equal(new LifeMeter(1, MinutesLater(30)), lostOnce.Wallet.Lives);
        Assert.Equal(new LifeMeter(2, MinutesLater(30)), lostTwice.Wallet.Lives);
        Assert.Equal(OnLevelForty().WithWallet(lostTwice.Wallet), lostTwice); // nothing else changed
        Assert.Equal(TheRules.StartingWallet.Coins, lostTwice.Wallet.Coins);
    }

    [Fact]
    public void Leaving_after_a_move_costs_a_life_and_leaving_before_any_costs_nothing()
    {
        PlayerProgress before = OnLevelForty();

        Assert.Same(before, Apply(before, new AttemptEvent.Left(afterAMove: false)));
        Assert.Equal(new LifeMeter(1, MinutesLater(30)), Apply(before, new AttemptEvent.Left(afterAMove: true)).Wallet.Lives);
    }

    [Fact]
    public void An_attempt_reported_from_its_first_move_to_its_loss_counts_once_and_costs_one_life()
    {
        PlayerProgress after = Apply(OnLevelForty(), new AttemptEvent.FirstMove(StartChoice.None), new AttemptEvent.LostForGood(LossReason.OutOfMoves));

        Assert.Equal((39, 3), (after.LevelsWon, after.AttemptsOnLevel));
        Assert.Equal(4, after.Wallet.Lives.LivesUnder(TheRules.Lives));
    }

    [Fact]
    public void A_loss_on_an_empty_meter_takes_nothing_more_and_a_life_that_came_back_meanwhile_goes_first()
    {
        AttemptEvent loss = new AttemptEvent.LostForGood(LossReason.OutOfMoves);
        PlayerProgress empty = Apply(OnLevelForty(), loss, loss, loss, loss, loss);
        Assert.Equal(new LifeMeter(5, MinutesLater(30)), empty.Wallet.Lives);

        // No level start refuses an attempt without a life yet: such a loss leaves the meter as it is.
        Assert.Equal(empty, Ledger.Apply(empty, loss, MinutesLater(20)));

        // Half an hour on, a life is back, and the loss takes it; the clock of the next one runs on from the first.
        PlayerProgress later = Ledger.Apply(empty, loss, MinutesLater(31));
        Assert.Equal(new LifeMeter(5, MinutesLater(60)), later.Wallet.Lives);
    }

    [Fact]
    public void A_booster_the_engine_accepted_leaves_the_shelf_and_one_not_held_is_a_mistake()
    {
        PlayerProgress used = Apply(OnLevelForty(), new AttemptEvent.BoosterAccepted(BoosterKind.Shuffle));

        Assert.Equal(1, used.Wallet.Shelf.Count(BoosterKind.Shuffle));
        Assert.Throws<InvalidOperationException>(() => Apply(OnLevelForty(), new AttemptEvent.BoosterAccepted(BoosterKind.HitRow)));
    }

    [Fact]
    public void Extra_moves_by_coins_are_charged_their_price_and_by_an_ad_counted_for_the_day()
    {
        PlayerProgress rich = OnLevelForty().WithWallet(WalletWith(1000));

        PlayerProgress byCoins = Apply(rich, new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByCoins));
        PlayerProgress byAd = Apply(rich, new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByAd));

        Assert.Equal(new Coins(100), byCoins.Wallet.Coins);
        Assert.Equal(new Coins(1000), byAd.Wallet.Coins);
        Assert.Equal(new AdsWatched(Today, forExtraMoves: 1, forCoins: 0), byAd.Wallet.Ads);
    }

    [Fact]
    public void Extra_moves_the_wallet_cannot_pay_for_are_a_mistake_of_whoever_offered_them()
    {
        var takenByCoins = new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByCoins);
        var takenByAd = new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByAd);
        PlayerProgress noAdLeft = Apply(OnLevelForty(), takenByAd, takenByAd, takenByAd);

        Assert.Throws<InvalidOperationException>(() => Apply(OnLevelForty(), takenByCoins)); // 100 coins of 900
        Assert.Throws<InvalidOperationException>(() => Apply(noAdLeft, takenByAd));
    }

    [Theory]
    [InlineData(6, BoosterKind.HitCell, 3)]
    [InlineData(11, BoosterKind.HitRow, 2)]
    [InlineData(15, BoosterKind.HitColumn, 1)]
    [InlineData(19, BoosterKind.Shuffle, 0)]
    public void Winning_the_level_before_a_boosters_own_gives_its_gift_once_and_the_level_before_that_does_not(int levelBefore, BoosterKind opens, int gift)
    {
        PlayerProgress twoBefore = OnTheLevel(levelBefore - 1);

        PlayerProgress onTheLevelBefore = Apply(twoBefore, new AttemptEvent.Won(movesLeft: 0));
        PlayerProgress onItsLevel = Apply(onTheLevelBefore, new AttemptEvent.Won(movesLeft: 0));
        PlayerProgress past = Apply(onItsLevel, new AttemptEvent.Won(movesLeft: 0));

        Assert.False(onTheLevelBefore.Wallet.Rewards.UnlockGifts.Contains(opens));
        Assert.Equal(twoBefore.Wallet.Shelf, onTheLevelBefore.Wallet.Shelf);
        Assert.True(onItsLevel.Wallet.Rewards.UnlockGifts.Contains(opens));
        Assert.Equal(onTheLevelBefore.Wallet.Shelf.With(opens, gift), onItsLevel.Wallet.Shelf);
        Assert.Equal(onItsLevel.Wallet.Shelf, past.Wallet.Shelf);
    }

    [Fact]
    public void A_save_from_before_the_opening_gifts_past_their_levels_is_given_each_once_and_never_again()
    {
        PlayerProgress oldSave = OnLevelForty();

        PlayerProgress given = Ledger.WithTheOpeningGiftsDue(oldSave);

        Assert.Equal(BoosterKindSet.Of(BoosterKinds.InLevel), given.Wallet.Rewards.UnlockGifts);
        Assert.Equal((3, 2, 1, 2), (given.Wallet.Shelf.Count(BoosterKind.HitCell), given.Wallet.Shelf.Count(BoosterKind.HitRow), given.Wallet.Shelf.Count(BoosterKind.HitColumn), given.Wallet.Shelf.Count(BoosterKind.Shuffle)));
        Assert.Same(given, Ledger.WithTheOpeningGiftsDue(given));

        // Before the first booster's level nothing is due, and the progress is the very one it was.
        PlayerProgress onSix = new PlayerProgress(levelsWon: 5, attemptsOnLevel: 0).WithWallet(TheRules.StartingWallet);
        Assert.Same(onSix, Ledger.WithTheOpeningGiftsDue(onSix));
    }

    [Fact]
    public void A_booster_before_any_move_counts_as_the_first_move_so_leaving_after_it_costs_a_life()
    {
        PlayerProgress before = Ledger.WithTheOpeningGiftsDue(OnLevelForty());
        var picked = Assert.IsType<StartToggle.Changed>(TheRules.StartBoosters.Toggled(StartChoice.None, BoosterKind.StartRocketAndBomb, before.Wallet.Shelf));

        // What the board reports when a spatula is the first thing the engine accepts.
        PlayerProgress used = Apply(before, new AttemptEvent.FirstMove(picked.Choice), new AttemptEvent.BoosterAccepted(BoosterKind.HitCell));
        PlayerProgress left = Apply(used, new AttemptEvent.Left(afterAMove: true));

        Assert.Equal(before.AttemptsOnLevel + 1, used.AttemptsOnLevel);
        Assert.Equal((0, 2), (used.Wallet.Shelf.Count(BoosterKind.StartRocketAndBomb), used.Wallet.Shelf.Count(BoosterKind.HitCell)));
        Assert.Equal(4, left.Wallet.Lives.LivesUnder(TheRules.Lives));
    }

    [Fact]
    public void An_attempt_has_begun_after_a_move_or_a_booster_used_during_play_and_not_with_the_start_boosters_alone()
    {
        LevelDefinition level = GameTests.OpenLevel();
        Game untouched = LevelStart.Attempt(level, attempt: 0, new[] { StartBooster.ColorBomb, StartBooster.ExtraMoves });
        Game afterABooster = LevelStart.Attempt(level, attempt: 0);
        Game afterAMove = LevelStart.Attempt(level, attempt: 0);

        Assert.True(afterABooster.TryUseBooster(new BoosterUse.Shuffle()));
        Assert.True(afterAMove.TryPlay(afterAMove.FindLegalMoves()[0]));

        Assert.False(AttemptBegun.In(untouched));
        Assert.True(AttemptBegun.In(afterABooster));
        Assert.True(AttemptBegun.In(afterAMove));
    }

    [Fact]
    public void A_win_cannot_leave_fewer_than_no_moves_and_a_ledger_needs_a_level()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptEvent.Won(movesLeft: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttemptLedger(TheRules, levelCount: 0));
    }

    [Fact]
    public void Only_a_win_a_loss_for_good_and_leaving_after_a_move_end_an_attempt_and_so_have_its_record_written()
    {
        AttemptEvent[] ending = { new AttemptEvent.Won(3), new AttemptEvent.LostForGood(LossReason.OutOfMoves), new AttemptEvent.Left(afterAMove: true) };
        AttemptEvent[] notEnding =
        {
            new AttemptEvent.FirstMove(StartChoice.None), new AttemptEvent.BoosterAccepted(BoosterKind.HitRow),
            new AttemptEvent.ExtraMovesTaken(ExtraMovesPayment.ByAd), new AttemptEvent.Left(afterAMove: false),
        };

        Assert.All(ending, happened => Assert.True(happened.EndsTheAttempt, happened.ToString()));
        Assert.All(notEnding, happened => Assert.False(happened.EndsTheAttempt, happened.ToString()));
    }
}

/// <summary>The wall clocks of the Unity game: they hand out whole seconds, which the save file keeps exactly.</summary>
public class WallClockTests
{
    [Fact]
    public void The_devices_clock_tells_whole_seconds_in_the_devices_time_zone()
    {
        DateTimeOffset now = new SystemWallClock().Now;

        Assert.Equal(0, now.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(now), now.Offset);
        Assert.InRange(DateTimeOffset.Now - now, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_fixed_clock_tells_its_time_and_refuses_one_between_two_seconds()
    {
        Assert.Equal(Evening, new FixedWallClock(Evening).Now);
        Assert.Throws<ArgumentException>(() => new FixedWallClock(Evening.AddMilliseconds(1)));
    }
}
