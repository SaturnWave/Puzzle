namespace Puzzle.Web.Api;

// Wire contract of the public mode's player page. Nothing here names a seed, a level file, a session id or a player
// id: the server chooses and keeps all of those, and the page only shows the game and sends moves.

/// <summary>Whether this browser is signed in, and as whom.</summary>
/// <param name="Username">The signed-in player's name; absent when nobody is signed in.</param>
public sealed record PlayerSessionDto(bool SignedIn, string? Username);

public sealed record SignInRequest(string? Username, string? Password);

/// <summary>
/// Where the player stands. <see cref="LevelNumber"/> is the 1-based level they are on (the last one once
/// <see cref="Finished"/>), <see cref="LevelCount"/> the size of the set, <see cref="AttemptsOnLevel"/> the attempts
/// on that level they have played a move in; an attempt only opened is not counted yet. <see cref="Finished"/>: every
/// level is won.
/// </summary>
public sealed record PlayerProgressDto(int LevelNumber, int LevelCount, int AttemptsOnLevel, bool Finished);

/// <summary>
/// The player's live attempt, being played or just decided. <see cref="Id"/> names it: a move says which attempt it was
/// made on. <see cref="LevelNumber"/> is the level it is on, which after a win is one behind
/// <see cref="PlayerProgressDto.LevelNumber"/>; <see cref="AttemptNumber"/> counts the player's attempts on that level
/// from 1. <see cref="State"/> is the board as the lab draws it; its status is "Playing", "Won", "LostOutOfMoves" or
/// "LostNoMovesPossible".
/// </summary>
public sealed record PlayerAttemptDto(string Id, int LevelNumber, int AttemptNumber, StateDto State);

/// <summary>The player's game: progress, and the live attempt when there is one.</summary>
public sealed record PlayerGameDto(PlayerProgressDto Progress, PlayerAttemptDto? Attempt);

/// <summary>
/// Asks for the player's next attempt. <see cref="Leaves"/> is the <see cref="PlayerAttemptDto.Id"/> of the attempt
/// the page was showing, absent when it showed none: an attempt that is being played is left, and recorded as
/// abandoned, only by a request that names it. The body may be left out altogether.
/// </summary>
public sealed record StartAttemptRequest(string? Leaves);

/// <summary>
/// A move, with what the page was looking at when the player made it: <see cref="Attempt"/> is the
/// <see cref="PlayerAttemptDto.Id"/> of the attempt on the page, <see cref="MovesUsed"/> the moves that attempt had
/// used there. A move made on another attempt, or on an earlier board of the same one, is refused ("attemptChanged")
/// and never played blind. <see cref="Kind"/> is "swap" or "tap"; <see cref="To"/> is absent for a tap.
/// </summary>
public sealed record PlayerMoveRequest(string? Attempt, int? MovesUsed, string? Kind, PositionDto? From, PositionDto? To);

/// <summary>
/// The answer to a move. A move the engine refuses (nothing would match) is not an error: <see cref="Accepted"/> is
/// false, <see cref="Events"/> is empty and nothing changed. <see cref="Progress"/> already counts a level this move won.
/// </summary>
public sealed record PlayerMoveResultDto(bool Accepted, IReadOnlyList<EventDto> Events, StateDto State, PlayerProgressDto Progress);

/// <summary>
/// A refusal: <see cref="Error"/> is a stable code for the page to act on, <see cref="Message"/> a sentence in Turkish
/// it can show as it is. <see cref="RetryAfterSeconds"/> comes with "lockedOut", "tooManyRequests" and
/// "tooManyAttempts".
/// </summary>
public sealed record ApiErrorDto(string Error, string Message, int? RetryAfterSeconds = null);

/// <summary>The owner's sign-in to the lab of a public server.</summary>
public sealed record OwnerSignInRequest(string? Key);

/// <summary>
/// One player of the public mode in the owner's recordings panel. <see cref="LevelsWon"/> is their progress,
/// <see cref="Records"/> every recorded attempt of theirs, <see cref="Play"/> the comparable ones as the panel counts
/// them; <see cref="LastPlayedUtc"/> is absent when they have recorded nothing yet.
/// </summary>
public sealed record PlayerAttemptsDto(string Username, int LevelsWon, int Records, PlayDto Play, DateTimeOffset? LastPlayedUtc);
