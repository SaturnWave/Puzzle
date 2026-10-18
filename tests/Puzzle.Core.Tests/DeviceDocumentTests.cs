using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
using Puzzle.Core.Recording;

namespace Puzzle.Core.Tests;

/// <summary>
/// The phone's own file, device.json, in what it holds: a random id of 128 bits in 32 lowercase hexadecimal digits,
/// which the attempt records carry as the only identity, and since layout 2 the player's settings, which a file of
/// layout 1 reads with as a first start has them; since layout 3 the tester mode among them, which a file of an
/// earlier layout reads with off; since layout 4 the language the player chose, which a file of an earlier layout
/// reads without, so the game speaks the phone's. <c>DeviceDocument</c> is the Unity project's source file,
/// compiled into these tests; here System.Text.Json fills it, in the game Unity's reader does
/// (unity/Assets/Editor/AttemptLogCheck.cs checks the real file with that one).
/// </summary>
public class DeviceDocumentTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private const string AnId = "6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a";

    private static DeviceReading Read(string json) => DeviceDocument.Read(JsonSerializer.Deserialize<DeviceDocument>(json, AsUnityMapsIt));

    [Fact]
    public void A_new_id_is_128_random_bits_in_32_lowercase_hexadecimal_digits_and_a_device_id()
    {
        DeviceId[] ids = Enumerable.Range(0, 200).Select(_ => DeviceDocument.NewId()).ToArray();

        Assert.All(ids, id => Assert.Matches(new Regex("^[0-9a-f]{32}$"), id.Value));
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void The_document_keeps_the_id_and_the_settings_in_its_layout_and_reads_back_as_the_phones()
    {
        var id = new DeviceId(AnId);
        var settings = new DeviceSettings(sound: false, vibration: true);

        string written = JsonSerializer.Serialize(DeviceDocument.Of(id, settings), AsUnityMapsIt);

        Assert.Equal("{\"version\":4,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":false,\"vibration\":true,\"testerMode\":false,\"language\":\"\"}}", written);
        var read = Assert.IsType<DeviceReading.Sound>(Read(written));
        Assert.Equal((id, settings), (read.Device, read.Settings));
    }

    [Theory]
    [InlineData("{\"version\":1,\"id\":\"" + AnId + "\"}")]
    [InlineData("{\"version\":1,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":false,\"vibration\":false}}")]
    public void A_document_of_the_earlier_layout_keeps_its_id_and_reads_with_the_settings_of_a_first_start(string json)
    {
        var read = Assert.IsType<DeviceReading.Sound>(Read(json));

        Assert.Equal((new DeviceId(AnId), DeviceSettings.Default), (read.Device, read.Settings));
    }

    [Fact]
    public void A_document_of_this_layout_without_its_block_of_settings_reads_with_the_settings_of_a_first_start()
    {
        var read = Assert.IsType<DeviceReading.Sound>(Read("{\"version\":4,\"id\":\"" + AnId + "\",\"settings\":null}"));

        Assert.Equal(DeviceSettings.Default, read.Settings);
    }

    [Fact]
    public void Each_setting_turned_off_alone_reads_back_as_it_was_written()
    {
        foreach (DeviceSetting setting in Enum.GetValues<DeviceSetting>())
        {
            DeviceSettings turnedOff = DeviceSettings.Default.With(setting, isOn: false);

            string written = JsonSerializer.Serialize(DeviceDocument.Of(new DeviceId(AnId), turnedOff), AsUnityMapsIt);

            Assert.Equal(turnedOff, Assert.IsType<DeviceReading.Sound>(Read(written)).Settings);
        }
    }

    [Theory]
    [InlineData("{\"id\":\"" + AnId + "\"}")]
    [InlineData("{\"version\":0,\"id\":\"" + AnId + "\"}")]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"id\":null}")]
    [InlineData("{\"version\":1,\"id\":\"6F1C0E9A2B7D4C3E8A5F0B1D2C3E4F5A\"}")]
    [InlineData("{\"version\":1,\"id\":\"6f1c0e9a2b7d4c3e\"}")]
    [InlineData("{\"version\":1,\"id\":\"3f2b8c1e-9d4a-4f6b-8e2c-1a7d5b9c0e3f\"}")]
    [InlineData("{\"version\":1,\"id\":\"ada@example.com\"}")]
    public void A_document_without_a_layout_or_with_an_id_the_game_never_makes_is_damaged(string json)
    {
        Assert.IsType<DeviceReading.Damaged>(Read(json));
    }

    [Fact]
    public void No_document_at_all_is_damaged()
    {
        Assert.IsType<DeviceReading.Damaged>(DeviceDocument.Read(null));
    }

    [Fact]
    public void A_later_layout_is_told_apart_and_its_id_and_settings_are_used_as_far_as_they_read()
    {
        var withItsId = Assert.IsType<DeviceReading.OfALaterGame>(
            Read("{\"version\":5,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":false,\"vibration\":true,\"testerMode\":true,\"language\":\"ko\",\"music\":false}}"));
        var withoutOne = Assert.IsType<DeviceReading.OfALaterGame>(Read("{\"version\":5,\"id\":\"a later spelling\"}"));

        Assert.Equal((5, new DeviceId(AnId)), (withItsId.Version, withItsId.Device));
        Assert.Equal(new DeviceSettings(sound: false, vibration: true, testerMode: true, Language.Korean), withItsId.Settings);
        Assert.Null(withoutOne.Device);
    }

    [Theory]
    [InlineData(Language.English, "en")]
    [InlineData(Language.Korean, "ko")]
    [InlineData(Language.Turkish, "tr")]
    [InlineData(Language.PortugueseBrazil, "pt-BR")]
    [InlineData(Language.ChineseTraditional, "zh-Hant")]
    public void A_language_the_player_chose_is_kept_by_its_code_and_reads_back(Language chosen, string code)
    {
        DeviceSettings settings = DeviceSettings.Default.WithLanguage(chosen);

        string written = JsonSerializer.Serialize(DeviceDocument.Of(new DeviceId(AnId), settings), AsUnityMapsIt);

        Assert.Contains("\"language\":\"" + code + "\"", written);
        Assert.Equal(settings, Assert.IsType<DeviceReading.Sound>(Read(written)).Settings);
    }

    [Fact]
    public void A_document_of_an_earlier_layout_reads_with_no_language_chosen_whatever_a_hand_wrote_into_it()
    {
        var read = Assert.IsType<DeviceReading.Sound>(
            Read("{\"version\":3,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":true,\"vibration\":false,\"testerMode\":true,\"language\":\"ko\"}}"));

        Assert.Null(read.Settings.Language);
        Assert.Equal(new DeviceSettings(sound: true, vibration: false, testerMode: true), read.Settings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xx")]
    [InlineData("EN")]
    public void A_language_code_the_game_does_not_know_reads_as_none_chosen(string code)
    {
        var read = Assert.IsType<DeviceReading.Sound>(
            Read("{\"version\":4,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":true,\"vibration\":true,\"testerMode\":false,\"language\":\"" + code + "\"}}"));

        Assert.Equal(DeviceSettings.Default, read.Settings);
    }
}
