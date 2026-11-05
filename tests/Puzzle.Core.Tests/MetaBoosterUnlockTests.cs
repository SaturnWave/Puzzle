using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// The boosters used during play open one at a time (the owner's decision of 2026-10-15): before its level a booster
/// cannot be used, on its level it is shown and its gift is given, once for good, also to a save already past the
/// level. The tests' pack opens the spatula on level 7, the brushes on 12 and 16, the shuffle on 20, with three, two,
/// one and no boosters of their kind (<see cref="MetaSupport"/>).
/// </summary>
public class MetaBoosterUnlockTests
{
    private static readonly BoosterUnlocks Unlocks = Rules().Unlocks;

    [Theory]
    [InlineData(BoosterKind.HitCell, 7)]
    [InlineData(BoosterKind.HitRow, 12)]
    [InlineData(BoosterKind.HitColumn, 16)]
    [InlineData(BoosterKind.Shuffle, 20)]
    public void A_booster_used_during_play_is_closed_on_the_level_before_its_own_and_open_from_its_own_on(BoosterKind booster, int level)
    {
        Assert.False(Unlocks.IsOpenOn(booster, level - 1));
        Assert.True(Unlocks.IsOpenOn(booster, level));
        Assert.True(Unlocks.IsOpenOn(booster, level + 1));
        Assert.Equal(new[] { booster }, Unlocks.OpeningOn(level));
        Assert.Equal(level, Unlocks.LevelOf(booster));
    }

    [Fact]
    public void The_start_boosters_are_open_from_the_first_level_and_have_no_level_of_their_own()
    {
        Assert.All(BoosterKinds.All.Where(BoosterKinds.IsStart), booster => Assert.True(Unlocks.IsOpenOn(booster, 1)));
        Assert.Throws<ArgumentException>(() => Unlocks.LevelOf(BoosterKind.StartColorBomb));
        Assert.Empty(Unlocks.OpeningOn(1));
        Assert.Empty(Unlocks.OpeningOn(8));
    }

    [Fact]
    public void A_boosters_gift_is_given_on_its_level_once_and_kept_as_given()
    {
        Wallet onSix = Unlocks.GivenTo(Wallet.Empty, level: 6);
        Wallet onSeven = Unlocks.GivenTo(onSix, level: 7);
        Wallet onSevenAgain = Unlocks.GivenTo(onSeven, level: 7);

        Assert.Same(Wallet.Empty, onSix);
        Assert.Equal(3, onSeven.Shelf.Count(BoosterKind.HitCell));
        Assert.Equal(BoosterKindSet.Of(new[] { BoosterKind.HitCell }), onSeven.Rewards.UnlockGifts);
        Assert.Same(onSeven, onSevenAgain);

        // A booster used up is not given again by the next level's question.
        Wallet usedUp = onSeven.WithShelf(BoosterShelf.Empty);
        Assert.Same(usedUp, Unlocks.GivenTo(usedUp, level: 8));
    }

    [Fact]
    public void A_wallet_past_every_opening_that_was_given_none_is_given_each_once()
    {
        Wallet oldSave = WalletWith(500);

        Wallet given = Unlocks.GivenTo(oldSave, level: 30);

        Assert.Equal(BoosterKindSet.Of(BoosterKinds.InLevel), given.Rewards.UnlockGifts);
        Assert.Equal((3, 2, 1, 0), (given.Shelf.Count(BoosterKind.HitCell), given.Shelf.Count(BoosterKind.HitRow), given.Shelf.Count(BoosterKind.HitColumn), given.Shelf.Count(BoosterKind.Shuffle)));
        Assert.Equal(new[] { BoosterKind.HitCell, BoosterKind.HitRow, BoosterKind.HitColumn, BoosterKind.Shuffle }, Unlocks.GiftsDueOn(oldSave, level: 30));
        Assert.Empty(Unlocks.GiftsDueOn(given, level: 30));
        Assert.Equal(oldSave.Coins, given.Coins);
        Assert.Same(given, Unlocks.GivenTo(given, level: 1000));
    }

    [Fact]
    public void Openings_that_no_rule_could_have_are_refused()
    {
        var everyLevel = BoosterKinds.InLevel.ToDictionary(kind => kind, kind => 5);

        Assert.Throws<ArgumentException>(() => new BoosterUnlocks(everyLevel.Where(opens => opens.Key != BoosterKind.Shuffle).ToDictionary(opens => opens.Key, opens => opens.Value), BoosterShelf.Empty));
        Assert.Throws<ArgumentException>(() => new BoosterUnlocks(everyLevel.Append(new KeyValuePair<BoosterKind, int>(BoosterKind.StartColorBomb, 3)).ToDictionary(opens => opens.Key, opens => opens.Value), BoosterShelf.Empty));
        Assert.Throws<ArgumentException>(() => new BoosterUnlocks(everyLevel.ToDictionary(opens => opens.Key, opens => 0), BoosterShelf.Empty));
        Assert.Throws<ArgumentException>(() => new BoosterUnlocks(everyLevel, BoosterShelf.Empty.With(BoosterKind.StartExtraMoves, 1)));
        Assert.Throws<ArgumentException>(() => new RewardsGiven(0, 0, null, BoosterKindSet.Of(new[] { BoosterKind.StartColorBomb })));
    }

    [Fact]
    public void A_set_of_kinds_is_equal_to_another_that_holds_the_same_kinds()
    {
        BoosterKindSet one = BoosterKindSet.None.With(BoosterKind.Shuffle).With(BoosterKind.HitCell);
        BoosterKindSet other = BoosterKindSet.Of(new[] { BoosterKind.HitCell, BoosterKind.Shuffle, BoosterKind.HitCell });

        Assert.Equal(one, other);
        Assert.Equal(new[] { BoosterKind.HitCell, BoosterKind.Shuffle }, one.Kinds);
        Assert.NotEqual(BoosterKindSet.None, one);
        Assert.Equal("hit-cell, shuffle", one.ToString());
    }
}
