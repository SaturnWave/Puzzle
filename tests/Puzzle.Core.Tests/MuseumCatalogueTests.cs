using System.IO;
using System.Text.Json;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The catalogue of rooms and paintings: the repository's own file (museum/catalogue.json) says what the design
/// decided, and a file that contradicts the museum's plan is refused with a message that names the problem.
/// </summary>
public class MuseumCatalogueTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(ReadShippedFile()));

    private static CatalogueFile ReadShippedFile() =>
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!;

    [Fact]
    public void The_first_room_shows_the_five_paintings_of_the_design_canvas_in_its_order()
    {
        MuseumRoom impressionism = Shipped.Value.Room(new RoomNumber(1));

        Assert.Equal(("Empresyonizm", false), (impressionism.Movement, impressionism.IsNameProvisional));
        Assert.Equal(
            new[]
            {
                "Sainte-Adresse Plajı, Claude Monet, 1867",
                "İki Kız Kardeş (Terasta), Pierre-Auguste Renoir, 1881",
                "Paris Sokağı; Yağmurlu Gün, Gustave Caillebotte, 1877",
                "Nilüferler, Claude Monet, 1906",
                "Buğday Yığınları (Yaz Sonu), Claude Monet, 1890–91",
            },
            impressionism.Artworks.Select(painting => painting.ToString()));
        Assert.Equal(
            new[] { PaintingOrientation.Landscape, PaintingOrientation.Portrait, PaintingOrientation.Landscape, PaintingOrientation.Landscape, PaintingOrientation.Landscape },
            impressionism.Artworks.Select(painting => painting.Orientation));
    }

    [Fact]
    public void Every_chosen_painting_names_its_source_its_licence_and_a_picture_that_is_in_the_game()
    {
        IEnumerable<Artwork> chosen = Shipped.Value.Rooms.SelectMany(room => room.Artworks);

        Assert.All(chosen, painting =>
        {
            Assert.Contains("https://", painting.Provenance.Source);
            Assert.Contains("ublic domain", painting.Provenance.Licence);
            Assert.True(File.Exists(Path.Combine(RepoFiles.PaintingPicturesFolder, painting.ImageFile)), $"{painting.ImageFile} is not in the Unity project.");
        });
    }

    [Fact]
    public void The_second_room_is_named_and_the_rooms_after_it_carry_provisional_names()
    {
        MuseumCatalogue catalogue = Shipped.Value;   // rooms whose paintings are chosen later keep provisional names until then

        Assert.Equal(("Post-empresyonizm", false), (catalogue.Room(new RoomNumber(2)).Movement, catalogue.Room(new RoomNumber(2)).IsNameProvisional));
        Assert.All(catalogue.Rooms.Skip(2), room => Assert.True(room.IsNameProvisional, $"Room {room.Number} is not marked provisional."));
        Assert.Equal(20, catalogue.Rooms.Select(room => room.Movement).Distinct().Count());
    }

    [Fact]
    public void A_place_whose_painting_is_not_chosen_yet_has_no_painting_and_still_its_levels_and_its_size()
    {
        var sixth = new PaintingNumber(6);

        Assert.Null(TestMuseums.EndingAfterRoomOne.ArtworkAt(sixth));
        Assert.Equal((51, 60, 10), (sixth.FirstLevel.Value, sixth.LastLevel.Value, sixth.Size.PieceCount));
        Assert.Equal("monet-stacks-of-wheat-end-of-summer", TestMuseums.EndingAfterRoomOne.ArtworkAt(new PaintingNumber(5))!.Id.Value);
    }

    [Fact]
    public void The_summary_lists_the_open_rooms_painting_by_painting_and_the_next_room_with_its_opening()
    {
        MuseumProgress progress = new MuseumProgress(levelsWon: 47, PlacedPieces.None).WithTrayPlaced(new PaintingNumber(1));

        IReadOnlyList<string> lines = MuseumSummary.Of(progress, Shipped.Value);

        Assert.Equal(
            new[]
            {
                "Museum after 47 of 1000 levels won:",
                "  rooms: 1 of 20 open, 0 restored",
                "  paintings: 1 of 100 whole; the next level gives pieces of painting 5",
                "  pieces: 47 won, 10 placed, 37 waiting on the trays",
                "Room 1: Empresyonizm, levels 1-50, open",
                "  painting 1, levels 1-10, 10 pieces (3-4-3): 10 won, whole; Sainte-Adresse Plajı, Claude Monet, 1867 [monet-beach-at-sainte-adresse, Landscape]",
                "  painting 2, levels 11-20, 10 pieces (3-4-3): 10 won, 0 placed, 10 waiting; İki Kız Kardeş (Terasta), Pierre-Auguste Renoir, 1881 [renoir-two-sisters-on-the-terrace, Portrait]",
                "  painting 3, levels 21-30, 10 pieces (3-4-3): 10 won, 0 placed, 10 waiting; Paris Sokağı; Yağmurlu Gün, Gustave Caillebotte, 1877 [caillebotte-paris-street-rainy-day, Landscape]",
                "  painting 4, levels 31-40, 10 pieces (3-4-3): 10 won, 0 placed, 10 waiting; Nilüferler, Claude Monet, 1906 [monet-water-lilies-1906, Landscape]",
                "  painting 5, levels 41-50, 10 pieces (3-4-3): 7 won, 0 placed, 7 waiting; Buğday Yığınları (Yaz Sonu), Claude Monet, 1890–91 [monet-stacks-of-wheat-end-of-summer, Landscape]",
                "Room 2: Post-empresyonizm, levels 51-100, closed, opens when level 50 is won",
            },
            lines);
    }

    [Fact]
    public void An_open_room_without_paintings_lists_its_places_as_not_chosen_yet()
    {
        IReadOnlyList<string> lines = MuseumSummary.Of(new MuseumProgress(levelsWon: 101, PlacedPieces.None), TestMuseums.EndingAfterRoomOne);

        Assert.Contains("  painting 6, levels 51-60, 10 pieces (3-4-3): 10 won, 0 placed, 10 waiting; not chosen yet", lines);
        Assert.Contains("  painting 11, levels 101-110, 20 pieces (5x4): 2 won, 0 placed, 2 waiting; not chosen yet", lines);
        Assert.StartsWith("Room 4: ", lines[^1]);
        Assert.Contains("(provisional name)", lines[^1]);
    }

    [Fact]
    public void A_paintings_cut_seed_follows_from_its_id_alone()
    {
        Assert.Equal(new ArtworkId("monet-water-lilies-1906").CutSeed, new ArtworkId("monet-water-lilies-1906").CutSeed);
        Assert.NotEqual(new ArtworkId("monet-water-lilies-1906").CutSeed, new ArtworkId("monet-water-lilies-1907").CutSeed);
        Assert.Equal(0xAF63DC4C8601EC8CUL, new ArtworkId("a").CutSeed); // the published FNV-1a sum of "a": the rule, not this runtime's string hash
        Assert.Throws<ArgumentException>(() => new ArtworkId("Water Lilies"));
        Assert.Throws<ArgumentException>(() => new ArtworkId(string.Empty));
    }

    [Theory]
    [MemberData(nameof(BrokenCatalogues))]
    public void A_catalogue_that_contradicts_the_museum_is_refused_by_name(string what, Action<CatalogueFile> breakIt, string expectedInMessage)
    {
        CatalogueFile file = ReadShippedFile();
        breakIt(file);

        var refusal = Assert.Throws<CatalogueFormatException>(() => CatalogueFileMapper.ToCatalogue(file));

        Assert.True(refusal.Message.Contains(expectedInMessage, StringComparison.Ordinal), $"{what}: the message was \"{refusal.Message}\".");
    }

    public static TheoryData<string, Action<CatalogueFile>, string> BrokenCatalogues() => new()
    {
        { "a later format", file => file.formatVersion = 2, "format version 2" },
        { "a room missing", file => file.rooms = file.rooms.Take(19).ToArray(), "19 rooms" },
        { "rooms out of order", file => (file.rooms[1], file.rooms[2]) = (file.rooms[2], file.rooms[1]), "Room 3 stands where room 2 belongs" },
        { "a room without a movement", file => file.rooms[7].movement = " ", "Room 8 has no art movement" },
        { "six paintings in a room", file => file.rooms[0].paintings = file.rooms[0].paintings.Append(new PaintingFile()).ToArray(), "6 paintings" },
        { "an id used twice", file => file.rooms[0].paintings[1].id = file.rooms[0].paintings[0].id, "used by another painting" },
        { "a picture used twice", file => file.rooms[0].paintings[1].image = file.rooms[0].paintings[0].image, "image file" },
        { "an id with a space", file => file.rooms[0].paintings[0].id = "Water Lilies", "may only hold" },
        { "no title", file => file.rooms[0].paintings[2].title = string.Empty, "has no title" },
        { "no licence note", file => file.rooms[0].paintings[2].licence = string.Empty, "has no licence note" },
        { "an unknown orientation", file => file.rooms[0].paintings[3].orientation = "square", "\"landscape\" or \"portrait\"" },
        { "a picture outside the folder", file => file.rooms[0].paintings[3].image = "../secret.jpg", "below the paintings folder" },
    };
}
