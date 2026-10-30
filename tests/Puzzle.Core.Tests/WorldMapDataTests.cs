using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Places;
using Puzzle.Tooling.WorldMap;

namespace Puzzle.Core.Tests;

/// <summary>
/// The world map's data (#4): where each painting's pin stands and what its card says of the place, read through a
/// closed loader that refuses a painting without a place and falls back cleanly for a missing language or key; the
/// projection both the map's build and the game use; the map file and the tool that writes it.
/// </summary>
public class WorldMapDataTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly Lazy<MuseumCatalogue> Catalogue = new(() =>
        CatalogueFileMapper.ToCatalogue(JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    private static PlacesFile ShippedPlacesFile() => JsonSerializer.Deserialize<PlacesFile>(File.ReadAllText(RepoFiles.PlacesPath), AsUnityMapsIt)!;

    /// <summary>A language's labels as tools/unity/sync-engine.js packs them for the game: each kind as a list of key and text.</summary>
    private static PlaceLabelsFile PackedLabels(string code)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoFiles.PlaceLabelsFolder, $"{code}.json")));
        JsonElement root = document.RootElement;
        LabelEntryFile[] Entries(string kind) => root.TryGetProperty(kind, out JsonElement labels)
            ? labels.EnumerateObject().Select(label => new LabelEntryFile { key = label.Name, text = label.Value.GetString()! }).ToArray()
            : Array.Empty<LabelEntryFile>();
        return new PlaceLabelsFile
        {
            language = root.GetProperty("language").GetString()!,
            review = root.GetProperty("review").GetString()!,
            places = Entries("places"),
            notes = Entries("notes"),
            museums = Entries("museums"),
        };
    }

    private static PlaceLabelTable Table(string code, params (LabelKind Kind, string Key, string Text)[] labels) => PlacesFileMapper.ToTable(new PlaceLabelsFile
    {
        language = code,
        review = "machine",
        places = labels.Where(label => label.Kind == LabelKind.Place).Select(label => new LabelEntryFile { key = label.Key, text = label.Text }).ToArray(),
        notes = labels.Where(label => label.Kind == LabelKind.Note).Select(label => new LabelEntryFile { key = label.Key, text = label.Text }).ToArray(),
        museums = labels.Where(label => label.Kind == LabelKind.Museum).Select(label => new LabelEntryFile { key = label.Key, text = label.Text }).ToArray(),
    });

    /// <summary>A places file that gives the first painting of the catalogue its place, and every other one a shared one.</summary>
    private static PlacesFile PlacesFor(MuseumCatalogue catalogue, Func<PaintingPlaceFile, PaintingPlaceFile>? first = null)
    {
        PaintingPlaceFile[] entries = catalogue.Rooms.SelectMany(room => room.Artworks).Select(artwork => new PaintingPlaceFile
        {
            id = artwork.Id.Value, lat = 48.85, lon = 2.35, place = "paris", precision = "city", verified = true, museum = "louvre", note = null,
        }).ToArray();
        if (first != null)
        {
            entries[0] = first(entries[0]);
        }

        return new PlacesFile { formatVersion = 1, paintings = entries };
    }

    [Fact]
    public void Every_painting_of_the_catalogue_has_a_place_in_the_repositorys_file_and_the_source_names_every_label()
    {
        MuseumPlaces places = PlacesFileMapper.ToPlaces(ShippedPlacesFile(), Catalogue.Value);
        PlaceLabels labels = PlaceLabels.Of(places, PlacesFileMapper.ToTable(PackedLabels("tr")), Array.Empty<PlaceLabelTable>());

        Assert.Equal(MuseumPlan.PaintingCount, places.All.Count());
        foreach (Artwork artwork in Catalogue.Value.Rooms.SelectMany(room => room.Artworks))
        {
            PlaceWords words = labels.WordsOf(places.Of(artwork));
            Assert.False(string.IsNullOrWhiteSpace(words.Place.Text), $"{artwork.Id} has no place to say");
            Assert.False(string.IsNullOrWhiteSpace(words.Museum.Text), $"{artwork.Id} has no museum to say");
        }
    }

    [Fact]
    public void Every_language_file_of_the_labels_reads_and_is_held_against_the_letters_the_game_can_write()
    {
        foreach (string file in Directory.GetFiles(RepoFiles.PlaceLabelsFolder, "*.json"))
        {
            string code = Path.GetFileNameWithoutExtension(file);
            PlaceLabelTable table = PlacesFileMapper.ToTable(PackedLabels(code));
            Assert.Equal(code, table.Language);
            Assert.True(table.Count > 0, $"{code}: no labels");
        }
    }

    [Fact]
    public void Every_language_the_game_speaks_names_every_place_museum_and_note_of_the_map_itself()
    {
        MuseumPlaces places = PlacesFileMapper.ToPlaces(ShippedPlacesFile(), Catalogue.Value);
        foreach (Puzzle.Client.Words.Language language in Puzzle.Client.Words.Languages.All)
        {
            string code = Puzzle.Client.Words.Languages.CodeOf(language);
            PlaceLabelTable table = PlacesFileMapper.ToTable(PackedLabels(code));
            Assert.All(places.KeysNamed, key => Assert.True(table.Find(key) != null, $"{code} lacks {key}: the map would say it in English"));
        }
    }

    [Fact]
    public void A_painting_without_a_place_is_refused_by_its_id()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        PlacesFile file = PlacesFor(catalogue);
        string left = file.paintings[7].id;
        file.paintings = file.paintings.Where(entry => entry.id != left).ToArray();

        PlacesFormatException refusal = Assert.Throws<PlacesFormatException>(() => PlacesFileMapper.ToPlaces(file, catalogue));
        Assert.Contains(left, refusal.Message);
    }

    [Theory]
    [InlineData("unknown-painting", 48.0, 2.0, "city", "names painting(s) the catalogue does not have")]
    [InlineData(null, 91.0, 2.0, "city", "latitude")]
    [InlineData(null, 48.0, 181.0, "city", "longitude")]
    [InlineData(null, 48.0, 2.0, "street", "exact, city or region")]
    public void A_place_off_the_earth_of_an_unknown_painting_or_of_an_unknown_precision_is_refused(string? id, double lat, double lon, string precision, string said)
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        PlacesFile file = PlacesFor(catalogue, entry => new PaintingPlaceFile
        {
            id = entry.id, lat = lat, lon = lon, place = entry.place, precision = precision, museum = entry.museum,
        });
        if (id != null)
        {
            file.paintings = file.paintings.Append(new PaintingPlaceFile { id = id, lat = lat, lon = lon, place = "x", precision = "city", museum = "y" }).ToArray();
        }

        PlacesFormatException refusal = Assert.Throws<PlacesFormatException>(() => PlacesFileMapper.ToPlaces(file, catalogue));
        Assert.Contains(said, refusal.Message);
    }

    [Fact]
    public void A_painting_given_two_places_or_a_file_of_another_version_is_refused()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        PlacesFile doubled = PlacesFor(catalogue);
        doubled.paintings = doubled.paintings.Append(doubled.paintings[0]).ToArray();
        Assert.Contains("two places", Assert.Throws<PlacesFormatException>(() => PlacesFileMapper.ToPlaces(doubled, catalogue)).Message);

        PlacesFile later = PlacesFor(catalogue);
        later.formatVersion = 2;
        Assert.Contains("format version 2", Assert.Throws<PlacesFormatException>(() => PlacesFileMapper.ToPlaces(later, catalogue)).Message);
    }

    [Fact]
    public void A_note_left_null_or_empty_is_no_note_and_a_given_one_is_said()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        PlacesFile file = PlacesFor(catalogue, entry => new PaintingPlaceFile
        {
            id = entry.id, lat = 43.68, lon = 4.63, place = "arles", precision = "city", museum = "louvre", note = "made-elsewhere",
        });
        file.paintings[1].note = string.Empty;
        MuseumPlaces places = PlacesFileMapper.ToPlaces(file, catalogue);
        PlaceLabelTable source = Table("tr", (LabelKind.Place, "paris", "Paris, Fransa"), (LabelKind.Place, "arles", "Arles, Fransa"), (LabelKind.Museum, "louvre", "Louvre, Paris"), (LabelKind.Note, "made-elsewhere", "Başka yerde yapıldı"));
        PlaceLabels labels = PlaceLabels.Of(places, source, Array.Empty<PlaceLabelTable>());
        Artwork[] artworks = catalogue.Rooms.SelectMany(room => room.Artworks).ToArray();

        Assert.Equal("Başka yerde yapıldı", labels.WordsOf(places.Of(artworks[0])).Note?.Text);
        Assert.Null(labels.WordsOf(places.Of(artworks[1])).Note);
        Assert.Null(labels.WordsOf(places.Of(artworks[2])).Note);
    }

    /// <summary>
    /// The fallback chosen: a key the player's language lacks is said in English, and one English lacks too in the
    /// source, Turkish; a language without a file is not asked at all, so it falls back to English as a whole.
    /// </summary>
    [Fact]
    public void A_missing_key_falls_back_to_English_then_to_the_source_and_a_missing_language_to_English_as_a_whole()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        MuseumPlaces places = PlacesFileMapper.ToPlaces(PlacesFor(catalogue), catalogue);
        PlaceLabelTable source = Table("tr", (LabelKind.Place, "paris", "Paris, Fransa"), (LabelKind.Museum, "louvre", "Louvre Müzesi, Paris"));
        PlaceLabelTable english = Table("en", (LabelKind.Place, "paris", "Paris, France"));
        PlaceLabelTable korean = Table("ko", (LabelKind.Place, "paris", "프랑스 파리"));
        PaintingPlace place = places.All.First();

        PlaceWords inKorean = PlaceLabels.Of(places, source, new[] { korean, english }).WordsOf(place);
        Assert.Equal(("프랑스 파리", "ko"), (inKorean.Place.Text, inKorean.Place.Language));
        Assert.Equal(("Louvre Müzesi, Paris", "tr"), (inKorean.Museum.Text, inKorean.Museum.Language));

        PlaceLabels withoutAJapaneseFile = PlaceLabels.Of(places, source, new[] { english });
        Assert.Equal(("Paris, France", "en"), (withoutAJapaneseFile.WordsOf(place).Place.Text, withoutAJapaneseFile.WordsOf(place).Place.Language));
        Assert.Equal(new[] { "en", "tr" }, withoutAJapaneseFile.Languages);

        Assert.Equal(new[] { "tr" }, PlaceLabels.Of(places, source, new[] { source }).Languages);
    }

    [Theory]
    [InlineData("tr", new string[0])]
    [InlineData("en", new[] { "en" })]
    [InlineData("ko", new[] { "ko", "en" })]
    [InlineData("ja", new[] { "ja", "en" })]
    public void A_player_reads_their_own_labels_then_Englishs_and_one_who_reads_the_source_reads_the_source_alone(string spoken, string[] asked)
    {
        Assert.Equal(asked, PlaceLabels.AskedBeforeTheSource(spoken, "en", "tr"));
    }

    [Fact]
    public void A_source_that_lacks_a_key_the_places_name_is_refused_by_the_key()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        MuseumPlaces places = PlacesFileMapper.ToPlaces(PlacesFor(catalogue), catalogue);
        PlaceLabelTable source = Table("tr", (LabelKind.Place, "paris", "Paris, Fransa"));

        PlacesFormatException refusal = Assert.Throws<PlacesFormatException>(() => PlaceLabels.Of(places, source, Array.Empty<PlaceLabelTable>()));
        Assert.Contains("Museum louvre", refusal.Message);
    }

    [Fact]
    public void A_label_table_with_a_key_twice_or_a_label_without_words_is_refused()
    {
        Assert.Contains("twice", Assert.Throws<PlacesFormatException>(() => Table("en", (LabelKind.Place, "paris", "Paris"), (LabelKind.Place, "paris", "Paris again"))).Message);
        Assert.Contains("no words", Assert.Throws<PlacesFormatException>(() => Table("en", (LabelKind.Museum, "louvre", " "))).Message);
        Assert.Contains("without spaces", Assert.Throws<PlacesFormatException>(() => Table("en", (LabelKind.Note, "two words", "Note"))).Message);
    }

    [Fact]
    public void The_projection_puts_the_poles_the_equator_and_the_date_line_where_the_design_has_them()
    {
        Assert.Equal(0f, NaturalEarthProjection.Project(0, 90).Y, 3);
        Assert.Equal(500f, NaturalEarthProjection.Project(0, 0).X, 3);
        Assert.Equal(NaturalEarthProjection.Top, NaturalEarthProjection.Project(0, 0).Y, 3);
        Assert.Equal(0f, NaturalEarthProjection.Project(-180, 0).X, 3);
        Assert.Equal(1000f, NaturalEarthProjection.Project(180, 0).X, 3);

        // The design canvas' own figures (out/design/worldmap-src/data.json): Monet's beach at Sainte-Adresse and Hokusai's Kanagawa.
        MapPoint beach = NaturalEarthProjection.Project(new GeoPoint(49.509, 0.084));
        Assert.Equal((500.205f, 100.854f), (MathF.Round(beach.X, 3), MathF.Round(beach.Y, 3)));
    }

    [Fact]
    public void The_map_file_reads_back_what_was_written_and_refuses_what_it_is_not()
    {
        var land = new MapOutline(
            new[] { new MapPoint(1f, 2f), new MapPoint(10.05f, 2f), new MapPoint(10.05f, 9.95f), new MapPoint(1f, 9.95f) },
            new[] { new RingSpan(0, 4) },
            new[] { 0, 1, 2, 0, 2, 3 });
        byte[] bytes = WorldMapFormat.Write(new WorldMapShape(land, MapOutline.Empty));

        WorldMapShape read = WorldMapFormat.Read(bytes);
        Assert.Equal(land.Points, read.Land.Points);
        Assert.Equal(land.Triangles, read.Land.Triangles);
        Assert.Equal((0, 4), (read.Land.Rings[0].Start, read.Land.Rings[0].Count));
        Assert.Empty(read.Lakes.Points);

        Assert.Throws<WorldMapFormatException>(() => WorldMapFormat.Read(bytes.Take(bytes.Length - 3).ToArray()));
        byte[] wrong = (byte[])bytes.Clone();
        wrong[0] = (byte)'X';
        Assert.Throws<WorldMapFormatException>(() => WorldMapFormat.Read(wrong));
        Assert.Throws<WorldMapFormatException>(() => WorldMapFormat.Read(bytes.Append((byte)0).ToArray()));
    }

    [Fact]
    public void The_map_the_game_carries_is_the_one_its_record_names_and_its_triangles_fill_exactly_its_rings()
    {
        byte[] bytes = File.ReadAllBytes(RepoFiles.WorldMapPath);
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.WorldMapRecordPath));
        Assert.Equal(record.RootElement.GetProperty("output").GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Contains("public domain", record.RootElement.GetProperty("licence").GetString()!, StringComparison.OrdinalIgnoreCase);

        WorldMapShape map = WorldMapFormat.Read(bytes);
        foreach (MapOutline outline in new[] { map.Land, map.Lakes })
        {
            double rings = outline.Rings.Sum(ring => SignedArea(outline.Points.Skip(ring.Start).Take(ring.Count).ToList()));
            double triangles = 0;
            for (int t = 0; t < outline.Triangles.Count; t += 3)
            {
                triangles += Math.Abs(SignedArea(new[] { outline.Points[outline.Triangles[t]], outline.Points[outline.Triangles[t + 1]], outline.Points[outline.Triangles[t + 2]] }));
            }

            Assert.InRange(triangles, Math.Abs(rings) * 0.999, Math.Abs(rings) * 1.001);
        }

        // Every painting's pin stands on the map's width, and none in the dropped south.
        (float top, float bottom) = map.Land.SpanOfY;
        Assert.True(top >= 0f && bottom <= NaturalEarthProjection.Project(0, -62).Y, $"the land runs from {top} to {bottom}");
    }

    /// <summary>Outer rings and holes run opposite ways, so their signed areas add up to the land's area.</summary>
    private static double SignedArea(IReadOnlyList<MapPoint> ring)
    {
        double sum = 0;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            sum += (double)ring[j].X * ring[i].Y - (double)ring[i].X * ring[j].Y;
        }

        return sum / 2;
    }

    [Fact]
    public void Earcut_fills_a_concave_ring_and_leaves_a_hole_empty()
    {
        var square = new List<(double X, double Y)> { (0, 0), (10, 0), (10, 10), (0, 10), (3, 3), (3, 7), (7, 7), (7, 3) };
        List<int> withHole = Earcut.Triangulate(square, new[] { 4 });
        Assert.Equal(100 - 16, AreaOf(square, withHole), 6);

        var notch = new List<(double X, double Y)> { (0, 0), (10, 0), (10, 10), (5, 4), (0, 10) };
        List<int> concave = Earcut.Triangulate(notch, Array.Empty<int>());
        Assert.Equal(WorldMapBuilder.AreaOf(notch), AreaOf(notch, concave), 6);
        Assert.Equal(3 * 3, concave.Count);
    }

    private static double AreaOf(IReadOnlyList<(double X, double Y)> points, IReadOnlyList<int> triangles)
    {
        double area = 0;
        for (int t = 0; t < triangles.Count; t += 3)
        {
            area += WorldMapBuilder.AreaOf(new[] { points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]] });
        }

        return area;
    }

    [Fact]
    public void Douglas_Peucker_keeps_the_ends_and_the_bends_and_drops_what_lies_on_the_line()
    {
        var run = new List<(double X, double Y)> { (0, 0), (1, 0.01), (2, 0), (3, 2), (4, 0) };
        Assert.Equal(new (double, double)[] { (0, 0), (2, 0), (3, 2), (4, 0) }, WorldMapBuilder.Simplify(run, 0.035));
    }

    [Fact]
    public void A_tiny_map_built_from_GeoJSON_drops_the_small_island_and_the_far_south_and_fills_what_it_keeps()
    {
        const string Land = """
            {"type":"FeatureCollection","features":[
              {"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[0,0],[20,0],[20,20],[0,20],[0,0]]]}},
              {"type":"Feature","geometry":{"type":"Polygon","coordinates":[[[40,40],[40.01,40],[40.01,40.01],[40,40]]]}},
              {"type":"Feature","geometry":{"type":"MultiPolygon","coordinates":[[[[0,-80],[60,-80],[60,-70],[0,-70],[0,-80]]]]}}]}
            """;
        const string Lakes = """{"type":"FeatureCollection","features":[]}""";

        WorldMapBuild build = WorldMapBuilder.Build(Land, Lakes, WorldMapSettings.OfTheDesign);

        Assert.Equal((1, 2), (build.LandPolygons, build.PolygonsDropped));
        Assert.Single(build.Map.Land.Rings);
        Assert.Equal(build.AreaAsRings, build.AreaAsTriangles, 6);
    }

    [Fact]
    public void Every_room_has_a_short_name_and_its_years_span_its_paintings_dates_as_the_design_writes_them()
    {
        MuseumCatalogue catalogue = Catalogue.Value;
        Assert.All(catalogue.Rooms, room => Assert.False(string.IsNullOrWhiteSpace(room.ShortName)));
        Assert.Equal("Edo ve Meiji", catalogue.Room(new RoomNumber(6)).ShortName);
        Assert.Equal((1867, 1906), catalogue.Room(new RoomNumber(1)).Years);
        Assert.Equal((1884, 1893), catalogue.Room(new RoomNumber(2)).Years);
        Assert.Equal((1720, 1775), catalogue.Room(new RoomNumber(14)).Years);
    }

    [Fact]
    public void A_room_without_a_short_name_is_refused()
    {
        CatalogueFile file = JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!;
        file.rooms[4].shortName = string.Empty;

        Assert.Contains("Room 5 has no short name", Assert.Throws<CatalogueFormatException>(() => CatalogueFileMapper.ToCatalogue(file)).Message);
    }
}
