namespace Puzzle.Web.Security;

/// <summary>
/// The headers Tailscale Serve and Funnel put on a request they pass on to this server, besides the X-Forwarded ones
/// every proxy uses. Tailscale takes these names off whatever the client sent and sets them itself: Funnel marks a
/// request that came from the internet, Serve names the tailnet user it came from (and, where the tailnet grants
/// any, the user's capabilities). One of them on a request means Tailscale passed it on. (A request from a tagged
/// device of the tailnet carries none of them.)
/// </summary>
internal static class TailscaleHeaders
{
    public static readonly IReadOnlyList<string> All =
    [
        "Tailscale-Funnel-Request",
        "Tailscale-User-Login", "Tailscale-User-Name", "Tailscale-User-Profile-Pic",
        "Tailscale-Headers-Info", "Tailscale-App-Capabilities",
    ];

    public static bool AreOn(HttpRequest request) => All.Any(request.Headers.ContainsKey);
}
