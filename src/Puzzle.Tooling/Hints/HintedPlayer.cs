using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Tooling.Hints;

/// <summary>When a simulated player is shown a hint. A closed set (private constructor).</summary>
public abstract record HintMoment
{
    private HintMoment()
    {
    }

    /// <summary>
    /// The player thinks longer than the hint's delay before a move with this chance, whatever the board. The game
    /// shows a hint after 5 s without a move, and the recordings have 5.3 s a move: about half the moves.
    /// </summary>
    public sealed record WhenIdle : HintMoment
    {
        public WhenIdle(int chancePercent)
        {
            ChancePercent = chancePercent is >= 1 and <= 100
                ? chancePercent
                : throw new ArgumentOutOfRangeException(nameof(chancePercent), chancePercent, "A chance is 1 to 100 percent.");
        }

        public int ChancePercent { get; }

        public override string ToString() => $"idle before {ChancePercent} % of the moves";
    }

    /// <summary>
    /// The move the player would make counts nothing for any goal: they see nothing that helps, look on, and the
    /// hint comes. The stand-in for a player who is stuck.
    /// </summary>
    public sealed record WhenStuck : HintMoment
    {
        public override string ToString() => "stuck (own move counts nothing for the goals)";
    }
}

/// <summary>
/// A player model that is shown the game's hint at some moments and then plays the hinted move instead of its own,
/// up to a number of hints per attempt: the upper bound of what hints can do, since a real player may look past one.
/// The player's own choice is always made first, from the game's bot stream as in a game without hints, so a hinted
/// game and a plain one on the same seeds are the same game until the first hint is followed. Whether a hint is
/// shown, and the hint itself, come from a stream of their own. One instance plays one game.
/// </summary>
public sealed class HintedPlayer : IBotPolicy
{
    private const ulong HintStream = 0x48_49_4E_54; // "HINT"

    private readonly IBotPolicy _player;
    private readonly HintAdvisor _advisor;
    private readonly HintMoment _moment;
    private readonly int _hintsPerAttempt;
    private Rng? _hints;

    /// <exception cref="ArgumentOutOfRangeException">Fewer than one hint per attempt.</exception>
    public HintedPlayer(IBotPolicy player, HintAdvisor advisor, HintMoment moment, int hintsPerAttempt)
    {
        _player = player;
        _advisor = advisor;
        _moment = moment;
        _hintsPerAttempt = hintsPerAttempt >= 1
            ? hintsPerAttempt
            : throw new ArgumentOutOfRangeException(nameof(hintsPerAttempt), hintsPerAttempt, "A hinted player is shown at least one hint.");
    }

    public string Name => $"{_player.Name}+hints-{_advisor.BotName}";

    /// <summary>Hints shown so far in the game, and followed: each one took the place of the player's own move.</summary>
    public int HintsFollowed { get; private set; }

    public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng)
    {
        Move own = _player.ChooseMove(game, legalMoves, rng);
        if (HintsFollowed >= _hintsPerAttempt)
        {
            return own;
        }

        // Seeded from a copy of the bot stream: drawing from the stream itself would change the player's next choices.
        Rng hints = _hints ??= new Rng(Rng.Derive(rng.Clone().NextULong(), HintStream));
        if (!IsShownAHint(game, own, hints) || _advisor.MoveFor(game, hints.NextULong()) is not Move hint)
        {
            return own;
        }

        HintsFollowed++;
        return hint;
    }

    private bool IsShownAHint(Game game, Move own, Rng hints) => _moment switch
    {
        HintMoment.WhenIdle idle => hints.RollPercent(idle.ChancePercent),
        HintMoment.WhenStuck => CountsNothing(game, own, hints),
        _ => throw new InvalidOperationException($"Unhandled hint moment {_moment}."),
    };

    /// <summary>Whether the move, tried on a copy with the refills hidden, leaves every goal where it was.</summary>
    private static bool CountsNothing(Game game, Move move, Rng hints)
    {
        Game tried = game.CreateLookahead(hints.NextULong());
        if (!tried.TryPlay(move))
        {
            throw new InvalidOperationException($"The player's legal move {move} was refused by its lookahead.");
        }

        return tried.Goals.OverallProgress <= game.Goals.OverallProgress;
    }
}
