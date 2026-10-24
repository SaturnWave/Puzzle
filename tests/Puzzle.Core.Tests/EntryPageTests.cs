using System.IO;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// The entry page the game opens on (EntryTR) in what can be said of it before anything is drawn: the start-up
/// behind it, done one step a frame (<c>StartUpWork</c>); its bar, which follows that work and is never seen to
/// flash (<c>LoadingBar</c>); and where its parts lie on the screens the game is made for (<c>EntryLayout</c>).
/// They are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class EntryPageTests
{
    /// <summary>
    /// The Pixel Fold's two screens bare and with a cutout and system bars as large as a phone's (a status bar of
    /// about 44 points and a gesture bar of about 21), the tall phone, the tight 9:16 one and a small one; and the
    /// three iPads upright, with the status bar and the home indicator (24 and 20 of their points).
    /// </summary>
    public static TheoryData<int, int, int, int> Screens() => new TheoryData<int, int, int, int>
    {
        { 1080, 2424, 0, 0 },
        { 2076, 2152, 0, 0 },
        { 1080, 2424, 132, 63 },
        { 2076, 2152, 110, 53 },
        { 1080, 2340, 0, 0 },
        { 1080, 1920, 90, 48 },
        { 720, 1280, 0, 0 },
        { 2048, 2732, 48, 40 },
        { 1640, 2360, 48, 40 },
        { 1488, 2266, 48, 40 },
        { 2732, 2048, 0, 40 },
        { 2360, 1640, 0, 40 },
        { 2152, 2076, 0, 0 },
        { 2560, 1600, 0, 0 },
    };

    [Fact]
    public void The_start_up_does_its_steps_in_their_order_one_at_a_time_and_counts_them_by_their_weights()
    {
        var done = new List<string>();
        var work = new StartUpWork(new[]
        {
            new StartUpStep("levels", 3f, () => done.Add("levels")),
            new StartUpStep("museum", 1f, () => done.Add("museum")),
            new StartUpStep("hall", 4f, () => done.Add("hall")),
        });

        Assert.Equal(0f, work.Done);
        Assert.Equal("levels", work.DoTheNext().Name);
        Assert.Equal(new[] { "levels" }, done);
        Assert.Equal(3f / 8f, work.Done, 5);
        work.DoTheNext();
        Assert.False(work.IsDone);
        Assert.Equal(4f / 8f, work.Done, 5);
        work.DoTheNext();

        Assert.Equal(new[] { "levels", "museum", "hall" }, done);
        Assert.True(work.IsDone);
        Assert.Equal(1f, work.Done);
        Assert.Throws<InvalidOperationException>(() => work.DoTheNext());
    }

    [Fact]
    public void A_step_that_fails_stops_the_start_up_there_for_good_and_is_not_counted_as_done()
    {
        int tries = 0;
        var work = new StartUpWork(new[]
        {
            new StartUpStep("levels", 1f, () => { }),
            new StartUpStep("museum", 1f, () =>
            {
                tries++;
                throw new IOException("the catalogue cannot be read");
            }),
            new StartUpStep("hall", 1f, () => { }),
        });

        work.DoTheNext();
        Assert.Throws<IOException>(() => work.DoTheNext());

        Assert.True(work.HasFailed);
        Assert.False(work.IsDone);
        Assert.Equal(1f / 3f, work.Done, 5);
        Assert.Throws<InvalidOperationException>(() => work.DoTheNext());
        Assert.Equal(1, tries);
    }

    [Fact]
    public void A_start_up_has_steps_that_weigh_something()
    {
        Assert.Throws<ArgumentException>(() => new StartUpWork(Array.Empty<StartUpStep>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StartUpStep("nothing", 0f, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StartUpStep("less", -1f, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StartUpStep("no number", float.NaN, () => { }));
    }

    [Fact]
    public void Work_done_at_once_still_fills_the_bar_in_no_less_than_its_shortest_time()
    {
        var bar = new LoadingBar();
        float elapsed = 0f;
        const float frame = 1f / 60f;
        while (!bar.IsFull)
        {
            bar.Advance(frame, workDone: 1f);
            elapsed += frame;
            Assert.True(elapsed < 10f, "the bar never filled");
        }

        Assert.InRange(elapsed, LoadingBar.ShortestFillSeconds - frame, LoadingBar.ShortestFillSeconds + 2 * frame);
        Assert.Equal(100, bar.Percent);
    }

    [Fact]
    public void The_bar_never_runs_ahead_of_the_work_never_goes_back_and_is_full_only_when_the_work_is_done()
    {
        var bar = new LoadingBar();

        bar.Advance(5f, workDone: 0.45f);
        Assert.Equal(0.45f, bar.Shown, 5);
        Assert.Equal(45, bar.Percent);

        bar.Advance(5f, workDone: 0.4f);
        Assert.Equal(0.45f, bar.Shown, 5);

        bar.Advance(5f, workDone: 0.2f);
        Assert.Equal(0.45f, bar.Shown, 5);

        bar.Advance(5f, workDone: 0.999f);
        Assert.False(bar.IsFull);
        Assert.Equal(99, bar.Percent);

        bar.Advance(5f, workDone: 3f);
        Assert.True(bar.IsFull);
        Assert.Equal(1f, bar.Shown);
    }

    [Fact]
    public void A_long_step_moves_the_bar_by_no_more_than_its_share_of_time()
    {
        var bar = new LoadingBar();

        // A frame the game holds for half a second reaches the bar as the root's longest step, a twentieth of one.
        bar.Advance(1f / 20f, workDone: 0.8f);

        Assert.Equal(1f / 20f / LoadingBar.ShortestFillSeconds, bar.Shown, 5);
    }

    [Fact]
    public void The_bar_takes_no_step_back_in_time_and_no_work_that_is_no_number()
    {
        var bar = new LoadingBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Advance(-0.1f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Advance(float.NaN, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Advance(0.1f, float.NaN));
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void Every_part_of_the_page_lies_inside_the_usable_screen_from_the_palette_down_to_the_footer(int width, int height, int topInset, int bottomInset)
    {
        EntryLayout page = EntryLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        ScreenView view = page.View;
        float scale = page.Scale;
        float titleCapitals = 0.75f * EntryLayout.TitleSize * scale;

        Assert.True(page.Palette.Top <= view.UsableTop, $"the palette's top {page.Palette.Top} is over the usable top {view.UsableTop}");
        Assert.True(page.Palette.Bottom > page.TitleMiddle + titleCapitals / 2f, "the title runs into the palette");
        Assert.True(page.TitleMiddle > page.SubtitleMiddle && page.SubtitleMiddle > page.BadgeMiddle, "the lines under the palette are out of their order");
        Assert.True(page.BadgeMiddle - EntryLayout.BadgeHeight * scale / 2f > page.BarMiddle + EntryLayout.BarHeight * scale / 2f, "the bar runs into the badge");
        Assert.True(page.BarMiddle > page.TapMiddle && page.TapMiddle > page.LoadingLineMiddle, "the line to touch does not stand where the bar and its line were");
        Assert.True(page.LoadingLineMiddle - EntryLayout.LoadingLineSize * scale > page.FooterMiddle + EntryLayout.FooterSize * scale, "the bar's line runs into the footer");
        Assert.True(page.FooterMiddle - EntryLayout.FooterSize * scale / 2f >= view.UsableBottom, "the footer lies under the usable foot");
        Assert.True(page.FooterLeft > view.Left && page.FooterRight < view.Right);
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_palette_and_its_gems_stay_on_the_screen_and_the_gems_float_clear_of_the_title(int width, int height, int topInset, int bottomInset)
    {
        EntryLayout page = EntryLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        float titleTop = page.TitleMiddle + 0.75f * EntryLayout.TitleSize * page.Scale / 2f;

        Assert.Equal(4, page.Gems.Count);
        Assert.True(page.Palette.Left > page.View.Left && page.Palette.Right < page.View.Right);
        Assert.All(page.Gems, gem =>
        {
            Assert.True(gem.Left > page.View.Left && gem.Right < page.View.Right, $"a gem at {gem} leaves the screen");
            Assert.True(gem.Bottom > titleTop, $"a gem at {gem} floats into the title");
            Assert.True(gem.Top < page.View.UsableTop, $"a gem at {gem} floats under the cutout");
        });

        // The picture holds the palette where the logo's picture has it.
        Assert.True(page.Picture.Left < page.Palette.Left && page.Picture.Right > page.Palette.Right);
        Assert.True(page.Picture.Bottom < page.Palette.Bottom && page.Picture.Top > page.Palette.Top);
        Assert.Equal(page.Picture.Width, page.Picture.Height, 3);
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_page_is_drawn_no_larger_than_the_canvas_and_no_wider_than_the_screen(int width, int height, int topInset, int bottomInset)
    {
        EntryLayout page = EntryLayout.Of(new ScreenShape(width, height, topInset, bottomInset));

        Assert.InRange(page.Scale, 0.7f, EntryLayout.LargestScale);
        Assert.True(EntryLayout.CanvasWidth * page.Scale <= page.View.Width + 0.01f);
        Assert.True(page.LineRoom < page.View.Width);
    }

    [Fact]
    public void The_page_says_the_canvas_words_in_Turkish_and_writes_the_share_as_each_language_writes_one()
    {
        using (TestWords.Speak(Language.Turkish))
        {
            Assert.Equal("Müze Yapbozu", EntryWords.Title);
            Assert.Equal("ÇALIŞMA ADI", EntryWords.WorkingTitle);
            Assert.Equal("Tabloları onar, müzeyi yeniden aç", EntryWords.Subtitle);
            Assert.Equal("Yükleniyor… %40", EntryWords.Loading(40));
            Assert.Equal("Başlamak için dokun", EntryWords.TapToStart);
            Assert.Equal("SaturnWave", EntryWords.Studio);
            Assert.Equal("Sürüm 1.0 · test sürümü", EntryWords.Version("1.0"));
        }

        using (TestWords.Speak(Language.English))
        {
            Assert.Equal("Loading… 100%", EntryWords.Loading(100));
        }

        using (TestWords.Speak(Language.French))
        {
            Assert.Equal("Chargement… 0 %", EntryWords.Loading(0));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => EntryWords.Loading(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => EntryWords.Loading(-1));
    }

    [Fact]
    public void The_studios_name_is_the_same_in_every_language()
    {
        foreach (Language language in Languages.All)
        {
            using (TestWords.Speak(language))
            {
                Assert.Equal("SaturnWave", EntryWords.Studio);
            }
        }
    }

    [Fact]
    public void The_narrow_cover_screen_draws_the_canvas_at_its_width_and_the_wide_inner_screen_at_its_own_size()
    {
        EntryLayout cover = EntryLayout.Of(new ScreenShape(1080, 2424, 132, 63));
        EntryLayout inner = EntryLayout.Of(new ScreenShape(2076, 2152, 110, 53));

        Assert.Equal(ScreenView.DesignWidth / EntryLayout.CanvasWidth, cover.Scale, 3);
        Assert.Equal(1f, inner.Scale, 3);

        // A palette as wide as the canvas' share of the narrow screen, in more than 200 points either way.
        Assert.InRange(cover.Palette.Width, 200f, 236f);
        Assert.Equal(EntryLayout.PaletteWidth, inner.Palette.Width, 3);
    }
}
