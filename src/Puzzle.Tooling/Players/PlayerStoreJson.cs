using System.Text.Json;
using System.Text.Json.Serialization;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Tooling.Players;

/// <summary>
/// The JSON form of the player store: one object with the schema version and the accounts in registration order,
/// camelCase fields, times in UTC. Unknown fields are ignored, so a later schema may add fields.
/// </summary>
internal static class PlayerStoreJson
{
    /// <summary>
    /// Version of the file format. Readers accept every version up to this one; changes are additive. Version 2 added
    /// the sign-in stamp, the id of the level won last and the live attempt; a file of version 1 names none of them, and
    /// reads as accounts on their first stamp with no live attempt.
    /// </summary>
    public const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectNullableAnnotations = true,
        WriteIndented = true,
    };

    public static byte[] Serialize(IEnumerable<PlayerAccount> accounts) => JsonSerializer.SerializeToUtf8Bytes(
        new StoreFile { Schema = SchemaVersion, Accounts = accounts.Select(ToLine).ToArray() },
        Options);

    /// <exception cref="PlayerStoreFormatException">The text is not a valid store, or was written by a newer schema.</exception>
    public static PlayerAccount[] Parse(Stream json)
    {
        StoreFile file = Deserialize(json);
        if (file.Schema < 1 || file.Schema > SchemaVersion)
        {
            throw new PlayerStoreFormatException($"Schema version {file.Schema} is not supported; this build reads 1..{SchemaVersion}.");
        }

        try
        {
            return file.Accounts.Select(ToAccount).ToArray();
        }
        catch (ArgumentException ex)
        {
            throw new PlayerStoreFormatException($"Not a valid player account: {ex.Message}", ex);
        }
    }

    private static StoreFile Deserialize(Stream json)
    {
        try
        {
            return JsonSerializer.Deserialize<StoreFile>(json, Options)
                ?? throw new PlayerStoreFormatException("The file holds null instead of a player store.");
        }
        catch (JsonException ex)
        {
            throw new PlayerStoreFormatException($"Not a valid player store: {ex.Message}", ex);
        }
    }

    private static AccountLine ToLine(PlayerAccount account) => new()
    {
        Username = account.Username.Value,
        PlayerId = account.PlayerId.Value,
        PasswordHash = account.PasswordHash,
        SignInStamp = account.SignInStamp.Value,
        CreatedUtc = account.CreatedUtc,
        Progress = new ProgressLine
        {
            LevelsWon = account.Progress.LevelsWon,
            LastWonLevel = account.Progress.LastWonLevel?.Value,
            AttemptedLevel = account.Progress.AttemptedLevel,
            AttemptsStarted = account.Progress.AttemptsStarted,
            Live = account.Progress.Live is { } live ? ToLine(live) : null,
        },
    };

    private static LiveLine ToLine(SavedAttempt live) => new()
    {
        Id = live.Id,
        Level = live.Level.Value,
        LevelFingerprint = live.LevelFingerprint,
        Attempt = live.Attempt,
        BoardSeed = live.Seeds.Board,
        PlaySeed = live.Seeds.Play,
        StartedUtc = live.StartedUtc,
        Moves = live.Moves.Select(MoveNumbers.Of).ToArray(),
    };

    /// <exception cref="ArgumentException">A field holds a value its type refuses.</exception>
    private static PlayerAccount ToAccount(AccountLine? line) => line is null
        ? throw new ArgumentException("An account cannot be null.")
        : new PlayerAccount(
            new Username(line.Username),
            new DeviceId(line.PlayerId),
            line.PasswordHash,
            line.SignInStamp is { } stamp ? new SignInStamp(stamp) : SignInStamp.First,
            line.CreatedUtc,
            ToProgress(line.Progress));

    /// <exception cref="ArgumentException">A field holds a value its type refuses.</exception>
    private static PlayerProgress ToProgress(ProgressLine line) => new(
        line.LevelsWon,
        line.AttemptedLevel,
        line.AttemptsStarted,
        line.LastWonLevel is int lastWon ? new LevelId(lastWon) : null,
        line.Live is { } live ? ToSavedAttempt(live) : null);

    /// <exception cref="ArgumentException">A field holds a value its type refuses.</exception>
    private static SavedAttempt ToSavedAttempt(LiveLine line) => new(
        line.Id,
        new LevelId(line.Level),
        line.LevelFingerprint,
        line.Attempt,
        AttemptSeeds.WithFixedBoard(line.BoardSeed, line.PlaySeed),
        line.StartedUtc,
        line.Moves.Select(MoveNumbers.ToMove).ToArray());

    // Wire shapes. Declaration order is field order in the file; "required" makes a missing field a format error.
    private sealed class StoreFile
    {
        public required int Schema { get; init; }

        public required AccountLine[] Accounts { get; init; }
    }

    private sealed class AccountLine
    {
        public required string Username { get; init; }

        public required string PlayerId { get; init; }

        public required string PasswordHash { get; init; }

        /// <summary>Absent in a file of schema 1: the account is on its first stamp.</summary>
        public string? SignInStamp { get; init; }

        public required DateTimeOffset CreatedUtc { get; init; }

        public required ProgressLine Progress { get; init; }
    }

    private sealed class ProgressLine
    {
        public required int LevelsWon { get; init; }

        /// <summary>Absent before the first win, and in a file of schema 1.</summary>
        public int? LastWonLevel { get; init; }

        /// <summary>Absent before the first attempt on the level the player is on.</summary>
        public string? AttemptedLevel { get; init; }

        public required int AttemptsStarted { get; init; }

        /// <summary>Absent while the player is in no attempt.</summary>
        public LiveLine? Live { get; init; }
    }

    /// <summary>A live attempt; its moves are spelled as in the attempt records (<see cref="MoveNumbers"/>).</summary>
    private sealed class LiveLine
    {
        public required Guid Id { get; init; }

        public required int Level { get; init; }

        public required string LevelFingerprint { get; init; }

        public required int Attempt { get; init; }

        public required ulong BoardSeed { get; init; }

        public required ulong PlaySeed { get; init; }

        public required DateTimeOffset StartedUtc { get; init; }

        public required int[][] Moves { get; init; }
    }
}
