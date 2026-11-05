using Puzzle.Client.Flow;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// The languages the game is made to speak, without their tables: the closed set of the first release, the code
/// each is filed by, the language a phone's setting means, which the game speaks, and how each counts and writes
/// a large number. <c>Languages</c> is the Unity project's source file, compiled into these tests.
/// </summary>
public class LanguagesTests
{
    [Fact]
    public void The_first_release_is_made_to_speak_thirteen_languages_each_with_a_code_of_its_own_that_reads_back()
    {
        Assert.Equal(13, Languages.All.Count);
        Assert.Equal(
            new[] { "tr", "en", "ko", "ja", "de", "fr", "es", "pt-BR", "it", "ru", "zh-Hans", "zh-Hant", "id" },
            Languages.All.Select(Languages.CodeOf));
        Assert.All(Languages.All, language => Assert.Equal(language, Languages.FromCode(Languages.CodeOf(language))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("EN")]
    [InlineData("pt")]
    [InlineData("zh")]
    [InlineData(null)]
    public void A_text_that_is_no_languages_code_names_no_language(string? code)
    {
        Assert.Null(Languages.FromCode(code));
    }

    [Theory]
    [InlineData("tr-TR", Language.Turkish)]
    [InlineData("en-US", Language.English)]
    [InlineData("en-GB", Language.English)]
    [InlineData("ko-KR", Language.Korean)]
    [InlineData("ja-JP", Language.Japanese)]
    [InlineData("de-AT", Language.German)]
    [InlineData("fr-CA", Language.French)]
    [InlineData("es-419", Language.Spanish)]
    [InlineData("pt-BR", Language.PortugueseBrazil)]
    [InlineData("pt-PT", Language.PortugueseBrazil)]
    [InlineData("pt_AO", Language.PortugueseBrazil)]
    [InlineData("it-IT", Language.Italian)]
    [InlineData("ru-RU", Language.Russian)]
    [InlineData("zh-CN", Language.ChineseSimplified)]
    [InlineData("zh-Hans-SG", Language.ChineseSimplified)]
    [InlineData("zh", Language.ChineseSimplified)]
    [InlineData("zh-TW", Language.ChineseTraditional)]
    [InlineData("zh-HK", Language.ChineseTraditional)]
    [InlineData("zh_MO", Language.ChineseTraditional)]
    [InlineData("zh-Hant-TW", Language.ChineseTraditional)]
    [InlineData("zh-Hant", Language.ChineseTraditional)]
    [InlineData("id-ID", Language.Indonesian)]
    [InlineData("in-ID", Language.Indonesian)]
    public void A_phone_set_to_a_language_and_region_gets_the_language_the_owner_decided(string tag, Language expected)
    {
        Assert.Equal(expected, Languages.FromPhone(tag));
    }

    [Theory]
    [InlineData("ar-EG")]
    [InlineData("th-TH")]
    [InlineData("nl-NL")]
    [InlineData("")]
    [InlineData(null)]
    public void A_phone_in_a_language_the_game_is_not_made_to_speak_gets_English(string? tag)
    {
        Assert.Equal(Language.English, Languages.FromPhone(tag));
    }

    [Fact]
    public void The_game_speaks_the_players_choice_or_else_the_phones_language_where_it_offers_them_and_English_otherwise()
    {
        Language[] offered = { Language.Turkish, Language.English, Language.Korean };

        Assert.Equal(Language.Korean, Languages.InEffect(Language.Korean, Language.Turkish, offered));
        Assert.Equal(Language.Turkish, Languages.InEffect(null, Language.Turkish, offered));
        Assert.Equal(Language.English, Languages.InEffect(null, Language.Japanese, offered));
        Assert.Equal(Language.Turkish, Languages.InEffect(Language.German, Language.Turkish, offered));
        Assert.Equal(Language.English, Languages.InEffect(Language.German, Language.Russian, offered));
    }

    [Theory]
    [InlineData(Language.English, 0, PluralForm.Other)]
    [InlineData(Language.English, 1, PluralForm.One)]
    [InlineData(Language.English, 2, PluralForm.Other)]
    [InlineData(Language.English, 21, PluralForm.Other)]
    [InlineData(Language.Turkish, 1, PluralForm.One)]
    [InlineData(Language.Turkish, 5, PluralForm.Other)]
    [InlineData(Language.Korean, 1, PluralForm.Other)]
    [InlineData(Language.Japanese, 1, PluralForm.Other)]
    [InlineData(Language.ChineseTraditional, 1, PluralForm.Other)]
    [InlineData(Language.Indonesian, 1, PluralForm.Other)]
    [InlineData(Language.French, 0, PluralForm.One)]
    [InlineData(Language.French, 1, PluralForm.One)]
    [InlineData(Language.French, 2, PluralForm.Other)]
    [InlineData(Language.PortugueseBrazil, 0, PluralForm.One)]
    [InlineData(Language.German, 1, PluralForm.One)]
    [InlineData(Language.Russian, 1, PluralForm.One)]
    [InlineData(Language.Russian, 21, PluralForm.One)]
    [InlineData(Language.Russian, 11, PluralForm.Many)]
    [InlineData(Language.Russian, 2, PluralForm.Few)]
    [InlineData(Language.Russian, 24, PluralForm.Few)]
    [InlineData(Language.Russian, 12, PluralForm.Many)]
    [InlineData(Language.Russian, 5, PluralForm.Many)]
    [InlineData(Language.Russian, 0, PluralForm.Many)]
    [InlineData(Language.Russian, 111, PluralForm.Many)]
    public void A_count_takes_the_form_its_languages_rules_give_it(Language language, long count, PluralForm form)
    {
        Assert.Equal(form, Languages.FormOf(language, count));
    }

    [Fact]
    public void Every_language_has_rules_for_its_counted_words_and_its_thousands_and_counts_nothing_under_none()
    {
        Assert.All(Languages.All, language =>
        {
            Languages.FormOf(language, 7);
            Languages.ThousandsMarkOf(language);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => Languages.FormOf(Language.English, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Languages.CodeOf((Language)99));
    }

    [Theory]
    [InlineData(Language.Turkish, "1.250")]
    [InlineData(Language.German, "1.250")]
    [InlineData(Language.English, "1,250")]
    [InlineData(Language.Korean, "1,250")]
    [InlineData(Language.ChineseSimplified, "1,250")]
    [InlineData(Language.French, "1 250")]
    [InlineData(Language.Russian, "1 250")]
    public void A_language_sets_the_thousands_of_an_amount_apart_with_its_own_mark(Language language, string written)
    {
        Assert.Equal(written, $"1{Languages.ThousandsMarkOf(language)}250");
    }

    [Fact]
    public void A_language_offered_is_named_in_its_own_words_and_the_offers_stand_in_the_order_of_the_languages()
    {
        IReadOnlyList<LanguageOffer> offers = LanguageOffer.Of(new[] { TestWords.Of(Language.Korean), TestWords.Of(Language.Turkish), TestWords.Of(Language.English) });

        Assert.Equal(new[] { "Türkçe", "English", "한국어" }, offers.Select(offer => offer.OwnName));
        Assert.Throws<ArgumentException>(() => new LanguageOffer(Language.English, string.Empty));
    }

    [Fact]
    public void Settings_with_a_language_chosen_keep_their_switches_and_differ_from_the_same_switches_without_it()
    {
        DeviceSettings quiet = DeviceSettings.Default.With(DeviceSetting.Sound, isOn: false);
        DeviceSettings quietInKorean = quiet.WithLanguage(Language.Korean);

        Assert.Equal(Language.Korean, quietInKorean.Language);
        Assert.False(quietInKorean.Sound);
        Assert.NotEqual(quiet, quietInKorean);
        Assert.Equal(quietInKorean, quiet.WithLanguage(Language.Korean));
        Assert.Equal(Language.Korean, quietInKorean.With(DeviceSetting.Vibration, isOn: false).Language);
        Assert.Null(DeviceSettings.Default.Language);
    }
}
