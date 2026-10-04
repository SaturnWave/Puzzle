using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Tooling.Recording;
using Puzzle.Web;
using Puzzle.Web.Security;

namespace Puzzle.Core.Tests;

/// <summary>
/// The public mode over HTTP, on the real server: what a player may reach, what only the owner may, how a sign-in is
/// kept and guarded, and that the lab mode is what it was for whoever reaches it directly, and closed to a tunnel.
/// </summary>
public sealed class PublicModeTests : IDisposable
{
    /// <summary>What marks the lab's page: its level picker, which no player may see.</summary>
    private const string LabMarker = "level-select";

    private const string DeviceHeader = "X-Device-Id";
    private const string Device = "0123456789abcdef0123456789abcdef";
    private const string FirstLevel = PublicLevels.Set + "/level_0001";

    /// <summary>Every request the lab's client makes, with the answer the owner gets to it: found, or no such session.</summary>
    private static readonly (string Method, string Path, string? Body, HttpStatusCode ForOwner)[] LabRequests =
    {
        ("GET", "/api/levels", null, HttpStatusCode.OK),
        ("GET", "/api/bots", null, HttpStatusCode.OK),
        ("POST", "/api/sessions", $"{{\"levelKey\":\"{FirstLevel}\"}}", HttpStatusCode.OK),
        ("GET", "/api/sessions/none", null, HttpStatusCode.NotFound),
        ("POST", "/api/sessions/none/moves", "{\"kind\":\"tap\",\"from\":{\"x\":0,\"y\":0}}", HttpStatusCode.NotFound),
        ("POST", "/api/sessions/none/bot", "{}", HttpStatusCode.NotFound),
        ("GET", "/api/sessions/none/hint", null, HttpStatusCode.NotFound),
        ("POST", "/api/sessions/none/solve", "{}", HttpStatusCode.NotFound),
        ("POST", "/api/simulate", $"{{\"levelKey\":\"{FirstLevel}\",\"runs\":1}}", HttpStatusCode.OK),
        ("GET", "/api/recordings?scope=all", null, HttpStatusCode.OK),
        ("POST", "/api/recordings/compare", "{\"scope\":\"all\",\"runs\":1}", HttpStatusCode.OK),
    };

    private readonly TempFolder _folder = new();

    public PublicModeTests()
    {
        PublicLevels.Write(Path.Combine(_folder.Path, "levels"), PublicLevels.Easy(1), PublicLevels.Easy(2), PublicLevels.Hopeless(3));
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task In_public_mode_every_lab_endpoint_is_refused_without_the_owner_cookie_even_to_a_signed_in_player()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser stranger = server.NewBrowser();
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");

        foreach ((string method, string path, string? body, _) in LabRequests)
        {
            foreach (TestBrowser browser in new[] { stranger, player })
            {
                using HttpResponseMessage response = await SendAsLabAsync(browser, method, path, body);
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{method} {path} answered {response.StatusCode}");
            }
        }
    }

    [Fact]
    public async Task In_public_mode_the_owner_cookie_opens_every_lab_endpoint()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);

        foreach ((string method, string path, string? body, HttpStatusCode forOwner) in LabRequests)
        {
            using HttpResponseMessage response = await SendAsLabAsync(owner, method, path, body);
            Assert.True(response.StatusCode == forOwner, $"{method} {path} answered {response.StatusCode}");
        }
    }

    [Fact]
    public async Task The_owner_plays_a_lab_session_with_every_tool_on_a_public_server()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();
        (string, string)[] asLab = [(DeviceHeader, Device)];

        JsonElement session = await owner.PostJsonAsync("/api/sessions", new { levelKey = FirstLevel }, asLab);
        string id = session.GetProperty("sessionId").GetString() ?? string.Empty;
        JsonElement hint = await owner.GetJsonAsync($"/api/sessions/{id}/hint", asLab);
        JsonElement botMove = await owner.PostJsonAsync($"/api/sessions/{id}/bot", new { bot = "expert" }, asLab);
        JsonElement solved = await owner.PostJsonAsync($"/api/sessions/{id}/solve", new { beam = 1 }, asLab);
        JsonElement reloaded = await owner.GetJsonAsync($"/api/sessions/{id}", asLab);

        Assert.Equal("swap", hint.GetProperty("kind").GetString());
        Assert.True(botMove.GetProperty("accepted").GetBoolean());
        Assert.True(solved.TryGetProperty("solved", out _));
        Assert.Equal("assisted", reloaded.GetProperty("recording").GetProperty("standing").GetString());
    }

    [Fact]
    public async Task Every_endpoint_of_a_public_server_is_on_the_players_short_list_or_asks_for_the_owner()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        RouteEndpoint[] endpoints = server.App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();

        string[] open = endpoints.Where(endpoint => PolicyOf(endpoint) is null).Select(Describe).Order(StringComparer.Ordinal).ToArray();
        string[] forPlayers = endpoints.Where(endpoint => PolicyOf(endpoint) == PuzzleAuth.PlayerPolicy).Select(Describe).Order(StringComparer.Ordinal).ToArray();
        string[] forTheOwner = endpoints.Where(endpoint => PolicyOf(endpoint) == PuzzleAuth.OwnerPolicy).Select(Describe).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            new[]
            {
                "GET /api/player/me", "GET /app.css", "GET /dom.js", "GET /game-view.js", "GET /lab/sign-in", "GET /lab/sign-in.js", "GET /tiles.svg",
                "POST /api/owner/sign-in", "POST /api/owner/sign-out", "POST /api/player/sign-in", "POST /api/player/sign-out",
            },
            open);
        Assert.Equal(new[] { "GET /api/player/game", "POST /api/player/game/attempts", "POST /api/player/game/moves" }, forPlayers);
        Assert.Equal(endpoints.Length, open.Length + forPlayers.Length + forTheOwner.Length);
        Assert.Contains("GET /lab/{file?}", forTheOwner);
        foreach ((string method, string path, _, _) in LabRequests)
        {
            string route = path.Split('?')[0].Replace("/none", "/{id}");
            Assert.Contains($"{method} {route}", forTheOwner);
        }
    }

    [Fact]
    public async Task The_root_of_a_public_server_is_the_player_page_and_the_labs_page_is_for_the_owner_under_lab()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser stranger = server.NewBrowser();
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();

        using HttpResponseMessage root = await stranger.GetAsync("/");
        using HttpResponseMessage stylesheet = await stranger.GetAsync("/app.css");
        using HttpResponseMessage labScriptAtRoot = await stranger.GetAsync("/app.js");
        using HttpResponseMessage labForStranger = await stranger.GetAsync("/lab/");
        using HttpResponseMessage labScriptForStranger = await stranger.GetAsync("/lab/app.js");
        using HttpResponseMessage keyForm = await stranger.GetAsync("/lab/sign-in");
        using HttpResponseMessage labWithoutSlash = await owner.GetAsync("/lab");
        using HttpResponseMessage lab = await owner.GetAsync("/lab/");
        using HttpResponseMessage labScript = await owner.GetAsync("/lab/app.js");
        using HttpResponseMessage nested = await owner.GetAsync("/lab/owner/sign-in.html");

        Assert.Equal((HttpStatusCode.OK, "text/html"), (root.StatusCode, root.Content.Headers.ContentType?.MediaType));
        Assert.DoesNotContain(LabMarker, await root.Content.ReadAsStringAsync());
        Assert.Equal((HttpStatusCode.OK, "text/css"), (stylesheet.StatusCode, stylesheet.Content.Headers.ContentType?.MediaType));
        Assert.Equal(HttpStatusCode.NotFound, labScriptAtRoot.StatusCode);
        Assert.Equal((HttpStatusCode.Redirect, "/lab/sign-in"), (labForStranger.StatusCode, labForStranger.Headers.Location?.OriginalString));
        Assert.Equal((HttpStatusCode.Redirect, "/lab/sign-in"), (labScriptForStranger.StatusCode, labScriptForStranger.Headers.Location?.OriginalString));
        Assert.Equal(HttpStatusCode.OK, keyForm.StatusCode);
        Assert.Contains("Yönetici anahtarı", await keyForm.Content.ReadAsStringAsync());
        Assert.Equal((HttpStatusCode.Redirect, "/lab/"), (labWithoutSlash.StatusCode, labWithoutSlash.Headers.Location?.OriginalString));
        Assert.Equal(HttpStatusCode.OK, lab.StatusCode);
        Assert.Contains(LabMarker, await lab.Content.ReadAsStringAsync());
        Assert.Equal("no-store", lab.Headers.CacheControl?.ToString());
        Assert.Equal("no-store", labScript.Headers.CacheControl?.ToString());
        Assert.Equal((HttpStatusCode.OK, "text/javascript"), (labScript.StatusCode, labScript.Content.Headers.ContentType?.MediaType));
        Assert.Equal(HttpStatusCode.NotFound, nested.StatusCode);
    }

    [Theory]
    [InlineData("/player.js", "text/javascript")]
    [InlineData("/player.css", "text/css")]
    [InlineData("/game-view.js", "text/javascript")]
    [InlineData("/dom.js", "text/javascript")]
    [InlineData("/tiles.svg", "image/svg+xml")]
    [InlineData("/lab/sign-in.js", "text/javascript")]
    public async Task What_the_public_pages_load_is_served_to_everyone_and_calls_nothing_of_the_lab(string path, string mediaType)
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);

        using HttpResponseMessage file = await server.NewBrowser().GetAsync(path);
        string content = await file.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode.OK, mediaType), (file.StatusCode, file.Content.Headers.ContentType?.MediaType));
        Assert.Equal("no-cache", file.Headers.CacheControl?.ToString());
        foreach (string labsOwn in new[] { "/api/sessions", "/api/levels", "/api/bots", "/api/simulate", "/api/recordings", "X-Device-Id", LabMarker })
        {
            Assert.DoesNotContain(labsOwn, content);
        }
    }

    [Fact]
    public async Task The_tile_art_has_a_symbol_for_every_gem_colour_and_every_other_tile_the_board_draws()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        using HttpResponseMessage answer = await server.NewBrowser().GetAsync("/tiles.svg");
        string sprite = await answer.Content.ReadAsStringAsync();

        // The board (game-view.js) names a gem's symbol after its colour; the rest are the names it draws the other tiles by.
        IEnumerable<string> gems = Enum.GetNames<GemColor>().Select(colour => $"gem-{colour.ToLowerInvariant()}");
        string[] others =
        {
            "sp-rocket-h", "sp-rocket-v", "sp-bomb", "sp-bigbomb", "sp-seeker", "sp-colorbomb",
            "sp-box-1", "sp-box-2", "sp-box-3", "item", "ov-chain-1", "ov-chain-2", "unknown",
        };

        foreach (string symbol in gems.Concat(others))
        {
            Assert.Contains($"<symbol id=\"{symbol}\"", sprite);
        }
    }

    [Fact]
    public async Task The_pages_of_a_public_server_may_run_only_its_own_files_and_carry_no_inline_script_or_style()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();

        foreach (string path in new[] { "/", "/lab/sign-in", "/lab/" })
        {
            using HttpResponseMessage page = await owner.GetAsync(path);
            string policy = Assert.Single(page.Headers.GetValues("Content-Security-Policy"));
            string html = await page.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            Assert.Contains("default-src 'self'", policy);
            Assert.Contains("frame-ancestors 'none'", policy);
            Assert.DoesNotContain("unsafe", policy);
            Assert.DoesNotMatch("<script(?![^>]*\\ssrc=)", html); // every script is a file of this server
            Assert.DoesNotContain("<style", html);
            Assert.DoesNotMatch("\\sstyle=", html);
            Assert.DoesNotMatch("\\son[a-z]+=", html);
            Assert.DoesNotMatch("(src|href)=\"(https?:)?//", html); // nothing is loaded from another site
        }
    }

    [Fact]
    public async Task Without_an_owner_key_the_lab_does_not_exist_and_the_player_page_still_does()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path, ("AdminKey", string.Empty));
        TestBrowser browser = server.NewBrowser();

        foreach (string path in new[] { "/lab", "/lab/", "/lab/sign-in", "/lab/app.js", "/api/levels", "/api/recordings?scope=all" })
        {
            using HttpResponseMessage response = await browser.GetAsync(path, (DeviceHeader, Device));
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"GET {path} answered {response.StatusCode}");
        }

        using HttpResponseMessage keyTried = await browser.SignInAsOwnerAsync();
        using HttpResponseMessage root = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, keyTried.StatusCode);
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.False((await browser.GetJsonAsync("/api/player/me")).GetProperty("signedIn").GetBoolean());
    }

    [Fact]
    public async Task The_owner_signs_in_with_the_key_alone_and_a_changed_key_ends_the_old_sign_in()
    {
        TestBrowser owner;
        await using (PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            owner = server.NewBrowser();
            using HttpResponseMessage wrong = await owner.SignInAsOwnerAsync("not-the-owner-key-at-all");
            using HttpResponseMessage refused = await owner.GetAsync("/api/levels");
            using HttpResponseMessage right = await owner.SignInAsOwnerAsync();
            string cookie = Assert.Single(owner.LastSetCookies).ToLowerInvariant();
            using HttpResponseMessage opened = await owner.GetAsync("/api/levels");

            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);
            Assert.StartsWith("puzzle.owner=", cookie);
            Assert.Contains("httponly", cookie);
            Assert.Contains("secure", cookie);
            Assert.Contains("samesite=lax", cookie);
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        }

        await using PuzzleServer rekeyed = await PuzzleServer.StartPublicAsync(_folder.Path, ("AdminKey", "another-owner-key-0123456789"));
        using HttpResponseMessage afterNewKey = await rekeyed.NewBrowserFrom(owner).GetAsync("/api/levels");
        Assert.Equal(HttpStatusCode.Unauthorized, afterNewKey.StatusCode);
    }

    [Fact]
    public async Task The_owner_signs_out_as_the_labs_page_does_and_the_lab_is_closed_to_that_browser_again()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();
        using HttpResponseMessage opened = await owner.GetAsync("/lab/");

        // The lab's page marks its requests with the device header, not the player page's client header.
        using HttpResponseMessage signedOut = await owner.PostWithoutClientHeaderAsync("/api/owner/sign-out", body: null, (DeviceHeader, Device));
        using HttpResponseMessage page = await owner.GetAsync("/lab/");
        using HttpResponseMessage api = await owner.GetAsync("/api/levels", (DeviceHeader, Device));

        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Equal((HttpStatusCode.Redirect, "/lab/sign-in"), (page.StatusCode, page.Headers.Location?.OriginalString));
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task Signing_in_gives_the_player_a_cookie_scripts_cannot_read_and_only_https_carries()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser browser = server.NewBrowser();
        JsonElement before = await browser.GetJsonAsync("/api/player/me");

        using HttpResponseMessage signedIn = await browser.SignInAsync("ayse");
        string setCookie = Assert.Single(browser.LastSetCookies);
        string cookie = setCookie.ToLowerInvariant();
        JsonElement body = await TestBrowser.ReadJsonAsync(signedIn, HttpStatusCode.OK);
        JsonElement after = await browser.GetJsonAsync("/api/player/me");

        Assert.False(before.GetProperty("signedIn").GetBoolean());
        Assert.False(before.TryGetProperty("username", out _));
        Assert.Equal("Ayse", body.GetProperty("username").GetString());
        Assert.StartsWith("puzzle.player=", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=lax", cookie);
        Assert.InRange(ExpiryOf(setCookie) - DateTimeOffset.UtcNow, TimeSpan.FromDays(29), TimeSpan.FromDays(31));
        Assert.True(after.GetProperty("signedIn").GetBoolean());
        Assert.Equal("Ayse", after.GetProperty("username").GetString());
        Assert.DoesNotContain(server.PlayerIdOf("Ayse").Value, cookie); // the cookie is sealed: not even the player id shows
    }

    [Fact]
    public async Task A_player_the_owner_added_while_the_server_was_stopped_signs_in_and_nobody_can_register()
    {
        PuzzleServer.AddPlayerTo(_folder.Path, "Ayse");
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser ayse = server.NewBrowser();

        using HttpResponseMessage signedIn = await ayse.SignInAsync("Ayse");
        JsonElement game = await ayse.GetJsonAsync("/api/player/game");
        using HttpResponseMessage register = await server.NewBrowser().PostAsync(
            "/api/player/register",
            new { username = "Mert", password = PuzzleServer.PlayerPassword, registrationCode = "elma-2026" });

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(1, game.GetProperty("progress").GetProperty("levelNumber").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, register.StatusCode);
        PlayerAccount only = Assert.Single(PlayerStore.ReadAccounts(server.DataFolder));
        Assert.Equal("Ayse", only.Username.Value);
        Assert.StartsWith($"pbkdf2-sha256${PasswordHasher.DefaultIterations}$", only.PasswordHash); // raised to the server's cost at the first sign-in
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_username_get_the_same_answer_over_http()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser browser = server.NewBrowser();

        using HttpResponseMessage wrongPassword = await browser.SignInAsync("Ayse", "not her password");
        using HttpResponseMessage unknownUser = await browser.SignInAsync("Nobody", "not her password");

        JsonElement refusal = await TestBrowser.ReadJsonAsync(wrongPassword, HttpStatusCode.Unauthorized);
        Assert.Equal("invalidCredentials", refusal.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(refusal.GetProperty("message").GetString())); // a sentence the page can show as it is
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownUser.Content.ReadAsStringAsync());
        Assert.Empty(browser.LastSetCookies);
        Assert.False((await browser.GetJsonAsync("/api/player/me")).GetProperty("signedIn").GetBoolean());
    }

    [Fact]
    public async Task Repeated_failures_lock_the_username_over_http_whatever_address_they_come_from()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");

        for (int failure = 0; failure < 5; failure++)
        {
            using HttpResponseMessage wrong = await server.NewBrowser($"203.0.113.{failure + 1}").SignInAsync("Ayse", "a guess");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using HttpResponseMessage locked = await server.NewBrowser("203.0.113.99").SignInAsync("Ayse");
        JsonElement refusal = await TestBrowser.ReadJsonAsync(locked, HttpStatusCode.TooManyRequests);

        Assert.Equal("lockedOut", refusal.GetProperty("error").GetString());
        Assert.InRange(refusal.GetProperty("retryAfterSeconds").GetInt32(), 1, 300);
        Assert.Equal(refusal.GetProperty("retryAfterSeconds").GetRawText(), Assert.Single(locked.Headers.GetValues("Retry-After")));
    }

    [Fact]
    public async Task One_address_gets_only_so_many_tries_at_a_secret_a_minute_and_another_address_is_not_held_up()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser hasty = server.NewBrowser("203.0.113.7");

        for (int attempt = 0; attempt < 10; attempt++)
        {
            // An empty password is refused before any hashing, so the test stays quick; each username is its own.
            using HttpResponseMessage tried = await hasty.SignInAsync($"guess-{attempt}", string.Empty);
            Assert.Equal(HttpStatusCode.Unauthorized, tried.StatusCode);
        }

        using HttpResponseMessage signInLimited = await hasty.SignInAsync("guess-10", string.Empty);
        using HttpResponseMessage ownerKeyLimited = await hasty.SignInAsOwnerAsync("a-guess-at-the-owner-key");
        using HttpResponseMessage elsewhere = await server.NewBrowser("203.0.113.8").SignInAsync("guess-10", string.Empty);
        using HttpResponseMessage otherRequests = await hasty.GetAsync("/api/player/me");

        Assert.Equal("tooManyRequests", await TestBrowser.ReadErrorAsync(signInLimited, HttpStatusCode.TooManyRequests));
        Assert.True(signInLimited.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.TooManyRequests, ownerKeyLimited.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherRequests.StatusCode);
    }

    [Fact]
    public async Task A_flood_of_requests_from_one_address_is_slowed_down_and_other_addresses_play_on()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser flooding = server.NewBrowser("203.0.113.50");

        for (int request = 0; request < 600; request++)
        {
            using HttpResponseMessage answered = await flooding.GetAsync("/api/player/me");
            Assert.True(answered.StatusCode == HttpStatusCode.OK, $"request {request + 1} answered {answered.StatusCode}");
        }

        using HttpResponseMessage api = await flooding.GetAsync("/api/player/me");
        using HttpResponseMessage page = await flooding.GetAsync("/");
        using HttpResponseMessage elsewhere = await server.NewBrowser("203.0.113.51").GetAsync("/api/player/me");

        Assert.Equal("tooManyRequests", await TestBrowser.ReadErrorAsync(api, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.TooManyRequests, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, elsewhere.StatusCode);
    }

    [Fact]
    public async Task Only_a_proxy_on_this_machine_is_believed_about_https()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);

        ForwardedHeadersOptions forwarded = server.App.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedProto, forwarded.ForwardedHeaders);
        Assert.Equal(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }, forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownIPNetworks);
    }

    [Theory]
    [InlineData("127.0.0.1", "203.0.113.7", "203.0.113.7")] // Tailscale: the one address it saw
    [InlineData("::1", "2001:db8::7", "2001:db8::7")]
    [InlineData("127.0.0.1", "198.51.100.1, 203.0.113.9", "203.0.113.9")] // Cloudflare: appended to what the client wrote
    [InlineData("127.0.0.1", null, "127.0.0.1")]
    [InlineData("127.0.0.1", "not an address", "127.0.0.1")]
    [InlineData("198.51.100.20", "203.0.113.9", "198.51.100.20")]
    public void The_proxys_word_for_the_clients_address_counts_only_when_the_proxy_is_on_this_machine(
        string peer, string? forwardedFor, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        Assert.Equal(IPAddress.Parse(expected), ClientIp.Of(context));
    }

    [Theory]
    [InlineData("CF-Connecting-IP")]
    [InlineData("X-Real-IP")]
    [InlineData("True-Client-IP")]
    public void A_header_the_client_could_have_written_itself_names_nobodys_address(string header)
    {
        // Tailscale passes these on as the client sent them; only X-Forwarded-For is the proxy's own word.
        var withTheProxysWord = new DefaultHttpContext();
        withTheProxysWord.Connection.RemoteIpAddress = IPAddress.Loopback;
        withTheProxysWord.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        withTheProxysWord.Request.Headers[header] = "198.51.100.99";
        var withoutIt = new DefaultHttpContext();
        withoutIt.Connection.RemoteIpAddress = IPAddress.Loopback;
        withoutIt.Request.Headers[header] = "198.51.100.99";

        Assert.Equal(IPAddress.Parse("203.0.113.7"), ClientIp.Of(withTheProxysWord));
        Assert.Equal(IPAddress.Loopback, ClientIp.Of(withoutIt));
    }

    [Fact]
    public void Addresses_of_one_ipv6_network_count_as_one_client()
    {
        var first = new DefaultHttpContext();
        var second = new DefaultHttpContext();
        var other = new DefaultHttpContext();
        first.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:1:2:aaaa::1");
        second.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:1:2:bbbb::2");
        other.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:1:3::1");

        Assert.Equal(ClientIp.PartitionOf(first), ClientIp.PartitionOf(second));
        Assert.NotEqual(ClientIp.PartitionOf(first), ClientIp.PartitionOf(other));
    }

    [Fact]
    public async Task A_change_without_the_client_header_is_refused_so_another_site_cannot_make_one()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");
        server.AddPlayer("Mert");
        TestBrowser foreign = server.NewBrowser();

        using HttpResponseMessage start = await player.PostWithoutClientHeaderAsync("/api/player/game/attempts");
        using HttpResponseMessage signOut = await player.PostWithoutClientHeaderAsync("/api/player/sign-out");
        using HttpResponseMessage signIn = await foreign.PostWithoutClientHeaderAsync(
            "/api/player/sign-in",
            new { username = "Mert", password = PuzzleServer.PlayerPassword });
        JsonElement game = await player.GetJsonAsync("/api/player/game");

        Assert.Equal("clientHeaderRequired", await TestBrowser.ReadErrorAsync(start, HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.BadRequest, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, signIn.StatusCode);
        Assert.Empty(foreign.LastSetCookies);
        Assert.False(game.TryGetProperty("attempt", out _)); // nothing was started, and the player is still signed in
        Assert.Equal(0, game.GetProperty("progress").GetProperty("attemptsOnLevel").GetInt32());
    }

    [Fact]
    public async Task The_game_is_for_signed_in_players_only()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser stranger = server.NewBrowser();

        using HttpResponseMessage game = await stranger.GetAsync("/api/player/game");
        using HttpResponseMessage start = await stranger.PostAsync("/api/player/game/attempts");
        using HttpResponseMessage move = await stranger.PostAsync("/api/player/game/moves", new { kind = "tap", from = new { x = 0, y = 0 } });

        Assert.Equal("signInRequired", await TestBrowser.ReadErrorAsync(game, HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, move.StatusCode);
    }

    [Fact]
    public async Task A_request_too_large_or_not_json_is_refused_without_a_trace_of_the_server()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");
        using HttpResponseMessage started = await player.PostAsync("/api/player/game/attempts");

        using HttpResponseMessage tooLarge = await server.NewBrowser().SignInAsync("Ayse", password: new string('x', 20_000));
        string attempt = (await TestBrowser.ReadJsonAsync(started, HttpStatusCode.OK)).GetProperty("attempt").GetProperty("id").GetString() ?? string.Empty;
        using HttpResponseMessage notJson = await player.PostAsync("/api/player/game/moves", "{\"kind\":");
        using HttpResponseMessage noSuchMove = await player.PostAsync("/api/player/game/moves", new { attempt, movesUsed = 0, kind = "jump", from = new { x = 0, y = 0 } });
        using HttpResponseMessage halfAMove = await player.PostAsync("/api/player/game/moves", new { attempt, movesUsed = 0, kind = "swap" });
        using HttpResponseMessage farApart = await player.PostAsync("/api/player/game/moves", new { attempt, movesUsed = 0, kind = "swap", from = new { x = 0, y = 0 }, to = new { x = 5, y = 5 } });
        using HttpResponseMessage onNoAttempt = await player.PostAsync("/api/player/game/moves", new { movesUsed = 0, kind = "tap", from = new { x = 0, y = 0 } });
        using HttpResponseMessage onNoBoard = await player.PostAsync("/api/player/game/moves", new { attempt, kind = "tap", from = new { x = 0, y = 0 } });
        using HttpResponseMessage onWhatIsNoId = await player.PostAsync("/api/player/game/moves", new { attempt = "level_0001", movesUsed = 0, kind = "tap", from = new { x = 0, y = 0 } });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.DoesNotContain("Exception", await notJson.Content.ReadAsStringAsync());
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(noSuchMove, HttpStatusCode.BadRequest));
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(halfAMove, HttpStatusCode.BadRequest));
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(farApart, HttpStatusCode.BadRequest));
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(onNoAttempt, HttpStatusCode.BadRequest));
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(onNoBoard, HttpStatusCode.BadRequest));
        Assert.Equal("invalidMove", await TestBrowser.ReadErrorAsync(onWhatIsNoId, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task A_player_plays_a_level_to_the_end_moves_on_signs_out_and_resumes_after_signing_in_again()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Zeynep_K");
        DeviceId playerId = server.PlayerIdOf("Zeynep_K");

        JsonElement started = await player.PostJsonAsync("/api/player/game/attempts");
        JsonElement? lastMove = null;
        await PublicLevels.PlayToTheEndAsync(server.Games, playerId, async move =>
        {
            using HttpResponseMessage played = await player.PlayAsync(move);
            lastMove = await TestBrowser.ReadJsonAsync(played, HttpStatusCode.OK);
        });
        JsonElement afterWin = await player.GetJsonAsync("/api/player/game");
        using HttpResponseMessage moveAfterTheEnd = await player.PlayAsync(PublicLevels.OnTheLiveAttempt(server.Games, playerId, Move.Tap(TestBoards.P(0, 0))));
        JsonElement next = await player.PostJsonAsync("/api/player/game/attempts");

        using HttpResponseMessage signedOut = await player.PostAsync("/api/player/sign-out");
        using HttpResponseMessage gameWhileSignedOut = await player.GetAsync("/api/player/game");
        using HttpResponseMessage signedIn = await player.SignInAsync("zeynep_k");
        JsonElement resumed = await player.GetJsonAsync("/api/player/game");

        Assert.Equal((1, 1, "Playing"), AttemptOf(started));
        Assert.NotNull(lastMove);
        Assert.True(lastMove.Value.GetProperty("accepted").GetBoolean());
        Assert.NotEqual(0, lastMove.Value.GetProperty("events").GetArrayLength());
        Assert.Equal("Won", lastMove.Value.GetProperty("state").GetProperty("status").GetString());
        Assert.Equal(2, lastMove.Value.GetProperty("progress").GetProperty("levelNumber").GetInt32());
        Assert.Equal((1, 1, "Won"), AttemptOf(afterWin));
        Assert.Equal("attemptOver", await TestBrowser.ReadErrorAsync(moveAfterTheEnd, HttpStatusCode.Conflict));
        Assert.Equal((2, 1, "Playing"), AttemptOf(next));
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, gameWhileSignedOut.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal((2, 1, "Playing"), AttemptOf(resumed));
        Assert.Equal(next.GetProperty("attempt").GetProperty("state").GetRawText(), resumed.GetProperty("attempt").GetProperty("state").GetRawText());

        // What the player's page is told never names a seed, a level file, a session or a player id.
        string told = started.GetRawText() + lastMove.Value.GetRawText() + afterWin.GetRawText();
        Assert.DoesNotContain("seed", told, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("level_0001", told);
        Assert.DoesNotContain("sessionId", told);
        Assert.DoesNotContain(playerId.Value, told);

        // The record is the player id's, and the owner alone sees whose that is.
        AttemptRecord record = Assert.Single(new AttemptLog(server.RecordingsPath).ReadAll().Records);
        Assert.Equal((playerId, "web-public", AttemptResult.Won), (record.Start.Device, record.Start.Client, record.Outcome.Result));
        Assert.DoesNotContain("zeynep", await File.ReadAllTextAsync(server.RecordingsPath), StringComparison.OrdinalIgnoreCase);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage ownerSignedIn = await owner.SignInAsOwnerAsync();
        JsonElement everyone = await owner.GetJsonAsync("/api/recordings?scope=all", (DeviceHeader, Device));
        JsonElement ownDevice = await owner.GetJsonAsync("/api/recordings?scope=device", (DeviceHeader, Device));
        JsonElement listed = Assert.Single(everyone.GetProperty("players").EnumerateArray().ToArray());
        Assert.Equal(("Zeynep_K", 1, 1, 1), (
            listed.GetProperty("username").GetString(),
            listed.GetProperty("levelsWon").GetInt32(),
            listed.GetProperty("records").GetInt32(),
            listed.GetProperty("play").GetProperty("wins").GetInt32()));
        Assert.False(ownDevice.TryGetProperty("players", out _));
    }

    [Fact]
    public async Task A_players_game_takes_no_room_from_the_labs_sessions_and_needs_none_of_it()
    {
        PuzzleServer.AddPlayerTo(_folder.Path, "Ayse");
        PuzzleServer.AddPlayerTo(_folder.Path, "Mert");
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path, ("MaxSessions", "1"));
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage ownerSignedIn = await owner.SignInAsOwnerAsync();
        JsonElement labSession = await owner.PostJsonAsync("/api/sessions", new { levelKey = FirstLevel }, (DeviceHeader, Device));

        foreach (string name in new[] { "Ayse", "Mert" })
        {
            TestBrowser player = server.NewBrowser();
            using HttpResponseMessage signedIn = await player.SignInAsync(name);
            Assert.Equal((1, 1, "Playing"), AttemptOf(await player.PostJsonAsync("/api/player/game/attempts")));
        }

        using HttpResponseMessage labSessionKept = await owner.GetAsync($"/api/sessions/{labSession.GetProperty("sessionId").GetString()}", (DeviceHeader, Device));
        Assert.Equal(HttpStatusCode.OK, labSessionKept.StatusCode);
    }

    [Fact]
    public async Task A_player_starts_a_game_even_when_the_lab_may_hold_no_session_at_all()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path, ("MaxSessions", "0"));
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");

        Assert.Equal((1, 1, "Playing"), AttemptOf(await player.PostJsonAsync("/api/player/game/attempts")));
    }

    [Fact]
    public async Task A_sign_in_outlives_a_restart_and_a_deleted_players_cookie_signs_nobody_in()
    {
        TestBrowser ayse;
        TestBrowser mert;
        await using (PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            ayse = await server.SignedInPlayerAsync("Ayse");
            mert = await server.SignedInPlayerAsync("Mert");

            // While the server runs it alone writes the store: the owner's tool has to wait for it to stop.
            Assert.Throws<PlayerStoreInUseException>(() => PlayerStore.Open(server.DataFolder));
        }

        using (PlayerStore store = PlayerStore.Open(PuzzleServer.DataFolderIn(_folder.Path)))
        {
            var log = new AttemptLog(PuzzleServer.RecordingsPathIn(_folder.Path));
            Assert.True(PlayerDeletion.TryDelete(store, log, new Username("Ayse"), out _));
        }

        await using PuzzleServer restarted = await PuzzleServer.StartPublicAsync(_folder.Path);
        JsonElement deleted = await restarted.NewBrowserFrom(ayse).GetJsonAsync("/api/player/me");
        using HttpResponseMessage deletedGame = await restarted.NewBrowserFrom(ayse).GetAsync("/api/player/game");
        JsonElement kept = await restarted.NewBrowserFrom(mert).GetJsonAsync("/api/player/me");
        JsonElement keptGame = await restarted.NewBrowserFrom(mert).GetJsonAsync("/api/player/game");

        Assert.False(deleted.GetProperty("signedIn").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, deletedGame.StatusCode);
        Assert.Equal("Mert", kept.GetProperty("username").GetString());
        Assert.Equal(1, keptGame.GetProperty("progress").GetProperty("levelNumber").GetInt32());
    }

    [Fact]
    public async Task A_public_server_tells_caches_and_frames_to_keep_out()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();

        using HttpResponseMessage api = await browser.GetAsync("/api/player/me");
        using HttpResponseMessage page = await browser.GetAsync("/");
        using HttpResponseMessage refusedLabFile = await browser.GetAsync("/lab/dom.js");
        using HttpResponseMessage nothingThere = await browser.GetAsync("/no-such-file.js");
        using HttpResponseMessage refusedChange = await browser.PostWithoutClientHeaderAsync("/api/player/sign-out");

        // A cache between the server and the browsers keeps scripts by their name alone: a redirect to the key form it
        // kept for /lab/dom.js would be handed to the signed-in owner too, and the lab would not start.
        Assert.Equal(HttpStatusCode.Redirect, refusedLabFile.StatusCode);
        Assert.Equal("no-store", refusedLabFile.Headers.CacheControl?.ToString());
        Assert.Equal((HttpStatusCode.NotFound, "no-store"), (nothingThere.StatusCode, nothingThere.Headers.CacheControl?.ToString()));
        Assert.Equal((HttpStatusCode.BadRequest, "no-store"), (refusedChange.StatusCode, refusedChange.Headers.CacheControl?.ToString()));
        Assert.Equal("no-store", api.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", page.Headers.CacheControl?.ToString());
        Assert.Equal("DENY", Assert.Single(page.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("nosniff", Assert.Single(page.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task A_failure_inside_a_public_server_is_answered_without_any_of_its_details()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser owner = server.NewBrowser();
        using HttpResponseMessage signedIn = await owner.SignInAsOwnerAsync();
        JsonElement session = await owner.PostJsonAsync("/api/sessions", new { levelKey = FirstLevel }, (DeviceHeader, Device));

        // The lab's move endpoint breaks on a move without its cell: a failure nothing in the lab answers.
        using HttpResponseMessage failed = await owner.PostAsync($"/api/sessions/{session.GetProperty("sessionId").GetString()}/moves", new { kind = "tap" });
        string body = await failed.Content.ReadAsStringAsync();

        Assert.Equal("serverError", await TestBrowser.ReadErrorAsync(failed, HttpStatusCode.InternalServerError));
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("   at ", body);
        Assert.Equal("nosniff", Assert.Single(failed.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(failed.Headers.CacheControl is { NoStore: true }, $"{failed.Headers.CacheControl}");
    }

    [Fact]
    public async Task In_lab_mode_the_lab_is_as_open_as_it_was_and_nothing_of_the_public_mode_exists()
    {
        await using PuzzleServer server = await PuzzleServer.StartLabAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        (string, string)[] asLab = [(DeviceHeader, Device)];

        using HttpResponseMessage root = await browser.GetAsync("/");
        using HttpResponseMessage script = await browser.GetAsync("/app.js");
        JsonElement levels = await browser.GetJsonAsync("/api/levels");
        using HttpResponseMessage created = await SendAsLabAsync(browser, "POST", "/api/sessions", $"{{\"levelKey\":\"{FirstLevel}\"}}");
        JsonElement session = await TestBrowser.ReadJsonAsync(created, HttpStatusCode.OK);
        using HttpResponseMessage botMove = await SendAsLabAsync(browser, "POST", $"/api/sessions/{session.GetProperty("sessionId").GetString()}/bot", "{}");
        JsonElement recordings = await browser.GetJsonAsync("/api/recordings?scope=all", asLab);

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains(LabMarker, await root.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", root.Headers.CacheControl?.ToString());
        Assert.False(root.Headers.Contains("X-Frame-Options"));
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(3, levels.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, botMove.StatusCode);
        Assert.Empty(browser.LastSetCookies);
        Assert.False(recordings.TryGetProperty("players", out _));
        foreach ((string method, string path, string? body, HttpStatusCode forOwner) in LabRequests)
        {
            using HttpResponseMessage response = await SendAsLabAsync(browser, method, path, body);
            Assert.True(response.StatusCode == forOwner, $"{method} {path} answered {response.StatusCode}");
        }

        foreach (string path in new[] { "/api/player/me", "/api/player/game", "/lab/", "/lab/sign-in" })
        {
            using HttpResponseMessage response = await browser.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"GET {path} answered {response.StatusCode}");
        }

        using HttpResponseMessage playerSignIn = await browser.SignInAsync("Ayse");
        using HttpResponseMessage ownerSignIn = await browser.SignInAsOwnerAsync();
        Assert.True(playerSignIn.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{playerSignIn.StatusCode}");
        Assert.True(ownerSignIn.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{ownerSignIn.StatusCode}");
        Assert.False(Directory.Exists(server.DataFolder)); // the lab mode keeps no accounts and no keys
    }

    [Theory]
    [InlineData("CF-Connecting-IP", "203.0.113.7")]
    [InlineData("CF-Ray", "8f2a1b3c4d5e6f70-IST")]
    [InlineData("X-Forwarded-For", "203.0.113.7")]
    [InlineData("X-Forwarded-Proto", "https")]
    [InlineData("Forwarded", "for=203.0.113.7;proto=https")]
    [InlineData("Tailscale-Funnel-Request", "?1")] // Tailscale Funnel: a request from the internet
    [InlineData("Tailscale-User-Login", "ayse@example.com")] // Tailscale Serve: a request from a user of the tailnet
    [InlineData("Tailscale-User-Name", "Ayse")]
    [InlineData("Tailscale-User-Profile-Pic", "https://example.com/ayse.png")]
    [InlineData("Tailscale-Headers-Info", "https://tailscale.com/s/serve-headers")]
    [InlineData("Tailscale-App-Capabilities", "{}")]
    public async Task In_lab_mode_whatever_comes_through_a_tunnel_or_proxy_is_refused_and_told_the_setting_that_serves_players(string relayHeader, string value)
    {
        await using PuzzleServer server = await PuzzleServer.StartLabAsync(_folder.Path);
        TestBrowser throughTheTunnel = server.NewBrowser();
        (string, string) relayed = (relayHeader, value);

        using HttpResponseMessage page = await throughTheTunnel.GetAsync("/", relayed);
        string told = await page.Content.ReadAsStringAsync();
        Assert.Equal((HttpStatusCode.Forbidden, "text/plain"), (page.StatusCode, page.Content.Headers.ContentType?.MediaType));
        Assert.Equal("no-store", page.Headers.CacheControl?.ToString()); // or a cache on the way would go on refusing once the mode is set
        Assert.Contains("Puzzle__Mode=Public", told);
        Assert.Contains("tünel", told);
        Assert.Contains("tunnel", told);
        Assert.DoesNotContain(LabMarker, told);

        foreach (string file in new[] { "/index.html", "/app.js", "/app.css", "/game-view.js", "/tiles.svg" })
        {
            using HttpResponseMessage response = await throughTheTunnel.GetAsync(file, relayed);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"GET {file} answered {response.StatusCode}");
        }

        foreach ((string method, string path, string? body, _) in LabRequests)
        {
            using HttpResponseMessage response = await SendAsLabAsync(throughTheTunnel, method, path, body, relayed);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} answered {response.StatusCode}");
        }

        // The same server goes on serving whoever reaches it directly.
        using HttpResponseMessage direct = await server.NewBrowser().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Contains(LabMarker, await direct.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_lab_says_in_its_log_once_why_it_refuses_what_a_tunnel_passes_on()
    {
        var logger = new ListLogger<DirectOnly>();
        int passedOn = 0;
        var lab = new DirectOnly(CountAsync, logger);

        for (int request = 0; request < 3; request++)
        {
            var relayed = new DefaultHttpContext();
            relayed.Request.Headers["CF-Ray"] = "8f2a1b3c4d5e6f70-IST";
            await lab.InvokeAsync(relayed);
            Assert.Equal(StatusCodes.Status403Forbidden, relayed.Response.StatusCode);
        }

        await lab.InvokeAsync(new DefaultHttpContext()); // a direct request is passed on, and says nothing in the log

        Assert.Equal(1, passedOn);
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);

        Task CountAsync(HttpContext served)
        {
            passedOn++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void A_setting_the_public_mode_cannot_run_with_stops_the_start_and_says_why()
    {
        string folder = _folder.Path;
        Directory.CreateDirectory(Path.Combine(folder, "levels", "empty"));

        InvalidOperationException shortKey = Assert.Throws<InvalidOperationException>(() => PuzzleApp.Build(PuzzleServer.ArgsFor(folder, "Public", ("AdminKey", "too-short"))));
        InvalidOperationException noSuchMode = Assert.Throws<InvalidOperationException>(() => PuzzleApp.Build(PuzzleServer.ArgsFor(folder, "Open")));
        InvalidOperationException noLevels = Assert.Throws<InvalidOperationException>(() => PuzzleApp.Build(PuzzleServer.ArgsFor(folder, "Public", ("PublicLevels", "empty"))));

        Assert.Contains("Puzzle:AdminKey", shortKey.Message);
        Assert.DoesNotContain("too-short", shortKey.Message); // a message that ends up in a log must not carry the key
        Assert.Contains("Puzzle:Mode", noSuchMode.Message);
        Assert.Contains("Puzzle:PublicLevels", noLevels.Message);
    }

    [Fact]
    public async Task A_level_file_of_the_public_set_that_does_not_load_stops_a_public_server_at_the_start_and_only_a_public_one()
    {
        string broken = Path.Combine(_folder.Path, "levels", PublicLevels.Set, "level_0002.json");
        File.WriteAllText(broken, "{ \"id\": 2, cut short");

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => PuzzleApp.Build(PuzzleServer.ArgsFor(_folder.Path, "Public")));
        await using PuzzleServer lab = await PuzzleServer.StartLabAsync(_folder.Path); // the lab skips the file, as it always did
        JsonElement labLevels = await lab.NewBrowser().GetJsonAsync("/api/levels");

        Assert.Contains("level_0002.json", refused.Message);
        Assert.Contains("Puzzle:PublicLevels", refused.Message);
        Assert.Equal(2, labLevels.GetArrayLength());
    }

    [Fact]
    public async Task A_second_public_server_over_the_same_data_does_not_start()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);

        Assert.Throws<PlayerStoreInUseException>(() => PuzzleApp.Build(PuzzleServer.ArgsFor(_folder.Path, "Public")));
    }

    /// <summary>
    /// Sends a request the way the lab's client does: with its device header, and a JSON body on a POST.
    /// <paramref name="added"/> are the headers something on the way put on it.
    /// </summary>
    private static Task<HttpResponseMessage> SendAsLabAsync(TestBrowser browser, string method, string path, string? body, params (string Name, string Value)[] added)
    {
        (string, string)[] headers = [(DeviceHeader, Device), .. added];
        return method == "GET" ? browser.GetAsync(path, headers) : browser.PostWithoutClientHeaderAsync(path, body, headers);
    }

    private static (int Level, int Attempt, string? Status) AttemptOf(JsonElement game)
    {
        JsonElement attempt = game.GetProperty("attempt");
        return (
            attempt.GetProperty("levelNumber").GetInt32(),
            attempt.GetProperty("attemptNumber").GetInt32(),
            attempt.GetProperty("state").GetProperty("status").GetString());
    }

    private static DateTimeOffset ExpiryOf(string cookie)
    {
        string expires = cookie.Split(';').Select(part => part.Trim()).Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..];
        return DateTimeOffset.Parse(expires, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    private static string? PolicyOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).SingleOrDefault();

    private static string Describe(RouteEndpoint endpoint)
    {
        string method = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Single() ?? "ANY";
        return $"{method} /{endpoint.RoutePattern.RawText?.Trim('/')}";
    }
}
