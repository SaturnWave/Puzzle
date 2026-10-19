using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Meta;
using static Puzzle.Core.Tests.TestBoards;
using Box = Puzzle.Client.Layout.Box;

namespace Puzzle.Core.Tests;

/// <summary>
/// The board's shelf of boosters used during play in the Unity game, as far as it is plain code: which discs are
/// open on a level and what each shows (<c>ShelfBrief</c>, <c>BoosterFace</c>), the words of the pill over the
/// shelf (<c>BoosterWords</c>, <c>PillWords</c>), where a finger presses a disc (<c>ScreenLayout</c>) and how a
/// finger aims an armed booster (<c>BoardPointer</c>). The Unity project's source files, compiled into these tests.
/// The rules are the tests' pack: the spatula opens on level 7, the brushes on 12 and 16, the shuffle on 20.
/// </summary>
public class ShelfOfBoostersTests
{
    private static readonly EconomyRules TheRules = MetaSupport.Rules();

    [Theory]
    [InlineData(BoosterKind.HitCell, 7)]
    [InlineData(BoosterKind.HitRow, 12)]
    [InlineData(BoosterKind.HitColumn, 16)]
    [InlineData(BoosterKind.Shuffle, 20)]
    public void A_disc_is_locked_with_its_level_on_it_before_that_level_and_counts_what_is_held_from_it_on(BoosterKind booster, int level)
    {
        BoosterShelf held = BoosterShelf.Empty.With(booster, 2);

        BoosterFace before = ShelfBrief.Of(level - 1, TheRules).FaceOf(booster, held, BoosterChoice.Waiting);
        BoosterFace on = ShelfBrief.Of(level, TheRules).FaceOf(booster, held, BoosterChoice.Waiting);

        Assert.Equal(BoosterFace.LockedUntil(level), before);
        Assert.Equal((BoosterBadge.OpensOn, true, level), (before.Badge, before.IsFaint, before.OpensOn!.Value));
        Assert.Equal(BoosterFace.Of(2, BoosterChoice.Waiting), on);
        Assert.Equal(BoosterBadge.Count, on.Badge);
        Assert.Equal("Bölüm " + level, BoardWords.OpensOn(level));
    }

    [Fact]
    public void A_locked_face_or_an_armed_one_held_none_of_cannot_be_made()
    {
        Assert.Throws<ArgumentException>(() => BoosterFace.Of(2, BoosterChoice.Locked));
        Assert.Throws<ArgumentException>(() => BoosterFace.Of(0, BoosterChoice.Armed));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoosterFace.LockedUntil(1));
        Assert.False(BoosterFace.Of(0, BoosterChoice.Waiting).IsFaint);
        Assert.Equal(BoosterBadge.GetMore, BoosterFace.Of(0, BoosterChoice.Waiting).Badge);
    }

    [Fact]
    public void The_pill_names_the_armed_booster_says_what_it_does_and_what_to_pick()
    {
        Assert.Equal("Spatula · bir taşı ya da bir katı kaldırır", BoosterWords.Armed(BoosterKind.HitCell));
        Assert.Equal("Bir taş seç", BoosterWords.WhatToPick(BoosterKind.HitCell));
        Assert.Equal("Yatay fırça · bir satırı boyar", BoosterWords.Armed(BoosterKind.HitRow));
        Assert.Equal("Bir satır seç", BoosterWords.WhatToPick(BoosterKind.HitRow));
        Assert.Equal("Bir sütun seç", BoosterWords.WhatToPick(BoosterKind.HitColumn));
        Assert.Throws<ArgumentException>(() => BoosterWords.WhatToPick(BoosterKind.Shuffle));
        Assert.Throws<ArgumentException>(() => BoosterWords.NameOf(BoosterKind.StartColorBomb));

        // A refusal says why; the card that offers one says what it does as a sentence of its own.
        Assert.Equal("Burada vurulacak bir şey yok", BoosterWords.Refused(BoosterKind.HitColumn));
        Assert.Equal("Tahta karıştırılamıyor", BoosterWords.Refused(BoosterKind.Shuffle));
        Assert.Equal("Bir satırı boyar", BoosterWords.OnTheCard(BoosterKind.HitRow));
        Assert.Equal("Her bölüm booster'sız geçilebilir", BoosterWords.EveryLevelWithout);
    }

    [Fact]
    public void The_pills_words_are_equal_when_they_say_the_same()
    {
        Assert.Equal(new PillWords.Line("a"), new PillWords.Line("a"));
        Assert.NotEqual<PillWords>(new PillWords.Line("a"), new PillWords.Lesson("a", "b", null));
        Assert.Equal(new PillWords.Armed("a", "b"), new PillWords.Armed("a", "b"));
        Assert.Equal(PillWords.None, new PillWords.Silent());
        Assert.Equal("a / b", new PillWords.Armed("a", "b").ToString());
        Assert.Equal(string.Empty, PillWords.None.ToString());
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(1080, 1920)]
    [InlineData(2076, 2152)]
    [InlineData(720, 1280)]
    public void A_finger_presses_a_boosters_disc_from_44_points_across_in_the_hand_and_two_discs_reaches_never_meet(int width, int height)
    {
        ScreenLayout layout = ScreenLayout.Of(new ScreenShape(width, height), new BoardFootprint(10, 12, hasExits: false));
        IReadOnlyList<Box> discs = layout.ShelfDiscs;
        float reach = layout.ShelfTouchReach;

        Assert.True(2f * reach * layout.PointInTheHand >= ScreenLayout.SmallestTouchInTheHand - 0.001f, $"a disc takes a finger {2f * reach * layout.PointInTheHand} points across in the hand");
        Assert.True(reach >= discs[1].Width / 2f, "a finger on the rim of a disc misses it");
        for (int place = 2; place < discs.Count; place++)
        {
            Assert.True(discs[place].MiddleX - discs[place - 1].MiddleX >= 2f * reach - 0.001f, "two discs' reaches meet");
        }

        Assert.True(discs[1].MiddleX - reach >= layout.PauseButton.Right - 0.001f, "the first booster's reach runs into the pause disc");
    }

    [Fact]
    public void An_aiming_finger_lights_the_cell_under_it_follows_it_as_it_slides_and_lifting_aims_there()
    {
        var pointer = new BoardPointer(Start(Level(Filler(6, 6).With(3, 3, 'H'))));

        pointer.AimDown(1.5f, 2.5f);
        Assert.Equal(P(1, 2), pointer.AimedAt);
        pointer.AimMovedTo(4.2f, 2.9f);
        Assert.Equal(P(4, 2), pointer.AimedAt);

        BoardGesture.Aim aim = Assert.IsType<BoardGesture.Aim>(pointer.AimUp());
        Assert.Equal(P(4, 2), aim.Cell);
        Assert.Null(pointer.AimedAt);
        Assert.Throws<InvalidOperationException>(() => pointer.MoveMeantBy(aim));
    }

    [Fact]
    public void An_aiming_finger_that_lifts_off_the_board_or_is_taken_away_aims_at_nothing()
    {
        var pointer = new BoardPointer(Start(Level(Filler(6, 6).With(3, 3, 'H'))));

        pointer.AimDown(1.5f, 1.5f);
        pointer.AimMovedTo(7.5f, 1.5f);
        Assert.Null(pointer.AimedAt);
        Assert.Null(pointer.AimUp());

        pointer.AimDown(1.5f, 1.5f);
        pointer.Cancel();
        Assert.Null(pointer.AimedAt);
        Assert.Null(pointer.AimUp());

        // A gap in the board is no cell to aim at either.
        string[] cells = Blank(6, 6, '#').With(2, 2, '.');
        var withAGap = new BoardPointer(Start(Level(Filler(6, 6).With(3, 3, 'H').With(2, 2, '.'), cells: cells)));
        withAGap.AimDown(2.5f, 2.5f);
        Assert.Null(withAGap.AimedAt);
    }
}
