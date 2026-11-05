using Puzzle.Tooling.Players;

namespace Puzzle.Web.Accounts;

/// <summary>What a sign-in came to. Closed set (private constructor).</summary>
public abstract record SignInOutcome
{
    private SignInOutcome()
    {
    }

    public sealed record SignedIn(PlayerAccount Account) : SignInOutcome;

    /// <summary>No such username, or not its password: the two are never told apart.</summary>
    public sealed record WrongCredentials : SignInOutcome;

    /// <summary>Too many failures for this username; it may try again after <paramref name="RetryAfter"/>.</summary>
    public sealed record LockedOut(TimeSpan RetryAfter) : SignInOutcome;
}

/// <summary>
/// How a password is checked against an encoded hash: <see cref="PasswordHasher.Verify"/> on the server. The checks go
/// through this one seam so a test can count them, and the cost each one names.
/// </summary>
public delegate bool PasswordCheck(string password, string encodedHash);

/// <summary>
/// Checks the sign-ins of the public mode's players. Nobody registers here: an account is one the owner added with
/// <c>puzzle players add</c>, or one of the ready-made accounts of the public test, which the server makes at its
/// start (<see cref="PlayerAddition"/>).
/// </summary>
public sealed class PlayerAccounts
{
    private readonly PlayerStore _store;
    private readonly PasswordHasher _hasher;
    private readonly SignInLockouts _lockouts;
    private readonly ILogger<PlayerAccounts> _logger;
    private readonly PasswordCheck _check;

    /// <param name="check">How a password is checked; <see cref="PasswordHasher.Verify"/> when absent.</param>
    public PlayerAccounts(PlayerStore store, PasswordHasher hasher, SignInLockouts lockouts, ILogger<PlayerAccounts> logger, PasswordCheck? check = null)
    {
        _store = store;
        _hasher = hasher;
        _lockouts = lockouts;
        _logger = logger;
        _check = check ?? PasswordHasher.Verify;
    }

    /// <summary>
    /// Checks a username and password. A wrong password and a username nobody has get the same answer after the same
    /// work: one password check each, the unknown name's against the hasher's decoy.
    /// </summary>
    public SignInOutcome SignIn(string? username, string? password)
    {
        if (!Username.TryCreate(username?.Trim(), out Username? name) || !PasswordRules.CanBeSignedInWith(password))
        {
            return new SignInOutcome.WrongCredentials();
        }

        if (_lockouts.LockedFor(name) is TimeSpan left)
        {
            return new SignInOutcome.LockedOut(left);
        }

        string hashToCheck = _store.TryFind(name, out PlayerAccount? account) ? account.PasswordHash : _hasher.DecoyHash;
        if (!_check(password, hashToCheck) || account is null)
        {
            return Failed(name);
        }

        _lockouts.RecordSuccess(name);
        return new SignInOutcome.SignedIn(_hasher.NeedsRehash(account.PasswordHash) ? Rehashed(account, password) : account);
    }

    private SignInOutcome.WrongCredentials Failed(Username name)
    {
        if (_lockouts.RecordFailure(name))
        {
            Log.SignInLocked(_logger, SignInLockouts.MaxFailures, SignInLockouts.LockoutTime.TotalMinutes);
        }

        return new SignInOutcome.WrongCredentials();
    }

    /// <summary>
    /// The account with its password hashed again at today's cost. A store that cannot be written is no reason to turn
    /// the player away: the old hash stays, and the next sign-in tries again.
    /// </summary>
    private PlayerAccount Rehashed(PlayerAccount account, string password)
    {
        try
        {
            return _store.ReplacePasswordHash(account.PlayerId, _hasher.Hash(password));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.PasswordHashNotRaised(_logger, _store.Path, ex);
            return account;
        }
    }
}
