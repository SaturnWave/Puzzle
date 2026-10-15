using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using static Puzzle.Core.Tests.MetaSupport;
using static Puzzle.Core.Tests.TestBoards;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the card before a level shows and does with the start boosters, by the economy's rules: a press chooses a
/// booster the player holds and a second press puts it back, one held none of is offered and not chosen, no more
/// are chosen than the settings allow, the booster of extra moves shows its moves on the medallion, and nothing is
/// ever chosen for the player. <c>LevelBrief</c>, <c>LevelStartWords</c> and <c>BoosterFace</c> are the Unity
/// project's source files, compiled into these tests. The rules are the tests' pack (<see cref="MetaSupport"/>): two
/// start boosters an attempt, a new player with one rocket-and-bomb, single prices of 600, 1 000 and 500.
/// </summary>
public class StartChoiceTests
{
    private static readonly EconomyRules TheRules = Rules();

    private static readonly BoosterShelf OneOfEachStartBooster = LevelBrief.StartBoosters.Aggregate(BoosterShelf.Empty, (shelf, booster) => shelf.WithOneMore(booster));

    /// <summary>A level of 14 moves with two goals: twelve red gems and sixteen blue ones.</summary>
    private static LevelDefinition TwoGoalLevel() => Level(
        Filler(6, 6), goals: new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, 12), new GoalSpec.CollectColor(GemColor.Blue, 16) }, moves: 14);

    private static LevelBrief BriefOf(LevelDefinition level, PieceReward? reward = null, EconomyRules? rules = null) =>
        LevelBrief.Of(level, reward, rules ?? TheRules, RuleSet.Default);

    private static StartChoice Chosen(LevelBrief brief, StartChoice choice, BoosterKind booster, BoosterShelf owned) =>
        Assert.IsType<StartToggle.Changed>(brief.Press(choice, booster, owned)).Choice;

    [Fact]
    public void The_card_tells_the_level_its_goals_its_moves_and_the_piece_winning_it_gives()
    {
        PieceReward reward = PieceReward.ForWinning(new PlayerProgress(levelsWon: 46, attemptsOnLevel: 0), levelCount: 1000, TestMuseums.EndingAfterRoomOne)!;

        LevelBrief brief = BriefOf(TwoGoalLevel(), reward);

        Assert.Equal((1, 14), (brief.LevelNumber, brief.MoveLimit));
        Assert.Equal(new[] { 12, 16 }, brief.Goals.Select(goal => goal.Remaining).ToArray());
        Assert.Same(reward, brief.Reward);
        Assert.Equal("Bölüm 47", LevelStartWords.Title(47));
        Assert.Equal("ÖDÜL · PARÇA 7 / 10", LevelStartWords.Reward(reward));
    }

    [Fact]
    public void A_level_that_gives_no_piece_has_a_card_without_the_reward_line()
    {
        // The last level of a pack won once more gives nothing new.
        PieceReward? reward = PieceReward.ForWinning(new PlayerProgress(levelsWon: 1000, attemptsOnLevel: 0), levelCount: 1000, TestMuseums.EndingAfterRoomOne);

        LevelBrief brief = BriefOf(TwoGoalLevel(), reward);

        Assert.Null(brief.Reward);
    }

    [Fact]
    public void Nothing_is_chosen_for_the_player_and_each_disc_shows_how_many_are_held()
    {
        BoosterShelf owned = TheRules.StartingWallet.Shelf;
        LevelBrief brief = BriefOf(TwoGoalLevel());

        BoosterFace[] faces = LevelBrief.StartBoosters.Select(booster => brief.FaceOf(booster, owned, StartChoice.None)).ToArray();

        Assert.Equal(new[] { BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb, BoosterKind.StartExtraMoves }, LevelBrief.StartBoosters);
        Assert.Equal(new[] { BoosterFace.Of(1, BoosterChoice.Waiting), BoosterFace.Of(0, BoosterChoice.Waiting), BoosterFace.Of(0, BoosterChoice.Waiting) }, faces);
        Assert.Equal(new[] { BoosterBadge.Count, BoosterBadge.GetMore, BoosterBadge.GetMore }, faces.Select(face => face.Badge).ToArray());
        Assert.Equal(14, brief.MovesWith(StartChoice.None));
    }

    [Fact]
    public void A_held_booster_is_chosen_by_a_press_and_put_back_by_the_next()
    {
        BoosterShelf owned = TheRules.StartingWallet.Shelf;
        LevelBrief brief = BriefOf(TwoGoalLevel());

        StartChoice chosen = Chosen(brief, StartChoice.None, BoosterKind.StartRocketAndBomb, owned);
        BoosterFace face = brief.FaceOf(BoosterKind.StartRocketAndBomb, owned, chosen);
        StartChoice offAgain = Chosen(brief, chosen, BoosterKind.StartRocketAndBomb, owned);

        Assert.True(chosen.Has(BoosterKind.StartRocketAndBomb));
        Assert.True(face.IsRinged && face.IsTicked);
        Assert.Equal(1, face.Owned);
        Assert.Equal(StartChoice.None, offAgain);
        Assert.Equal(BoosterFace.Of(1, BoosterChoice.Waiting), brief.FaceOf(BoosterKind.StartRocketAndBomb, owned, offAgain));
    }

    [Fact]
    public void The_booster_of_extra_moves_shows_its_moves_on_the_medallion_while_it_is_chosen()
    {
        LevelBrief brief = BriefOf(TwoGoalLevel());

        StartChoice chosen = Chosen(brief, StartChoice.None, BoosterKind.StartExtraMoves, OneOfEachStartBooster);

        Assert.Equal(RuleSet.Default.StartBoosterExtraMoves, brief.ExtraMoves);
        Assert.Equal(14 + brief.ExtraMoves, brief.MovesWith(chosen));
        Assert.Equal(14, brief.MovesWith(Chosen(brief, chosen, BoosterKind.StartExtraMoves, OneOfEachStartBooster)));
        Assert.Equal("+3 hamle", LevelStartWords.OnTheShelf(BoosterKind.StartExtraMoves, extraMoves: 3));
    }

    [Fact]
    public void A_booster_held_none_of_shows_the_plus_and_a_press_on_it_offers_it_instead_of_choosing_it()
    {
        BoosterShelf owned = TheRules.StartingWallet.Shelf;
        LevelBrief brief = BriefOf(TwoGoalLevel());

        StartToggle pressed = brief.Press(StartChoice.None, BoosterKind.StartColorBomb, owned);

        Assert.Equal(new StartToggle.NoneOwned(BoosterKind.StartColorBomb), pressed);
        Assert.Equal(BoosterBadge.GetMore, brief.FaceOf(BoosterKind.StartColorBomb, owned, StartChoice.None).Badge);
        Assert.Throws<ArgumentException>(() => brief.FaceOf(BoosterKind.StartColorBomb, owned, Chosen(brief, StartChoice.None, BoosterKind.StartColorBomb, OneOfEachStartBooster)));
    }

    [Fact]
    public void No_more_start_boosters_are_chosen_for_one_attempt_than_the_settings_allow()
    {
        LevelBrief twoAnAttempt = BriefOf(TwoGoalLevel());
        LevelBrief oneAnAttempt = BriefOf(TwoGoalLevel(), rules: Rules(pack => pack.startBoosters.perAttempt = 1));

        StartChoice two = Chosen(twoAnAttempt, Chosen(twoAnAttempt, StartChoice.None, BoosterKind.StartRocketAndBomb, OneOfEachStartBooster), BoosterKind.StartColorBomb, OneOfEachStartBooster);
        StartChoice one = Chosen(oneAnAttempt, StartChoice.None, BoosterKind.StartColorBomb, OneOfEachStartBooster);

        Assert.Equal(new StartToggle.NoMoreThisAttempt(PerAttempt: 2), twoAnAttempt.Press(two, BoosterKind.StartExtraMoves, OneOfEachStartBooster));
        Assert.Equal(new StartToggle.NoMoreThisAttempt(PerAttempt: 1), oneAnAttempt.Press(one, BoosterKind.StartRocketAndBomb, OneOfEachStartBooster));

        // A chosen booster can always be put back, and another chosen in its place.
        StartChoice swapped = Chosen(oneAnAttempt, Chosen(oneAnAttempt, one, BoosterKind.StartColorBomb, OneOfEachStartBooster), BoosterKind.StartRocketAndBomb, OneOfEachStartBooster);
        Assert.Equal(new[] { BoosterKind.StartRocketAndBomb }, swapped.Boosters);
    }

    [Fact]
    public void The_shipped_settings_give_a_new_player_three_of_each_start_booster_and_let_one_attempt_take_all_three()
    {
        EconomyRules shipped = EconomyFileMapper.ToRules(JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), new JsonSerializerOptions { IncludeFields = true })!);
        LevelBrief brief = BriefOf(TwoGoalLevel(), rules: shipped);
        BoosterShelf owned = shipped.StartingWallet.Shelf;

        StartChoice all = LevelBrief.StartBoosters.Aggregate(StartChoice.None, (choice, booster) => Chosen(brief, choice, booster, owned));

        // The boosters used during play come with three of their kind as each opens (Puzzle.Meta's BoosterUnlocks).
        Assert.All(BoosterKinds.All, booster => Assert.Equal(booster.IsStart() ? 3 : 0, owned.Count(booster)));
        Assert.All(BoosterKinds.InLevel, booster => Assert.Equal(3, shipped.Unlocks.Gift.Count(booster)));
        Assert.Equal(3, all.Count);
        Assert.Equal(new[] { StartBooster.RocketAndBomb, StartBooster.ColorBomb, StartBooster.ExtraMoves }, all.ForTheEngine());
    }

    [Fact]
    public void A_booster_held_none_of_is_offered_at_its_single_price_which_the_coins_must_cover()
    {
        LevelBrief brief = BriefOf(TwoGoalLevel());

        Assert.Equal(new Coins(1000), brief.PriceOf(BoosterKind.StartColorBomb));
        Assert.False(brief.CanBuy(BoosterKind.StartColorBomb, new Coins(999)));
        Assert.True(brief.CanBuy(BoosterKind.StartColorBomb, new Coins(1000)));
        Assert.Equal(TheRules.Shop.PriceOf(BoosterKind.StartExtraMoves), brief.PriceOf(BoosterKind.StartExtraMoves));
    }

    [Fact]
    public void A_choice_keeps_only_the_boosters_the_shelf_still_holds()
    {
        LevelBrief brief = BriefOf(TwoGoalLevel());
        StartChoice both = Chosen(brief, Chosen(brief, StartChoice.None, BoosterKind.StartRocketAndBomb, OneOfEachStartBooster), BoosterKind.StartExtraMoves, OneOfEachStartBooster);

        StartChoice kept = brief.HeldOnly(both, OneOfEachStartBooster.WithOneFewer(BoosterKind.StartRocketAndBomb));

        Assert.Equal(new[] { BoosterKind.StartExtraMoves }, kept.Boosters);
        Assert.Equal(both, brief.HeldOnly(both, OneOfEachStartBooster));
    }

    [Fact]
    public void The_start_boosters_are_named_as_the_canvas_names_them_and_a_booster_of_play_has_no_place_on_the_card()
    {
        Assert.Equal("Fırça +\nsıçrama", LevelStartWords.OnTheShelf(BoosterKind.StartRocketAndBomb, extraMoves: 3));
        Assert.Equal("Gökkuşağı paleti", LevelStartWords.NameOf(BoosterKind.StartColorBomb, extraMoves: 3));
        Assert.Equal("Bölüm 3 hamle fazlasıyla başlar", LevelStartWords.WhatItDoes(BoosterKind.StartExtraMoves, extraMoves: 3));
        Assert.Throws<ArgumentException>(() => LevelStartWords.NameOf(BoosterKind.Shuffle, extraMoves: 3));
        Assert.Throws<ArgumentException>(() => BriefOf(TwoGoalLevel()).Press(StartChoice.None, BoosterKind.HitCell, OneOfEachStartBooster));
    }
}
