namespace Puzzle.Web.Players;

/// <summary>
/// How many new attempts one player may open in a stretch of time. Every attempt record is the record of an attempt of
/// its own, so this is also the most one account can add to the attempt log, however fast its requests come: opening
/// an attempt, playing one move and leaving it for the next would otherwise write a record every two requests.
/// </summary>
/// <remarks>
/// The budget is far from play: it allows a new attempt every ten seconds for ten minutes on end, and the shortest
/// level takes about that long for its animations alone, played without a thought; someone who plays gets through a
/// third of it at most. It is kept in memory, with the player's seat, and starts again with the server; it measures
/// time that only runs forward, so a wall clock set back or forth changes nothing. Not thread-safe: use it under the
/// seat's gate.
/// </remarks>
internal sealed class AttemptBudget
{
    /// <summary>New attempts a player may open within any <see cref="Window"/>.</summary>
    public const int Attempts = 60;

    /// <summary>How long an opened attempt counts against the budget.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly TimeProvider _clock;

    /// <summary>The clock's timestamps of the attempts opened, oldest first; never more than <see cref="Attempts"/>.</summary>
    private readonly Queue<long> _opened = new();

    public AttemptBudget(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>The time until the player may open another attempt; absent when they may open one now.</summary>
    public TimeSpan? UsedUpFor()
    {
        long now = _clock.GetTimestamp();
        TimeSpan[] agesThatCount = _opened.Select(opened => _clock.GetElapsedTime(opened, now)).Where(age => age < Window).ToArray();
        return agesThatCount.Length < Attempts ? null : Window - agesThatCount[0];
    }

    /// <summary>The player opened an attempt now. Ask <see cref="UsedUpFor"/> first: this counts whatever it is told.</summary>
    public void Spend()
    {
        long now = _clock.GetTimestamp();
        while (_opened.TryPeek(out long oldest) && _clock.GetElapsedTime(oldest, now) >= Window)
        {
            _opened.Dequeue();
        }

        _opened.Enqueue(now);
    }
}
