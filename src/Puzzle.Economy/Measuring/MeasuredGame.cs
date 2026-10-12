using Puzzle.Economy.Units;

namespace Puzzle.Economy.Measuring;

/// <summary>
/// One bot game as the economy keeps it: how it ended, and the move on which the level's goals were first done to
/// each measured share. The bot never reads the moves left and the engine's extra moves revive a board exactly as a
/// longer limit would, so this one game tells how the attempt ends under every budget of moves up to the cap: the
/// level's limit, the limit with the start booster's moves, with the extra-moves offer, and with both.
/// The questions below are the only arithmetic the simulation does on a game.
/// </summary>
public sealed class MeasuredGame
{
    private readonly IReadOnlyList<Percent> _marks;
    private readonly MoveCount?[] _reachedOn;

    /// <param name="marks">The measured shares of the goals, in percent.</param>
    /// <param name="reachedOn">For each mark, the move on which the goals' progress first reached it; absent when it never did.</param>
    /// <exception cref="ArgumentException">Not one move per mark.</exception>
    public MeasuredGame(GameEnd end, IReadOnlyList<Percent> marks, IReadOnlyList<MoveCount?> reachedOn)
    {
        if (marks.Count != reachedOn.Count)
        {
            throw new ArgumentException($"{reachedOn.Count} moves were given for {marks.Count} marks.", nameof(reachedOn));
        }

        End = end;
        _marks = marks;
        _reachedOn = [.. reachedOn];
    }

    public GameEnd End { get; }

    /// <summary>For each measured mark, in their order, the move on which it was first reached; absent when never.</summary>
    public IReadOnlyList<MoveCount?> ReachedOn => _reachedOn;

    /// <summary>Whether a player with <paramref name="budget"/> moves wins this game.</summary>
    public bool WonWithin(MoveCount budget) => End is GameEnd.Won won && won.OnMove <= budget;

    /// <summary>
    /// Whether the board has no move left by the time <paramref name="budget"/> moves are played. A board stuck on the
    /// very last move counts: the engine sells no extra moves that nobody could play.
    /// </summary>
    public bool StuckWithin(MoveCount budget) => End is GameEnd.Stuck stuck && stuck.AfterMove <= budget;

    /// <summary>Whether the game ends for lack of a move before the budget is spent: the engine's "no moves possible".</summary>
    public bool StuckBefore(MoveCount budget) => End is GameEnd.Stuck stuck && stuck.AfterMove < budget;

    /// <summary>The moves a winner has left: what the finale pays.</summary>
    /// <exception cref="InvalidOperationException">The game is not won within the budget.</exception>
    public MoveCount MovesLeftAt(MoveCount budget) => End is GameEnd.Won won && won.OnMove <= budget
        ? budget - won.OnMove
        : throw new InvalidOperationException($"A game that is not won within {budget} moves leaves none to count.");

    /// <summary>The moves the player makes: to the game's end, or all of the budget.</summary>
    public MoveCount MovesPlayedWithin(MoveCount budget) => End switch
    {
        GameEnd.Won won when won.OnMove <= budget => won.OnMove,
        GameEnd.Stuck stuck when stuck.AfterMove <= budget => stuck.AfterMove,
        GameEnd.Won or GameEnd.Stuck or GameEnd.Unfinished => budget,
        _ => throw new InvalidOperationException($"Unhandled game end {End.GetType().Name}."),
    };

    /// <summary>
    /// Whether <paramref name="mark"/> percent of the goals were done when <paramref name="budget"/> moves had been
    /// played. Progress never falls, so the first move a mark was reached on answers this for every budget.
    /// </summary>
    /// <exception cref="ArgumentException">The mark is neither 0 nor one the measurement kept.</exception>
    public bool HasReached(Percent mark, MoveCount budget)
    {
        if (mark.Value == 0)
        {
            return true;
        }

        for (int index = 0; index < _marks.Count; index++)
        {
            if (_marks[index] == mark)
            {
                return _reachedOn[index] is MoveCount move && move <= budget;
            }
        }

        throw new ArgumentException($"The measurement kept no mark at {mark} % of the goals (it has {string.Join(", ", _marks)}).", nameof(mark));
    }

    /// <summary>
    /// What a player sees of the goals when <paramref name="budget"/> moves have been played: the largest measured share
    /// that is done; 0 below the first mark.
    /// </summary>
    public Percent GoalsDoneWithin(MoveCount budget)
    {
        Percent done = default;
        for (int index = 0; index < _marks.Count; index++)
        {
            if (_reachedOn[index] is MoveCount move && move <= budget && _marks[index] > done)
            {
                done = _marks[index];
            }
        }

        return done;
    }

    /// <summary>Whether both are the same game in everything that is kept of one.</summary>
    public bool IsSameGameAs(MeasuredGame other) => End == other.End && _reachedOn.SequenceEqual(other._reachedOn);
}

/// <summary>The stored games of one pool seed, for one level, player and board start; game j was played with bot repeat <c>firstRepeat + j</c>.</summary>
public sealed class SeedGames
{
    private const short NeverReached = -1;

    private readonly IReadOnlyList<Percent> _marks;
    private readonly short[] _ends;

    /// <summary>For each mark the move each game reached it on, or <see cref="NeverReached"/>.</summary>
    private readonly short[][] _reachedOn;

    public SeedGames(IReadOnlyList<MeasuredGame> games, IReadOnlyList<Percent> marks)
    {
        _marks = marks;
        _ends = games.Select(game => checked((short)game.End.ToStored())).ToArray();
        _reachedOn = Enumerable.Range(0, marks.Count)
            .Select(mark => games.Select(game => game.ReachedOn[mark] is MoveCount move ? checked((short)move.Value) : NeverReached).ToArray())
            .ToArray();
    }

    public int Count => _ends.Length;

    public MeasuredGame this[int index]
    {
        get
        {
            var reachedOn = new MoveCount?[_marks.Count];
            for (int mark = 0; mark < reachedOn.Length; mark++)
            {
                short move = _reachedOn[mark][index];
                reachedOn[mark] = move == NeverReached ? null : new MoveCount(move);
            }

            return new MeasuredGame(GameEnd.FromStored(_ends[index]), _marks, reachedOn);
        }
    }

    public IEnumerable<MeasuredGame> Games => Enumerable.Range(0, Count).Select(index => this[index]);
}
