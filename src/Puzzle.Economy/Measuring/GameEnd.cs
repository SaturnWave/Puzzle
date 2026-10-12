using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>How a bot game that was followed past the level's limit ended. Closed set (private constructor).</summary>
public abstract record GameEnd
{
    private const int StoredUnfinished = 0;

    private GameEnd()
    {
    }

    /// <summary>Every goal was met on this move.</summary>
    public sealed record Won(MoveCount OnMove) : GameEnd;

    /// <summary>After this move the board had no move left and no shuffle brought one back.</summary>
    public sealed record Stuck(MoveCount AfterMove) : GameEnd;

    /// <summary>The game was still running at the move cap; nobody knows how it would have ended.</summary>
    public sealed record Unfinished : GameEnd;

    /// <summary>
    /// The end as one number, the form it has in the measurement file: k above 0 for won on move k, 0 for unfinished,
    /// -m for stuck after m moves. A game cannot end before its first move, so the three never meet.
    /// </summary>
    public int ToStored() => this switch
    {
        Won won => won.OnMove.Value,
        Stuck stuck => -stuck.AfterMove.Value,
        Unfinished => StoredUnfinished,
        _ => throw new InvalidOperationException($"Unhandled game end {GetType().Name}."),
    };

    public static GameEnd FromStored(int stored) => stored switch
    {
        > StoredUnfinished => new Won(new MoveCount(stored)),
        StoredUnfinished => new Unfinished(),
        _ => new Stuck(new MoveCount(-stored)),
    };
}
