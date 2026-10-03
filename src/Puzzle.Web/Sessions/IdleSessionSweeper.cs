using Puzzle.Web.Recording;

namespace Puzzle.Web.Sessions;

/// <summary>
/// Once a minute, tries the attempt records the log refused again, then sweeps idle sessions out of the store. The cache
/// notices an expired session only when it is used, so without the sweep a lab nobody touches would keep — and never
/// record — the attempts its testers walked away from.
/// </summary>
public sealed class IdleSessionSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly GameSessionStore _sessions;
    private readonly AttemptRecorder _recorder;
    private readonly TimeProvider _clock;

    public IdleSessionSweeper(GameSessionStore sessions, AttemptRecorder recorder, TimeProvider clock)
    {
        _sessions = sessions;
        _recorder = recorder;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _recorder.RetryWaiting();
            _sessions.RemoveIdleSessions();
        }
    }
}
