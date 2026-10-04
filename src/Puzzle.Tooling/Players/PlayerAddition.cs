namespace Puzzle.Tooling.Players;

/// <summary>What adding a player came to. Closed set (private constructor).</summary>
public abstract record AddPlayerOutcome
{
    private AddPlayerOutcome()
    {
    }

    public sealed record Added(PlayerAccount Account) : AddPlayerOutcome;

    /// <summary>The password is outside <see cref="PasswordRules"/>.</summary>
    public sealed record PasswordRefused : AddPlayerOutcome;

    /// <summary>Another account has the username, in whatever case.</summary>
    public sealed record UsernameTaken : AddPlayerOutcome;
}

/// <summary>
/// Makes the accounts. An account comes to be here alone: on the owner's word (<c>puzzle players add</c>), or as one
/// of the ready-made accounts the owner listed for the public test. Nobody registers on the public server.
/// </summary>
public static class PlayerAddition
{
    /// <summary>
    /// Makes an account for <paramref name="username"/> with a new random player id, the hash of
    /// <paramref name="password"/>, a sign-in stamp of its own and no progress.
    /// </summary>
    /// <param name="now">When the account is made.</param>
    /// <exception cref="IOException">The account could not be written; nothing was added.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store; nothing was added.</exception>
    public static AddPlayerOutcome Add(PlayerStore store, PasswordHasher hasher, Username username, string? password, DateTimeOffset now)
    {
        if (!PasswordRules.Allows(password))
        {
            return new AddPlayerOutcome.PasswordRefused();
        }

        return AddAccount(store, hasher, username, password, now);
    }

    /// <summary>
    /// Makes the account of every one of <paramref name="players"/> that has none yet, and returns the accounts it
    /// made, in the order given. An account that is there already is left as it is: its password — also when the list
    /// now names another one —, its progress and its sign-ins. A ready-made account is an account like any other (the
    /// hash of its password, a random player id, a sign-in stamp of its own) but for the one rule
    /// <see cref="ReadyMadePlayer"/> skips.
    /// </summary>
    /// <param name="now">When the accounts are made.</param>
    /// <exception cref="IOException">An account could not be written; the ones before it were added.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write the store.</exception>
    public static IReadOnlyList<PlayerAccount> AddMissing(PlayerStore store, PasswordHasher hasher, IReadOnlyList<ReadyMadePlayer> players, DateTimeOffset now)
    {
        var made = new List<PlayerAccount>();
        foreach (ReadyMadePlayer player in players)
        {
            if (AddAccount(store, hasher, player.Username, player.Password, now) is AddPlayerOutcome.Added added)
            {
                made.Add(added.Account);
            }
        }

        return made;
    }

    /// <summary>The store is asked about the name before the costly hash, and asks again as it adds.</summary>
    private static AddPlayerOutcome AddAccount(PlayerStore store, PasswordHasher hasher, Username username, string password, DateTimeOffset now)
    {
        if (store.TryFind(username, out _))
        {
            return new AddPlayerOutcome.UsernameTaken();
        }

        var account = new PlayerAccount(username, PlayerAccount.NewPlayerId(), hasher.Hash(password), SignInStamp.New(), now, PlayerProgress.None);
        return store.TryAdd(account) ? new AddPlayerOutcome.Added(account) : new AddPlayerOutcome.UsernameTaken();
    }
}
