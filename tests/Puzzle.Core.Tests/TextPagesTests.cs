using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// A long text set on the pages of a card (the privacy card of v1): its paragraphs in their order, as many to a page
/// as the room holds, none broken over two pages. <c>TextPages</c> is the Unity project's source file, compiled into
/// these tests; the card itself is pictured by the Editor's screen shots (-screens privacy).
/// </summary>
public class TextPagesTests
{
    [Fact]
    public void A_text_that_fits_the_room_is_one_page()
    {
        Assert.Equal(new[] { new TextPage(0, 3) }, TextPages.Of(new[] { 40f, 50f, 60f }, gap: 7f, room: 164f));
    }

    [Fact]
    public void A_paragraph_that_would_run_over_the_room_begins_the_next_page()
    {
        IReadOnlyList<TextPage> pages = TextPages.Of(new[] { 60f, 70f, 80f, 30f, 90f }, gap: 7f, room: 140f);

        Assert.Equal(new[] { new TextPage(0, 2), new TextPage(2, 2), new TextPage(4, 1) }, pages);
        Assert.Equal(137f, TextPages.HeightOf(pages[0], new[] { 60f, 70f, 80f, 30f, 90f }, gap: 7f));
    }

    [Fact]
    public void A_paragraph_higher_than_the_room_has_a_page_of_its_own_and_every_paragraph_is_on_one_page()
    {
        float[] heights = { 20f, 300f, 20f, 20f };

        IReadOnlyList<TextPage> pages = TextPages.Of(heights, gap: 7f, room: 100f);

        Assert.Equal(new[] { new TextPage(0, 1), new TextPage(1, 1), new TextPage(2, 2) }, pages);
        Assert.All(Enumerable.Range(0, heights.Length), paragraph => Assert.Single(pages, page => page.Shows(paragraph)));
    }

    [Fact]
    public void No_paragraph_is_no_text_to_set()
    {
        Assert.Throws<ArgumentException>(() => TextPages.Of(Array.Empty<float>(), gap: 7f, room: 100f));
    }
}
