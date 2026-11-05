namespace Puzzle.Web.Security;

/// <summary>
/// Keeps the lab off the internet. The lab asks nobody to sign in: it is for this machine and the home network. A
/// server reached through a tunnel while it is still in the lab mode — <c>Puzzle__Mode</c> was forgotten — would hand
/// the level picker, the bots, the solver and everyone's recordings to whoever has the address. A tunnel or a reverse
/// proxy marks what it passes on with headers no browser sends by itself, so a request that carries one is refused,
/// with the way out in the answer; a request that reaches the server directly is served as it always was.
/// </summary>
/// <remarks>
/// Whoever sits behind the relay cannot take the headers off: the relay adds them after the request left the browser.
/// A relay that passes bytes on without adding any header (a forwarded port) is not seen here.
/// </remarks>
internal sealed class DirectOnly
{
    /// <summary>What the 403 says: why, and the setting that serves players through a tunnel. In the lab's language first.</summary>
    private const string Refusal =
        "Laboratuvar tünel ya da vekil sunucu (proxy) üzerinden sunulmaz: giriş istemez, yalnızca bu bilgisayar ve yerel ağ içindir.\n"
        + "Oyunculara açmak için sunucuyu Puzzle__Mode=Public ayarıyla başlat.\n\n"
        + "The lab is not served through a tunnel or a proxy: it asks for no sign-in and is for this machine and the local network only.\n"
        + "To serve players, start the server with Puzzle__Mode=Public.\n";

    /// <summary>
    /// The headers a relay adds: Cloudflare's own, the ones reverse proxies and tunnels use to pass on the client's
    /// address, scheme and host, their standard form, and Tailscale's own (Serve and Funnel). One of them is enough.
    /// </summary>
    private static readonly string[] RelayHeaders =
    [
        "CF-Connecting-IP", "CF-Ray", "CF-Visitor",
        "X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host", "X-Real-IP",
        "Forwarded",
        .. TailscaleHeaders.All,
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<DirectOnly> _logger;

    /// <summary>1 once the log was told; the owner needs the hint once, and a stranger's requests must not fill the log.</summary>
    private int _logged;

    public DirectOnly(RequestDelegate next, ILogger<DirectOnly> logger)
    {
        _next = next;
        _logger = logger;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (RelayHeaders.FirstOrDefault(context.Request.Headers.ContainsKey) is not { } relayHeader)
        {
            return _next(context);
        }

        if (Interlocked.Exchange(ref _logged, 1) == 0)
        {
            Log.LabRefusedRelayedRequest(_logger, relayHeader);
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";

        // A cache on the way must not keep this answer for an address that serves a page once the mode is set.
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsync(Refusal, context.RequestAborted);
    }
}
