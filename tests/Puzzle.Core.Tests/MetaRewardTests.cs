using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the game gives: coins for a win by the moves left, a reward for a completed painting and one for the day.
/// Nothing is random: a reward's number says what it holds.
/// </summary>
public class MetaRewardTests
{
    private static readonly EconomyRules Rules = MetaSupport.Rules();

    private static readonly BoosterKind[] StartBoostersOnly = [BoosterKind.StartColorBomb, BoosterKind.StartRocketAndBomb, BoosterKind.StartExtraMoves];

    [Fact]
    public void A_win_pays_for_every_move_left_and_for_the_level()
    {
        // The tests' settings: 10 coins a move left, 25 for the win itself.
        Assert.Equal(new Coins(25), Rules.Win.Of(movesLeft: 0));
        Assert.Equal(new Coins(95), Rules.Win.Of(movesLeft: 7));
        Assert.Equal(new Coins(10 * 150 + 25), Rules.Win.Of(movesLeft: 150));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules.Win.Of(movesLeft: -1));

        // With nothing for the win itself, a win on the last move pays nothing.
        Assert.Equal(Coins.None, new WinReward(perMoveLeft: new Coins(4), perLevelWon: Coins.None).Of(movesLeft: 0));
    }

    [Fact]
    public void Reward_number_n_holds_the_nth_booster_of_the_rotation_and_starts_over_at_its_end()
    {
        var rotation = new RewardRotation(new Coins(80), boostersEachTime: 1, BoosterKinds.All);

        IEnumerable<BoosterKind> firstFifteen = Enumerable.Range(0, 15).Select(number => Assert.Single(rotation.Of(number).Boosters));

        Assert.Equal([.. BoosterKinds.All, .. BoosterKinds.All, BoosterKind.StartRocketAndBomb], firstFifteen);
        Assert.All(Enumerable.Range(0, 15), number => Assert.Equal(new Coins(80), rotation.Of(number).Coins));
        Assert.Equal(BoosterKind.HitCell, Assert.Single(rotation.Of(7 * 100_000 + 3).Boosters));
        Assert.Equal(rotation.Of(2), rotation.Of(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => rotation.Of(-1));
    }

    [Fact]
    public void A_reward_of_several_boosters_takes_the_next_ones_of_the_rotation()
    {
        var twoEachTime = new RewardRotation(Coins.None, boostersEachTime: 2, StartBoostersOnly);

        // Three boosters in turn, two at a time: the second reward starts where the first stopped, and may hold a kind twice over two rewards.
        Assert.Equal([BoosterKind.StartColorBomb, BoosterKind.StartRocketAndBomb], twoEachTime.Of(0).Boosters);
        Assert.Equal([BoosterKind.StartExtraMoves, BoosterKind.StartColorBomb], twoEachTime.Of(1).Boosters);
        Assert.Equal([BoosterKind.StartRocketAndBomb, BoosterKind.StartExtraMoves], twoEachTime.Of(2).Boosters);
        Assert.Equal(twoEachTime.Of(0), twoEachTime.Of(3));

        // A reward of coins alone needs no rotation; one that gives boosters cannot do without.
        Assert.Empty(new RewardRotation(new Coins(40), boostersEachTime: 0, []).Of(5).Boosters);
        Assert.Throws<ArgumentException>(() => new RewardRotation(Coins.None, boostersEachTime: 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RewardRotation(Coins.None, boostersEachTime: -1, StartBoostersOnly));
    }

    [Fact]
    public void A_completed_painting_gives_its_reward_and_counts_it()
    {
        Wallet wallet = WalletWith(100);

        Wallet afterOne = Rules.PaintingCompleted.GivenTo(wallet);
        Wallet afterThree = Rules.PaintingCompleted.GivenTo(Rules.PaintingCompleted.GivenTo(afterOne));

        // The tests' settings: 80 coins and one booster each time, all seven in the engine's order.
        Assert.Equal(new Reward(new Coins(80), [BoosterKind.StartRocketAndBomb]), Rules.PaintingCompleted.NextFor(wallet));
        Assert.Equal(new Coins(180), afterOne.Coins);
        Assert.Equal(BoosterShelf.Empty.WithOneMore(BoosterKind.StartRocketAndBomb), afterOne.Shelf);
        Assert.Equal(1, afterOne.Rewards.ForPaintings);

        Assert.Equal(new Coins(340), afterThree.Coins);
        Assert.Equal(
            BoosterShelf.Empty.WithOneMore(BoosterKind.StartRocketAndBomb).WithOneMore(BoosterKind.StartColorBomb).WithOneMore(BoosterKind.StartExtraMoves),
            afterThree.Shelf);
        Assert.Equal(new Reward(new Coins(80), [BoosterKind.HitCell]), Rules.PaintingCompleted.NextFor(afterThree));

        // It touches nothing else, and no daily reward is counted with it.
        Assert.Equal(wallet.Lives, afterThree.Lives);
        Assert.Equal(wallet.Ads, afterThree.Ads);
        Assert.Equal(new RewardsGiven(forPaintings: 3, daily: 0, lastDailyOn: null), afterThree.Rewards);
    }

    [Fact]
    public void The_daily_reward_is_given_once_a_calendar_day()
    {
        Wallet wallet = WalletWith(0);
        Assert.True(Rules.Daily.IsDueOn(wallet, Today));
        Assert.Null(Rules.Daily.DueOn(wallet));

        var first = Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(wallet, Today));

        // The tests' settings: 5 coins and one booster of the rotation.
        Assert.Equal(new Reward(new Coins(5), [BoosterKind.StartRocketAndBomb]), first.Reward);
        Assert.Equal(new Coins(5), first.Wallet.Coins);
        Assert.Equal(new RewardsGiven(forPaintings: 0, daily: 1, lastDailyOn: Today), first.Wallet.Rewards);

        // A second time on the same day it is refused, with the day it is due, and nothing changes.
        Assert.False(Rules.Daily.IsDueOn(first.Wallet, Today));
        Assert.Equal(new DailyGift.NotDueYet(DueOn: Today.Plus(1)), Rules.Daily.TakenOn(first.Wallet, Today));

        // The next calendar day gives the next booster of the rotation; a day missed is not made up for.
        var second = Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(first.Wallet, Today.Plus(1)));
        var afterAWeekAway = Assert.IsType<DailyGift.Given>(Rules.Daily.TakenOn(second.Wallet, Today.Plus(9)));
        Assert.Equal([BoosterKind.StartColorBomb], second.Reward.Boosters);
        Assert.Equal([BoosterKind.StartExtraMoves], afterAWeekAway.Reward.Boosters);
        Assert.Equal(3, afterAWeekAway.Wallet.Rewards.Daily);
        Assert.Equal(new Coins(15), afterAWeekAway.Wallet.Coins);
    }

    [Fact]
    public void A_daily_reward_every_few_days_waits_them_out_and_a_date_set_back_gives_none()
    {
        DailyReward everyThirdDay = MetaSupport.Rules(pack => pack.rewards.daily.everyDays = 3).Daily;
        Wallet taken = Assert.IsType<DailyGift.Given>(everyThirdDay.TakenOn(WalletWith(0), Today)).Wallet;

        Assert.Equal(
            [false, false, false, true, true],
            Enumerable.Range(0, 5).Select(days => everyThirdDay.IsDueOn(taken, Today.Plus(days))));
        Assert.Equal(new DailyGift.NotDueYet(DueOn: Today.Plus(3)), everyThirdDay.TakenOn(taken, Today.Plus(2)));

        // The phone's date set back: a day before the latest reward's has no reward, however far back.
        var lastYear = new DayStamp(Today.Number - 365);
        Assert.False(Rules.Daily.IsDueOn(taken, lastYear));
        Assert.Equal(new DailyGift.NotDueYet(DueOn: Today.Plus(1)), Rules.Daily.TakenOn(taken, lastYear));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DailyReward(Rules.Daily.Rotation, everyDays: 0));
    }
}
