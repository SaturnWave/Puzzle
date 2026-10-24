using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the Unity game puts the shop on a screen (<c>ShopLayout</c>, the Unity project's source file, compiled into
/// these tests): the sign under the wallet row, the three panels over the navigation bar, one column on a phone and
/// two on the opened foldable, nothing scrolled away and every button large enough for a finger.
/// </summary>
public class ShopLayoutTests
{
    /// <summary>The shipped settings' goods: three packs of coins and two sets.</summary>
    private const int Packs = 3;
    private const int Sets = 2;

    /// <summary>The screens the design names for the shop, and the tall phone of the other pictures.</summary>
    public static TheoryData<int, int> FourScreens() => new() { { 1080, 2340 }, { 1080, 1920 }, { 2076, 2152 }, { 1080, 2424 } };

    /// <summary>Every screen the museum's pages are tested on, with cutouts and system bars.</summary>
    public static TheoryData<int, int, int, int> EveryScreen() => new()
    {
        { 1080, 2424, 0, 0 }, { 1080, 2424, 96, 48 }, { 1080, 2340, 0, 0 }, { 1080, 2340, 110, 60 }, { 1080, 1920, 0, 0 }, { 1080, 1920, 90, 48 },
        { 720, 1280, 0, 0 }, { 1200, 1920, 0, 0 }, { 2076, 2152, 0, 0 }, { 2076, 2152, 80, 60 }, { 1600, 2560, 0, 0 },
        { 2048, 2732, 48, 40 }, { 1640, 2360, 48, 40 }, { 1488, 2266, 48, 40 },
    };

    [Theory]
    [MemberData(nameof(FourScreens))]
    public void On_the_four_screens_the_shop_keeps_at_least_the_columns_least_size(int width, int height)
    {
        ShopLayout shop = ShopLayout.Of(new ScreenShape(width, height), Packs, Sets);

        Assert.InRange(shop.Share, ShopLayout.SmallestShare, 1f);
    }

    [Theory]
    [MemberData(nameof(FourScreens))]
    public void A_phone_has_one_column_and_the_opened_foldable_two(int width, int height)
    {
        ShopLayout shop = ShopLayout.Of(new ScreenShape(width, height), Packs, Sets);

        Assert.Equal(width == 2076, shop.IsTwoColumns);
    }

    [Fact]
    public void A_tall_phone_draws_the_shop_at_its_full_size_and_the_design_phone_gives_the_gaps_up_first()
    {
        Assert.Equal(1f, ShopLayout.Of(new ScreenShape(1080, 2340), Packs, Sets).Share, precision: 4);
        Assert.Equal(1f, ShopLayout.Of(new ScreenShape(1080, 2424), Packs, Sets).Share, precision: 4);

        ShopLayout tight = ShopLayout.Of(new ScreenShape(1080, 1920), Packs, Sets);
        Assert.True(tight.Share < 1f, "the design phone has no room for the shop at its full size with its least gaps");
        Assert.True(tight.Sign.Bottom - tight.Panel(ShopPanel.Coins).Top < 5.01f, "the gaps did not give way before the column was drawn smaller");
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void Nothing_of_the_shop_overlaps_and_all_of_it_lies_between_the_wallet_row_and_the_bar(int width, int height, int top, int bottom)
    {
        ShopLayout shop = ShopLayout.Of(new ScreenShape(width, height, top, bottom), Packs, Sets);
        PageLayout page = shop.Page;
        Box[] parts = new[] { shop.Sign }.Concat(ShopLayout.Panels.Select(shop.Panel)).ToArray();

        Assert.True(shop.Pin.Top < page.WalletRow.Bottom, "the sign's pin runs into the wallet row");
        Assert.All(parts, part =>
        {
            Assert.True(part.Top < page.WalletRow.Bottom && part.Bottom > page.BarReach, $"{part} leaves the room between the wallet row and the bar");
            Assert.True(part.Left >= page.View.Left + PageLayout.PlaqueSideMargin - 0.01f && part.Right <= page.View.Right - PageLayout.PlaqueSideMargin + 0.01f, $"{part} is cut off at a side");
        });
        for (int one = 0; one < parts.Length; one++)
        {
            for (int other = one + 1; other < parts.Length; other++)
            {
                Assert.False(Overlap(parts[one], parts[other]), $"{parts[one]} and {parts[other]} overlap");
            }
        }
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void Every_button_is_held_at_least_38_points_high_inside_its_panel_and_apart_from_the_others(int width, int height, int top, int bottom)
    {
        ShopLayout shop = ShopLayout.Of(new ScreenShape(width, height, top, bottom), Packs, Sets);
        Box[] buttons = shop.Buttons.ToArray();
        Box[] panels = ShopLayout.Panels.Select(shop.Panel).ToArray();

        Assert.Equal(Packs + Sets + 1, buttons.Length);
        Assert.All(buttons, button =>
        {
            Assert.True(button.Height >= 38f, $"a button of {button.Height:0.#} points is too low for a finger");
            Assert.Contains(panels, panel => Inside(button, panel));
            Assert.True(button.Bottom > shop.Page.View.UsableBottom && button.Top < shop.Page.View.UsableTop, $"{button} is under a system bar");
        });
        for (int one = 0; one < buttons.Length; one++)
        {
            for (int other = one + 1; other < buttons.Length; other++)
            {
                Assert.False(Overlap(buttons[one], buttons[other]), $"{buttons[one]} and {buttons[other]} overlap");
            }
        }
    }

    [Fact]
    public void Packs_stand_as_a_centred_row_and_a_shelf_gives_each_disc_an_equal_column()
    {
        ShopLayout shop = ShopLayout.Of(new ScreenShape(1080, 2340), Packs, Sets);

        Assert.Equal(ShopLayout.PanelWidth / 2f, ShopLayout.PackMiddle(1, Packs), precision: 3);
        Assert.Equal(ShopLayout.PanelWidth - ShopLayout.PackMiddle(2, Packs), ShopLayout.PackMiddle(0, Packs), precision: 3);
        Assert.Equal(0f, shop.PackButton(1).MiddleX, precision: 3);
        Assert.Equal(ShopLayout.ShelfLeft + ShopLayout.ShelfWidth / 2f, ShopLayout.DiscMiddle(1, 3), precision: 3);
        Assert.Equal(ShopLayout.DiscMiddle(1, 4) - ShopLayout.DiscMiddle(0, 4), ShopLayout.DiscMiddle(3, 4) - ShopLayout.DiscMiddle(2, 4), precision: 3);
        Assert.Equal(204f, ShopLayout.BoostersPanelHeight(2));
    }

    [Fact]
    public void A_shop_without_goods_or_with_more_packs_than_a_row_holds_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopLayout.Of(new ScreenShape(1080, 2340), 0, Sets));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopLayout.Of(new ScreenShape(1080, 2340), 4, Sets));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShopLayout.Of(new ScreenShape(1080, 2340), Packs, 0));
    }

    private static bool Overlap(Box one, Box other) =>
        one.Left < other.Right - 0.01f && other.Left < one.Right - 0.01f && one.Bottom < other.Top - 0.01f && other.Bottom < one.Top - 0.01f;

    private static bool Inside(Box inner, Box outer) =>
        inner.Left >= outer.Left - 0.01f && inner.Right <= outer.Right + 0.01f && inner.Bottom >= outer.Bottom - 0.01f && inner.Top <= outer.Top + 0.01f;
}
