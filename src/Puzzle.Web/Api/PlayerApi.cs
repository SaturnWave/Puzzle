using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Puzzle.Core.Engine;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Web.Accounts;
using Puzzle.Web.Players;
using Puzzle.Web.Security;

namespace Puzzle.Web.Api;

/// <summary>
/// HTTP endpoints of the public mode's player page: the sign-in to an account the owner made, and the player's one
/// game. Nothing else of the server is open to a player, and nobody can register here.
/// </summary>
internal static class PlayerApi
{
    private const string LoggerCategory = "Puzzle.Web.Players";

    /// <summary>The form of an attempt's id on the wire: 32 hex digits.</summary>
    private const string AttemptIdFormat = "N";

    public static void MapPlayerApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder player = app.MapGroup("/api/player").AddEndpointFilter(AnswerUnsavedChangesAsync);
        player.MapGet("/me", DescribeSessionAsync);
        player.MapPost("/sign-in", SignInAsync).LimitSecretChecks();
        player.MapPost("/sign-out", SignOutAsync);

        RouteGroupBuilder game = player.MapGroup("/game").RequireAuthorization(PuzzleAuth.PlayerPolicy);
        game.MapGet(string.Empty, GetGame);
        game.MapPost("/attempts", StartAttempt);
        game.MapPost("/moves", PlayMove);
    }

    private static async Task<IResult> DescribeSessionAsync(HttpContext context, PlayerStore store)
    {
        AuthenticateResult signIn = await context.AuthenticateAsync(PuzzleAuth.PlayerScheme);
        string? username = signIn.Principal is { } user && TryFindAccount(user, store, out PlayerAccount? account)
            ? account.Username.Value
            : null;
        return Results.Ok(new PlayerSessionDto(username is not null, username));
    }

    private static async Task<IResult> SignInAsync(SignInRequest request, PlayerAccounts accounts, SecretChecks checks, HttpContext context)
    {
        SignInOutcome outcome = await checks.RunAsync(() => accounts.SignIn(request.Username, request.Password), context.RequestAborted);
        switch (outcome)
        {
            case SignInOutcome.SignedIn signedIn:
                await PuzzleAuth.SignInPlayerAsync(context, signedIn.Account);
                return Results.Ok(new PlayerSessionDto(SignedIn: true, signedIn.Account.Username.Value));
            case SignInOutcome.LockedOut lockedOut:
                return PlayerErrors.Refuse(PlayerError.LockedOut, (int)Math.Ceiling(lockedOut.RetryAfter.TotalSeconds));
            case SignInOutcome.WrongCredentials:
                return PlayerErrors.Refuse(PlayerError.InvalidCredentials);
            default:
                throw new UnreachableException("Unhandled sign-in outcome.");
        }
    }

    /// <summary>Signing out leaves the player's live attempt as it is: signing in again goes on with it.</summary>
    private static async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(PuzzleAuth.PlayerScheme);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static IResult GetGame(ClaimsPrincipal user, PlayerGames games) =>
        PuzzleAuth.TryGetPlayerId(user, out DeviceId? playerId)
            ? Results.Ok(games.Current(playerId))
            : PlayerErrors.Refuse(PlayerError.SignInRequired);

    private static IResult StartAttempt(StartAttemptRequest? request, ClaimsPrincipal user, PlayerGames games, ILoggerFactory loggers)
    {
        if (!PuzzleAuth.TryGetPlayerId(user, out DeviceId? playerId))
        {
            return PlayerErrors.Refuse(PlayerError.SignInRequired);
        }

        // What is not an attempt's id names no attempt, so it leaves none that is being played.
        Guid? leaving = Guid.TryParseExact(request?.Leaves, AttemptIdFormat, out Guid shown) ? shown : null;
        try
        {
            return games.Start(playerId, leaving) switch
            {
                StartOutcome.Started started => Results.Ok(started.Game),
                StartOutcome.TooManyAttempts refused => PlayerErrors.Refuse(PlayerError.TooManyAttempts, (int)Math.Ceiling(refused.RetryAfter.TotalSeconds)),
                StartOutcome unknown => throw new UnreachableException($"Unhandled start outcome {unknown.GetType().Name}."),
            };
        }
        catch (LevelInitializationException ex)
        {
            // A level of the public set cannot be started: the owner's to fix, nothing a player can act on.
            Log.PublicLevelNotStarted(loggers.CreateLogger(LoggerCategory), ex);
            return PlayerErrors.Refuse(PlayerError.ServerError);
        }
    }

    private static IResult PlayMove(PlayerMoveRequest request, ClaimsPrincipal user, PlayerGames games)
    {
        if (!PuzzleAuth.TryGetPlayerId(user, out DeviceId? playerId))
        {
            return PlayerErrors.Refuse(PlayerError.SignInRequired);
        }

        if (!TryReadMove(request, out PlayerMove? move))
        {
            return PlayerErrors.Refuse(PlayerError.InvalidMove);
        }

        return games.Play(playerId, move) switch
        {
            MoveOutcome.Played played => Results.Ok(played.Result),
            MoveOutcome.NoLiveAttempt => PlayerErrors.Refuse(PlayerError.NoLiveAttempt),
            MoveOutcome.AttemptChanged => PlayerErrors.Refuse(PlayerError.AttemptChanged),
            MoveOutcome.AttemptOver => PlayerErrors.Refuse(PlayerError.AttemptOver),
            MoveOutcome unknown => throw new UnreachableException($"Unhandled move outcome {unknown.GetType().Name}."),
        };
    }

    private static bool TryFindAccount(ClaimsPrincipal user, PlayerStore store, [NotNullWhen(true)] out PlayerAccount? account)
    {
        account = null;
        return PuzzleAuth.TryGetPlayerId(user, out DeviceId? playerId) && store.TryFind(playerId, out account);
    }

    /// <summary>
    /// The move a request describes, with the attempt and the board it was made on; false when a part is missing or
    /// the shape is no move at all.
    /// </summary>
    private static bool TryReadMove(PlayerMoveRequest request, [NotNullWhen(true)] out PlayerMove? move)
    {
        move = null;

        // The JSON reader leaves a part the request did not send as null.
        if (request is not { Kind: { } kind, From: { } from, MovesUsed: >= 0 and int movesUsed }
            || !Guid.TryParseExact(request.Attempt, AttemptIdFormat, out Guid attempt))
        {
            return false;
        }

        try
        {
            move = new PlayerMove(attempt, movesUsed, StateMapper.ToMove(new MoveDto(kind, from, request.To)));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// A change the player store could not write (a full disk, a file another program holds) is logged here, once, and
    /// answered as "not saved, try again"; the store is then as it was before the request.
    /// </summary>
    private static async ValueTask<object?> AnswerUnsavedChangesAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        try
        {
            return await next(invocation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HttpContext context = invocation.HttpContext;
            ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
            Log.PlayerChangeNotSaved(logger, context.Request.Path, ex);
            return PlayerErrors.Refuse(PlayerError.NotSaved);
        }
    }
}
