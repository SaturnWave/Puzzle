using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Puzzle.Tooling;
using Puzzle.Tooling.Players;
using Puzzle.Web.Accounts;
using Puzzle.Web.Api;
using Puzzle.Web.Players;
using Puzzle.Web.Security;

namespace Puzzle.Web;

/// <summary>
/// Wires the public mode: the player page at the site's root with its API, open to everyone the limits let through,
/// and the lab — its page under <c>/lab/</c> and every one of its endpoints — for the owner alone.
/// </summary>
internal static class PublicMode
{
    /// <summary>The folder of the web root that holds the player page; in the public mode it is the site's root.</summary>
    private const string PlayerPageFolder = "player";

    /// <summary>The largest request the public mode reads: its biggest honest one, a sign-in, is a few hundred bytes.</summary>
    private const int MaxRequestBodyBytes = 16 * 1024;

    private const string CookieKeysFolder = "keys";
    private const string LoggerCategory = "Puzzle.Web.Public";

    /// <summary>What an answer may be kept as unless the file it carries says otherwise: not at all, by nobody.</summary>
    private const string NeverStored = "no-store";

    /// <summary>For a year, a browser that reached this site over HTTPS does not try plain HTTP again.</summary>
    private const string HttpsForAYear = "max-age=31536000";

    /// <summary>
    /// The lab's files that the public mode's own pages share, served under the same names at the site's root; they alone
    /// of the lab's files are open to everyone: the stylesheet, and the game as both pages draw it (the board, the tile
    /// art). Nothing in them is the lab's own: no level picker, no seed, no bot.
    /// </summary>
    private static readonly string[] SharedFiles = ["app.css", "dom.js", "game-view.js", "tiles.svg"];

    /// <summary>
    /// What a page of a public server may load and run: only this server's own files, and one thing of another site,
    /// the typefaces of the player page, which Google Fonts serves (a stylesheet from one of its hosts, the font files
    /// that stylesheet names from the other). No inline script or style, no plugin, no frame around it, and no form
    /// or base address that points elsewhere, so markup that slipped into a page could neither run nor send anything
    /// away.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; style-src 'self' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; " +
        "object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

    /// <exception cref="InvalidOperationException">The public level set cannot be played through; the message says why.</exception>
    public static void AddPublicMode(this WebApplicationBuilder builder, PublicSettings settings, LevelLibrary library)
    {
        var levels = new PublicLevelSet(library, settings.LevelSet);

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes);
        builder.Services.Configure<ForwardedHeadersOptions>(TrustTheLocalProxyForTheScheme);
        KeepCookieKeysWithTheData(builder.Services, settings.DataFolder);

        IServiceCollection services = builder.Services;
        services.AddSingleton(levels);
        services.AddSingleton(_ => PlayerStore.Open(settings.DataFolder));
        services.AddSingleton(new PasswordHasher());
        services.AddSingleton(provider => new SignInLockouts(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<SecretChecks>();
        services.AddSingleton<PlayerAccounts>();
        services.AddSingleton<PlayerGames>();
        services.AddHostedService<UnsavedProgressSaver>();
        services.AddPuzzleAuth(settings.OwnerKey);
        services.AddPuzzleRateLimits();
        if (settings.OwnerKey is { } ownerKey)
        {
            services.AddSingleton(ownerKey);
        }
    }

    /// <exception cref="IOException">The player store could not be read, or a ready-made account could not be written to it.</exception>
    public static void UsePublicMode(this WebApplication app, PublicSettings settings)
    {
        AddMissingReadyMadePlayers(app, settings.ReadyMadePlayers);
        AnnounceStart(app, settings);

        // Once the server takes no more requests, and before its services are put away.
        app.Lifetime.ApplicationStopped.Register(() => SaveUnsavedProgressAtStop(app));

        app.UseForwardedHeaders();
        app.UseExceptionHandler(failed => failed.Use(AddResponseRulesAsync).Run(AnswerFailureAsync));
        app.Use(AddResponseRulesAsync);
        app.UseHttpsOnly();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseCrossSiteGuard();
        app.UseAuthentication();
        app.UseAuthorization();
        ServePlayerPage(app);

        foreach (string shared in SharedFiles)
        {
            app.MapGet("/" + shared, (HttpContext context) => WebRootFiles.Serve(context, shared, WebRootFiles.Revalidated));
        }

        app.MapPlayerApi();
        if (settings.OwnerKey is null)
        {
            return;
        }

        // Everything the lab can do goes through this one group, so nothing of it can be left open by oversight.
        app.MapOwnerSignIn();
        RouteGroupBuilder lab = app.MapGroup(string.Empty).RequireAuthorization(PuzzleAuth.OwnerPolicy);
        lab.MapPuzzleApi();
        lab.MapRecordingsApi();
        lab.MapLabPage();
    }

    /// <summary>
    /// Makes the ready-made accounts of the public test that are not there yet, before the server takes a request; the
    /// ones that are there are left as they are. Their passwords are hashed by the server's own hasher, at its cost.
    /// </summary>
    private static void AddMissingReadyMadePlayers(WebApplication app, IReadOnlyList<ReadyMadePlayer> listed)
    {
        if (listed.Count == 0)
        {
            return;
        }

        ILogger logger = PublicLogger(app);
        IReadOnlyList<PlayerAccount> made = PlayerAddition.AddMissing(
            app.Services.GetRequiredService<PlayerStore>(),
            app.Services.GetRequiredService<PasswordHasher>(),
            listed,
            app.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        Log.ReadyMadePlayersAdded(logger, made.Count, listed.Count);
    }

    /// <summary>
    /// Opens the player store now — unless the ready-made accounts just did — rather than at the first request, so a
    /// store another process holds, or a broken file, stops the start; then says in the log what this public server
    /// offers.
    /// </summary>
    private static void AnnounceStart(WebApplication app, PublicSettings settings)
    {
        PlayerStore store = app.Services.GetRequiredService<PlayerStore>();
        ILogger logger = PublicLogger(app);
        int levelCount = app.Services.GetRequiredService<PublicLevelSet>().Count;
        string labAccess = settings.OwnerKey is null ? "off (no Puzzle:AdminKey)" : "for the owner at /lab";
        Log.PublicModeStarted(logger, levelCount, settings.LevelSet, store.Accounts.Count, store.Path, labAccess);
        if (store.Accounts.Count == 0)
        {
            Log.PublicModeHasNoAccounts(logger, store.Path);
        }

        app.Lifetime.ApplicationStarted.Register(() => WarnAboutOutsideAddresses(app, logger));
    }

    /// <summary>
    /// The players' live attempts need no word at a stop: the store has them. Only what the store refused before gets
    /// a last try here; whatever it still refuses is lost with the process, and the log says so.
    /// </summary>
    private static void SaveUnsavedProgressAtStop(WebApplication app)
    {
        int stillUnsaved = app.Services.GetRequiredService<PlayerGames>().SaveUnsavedProgress();
        if (stillUnsaved > 0)
        {
            Log.UnsavedProgressLost(PublicLogger(app), stillUnsaved, app.Services.GetRequiredService<PlayerStore>().Path);
        }
    }

    private static ILogger PublicLogger(WebApplication app) =>
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);

    /// <summary>
    /// The tunnel (Tailscale Funnel, or a Cloudflare Tunnel) ends HTTPS and passes requests on as plain HTTP from this
    /// machine, saying in X-Forwarded-Proto which scheme the browser used. Only a proxy on this machine is believed;
    /// the client's address is worked out apart from this (<see cref="ClientIp"/>). A proxy that names no scheme is
    /// served as well: the sign-in cookies are marked Secure whatever the server takes the scheme for, and the
    /// browser, which is on HTTPS, keeps them.
    /// </summary>
    private static void TrustTheLocalProxyForTheScheme(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    }

    /// <summary>
    /// The sign-in cookies are sealed with keys kept beside the player store, so a restart signs nobody out, wherever
    /// the server runs. On Windows the keys are themselves encrypted for the account the server runs under.
    /// </summary>
    private static void KeepCookieKeysWithTheData(IServiceCollection services, string dataFolder)
    {
        IDataProtectionBuilder protection = services.AddDataProtection()
            .SetApplicationName("Puzzle.Web")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataFolder, CookieKeysFolder)));
        if (OperatingSystem.IsWindows())
        {
            protection.ProtectKeysWithDpapi();
        }
    }

    private static void ServePlayerPage(WebApplication app)
    {
        string folder = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), PlayerPageFolder);
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"The player page is missing: no folder '{folder}'.");
        }

        var playerPage = new PhysicalFileProvider(folder);
        app.Lifetime.ApplicationStopped.Register(playerPage.Dispose);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = playerPage });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = playerPage,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = WebRootFiles.Revalidated,
        });
    }

    /// <summary>
    /// Headers every answer of a public server carries, the answer to a failure too (the handler wipes what was set
    /// before it, so its own branch sets them again). No cache may keep an answer — what the API says, a refusal, a
    /// redirect to the sign-in, which a cache on the way would otherwise hand to the signed-in owner as well — unless
    /// it is one of the public pages' files, which then name their own rule. Over HTTPS the browser is told to stay
    /// there, where the address is this server's alone.
    /// </summary>
    private static Task AddResponseRulesAsync(HttpContext context, RequestDelegate next)
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers["Referrer-Policy"] = "same-origin";
        headers.CacheControl = NeverStored;
        if (MayPromiseHttpsForTheHost(context.Request))
        {
            headers.StrictTransportSecurity = HttpsForAYear;
        }

        return next(context);
    }

    /// <summary>
    /// The promise holds for the host's name on every port, for a year. Through Tailscale that name is this machine's
    /// own in the tailnet, not this server's: whatever else the machine serves there over plain HTTP (a
    /// <c>tailscale serve --http</c> inside the tailnet) would stop opening in every browser that was once here. Funnel
    /// itself answers only HTTPS, so there the promise would add little.
    /// </summary>
    private static bool MayPromiseHttpsForTheHost(HttpRequest request) =>
        request.IsHttps && !TailscaleHeaders.AreOn(request);

    /// <summary>
    /// The last resort for a failure nothing else answered: the server's log has the details (the handler middleware
    /// writes them), the client gets none of them. A request the server could not read keeps its own status.
    /// </summary>
    private static Task AnswerFailureAsync(HttpContext context)
    {
        if (context.Features.Get<IExceptionHandlerFeature>()?.Error is not BadHttpRequestException unreadable)
        {
            return PlayerErrors.WriteAsync(context, PlayerError.ServerError);
        }

        context.Response.StatusCode = unreadable.StatusCode;
        return context.Response.WriteAsJsonAsync(PlayerErrors.Describe(PlayerError.BadRequest), context.RequestAborted);
    }

    private static void WarnAboutOutsideAddresses(WebApplication app, ILogger logger)
    {
        foreach (string address in app.Urls.Where(address => !IsThisMachineAlone(address)))
        {
            Log.PublicModeNotLocal(logger, address);
        }
    }

    private static bool IsThisMachineAlone(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) && uri.IsLoopback;
}
