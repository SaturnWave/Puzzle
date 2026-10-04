using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Recording;
using Puzzle.Tooling;
using Puzzle.Tooling.Players;
using Puzzle.Web;
using Puzzle.Web.Api;
using Puzzle.Web.Players;
using Puzzle.Web.Sessions;

namespace Puzzle.Core.Tests;

/// <summary>Small levels for the public mode's tests, written to a folder as a level set of their own.</summary>
internal static class PublicLevels
{
    /// <summary>The name of the test level set, and of its folder under the levels root.</summary>
    public const string Set = "public";

    /// <summary>A level any sensible play wins within a few moves: three red gems in thirty moves.</summary>
    public static LevelDefinition Easy(int id, uint[]? seedPool = null) => Open(id, redToCollect: 3, moves: 30, seedPool);

    /// <summary>A level no single move decides: far more red gems than one move clears, and thirty moves to try.</summary>
    public static LevelDefinition Long(int id, uint[]? seedPool = null) => Open(id, redToCollect: 500, moves: 30, seedPool);

    /// <summary>A level nobody wins: more red gems than two moves can ever clear.</summary>
    public static LevelDefinition Hopeless(int id, uint[]? seedPool = null) => Open(id, redToCollect: 999, moves: 2, seedPool);

    /// <summary>Writes <paramref name="levels"/> as the set <see cref="Set"/> under <paramref name="levelsRoot"/>, each in a file named after its id, and loads the library.</summary>
    public static LevelLibrary Write(string levelsRoot, params LevelDefinition[] levels) =>
        WriteNamed(levelsRoot, levels.Select(level => ($"level_{level.Id.Value:0000}", level)).ToArray());

    /// <summary>Writes each level as the file <c>Name.json</c> of the set <see cref="Set"/> under <paramref name="levelsRoot"/> and loads the library.</summary>
    public static LevelLibrary WriteNamed(string levelsRoot, params (string Name, LevelDefinition Level)[] files)
    {
        string folder = Directory.CreateDirectory(Path.Combine(levelsRoot, Set)).FullName;
        foreach ((string name, LevelDefinition level) in files)
        {
            LevelJson.Save(level, Path.Combine(folder, name + ".json"));
        }

        return LevelLibrary.Load(levelsRoot);
    }

    /// <summary>
    /// Plays the player's live attempt to its end with an expert bot's choices, through <paramref name="send"/>, which
    /// plays one move the way the test under way sends moves; returns the moves it sent.
    /// </summary>
    public static async Task<int> PlayToTheEndAsync(PlayerGames games, DeviceId playerId, Func<PlayerMove, Task> send)
    {
        var bot = new GreedyPolicy(SkillProfile.Expert);
        for (int sent = 0; ; sent++)
        {
            Assert.True(games.TryGetLiveSession(playerId, out GameSession? session), "The player has no live attempt.");
            Move move;
            lock (session.Gate)
            {
                if (session.Game.Status.IsOver())
                {
                    return sent;
                }

                move = bot.ChooseMove(session.Game, session.Game.FindLegalMoves(), session.BotRng);
            }

            await send(OnTheLiveAttempt(games, playerId, move));
        }
    }

    /// <summary>A move the engine takes on the board of the player's live attempt as it is now.</summary>
    public static Move FirstLegalMove(PlayerGames games, DeviceId playerId)
    {
        Assert.True(games.TryGetLiveSession(playerId, out GameSession? session), "The player has no live attempt.");
        lock (session.Gate)
        {
            return session.Game.FindLegalMoves()[0];
        }
    }

    /// <summary><paramref name="move"/> as a page that shows the player's live attempt as it is now would send it.</summary>
    public static PlayerMove OnTheLiveAttempt(PlayerGames games, DeviceId playerId, Move move)
    {
        PlayerAttemptDto? attempt = games.Current(playerId).Attempt;
        Assert.NotNull(attempt);
        return new PlayerMove(IdOf(attempt), attempt.State.MovesUsed, move);
    }

    /// <summary>The id of <paramref name="attempt"/> as the server keeps it.</summary>
    public static Guid IdOf(PlayerAttemptDto attempt) => Guid.ParseExact(attempt.Id, "N");

    private static LevelDefinition Open(int id, int redToCollect, int moves, uint[]? seedPool) => new(
        new LevelId(id),
        DifficultyTier.Normal,
        LevelLayout.Parse(new LayoutLayers { Cells = TestBoards.Blank(10, 12, '#') }),
        new GoalSpec[] { new GoalSpec.CollectColor(GemColor.Red, redToCollect) },
        moves,
        new SpawnRules { Palette = GemColors.First(5) },
        new AttemptPolicy { Board = new StartBoard.Varied(), SeedPool = seedPool ?? Array.Empty<uint>() });
}

/// <summary>
/// The real web app on a loopback port of its own, as the tunnel's proxy would reach it, with its levels, recordings and
/// player data in a temporary folder. Stopping it releases the player store.
/// </summary>
internal sealed class PuzzleServer : IAsyncDisposable
{
    public const string OwnerKey = "test-owner-key-0123456789abcdef";

    /// <summary>The password of every player these tests add, unless a test names another.</summary>
    public const string PlayerPassword = "correct horse";

    /// <summary>The players these tests add get a cheap hash; the server replaces it at its own cost when they first sign in.</summary>
    private static readonly PasswordHasher CheapHasher = new(iterations: 1_000);

    private readonly HttpClient _http;

    private PuzzleServer(WebApplication app, string folder)
    {
        App = app;
        Folder = folder;
        Address = new Uri(app.Urls.Single());

        // Cookies are kept by each TestBrowser: the server marks them Secure, which the client's own jar would not send over plain HTTP.
        _http = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = Address };
    }

    public WebApplication App { get; }

    /// <summary>The folder that holds this server's levels, recordings and data.</summary>
    public string Folder { get; }

    public Uri Address { get; }

    public string RecordingsPath => RecordingsPathIn(Folder);

    public string DataFolder => DataFolderIn(Folder);

    public PlayerGames Games => App.Services.GetRequiredService<PlayerGames>();

    public static string RecordingsPathIn(string folder) => Path.Combine(folder, "recordings", "attempts.jsonl");

    public static string DataFolderIn(string folder) => Path.Combine(folder, "data");

    /// <summary>Starts a public server over <paramref name="folder"/>; <paramref name="settings"/> replace or add <c>Puzzle:</c> settings.</summary>
    public static Task<PuzzleServer> StartPublicAsync(string folder, params (string Key, string Value)[] settings) =>
        StartAsync(folder, "Public", settings);

    public static Task<PuzzleServer> StartLabAsync(string folder, params (string Key, string Value)[] settings) =>
        StartAsync(folder, "Lab", settings);

    /// <summary>The command line of a server over <paramref name="folder"/>: every <c>Puzzle:</c> setting is given, so nothing of the developer's own environment leaks in.</summary>
    public static string[] ArgsFor(string folder, string mode, params (string Key, string Value)[] settings)
    {
        var all = new Dictionary<string, string>
        {
            ["Mode"] = mode,
            ["LevelsPath"] = Path.Combine(folder, "levels"),
            ["RecordingsPath"] = RecordingsPathIn(folder),
            ["DataPath"] = DataFolderIn(folder),
            ["AdminKey"] = OwnerKey,
            ["PublicLevels"] = PublicLevels.Set,
            ["ReadyMadePlayersPath"] = string.Empty, // the repository's own test accounts are not these tests' business
        };
        foreach ((string key, string value) in settings)
        {
            all[key] = value;
        }

        return all.Select(pair => $"--Puzzle:{pair.Key}={pair.Value}")
            .Concat(["--contentRoot", WebProjectFolder(), "--environment", "Production", "--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning"])
            .ToArray();
    }

    /// <summary>
    /// Adds a player to the store in <paramref name="folder"/> while no server runs over it, as the owner's
    /// <c>puzzle players add</c> does.
    /// </summary>
    public static void AddPlayerTo(string folder, string username, string password = PlayerPassword)
    {
        using PlayerStore store = PlayerStore.Open(DataFolderIn(folder));
        AddPlayer(store, username, password);
    }

    /// <summary>
    /// Gives a player another password while no server runs over the store in <paramref name="folder"/>, as the
    /// owner's <c>puzzle players password</c> does.
    /// </summary>
    public static void ChangePasswordIn(string folder, string username, string password)
    {
        using PlayerStore store = PlayerStore.Open(DataFolderIn(folder));
        Assert.IsType<ChangePasswordOutcome.Changed>(PlayerPasswordChange.Change(store, CheapHasher, new Username(username), password));
    }

    /// <summary>
    /// Adds a player to this running server's own store. The owner's tool cannot, while the server holds the store; a
    /// test may, so it need not stop the server for every player.
    /// </summary>
    public void AddPlayer(string username, string password = PlayerPassword) =>
        AddPlayer(App.Services.GetRequiredService<PlayerStore>(), username, password);

    /// <summary>A browser of its own for a player added now and signed in over HTTP.</summary>
    public async Task<TestBrowser> SignedInPlayerAsync(string username)
    {
        AddPlayer(username);
        TestBrowser browser = NewBrowser();
        using HttpResponseMessage signedIn = await browser.SignInAsync(username);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        return browser;
    }

    public TestBrowser NewBrowser(string? clientIp = null) => new(_http, clientIp);

    /// <summary>A browser that kept the cookies another server gave <paramref name="earlier"/>, now talking to this one: the same browser after a restart.</summary>
    public TestBrowser NewBrowserFrom(TestBrowser earlier) => earlier.MovedTo(_http);

    /// <summary>The player id the store holds for <paramref name="username"/>.</summary>
    public DeviceId PlayerIdOf(string username)
    {
        Assert.True(App.Services.GetRequiredService<PlayerStore>().TryFind(new Username(username), out PlayerAccount? account));
        return account.PlayerId;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
    }

    private static void AddPlayer(PlayerStore store, string username, string password) =>
        Assert.IsType<AddPlayerOutcome.Added>(PlayerAddition.Add(store, CheapHasher, new Username(username), password, DateTimeOffset.UtcNow));

    private static async Task<PuzzleServer> StartAsync(string folder, string mode, (string Key, string Value)[] settings)
    {
        WebApplication app = PuzzleApp.Build(ArgsFor(folder, mode, settings));
        await app.StartAsync();
        return new PuzzleServer(app, folder);
    }

    /// <summary>The web project's folder, the content root that holds its pages, found by walking up from the test binaries.</summary>
    private static string WebProjectFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "src", "Puzzle.Web");
            if (File.Exists(Path.Combine(candidate, "Puzzle.Web.csproj")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"src/Puzzle.Web not found above {AppContext.BaseDirectory}.");
    }
}

/// <summary>
/// One browser as a server sees it: it keeps the cookies it is given, marks its changes with the client header as the
/// player page does, and may come from an address of its own, named by the local proxy.
/// </summary>
internal sealed class TestBrowser
{
    public const string ClientHeader = "X-Puzzle-Client";

    private readonly HttpClient _http;
    private readonly string? _clientIp;
    private readonly Dictionary<string, string> _cookies = new();

    public TestBrowser(HttpClient http, string? clientIp)
    {
        _http = http;
        _clientIp = clientIp;
    }

    /// <summary>The Set-Cookie headers of the last answer, as they came.</summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    public Task<HttpResponseMessage> GetAsync(string path, params (string Name, string Value)[] headers) =>
        SendAsync(HttpMethod.Get, path, body: null, headers);

    /// <summary>A change as this site's own pages send it: with the client header.</summary>
    public Task<HttpResponseMessage> PostAsync(string path, object? body = null, params (string Name, string Value)[] headers) =>
        SendAsync(HttpMethod.Post, path, body, [(ClientHeader, "tests"), .. headers]);

    /// <summary>A change as a foreign page could make a signed-in browser send it: cookies, but no header of ours.</summary>
    public Task<HttpResponseMessage> PostWithoutClientHeaderAsync(string path, object? body = null, params (string Name, string Value)[] headers) =>
        SendAsync(HttpMethod.Post, path, body, headers);

    /// <summary>This browser's cookies in a browser that talks through <paramref name="http"/>.</summary>
    public TestBrowser MovedTo(HttpClient http)
    {
        var moved = new TestBrowser(http, _clientIp);
        foreach ((string name, string value) in _cookies)
        {
            moved._cookies[name] = value;
        }

        return moved;
    }

    public async Task<JsonElement> GetJsonAsync(string path, params (string Name, string Value)[] headers)
    {
        using HttpResponseMessage response = await GetAsync(path, headers);
        return await ReadJsonAsync(response, HttpStatusCode.OK);
    }

    public async Task<JsonElement> PostJsonAsync(string path, object? body = null, params (string Name, string Value)[] headers)
    {
        using HttpResponseMessage response = await PostAsync(path, body, headers);
        return await ReadJsonAsync(response, HttpStatusCode.OK);
    }

    public Task<HttpResponseMessage> SignInAsync(string username, string password = PuzzleServer.PlayerPassword) =>
        PostAsync("/api/player/sign-in", new { username, password });

    public Task<HttpResponseMessage> SignInAsOwnerAsync(string key = PuzzleServer.OwnerKey) =>
        PostAsync("/api/owner/sign-in", new { key });

    /// <summary>Sends <paramref name="move"/> as the player page does: the move, with the attempt and the board it was made on.</summary>
    public Task<HttpResponseMessage> PlayAsync(PlayerMove move) => PostAsync("/api/player/game/moves", new
    {
        attempt = move.Attempt.ToString("N"),
        movesUsed = move.MovesSeen,
        kind = move.Move.Kind == MoveKind.Tap ? "tap" : "swap",
        from = new { x = move.Move.From.X, y = move.Move.From.Y },
        to = move.Move.Kind == MoveKind.Tap ? null : new { x = move.Move.To.X, y = move.Move.To.Y },
    });

    /// <summary>The body of <paramref name="response"/> as JSON, once its status is the expected one.</summary>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        string text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>The error code of a refusal with the expected status.</summary>
    public static async Task<string?> ReadErrorAsync(HttpResponseMessage response, HttpStatusCode expected) =>
        (await ReadJsonAsync(response, expected)).GetProperty("error").GetString();

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is string text)
        {
            request.Content = new StringContent(text, Encoding.UTF8, "application/json");
        }
        else if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        if (_clientIp is not null)
        {
            // What the proxy of either tunnel says of a request it took over HTTPS: the client's address, and the scheme.
            request.Headers.Add("X-Forwarded-For", _clientIp);
            request.Headers.Add("X-Forwarded-Proto", "https");
        }

        foreach ((string name, string value) in headers)
        {
            request.Headers.Add(name, value);
        }

        HttpResponseMessage response = await _http.SendAsync(request);
        KeepCookies(response);
        return response;
    }

    private void KeepCookies(HttpResponseMessage response)
    {
        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? set) ? set.ToArray() : [];
        foreach (string cookie in LastSetCookies)
        {
            string[] pair = cookie.Split(';')[0].Split('=', 2);
            if (pair[1].Length == 0)
            {
                _cookies.Remove(pair[0]);
            }
            else
            {
                _cookies[pair[0]] = pair[1];
            }
        }
    }
}
