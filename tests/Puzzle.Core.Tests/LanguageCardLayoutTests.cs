using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// The language card, in the design canvas' points: a row for each language it offers, in one column up to seven and
/// in two beyond, every row as high as a finger needs, everything inside the card's rows, and the card as wide as
/// the settings card it hangs in front of. <c>LanguageCardLayout</c> is the Unity project's source file, compiled
/// into these tests.
/// </summary>
public class LanguageCardLayoutTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(13, 2)]
    public void Up_to_seven_languages_stand_in_one_column_and_more_in_two_filled_down_the_left_first(int languages, int columns)
    {
        LanguageCardLayout layout = LanguageCardLayout.For(languages);

        Assert.Equal(languages, layout.Rows.Count);
        Assert.Equal(columns, layout.Rows.Select(row => row.Column).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, languages).Select(index => index < (languages + columns - 1) / columns ? 0 : 1), layout.Rows.Select(row => row.Column));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public void Every_row_lies_inside_the_cards_rows_and_below_the_one_above_it_in_its_column(int languages)
    {
        LanguageCardLayout layout = LanguageCardLayout.For(languages);

        Assert.All(layout.Rows, row =>
        {
            Assert.InRange(row.Left, SettingsLayout.RowsLeft, SettingsLayout.RowsRight);
            Assert.InRange(row.Right, row.Left + 100f, SettingsLayout.RowsRight);
            Assert.True(row.Height >= 44f);
            Assert.True(row.Bottom < layout.ButtonMiddle - LanguageCardLayout.ButtonUnderTheRows / 2f);
        });
        foreach (IGrouping<int, LanguageRowPlace> column in layout.Rows.GroupBy(row => row.Column))
        {
            LanguageRowPlace[] rows = column.ToArray();
            Assert.All(rows.Zip(rows.Skip(1)), pair => Assert.Equal(pair.First.Bottom + SettingsLayout.Hairline, pair.Second.Down, 3));
        }
    }

    [Fact]
    public void A_hairline_runs_under_every_row_but_the_last_of_its_column()
    {
        Assert.Equal(2, LanguageCardLayout.For(3).HairlinesUnder.Count);
        Assert.Equal(11, LanguageCardLayout.For(13).HairlinesUnder.Count);
        Assert.Empty(LanguageCardLayout.For(1).HairlinesUnder);
    }

    [Fact]
    public void The_card_of_the_three_languages_of_this_build_is_no_higher_than_the_support_card_and_as_wide_as_the_settings_card()
    {
        Assert.True(LanguageCardLayout.For(3).BodyHeight <= SettingsLayout.SupportBodyHeight);
        Assert.Equal(SettingsLayout.BodyWidth, LanguageCardLayout.BodyWidth);
    }

    /// <summary>The tall phone the canvas is drawn for, the tight 9:16 one, the open fold, and the two phones with a cutout and a system bar.</summary>
    public static TheoryData<int, int, int, int> Screens() => new()
    {
        { 1080, 2340, 0, 0 },
        { 1080, 1920, 0, 0 },
        { 2076, 2152, 0, 0 },
        { 1080, 2340, 110, 60 },
        { 1080, 1920, 90, 48 },
    };

    /// <summary>The three languages of this build; up to five stand in front of the settings card the same way.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void A_card_of_up_to_five_rows_stands_in_front_of_the_settings_card_inside_the_usable_screen_with_rows_a_finger_can_press(
        int width, int height, int topInset, int bottomInset)
    {
        foreach (int languages in new[] { 3, LanguageCardLayout.MostRowsInFront })
        {
            Assert.True(StandsInFront(new ScreenShape(width, height, topInset, bottomInset), languages, out string why), why);
        }
    }

    /// <summary>The thirteen languages of the first release, which the next step completes: their card hangs by itself in the page's place for a card.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void The_card_of_the_first_releases_thirteen_languages_hangs_by_itself_inside_the_usable_screen_with_rows_a_finger_can_press(
        int width, int height, int topInset, int bottomInset)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        LanguageCardLayout card = LanguageCardLayout.For(13);
        CardPlace place = card.PlaceOn(page);

        Assert.True(place.Middle + place.Scale * card.Height / 2f <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.01f, "the nameplate runs under the cutout");
        Assert.True(place.Middle - place.Scale * card.Height / 2f >= page.View.UsableBottom + PageLayout.CardMarginAboveAndBelow - 0.01f, "the foot runs under the system bar");
        Assert.All(card.Rows, row => Assert.True(row.Height * place.Scale >= 44f, $"a row is {row.Height * place.Scale} points high"));
    }

    private static bool StandsInFront(ScreenShape screen, int languages, out string why)
    {
        PageLayout page = PageLayout.Of(screen);
        SettingsLayout settings = SettingsLayout.For(switches: 2);
        CardPlace behind = settings.PlaceOn(page);
        float footBehind = behind.Middle - behind.Scale * settings.Height / 2f;
        LanguageCardLayout card = LanguageCardLayout.For(languages);

        // The museum's stage stands a card in front on the foot of the one behind, a little over it, at the same scale.
        float top = footBehind + behind.Scale * (SettingsLayout.InFrontOverTheFoot + card.Height);
        float rowsHigh = card.Rows.Min(row => row.Height) * behind.Scale;
        why = $"{languages} languages on {screen}: the card runs to {top} under a top of {page.View.UsableTop}, its rows {rowsHigh} points high";
        return top <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.01f && rowsHigh >= 44f;
    }

    [Fact]
    public void A_card_offers_the_language_the_game_speaks_at_least()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LanguageCardLayout.For(0));
    }
}
