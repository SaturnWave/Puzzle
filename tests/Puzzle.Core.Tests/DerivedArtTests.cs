using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Puzzle.Client.Art;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the Unity Editor derives from the game's sources and the repository keeps beside them: the art catalogue
/// the game's scene refers to, and the typefaces made from the font files. The game reads the catalogue by the
/// order of its lists of drawings and styles, and a build carries a typeface as pictures of its letters; so a list
/// that grew, or a font file that changed, without the Editor's project setup having run since leaves files that
/// no longer fit their sources, and a game that stops at the first new drawing. Only the Editor can write these
/// files. Here a stale one fails a test that says what to run, where nothing else would notice before the game
/// is started.
/// </summary>
public partial class DerivedArtTests
{
    private const string HowToDerive = "run ProjectSetup.Run in the Unity Editor's batch mode and keep the files it writes with the sources";

    private static readonly Lazy<string> Catalogue = new(() => File.ReadAllText(RepoFiles.ArtCataloguePath));

    [Fact]
    public void The_catalogue_on_disk_holds_a_sprite_for_every_drawing_of_the_pieces_and_of_the_gallery()
    {
        AssertHoldsOneFor<PieceArt>("_sprites");
        AssertHoldsOneFor<GalleryArt>("_gallerySprites");
    }

    [Fact]
    public void The_catalogue_on_disk_holds_a_typeface_and_a_material_for_every_lettering_style()
    {
        AssertHoldsOneFor<LetteringStyle>("_typefaces");
        AssertHoldsOneFor<LetteringStyle>("_letteringMaterials");
    }

    /// <summary>
    /// Young Serif's own figures are old-style ones: "10" in them reads as the Turkish letters "ıo", and 3, 4, 5, 7
    /// and 9 hang under the line. The game's copy of the font answers with its lining figures instead
    /// (tools/unity/lining-figures.js), and the typeface made from it must hold those.
    /// </summary>
    [Fact]
    public void The_serifs_typeface_on_disk_draws_figures_as_high_as_its_capitals_that_stand_on_the_line()
    {
        TypefaceFile serif = TypefaceFile.Read(RepoFiles.TypefacePath("YoungSerif-Regular"));
        Drawing capital = serif.DrawingOf('H');

        foreach (char figure in "0123456789")
        {
            Drawing drawn = serif.DrawingOf(figure);

            Assert.True(drawn.Top >= 0.97f * capital.Top, $"The serif's {figure} is {drawn.Top} high beside a capital of {capital.Top}: an old-style figure; {HowToDerive}.");
            Assert.True(drawn.Foot >= -0.05f * capital.Top, $"The serif's {figure} hangs {-drawn.Foot} under the line: an old-style figure; {HowToDerive}.");
        }
    }

    /// <summary>A list of the catalogue must name one thing for every member of the enum the game reads it by, and nothing that is missing.</summary>
    private static void AssertHoldsOneFor<TEnum>(string list)
        where TEnum : struct, Enum
    {
        string[] entries = EntriesOf(list);
        int members = Enum.GetValues<TEnum>().Length;

        Assert.True(
            entries.Length == members,
            $"The art catalogue on disk lists {entries.Length} under {list} and the game has {members} of {typeof(TEnum).Name}: {HowToDerive}.");
        Assert.All(entries, entry => Assert.False(NamesNothing().IsMatch(entry), $"The art catalogue on disk has an empty place under {list}: {HowToDerive}."));
    }

    /// <summary>The entries of one list of the catalogue, as Unity writes a list: a line per entry under the list's name, each beginning with a dash.</summary>
    private static string[] EntriesOf(string list)
    {
        string[] lines = Catalogue.Value.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        int name = Array.FindIndex(lines, line => line.StartsWith($"  {list}:", StringComparison.Ordinal));
        Assert.True(name >= 0, $"The art catalogue on disk has no list {list}: {HowToDerive}.");
        return lines.Skip(name + 1).TakeWhile(line => line.StartsWith("  - ", StringComparison.Ordinal)).ToArray();
    }

    /// <summary>An entry that refers to no asset: <c>{fileID: 0}</c>.</summary>
    [GeneratedRegex(@"\{fileID: 0\}")]
    private static partial Regex NamesNothing();

    /// <summary>A letter of a typeface's character table: <c>m_Unicode: 72</c> and, on the next line, <c>m_GlyphIndex: 41</c>.</summary>
    [GeneratedRegex(@"m_Unicode: (?<letter>\d+)\s+m_GlyphIndex: (?<drawing>\d+)")]
    private static partial Regex LetterOfATypeface();

    /// <summary>A drawing of a typeface's glyph table, with the height of its picture and how far its top lies over the line.</summary>
    [GeneratedRegex(@"m_Index: (?<drawing>\d+)\s+m_Metrics:\s+m_Width: [-\d.e]+\s+m_Height: (?<height>[-\d.e]+)\s+m_HorizontalBearingX: [-\d.e]+\s+m_HorizontalBearingY: (?<top>[-\d.e]+)")]
    private static partial Regex DrawingOfATypeface();

    /// <summary>How a letter is drawn, in the pixels of its typeface's picture: its top and its foot, from the line it stands on.</summary>
    private sealed record Drawing(float Top, float Foot);

    /// <summary>A typeface as the Editor wrote it: the drawing of each of its letters.</summary>
    private sealed class TypefaceFile
    {
        private readonly Dictionary<int, int> _drawingByLetter;
        private readonly Dictionary<int, Drawing> _drawings;

        private TypefaceFile(Dictionary<int, int> drawingByLetter, Dictionary<int, Drawing> drawings)
        {
            _drawingByLetter = drawingByLetter;
            _drawings = drawings;
        }

        public static TypefaceFile Read(string path)
        {
            Assert.True(File.Exists(path), $"{Path.GetFileName(path)} is missing: {HowToDerive}.");
            string text = File.ReadAllText(path);

            // The tables come before the picture of the letters, which is most of the file.
            int glyphs = text.IndexOf("m_GlyphTable:", StringComparison.Ordinal);
            int letters = text.IndexOf("m_CharacterTable:", StringComparison.Ordinal);
            int afterLetters = text.IndexOf("m_AtlasTextures:", StringComparison.Ordinal);
            Assert.True(glyphs >= 0 && letters > glyphs && afterLetters > letters, $"{Path.GetFileName(path)} is not a typeface as the Editor writes one.");

            return new TypefaceFile(
                LetterOfATypeface().Matches(text[letters..afterLetters]).ToDictionary(Whole("letter"), Whole("drawing")),
                DrawingOfATypeface().Matches(text[glyphs..letters]).ToDictionary(
                    Whole("drawing"),
                    drawn => new Drawing(Number(drawn, "top"), Number(drawn, "top") - Number(drawn, "height"))));
        }

        public Drawing DrawingOf(char letter)
        {
            Assert.True(_drawingByLetter.TryGetValue(letter, out int drawing) && _drawings.ContainsKey(drawing), $"The typeface has no drawing for {letter}.");
            return _drawings[drawing];
        }

        private static Func<Match, int> Whole(string group) => match => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

        private static float Number(Match match, string group) => float.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }
}
