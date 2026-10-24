using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the settings card hangs on a museum page and where its rows stand, before anything is drawn: the card of the
/// design (SettingsTR) with as many switches as the build honours, on the screens the game is made for and on screens
/// with a cutout and a system bar, and the privacy and support cards that hang in front of it. <c>SettingsLayout</c>
/// and <c>PageLayout</c> are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class SettingsLayoutTests
{
    /// <summary>
    /// A card with no switch; the sounds and the buzz of play brought two; the design draws three, as this build has
    /// them with the music; the tester mode's row makes four while it holds.
    /// </summary>
    public static TheoryData<int> Switches() => new TheoryData<int> { 0, 2, 3, 4 };

    /// <summary>
    /// The tall phone the canvas is drawn for, the tight 9:16 one, the owner's phone folded and open, the two
    /// phones with a cutout and a system bar, and the largest and the smallest iPad upright.
    /// </summary>
    public static TheoryData<int, int, int, int, int> ScreensAndSwitches()
    {
        var data = new TheoryData<int, int, int, int, int>();
        foreach (int switches in new[] { 0, 2, 3, 4 })
        {
            data.Add(1080, 2340, 0, 0, switches);
            data.Add(1080, 1920, 0, 0, switches);
            data.Add(1080, 2424, 0, 0, switches);
            data.Add(2076, 2152, 0, 0, switches);
            data.Add(1080, 2340, 110, 60, switches);
            data.Add(1080, 1920, 90, 48, switches);
            data.Add(2048, 2732, 48, 40, switches);
            data.Add(1488, 2266, 48, 40, switches);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ScreensAndSwitches))]
    public void The_card_with_its_nameplate_hangs_inside_the_usable_screen_and_is_drawn_smaller_before_anything_is_cut_off(
        int width, int height, int topInset, int bottomInset, int switches)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        SettingsLayout card = SettingsLayout.For(switches);
        float room = page.View.UsableTop - page.View.UsableBottom - 2 * PageLayout.CardMarginAboveAndBelow;
        float widest = (page.View.Width - 2 * PageLayout.CardSideMargin) / SettingsLayout.BodyWidth;

        CardPlace place = card.PlaceOn(page);

        Assert.Equal(Math.Min(1f, Math.Min(widest, room / card.Height)), place.Scale, precision: 4);
        Assert.True(place.Middle + place.Scale * card.Height / 2f <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.01f, "the nameplate runs under the cutout");
        Assert.True(place.Middle - place.Scale * card.Height / 2f >= page.View.UsableBottom + PageLayout.CardMarginAboveAndBelow - 0.01f, "the foot runs under the system bar");
    }

    [Theory]
    [MemberData(nameof(ScreensAndSwitches))]
    public void Every_row_a_finger_presses_is_at_least_44_points_high_on_the_screen(int width, int height, int topInset, int bottomInset, int switches)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        SettingsLayout card = SettingsLayout.For(switches);
        float scale = card.PlaceOn(page).Scale;

        Assert.All(card.Rows, row => Assert.True(row.Height * scale >= 44f, $"the {row.Kind} row is {row.Height * scale} points high"));
    }

    [Theory]
    [MemberData(nameof(ScreensAndSwitches))]
    public void The_privacy_and_the_support_card_stand_in_front_of_the_settings_card_inside_the_usable_screen(
        int width, int height, int topInset, int bottomInset, int switches)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        SettingsLayout card = SettingsLayout.For(switches);
        CardPlace behind = card.PlaceOn(page);
        float footBehind = behind.Middle - behind.Scale * card.Height / 2f;

        foreach (float front in new[] { SettingsLayout.PrivacyBodyHeight, SettingsLayout.SupportBodyHeight })
        {
            // The museum's stage stands a card in front on the foot of the one behind, a little over it, at the same scale.
            float top = footBehind + behind.Scale * (SettingsLayout.InFrontOverTheFoot + front + SettingsLayout.RidesAbove);
            Assert.True(top <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.01f, $"a card {front} high in front runs to {top}, over {page.View.UsableTop}");
        }
    }

    [Fact]
    public void With_the_designs_three_switches_the_card_is_as_high_as_the_canvas_draws_it_with_the_builds_line_under_its_foot()
    {
        SettingsLayout card = SettingsLayout.For(3);

        // SettingsTR: a body of 430 whose foot is one line; the build's line adds a band of 17.
        Assert.Equal(430f + SettingsLayout.BuildLineBand, card.BodyHeight, precision: 3);
        Assert.Equal(37f + 3 * 52f + 2 * 1.5f + 17f / 2f, card.DividerMiddle!.Value, precision: 3);
        Assert.Equal(213f + 26f, card.RowOf(SettingsRowKind.Language).Middle, precision: 3);
    }

    [Fact]
    public void A_card_without_switches_has_no_rule_under_them_and_is_shorter_by_their_rows()
    {
        SettingsLayout none = SettingsLayout.For(0);
        SettingsLayout three = SettingsLayout.For(3);

        Assert.Null(none.DividerMiddle);
        Assert.Equal(new[] { SettingsRowKind.Language, SettingsRowKind.Privacy, SettingsRowKind.Support }, none.Rows.Select(row => row.Kind));
        Assert.Equal(SettingsLayout.RowsDown, none.Rows[0].Down, precision: 3);
        Assert.Equal(three.BodyHeight - 3 * SettingsLayout.RowHeight - 2 * SettingsLayout.Hairline - SettingsLayout.DividerBand, none.BodyHeight, precision: 3);
    }

    [Theory]
    [MemberData(nameof(Switches))]
    public void Rows_follow_one_another_with_a_hairline_between_two_of_a_group_and_one_under_the_last(int switches)
    {
        SettingsLayout card = SettingsLayout.For(switches);

        Assert.Equal(switches + 3, card.Rows.Count);
        Assert.Equal(Enumerable.Range(0, switches), card.Rows.Take(switches).Select(row => row.SwitchIndex));
        Assert.Equal(Math.Max(0, switches - 1) + 3, card.Hairlines.Count);
        for (int index = 1; index < card.Rows.Count; index++)
        {
            Assert.True(card.Rows[index].Down >= card.Rows[index - 1].Bottom, $"row {index} begins inside the one above it");
        }

        Assert.True(card.FootMiddle - SettingsLayout.FootBand / 2f > card.Rows[^1].Bottom, "the foot begins inside the last row");
        Assert.True(card.BuildLineMiddle + SettingsLayout.BuildLineBand / 2f < card.BodyHeight, "the build's line runs over the card's lower edge");
    }

    [Fact]
    public void A_row_keeps_its_disc_its_name_and_its_switch_or_chevron_inside_the_card()
    {
        float half = SettingsLayout.BodyWidth / 2f;

        Assert.True(SettingsLayout.DiscMiddle - SettingsLayout.DiscSize / 2f > -half);
        Assert.True(SettingsLayout.SwitchMiddle + SettingsLayout.SwitchWidth / 2f < half);
        Assert.True(SettingsLayout.ChevronMiddle + SettingsLayout.ChevronSize / 2f < half);
        Assert.True(SettingsLayout.ValueRight(hasAChevron: true) < SettingsLayout.ChevronMiddle - SettingsLayout.ChevronSize / 2f);
    }

    [Fact]
    public void A_card_has_no_fewer_switches_than_none()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettingsLayout.For(-1));
    }
}
