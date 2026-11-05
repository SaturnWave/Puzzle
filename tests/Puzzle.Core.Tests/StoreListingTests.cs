using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// The Google Play store listing's words (release/store-listing/&lt;Play locale&gt;.json), one file for each of the
/// game's thirteen languages: within Play's limits (a title of 30 characters, a short description of 80, a full one of
/// 4,000, Play counting every character alike, full width or half), named as the game names itself in that language,
/// and clear of what Play's metadata policy refuses (emoji and symbols, words in capitals, price, ranking and
/// promotion claims, other games' names). English and Turkish are the source; the others are machine drafts until a
/// native speaker has read them, as the game's own tables of words are.
/// </summary>
public class StoreListingTests
{
    private const int TitleLimit = 30;
    private const int ShortDescriptionLimit = 80;
    private const int FullDescriptionLimit = 4000;

    /// <summary>The Play Console locale each language of the game is listed under.</summary>
    private static readonly IReadOnlyDictionary<Language, string> PlayLocales = new Dictionary<Language, string>
    {
        [Language.English] = "en-US",
        [Language.Turkish] = "tr-TR",
        [Language.Korean] = "ko-KR",
        [Language.Japanese] = "ja-JP",
        [Language.German] = "de-DE",
        [Language.French] = "fr-FR",
        [Language.Spanish] = "es-ES",
        [Language.PortugueseBrazil] = "pt-BR",
        [Language.Italian] = "it-IT",
        [Language.Russian] = "ru-RU",
        [Language.ChineseSimplified] = "zh-CN",
        [Language.ChineseTraditional] = "zh-TW",
        [Language.Indonesian] = "id",
    };

    /// <summary>The languages whose listing is written first, by hand; the others are translated from them.</summary>
    private static readonly HashSet<Language> SourceLanguages = new() { Language.English, Language.Turkish };

    /// <summary>
    /// Words of price, ranking and promotion that Play's metadata policy turns away ("free", "best", "#1", "discount",
    /// "download now"), in each language as a shop would write them: whole words where the script has spaces,
    /// otherwise as they stand.
    /// </summary>
    private static readonly Dictionary<Language, string[]> PromotionWords = new()
    {
        [Language.English] = new[] { "free", "best", "top", "number one", "discount", "sale", "download now", "install now" },
        [Language.Turkish] = new[] { "ücretsiz", "bedava", "en iyi", "indirim", "hemen indir" },
        [Language.Korean] = new[] { "무료", "최고", "할인", "1위" },
        [Language.Japanese] = new[] { "無料", "最高", "割引", "ナンバーワン", "1位" },
        [Language.German] = new[] { "kostenlos", "gratis", "beste", "rabatt", "jetzt herunterladen" },
        [Language.French] = new[] { "gratuit", "gratuite", "meilleur", "meilleure", "promo", "réduction" },
        [Language.Spanish] = new[] { "gratis", "gratuito", "mejor", "descuento" },
        [Language.PortugueseBrazil] = new[] { "grátis", "gratuito", "melhor", "desconto" },
        [Language.Italian] = new[] { "gratis", "gratuito", "migliore", "sconto" },
        [Language.Russian] = new[] { "бесплатно", "бесплатная", "бесплатный", "лучшая", "лучший", "скидка" },
        [Language.ChineseSimplified] = new[] { "免费", "最好", "第一", "折扣" },
        [Language.ChineseTraditional] = new[] { "免費", "最好", "第一", "折扣" },
        [Language.Indonesian] = new[] { "gratis", "terbaik", "diskon" },
    };

    /// <summary>Games of the same kind the listing must not name.</summary>
    private static readonly string[] OtherGames =
    {
        "Candy Crush", "Royal Match", "Gardenscapes", "Homescapes", "Toon Blast", "Toy Blast", "Project Makeover",
        "Matchington", "Township", "Fishdom", "Lily's Garden", "June's Journey",
    };

    /// <summary>A word of two letters or more in capitals; Roman numerals of a century ("XV") are left to the check.</summary>
    private static readonly Regex WordInCapitals = new(@"\b\p{Lu}{2,}\b", RegexOptions.CultureInvariant);

    private static readonly Regex RomanNumeral = new("^[IVXLCDM]+$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions AsWritten = new(JsonSerializerDefaults.Web);

    /// <summary>One language's listing as the file holds it; a field the file leaves out fails its reading.</summary>
    private sealed record Listing
    {
        public required string Locale { get; init; }

        /// <summary>The code of the game's table of words in the same language (words/&lt;code&gt;.json).</summary>
        public required string WordsLanguage { get; init; }

        /// <summary>"source" for a listing written first, "machine" for a translation nobody has read yet.</summary>
        public required string Review { get; init; }

        public required string ReviewNote { get; init; }

        public required string Title { get; init; }

        public required string ShortDescription { get; init; }

        public required string FullDescription { get; init; }

        public (string Field, string Text)[] Fields() => new[]
        {
            ("title", Title), ("shortDescription", ShortDescription), ("fullDescription", FullDescription),
        };
    }

    public static TheoryData<Language> EveryLanguage()
    {
        var every = new TheoryData<Language>();
        foreach (Language language in Languages.All)
        {
            every.Add(language);
        }

        return every;
    }

    private static Listing ListingOf(Language language) =>
        JsonSerializer.Deserialize<Listing>(File.ReadAllText(Path.Combine(RepoFiles.StoreListingFolder, $"{PlayLocales[language]}.json")), AsWritten)!;

    /// <summary>Characters as Play counts them: each one once, whatever its width, so a character beyond the 16-bit range is one too.</summary>
    private static int CharactersIn(string text) => text.EnumerateRunes().Count();

    /// <summary>
    /// True when the text, in small letters, says the word: as a whole word in a language that spaces its words, and
    /// anywhere in Japanese, Chinese and Korean, where a word runs on into the next or takes its particle.
    /// </summary>
    private static bool Says(string lowerText, string word, Language language)
    {
        bool wordsStandApart = language is not (Language.Japanese or Language.ChineseSimplified or Language.ChineseTraditional or Language.Korean);
        return wordsStandApart
            ? Regex.IsMatch(lowerText, $@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})", RegexOptions.CultureInvariant)
            : lowerText.Contains(word, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_language_of_the_game_has_a_listing_under_its_Play_locale_and_nothing_else_is_listed()
    {
        string[] files = Directory.GetFiles(RepoFiles.StoreListingFolder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToArray();

        Assert.Equal(PlayLocales.Values.OrderBy(locale => locale, StringComparer.Ordinal), files.OrderBy(file => file, StringComparer.Ordinal));
        Assert.Equal(Languages.All.Count, PlayLocales.Count);
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void Each_listing_fits_Plays_limits(Language language)
    {
        Listing listing = ListingOf(language);

        Assert.InRange(CharactersIn(listing.Title), 1, TitleLimit);
        Assert.InRange(CharactersIn(listing.ShortDescription), 1, ShortDescriptionLimit);
        Assert.InRange(CharactersIn(listing.FullDescription), 1, FullDescriptionLimit);
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void Each_listing_is_named_as_the_game_names_itself_in_its_language(Language language)
    {
        Listing listing = ListingOf(language);
        string code = Languages.CodeOf(language);
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(RepoFiles.WordTablePath(code)));
        string entryTitle = table.RootElement.GetProperty("words").EnumerateArray()
            .Single(word => word.GetProperty("id").GetString() == nameof(WordId.EntryTitle))
            .GetProperty("text").GetString()!;

        Assert.Equal(PlayLocales[language], listing.Locale);
        Assert.Equal(code, listing.WordsLanguage);
        Assert.Equal(entryTitle, listing.Title);
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void Each_listing_says_whether_a_native_speaker_has_read_it(Language language)
    {
        Listing listing = ListingOf(language);

        Assert.Equal(SourceLanguages.Contains(language) ? "source" : "machine", listing.Review);
        Assert.False(string.IsNullOrWhiteSpace(listing.ReviewNote), $"{listing.Locale} says nothing of its review");
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void Each_listing_keeps_clear_of_emoji_symbols_and_words_in_capitals(Language language)
    {
        Listing listing = ListingOf(language);

        foreach ((string field, string text) in listing.Fields())
        {
            foreach (Rune rune in text.EnumerateRunes())
            {
                bool isASymbol = Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol || rune.Value > 0xFFFF;
                bool isAControl = Rune.IsControl(rune) && rune.Value != '\n';
                Assert.False(isASymbol || isAControl, $"{listing.Locale} {field} holds U+{rune.Value:X4}");
            }

            string[] inCapitals = WordInCapitals.Matches(text).Select(match => match.Value).Where(word => !RomanNumeral.IsMatch(word)).ToArray();
            Assert.True(inCapitals.Length == 0, $"{listing.Locale} {field} writes in capitals: {string.Join(", ", inCapitals)}");
        }

        Assert.DoesNotContain("\n", listing.Title + listing.ShortDescription);
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void Each_listing_makes_no_price_ranking_or_promotion_claim_and_names_no_other_game(Language language)
    {
        Listing listing = ListingOf(language);
        CultureInfo culture = CultureInfo.GetCultureInfo(Languages.CodeOf(language));

        foreach ((string field, string text) in listing.Fields())
        {
            string lower = text.ToLower(culture);
            foreach (string word in PromotionWords[language])
            {
                Assert.False(Says(lower, word, language), $"{listing.Locale} {field} says \"{word}\"");
            }

            Assert.DoesNotMatch(@"[#№]\s?1|[$€£₺¥₩₽%]", text);
            foreach (string game in OtherGames)
            {
                Assert.DoesNotContain(game, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
