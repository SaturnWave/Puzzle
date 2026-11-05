using System.Globalization;
using System.Text.Json;

namespace Puzzle.Web.Api;

/// <summary>Every refusal the public mode answers a player's page with; the camelCase name is the code on the wire.</summary>
internal enum PlayerError
{
    InvalidCredentials,
    LockedOut,
    TooManyRequests,
    TooManyAttempts,
    SignInRequired,
    ClientHeaderRequired,
    HttpsRequired,
    BadRequest,
    InvalidMove,
    NoLiveAttempt,
    AttemptChanged,
    AttemptOver,
    NotSaved,
    ServerError,
}

/// <summary>
/// The status and the Turkish sentence of each <see cref="PlayerError"/>, in one place. A refusal that knows when to
/// try again says so twice: in its body, for the page, and in the Retry-After header, for whatever else reads a 429.
/// </summary>
internal static class PlayerErrors
{
    /// <summary>The refusal as an endpoint's result.</summary>
    public static IResult Refuse(PlayerError error, int? retryAfterSeconds = null) => new Refusal(error, retryAfterSeconds);

    /// <summary>Answers the request with <paramref name="error"/> from a middleware, where no endpoint result is at hand.</summary>
    public static Task WriteAsync(HttpContext context, PlayerError error, int? retryAfterSeconds = null)
    {
        context.Response.StatusCode = StatusOf(error);
        if (retryAfterSeconds is int seconds)
        {
            context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return context.Response.WriteAsJsonAsync(Describe(error, retryAfterSeconds), context.RequestAborted);
    }

    public static ApiErrorDto Describe(PlayerError error, int? retryAfterSeconds = null) =>
        new(JsonNamingPolicy.CamelCase.ConvertName(error.ToString()), MessageOf(error), retryAfterSeconds);

    public static int StatusOf(PlayerError error) => error switch
    {
        PlayerError.ClientHeaderRequired or PlayerError.HttpsRequired or PlayerError.BadRequest or PlayerError.InvalidMove => StatusCodes.Status400BadRequest,
        PlayerError.InvalidCredentials or PlayerError.SignInRequired => StatusCodes.Status401Unauthorized,
        PlayerError.NoLiveAttempt or PlayerError.AttemptChanged or PlayerError.AttemptOver => StatusCodes.Status409Conflict,
        PlayerError.LockedOut or PlayerError.TooManyRequests or PlayerError.TooManyAttempts => StatusCodes.Status429TooManyRequests,
        PlayerError.NotSaved => StatusCodes.Status503ServiceUnavailable,
        PlayerError.ServerError => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown player error."),
    };

    private static string MessageOf(PlayerError error) => error switch
    {
        PlayerError.InvalidCredentials => "Kullanıcı adı ya da şifre yanlış.",
        PlayerError.LockedOut => "Çok fazla hatalı deneme. Birkaç dakika sonra tekrar dene.",
        PlayerError.TooManyRequests => "Çok fazla istek. Biraz sonra tekrar dene.",
        PlayerError.TooManyAttempts => "Kısa sürede çok fazla yeni deneme başlattın. Birkaç dakika sonra tekrar dene.",
        PlayerError.SignInRequired => "Önce giriş yap.",
        PlayerError.ClientHeaderRequired => "İstek bu sayfadan gelmiyor.",
        PlayerError.HttpsRequired => "Bu adres yalnızca güvenli bağlantıyla (https) kullanılır.",
        PlayerError.BadRequest => "İstek anlaşılamadı.",
        PlayerError.InvalidMove => "Geçersiz hamle.",
        PlayerError.NoLiveAttempt => "Devam eden bir deneme yok.",
        PlayerError.AttemptChanged => "Oyun bu sayfadakinden ileride. Son hâli yükleniyor.",
        PlayerError.AttemptOver => "Bu deneme bitti.",
        PlayerError.NotSaved => "Şu anda kaydedilemedi. Biraz sonra tekrar dene.",
        PlayerError.ServerError => "Bir şeyler ters gitti. Biraz sonra tekrar dene.",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown player error."),
    };

    /// <summary>An endpoint's refusal: written exactly as a middleware's is.</summary>
    private sealed class Refusal(PlayerError error, int? retryAfterSeconds) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => WriteAsync(httpContext, error, retryAfterSeconds);
    }
}
