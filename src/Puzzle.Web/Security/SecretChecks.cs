namespace Puzzle.Web.Security;

/// <summary>
/// Lets only a few secrets be checked at the same time, whoever asks. A password check is a deliberately slow hash; a
/// flood of sign-ins from ever new addresses must wait its turn for the processor instead of taking every core from
/// the players' moves. How many checks a minute get this far at all is the rate limits' business
/// (<see cref="RateLimits.SecretChecksPerMinute"/>), which also bounds how many can be waiting here.
/// </summary>
internal sealed class SecretChecks : IDisposable
{
    /// <summary>Checks running at once: two cores at most are ever busy hashing.</summary>
    public const int AtOnce = 2;

    private readonly SemaphoreSlim _turns = new(AtOnce, AtOnce);

    /// <summary>Runs <paramref name="check"/> when its turn comes and returns what it found.</summary>
    /// <exception cref="OperationCanceledException">The request was given up while it waited for its turn.</exception>
    public async Task<TResult> RunAsync<TResult>(Func<TResult> check, CancellationToken cancellation)
    {
        await _turns.WaitAsync(cancellation);
        try
        {
            return check();
        }
        finally
        {
            _turns.Release();
        }
    }

    public void Dispose() => _turns.Dispose();
}
