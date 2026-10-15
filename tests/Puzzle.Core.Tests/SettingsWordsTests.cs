using System.IO;
using System.Text.Json;
using Puzzle.Client.Flow;
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
    public void The_build_line_says_what_the_builds_marks_say_about_ads_and_purchases()
    {
        Assert.Equal("Test sürümü · reklamlar canlandırma, satın alma kapalı", SettingsWords.BuildLine(AdSource.Simulated, RealMoneySales.NotAvailable));
    }

    [Fact]
    public void The_build_line_of_the_pack_the_game_carries_is_the_test_builds()
    {
        var file = JsonSerializer.Deserialize<EconomyFile>(File.ReadAllText(RepoFiles.EconomyPackPath), AsUnityMapsIt)!;
        EconomyRules rules = EconomyFileMapper.ToRules(file);

        Assert.StartsWith("Test sürümü", SettingsWords.BuildLine(rules.Ads, rules.Shop.RealMoney));
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
