using Puzzle.Tooling.Players;

namespace Puzzle.Web.Accounts;

/// <summary>
/// Locks a username for a while after repeated failed sign-ins, so a password cannot be guessed at speed whatever
/// address the guesses come from. A username nobody has is counted like any other: whether a name is locked says
/// nothing about whether it exists. The counts live in memory only and start again with the server.
/// </summary>
/// <remarks>
/// At most <see cref="DefaultCapacity"/> usernames are counted at once; past that, the counts that no longer matter go
/// first, then the one whose last failure is oldest. Only a flood of made-up names reaches the bound.
/// </remarks>
public sealed class SignInLockouts
{
    /// <summary>Failures in a row that lock a username.</summary>
    public const int MaxFailures = 5;

    /// <summary>Usernames counted at once by default: some hundred kilobytes.</summary>
    public const int DefaultCapacity = 10_000;

    /// <summary>How long a locked username stays locked.</summary>
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(5);

    /// <summary>A failure this long after the one before starts a new count.</summary>
    public static readonly TimeSpan FailureMemory = TimeSpan.FromMinutes(15);

    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly int _capacity;
    private readonly Dictionary<Username, Failures> _failures = new();

    /// <param name="capacity">Usernames counted at once; at least 1.</param>
    public SignInLockouts(TimeProvider clock, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _clock = clock;
        _capacity = capacity;
    }

    /// <summary>The time left on the lockout of <paramref name="username"/>; absent when it may try to sign in.</summary>
    public TimeSpan? LockedFor(Username username)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        lock (_gate)
        {
            return _failures.TryGetValue(username, out Failures? failures) && failures.LockedUntil > now
                ? failures.LockedUntil - now
                : null;
        }
    }

    /// <summary>Counts a failed sign-in for <paramref name="username"/>; true when this one locked it.</summary>
    public bool RecordFailure(Username username)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (!_failures.TryGetValue(username, out Failures? failures) || !failures.StillCounts(now))
            {
                MakeRoom(now);
                failures = new Failures();
                _failures[username] = failures;
            }

            failures.Count++;
            failures.LastFailure = now;
            if (failures.Count < MaxFailures)
            {
                return false;
            }

            failures.LockedUntil = now + LockoutTime;
            return true;
        }
    }

    /// <summary>A sign-in succeeded: the username's failures are forgotten.</summary>
    public void RecordSuccess(Username username)
    {
        lock (_gate)
        {
            _failures.Remove(username);
        }
    }

    /// <summary>Before a new username is counted at capacity: drops the counts that no longer matter, or else the one that failed longest ago.</summary>
    private void MakeRoom(DateTimeOffset now)
    {
        if (_failures.Count < _capacity)
        {
            return;
        }

        foreach (Username stale in _failures.Where(pair => !pair.Value.StillCounts(now)).Select(pair => pair.Key).ToArray())
        {
            _failures.Remove(stale);
        }

        if (_failures.Count >= _capacity)
        {
            _failures.Remove(_failures.MinBy(pair => pair.Value.LastFailure).Key);
        }
    }

    /// <summary>The failed sign-ins of one username since its last success or its last lockout.</summary>
    private sealed class Failures
    {
        public int Count { get; set; }

        public DateTimeOffset LastFailure { get; set; }

        /// <summary>The end of the lockout; the minimum value while the username was never locked.</summary>
        public DateTimeOffset LockedUntil { get; set; } = DateTimeOffset.MinValue;

        /// <summary>
        /// Whether the next failure adds to this count: the username is locked now, or its failures are recent and it
        /// has not served a lockout for them yet.
        /// </summary>
        public bool StillCounts(DateTimeOffset now)
        {
            bool locked = LockedUntil > now;
            bool recentAndNeverLocked = LockedUntil == DateTimeOffset.MinValue && now - LastFailure <= FailureMemory;
            return locked || recentAndNeverLocked;
        }
    }
}
