using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Museum;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the Unity game puts the museum's pages on a screen: the hall with its painting, and the map with its floor
/// plan, between the plaque at the top and the navigation bar at the foot. The layouts are the Unity project's
/// source files, compiled into these tests. The screens are the ones the board's layout is tested on: tall phones,
/// the 9:16 phone of the design, the owner's foldable opened, each also with a cutout and a system bar.
/// </summary>
public class MuseumLayoutTests
{
    /// <summary>The proportions of the first room's paintings as the game carries them, and of a blank canvas: width over height.</summary>
    private static readonly float[] PictureAspects = { 1.69f, 1.25f, 0.81f, 4f / 3f, 2.2f, 0.6f };

    private static readonly (int Width, int Height, int Top, int Bottom)[] Screens =
    {
        (1080, 2424, 0, 0), (1080, 2424, 96, 48), (1080, 2340, 0, 0), (1080, 2340, 110, 60), (1080, 1920, 0, 0), (1080, 1920, 90, 48),
        (720, 1280, 0, 0), (1200, 1920, 0, 0), (2076, 2152, 0, 0), (2076, 2152, 80, 60), (1600, 2560, 0, 0),
    };

    public static TheoryData<int, int, int, int> EveryScreen()
    {
        var cases = new TheoryData<int, int, int, int>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            cases.Add(width, height, top, bottom);
        }

        return cases;
    }

    [Fact]
    public void A_box_knows_its_edges_and_what_lies_in_it()
    {
        Box box = Box.Around(middleX: 10f, middleY: -5f, width: 40f, height: 20f);

        Assert.Equal((-10f, 30f, -15f, 5f), (box.Left, box.Right, box.Bottom, box.Top));
        Assert.True(box.Contains(-10f, 5f));
        Assert.False(box.Contains(31f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box(0f, 0f, -1f, 1f));
    }

    [Theory]
    [MemberData(nameof(EveryScreen))]
    public void A_page_keeps_its_wallet_row_and_plaque_under_the_cornice_and_its_bar_over_the_screens_foot(int width, int height, int top, int bottom)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, top, bottom));

        Assert.True(page.CorniceBottom < page.View.UsableTop, "the cornice does not show under the cutout");
        Assert.True(page.WalletRow.Top < page.CorniceBottom, "the wallet row runs into the cornice");
        Assert.True(page.Plaque.Top < page.WalletRow.Bottom, "the plaque runs into the wallet row");
        Assert.True(page.WalletRow.Left >= page.Plaque.Left - 0.01f && page.WalletRow.Right <= page.Plaque.Right + 0.01f, "the wallet row is wider than the plaque");
        Assert.Equal(page.Plaque.MiddleX, page.WalletRow.MiddleX, precision: 3);
        Assert.True(page.RailTop < page.Plaque.Bottom, "the rail runs into the plaque");
        Assert.True(page.Plaque.Left >= page.View.Left + PageLayout.PlaqueSideMargin - 0.01f, "the plaque is cut off at the side");
        Assert.True(page.BarTop >= page.View.UsableBottom + PageLayout.BarHeight * PageLayout.TightBarScale - 0.01f, "the bar is hidden by the system bar");
        Assert.True(page.BarReach > page.BarTop);
        Assert.InRange(page.Roominess, 0f, 1f);
    }

    [Theory]
    [InlineData(1080, 2340, 0, 0, 1f)]
    [InlineData(1080, 2424, 96, 48, 1f)]
    [InlineData(1080, 1920, 0, 0, PageLayout.WalletRowTightScale)]
    [InlineData(2076, 2152, 0, 0, PageLayout.WalletRowTightScale)]
    public void The_wallet_row_is_as_wide_as_the_plaque_on_a_roomy_screen_and_drawn_smaller_on_a_tight_one(int width, int height, int top, int bottom, float shareOfThePlaque)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, top, bottom));

        Assert.Equal(PageLayout.WidestPlaque, page.Plaque.Width, precision: 3);
        Assert.Equal(page.Plaque.Width * shareOfThePlaque, page.WalletRow.Width, precision: 2);
        Assert.Equal(page.WalletRow.Width / WalletRowLayout.Width, page.WalletRowScale, precision: 4);
        Assert.Equal(WalletRowLayout.Height * page.WalletRowScale, page.WalletRow.Height, precision: 3);

        // The row hangs as close under the cornice on every screen, a cutout or not: what is over the cornice is not the page's.
        Assert.InRange(page.CorniceBottom - page.WalletRow.Top, 5f - 0.01f, 12f + 0.01f);
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(1080, 1920)]
    public void A_page_without_the_wallet_row_hangs_its_plaque_right_under_the_cornice_as_the_jigsaw_has_it(int width, int height)
    {
        var screen = new ScreenShape(width, height);
        PageLayout bare = PageLayout.Of(screen, PageTop.PlaqueUnderTheCornice);
        PageLayout withTheRow = PageLayout.Of(screen);

        Assert.Equal((0f, 0f), (bare.WalletRow.Height, bare.WalletRowScale));
        Assert.InRange(bare.CorniceBottom - bare.Plaque.Top, 9f - 0.01f, 15f + 0.01f);
        Assert.True(withTheRow.Plaque.Top < bare.Plaque.Top - withTheRow.WalletRow.Height, "the row's height did not come out of the page under it");
        Assert.Equal(bare.Plaque.MiddleY, JigsawLayout.Of(screen, 1.69f, JigsawSize.Ten, PaintingOrientation.Landscape).Plaque.MiddleY, precision: 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(40.8f)]
    [InlineData(82f)]
    [InlineData(400f)]
    public void Nothing_of_the_wallet_row_runs_into_anything_else_of_it_whatever_the_amount(float amountWidth)
    {
        WalletRowLayout row = WalletRowLayout.ForAnAmount(amountWidth);

        Assert.InRange(row.CoinsPill, WalletRowLayout.NarrowestCoinsPill, WalletRowLayout.WidestCoinsPill);
        Assert.True(WalletRowLayout.Coin.Left >= 0f && WalletRowLayout.Gear.Right <= WalletRowLayout.Width + 2f, "the row reaches out of its place over the plaque");
        Assert.True(row.CoinsPillBox.Left > WalletRowLayout.Coin.Left && row.CoinsPillBox.Left < WalletRowLayout.Coin.Right, "the coin does not stand on its pill's end");
        Assert.True(row.CoinsPlus.Right < row.Heart.Left, "the coins' plus runs into the heart");
        Assert.True(row.LivesPlus.Right + WalletRowLayout.LeastGapToGear <= WalletRowLayout.Gear.Left + 0.01f, "the lives run into the settings' button");
        Assert.True(row.AmountRoom >= WalletRowLayout.NarrowestCoinsPill - WalletRowLayout.WordsInFromLeft - WalletRowLayout.WordsInFromRight - 0.01f);
        Assert.True(WalletRowLayout.WaitRoom > 0f && row.LivesButton.Contains(row.WaitMiddle, 0f));
        Assert.True(WalletRowLayout.Coin.Height <= WalletRowLayout.Height && row.CoinsPillBox.Height <= WalletRowLayout.Height);
    }

    [Fact]
    public void The_wallet_row_stands_where_the_canvas_draws_it_for_its_own_amount()
    {
        // HomeTR: the coin 14..60, its pill 36..136, its plus 122..148 (middle 135), the heart 164..210, its pill 186..278, the gear's middle 356; the row begins at 14.
        WalletRowLayout row = WalletRowLayout.ForAnAmount(40.8f);

        Assert.Equal(WalletRowLayout.NarrowestCoinsPill, row.CoinsPill);
        Assert.Equal((0f, 46f), (WalletRowLayout.Coin.Left, WalletRowLayout.Coin.Right));
        Assert.Equal((22f, 122f), (row.CoinsPillBox.Left, row.CoinsPillBox.Right));
        Assert.Equal(121f, row.CoinsPlus.MiddleX);
        Assert.Equal((150f, 196f), (row.Heart.Left, row.Heart.Right));
        Assert.Equal((172f, 264f), (row.LivesPillBox.Left, row.LivesPillBox.Right));
        Assert.Equal(342f, WalletRowLayout.Gear.MiddleX);
        Assert.Throws<ArgumentOutOfRangeException>(() => WalletRowLayout.ForAnAmount(-1f));
    }

    [Fact]
    public void A_tall_phone_is_roomy_and_a_wide_screen_is_as_tight_as_the_design_phone()
    {
        Assert.Equal(1f, PageLayout.Of(new ScreenShape(1080, 2424)).Roominess);
        Assert.Equal(0f, PageLayout.Of(new ScreenShape(1080, 1920)).Roominess);
        Assert.Equal(0f, PageLayout.Of(new ScreenShape(2076, 2152)).Roominess);
        Assert.Equal(PageLayout.TightBarScale, PageLayout.Of(new ScreenShape(2076, 2152)).BarScale);
    }

    [Fact]
    public void The_bars_places_stand_as_a_centred_row_inside_the_screen()
    {
        PageLayout page = PageLayout.Of(new ScreenShape(1080, 1920));

        Assert.Equal(new[] { -52f, 52f }, new[] { page.TabMiddle(0, 2), page.TabMiddle(1, 2) });
        Assert.Equal(new[] { -104f, 0f, 104f }, new[] { page.TabMiddle(0, 3), page.TabMiddle(1, 3), page.TabMiddle(2, 3) });
        Assert.Equal(72f, page.TabWidth(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.TabMiddle(2, 2));
    }

    [Fact]
    public void The_bar_has_four_places_the_shop_leftmost_and_each_place_as_wide_as_a_quarter_of_a_phone()
    {
        PageLayout page = PageLayout.Of(new ScreenShape(1080, 1920));
        int count = Destinations.OnTheBar.Count;

        Assert.Equal(new[] { Destination.Shop, Destination.Collection, Destination.Home, Destination.Map }, Destinations.OnTheBar);
        Assert.Equal(new[] { -135f, -45f, 45f, 135f }, Destinations.OnTheBar.Select(place => page.TabMiddle(Destinations.PlaceOf(place), count)));
        Assert.Equal(90f, page.TabWidth(count));
        Assert.Equal(PageLayout.WidestTab, PageLayout.Of(new ScreenShape(2076, 2152)).TabWidth(count));
        Assert.Equal(("Mağaza", GalleryArt.IconShop), (Destinations.NameOf(Destination.Shop), Destinations.MarkOf(Destination.Shop)));
    }

    public static TheoryData<int, int, int, int, float> EveryScreenAndPicture()
    {
        var cases = new TheoryData<int, int, int, int, float>();
        foreach ((int width, int height, int top, int bottom) in Screens)
        {
            foreach (float aspect in PictureAspects)
            {
                cases.Add(width, height, top, bottom, aspect);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void The_hall_stacks_the_painting_the_bench_and_the_button_without_any_two_meeting(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        PageLayout page = hall.Page;
        float onePixel = 1f / page.View.PixelsPerPoint;

        Assert.True(hall.Frame.Top < page.RailBottom, "the frame runs into the picture rail");
        Assert.True(hall.Frame.Left >= page.View.Left && hall.Frame.Right <= page.View.Right, "the frame is cut off at a side");
        float plateFoot = hall.PlateMiddle - HallLayout.PlateHeight / 2;
        float benchTop = hall.FloorLine + HallLayout.BenchAboveFloorLine * hall.BenchScale;
        Assert.True(plateFoot > benchTop, "the plate under the painting reaches the brushes on the bench");
        Assert.True(hall.BenchFeet > hall.Button.Top, "the bench stands behind the play button");
        Assert.True(hall.Button.Bottom - HallLayout.ButtonLip * hall.ButtonScale > page.BarReach, "the play button reaches the bar");
        Assert.True(hall.Button.Left >= page.View.Left && hall.Button.Right <= page.View.Right, "the play button is cut off at a side");

        // The picture keeps its proportions, to the pixel it is rounded to.
        Assert.InRange(hall.Picture.Width / hall.Picture.Height, aspect * (1 - 2 * onePixel / hall.Picture.Height) - 0.01f, aspect * (1 + 2 * onePixel / hall.Picture.Height) + 0.01f);
        Assert.True(hall.Picture.Width <= HallLayout.WidestPicture + onePixel);
        Assert.True(hall.Picture.Height >= HallLayout.SmallestPicture, $"the picture is only {hall.Picture.Height} points high");
        Assert.InRange(hall.BenchScale, HallLayout.SmallestBench, 1f);

        // The bench before the painting: a picture under the height the bench gives way to has the smallest bench over it.
        bool isAsWideAsTheWallAllows = hall.Picture.Width >= Math.Min(HallLayout.WidestPicture, page.View.Width - 2 * (HallLayout.FrameWidth + 10f)) - 2 * onePixel;
        if (hall.Picture.Height < HallLayout.PictureHeightTheBenchGivesWayTo - 2 * onePixel && !isAsWideAsTheWallAllows)
        {
            Assert.Equal(HallLayout.SmallestBench, hall.BenchScale, precision: 3);
        }
    }

    [Theory]
    [MemberData(nameof(EveryScreenAndPicture))]
    public void The_gift_frame_hangs_on_the_wall_beside_the_bench_between_the_skirting_and_the_plate(int width, int height, int top, int bottom, float aspect)
    {
        HallLayout hall = HallLayout.Of(new ScreenShape(width, height, top, bottom), aspect);
        Box gift = hall.GiftButton;

        // The workbench's table is 200 of its drawing's points wide: the frame hangs at its right, not behind it.
        float tableRight = 100f * hall.BenchScale;
        Assert.True(gift.Bottom >= hall.FloorLine + HallLayout.SkirtingHeight - 0.01f, "the gift frame stands on the skirting board");
        Assert.True(gift.Top <= hall.Hanging.PlateBottom - 0.01f, "the gift frame reaches the painting's plate");
        Assert.True(gift.Left >= tableRight, $"the gift frame hangs behind the workbench ({gift.Left:0.#} against {tableRight:0.#})");
        Assert.True(gift.Right <= hall.Page.View.Right, "the gift frame is cut off at the side");
        Assert.True(gift.Height >= 44f, $"the gift frame is only {gift.Height:0.#} points high for a finger");
        Assert.InRange(hall.GiftScale, 0.5f, hall.Page.WalletRowScale);
    }

    [Fact]
    public void A_screen_that_loses_height_draws_the_bench_smaller_before_the_painting_gives_up_its_height()
    {
        HallLayout whole = HallLayout.Of(new ScreenShape(2076, 2152), 1.69f);
        HallLayout withBars = HallLayout.Of(new ScreenShape(2076, 2152, topInsetPixels: 80, bottomInsetPixels: 60), 1.69f);

        // The wallet row costs the open foldable its usual bench; the painting keeps the height the bench gives way to.
        Assert.True(whole.BenchScale < 0.7f && whole.BenchScale > HallLayout.SmallestBench);
        Assert.True(whole.Picture.Height >= HallLayout.PictureHeightTheBenchGivesWayTo - 1f, $"the picture is only {whole.Picture.Height} points high");

        // A cutout and a system bar take more: the bench is at its smallest before the painting goes lower.
        Assert.Equal(HallLayout.SmallestBench, withBars.BenchScale, precision: 3);
        Assert.InRange(withBars.Picture.Height, HallLayout.SmallestPicture, whole.Picture.Height);
    }

    [Fact]
    public void The_hall_has_wall_lamps_where_the_wall_beside_the_painting_is_wide_enough()
    {
        Assert.True(HallLayout.Of(new ScreenShape(2076, 2152), 1.69f).HasLamps);
        Assert.False(HallLayout.Of(new ScreenShape(1080, 2424), 1.69f).HasLamps);
        Assert.False(HallLayout.Of(new ScreenShape(1080, 1920), 1.69f).HasLamps);

        // An upright painting is narrow: on a phone it leaves the wall a lamp needs.
        Assert.True(HallLayout.Of(new ScreenShape(1080, 1920), 0.81f).HasLamps);
        Assert.Throws<ArgumentOutOfRangeException>(() => HallLayout.Of(new ScreenShape(1080, 1920), 0f));
    }
}
