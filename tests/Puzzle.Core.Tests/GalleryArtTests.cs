using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Puzzle.Client.Art;

namespace Puzzle.Core.Tests;

/// <summary>
/// The gallery's drawings in what can be said of them without the Editor that imports them. A drawing stands in
/// four places that must agree: its source in art/unity, its member of <see cref="GalleryArt"/>, its file name in
/// the Editor's catalogue builder and its measures in the Editor's import. The Editor's project setup refuses a
/// disagreement too, but only when it is run; here a forgotten place fails a test, with the drawing's name.
/// The two Editor files cannot be compiled outside Unity, so their tables are read as they are written.
/// </summary>
public partial class GalleryArtTests
{
    /// <summary>The wall's pattern is exported with the sprites and is none: a texture that repeats, with no member and no measures.</summary>
    private const string Damask = "damask";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> FileNamesByMember = new(() => CatalogueFileName()
        .Matches(File.ReadAllText(RepoFiles.ClientEditorScriptPath("ArtCatalogueBuilder.cs")))
        .ToDictionary(line => line.Groups["member"].Value, line => line.Groups["file"].Value));

    private static readonly Lazy<IReadOnlyDictionary<string, Measures>> MeasuresByFile = new(() => ImportMeasures()
        .Matches(File.ReadAllText(RepoFiles.ClientEditorScriptPath("GalleryArtImport.cs")))
        .ToDictionary(line => line.Groups["file"].Value, Measures.Of));

    [Fact]
    public void Every_drawing_of_the_gallery_has_one_file_name_in_the_catalogue_builder()
    {
        string[] members = Enum.GetNames<GalleryArt>();

        Assert.Equal(members.OrderBy(name => name, StringComparer.Ordinal), FileNamesByMember.Value.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(members.Length, FileNamesByMember.Value.Values.Distinct().Count());
    }

    [Fact]
    public void Every_drawing_of_the_gallery_has_a_source_and_every_source_is_a_drawing_of_the_gallery()
    {
        Assert.Equal(Sorted(FileNamesByMember.Value.Values.Append(Damask)), FilesOf(RepoFiles.GalleryArtSourcesFolder, "*.svg"));
    }

    [Fact]
    public void Every_drawing_of_the_gallery_has_measures_for_its_import_and_no_other_file_has()
    {
        Assert.Equal(Sorted(FileNamesByMember.Value.Values), Sorted(MeasuresByFile.Value.Keys));
    }

    [Fact]
    public void Every_source_is_exported_as_a_picture_and_no_picture_is_left_without_its_source()
    {
        Assert.Equal(FilesOf(RepoFiles.GalleryArtSourcesFolder, "*.svg"), FilesOf(RepoFiles.GalleryArtPicturesFolder, "*.png"));
    }

    /// <summary>
    /// The import turns a source's points into a sprite's: it takes the picture's width for the points the table
    /// states and lays the pivot and the borders a sprite stretches from inside the picture by the same measure.
    /// </summary>
    [Fact]
    public void A_drawings_measures_are_those_of_its_source()
    {
        foreach ((string file, Measures measures) in MeasuresByFile.Value)
        {
            Source source = Source.Read(Path.Combine(RepoFiles.GalleryArtSourcesFolder, file + ".svg"));

            Assert.True(measures.PointsWide == source.PointsWide, $"{file}: the import takes it for {measures.PointsWide} points wide, its source draws {source.PointsWide}");
            Assert.True(measures.PivotX >= 0f && measures.PivotX <= source.PointsWide && measures.PivotY >= 0f && measures.PivotY <= source.PointsHigh, $"{file}: the pivot lies outside the picture");
            Assert.True(2f * measures.StretchesFrom < source.PointsWide, $"{file}: the borders at its sides leave no middle to stretch");
            Assert.True(measures.StretchesFromTop + measures.StretchesFromFoot < source.PointsHigh, $"{file}: the borders at its top and foot leave no middle to stretch");
        }
    }

    /// <summary>
    /// The exporter photographs a source in a browser window of the size its root tag states: a window cannot be
    /// narrower than 512 px, and a point must be as many pixels high as it is wide, or the sprite comes out squeezed.
    /// </summary>
    [Fact]
    public void A_source_states_a_picture_the_exporter_can_photograph_with_square_points()
    {
        foreach (string file in FilesOf(RepoFiles.GalleryArtSourcesFolder, "*.svg"))
        {
            Source source = Source.Read(Path.Combine(RepoFiles.GalleryArtSourcesFolder, file + ".svg"));

            Assert.True(source.PixelsWide >= 512, $"{file}: {source.PixelsWide} px is narrower than the browser's narrowest window");
            Assert.Equal(source.PixelsWide / source.PointsWide, source.PixelsHigh / source.PointsHigh, precision: 3);
        }
    }

    private static string[] FilesOf(string folder, string pattern) =>
        Sorted(Directory.GetFiles(folder, pattern).Select(path => Path.GetFileNameWithoutExtension(path)!));

    private static string[] Sorted(IEnumerable<string> names) => names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    /// <summary>A line of the catalogue builder's list: <c>[GalleryArt.Plaque] = "plaque",</c>.</summary>
    [GeneratedRegex(@"\[GalleryArt\.(?<member>\w+)\] = ""(?<file>[a-z0-9-]+)""")]
    private static partial Regex CatalogueFileName();

    /// <summary>A line of the import's table: <c>["plaque"] = new Picture(pointsWide: 128f, pivot: new Vector2(64f, 46f), stretchesFrom: 36f, ...),</c>.</summary>
    [GeneratedRegex(@"\[""(?<file>[a-z0-9-]+)""\] = new Picture\(pointsWide: (?<wide>[\d.]+)f, pivot: new Vector2\((?<pivotX>[\d.]+)f, (?<pivotY>[\d.]+)f\)(?<rest>[^\n]*)\),")]
    private static partial Regex ImportMeasures();

    [GeneratedRegex(@"\bstretchesFrom: (?<points>[\d.]+)f")]
    private static partial Regex SideBorder();

    [GeneratedRegex(@"\bstretchesFromTop: (?<points>[\d.]+)f")]
    private static partial Regex TopBorder();

    [GeneratedRegex(@"\bstretchesFromFoot: (?<points>[\d.]+)f")]
    private static partial Regex FootBorder();

    [GeneratedRegex(@"<svg\b[^>]*\bwidth=""(?<pixelsWide>\d+)""[^>]*\bheight=""(?<pixelsHigh>\d+)""[^>]*\bviewBox=""[-\d.]+ [-\d.]+ (?<pointsWide>[\d.]+) (?<pointsHigh>[\d.]+)""")]
    private static partial Regex RootTag();

    private static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>What the import's table says of a drawing, in the points of its source.</summary>
    private sealed record Measures(float PointsWide, float PivotX, float PivotY, float StretchesFrom, float StretchesFromTop, float StretchesFromFoot)
    {
        public static Measures Of(Match line)
        {
            string rest = line.Groups["rest"].Value;
            return new Measures(
                Number(line.Groups["wide"].Value),
                Number(line.Groups["pivotX"].Value),
                Number(line.Groups["pivotY"].Value),
                BorderIn(SideBorder(), rest),
                BorderIn(TopBorder(), rest),
                BorderIn(FootBorder(), rest));
        }

        /// <summary>A border the table does not state is none: the sprite does not stretch that way.</summary>
        private static float BorderIn(Regex border, string rest) => border.Match(rest) is { Success: true } stated ? Number(stated.Groups["points"].Value) : 0f;
    }

    /// <summary>What a source's root tag says of its picture: its size in px and in the design's points.</summary>
    private sealed record Source(int PixelsWide, int PixelsHigh, float PointsWide, float PointsHigh)
    {
        public static Source Read(string path)
        {
            Match root = RootTag().Match(File.ReadAllText(path));
            Assert.True(root.Success, $"{Path.GetFileName(path)}: the root tag must state width, height and viewBox, in that order");
            return new Source(
                int.Parse(root.Groups["pixelsWide"].Value, CultureInfo.InvariantCulture),
                int.Parse(root.Groups["pixelsHigh"].Value, CultureInfo.InvariantCulture),
                Number(root.Groups["pointsWide"].Value),
                Number(root.Groups["pointsHigh"].Value));
        }
    }
}
