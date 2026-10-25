using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Meta.Settings;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the settings card and the cards it opens say about the phone and the build: the player's id as a tester reads
/// it out, the foot, the build's line from the build's marks, and the privacy card's count and question.
/// <c>SettingsWords</c> and <c>PrivacyWords</c> are the Unity project's source files, compiled into these tests; that
/// every word of them is in the typefaces' letter set is LetteringTests'.
/// </summary>
public class SettingsWordsTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    [Fact]
    public void The_players_id_is_the_first_eight_characters_of_the_phones_id_in_two_groups_of_capitals()
    {
        Assert.Equal("6F1C-0E9A", SettingsWords.PlayerId(new DeviceId("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a")));
    }

    [Fact]
    public void The_foot_says_the_players_id_and_the_games_version()
    {
        Assert.Equal("Oyuncu kimliği: 6F1C-0E9A · Sürüm 1.0", SettingsWords.Foot(new DeviceId("6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a"), "1.0"));
    }

    [Fact]
    public void The_build_line_says_what_the_builds_marks_say_about_ads_and_purchases_and_calls_only_the_stand_in_build_a_test_build()
    {
        Assert.Equal("Test sürümü · reklamlar canlandırma, satın alma kapalı", SettingsWords.BuildLine(AdSource.Simulated, RealMoneySales.NotAvailable));
        Assert.Equal("Bu sürüm · reklamlar canlandırma, satın alma mağazadan", SettingsWords.BuildLine(AdSource.Simulated, RealMoneySales.ThroughTheStore));
    }

    [Fact]
    public void The_build_line_of_the_pack_the_game_carries_which_sells_through_the_store_is_no_test_builds()
    {
        var file = JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), AsUnityMapsIt)!;
        EconomyRules rules = EconomyFileMapper.ToRules(file);

        Assert.Equal(RealMoneySales.ThroughTheStore, rules.Shop.RealMoney);
        Assert.DoesNotContain("Test", SettingsWords.BuildLine(rules.Ads, rules.Shop.RealMoney), StringComparison.Ordinal);
        Assert.EndsWith("satın alma mağazadan", SettingsWords.BuildLine(rules.Ads, rules.Shop.RealMoney), StringComparison.Ordinal);
    }

    /// <summary>
    /// Aptabase works out the player's country and region from the connection's IP address, and the stores' forms
    /// declare it (Play's Approximate location, the App Store's Coarse Location): the privacy card and the question
    /// before any note leaves the phone say so too, in every language.
    /// </summary>
    [Theory]
    [InlineData(Language.Turkish, "ülke", "bölge")]
    [InlineData(Language.English, "country", "region")]
    [InlineData(Language.German, "Land", "Region")]
    [InlineData(Language.Spanish, "país", "región")]
    [InlineData(Language.French, "pays", "région")]
    [InlineData(Language.Italian, "paese", "regione")]
    [InlineData(Language.PortugueseBrazil, "país", "região")]
    [InlineData(Language.Indonesian, "negara", "wilayah")]
    [InlineData(Language.Russian, "страну", "регион")]
    [InlineData(Language.Japanese, "国", "地域")]
    [InlineData(Language.Korean, "국가", "지역")]
    [InlineData(Language.ChineseSimplified, "国家", "地区")]
    [InlineData(Language.ChineseTraditional, "國家", "地區")]
    public void The_privacy_card_and_the_statistics_question_say_that_the_country_and_region_are_worked_out_from_the_connection(Language language, string country, string region)
    {
        using IDisposable speaking = TestWords.Speak(language);

        string privacy = string.Join(" ", PrivacyWords.Text);
        string question = string.Join(" ", ConsentWords.Text);

        Assert.All(new[] { privacy, question }, said => Assert.Contains(country, said, StringComparison.Ordinal));
        Assert.All(new[] { privacy, question }, said => Assert.Contains(region, said, StringComparison.Ordinal));
        Assert.Contains("IP", privacy, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_setting_has_a_name_on_its_row()
    {
        Assert.All(Enum.GetValues<DeviceSetting>(), setting => Assert.False(string.IsNullOrWhiteSpace(SettingsWords.NameOf(setting))));
    }

    [Fact]
    public void The_content_version_is_said_as_a_version()
    {
        Assert.Equal("sürüm 1", SettingsWords.ContentVersionOf(ContentVersion.First));
    }

    [Theory]
    [InlineData(0, "Deneme kayıtları: 0", "0 kayıt silinsin mi?")]
    [InlineData(37, "Deneme kayıtları: 37", "37 kayıt silinsin mi?")]
    [InlineData(1250, "Deneme kayıtları: 1.250", "1.250 kayıt silinsin mi?")]
    public void The_privacy_card_counts_the_records_as_Turkish_writes_numbers(int count, string kept, string question)
    {
        Assert.Equal((kept, question), (PrivacyWords.RecordsKept(count), PrivacyWords.Question(count)));
    }
}
