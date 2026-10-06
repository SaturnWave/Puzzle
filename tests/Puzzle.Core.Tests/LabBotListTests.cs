using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Puzzle.Core.Tests;

/// <summary>
/// The lab's bots over HTTP, on the real server: its list offers the skill ladder beside the named profiles, and a rung
/// can play a move or a simulation like any player model.
/// </summary>
public sealed class LabBotListTests : IDisposable
{
    private const string DeviceHeader = "X-Device-Id";
    private const string Device = "0123456789abcdef0123456789abcdef";
    private const string FirstLevel = PublicLevels.Set + "/level_0001";

    private readonly TempFolder _folder = new();

    public LabBotListTests()
    {
        PublicLevels.Write(Path.Combine(_folder.Path, "levels"), PublicLevels.Easy(1));
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task The_lab_lists_every_player_model_weakest_first_with_the_named_profiles_on_their_rungs()
    {
        await using PuzzleServer server = await PuzzleServer.StartLabAsync(_folder.Path);

        JsonElement bots = await server.NewBrowser().GetJsonAsync("/api/bots");

        Assert.Equal(
            new[]
            {
                "random", "skill-30", "skill-35", "skill-40", "skill-45", "novice", "skill-55", "skill-60", "skill-65",
                "casual", "skill-75", "skill-80", "average", "skill-90", "skill-95", "expert",
            },
            bots.EnumerateArray().Select(bot => bot.GetString()));
    }

    [Fact]
    public async Task A_rung_plays_a_move_in_the_lab_and_a_name_off_the_ladder_is_refused()
    {
        await using PuzzleServer server = await PuzzleServer.StartLabAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        using HttpResponseMessage created = await PostAsLabAsync(browser, "/api/sessions", new { levelKey = FirstLevel });
        string? session = (await TestBrowser.ReadJsonAsync(created, HttpStatusCode.OK)).GetProperty("sessionId").GetString();

        using HttpResponseMessage offTheLadder = await PostAsLabAsync(browser, $"/api/sessions/{session}/bot", new { bot = "skill-52" });
        using HttpResponseMessage byARung = await PostAsLabAsync(browser, $"/api/sessions/{session}/bot", new { bot = "skill-35" });

        Assert.Equal(HttpStatusCode.BadRequest, offTheLadder.StatusCode);
        Assert.True((await TestBrowser.ReadJsonAsync(byARung, HttpStatusCode.OK)).GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task A_rung_is_simulated_in_the_lab_with_the_run_cap_of_a_player_model()
    {
        await using PuzzleServer server = await PuzzleServer.StartLabAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();

        // A search bot is cut to 100 runs; a player model may play 150.
        using HttpResponseMessage byARung = await PostAsLabAsync(browser, "/api/simulate", new { levelKey = FirstLevel, bot = "skill-65", runs = 150 });
        using HttpResponseMessage offTheLadder = await PostAsLabAsync(browser, "/api/simulate", new { levelKey = FirstLevel, bot = "skill-105", runs = 1 });

        JsonElement simulation = await TestBrowser.ReadJsonAsync(byARung, HttpStatusCode.OK);
        Assert.Equal(("skill-65", 150), (simulation.GetProperty("bot").GetString(), simulation.GetProperty("runs").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, offTheLadder.StatusCode);
    }

    /// <summary>A POST the way the lab's client sends it: with its device header.</summary>
    private static Task<HttpResponseMessage> PostAsLabAsync(TestBrowser browser, string path, object body) =>
        browser.PostWithoutClientHeaderAsync(path, body, (DeviceHeader, Device));
}
