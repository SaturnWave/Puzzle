using Puzzle.Client.Canvases;

namespace Puzzle.Core.Tests;

/// <summary>
/// The shelf of the Unity game's loaded pictures: each picture is loaded once however many screens hold it, a held
/// picture is never unloaded, and of the pictures nothing holds only the ones let go of last stay loaded, so turning
/// through the collection's rooms keeps a bounded number in memory (A42). <c>PictureShelf</c> is the Unity project's
/// source file, compiled into these tests: it has no Unity dependency. The pictures here are named by numbers and
/// stand for themselves; the shelf's loader and unloader write down what they are asked.
/// </summary>
public class PictureShelfTests
{
    private sealed class Picture
    {
        public Picture(int name)
        {
            Name = name;
        }

        public int Name { get; }
    }

    /// <summary>A shelf whose loader finds a picture for every name but the ones it is told are missing, and which writes down every load and unload.</summary>
    private sealed class Pictures
    {
        public Pictures(int keptUnheld, params int[] missing)
        {
            Shelf = new PictureShelf<int, Picture>(
                keptUnheld,
                name =>
                {
                    Loads.Add(name);
                    return missing.Contains(name) ? null : new Picture(name);
                },
                picture => Unloads.Add(picture.Name));
        }

        public PictureShelf<int, Picture> Shelf { get; }

        public List<int> Loads { get; } = new List<int>();

        public List<int> Unloads { get; } = new List<int>();

        /// <summary>Holds the pictures of a room and lets go of them: what the collection does when a room is turned past.</summary>
        public void TurnPast(params int[] room)
        {
            foreach (PictureShelf<int, Picture>.PictureHold hold in room.Select(name => Shelf.Hold(name)).ToList())
            {
                hold.Dispose();
            }
        }
    }

    [Fact]
    public void A_picture_is_loaded_once_however_many_hold_it()
    {
        var pictures = new Pictures(keptUnheld: 0);

        PictureShelf<int, Picture>.PictureHold first = pictures.Shelf.Hold(7);
        PictureShelf<int, Picture>.PictureHold second = pictures.Shelf.Hold(7);

        Assert.Same(first.Picture, second.Picture);
        Assert.Equal(new[] { 7 }, pictures.Loads);
        Assert.Equal(1, pictures.Shelf.Loaded);
        Assert.Equal(1, pictures.Shelf.Held);
    }

    [Fact]
    public void A_picture_stays_loaded_while_any_holder_still_holds_it()
    {
        var pictures = new Pictures(keptUnheld: 0);
        PictureShelf<int, Picture>.PictureHold first = pictures.Shelf.Hold(7);
        PictureShelf<int, Picture>.PictureHold second = pictures.Shelf.Hold(7);

        first.Dispose();
        Assert.Empty(pictures.Unloads);

        second.Dispose();
        Assert.Equal(new[] { 7 }, pictures.Unloads);
        Assert.Equal(0, pictures.Shelf.Loaded);
    }

    [Fact]
    public void Letting_go_of_a_hold_twice_lets_go_once()
    {
        var pictures = new Pictures(keptUnheld: 0);
        PictureShelf<int, Picture>.PictureHold first = pictures.Shelf.Hold(7);
        PictureShelf<int, Picture>.PictureHold second = pictures.Shelf.Hold(7);

        first.Dispose();
        first.Dispose();

        Assert.Empty(pictures.Unloads);
        Assert.Equal(1, pictures.Shelf.Held);
        second.Dispose();
        Assert.Equal(new[] { 7 }, pictures.Unloads);
    }

    [Fact]
    public void Of_the_pictures_nothing_holds_the_ones_let_go_of_last_stay_and_the_oldest_is_unloaded_first()
    {
        var pictures = new Pictures(keptUnheld: 3);

        pictures.TurnPast(1, 2, 3);
        Assert.Empty(pictures.Unloads);
        Assert.Equal(3, pictures.Shelf.Loaded);

        pictures.TurnPast(4, 5);
        Assert.Equal(new[] { 1, 2 }, pictures.Unloads);
        Assert.Equal(3, pictures.Shelf.Loaded);
        Assert.Equal(0, pictures.Shelf.Held);
    }

    [Fact]
    public void A_kept_picture_held_again_is_not_loaded_again_and_counts_as_let_go_of_anew()
    {
        var pictures = new Pictures(keptUnheld: 2);
        pictures.TurnPast(1, 2);

        pictures.TurnPast(1);
        pictures.TurnPast(3);

        Assert.Equal(new[] { 1, 2, 3 }, pictures.Loads);
        Assert.Equal(new[] { 2 }, pictures.Unloads);
    }

    [Fact]
    public void A_held_picture_is_never_unloaded_however_many_others_are_let_go_of()
    {
        var pictures = new Pictures(keptUnheld: 1);
        PictureShelf<int, Picture>.PictureHold shown = pictures.Shelf.Hold(1);

        for (int name = 2; name <= 30; name++)
        {
            pictures.TurnPast(name);
        }

        pictures.Shelf.LetGoOfUnheld();
        Assert.DoesNotContain(1, pictures.Unloads);
        Assert.Equal(1, pictures.Shelf.Loaded);
        Assert.Same(shown.Picture, pictures.Shelf.Hold(1).Picture);
    }

    [Fact]
    public void Turning_through_a_hundred_pictures_room_by_room_keeps_no_more_than_the_room_shown_and_the_kept_ones_loaded()
    {
        const int kept = 10;
        var pictures = new Pictures(keptUnheld: kept);
        var most = 0;
        List<PictureShelf<int, Picture>.PictureHold> shown = new List<PictureShelf<int, Picture>.PictureHold>();
        for (int walk = 0; walk < 3; walk++)
        {
            for (int room = 0; room < 20; room++)
            {
                List<PictureShelf<int, Picture>.PictureHold> next = Enumerable.Range(room * 5, 5).Select(name => pictures.Shelf.Hold(name)).ToList();
                most = Math.Max(most, pictures.Shelf.Loaded);
                shown.ForEach(hold => hold.Dispose());
                shown = next;
            }
        }

        Assert.Equal(5 + 5 + kept, most);
        Assert.Equal(5 + kept, pictures.Shelf.Loaded);
        Assert.Equal(5, pictures.Shelf.Held);
    }

    [Fact]
    public void Letting_go_of_the_unheld_unloads_every_picture_nothing_holds()
    {
        var pictures = new Pictures(keptUnheld: 10);
        pictures.TurnPast(1, 2, 3);
        PictureShelf<int, Picture>.PictureHold shown = pictures.Shelf.Hold(4);

        pictures.Shelf.LetGoOfUnheld();

        Assert.Equal(new[] { 1, 2, 3 }, pictures.Unloads);
        Assert.Equal(1, pictures.Shelf.Loaded);
        shown.Dispose();
        Assert.Equal(1, pictures.Shelf.Loaded);
    }

    [Fact]
    public void A_name_without_a_picture_gives_a_hold_of_nothing_and_is_tried_again_when_asked_again()
    {
        var pictures = new Pictures(keptUnheld: 10, missing: new[] { 9 });

        PictureShelf<int, Picture>.PictureHold nothing = pictures.Shelf.Hold(9);
        nothing.Dispose();
        pictures.Shelf.Hold(9).Dispose();

        Assert.Null(nothing.Picture);
        Assert.Equal(new[] { 9, 9 }, pictures.Loads);
        Assert.Equal(0, pictures.Shelf.Loaded);
        Assert.Empty(pictures.Unloads);
    }

    [Fact]
    public void A_shelf_that_keeps_nothing_unloads_a_picture_as_soon_as_it_is_let_go_of()
    {
        var pictures = new Pictures(keptUnheld: 0);

        pictures.TurnPast(1);

        Assert.Equal(new[] { 1 }, pictures.Unloads);
        Assert.Equal(0, pictures.Shelf.Loaded);
    }

    [Fact]
    public void A_shelf_cannot_keep_fewer_than_no_pictures()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PictureShelf<int, Picture>(-1, _ => null, _ => { }));
    }
}
