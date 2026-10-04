using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Puzzle.Web.Security;

/// <summary>
/// Serves single files of the web root from endpoints, where the public mode needs a file under another address or
/// behind a sign-in; the static file middleware can do neither.
/// </summary>
internal static class WebRootFiles
{
    /// <summary>For everyone, but asked for again each time: the pages change often, and Cloudflare must not keep its own copy.</summary>
    public const string Revalidated = "no-cache";

    /// <summary>For the owner alone: nothing may keep a copy, neither a cache on the way nor the browser.</summary>
    public const string OwnerOnly = "no-store";

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>The file at <paramref name="path"/> in the web root, or 404.</summary>
    public static IResult Serve(HttpContext context, string path, string cacheControl)
    {
        IFileInfo file = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider.GetFileInfo(path);
        if (!file.Exists || file.IsDirectory)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = cacheControl;
        string contentType = ContentTypes.TryGetContentType(path, out string? known) ? known : "application/octet-stream";
        return Results.Stream(file.CreateReadStream(), contentType, lastModified: file.LastModified);
    }
}
