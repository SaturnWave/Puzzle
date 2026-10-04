using Microsoft.AspNetCore.Authentication;
using Puzzle.Web.Security;

namespace Puzzle.Web.Api;

/// <summary>
/// The owner's way into the lab of a public server: a key form, the sign-in it posts to, and the lab's page under
/// <c>/lab/</c>. Mapped only when an owner key is configured; without one the lab does not exist.
/// </summary>
internal static class OwnerApi
{
    private const string LabPath = "/lab";
    private const string LabStartFile = "index.html";
    private const string SignInPage = "owner/sign-in.html";
    private const string SignInScript = "owner/sign-in.js";
    private const string LoggerCategory = "Puzzle.Web.Owner";

    /// <summary>The key form with its script, and the sign-in: open to anyone, since nobody is the owner before the key is checked.</summary>
    public static void MapOwnerSignIn(this IEndpointRouteBuilder app)
    {
        app.MapGet(PuzzleAuth.OwnerSignInPath, (HttpContext context) => WebRootFiles.Serve(context, SignInPage, WebRootFiles.Revalidated));
        app.MapGet(PuzzleAuth.OwnerSignInPath + ".js", (HttpContext context) => WebRootFiles.Serve(context, SignInScript, WebRootFiles.Revalidated));
        app.MapPost("/api/owner/sign-in", SignInAsync).LimitSecretChecks();
        app.MapPost("/api/owner/sign-out", SignOutAsync);
    }

    /// <summary>
    /// The lab's own files, which in the lab mode sit at the site's root, under <c>/lab/</c>; the page finds its script
    /// and stylesheet beside itself there. Map it inside the owner's group.
    /// </summary>
    public static void MapLabPage(this IEndpointRouteBuilder lab) => lab.MapGet(LabPath + "/{file?}", ServeLabFile);

    private static async Task<IResult> SignInAsync(OwnerSignInRequest request, OwnerKey key, SecretChecks checks, HttpContext context, ILoggerFactory loggers)
    {
        bool isTheKey = await checks.RunAsync(() => key.Matches(request.Key), context.RequestAborted);
        if (!isTheKey)
        {
            Log.OwnerSignInRefused(loggers.CreateLogger(LoggerCategory));
            return Results.Json("The owner key is wrong.", statusCode: StatusCodes.Status401Unauthorized);
        }

        await PuzzleAuth.SignInOwnerAsync(context, key);
        return Results.NoContent();
    }

    private static async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(PuzzleAuth.OwnerScheme);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static IResult ServeLabFile(string? file, HttpContext context)
    {
        if (file is null)
        {
            // The page's relative links need the trailing slash; the route alone cannot tell "/lab" from "/lab/".
            return context.Request.Path.Value == LabPath
                ? Results.Redirect(LabPath + "/")
                : WebRootFiles.Serve(context, LabStartFile, WebRootFiles.OwnerOnly);
        }

        // One file name, nothing nested: the web root's folders hold the public mode's own pages, which are not the lab.
        bool nested = file.AsSpan().IndexOfAny('/', '\\') >= 0;
        return nested ? Results.NotFound() : WebRootFiles.Serve(context, file, WebRootFiles.OwnerOnly);
    }
}
