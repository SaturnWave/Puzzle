using Puzzle.Bot;

namespace Puzzle.Tooling.Hints;

/// <summary>
/// The hints the released game shows, as a bot meets them (the settings' hints section): the move
/// <see cref="HintBot"/> would play, at most <see cref="PerAttempt"/> an attempt, at <see cref="Moment"/>. A bot
/// shown them follows every one, as the measurement's hinted player does (<see cref="HintedPlayer"/>).
/// </summary>
public sealed record GameHints
{
    /// <exception cref="ArgumentException"><paramref name="hintBot"/> gives no hints.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Fewer than one hint an attempt: a game that shows none has no hints to follow.</exception>
    public GameHints(string hintBot, int perAttempt, HintMoment moment)
    {
        HintBot = HintAdvisor.TryCreate(hintBot, out _) ? hintBot : throw new ArgumentException($"'{hintBot}' gives no hints.", nameof(hintBot));
        PerAttempt = perAttempt >= 1
            ? perAttempt
            : throw new ArgumentOutOfRangeException(nameof(perAttempt), perAttempt, "A game with hints shows at least one an attempt.");
        Moment = moment;
    }

    /// <summary>The player model whose move a hint shows.</summary>
    public string HintBot { get; }

    public int PerAttempt { get; }

    public HintMoment Moment { get; }

    /// <summary><paramref name="player"/> for one game, shown these hints and following each.</summary>
    public HintedPlayer Follow(IBotPolicy player) => HintAdvisor.TryCreate(HintBot, out HintAdvisor? advisor)
        ? new HintedPlayer(player, advisor, Moment, PerAttempt)
        : throw new InvalidOperationException($"'{HintBot}' gave hints when these hints were made, and gives none now.");

    public override string ToString() => $"{HintBot}'s hints, at most {PerAttempt} an attempt, {Moment}";
}
