namespace Puzzle.Web.Players;

/// <summary>
/// Once a minute, tries again to write the players' moves and wins the store refused (a full disk, a file another
/// program held). A store that was unwritable for a moment then has everything again within the minute, whether or not
/// the player comes back; without this, what a player left unsaved would wait for their next visit.
/// </summary>
public sealed class UnsavedProgressSaver : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly PlayerGames _games;
    private readonly TimeProvider _clock;

    public UnsavedProgressSaver(PlayerGames games, TimeProvider clock)
    {
        _games = games;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _games.SaveUnsavedProgress();
        }
    }
}
