using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Puzzle.Core.Engine;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;
using Puzzle.Web.Players;
using Puzzle.Web.Security;

namespace Puzzle.Core.Tests;

/// <summary>
/// What keeps a public server safe and its players' games whole on the open internet, over HTTP on the real server:
/// plain HTTP is turned away, tries at a secret are bounded for everyone together, a new password ends the old
/// sign-ins, a live attempt outlasts a restart and is never played blind, and one account cannot flood the attempt log.
/// </summary>
public sealed class PublicModeSafeguardTests : IDisposable
{
    private const string PlainHttp = "http";

    private readonly TempFolder _folder = new();

    public PublicModeSafeguardTests()
    {
        PublicLevels.Write(Path.Combine(_folder.Path, "levels"), PublicLevels.Easy(1), PublicLevels.Long(2), PublicLevels.Hopeless(3));
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task A_request_the_tunnel_took_over_plain_http_is_sent_to_https_when_it_reads_and_refused_when_it_changes()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser browser = server.NewBrowser();
        (string, string) viaTheTunnelOverHttp = ("X-Forwarded-Proto", PlainHttp);

        using HttpResponseMessage page = await browser.GetAsync("/?from=a-link", viaTheTunnelOverHttp);
        using HttpResponseMessage keyForm = await browser.GetAsync("/lab/sign-in", viaTheTunnelOverHttp);
        using HttpResponseMessage signIn = await browser.PostAsync(
            "/api/player/sign-in",
            new { username = "Ayse", password = PuzzleServer.PlayerPassword },
            viaTheTunnelOverHttp);
        IReadOnlyList<string> cookiesOfTheSignIn = browser.LastSetCookies;
        using HttpResponseMessage ownerKey = await browser.PostAsync("/api/owner/sign-in", new { key = PuzzleServer.OwnerKey }, viaTheTunnelOverHttp);

        Assert.Equal(
            (HttpStatusCode.PermanentRedirect, $"https://{server.Address.Authority}/?from=a-link"),
            (page.StatusCode, page.Headers.Location?.OriginalString));
        Assert.Equal("no-store", page.Headers.CacheControl?.ToString());
        Assert.Equal(
            (HttpStatusCode.PermanentRedirect, $"https://{server.Address.Authority}/lab/sign-in"),
            (keyForm.StatusCode, keyForm.Headers.Location?.OriginalString));
        Assert.Equal("httpsRequired", await TestBrowser.ReadErrorAsync(signIn, HttpStatusCode.BadRequest));
        Assert.Empty(cookiesOfTheSignIn); // the right password, sent in clear, signs nobody in
        Assert.Equal(HttpStatusCode.BadRequest, ownerKey.StatusCode);
        Assert.Empty(browser.LastSetCookies);
    }

    [Fact]
    public async Task Over_https_the_browser_is_told_to_stay_there_and_a_request_no_proxy_vouches_for_is_served_as_it_comes()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser throughTheTunnel = server.NewBrowser("203.0.113.7"); // the tunnel says "https" for it
        TestBrowser onThisMachine = server.NewBrowser();

        using HttpResponseMessage page = await throughTheTunnel.GetAsync("/");
        using HttpResponseMessage signIn = await throughTheTunnel.SignInAsync("Ayse");
        using HttpResponseMessage gameWithTheCookie = await throughTheTunnel.GetAsync("/api/player/game");
        using HttpResponseMessage localPage = await onThisMachine.GetAsync("/");
        using HttpResponseMessage localSignIn = await onThisMachine.SignInAsync("Ayse");

        foreach (HttpResponseMessage overHttps in new[] { page, signIn })
        {
            Assert.Equal(HttpStatusCode.OK, overHttps.StatusCode);
            Assert.Equal("max-age=31536000", Assert.Single(overHttps.Headers.GetValues("Strict-Transport-Security")));
        }

        Assert.Equal(HttpStatusCode.OK, gameWithTheCookie.StatusCode);
        Assert.Equal(HttpStatusCode.OK, localPage.StatusCode);
        Assert.Equal(HttpStatusCode.OK, localSignIn.StatusCode);
        Assert.False(localPage.Headers.Contains("Strict-Transport-Security")); // plain HTTP can promise nothing about HTTPS
    }

    [Theory]
    [InlineData("https")]
    [InlineData(null)]
    public async Task Through_tailscale_funnel_a_player_signs_in_and_plays_whether_or_not_the_proxy_names_the_scheme(string? schemeTheProxyNames)
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser browser = server.NewBrowser();
        (string, string)[] viaFunnel = schemeTheProxyNames is null
            ? ViaTailscaleFunnel("203.0.113.7")
            : ViaTailscaleFunnel("203.0.113.7", ("X-Forwarded-Proto", schemeTheProxyNames));

        using HttpResponseMessage page = await browser.GetAsync("/", viaFunnel);
        using HttpResponseMessage signIn = await browser.PostAsync(
            "/api/player/sign-in",
            new { username = "Ayse", password = PuzzleServer.PlayerPassword },
            viaFunnel);
        string cookie = Assert.Single(browser.LastSetCookies).ToLowerInvariant();
        JsonElement game = await browser.PostJsonAsync("/api/player/game/attempts", body: null, viaFunnel);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Contains("secure", cookie); // the browser is on HTTPS whatever the proxy said, and keeps the cookie
        Assert.Equal("Playing", game.GetProperty("attempt").GetProperty("state").GetProperty("status").GetString());
    }

    [Fact]
    public async Task What_tailscale_passes_on_is_not_told_to_keep_the_machines_whole_name_on_https()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        (string, string) overHttps = ("X-Forwarded-Proto", "https");

        // The name in the address is the machine's in its tailnet; what else it serves there over plain HTTP must go on opening.
        using HttpResponseMessage fromTheInternet = await browser.GetAsync("/", ViaTailscaleFunnel("203.0.113.7", overHttps));
        using HttpResponseMessage fromTheTailnet = await browser.GetAsync("/", ("Tailscale-User-Login", "ayse@example.com"), ("X-Forwarded-For", "100.64.0.7"), overHttps);
        using HttpResponseMessage throughAnotherTunnel = await browser.GetAsync("/", ("X-Forwarded-For", "203.0.113.7"), overHttps);

        Assert.Equal(HttpStatusCode.OK, fromTheInternet.StatusCode);
        Assert.False(fromTheInternet.Headers.Contains("Strict-Transport-Security"));
        Assert.False(fromTheTailnet.Headers.Contains("Strict-Transport-Security"));
        Assert.True(throughAnotherTunnel.Headers.Contains("Strict-Transport-Security")); // an address that is this server's alone
    }

    [Fact]
    public async Task Through_tailscale_the_address_the_proxy_names_is_the_one_counted_whatever_address_the_client_claims()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();

        for (int number = 1; number <= RateLimits.SecretChecksPerAddressPerMinute; number++)
        {
            // Tailscale passes a Cloudflare header on as the client wrote it; were it believed, each try would come
            // from an address of its own. An empty password is refused before any hashing, which keeps the test quick.
            using HttpResponseMessage tried = await TryToSignInAsync(browser, ViaTailscaleFunnel("203.0.113.7", ("CF-Connecting-IP", $"198.51.100.{number}")));
            Assert.True(tried.StatusCode == HttpStatusCode.Unauthorized, $"try {number} answered {tried.StatusCode}");
        }

        using HttpResponseMessage oneTooMany = await TryToSignInAsync(browser, ViaTailscaleFunnel("203.0.113.7", ("CF-Connecting-IP", "198.51.100.200")));
        using HttpResponseMessage fromAnotherAddress = await TryToSignInAsync(browser, ViaTailscaleFunnel("203.0.113.8"));

        Assert.Equal("tooManyRequests", await TestBrowser.ReadErrorAsync(oneTooMany, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.Unauthorized, fromAnotherAddress.StatusCode);
    }

    [Fact]
    public async Task Tries_at_a_secret_from_ever_new_addresses_meet_a_bound_all_addresses_share_and_playing_goes_on()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse"); // the first of the minute's tries
        server.AddPlayer("Mert");

        for (int number = 1; number < RateLimits.SecretChecksPerMinute; number++)
        {
            // Each from an address of its own, so no address meets its own limit. An empty password is refused before
            // any hashing, which keeps the test quick.
            using HttpResponseMessage tried = await server.NewBrowser($"198.51.100.{number}").SignInAsync($"guess-{number}", string.Empty);
            Assert.True(tried.StatusCode == HttpStatusCode.Unauthorized, $"try {number} answered {tried.StatusCode}");
        }

        using HttpResponseMessage signIn = await server.NewBrowser("203.0.113.200").SignInAsync("Mert");
        using HttpResponseMessage ownerKey = await server.NewBrowser("203.0.113.201").SignInAsOwnerAsync();
        JsonElement game = await player.PostJsonAsync("/api/player/game/attempts");

        Assert.Equal("tooManyRequests", await TestBrowser.ReadErrorAsync(signIn, HttpStatusCode.TooManyRequests));
        Assert.True(signIn.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.TooManyRequests, ownerKey.StatusCode);
        Assert.Equal("Playing", game.GetProperty("attempt").GetProperty("state").GetProperty("status").GetString()); // a signed-in player is not held up
    }

    [Fact]
    public async Task One_address_that_floods_uses_up_its_own_tries_at_a_secret_and_not_the_ones_all_addresses_share()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser flooding = server.NewBrowser("203.0.113.9");
        int flood = RateLimits.SecretChecksPerAddressPerMinute + 30;
        int refusedOfTheFlood = 0;

        for (int number = 0; number < flood; number++)
        {
            using HttpResponseMessage tried = await flooding.SignInAsync($"guess-{number}", string.Empty);
            refusedOfTheFlood += tried.StatusCode == HttpStatusCode.TooManyRequests ? 1 : 0;
        }

        // What the flood was refused must not count against everyone: all the tries the minute has left can still be made.
        for (int number = 1; number <= RateLimits.SecretChecksPerMinute - RateLimits.SecretChecksPerAddressPerMinute; number++)
        {
            using HttpResponseMessage tried = await server.NewBrowser($"198.51.100.{number}").SignInAsync($"guess-{number}", string.Empty);
            Assert.True(tried.StatusCode == HttpStatusCode.Unauthorized, $"try {number} from another address answered {tried.StatusCode}");
        }

        Assert.Equal(30, refusedOfTheFlood);
    }

    [Fact]
    public async Task Only_a_few_secrets_are_checked_at_once_and_each_check_still_gets_its_own_answer()
    {
        using var checks = new SecretChecks();
        int running = 0;
        int mostAtOnce = 0;

        int[] answers = await Task.WhenAll(Enumerable.Range(0, 12).Select(number => Task.Run(() => checks.RunAsync(
            () =>
            {
                int now = Interlocked.Increment(ref running);
                InterlockedMax(ref mostAtOnce, now);
                Thread.Sleep(20); // long enough for checks that are not held back to pile up
                Interlocked.Decrement(ref running);
                return number;
            },
            CancellationToken.None))));

        Assert.InRange(mostAtOnce, 1, SecretChecks.AtOnce);
        Assert.Equal(Enumerable.Range(0, 12), answers);
    }

    [Fact]
    public async Task A_check_whose_request_was_given_up_while_it_waited_is_never_run()
    {
        using var checks = new SecretChecks();
        using var release = new ManualResetEventSlim();
        int running = 0;
        Task<bool>[] holders = Enumerable.Range(0, SecretChecks.AtOnce).Select(_ => Task.Run(() => checks.RunAsync(
            () =>
            {
                Interlocked.Increment(ref running);
                return release.Wait(TimeSpan.FromSeconds(30));
            },
            CancellationToken.None))).ToArray();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref running) == SecretChecks.AtOnce, TimeSpan.FromSeconds(10)), "The turns were not taken.");
        using var givenUp = new CancellationTokenSource();
        bool ran = false;

        Task<bool> waiting = checks.RunAsync(() => ran = true, givenUp.Token);
        await givenUp.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        release.Set();
        Assert.All(await Task.WhenAll(holders), released => Assert.True(released));
        Assert.False(ran);
    }

    [Theory]
    [InlineData("/", "/api/player/sign-in", "{\"username\":\"Ayse\",\"password\":\"correct horse\"}")]
    [InlineData("/lab/sign-in", "/api/owner/sign-in", "{\"key\":\"" + PuzzleServer.OwnerKey + "\"}")]
    public async Task A_sign_in_form_the_browser_sends_by_itself_goes_out_as_a_post_never_in_an_address_and_is_refused(string page, string action, string secret)
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        server.AddPlayer("Ayse");
        TestBrowser browser = server.NewBrowser();

        using HttpResponseMessage served = await browser.GetAsync(page);
        string html = await served.Content.ReadAsStringAsync();
        using HttpResponseMessage sentByTheBrowserItself = await browser.PostWithoutClientHeaderAsync(action, secret);

        string form = Assert.Single(Regex.Matches(html, "<form\\b[^>]*>")).Value;
        Assert.Contains("method=\"post\"", form); // without it the fields would go into the address, and from there into history and logs
        Assert.Contains($"action=\"{action}\"", form);
        Assert.Equal("clientHeaderRequired", await TestBrowser.ReadErrorAsync(sentByTheBrowserItself, HttpStatusCode.BadRequest));
        Assert.Empty(browser.LastSetCookies);
    }

    [Fact]
    public async Task A_new_password_from_the_owner_signs_out_every_browser_of_that_player_and_keeps_their_progress()
    {
        const string NewPassword = "a new password";
        TestBrowser phone;
        TestBrowser tablet;
        TestBrowser mert;
        await using (PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            phone = await server.SignedInPlayerAsync("Ayse");
            tablet = server.NewBrowser();
            using HttpResponseMessage secondSignIn = await tablet.SignInAsync("Ayse");
            mert = await server.SignedInPlayerAsync("Mert");
            await WinTheFirstLevelAsync(server, phone, "Ayse");

            // Signing in elsewhere, and the hash the server raised to its own cost meanwhile, sign nobody out.
            Assert.True((await phone.GetJsonAsync("/api/player/me")).GetProperty("signedIn").GetBoolean());
        }

        PuzzleServer.ChangePasswordIn(_folder.Path, "Ayse", NewPassword);

        await using PuzzleServer restarted = await PuzzleServer.StartPublicAsync(_folder.Path);
        JsonElement phoneAfter = await restarted.NewBrowserFrom(phone).GetJsonAsync("/api/player/me");
        using HttpResponseMessage tabletGame = await restarted.NewBrowserFrom(tablet).GetAsync("/api/player/game");
        JsonElement mertAfter = await restarted.NewBrowserFrom(mert).GetJsonAsync("/api/player/me");
        TestBrowser again = restarted.NewBrowser();
        using HttpResponseMessage withTheOldPassword = await again.SignInAsync("Ayse");
        using HttpResponseMessage withTheNewPassword = await again.SignInAsync("Ayse", NewPassword);
        JsonElement game = await again.GetJsonAsync("/api/player/game");

        Assert.False(phoneAfter.GetProperty("signedIn").GetBoolean());
        Assert.Equal("signInRequired", await TestBrowser.ReadErrorAsync(tabletGame, HttpStatusCode.Unauthorized));
        Assert.Equal("Mert", mertAfter.GetProperty("username").GetString()); // nobody else is signed out
        Assert.Equal(HttpStatusCode.Unauthorized, withTheOldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withTheNewPassword.StatusCode);
        Assert.Equal(2, game.GetProperty("progress").GetProperty("levelNumber").GetInt32()); // the win is still theirs
        Assert.Equal("Won", game.GetProperty("attempt").GetProperty("state").GetProperty("status").GetString());
        Assert.Single(new AttemptLog(restarted.RecordingsPath).ReadAll().Records); // and so is its record
    }

    [Fact]
    public async Task A_players_live_attempt_is_there_move_for_move_after_the_server_restarts_and_the_page_left_open_plays_on()
    {
        TestBrowser player;
        JsonElement before;
        await using (PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            player = await server.SignedInPlayerAsync("Ayse");
            await OpenTheLongLevelAsync(server, player, "Ayse");
            await PlayMovesAsync(server, player, "Ayse", count: 3);
            before = await player.GetJsonAsync("/api/player/game");
        }

        await using PuzzleServer restarted = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser back = restarted.NewBrowserFrom(player);
        JsonElement after = await back.GetJsonAsync("/api/player/game");
        JsonElement attempt = before.GetProperty("attempt");
        var withWhatThePageHad = new PlayerMove(
            Guid.ParseExact(attempt.GetProperty("id").GetString() ?? string.Empty, "N"),
            attempt.GetProperty("state").GetProperty("movesUsed").GetInt32(),
            PublicLevels.FirstLegalMove(restarted.Games, restarted.PlayerIdOf("Ayse")));
        using HttpResponseMessage played = await back.PlayAsync(withWhatThePageHad);
        JsonElement move = await TestBrowser.ReadJsonAsync(played, HttpStatusCode.OK);

        Assert.Equal((2, 3), (attempt.GetProperty("levelNumber").GetInt32(), attempt.GetProperty("state").GetProperty("movesUsed").GetInt32()));
        Assert.Equal(before.GetRawText(), after.GetRawText()); // the same attempt under the same id, and the board cell for cell
        Assert.True(move.GetProperty("accepted").GetBoolean());
        Assert.Equal(4, move.GetProperty("state").GetProperty("movesUsed").GetInt32());
        AttemptRecord onlyRecord = Assert.Single(new AttemptLog(restarted.RecordingsPath).ReadAll().Records);
        Assert.Equal(AttemptResult.Won, onlyRecord.Outcome.Result); // level 1's: the stop recorded nothing as abandoned
    }

    [Fact]
    public async Task A_move_from_a_page_that_is_behind_is_refused_under_a_code_of_its_own_and_plays_nothing()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");
        DeviceId playerId = server.PlayerIdOf("Ayse");
        await OpenTheLongLevelAsync(server, player, "Ayse");
        PlayerMove asASecondTabSawIt = PublicLevels.OnTheLiveAttempt(server.Games, playerId, PublicLevels.FirstLegalMove(server.Games, playerId));
        await PlayMovesAsync(server, player, "Ayse", count: 1); // the first tab plays on

        Move legalNow = PublicLevels.FirstLegalMove(server.Games, playerId);
        using HttpResponseMessage onAnEarlierBoard = await player.PlayAsync(asASecondTabSawIt with { Move = legalNow });
        using HttpResponseMessage onAnotherAttempt = await player.PlayAsync(new PlayerMove(Guid.NewGuid(), MovesSeen: 1, legalNow));
        JsonElement game = await player.GetJsonAsync("/api/player/game");

        JsonElement refusal = await TestBrowser.ReadJsonAsync(onAnEarlierBoard, HttpStatusCode.Conflict);
        Assert.Equal("attemptChanged", refusal.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(refusal.GetProperty("message").GetString()));
        Assert.Equal("attemptChanged", await TestBrowser.ReadErrorAsync(onAnotherAttempt, HttpStatusCode.Conflict));
        Assert.Equal(1, game.GetProperty("attempt").GetProperty("state").GetProperty("movesUsed").GetInt32());
    }

    [Fact]
    public async Task An_attempt_being_played_is_left_only_by_a_start_that_names_it()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");
        await OpenTheLongLevelAsync(server, player, "Ayse");
        await PlayMovesAsync(server, player, "Ayse", count: 2);
        JsonElement beingPlayed = await player.GetJsonAsync("/api/player/game");
        string id = beingPlayed.GetProperty("attempt").GetProperty("id").GetString() ?? string.Empty;

        JsonElement withoutAName = await player.PostJsonAsync("/api/player/game/attempts");
        JsonElement namingAnother = await player.PostJsonAsync("/api/player/game/attempts", new { leaves = Guid.NewGuid().ToString("N") });
        JsonElement namingWhatIsNoId = await player.PostJsonAsync("/api/player/game/attempts", new { leaves = "the attempt" });
        int recordsBefore = new AttemptLog(server.RecordingsPath).ReadAll().Records.Count;
        JsonElement namingIt = await player.PostJsonAsync("/api/player/game/attempts", new { leaves = id });

        foreach (JsonElement left in new[] { withoutAName, namingAnother, namingWhatIsNoId })
        {
            Assert.Equal(beingPlayed.GetRawText(), left.GetRawText()); // the page gets the attempt as it stands, and nothing ended
        }

        Assert.Equal(1, recordsBefore); // level 1's win, and nothing since
        Assert.NotEqual(id, namingIt.GetProperty("attempt").GetProperty("id").GetString());
        Assert.Equal((2, 2, "Playing"), (
            namingIt.GetProperty("attempt").GetProperty("levelNumber").GetInt32(),
            namingIt.GetProperty("attempt").GetProperty("attemptNumber").GetInt32(),
            namingIt.GetProperty("attempt").GetProperty("state").GetProperty("status").GetString()));
        AttemptRecord abandoned = new AttemptLog(server.RecordingsPath).ReadAll().Records[^1];
        Assert.Equal((AttemptResult.Abandoned, 2), (abandoned.Outcome.Result, abandoned.Outcome.MovesUsed));
    }

    [Fact]
    public async Task A_player_who_leaves_attempts_faster_than_anyone_plays_is_told_to_wait_and_the_attempt_log_stays_bounded()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser player = await server.SignedInPlayerAsync("Ayse");
        await OpenTheLongLevelAsync(server, player, "Ayse"); // two attempts so far: level 1's, won, and this one
        for (int opened = 2; opened < AttemptBudget.Attempts; opened++)
        {
            await PlayMovesAsync(server, player, "Ayse", count: 1);
            using HttpResponseMessage next = await LeaveTheLiveAttemptAsync(player);
            Assert.True(next.StatusCode == HttpStatusCode.OK, $"attempt {opened + 1} answered {next.StatusCode}");
        }

        await PlayMovesAsync(server, player, "Ayse", count: 1);
        JsonElement beingPlayed = await player.GetJsonAsync("/api/player/game");

        using HttpResponseMessage oneTooMany = await LeaveTheLiveAttemptAsync(player);
        JsonElement refusal = await TestBrowser.ReadJsonAsync(oneTooMany, HttpStatusCode.TooManyRequests);
        JsonElement afterwards = await player.GetJsonAsync("/api/player/game");
        await PlayMovesAsync(server, player, "Ayse", count: 1); // the attempt that stayed plays on

        Assert.Equal("tooManyAttempts", refusal.GetProperty("error").GetString());
        Assert.Contains("deneme", refusal.GetProperty("message").GetString()); // a sentence the page shows as it is
        Assert.InRange(refusal.GetProperty("retryAfterSeconds").GetInt32(), 1, (int)AttemptBudget.Window.TotalSeconds);
        Assert.Equal(refusal.GetProperty("retryAfterSeconds").GetRawText(), Assert.Single(oneTooMany.Headers.GetValues("Retry-After"))); // the header says the same seconds
        Assert.Equal(beingPlayed.GetRawText(), afterwards.GetRawText());
        Assert.Equal(AttemptBudget.Attempts - 1, new AttemptLog(server.RecordingsPath).ReadAll().Records.Count); // not one for the attempt that stayed
    }

    [Fact]
    public async Task Only_a_public_server_keeps_trying_to_save_what_the_player_store_refused()
    {
        await using (PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path))
        {
            Assert.Single(server.App.Services.GetServices<IHostedService>().OfType<UnsavedProgressSaver>());
        }

        await using PuzzleServer lab = await PuzzleServer.StartLabAsync(_folder.Path);
        Assert.Empty(lab.App.Services.GetServices<IHostedService>().OfType<UnsavedProgressSaver>());
    }

    /// <summary>
    /// What Tailscale Funnel puts on a request from the internet: its mark, the client's address and the host the
    /// browser asked for. <paramref name="more"/> is whatever else is on it: the scheme, when the proxy names one, or
    /// a header the client wrote, which Tailscale passes on.
    /// </summary>
    private static (string Name, string Value)[] ViaTailscaleFunnel(string clientAddress, params (string Name, string Value)[] more) =>
        [("Tailscale-Funnel-Request", "?1"), ("X-Forwarded-For", clientAddress), ("X-Forwarded-Host", "machine.tailnet.example:8443"), .. more];

    /// <summary>A sign-in that names nobody and is refused before any password is checked, sent with <paramref name="headers"/>.</summary>
    private static Task<HttpResponseMessage> TryToSignInAsync(TestBrowser browser, (string Name, string Value)[] headers) =>
        browser.PostAsync("/api/player/sign-in", new { username = "guess", password = string.Empty }, headers);

    /// <summary>Starts the player's attempt on level 1, which any sensible play wins, and plays it to its end over HTTP.</summary>
    private static async Task WinTheFirstLevelAsync(PuzzleServer server, TestBrowser player, string username)
    {
        using HttpResponseMessage started = await player.PostAsync("/api/player/game/attempts");
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        await PublicLevels.PlayToTheEndAsync(server.Games, server.PlayerIdOf(username), async move =>
        {
            using HttpResponseMessage played = await player.PlayAsync(move);
            Assert.Equal(HttpStatusCode.OK, played.StatusCode);
        });
    }

    /// <summary>Wins level 1 and opens the player's attempt on level 2, which no few moves decide.</summary>
    private static async Task OpenTheLongLevelAsync(PuzzleServer server, TestBrowser player, string username)
    {
        await WinTheFirstLevelAsync(server, player, username);
        JsonElement opened = await player.PostJsonAsync("/api/player/game/attempts");
        Assert.Equal(2, opened.GetProperty("attempt").GetProperty("levelNumber").GetInt32());
    }

    /// <summary>Asks for the next attempt in place of the live one, naming it as the page that shows it does.</summary>
    private static async Task<HttpResponseMessage> LeaveTheLiveAttemptAsync(TestBrowser player)
    {
        JsonElement game = await player.GetJsonAsync("/api/player/game");
        return await player.PostAsync("/api/player/game/attempts", new { leaves = game.GetProperty("attempt").GetProperty("id").GetString() });
    }

    private static async Task PlayMovesAsync(PuzzleServer server, TestBrowser player, string username, int count)
    {
        DeviceId playerId = server.PlayerIdOf(username);
        for (int played = 0; played < count; played++)
        {
            PlayerMove move = PublicLevels.OnTheLiveAttempt(server.Games, playerId, PublicLevels.FirstLegalMove(server.Games, playerId));
            using HttpResponseMessage answered = await player.PlayAsync(move);
            JsonElement result = await TestBrowser.ReadJsonAsync(answered, HttpStatusCode.OK);
            Assert.True(result.GetProperty("accepted").GetBoolean());
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}
