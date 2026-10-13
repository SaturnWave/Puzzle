using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>
/// A player's lives (docs/GAME_DESIGN.md section 19): full at the start, one lost with every lost attempt, one back
/// after a fixed time while below the maximum. The clock of the next life starts with the loss that takes the meter
/// below full and is not started again by a later loss.
/// </summary>
public sealed class LifeMeter
{
    private readonly LifeCount _max;
    private readonly TimeSpan _perLife;

    /// <summary>When the next life comes back; absent while the meter is full.</summary>
    private PlayerTime? _nextLifeAt;

    public LifeMeter(LivesSettings settings)
    {
        _max = settings.Max;
        _perLife = settings.PerLife;
        Lives = settings.Max;
    }

    public LifeCount Lives { get; private set; }

    public bool HasOne => !Lives.IsNone;

    public bool IsFull => Lives >= _max;

    /// <summary>The lives that have come back since the player's first day: what the timer has given so far.</summary>
    public int LivesBack { get; private set; }

    /// <summary>How long from <paramref name="now"/> until the next life comes back; absent while the meter is full.</summary>
    public TimeSpan? UntilNextLife(PlayerTime now) => _nextLifeAt is PlayerTime due
        ? (due > now ? due - now : TimeSpan.Zero)
        : null;

    /// <summary>Adds every life that has come back by <paramref name="now"/>.</summary>
    public void BringUpTo(PlayerTime now)
    {
        while (_nextLifeAt is PlayerTime due && due <= now)
        {
            Lives = Lives.OneMore;
            LivesBack++;
            _nextLifeAt = IsFull ? null : due + _perLife;
        }
    }

    /// <summary>
    /// Takes the life a lost attempt costs. A life that fell due while the attempt was played is back first: a meter
    /// that was full by then had no clock, and this loss starts one of its own.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no life to lose: the attempt should not have started.</exception>
    public void LoseOne(PlayerTime now)
    {
        BringUpTo(now);
        if (!HasOne)
        {
            throw new InvalidOperationException("An attempt was lost without a life to lose.");
        }

        Lives = Lives.OneFewer;
        _nextLifeAt ??= now + _perLife;
    }
}
