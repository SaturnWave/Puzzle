using System.Diagnostics.CodeAnalysis;

namespace Puzzle.Tooling.Players;

/// <summary>
/// One of the ready-made accounts of the public test, as the test settings file lists it: a username, and the test
/// password the owner chose for it. The password may be shorter than <see cref="PasswordRules.MinLength"/>; that is
/// the one rule these accounts skip.
/// </summary>
public sealed record ReadyMadePlayer
{
    /// <exception cref="ArgumentException"><paramref name="password"/> is not one a sign-in takes: empty, or too long.</exception>
    public ReadyMadePlayer(Username username, string password)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = PasswordRules.CanBeSignedInWith(password)
            ? password
            : throw new ArgumentException($"A ready-made account's password has 1..{PasswordRules.MaxLength} characters.", nameof(password));
    }

    public Username Username { get; }

    /// <summary>The password as the file spells it; the account keeps only its hash.</summary>
    public string Password { get; }

    /// <summary>The ready-made player with that name and password; false when the password is not one a sign-in takes.</summary>
    public static bool TryCreate(Username username, string? password, [NotNullWhen(true)] out ReadyMadePlayer? player)
    {
        player = PasswordRules.CanBeSignedInWith(password) ? new ReadyMadePlayer(username, password) : null;
        return player is not null;
    }

    /// <summary>Only the username: a ready-made player written to a log by accident must not spell out its password.</summary>
    public override string ToString() => Username.Value;
}
