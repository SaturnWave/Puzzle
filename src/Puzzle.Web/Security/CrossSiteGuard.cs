using Puzzle.Web.Api;

namespace Puzzle.Web.Security;

/// <summary>
/// Keeps other sites from making a signed-in browser change anything here. Every request that is not a plain read
/// must carry a header only this site's own scripts can add: a form on another site cannot set headers, and another
/// site's script may not without this server's leave, which it never gives. The cookies' SameSite=Lax is the first
/// line; this is the second.
/// </summary>
internal static class CrossSiteGuard
{
    /// <summary>The header the player page and the owner's key form send with every request; any value will do.</summary>
    public const string ClientHeader = "X-Puzzle-Client";

    public static IApplicationBuilder UseCrossSiteGuard(this IApplicationBuilder app) => app.Use(RefuseForeignChangesAsync);

    private static Task RefuseForeignChangesAsync(HttpContext context, RequestDelegate next)
    {
        bool allowed = IsRead(context.Request.Method) || ComesFromOurPages(context.Request.Headers);
        return allowed ? next(context) : PlayerErrors.WriteAsync(context, PlayerError.ClientHeaderRequired);
    }

    private static bool IsRead(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    /// <summary>The lab's client marks its requests with the device header instead; it is as out of a foreign page's reach.</summary>
    private static bool ComesFromOurPages(IHeaderDictionary headers) =>
        headers.ContainsKey(ClientHeader) || headers.ContainsKey(PuzzleApi.DeviceHeader);
}
