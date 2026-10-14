using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Flow;
using Puzzle.Core.Recording;

namespace Puzzle.Core.Tests;

/// <summary>
/// The phone's own file, device.json, in what it holds: a random id of 128 bits in 32 lowercase hexadecimal digits,
/// which the attempt records carry as the only identity. <c>DeviceDocument</c> is the Unity project's source file,
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
    public void The_document_keeps_the_id_in_its_layout_and_reads_back_as_the_phones()
    {
        var id = new DeviceId(AnId);

        string written = JsonSerializer.Serialize(DeviceDocument.Of(id), AsUnityMapsIt);

        Assert.Equal("{\"version\":1,\"id\":\"" + AnId + "\"}", written);
        Assert.Equal(id, Assert.IsType<DeviceReading.Sound>(Read(written)).Device);
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
    public void A_later_layout_is_told_apart_and_its_id_is_used_when_it_reads()
    {
        var withItsId = Assert.IsType<DeviceReading.OfALaterGame>(Read("{\"version\":2,\"id\":\"" + AnId + "\",\"settings\":{\"sound\":false}}"));
        var withoutOne = Assert.IsType<DeviceReading.OfALaterGame>(Read("{\"version\":2,\"id\":\"a later spelling\"}"));

        Assert.Equal((2, new DeviceId(AnId)), (withItsId.Version, withItsId.Device));
        Assert.Null(withoutOne.Device);
    }
}
