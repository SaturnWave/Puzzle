using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Puzzle.Web.Api;

namespace Puzzle.Web.Security;

/// <summary>
/// Keeps a public server's pages and secrets off plain HTTP. A Cloudflare Tunnel takes <c>http://</c> requests as well
/// as <c>https://</c> ones and says which it was in X-Forwarded-Proto. A read that came over plain HTTP is sent to the
/// same address on HTTPS; anything else is refused: its body, a password perhaps, has crossed the network in clear
/// already, and answering as usual would let a page go on working that way. A request for which no proxy on this
/// machine names a scheme is served as it comes: the owner's own browser on this machine, and what Tailscale passes
/// on when it says nothing of the scheme (Funnel takes HTTPS alone and says so; Serve can pass on plain HTTP from
/// inside the tailnet, and then names none).
/// </summary>
/// <remarks>Use it after the forwarded-headers middleware, which is what believes the local proxy and nobody else.</remarks>
internal static class HttpsOnly
{
    public static IApplicationBuilder UseHttpsOnly(this IApplicationBuilder app) => app.Use(KeepOffPlainHttpAsync);

    private static Task KeepOffPlainHttpAsync(HttpContext context, RequestDelegate next)
    {
        HttpRequest request = context.Request;
        if (!CameOverPlainHttpThroughTheProxy(request))
        {
            return next(context);
        }

        bool isRead = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
        if (!isRead || !request.Host.HasValue)
        {
            return PlayerErrors.WriteAsync(context, PlayerError.HttpsRequired);
        }

        string onHttps = UriHelper.BuildAbsolute(Uri.UriSchemeHttps, request.Host, request.PathBase, request.Path, request.QueryString);
        context.Response.Redirect(onHttps, permanent: true, preserveMethod: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The forwarded-headers middleware takes the scheme from a proxy it trusts and leaves the connection's own scheme
    /// in X-Original-Proto. So that header on a request that is still not HTTPS means the proxy itself said "http".
    /// </summary>
    private static bool CameOverPlainHttpThroughTheProxy(HttpRequest request) =>
        !request.IsHttps && request.Headers.ContainsKey(ForwardedHeadersDefaults.XOriginalProtoHeaderName);
}
