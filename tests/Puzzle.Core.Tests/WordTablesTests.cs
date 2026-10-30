using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The tables of words, one a language (words/&lt;code&gt;.json), and the museum's words in the languages other than
/// the catalogue's own (museum/titles/&lt;code&gt;.json): what they hold, which of them the game may offer, how a
/// translation keeps its source's places, and how a table's words are filled and spoken. The Turkish table is the
/// source; the others are translations, marked machine-made until a native speaker has read them.
/// </summary>
public class WordTablesTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    /// <summary>The translations: every language of the first release but Turkish, the source.</summary>
    public static TheoryData<Language> Translations()
    {
        var translations = new TheoryData<Language>();
        foreach (Language language in Languages.All.Where(language => language != Language.Turkish))
        {
            translations.Add(language);
        }

        return translations;
    }

    /// <summary>Every language of the first release.</summary>
    public static TheoryData<Language> EveryLanguage()
    {
        var every = new TheoryData<Language>();
        foreach (Language language in Languages.All)
        {
            every.Add(language);
        }

        return every;
    }

    private static readonly Lazy<MuseumCatalogue> Shipped = new(() => CatalogueFileMapper.ToCatalogue(
        JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(RepoFiles.MuseumCataloguePath), AsUnityMapsIt)!));

    /// <summary>
    /// The places a translation leaves out of its source's, each with why: Turkish writes a level's number with the
    /// suffix its sound asks for ({levelIn}, the world map's {lastIn}), which no other language does; the Korean line about extra moves that
    /// were refused does not repeat their number.
    /// </summary>
    private static readonly HashSet<(WordId Id, string Place, Language Language)> PlacesLeftOut =
        Languages.All.Where(language => language != Language.Turkish)
            .SelectMany(language => new[] { (WordId.RoomOpensAt, "levelIn", language), (WordId.MapEraOpensAt, "levelIn", language), (WordId.MapStatusOpensAt, "lastIn", language) })
            .Append((WordId.ResultExtraMovesRefused, "moves", Language.Korean))
            .ToHashSet();

    /// <summary>The places a translation has that its source has not: the plain level's number, which Turkish fills as {levelIn}.</summary>
    private static readonly HashSet<(WordId Id, string Place)> PlacesAdded = new()
    {
        (WordId.RoomOpensAt, "level"),
        (WordId.MapEraOpensAt, "level"),
        (WordId.MapStatusOpensAt, "last"),
    };

    [Fact]
    public void Every_table_in_the_folder_of_words_is_a_table_of_the_language_its_file_is_named_for()
    {
        string[] files = Directory.GetFiles(RepoFiles.WordTablesFolder, "*.json");

        Assert.NotEmpty(files);
        Assert.All(files, file =>
        {
            WordTable table = WordTable.Of(JsonSerializer.Deserialize<WordFile>(File.ReadAllText(file), AsUnityMapsIt)!);
            Assert.Equal(Path.GetFileNameWithoutExtension(file), Languages.CodeOf(table.Language));
        });
    }

    [Fact]
    public void The_Turkish_table_is_the_source_with_a_note_for_its_translators_on_every_entry()
    {
        WordFile source = JsonSerializer.Deserialize<WordFile>(File.ReadAllText(RepoFiles.WordTablePath("tr")), AsUnityMapsIt)!;

        Assert.Equal(TableReview.Source, WordTable.Of(source).Review);
        Assert.All(source.words, entry => Assert.False(string.IsNullOrWhiteSpace(entry.note), $"{entry.id} has no note"));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Every_translation_is_marked_machine_made_until_a_native_speaker_has_read_it(Language language)
    {
        Assert.Equal(TableReview.MachineMade, TestWords.WordsOf(language).Review);
        Assert.Equal(TableReview.MachineMade, TestWords.CaptionsOf(language).Review);
    }

    [Theory]
    [MemberData(nameof(EveryLanguage))]
    public void The_words_of_every_language_are_complete_for_every_id_and_for_the_shipped_museum(Language language)
    {
        Wording wording = TestWords.Of(language);

        Assert.Empty(wording.Words.Missing);
        Assert.Empty(wording.Captions.MissingFrom(Shipped.Value));
        Assert.True(wording.IsCompleteFor(Shipped.Value));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void A_translation_keeps_every_place_of_its_source_and_adds_none_but_the_ones_its_language_needs(Language language)
    {
        WordTable source = TestWords.WordsOf(Language.Turkish);
        WordTable translation = TestWords.WordsOf(language);

        Assert.All(WordTable.Ids, id =>
        {
            var sourcePlaces = source.AllWords.Where(entry => entry.Id == id).SelectMany(entry => Template.PlacesIn(entry.Words)).ToHashSet();
            Assert.All(translation.AllWords.Where(entry => entry.Id == id), form =>
            {
                IReadOnlyList<string> places = Template.PlacesIn(form.Words);
                Assert.All(sourcePlaces.Where(place => !PlacesLeftOut.Contains((id, place, language))), place =>
                    Assert.True(places.Contains(place), $"{Languages.CodeOf(language)} {id}: \"{form.Words}\" leaves out {{{place}}}"));
                Assert.All(places, place =>
                    Assert.True(sourcePlaces.Contains(place) || PlacesAdded.Contains((id, place)), $"{Languages.CodeOf(language)} {id}: \"{form.Words}\" has {{{place}}}, which its source does not fill"));
            });
        });
    }

    [Fact]
    public void Every_language_names_itself_in_its_own_words()
    {
        Assert.Equal(
            new[]
            {
                "Türkçe", "English", "한국어", "日本語", "Deutsch", "Français", "Español", "Português (Brasil)", "Italiano", "Русский", "简体中文", "繁體中文",
                "Bahasa Indonesia",
            },
            Languages.All.Select(language => SettingsWords.OwnNameOf(TestWords.Of(language))));
    }

    /// <summary>
    /// A counted word gives a form for every count its language tells apart (<see cref="Languages.FormOf"/>): Russian
    /// "1 ход", "2 хода", "5 ходов"; German, French, Spanish, Brazilian Portuguese and Italian one form for one and
    /// another for more. A language that counts every number alike (Japanese, both Chinese, Korean, Indonesian) gives
    /// one text, as its source's single text does. A form the language never uses is no form of the table; "other"
    /// is every table's, the form a count without one of its own takes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Translations))]
    public void A_counted_word_has_a_form_for_every_count_its_language_tells_apart_and_none_it_never_uses(Language language)
    {
        WordFile file = JsonSerializer.Deserialize<WordFile>(File.ReadAllText(RepoFiles.WordTablePath(Languages.CodeOf(language))), AsUnityMapsIt)!;
        HashSet<PluralForm> used = Enumerable.Range(0, 200).Select(count => Languages.FormOf(language, count)).ToHashSet();
        bool countsAlike = used.Count == 1;

        Assert.All(file.words.Where(entry => string.IsNullOrEmpty(entry.text)), entry =>
        {
            Assert.False(countsAlike, $"{Languages.CodeOf(language)} counts every number alike, and {entry.id} gives forms");
            Assert.All(used, form => Assert.False(string.IsNullOrEmpty(FormIn(entry, form)), $"{Languages.CodeOf(language)} {entry.id} has no {form} form"));
            Assert.All(Enum.GetValues<PluralForm>().Except(used).Except(new[] { PluralForm.Other }), form =>
                Assert.True(string.IsNullOrEmpty(FormIn(entry, form)), $"{Languages.CodeOf(language)} {entry.id} gives a {form} form, which the language never uses"));
        });
        Assert.Equal(countsAlike, file.words.All(entry => !string.IsNullOrEmpty(entry.text)));
    }

    [Theory]
    [InlineData(1, "+1 ход")]
    [InlineData(2, "+2 хода")]
    [InlineData(5, "+5 ходов")]
    [InlineData(11, "+11 ходов")]
    [InlineData(21, "+21 ход")]
    [InlineData(24, "+24 хода")]
    public void Russian_says_a_count_in_the_form_its_last_figures_ask_for(int moves, string said)
    {
        using IDisposable speaking = TestWords.Speak(Language.Russian);

        Assert.Equal(said, LevelStartWords.PlusMoves(moves));
    }

    [Fact]
    public void A_template_fills_each_place_by_its_name_and_lists_its_places_once_in_the_order_they_come()
    {
        Assert.Equal("Level 47 · in 12 moves", Template.Fill("Level {level} · in {moves} moves", new[] { ("moves", "12"), ("level", "47") }));
        Assert.Equal("No place at all", Template.Fill("No place at all", Array.Empty<(string, string)>()));
        Assert.Equal(new[] { "level", "moves" }, Template.PlacesIn("{level} {moves} {level}"));
    }

    [Theory]
    [InlineData("Bölüm {level}")]
    [InlineData("Bölüm {level")]
    public void A_template_with_a_place_nothing_fills_or_a_brace_left_open_is_refused(string words)
    {
        Assert.Throws<FormatException>(() => Template.Fill(words, new[] { ("moves", "12") }));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{level 2}")]
    [InlineData("{level")]
    public void A_place_without_a_name_of_letters_is_refused(string words)
    {
        Assert.Throws<FormatException>(() => Template.PlacesIn(words));
    }

    [Fact]
    public void A_table_refuses_an_id_the_game_never_says_one_given_twice_one_without_words_and_one_with_both_words_and_forms()
    {
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "NoSuchWord", text = "x" }));
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "3", text = "x" }));
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "Play", text = "x" }, new WordEntry { id = "Play", text = "y" }));
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "Play" }));
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "PlusMoves", one = "+{moves} move" }));
        Assert.Throws<WordTableException>(() => TableOf(new WordEntry { id = "PlusMoves", text = "+{moves}", other = "+{moves} moves" }));
        Assert.Throws<WordTableException>(() => WordTable.Of(new WordFile { language = "xx", review = "machine" }));
        Assert.Throws<WordTableException>(() => WordTable.Of(new WordFile { language = "en", review = "checked" }));
    }

    [Fact]
    public void A_table_that_lacks_words_is_not_complete_and_says_which_it_lacks()
    {
        WordTable partial = TableOf(new WordEntry { id = "Play", text = "Jouer" });

        Assert.False(partial.IsComplete);
        Assert.Equal(WordTable.Ids.Count - 1, partial.Missing.Count);
        Assert.DoesNotContain(WordId.Play, partial.Missing);
        Assert.Throws<KeyNotFoundException>(() => partial.WordsOf(WordId.Done, PluralForm.Other));
    }

    [Fact]
    public void A_counted_word_gives_the_form_a_count_takes_and_the_other_form_where_it_has_none_of_its_own()
    {
        WordTable table = TableOf(new WordEntry { id = "PlusMoves", one = "+{moves} move", other = "+{moves} moves" });

        Assert.True(table.IsCounted(WordId.PlusMoves));
        Assert.Equal("+{moves} move", table.WordsOf(WordId.PlusMoves, PluralForm.One));
        Assert.Equal("+{moves} moves", table.WordsOf(WordId.PlusMoves, PluralForm.Few));
        Assert.Equal("+{moves} moves", table.WordsOf(WordId.PlusMoves, PluralForm.Other));
    }

    [Fact]
    public void The_museums_words_of_a_language_refuse_a_Turkish_file_a_plate_without_its_three_lines_and_an_established_title_without_its_source()
    {
        PaintingCaption Plate(string titleFrom, string reference = "") =>
            new() { id = "monet-water-lilies-1906", title = "Water Lilies", artist = "Claude Monet", year = "1906", titleFrom = titleFrom, reference = reference };

        Assert.Throws<WordTableException>(() => CaptionTable.Of(new CaptionFile { language = "tr", review = "source" }));
        Assert.Throws<WordTableException>(() => CaptionTable.Of(new CaptionFile { language = "en", review = "machine", paintings = new[] { Plate("guessed") } }));
        Assert.Throws<WordTableException>(() => CaptionTable.Of(new CaptionFile { language = "en", review = "machine", paintings = new[] { Plate("established") } }));
        Assert.Throws<WordTableException>(() => CaptionTable.Of(new CaptionFile
        {
            language = "en", review = "machine", paintings = new[] { new PaintingCaption { id = "x", title = "Title", titleFrom = "original" } },
        }));
        Assert.Throws<WordTableException>(() => CaptionTable.Of(new CaptionFile
        {
            language = "en", review = "machine", rooms = new[] { new RoomCaption { number = 1, name = "One" }, new RoomCaption { number = 1, name = "Again" } },
        }));
        Assert.NotNull(CaptionTable.Of(new CaptionFile { language = "en", review = "machine", paintings = new[] { Plate("established", "wikidata:Q1") } }));
    }

    [Fact]
    public void The_English_titles_are_the_catalogues_original_ones_wherever_they_say_so()
    {
        CaptionTable english = TestWords.CaptionsOf(Language.English);
        Artwork[] works = Shipped.Value.Rooms.SelectMany(room => room.Artworks).ToArray();

        Assert.All(works.Where(work => english.SourceOf(work) == TitleSource.Original), work => Assert.Equal(work.Caption.OriginalTitle, english.TitleOf(work)));
        Assert.True(works.Count(work => english.SourceOf(work) == TitleSource.Original) >= 90);
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void A_painter_keeps_their_name_in_every_language_and_only_the_words_around_it_change(Language language)
    {
        CaptionTable captions = TestWords.CaptionsOf(language);
        Artwork[] works = Shipped.Value.Rooms.SelectMany(room => room.Artworks).ToArray();
        Artwork monet = works.First(work => work.Id.Value == "monet-water-lilies-1906");
        Artwork holbein = works.First(work => work.Id.Value == "holbein-edward-vi-as-a-child");

        Assert.Equal("Claude Monet", captions.ArtistOf(monet));
        Assert.Contains("Hans Holbein", captions.ArtistOf(holbein));
        Assert.All(works, work => Assert.DoesNotContain("atfen", captions.ArtistOf(work)));
    }

    [Fact]
    public void English_names_the_younger_Holbein_in_its_own_words()
    {
        Artwork holbein = Shipped.Value.Rooms.SelectMany(room => room.Artworks).First(work => work.Id.Value == "holbein-edward-vi-as-a-child");

        Assert.Equal("Hans Holbein the Younger", TestWords.CaptionsOf(Language.English).ArtistOf(holbein));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void A_year_with_words_is_said_in_the_language_and_one_without_stays_as_it_is(Language language)
    {
        CaptionTable captions = TestWords.CaptionsOf(language);
        Artwork[] works = Shipped.Value.Rooms.SelectMany(room => room.Artworks).ToArray();

        Assert.All(works, work =>
        {
            string year = captions.YearOf(work);
            Assert.DoesNotContain("y.", year);
            Assert.DoesNotContain("sonrası", year);
            Assert.DoesNotContain("muhtemelen", year);
            Assert.DoesNotContain("belki", year);
            if (work.Caption.Year.All(letter => char.IsDigit(letter) || letter == '–'))
            {
                Assert.Equal(work.Caption.Year, year);
            }
        });
    }

    /// <summary>A title is the one the painting is known by in the language, with where it was found, or a translation; only English keeps the catalogue's original ones.</summary>
    [Theory]
    [MemberData(nameof(Translations))]
    public void A_title_is_established_with_where_it_was_found_or_marked_as_a_translation(Language language)
    {
        CaptionTable captions = TestWords.CaptionsOf(language);
        Artwork[] works = Shipped.Value.Rooms.SelectMany(room => room.Artworks).ToArray();

        Assert.All(works, work => Assert.NotNull(captions.SourceOf(work)));
        Assert.True(language == Language.English || works.All(work => captions.SourceOf(work) != TitleSource.Original), $"{Languages.CodeOf(language)} keeps an original title");
    }

    [Fact]
    public void A_Korean_title_is_established_with_where_it_was_found_or_marked_as_a_translation()
    {
        CaptionTable korean = TestWords.CaptionsOf(Language.Korean);
        Artwork[] works = Shipped.Value.Rooms.SelectMany(room => room.Artworks).ToArray();
        Artwork wave = works.First(work => work.Id.Value == "hokusai-great-wave");

        Assert.All(works, work => Assert.NotNull(korean.SourceOf(work)));
        Assert.Equal(TitleSource.Established, korean.SourceOf(wave));
        Assert.Equal("가나가와 해변의 높은 파도 아래", korean.TitleOf(wave));
        Assert.DoesNotContain(works, work => korean.SourceOf(work) == TitleSource.Original);
    }

    [Fact]
    public void The_rooms_and_plates_are_said_in_the_language_the_game_speaks()
    {
        MuseumRoom first = Shipped.Value.Room(new RoomNumber(1));
        Artwork waterLilies = first.Artworks.First(work => work.Id.Value == "monet-water-lilies-1906");

        Assert.Equal(("Empresyonizm", "Nilüferler", "CLAUDE MONET · 1906"), SaidOf(first, waterLilies));
        using (TestWords.Speak(Language.English))
        {
            Assert.Equal(("Impressionism", "Water Lilies", "CLAUDE MONET · 1906"), SaidOf(first, waterLilies));
        }

        using (TestWords.Speak(Language.Korean))
        {
            Assert.Equal(("인상주의", "수련", "CLAUDE MONET · 1906"), SaidOf(first, waterLilies));
        }

        using (TestWords.Speak(Language.Japanese))
        {
            Assert.Equal(("印象派", "睡蓮", "CLAUDE MONET · 1906"), SaidOf(first, waterLilies));
        }

        using (TestWords.Speak(Language.German))
        {
            Assert.Equal(("Impressionismus", "Seerosen", "CLAUDE MONET · 1906"), SaidOf(first, waterLilies));
        }

        Assert.Equal("Empresyonizm", MuseumWords.NameOf(first));
    }

    /// <summary>A flow of work that began elsewhere (a test running beside this one) does not hear it: it carries no part of this one's context.</summary>
    [Fact]
    public void A_language_spoken_within_a_flow_of_work_is_heard_there_alone_and_ends_with_its_scope()
    {
        string inEnglish;
        string besideIt = string.Empty;
        using (TestWords.Speak(Language.English))
        {
            inEnglish = MuseumWords.ShopTab;
            var beside = new System.Threading.Thread(() => besideIt = MuseumWords.ShopTab);
            using (System.Threading.ExecutionContext.SuppressFlow())
            {
                beside.Start();
            }

            beside.Join();
        }

        Assert.Equal("Shop", inEnglish);
        Assert.Equal("Mağaza", besideIt);
        Assert.Equal("Mağaza", MuseumWords.ShopTab);
        Assert.Equal(Language.Turkish, Speech.Language);
    }

    [Fact]
    public void Two_tables_of_different_languages_are_not_one_wording()
    {
        Assert.Throws<ArgumentException>(() => new Wording(TestWords.WordsOf(Language.English), TestWords.CaptionsOf(Language.Korean)));
    }

    private static (string Room, string Title, string ByLine) SaidOf(MuseumRoom room, Artwork artwork) =>
        (MuseumWords.NameOf(room), MuseumWords.TitleOf(PaintingNumber.All.First(), artwork), MuseumWords.ByLineOf(artwork));

    private static WordTable TableOf(params WordEntry[] entries) => WordTable.Of(new WordFile { language = "fr", review = "machine", words = entries });

    private static string? FormIn(WordEntry entry, PluralForm form) => form switch
    {
        PluralForm.One => entry.one,
        PluralForm.Few => entry.few,
        PluralForm.Many => entry.many,
        PluralForm.Other => entry.other,
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "A form of a counted word the test does not know."),
    };
}
