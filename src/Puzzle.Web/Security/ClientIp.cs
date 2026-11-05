using System.Net;
using System.Net.Sockets;

namespace Puzzle.Web.Security;

/// <summary>
/// The address a request really comes from, for the rate limits. Behind a tunnel every request reaches the server from
/// the tunnel's proxy on this machine — Tailscale, or cloudflared — which names the real client in
/// <c>X-Forwarded-For</c>: Tailscale replaces the header with the one address it saw, Cloudflare appends the address
/// that connected to it, so the last entry is the proxy's own word either way. No other header is believed: a proxy
/// passes on whatever else the client wrote (through Tailscale, a <c>CF-Connecting-IP</c> is the client's own
/// invention, and would let it pick a new address for every request). And the header is believed only when the
/// connection itself comes from this machine: anyone connecting directly could write whatever they like into it.
/// </summary>
internal static class ClientIp
{
    private const string ForwardedForHeader = "X-Forwarded-For";

    /// <summary>
    /// What the rate limits count requests by: an IPv4 address, or the first 64 bits of an IPv6 one, since a single
    /// home or phone is handed a whole /64 to pick addresses from.
    /// </summary>
    public static string PartitionOf(HttpContext context)
    {
        IPAddress address = Of(context);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? Convert.ToHexString(address.GetAddressBytes(), 0, 8)
            : address.ToString();
    }

    /// <summary>The client's address: the one a local proxy names, or else the connection's own.</summary>
    public static IPAddress Of(HttpContext context)
    {
        // Only a server without sockets (an in-memory test host) has no remote address; all its requests count together.
        IPAddress peer = context.Connection.RemoteIpAddress ?? IPAddress.None;
        return IPAddress.IsLoopback(peer) ? NamedByLocalProxy(context.Request.Headers) ?? peer : peer;
    }

    private static IPAddress? NamedByLocalProxy(IHeaderDictionary headers)
    {
        // Each proxy on the way appends the address it saw: the last entry is the one the local proxy vouches for.
        string? last = headers[ForwardedForHeader].ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        return IPAddress.TryParse(last, out IPAddress? forwarded) ? forwarded : null;
    }
}
