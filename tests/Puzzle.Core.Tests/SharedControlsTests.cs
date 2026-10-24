using Puzzle.Client;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the Unity game's shared controls decide before anything is drawn: where a card hangs in front of a page of
/// the museum, what a booster's disc shows of a stock and a choice, how a row of pips is set out, how amounts and
/// waits are written, and where the parts of a disc and of a card lie in the draw order. <c>PageLayout</c>,
/// <c>BoosterFace</c>, <c>PipRowPlan</c>, <c>NumberWords</c> and <c>DrawOrder</c> are the Unity project's source
/// files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class SharedControlsTests
{
    /// <summary>The room the level-start card of the design asks of a screen at the share the result card is drawn at: its body, half its nameplate, and the play button under it.</summary>
    private const float CardWidth = 332f;
    private const float CardHeight = 420f;

    public static TheoryData<int, int, int, int> Screens() => new TheoryData<int, int, int, int>
    {
        { 1080, 2340, 0, 0 },
        { 1080, 2340, 110, 60 },
        { 1080, 2424, 96, 48 },
        { 1080, 1920, 0, 0 },
        { 1080, 1920, 90, 48 },
        { 2076, 2152, 0, 0 },
        { 2076, 2152, 80, 60 },
        { 720, 1280, 0, 0 },
        { 2048, 2732, 48, 40 },
        { 1640, 2360, 48, 40 },
        { 1488, 2266, 48, 40 },
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void A_card_over_a_page_hangs_inside_the_usable_screen_at_its_full_size(int width, int height, int topInset, int bottomInset)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));

        CardPlace place = page.PlaceOfACard(CardWidth, CardHeight);

        Assert.Equal(1f, place.Scale);
        Assert.True(place.Middle + CardHeight / 2 <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.001f, "the card runs under the cutout");
        Assert.True(place.Middle - CardHeight / 2 >= page.View.UsableBottom + PageLayout.CardMarginAboveAndBelow - 0.001f, "the card runs under the system bar");
        Assert.True(CardWidth <= page.View.Width - 2 * PageLayout.CardSideMargin + 0.001f, "the card is cut off at the sides");
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(1080, 1920)]
    [InlineData(2076, 2152)]
    public void A_card_over_a_page_hangs_a_little_above_the_middle_as_the_design_hangs_its_cards(int width, int height)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height));

        // LevelStartTR and SettingsTR: 13.5 points above the middle of the canvas.
        Assert.Equal(PageLayout.CardAboveTheMiddle, page.PlaceOfACard(CardWidth, CardHeight).Middle, precision: 3);
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void A_card_too_tall_for_a_screen_is_drawn_smaller_before_anything_of_it_is_cut_off(int width, int height, int topInset, int bottomInset)
    {
        const float tallCard = 900f;
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));

        CardPlace place = page.PlaceOfACard(CardWidth, tallCard);

        float room = page.View.UsableTop - page.View.UsableBottom - 2 * PageLayout.CardMarginAboveAndBelow;
        Assert.Equal(room / tallCard, place.Scale, precision: 4);
        Assert.Equal(page.View.UsableTop - PageLayout.CardMarginAboveAndBelow, place.Middle + place.Scale * tallCard / 2, precision: 2);
        Assert.Equal(page.View.UsableBottom + PageLayout.CardMarginAboveAndBelow, place.Middle - place.Scale * tallCard / 2, precision: 2);
    }

    [Fact]
    public void A_card_that_nearly_fills_the_screen_gives_up_its_place_above_the_middle_and_stays_inside()
    {
        PageLayout page = PageLayout.Of(new ScreenShape(1080, 1920, topInsetPixels: 90, bottomInsetPixels: 48));
        float room = page.View.UsableTop - page.View.UsableBottom - 2 * PageLayout.CardMarginAboveAndBelow;

        CardPlace place = page.PlaceOfACard(CardWidth, room - 10f);

        Assert.Equal(1f, place.Scale);
        Assert.Equal(page.View.UsableTop - PageLayout.CardMarginAboveAndBelow, place.Middle + (room - 10f) / 2, precision: 3);
    }

    [Fact]
    public void A_card_wider_than_a_narrow_screen_is_drawn_smaller_and_keeps_its_wall_at_the_sides()
    {
        // The canvas' own card is 362 points wide on a canvas of 390; the game's narrow screen is 360.
        PageLayout page = PageLayout.Of(new ScreenShape(1080, 2340));

        CardPlace place = page.PlaceOfACard(width: 362f, height: 458f);

        Assert.Equal((360f - 2 * PageLayout.CardSideMargin) / 362f, place.Scale, precision: 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => page.PlaceOfACard(0f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.PlaceOfACard(100f, -1f));
    }

    [Fact]
    public void A_booster_the_player_owns_shows_its_count_and_one_they_own_none_of_offers_more()
    {
        BoosterFace owned = BoosterFace.Of(owned: 3, BoosterChoice.Waiting);
        BoosterFace none = BoosterFace.Of(owned: 0, BoosterChoice.Waiting);

        Assert.Equal((BoosterBadge.Count, 3), (owned.Badge, owned.Owned));
        Assert.Equal(BoosterBadge.GetMore, none.Badge);
        Assert.All(new[] { owned, none }, face => Assert.False(face.IsRinged || face.IsTicked || face.IsFaint));
    }

    [Fact]
    public void A_chosen_booster_stands_in_the_ring_with_a_tick_and_an_armed_one_in_the_ring_alone()
    {
        BoosterFace chosen = BoosterFace.Of(owned: 2, BoosterChoice.Chosen);
        BoosterFace armed = BoosterFace.Of(owned: 2, BoosterChoice.Armed);

        Assert.True(chosen.IsRinged && chosen.IsTicked && !chosen.IsFaint);
        Assert.True(armed.IsRinged && !armed.IsTicked && !armed.IsFaint);
        Assert.All(new[] { chosen, armed }, face => Assert.Equal(BoosterBadge.Count, face.Badge));
    }

    [Fact]
    public void A_booster_shown_as_goods_has_a_bare_disc_that_is_neither_ringed_nor_faint()
    {
        BoosterFace goods = BoosterFace.Goods;

        Assert.Equal((BoosterBadge.None, true), (goods.Badge, goods.IsGoods));
        Assert.False(goods.IsRinged || goods.IsTicked || goods.IsFaint);
        Assert.NotEqual(BoosterFace.Of(owned: 0, BoosterChoice.Waiting), goods);
        Assert.Equal(BoosterFace.Goods, goods);
    }

    [Fact]
    public void A_booster_that_is_set_aside_is_faint_whatever_the_player_owns_of_it()
    {
        Assert.True(BoosterFace.Of(owned: 4, BoosterChoice.SetAside).IsFaint);
        Assert.Equal(BoosterBadge.Count, BoosterFace.Of(owned: 4, BoosterChoice.SetAside).Badge);
        Assert.True(BoosterFace.Of(owned: 0, BoosterChoice.SetAside).IsFaint);
        Assert.Equal(BoosterBadge.GetMore, BoosterFace.Of(owned: 0, BoosterChoice.SetAside).Badge);
    }

    [Fact]
    public void Only_a_booster_the_player_owns_can_be_chosen_or_armed()
    {
        Assert.Throws<ArgumentException>(() => BoosterFace.Of(owned: 0, BoosterChoice.Chosen));
        Assert.Throws<ArgumentException>(() => BoosterFace.Of(owned: 0, BoosterChoice.Armed));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoosterFace.Of(owned: -1, BoosterChoice.Waiting));
        Assert.Equal(BoosterFace.Of(2, BoosterChoice.Chosen), BoosterFace.Of(2, BoosterChoice.Chosen));
        Assert.NotEqual(BoosterFace.Of(2, BoosterChoice.Chosen), BoosterFace.Of(2, BoosterChoice.Armed));
    }

    [Fact]
    public void A_row_of_pips_shows_the_pieces_won_before_then_the_ones_just_won_then_the_ones_to_win()
    {
        // WinTR: "Yeni parça: 7 / 10".
        PipRowPlan row = PipRowPlan.Of(pieceCount: 10, won: 7, justWon: 1);

        Assert.Equal(
            Enumerable.Repeat(PipFace.Won, 6).Append(PipFace.JustWon).Concat(Enumerable.Repeat(PipFace.NotWon, 3)),
            row.Faces);
        Assert.All(PipRowPlan.Of(pieceCount: 5, won: 0, justWon: 0).Faces, face => Assert.Equal(PipFace.NotWon, face));
        Assert.All(PipRowPlan.Of(pieceCount: 5, won: 5, justWon: 0).Faces, face => Assert.Equal(PipFace.Won, face));
    }

    [Fact]
    public void The_pips_stand_evenly_around_the_rows_middle_and_a_pip_just_won_has_more_room_at_its_sides()
    {
        PipRowPlan plain = PipRowPlan.Of(pieceCount: 10, won: 6, justWon: 0);
        PipRowPlan withANewOne = PipRowPlan.Of(pieceCount: 10, won: 7, justWon: 1);
        float pitch = PipRowPlan.PipWidth + PipRowPlan.Gap;

        // WinTR: ten pips of 19 with nine gaps of 5.5, and 3.2 more at each side of the seventh.
        Assert.Equal(10 * PipRowPlan.PipWidth + 9 * PipRowPlan.Gap, plain.Width, precision: 3);
        Assert.Equal(plain.Width + 2 * PipRowPlan.RoomBesideAPipJustWon, withANewOne.Width, precision: 3);
        Assert.Equal(-plain.MiddleOf(0), plain.MiddleOf(9), precision: 3);
        Assert.Equal(pitch, plain.MiddleOf(4) - plain.MiddleOf(3), precision: 3);
        Assert.Equal(pitch + PipRowPlan.RoomBesideAPipJustWon, withANewOne.MiddleOf(6) - withANewOne.MiddleOf(5), precision: 3);
        Assert.Equal(pitch + PipRowPlan.RoomBesideAPipJustWon, withANewOne.MiddleOf(7) - withANewOne.MiddleOf(6), precision: 3);
        Assert.Equal(-(withANewOne.Width - PipRowPlan.PipWidth) / 2, withANewOne.MiddleOf(0), precision: 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => plain.MiddleOf(10));
    }

    [Fact]
    public void A_row_of_pips_refuses_counts_no_painting_can_have()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PipRowPlan.Of(pieceCount: 0, won: 0, justWon: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PipRowPlan.Of(pieceCount: 10, won: 11, justWon: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PipRowPlan.Of(pieceCount: 10, won: 3, justWon: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PipRowPlan.Of(pieceCount: 10, won: 3, justWon: -1));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(45, "45")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.000")]
    [InlineData(1250, "1.250")]
    [InlineData(12500, "12.500")]
    [InlineData(1000000, "1.000.000")]
    [InlineData(int.MaxValue, "2.147.483.647")]
    public void An_amount_is_written_with_a_point_between_its_thousands(int amount, string written)
    {
        Assert.Equal(written, NumberWords.Amount(amount));
    }

    [Fact]
    public void No_amount_on_a_screen_is_less_than_nothing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberWords.Amount(-1));
    }

    [Theory]
    [InlineData(754.0, "12:34")]
    [InlineData(7.0, "00:07")]
    [InlineData(0.2, "00:01")]
    [InlineData(59.01, "01:00")]
    [InlineData(1800.0, "30:00")]
    [InlineData(4500.0, "75:00")]
    [InlineData(0.0, "00:00")]
    [InlineData(-5.0, "00:00")]
    public void A_short_wait_is_written_as_minutes_and_seconds_and_a_second_that_has_begun_counts(double seconds, string written)
    {
        Assert.Equal(written, NumberWords.Countdown(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(102.0, "1 sa 42 dk")]
    [InlineData(101.2, "1 sa 42 dk")]
    [InlineData(120.0, "2 sa")]
    [InlineData(119.5, "2 sa")]
    [InlineData(42.0, "42 dk")]
    [InlineData(0.1, "1 dk")]
    [InlineData(0.0, "0 dk")]
    [InlineData(-3.0, "0 dk")]
    [InlineData(1500.0, "25 sa")]
    public void A_long_wait_is_written_in_hours_and_minutes_and_a_minute_that_has_begun_counts(double minutes, string written)
    {
        Assert.Equal(written, NumberWords.HoursAndMinutes(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Amounts_and_waits_can_be_written_with_the_letter_set()
    {
        string[] written =
        {
            NumberWords.Amount(1234567890), NumberWords.Countdown(TimeSpan.FromSeconds(754)),
            NumberWords.HoursAndMinutes(TimeSpan.FromMinutes(102)), NumberWords.HoursAndMinutes(TimeSpan.FromMinutes(7)),
        };

        Assert.All(written, words => Assert.Equal(string.Empty, LetterSet.LettersOutside(words)));
    }

    [Fact]
    public void What_stands_on_a_booster_disc_is_drawn_over_the_disc_in_one_order_wherever_the_disc_lies()
    {
        DiscPart[] parts = Enum.GetValues<DiscPart>();

        Assert.Equal(new[] { DiscPart.Disc, DiscPart.Picture, DiscPart.Ring, DiscPart.Badge, DiscPart.BadgeLettering }, parts);
        Assert.Equal(0, (int)DiscPart.Disc);

        // A picture of two pieces takes two places: the ring comes over both.
        Assert.True((int)DiscPart.Ring >= (int)DiscPart.Picture + 2);
        Assert.Equal(parts.Select(part => (int)part).OrderBy(place => place), parts.Select(part => (int)part));
    }

    [Theory]
    [InlineData(DrawOrder.ShelfDisc, DrawOrder.ShelfButtonGlyph, DrawOrder.Tray)]
    [InlineData(DrawOrder.CardDisc, DrawOrder.CardLight, DrawOrder.CardExhibit)]
    public void A_discs_parts_have_places_of_their_own_between_what_lies_under_and_over_them(DrawOrder disc, DrawOrder under, DrawOrder over)
    {
        int[] places = Enum.GetValues<DiscPart>().Select(part => (int)disc + (int)part).ToArray();
        int topmost = (int)disc + (int)DiscPart.BadgeLettering;

        Assert.All(places, place => Assert.InRange(place, (int)under + 1, (int)over - 1));

        // The places above the disc's own are the disc's alone: nothing else of the list lies among them.
        Assert.DoesNotContain(Enum.GetValues<DrawOrder>(), place => (int)place > (int)disc && (int)place <= topmost);
    }

    [Fact]
    public void A_round_buttons_mark_takes_the_place_above_its_disc_on_the_shelf_and_on_a_card()
    {
        Assert.Equal((int)DrawOrder.ShelfButton + 1, (int)DrawOrder.ShelfButtonGlyph);
        Assert.Equal((int)DrawOrder.CardCloseButton + 1, (int)DrawOrder.CardCloseGlyph);
        Assert.Equal((int)DrawOrder.WalletButton + 1, (int)DrawOrder.WalletButtonGlyph);

        // Under a card the shelf's pause disc lies in the dark with the shelf: every card has its own way out.
        Assert.True(DrawOrder.ShelfButtonGlyph < DrawOrder.Veil);
    }

    [Fact]
    public void A_card_lies_over_the_dark_with_its_nameplate_and_its_close_button_over_all_that_stands_on_it()
    {
        Assert.True(DrawOrder.Veil < DrawOrder.CardWire && DrawOrder.CardWire < DrawOrder.Card);
        Assert.True(DrawOrder.CardArt < DrawOrder.CardDisc && DrawOrder.CardLettering < DrawOrder.CardNameplate);
        Assert.True(DrawOrder.CardNameplate < DrawOrder.CardNameplateLettering && DrawOrder.CardNameplateLettering < DrawOrder.CardNameplatePin);
        Assert.True(DrawOrder.CardNameplatePin < DrawOrder.CardCloseButton);

        // What is sunk into a button's face on a card takes the two places above the face, under everything written.
        Assert.True((int)DrawOrder.CardArt + 2 < (int)DrawOrder.CardExhibit);
    }

    [Fact]
    public void A_price_has_places_for_its_pill_and_its_coin_under_the_lettering_of_a_card_and_none_on_a_page_button()
    {
        // The pill of a price and the coin in it take the two places above a button's face; the button's words must come over both.
        Assert.True((int)DrawOrder.CardArt + 2 < (int)DrawOrder.CardLettering);

        // A page button's words lie on the place above its face: a price there would be drawn among them, which the button refuses.
        Assert.False((int)DrawOrder.PageButton + 2 < (int)DrawOrder.PageButtonLettering);
    }

    [Fact]
    public void A_nameplate_on_a_plaque_has_its_lettering_over_its_plate_and_its_studs_at_a_place_of_their_own()
    {
        // The banner of a restored room carries a card's nameplate: the plate is plaque art, its title plaque lettering.
        Assert.True(DrawOrder.PlaqueArt < DrawOrder.PlaqueLettering && DrawOrder.PlaqueLettering < DrawOrder.PlaqueNameplatePin);
        Assert.True(DrawOrder.PlaqueNameplatePin > DrawOrder.PlaquePictureMark && DrawOrder.PlaqueNameplatePin < DrawOrder.PlaqueRing);
    }

    [Fact]
    public void A_second_card_hangs_with_all_of_itself_over_the_dark_that_falls_on_the_first()
    {
        int lowestOfTheSecond = (int)DrawOrder.CardWire + (int)DrawOrder.OverACard;
        int topmostOfTheSecond = (int)DrawOrder.CardCloseGlyph + (int)DrawOrder.OverACard;

        Assert.True((int)DrawOrder.SecondVeil > (int)DrawOrder.CardCloseGlyph, "the second dark falls under the first card");
        Assert.True(lowestOfTheSecond > (int)DrawOrder.SecondVeil, "the second card hangs under its own dark");
        Assert.True(topmostOfTheSecond < (int)DrawOrder.OverTheDark);
    }

    [Fact]
    public void The_wallet_row_lies_with_the_bar_over_the_pages_and_under_the_dark()
    {
        DrawOrder[] row = { DrawOrder.WalletPill, DrawOrder.WalletMark, DrawOrder.WalletLettering, DrawOrder.WalletButton, DrawOrder.WalletButtonGlyph };

        Assert.Equal(row.OrderBy(place => (int)place), row);
        Assert.All(row, place => Assert.True(place > DrawOrder.Celebration && place > DrawOrder.BarLettering && place < DrawOrder.Veil));
    }
}
