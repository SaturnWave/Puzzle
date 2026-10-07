using System.IO;
using System.Linq;
using System.Text.Json;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The museums the tests walk through: the one the repository ships (museum/catalogue.json), in which only the
/// first room has its paintings, and one in which a work is chosen for every place, as the finished game will have
/// it. The rules that follow a player through the rooms are tested on the second; what a player meets where the
/// paintings end, on the first.
/// </summary>
internal static class TestMuseums
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<MuseumCatalogue> ShippedCatalogue = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    private static readonly Lazy<MuseumCatalogue> FullCatalogue = new(() => new MuseumCatalogue(
        RoomNumber.All.Select(room => new MuseumRoom(room, $"Akım {room}", isNameProvisional: false, room.Paintings.Select(Work).ToArray())).ToArray()));

    /// <summary>The catalogue the game carries today.</summary>
    public static MuseumCatalogue Shipped => ShippedCatalogue.Value;

    /// <summary>A catalogue with a work at each of the museum's hundred places.</summary>
    public static MuseumCatalogue Full => FullCatalogue.Value;

    private static Artwork Work(PaintingNumber painting) => new(
        new ArtworkId($"work-{painting.Value:D3}"),
        new ArtworkCaption($"Tablo {painting}", $"Painting {painting}", "Ressam", "1900"),
        PaintingOrientation.Landscape,
        $"room-{painting.Room.Value:D2}/work-{painting.Value:D3}.jpg",
        new ArtworkProvenance("test", "test"));
}
