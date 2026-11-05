using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Web.Api;

namespace Puzzle.Web.Security;

/// <summary>
/// The public mode's two sign-ins, each a cookie of its own: a player's, which holds their player id and the sign-in
/// stamp their account had, and the owner's, which opens the lab. Both cookies are HttpOnly, Secure and SameSite=Lax,
/// and slide: an active browser stays signed in, an idle one for about thirty days. A player's cookie stops signing in
/// when the account is deleted or given a new password; the owner's when the key changes.
/// </summary>
internal static class PuzzleAuth
{
    public const string PlayerScheme = "Player";
    public const string OwnerScheme = "Owner";

    /// <summary>The policy of the player's game endpoints.</summary>
    public const string PlayerPolicy = "Player";

    /// <summary>The policy of everything the lab can do, in the public mode.</summary>
    public const string OwnerPolicy = "Owner";

    /// <summary>Where the owner types the key.</summary>
    public const string OwnerSignInPath = "/lab/sign-in";

    private const string PlayerIdClaim = "puzzle:player";
    private const string PlayerStampClaim = "puzzle:stamp";
    private const string OwnerStampClaim = "puzzle:owner";

    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    /// <param name="ownerKey">The key of the lab; absent when the lab is switched off, and then nothing can sign in as the owner.</param>
    public static void AddPuzzleAuth(this IServiceCollection services, OwnerKey? ownerKey)
    {
        AuthenticationBuilder authentication = services.AddAuthentication();
        authentication.AddCookie(PlayerScheme, options =>
        {
            ApplyCookieRules(options, "puzzle.player");
            options.Events.OnValidatePrincipal = KeepOnlyCurrentSignInsAsync;
            options.Events.OnRedirectToLogin = context => PlayerErrors.WriteAsync(context.HttpContext, PlayerError.SignInRequired);
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(PlayerPolicy, policy => policy.AddAuthenticationSchemes(PlayerScheme).RequireAuthenticatedUser());
        if (ownerKey is null)
        {
            return;
        }

        authentication.AddCookie(OwnerScheme, options =>
        {
            ApplyCookieRules(options, "puzzle.owner");
            options.Events.OnValidatePrincipal = context => KeepOnlyTheCurrentKeyAsync(context, ownerKey);
            options.Events.OnRedirectToLogin = AskOwnerToSignInAsync;
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(OwnerPolicy, policy => policy.AddAuthenticationSchemes(OwnerScheme).RequireAuthenticatedUser());
    }

    /// <summary>
    /// Signs the browser in as the player of <paramref name="account"/>, for as long as the account keeps the sign-in
    /// stamp it has now; the cookie outlives the browser session.
    /// </summary>
    public static Task SignInPlayerAsync(HttpContext context, PlayerAccount account) => context.SignInAsync(
        PlayerScheme,
        PrincipalWith(PlayerScheme, new Claim(PlayerIdClaim, account.PlayerId.Value), new Claim(PlayerStampClaim, account.SignInStamp.Value)),
        new AuthenticationProperties { IsPersistent = true });

    /// <summary>Signs the browser in as the owner, for as long as <paramref name="key"/> stays the configured key.</summary>
    public static Task SignInOwnerAsync(HttpContext context, OwnerKey key) => context.SignInAsync(
        OwnerScheme,
        PrincipalWith(OwnerScheme, new Claim(OwnerStampClaim, key.CookieStamp)),
        new AuthenticationProperties { IsPersistent = true });

    /// <summary>The player id a signed-in player's cookie holds; false for anyone else.</summary>
    public static bool TryGetPlayerId(ClaimsPrincipal user, [NotNullWhen(true)] out DeviceId? playerId) =>
        DeviceId.TryCreate(user.FindFirstValue(PlayerIdClaim), out playerId);

    private static ClaimsPrincipal PrincipalWith(string scheme, params Claim[] claims) => new(new ClaimsIdentity(claims, scheme));

    private static void ApplyCookieRules(CookieAuthenticationOptions options, string cookieName)
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = CookieLifetime;
        options.SlidingExpiration = true;
    }

    /// <summary>
    /// A cookie signs its player in only while their account exists and still has the stamp the cookie remembers: one
    /// of a deleted player, or from before the owner gave the account a new password, signs nobody in.
    /// </summary>
    private static Task KeepOnlyCurrentSignInsAsync(CookieValidatePrincipalContext context)
    {
        PlayerStore store = context.HttpContext.RequestServices.GetRequiredService<PlayerStore>();
        bool current = context.Principal is { } user
            && TryGetPlayerId(user, out DeviceId? playerId)
            && store.TryFind(playerId, out PlayerAccount? account)
            && user.FindFirstValue(PlayerStampClaim) == account.SignInStamp.Value;
        return current ? Task.CompletedTask : RejectAsync(context, PlayerScheme);
    }

    private static Task KeepOnlyTheCurrentKeyAsync(CookieValidatePrincipalContext context, OwnerKey key)
    {
        bool current = context.Principal?.FindFirstValue(OwnerStampClaim) == key.CookieStamp;
        return current ? Task.CompletedTask : RejectAsync(context, OwnerScheme);
    }

    private static Task RejectAsync(CookieValidatePrincipalContext context, string scheme)
    {
        context.RejectPrincipal();
        return context.HttpContext.SignOutAsync(scheme);
    }

    /// <summary>The lab's page sends the owner to the key form; the lab's API answers as it answers every refusal, with a JSON string.</summary>
    private static Task AskOwnerToSignInAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        HttpContext http = context.HttpContext;
        if (!http.Request.Path.StartsWithSegments("/api"))
        {
            http.Response.Redirect(OwnerSignInPath);
            return Task.CompletedTask;
        }

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return http.Response.WriteAsJsonAsync($"The lab of a public server is for its owner: sign in at {OwnerSignInPath}.", http.RequestAborted);
    }
}
