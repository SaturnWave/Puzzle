using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's lettering in what can be said of it without the engine that draws it: every style has a typeface,
/// and everything the game says can be written with the letters its typefaces are made with. A typeface holds a
/// fixed set of letters, so a word with a letter outside the set would be drawn with a gap in it; here it fails a
/// test instead, with the letter's name.
/// </summary>
public partial class LetteringTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    /// <summary>The record of the font files names its fields in lower case.</summary>
    private static readonly JsonSerializerOptions AsTheRecordIsWritten = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    [Fact]
    public void Every_lettering_style_is_set_in_one_of_the_designs_typefaces()
    {
        Dictionary<LetteringStyle, Typeface> typefaces = Enum.GetValues<LetteringStyle>().ToDictionary(style => style, Typefaces.Of);

        Assert.All(typefaces.Values, typeface => Assert.True(Enum.IsDefined(typeface)));
        Assert.Equal(Typeface.PaytoneOne, typefaces[LetteringStyle.Figure]);
        Assert.Equal(Typeface.PaytoneOne, typefaces[LetteringStyle.OnButton]);
        Assert.Equal(Typeface.GabaritoExtraBold, typefaces[LetteringStyle.Label]);
        Assert.Equal(Typeface.GabaritoBold, typefaces[LetteringStyle.Soft]);
        Assert.Equal(Typeface.YoungSerif, typefaces[LetteringStyle.Title]);
    }

    [Fact]
    public void A_style_the_list_does_not_know_has_no_typeface()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Typefaces.Of((LetteringStyle)999));
    }

    [Fact]
    public void Only_white_lettering_on_colour_is_drawn_in_an_outline_and_in_none_wider_than_a_typefaces_picture_reaches()
    {
        LetteringStyle[] outlined = Enum.GetValues<LetteringStyle>().Where(style => LetteringOutlines.WidthOf(style) > 0f).ToArray();

        Assert.Equal(new[] { LetteringStyle.OnButton, LetteringStyle.OnTeal, LetteringStyle.OnBlue, LetteringStyle.OnHeart }, outlined);
        Assert.All(outlined, style => Assert.Equal(Typeface.PaytoneOne, Typefaces.Of(style)));
        Assert.All(outlined, style => Assert.InRange(LetteringOutlines.WidthOf(style), 0.07f, 0.125f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LetteringOutlines.WidthOf((LetteringStyle)999));
    }

    [Fact]
    public void The_letter_set_holds_plain_Latin_Turkish_and_the_letters_of_painters_names_each_once()
    {
        Assert.Equal(LetterSet.All.Length, LetterSet.All.Distinct().Count());
        Assert.Equal(string.Empty, LetterSet.LettersOutside("ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ abcçdefgğhıijklmnoöprsştuüvyz 0123456789"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("Bölüm 51'de açılır · “7 / 10” – 3×… kâğıt, hâlâ, sükûnet"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("PAUL CÉZANNE, Albrecht Dürer, Diego Velázquez, Eugène Delacroix, Jean-Léon Gérôme, Joan Miró, Edvard Munch"));
        Assert.DoesNotContain('÷', LetterSet.All);
    }

    [Fact]
    public void Letters_outside_the_set_are_named_each_once_in_the_order_they_come()
    {
        Assert.Equal("₺→", LetterSet.LettersOutside("900 ₺ → 1.250 ₺"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("Parçayı\nyerleştir"));
    }

    /// <summary>
    /// The words are read where they are written: every piece of text between quotation marks in the files that
    /// hold what the game says. That covers the sentences no test happens to build, too.
    /// </summary>
    [Theory]
    [InlineData("Flow", "MuseumWords.cs")]
    [InlineData("Flow", "LevelResult.cs")]
    [InlineData("Flow", "AttemptWords.cs")]
    [InlineData("Flow", "WalletWords.cs")]
    [InlineData("Flow", "NumberWords.cs")]
    [InlineData("Flow", "LevelStartWords.cs")]
    [InlineData("Flow", "BoardWords.cs")]
    [InlineData("Gallery", "Hud.cs")]
    public void Every_word_the_game_says_can_be_written_with_the_letter_set(string folder, string file)
    {
        string[] quoted = TextBetweenQuotationMarksIn(RepoFiles.ClientScriptPath(folder, file));

        Assert.NotEmpty(quoted);
        Assert.All(quoted, words => Assert.Equal(string.Empty, LetterSet.LettersOutside(words)));
    }

    [Fact]
    public void Every_word_on_the_sheets_of_the_shared_controls_can_be_written_with_the_letter_set()
    {
        // The Editor's specimen of the controls says the design's sample words, which no words file of the game holds yet.
        string[] quoted = TextBetweenQuotationMarksIn(RepoFiles.ClientEditorScriptPath("ControlsSpecimen.cs"));

        Assert.Contains("Altın yetmiyor", quoted);
        Assert.All(quoted, words => Assert.Equal(string.Empty, LetterSet.LettersOutside(words)));
    }

    /// <summary>
    /// A font file is kept with the bytes its record names: as the font service handed it out, or, where the record
    /// states a change, as tools/unity/fetch-fonts.js made it from that. A file that was replaced by hand, or
    /// fetched anew without its change, fails here by name.
    /// </summary>
    [Fact]
    public void Every_font_file_is_kept_with_the_bytes_its_record_names()
    {
        FontRecord[] fonts = JsonSerializer.Deserialize<FontRecords>(File.ReadAllText(RepoFiles.FontRecordPath), AsTheRecordIsWritten)!.Fonts;

        Assert.Equal(new[] { "Gabarito-Bold.ttf", "Gabarito-ExtraBold.ttf", "PaytoneOne-Regular.ttf", "YoungSerif-Regular.ttf" }, fonts.Select(font => font.File));
        Assert.All(fonts, font =>
        {
            byte[] kept = File.ReadAllBytes(RepoFiles.FontFilePath(font.File));
            string expected = font.Sha256AsKept ?? font.Sha256;

            Assert.True(
                Convert.ToHexStringLower(SHA256.HashData(kept)) == expected,
                $"{font.File} is not the file its record names; run node tools/unity/fetch-fonts.js from the repository root.");
            Assert.Equal(font.Changed is null, font.Sha256AsKept is null);
        });
    }

    /// <summary>
    /// Young Serif's own figures are old-style ones, in which "10" reads as the Turkish letters "ıo"; the design
    /// asks for lining figures, and the game's copy of the font is the one whose figures are those. A font whose
    /// authors reserve its name may not be kept changed under that name, which is why it matters whose is changed.
    /// </summary>
    [Fact]
    public void The_serif_is_kept_with_its_lining_figures_and_no_font_with_a_reserved_name_is_kept_changed()
    {
        FontRecord[] fonts = JsonSerializer.Deserialize<FontRecords>(File.ReadAllText(RepoFiles.FontRecordPath), AsTheRecordIsWritten)!.Fonts;

        FontRecord serif = Assert.Single(fonts, font => font.Family == "Young Serif");
        Assert.Contains("lining", serif.Changed);
        Assert.All(fonts.Where(font => font.Changed is not null), font => Assert.Empty(font.ReservedFontNames));
        Assert.Equal(new[] { "Paytone", "Paytone One" }, Assert.Single(fonts, font => font.Family == "Paytone One").ReservedFontNames);
    }

    /// <summary>art/fonts/licences.json.</summary>
    private sealed record FontRecords(FontRecord[] Fonts);

    /// <summary>What the record says of one font file; <c>Changed</c> and <c>Sha256AsKept</c> are there for a file that is not kept as it was fetched.</summary>
    private sealed record FontRecord(string File, string Family, string Sha256, string[] ReservedFontNames, string? Changed = null, string? Sha256AsKept = null);

    private static string[] TextBetweenQuotationMarksIn(string path) =>
        TextBetweenQuotationMarks().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value.Replace("\\n", "\n")).ToArray();

    [Fact]
    public void Every_caption_of_the_catalogue_can_be_written_with_the_letter_set()
    {
        IEnumerable<string> names = Shipped.Value.Rooms.Select(MuseumWords.NameOf);
        IEnumerable<string> plates = RoomNumber.All.SelectMany(room => room.Paintings).SelectMany(painting => new[]
        {
            MuseumWords.TitleOf(painting, Shipped.Value.ArtworkAt(painting)),
            MuseumWords.ByLineOf(Shipped.Value.ArtworkAt(painting)),
        });

        Assert.All(names.Concat(plates), words => Assert.Equal(string.Empty, LetterSet.LettersOutside(words)));
    }

    [GeneratedRegex("\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"")]
    private static partial Regex TextBetweenQuotationMarks();
}
