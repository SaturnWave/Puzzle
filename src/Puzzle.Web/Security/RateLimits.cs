using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Puzzle.Web.Api;

namespace Puzzle.Web.Security;

/// <summary>
/// The public mode's limits on how fast requests may come. One client address gets a generous number of requests,
/// which a person playing never meets, and only a few tries at a secret (a password or the owner key). All addresses
/// together get a bounded number of tries at a secret too: someone who keeps changing address then meets that bound,
/// not the server's processor. The limits are asked in this order, so one address that floods is stopped by its own
/// limit before it can use up what all share.
/// </summary>
internal static class RateLimits
{
    /// <summary>Tries at a secret one address gets a minute.</summary>
    internal const int SecretChecksPerAddressPerMinute = 10;

    /// <summary>
    /// Tries at a secret all addresses get a minute between them. A password check costs a fraction of a second of one
    /// core, so even a flood from ever new addresses keeps the hashing to a small share of one core; a handful of
    /// players signing in never comes near it. While a flood lasts, signing in may be refused; playing goes on.
    /// </summary>
    internal const int SecretChecksPerMinute = 60;

    /// <summary>A move a second is fast play; this leaves room for a household behind one address.</summary>
    private const int RequestsPerMinute = 600;

    /// <summary>The one partition of a limit all addresses share.</summary>
    private const string Everyone = "everyone";

    /// <summary>The partition of a request a limit does not count; no address is spelled like it.</summary>
    private const string NotCounted = "";

    public static void AddPuzzleRateLimits(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.OnRejected = RefuseAsync;
        options.GlobalLimiter = CreateLimiter();
    });

    /// <summary>Marks an endpoint as one that checks a secret, so the limits on those tries hold for it.</summary>
    public static TBuilder LimitSecretChecks<TBuilder>(this TBuilder endpoint)
        where TBuilder : IEndpointConventionBuilder => endpoint.WithMetadata(new SecretCheckingEndpoint());

    /// <summary>Every limit a request must pass, in the order they are asked.</summary>
    internal static PartitionedRateLimiter<HttpContext> CreateLimiter() => PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context => PerMinute(ClientIp.PartitionOf(context), RequestsPerMinute)),
        ForSecretChecks(context => PerMinute(ClientIp.PartitionOf(context), SecretChecksPerAddressPerMinute)),
        ForSecretChecks(_ => PerMinute(Everyone, SecretChecksPerMinute)));

    /// <summary>A limit that counts only the requests to endpoints that check a secret.</summary>
    private static PartitionedRateLimiter<HttpContext> ForSecretChecks(Func<HttpContext, RateLimitPartition<string>> limit) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context => ChecksASecret(context) ? limit(context) : RateLimitPartition.GetNoLimiter(NotCounted));

    private static bool ChecksASecret(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<SecretCheckingEndpoint>() is not null;

    private static RateLimitPartition<string> PerMinute(string partition, int requests) => RateLimitPartition.GetFixedWindowLimiter(
        partition,
        _ => new FixedWindowRateLimiterOptions { PermitLimit = requests, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });

    private static async ValueTask RefuseAsync(OnRejectedContext context, CancellationToken cancellation)
    {
        int? retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan wait)
            ? (int)Math.Ceiling(wait.TotalSeconds)
            : null;
        await PlayerErrors.WriteAsync(context.HttpContext, PlayerError.TooManyRequests, retryAfterSeconds);
    }

    /// <summary>Endpoint metadata: the endpoint checks a password or the owner key.</summary>
    private sealed class SecretCheckingEndpoint;
}
