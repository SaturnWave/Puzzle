using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's lettering in what can be said of it without the engine that draws it: every style has a typeface,
/// and everything the game says, in every language its tables are written in, can be written with the letters its
/// typefaces are made with: the design's own for Latin, a fallback's for another script. A typeface holds a fixed
/// set of letters, so a word with a letter outside the set would be drawn with a gap in it; here it fails a test
/// instead, with the letter's name.
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
    /// The words are read where they are written: every entry of every table of words, and every room and plate of
    /// the museum's words in each language. That covers the sentences no test happens to build, too. Turkish and
    /// English are written with the design's own letters alone.
    /// </summary>
    [Theory]
    [InlineData(Language.Turkish)]
    [InlineData(Language.English)]
    public void Every_word_a_Latin_language_says_can_be_written_with_the_letter_set(Language language)
    {
        Wording wording = TestWords.Of(language);
        IEnumerable<string> words = wording.Words.AllWords.Select(entry => entry.Words).Concat(wording.Captions.AllWords);

        Assert.All(words, each => Assert.Equal(string.Empty, LetterSet.LettersOutside(each)));
    }

    /// <summary>Every file of the folder of words, finished or not: a table being translated is held to the letters too.</summary>
    [Fact]
    public void Every_word_of_every_table_is_drawn_by_the_letter_set_or_by_a_fallback_that_covers_its_script()
    {
        IEnumerable<string> words = TablesWords();

        Assert.All(words, each => Assert.Equal(string.Empty, FallbackTypefaces.LettersNoneDraws(each)));
    }

    /// <summary>Words too long for one line are broken where the longer line is the shortest, at a space, which the break takes away; a letter is one unit wide here.</summary>
    [Theory]
    [InlineData("The Age of Revolutions: America and Europe", "The Age of Revolutions:", "America and Europe")]
    [InlineData("Elizabeth Farren, Later Countess of Derby", "Elizabeth Farren,", "Later Countess of Derby")]
    [InlineData("혁명의 시대: 미국과 유럽", "혁명의 시대:", "미국과 유럽")]
    [InlineData("Nilüferler", "Nilüferler", "")]
    public void Words_too_long_for_one_line_are_broken_in_two_where_the_longer_line_is_the_shortest(string words, string first, string second)
    {
        Assert.Equal((first, second), BalancedLines.SplitInTwo(words, line => line.Length));
    }

    [Fact]
    public void Every_typeface_has_its_fallbacks_and_the_serif_a_serif_one()
    {
        Assert.All(Enum.GetValues<Typeface>(), typeface => Assert.NotEmpty(FallbackTypefaces.Behind(typeface)));
        Assert.Equal(new[] { FallbackTypeface.NotoSerifKR }, FallbackTypefaces.Behind(Typeface.YoungSerif));
        Assert.Equal(new[] { FallbackTypeface.NotoSansKR }, FallbackTypefaces.Behind(Typeface.PaytoneOne));
        Assert.Equal("국어한", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSansKR, new[] { "Türkçe", "한국어", "English" }));
        Assert.Equal("가각", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSansKR, new[] { "각 가", "가" }));
        Assert.Equal("日", FallbackTypefaces.LettersNoneDraws("日 한"));
    }

    /// <summary>
    /// A fallback's typeface is made with exactly the letters of its script that the tables use (the Editor's
    /// LetteringBuilder makes it anew when they change): one that lacks a letter would draw a gap, so a word added to
    /// a table fails here until ProjectSetup.Run has made its fallback anew.
    /// </summary>
    [Theory]
    [InlineData(FallbackTypeface.NotoSansKR, "NotoSansKR-ExtraBold")]
    [InlineData(FallbackTypeface.NotoSerifKR, "NotoSerifKR-Bold")]
    public void Each_fallback_typeface_holds_exactly_the_letters_of_its_script_that_the_tables_use(FallbackTypeface fallback, string fontFileName)
    {
        string asked = FallbackTypefaces.LettersFor(fallback, TablesWords());
        string held = string.Concat(LettersHeldBy(RepoFiles.TypefacePath(fontFileName)).Order());

        Assert.NotEmpty(asked);
        Assert.Equal(asked, held);
    }

    [Fact]
    public void Each_design_typeface_asks_its_fallbacks_for_the_letters_it_lacks()
    {
        foreach (string typeface in new[] { "Gabarito-Bold", "Gabarito-ExtraBold", "PaytoneOne-Regular", "YoungSerif-Regular" })
        {
            string asset = File.ReadAllText(RepoFiles.TypefacePath(typeface));
            Match table = FallbackTable().Match(asset);
            Assert.True(table.Success && table.Groups[1].Value.Contains("guid:", StringComparison.Ordinal), $"{typeface} names no fallback");
        }
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

        Assert.Equal(
            new[] { "Gabarito-Bold.ttf", "Gabarito-ExtraBold.ttf", "PaytoneOne-Regular.ttf", "YoungSerif-Regular.ttf", "NotoSansKR-ExtraBold.ttf", "NotoSerifKR-Bold.ttf" },
            fonts.Select(font => font.File));
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
        Assert.Equal(new[] { "Source" }, Assert.Single(fonts, font => font.Family == "Noto Sans KR").ReservedFontNames);
    }

    /// <summary>
    /// The fallbacks are kept whole, as the font service hands them out, each under the SIL Open Font License and
    /// recorded with the script it draws: a changed copy of Noto Sans KR could not keep its name ("Source" is reserved).
    /// </summary>
    [Fact]
    public void The_fallback_fonts_are_kept_whole_under_the_open_font_licence_with_the_script_they_draw()
    {
        FontRecord[] fonts = JsonSerializer.Deserialize<FontRecords>(File.ReadAllText(RepoFiles.FontRecordPath), AsTheRecordIsWritten)!.Fonts;
        FontRecord[] fallbacks = fonts.Where(font => font.Fallback is not null).ToArray();

        Assert.Equal(new[] { "Noto Sans KR", "Noto Serif KR" }, fallbacks.Select(font => font.Family));
        Assert.All(fallbacks, font =>
        {
            Assert.Null(font.Changed);
            Assert.Equal("SIL Open Font License, Version 1.1", font.Licence);
            Assert.StartsWith("Korean", font.Fallback);
            Assert.True(File.Exists(RepoFiles.FontFilePath(font.LicenceText!)), $"{font.LicenceText} is missing");
        });
    }

    /// <summary>art/fonts/licences.json.</summary>
    private sealed record FontRecords(FontRecord[] Fonts);

    /// <summary>What the record says of one font file; <c>Changed</c> and <c>Sha256AsKept</c> are there for a file that is not kept as it was fetched.</summary>
    private sealed record FontRecord(
        string File, string Family, string Sha256, string[] ReservedFontNames, string? Changed = null, string? Sha256AsKept = null,
        string? Fallback = null, string? Licence = null, string? LicenceText = null);

    private static string[] TextBetweenQuotationMarksIn(string path) =>
        TextBetweenQuotationMarks().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value.Replace("\\n", "\n")).ToArray();

    /// <summary>Every piece of words of every table the repository has, finished or not, and of the museum's words in every language.</summary>
    private static IEnumerable<string> TablesWords()
    {
        foreach (string file in Directory.GetFiles(RepoFiles.WordTablesFolder, "*.json"))
        {
            foreach ((WordId _, string words) in WordTable.Of(JsonSerializer.Deserialize<WordFile>(File.ReadAllText(file), AsUnityMapsIt)!).AllWords)
            {
                yield return words;
            }
        }

        foreach (string file in Directory.GetFiles(RepoFiles.CaptionTablesFolder, "*.json"))
        {
            foreach (string words in CaptionTable.Of(JsonSerializer.Deserialize<CaptionFile>(File.ReadAllText(file), AsUnityMapsIt)!).AllWords)
            {
                yield return words;
            }
        }
    }

    /// <summary>The letters a typeface the Editor made holds, as its asset lists them by their code.</summary>
    private static IEnumerable<char> LettersHeldBy(string typefacePath) =>
        HeldLetter().Matches(File.ReadAllText(typefacePath)).Select(match => (char)int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

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

    [GeneratedRegex(@"m_ElementType: 1\s+m_Unicode: (\d+)")]
    private static partial Regex HeldLetter();

    [GeneratedRegex(@"m_FallbackFontAssetTable:(.*?)\r?\n  m_\w", RegexOptions.Singleline)]
    private static partial Regex FallbackTable();
}
