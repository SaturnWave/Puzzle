namespace Puzzle.Tooling.Players;

/// <summary>What giving a player a new password came to. Closed set (private constructor).</summary>
public abstract record ChangePasswordOutcome
{
    private ChangePasswordOutcome()
    {
    }

    /// <summary>The account as it now is: the new hash, a new sign-in stamp, the progress it had.</summary>
    public sealed record Changed(PlayerAccount Account) : ChangePasswordOutcome;

    /// <summary>The password is outside <see cref="PasswordRules"/>.</summary>
    public sealed record PasswordRefused : ChangePasswordOutcome;

    /// <summary>No account has the username.</summary>
    public sealed record NoSuchPlayer : ChangePasswordOutcome;
}

/// <summary>
/// Gives a player a new password on the owner's word (<c>puzzle players password</c>): for a password that was
/// forgotten, mistyped when the account was made, or may be known to someone else. The player's progress and attempt
/// records stay; every browser signed in with the old password is signed out.
/// </summary>
public static class PlayerPasswordChange
{
    /// <exception cref="IOException">The change could not be written; the account is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store; the account is as it was.</exception>
    public static ChangePasswordOutcome Change(PlayerStore store, PasswordHasher hasher, Username username, string? password)
    {
        if (!PasswordRules.Allows(password))
        {
            return new ChangePasswordOutcome.PasswordRefused();
        }

        if (!store.TryFind(username, out PlayerAccount? account))
        {
            return new ChangePasswordOutcome.NoSuchPlayer();
        }

        return new ChangePasswordOutcome.Changed(store.ChangePassword(account.PlayerId, hasher.Hash(password)));
    }
}
