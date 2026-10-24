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
    public void Only_white_lettering_on_colour_and_the_entry_pages_gilt_title_are_drawn_in_an_outline_and_in_none_wider_than_a_typefaces_picture_reaches()
    {
        LetteringStyle[] outlined = Enum.GetValues<LetteringStyle>().Where(style => LetteringOutlines.WidthOf(style) > 0f).ToArray();
        LetteringStyle[] white = outlined.Where(style => style != LetteringStyle.GiltTitle).ToArray();

        Assert.Equal(
            new[] { LetteringStyle.OnButton, LetteringStyle.OnTeal, LetteringStyle.OnBlue, LetteringStyle.OnHeart, LetteringStyle.GiltTitle, LetteringStyle.OnCream },
            outlined);
        Assert.All(white, style => Assert.Equal(Typeface.PaytoneOne, Typefaces.Of(style)));
        Assert.All(white, style => Assert.InRange(LetteringOutlines.WidthOf(style), 0.07f, 0.125f));

        // The title's thinner line leaves room in the serif's picture for the hard shade under it.
        Assert.Equal(Typeface.YoungSerif, Typefaces.Of(LetteringStyle.GiltTitle));
        Assert.InRange(LetteringOutlines.WidthOf(LetteringStyle.GiltTitle), 0.04f, 0.07f);
        Assert.Throws<ArgumentOutOfRangeException>(() => LetteringOutlines.WidthOf((LetteringStyle)999));
    }

    [Fact]
    public void The_letter_set_holds_plain_Latin_Turkish_and_the_letters_of_painters_names_each_once()
    {
        Assert.Equal(LetterSet.All.Length, LetterSet.All.Distinct().Count());
        Assert.Equal(string.Empty, LetterSet.LettersOutside("ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ abcçdefgğhıijklmnoöprsştuüvyz 0123456789"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("Bölüm 51'de açılır · “7 / 10” – 3×… kâğıt, hâlâ, sükûnet"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("PAUL CÉZANNE, Albrecht Dürer, Diego Velázquez, Eugène Delacroix, Jean-Léon Gérôme, Joan Miró, Edvard Munch"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("¡Nivel superado! ¿Seguro? Les Deux Sœurs, ŒUVRES, «Moulin Rouge»"));
        Assert.Equal(string.Empty, LetterSet.LettersOutside("Niveau terminé\u00A0! 1\u00A0250"));
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
    /// the museum's words in each language. That covers the sentences no test happens to build, too. The Latin
    /// languages are written with the design's own letters alone.
    /// </summary>
    [Theory]
    [InlineData(Language.Turkish)]
    [InlineData(Language.English)]
    [InlineData(Language.German)]
    [InlineData(Language.French)]
    [InlineData(Language.Spanish)]
    [InlineData(Language.PortugueseBrazil)]
    [InlineData(Language.Italian)]
    [InlineData(Language.Indonesian)]
    public void Every_word_a_Latin_language_says_can_be_written_with_the_letter_set(Language language)
    {
        Wording wording = TestWords.Of(language);
        IEnumerable<string> words = wording.Words.AllWords.Select(entry => entry.Words).Concat(wording.Captions.AllWords).Concat(MapLabelsOf(language));

        Assert.All(words, each => Assert.Equal(string.Empty, LetterSet.LettersOutside(each)));
    }

    /// <summary>Every file of the folder of words, finished or not: a table being translated is held to the letters too.</summary>
    [Fact]
    public void Every_word_of_every_table_is_drawn_by_the_letter_set_or_by_a_fallback_of_its_language_that_covers_its_script()
    {
        Assert.All(TablesWordsByLanguage(), language =>
            Assert.All(language.Value, each => Assert.Equal(string.Empty, FallbackTypefaces.LettersNoneDraws(language.Key, each))));
    }

    /// <summary>Words too long for one line are broken where the longer line is the shortest, at a space, which the break takes away; a letter is one unit wide here.</summary>
    [Theory]
    [InlineData("The Age of Revolutions: America and Europe", "The Age of Revolutions:", "America and Europe")]
    [InlineData("Elizabeth Farren, Later Countess of Derby", "Elizabeth Farren,", "Later Countess of Derby")]
    [InlineData("혁명의 시대: 미국과 유럽", "혁명의 시대:", "미국과 유럽")]
    [InlineData("Nilüferler", "Nilüferler", "")]
    [InlineData("Les Deux Sœurs", "Les Deux", "Sœurs")]
    [InlineData("Niveau terminé\u00A0!", "Niveau", "terminé\u00A0!")]
    [InlineData("神奈川沖浪裏", "神奈川", "沖浪裏")]
    [InlineData("新登場：ヘラ！ボタンをタップ", "新登場：ヘラ！", "ボタンをタップ")]
    [InlineData("パレットシャッフル", "パレット", "シャッフル")]
    [InlineData("「赤い」部屋です", "「赤い」", "部屋です")]
    [InlineData("长按道具，可再次查看它的效果", "长按道具，可再", "次查看它的效果")]
    [InlineData("エリザベス・ファーレン、後のダービー伯爵夫人", "エリザベス・ファーレン、", "後のダービー伯爵夫人")]
    [InlineData("伊丽莎白·法伦，后来的德比伯爵夫人", "伊丽莎白·法伦，", "后来的德比伯爵夫人")]
    public void Words_too_long_for_one_line_are_broken_in_two_where_the_longer_line_is_the_shortest(string words, string first, string second)
    {
        Assert.Equal((first, second), BalancedLines.SplitInTwo(words, line => line.Length));
    }

    /// <summary>
    /// Japanese and Chinese break between any two letters, but never before a mark that closes or trails ("ー", "ッ",
    /// "！", "」") and never after one that opens ("「", "（"); Korean breaks only at its spaces, as every language
    /// with spaces does, and a no-break space is no place to break.
    /// </summary>
    [Theory]
    [InlineData('東', '京', true)]
    [InlineData('ヘ', 'ラ', true)]
    [InlineData('A', '日', true)]
    [InlineData('日', '5', true)]
    [InlineData('パ', 'ー', false)]
    [InlineData('シ', 'ャ', false)]
    [InlineData('ラ', '！', false)]
    [InlineData('い', '」', false)]
    [InlineData('「', '赤', false)]
    [InlineData('（', '父', false)]
    [InlineData('한', '국', false)]
    [InlineData('a', 'b', false)]
    [InlineData('é', '\u00A0', false)]
    public void Japanese_and_Chinese_break_between_their_letters_but_never_before_a_closing_mark_or_after_an_opening_one(char before, char after, bool mayBreak)
    {
        Assert.Equal(mayBreak, LineBreaks.MayBreakBetween(before, after));
    }

    /// <summary>The game breaks a line where TextMesh Pro would: its lists of marks are the ones of TextMesh Pro's settings, word for word.</summary>
    [Fact]
    public void The_marks_a_line_never_ends_on_or_begins_with_are_text_mesh_pros_own()
    {
        Assert.Equal(TextMeshProLineBreaking("Leading"), LineBreaks.NeverEndALine);
        Assert.Equal(TextMeshProLineBreaking("Following"), LineBreaks.NeverBeginALine);
    }

    [Fact]
    public void Every_typeface_has_its_fallbacks_and_the_serif_the_serif_ones()
    {
        FallbackTypeface[] serifs = { FallbackTypeface.NotoSerifKR, FallbackTypeface.NotoSerif, FallbackTypeface.NotoSerifJP, FallbackTypeface.NotoSerifSC, FallbackTypeface.NotoSerifTC };
        FallbackTypeface[] sans = { FallbackTypeface.NotoSansKR, FallbackTypeface.NotoSans, FallbackTypeface.NotoSansJP, FallbackTypeface.NotoSansSC, FallbackTypeface.NotoSansTC };

        Assert.Equal(serifs, FallbackTypefaces.Behind(Typeface.YoungSerif));
        Assert.All(new[] { Typeface.PaytoneOne, Typeface.GabaritoBold, Typeface.GabaritoExtraBold }, typeface => Assert.Equal(sans, FallbackTypefaces.Behind(typeface)));
        Assert.Equal("국어한", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSansKR, new[] { "Türkçe", "한국어", "English" }));
        Assert.Equal("가각", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSansKR, new[] { "각 가", "가" }));
        Assert.Equal("ヘランー場新登！：", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSansJP, new[] { "新登場：ヘラ！", "ーン" }));
        Assert.Equal("МРеийклнсу", FallbackTypefaces.LettersFor(FallbackTypeface.NotoSerif, new[] { "Русский «Мулен»" }));
        Assert.Equal("日", FallbackTypefaces.LettersNoneDraws(Language.Korean, "日 한"));
        Assert.Equal("한", FallbackTypefaces.LettersNoneDraws(Language.Japanese, "日 한"));
        Assert.Equal(string.Empty, FallbackTypefaces.LettersNoneDraws(Language.Russian, "«Мулен Руж» 1\u00A0250"));
    }

    /// <summary>
    /// The language the game speaks asks its own fallback first, so a Chinese character shared by Japanese and both
    /// Chinese takes the form of the language spoken: Japanese asks Noto Sans JP first, Simplified Chinese Noto Sans
    /// SC, Traditional Chinese Noto Sans TC, Korean Noto Sans KR; a language without a fallback of its own keeps the
    /// order of the enumeration.
    /// </summary>
    [Theory]
    [InlineData(Language.Japanese, FallbackTypeface.NotoSansJP, FallbackTypeface.NotoSerifJP)]
    [InlineData(Language.ChineseSimplified, FallbackTypeface.NotoSansSC, FallbackTypeface.NotoSerifSC)]
    [InlineData(Language.ChineseTraditional, FallbackTypeface.NotoSansTC, FallbackTypeface.NotoSerifTC)]
    [InlineData(Language.Korean, FallbackTypeface.NotoSansKR, FallbackTypeface.NotoSerifKR)]
    [InlineData(Language.Russian, FallbackTypeface.NotoSans, FallbackTypeface.NotoSerif)]
    [InlineData(Language.Turkish, FallbackTypeface.NotoSansKR, FallbackTypeface.NotoSerifKR)]
    public void The_language_spoken_asks_its_own_fallback_first(Language language, FallbackTypeface firstSans, FallbackTypeface firstSerif)
    {
        Assert.Equal(firstSans, FallbackTypefaces.Behind(Typeface.PaytoneOne, language)[0]);
        Assert.Equal(firstSerif, FallbackTypefaces.Behind(Typeface.YoungSerif, language)[0]);
        Assert.Equal(FallbackTypefaces.Behind(Typeface.PaytoneOne).Order(), FallbackTypefaces.Behind(Typeface.PaytoneOne, language).Order());
        Assert.Equal(FallbackTypefaces.Behind(Typeface.PaytoneOne), FallbackTypefaces.Behind(Typeface.PaytoneOne, Language.English));
    }

    /// <summary>
    /// A fallback's typeface is made with exactly the letters of its script that its language's tables use (the
    /// Editor's LetteringBuilder makes it anew when they change): one that lacks a letter would draw a gap, so a word
    /// added to a table fails here until ProjectSetup.Run has made its fallback anew.
    /// </summary>
    [Theory]
    [InlineData(FallbackTypeface.NotoSansKR, "NotoSansKR-ExtraBold")]
    [InlineData(FallbackTypeface.NotoSerifKR, "NotoSerifKR-Bold")]
    [InlineData(FallbackTypeface.NotoSans, "NotoSans-ExtraBold")]
    [InlineData(FallbackTypeface.NotoSerif, "NotoSerif-Bold")]
    [InlineData(FallbackTypeface.NotoSansJP, "NotoSansJP-Bold")]
    [InlineData(FallbackTypeface.NotoSerifJP, "NotoSerifJP-Bold")]
    [InlineData(FallbackTypeface.NotoSansSC, "NotoSansSC-Bold")]
    [InlineData(FallbackTypeface.NotoSerifSC, "NotoSerifSC-Bold")]
    [InlineData(FallbackTypeface.NotoSansTC, "NotoSansTC-Bold")]
    [InlineData(FallbackTypeface.NotoSerifTC, "NotoSerifTC-Bold")]
    public void Each_fallback_typeface_holds_exactly_the_letters_of_its_script_that_its_languages_tables_use(FallbackTypeface fallback, string fontFileName)
    {
        string asked = FallbackTypefaces.LettersFor(fallback, TablesWordsByLanguage()[FallbackTypefaces.LanguageOf(fallback)]);
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
            new[]
            {
                "Gabarito-Bold.ttf", "Gabarito-ExtraBold.ttf", "PaytoneOne-Regular.ttf", "YoungSerif-Regular.ttf", "NotoSansKR-ExtraBold.ttf", "NotoSerifKR-Bold.ttf",
                "NotoSans-ExtraBold.ttf", "NotoSerif-Bold.ttf", "NotoSansJP-Bold.otf", "NotoSerifJP-Bold.otf", "NotoSansSC-Bold.otf", "NotoSerifSC-Bold.otf",
                "NotoSansTC-Bold.otf", "NotoSerifTC-Bold.otf",
            },
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
    /// The fallbacks are kept whole, as their makers hand them out (Google's font service for the Korean pair, the
    /// Noto project's releases on GitHub for the others), each under the SIL Open Font License and recorded with the
    /// language it draws: a changed copy of Noto Sans KR could not keep its name ("Source" is reserved).
    /// </summary>
    [Fact]
    public void The_fallback_fonts_are_kept_whole_under_the_open_font_licence_with_the_language_they_draw()
    {
        FontRecord[] fonts = JsonSerializer.Deserialize<FontRecords>(File.ReadAllText(RepoFiles.FontRecordPath), AsTheRecordIsWritten)!.Fonts;
        FontRecord[] fallbacks = fonts.Where(font => font.Fallback is not null).ToArray();

        Assert.Equal(
            new[]
            {
                ("Noto Sans KR", "Korean"), ("Noto Serif KR", "Korean"), ("Noto Sans", "Russian"), ("Noto Serif", "Russian"), ("Noto Sans JP", "Japanese"),
                ("Noto Serif JP", "Japanese"), ("Noto Sans SC", "Simplified Chinese"), ("Noto Serif SC", "Simplified Chinese"),
                ("Noto Sans TC", "Traditional Chinese"), ("Noto Serif TC", "Traditional Chinese"),
            },
            fallbacks.Select(font => (font.Family, font.Fallback!.Split(new[] { ',', '(' })[0].Trim())));
        Assert.All(fallbacks, font =>
        {
            Assert.Null(font.Changed);
            Assert.Equal("SIL Open Font License, Version 1.1", font.Licence);
            Assert.True(File.Exists(RepoFiles.FontFilePath(font.LicenceText!)), $"{font.LicenceText} is missing");
        });
        Assert.All(fallbacks.Skip(2), font => Assert.StartsWith("https://github.com/notofonts/", font.FetchedFrom));
    }

    /// <summary>art/fonts/licences.json.</summary>
    private sealed record FontRecords(FontRecord[] Fonts);

    /// <summary>What the record says of one font file; <c>Changed</c> and <c>Sha256AsKept</c> are there for a file that is not kept as it was fetched.</summary>
    private sealed record FontRecord(
        string File, string Family, string Sha256, string[] ReservedFontNames, string? Changed = null, string? Sha256AsKept = null,
        string? Fallback = null, string? Licence = null, string? LicenceText = null, string? FetchedFrom = null);

    private static string[] TextBetweenQuotationMarksIn(string path) =>
        TextBetweenQuotationMarks().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value.Replace("\\n", "\n")).ToArray();

    /// <summary>The world map's labels of a language (museum/places/&lt;code&gt;.json): its places, notes and museums; none where it has no file.</summary>
    private static IEnumerable<string> MapLabelsOf(Language language)
    {
        string file = Path.Combine(RepoFiles.PlaceLabelsFolder, $"{Languages.CodeOf(language)}.json");
        if (!File.Exists(file))
        {
            return Array.Empty<string>();
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        var labels = new List<string>();
        foreach (string kind in new[] { "places", "notes", "museums" })
        {
            if (document.RootElement.TryGetProperty(kind, out JsonElement entries))
            {
                labels.AddRange(entries.EnumerateObject().Select(entry => entry.Value.GetString()!));
            }
        }

        return labels;
    }

    /// <summary>Every piece of words of every table the repository has, finished or not, of the museum's words and of the world map's labels in every language, by language.</summary>
    private static Dictionary<Language, List<string>> TablesWordsByLanguage()
    {
        var byLanguage = Languages.All.ToDictionary(language => language, _ => new List<string>());
        foreach (string file in Directory.GetFiles(RepoFiles.WordTablesFolder, "*.json"))
        {
            WordTable table = WordTable.Of(JsonSerializer.Deserialize<WordFile>(File.ReadAllText(file), AsUnityMapsIt)!);
            byLanguage[table.Language].AddRange(table.AllWords.Select(entry => entry.Words));
        }

        foreach (string file in Directory.GetFiles(RepoFiles.CaptionTablesFolder, "*.json"))
        {
            CaptionTable table = CaptionTable.Of(JsonSerializer.Deserialize<CaptionFile>(File.ReadAllText(file), AsUnityMapsIt)!);
            byLanguage[table.Language].AddRange(table.AllWords);
        }

        foreach (Language language in Languages.All)
        {
            byLanguage[language].AddRange(MapLabelsOf(language));
        }

        return byLanguage;
    }

    /// <summary>One of TextMesh Pro's two line-breaking files, as its settings read it: every letter but the mark a text file may begin with.</summary>
    private static string TextMeshProLineBreaking(string which) =>
        File.ReadAllText(RepoFiles.TextMeshProResourcePath($"LineBreaking {which} Characters.txt")).TrimStart('\uFEFF');

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
