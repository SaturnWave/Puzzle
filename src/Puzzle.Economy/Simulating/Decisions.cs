using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>How a simulated attempt ended (named apart from the engine's AttemptOutcome, which records a real one). Closed set.</summary>
public abstract record AttemptEnding
{
    private AttemptEnding()
    {
    }

    /// <summary>Won with this many moves left: what the finale pays.</summary>
    public sealed record Won(MoveCount MovesLeft) : AttemptEnding;

    public sealed record LostOutOfMoves : AttemptEnding;

    /// <summary>The board had no move left and no shuffle brought one back, before the moves ran out.</summary>
    public sealed record LostNoMovePossible : AttemptEnding;
}

/// <summary>
/// A want that went unpaid: what it cost, and how much of that the player did not have. The two are different sums
/// over a journey: the coins a player holds are missing from none of their gaps and yet pay for one want only.
/// </summary>
public readonly record struct Shortfall(Coins Price, Coins Missing);

/// <summary>What became of the extra-moves offer in an attempt. Closed set.</summary>
public abstract record ExtraMovesDecision
{
    private ExtraMovesDecision()
    {
    }

    /// <summary>The attempt was decided without running out of moves, or its board could not go on.</summary>
    public sealed record NotOffered : ExtraMovesDecision;

    /// <summary>The player saw too little of the goals done to want more moves.</summary>
    public sealed record NotWanted : ExtraMovesDecision;

    public sealed record TakenForAd : ExtraMovesDecision;

    public sealed record TakenForCoins(Coins Price) : ExtraMovesDecision;

    /// <summary>The player wanted the moves and went without.</summary>
    /// <param name="Shortfall">The price and the coins that were missing; absent when the player does not pay coins for moves at all.</param>
    public sealed record WantedUnpaid(Shortfall? Shortfall) : ExtraMovesDecision;

    public bool IsTaken => this is TakenForAd or TakenForCoins;
}

/// <summary>What a player did about a start booster before an attempt. Closed set.</summary>
public abstract record StartBoosterDecision
{
    private StartBoosterDecision()
    {
    }

    public sealed record None : StartBoosterDecision;

    public sealed record UseOwned(BoosterKind Booster) : StartBoosterDecision;

    public sealed record Buy(BoosterKind Booster, Coins Price) : StartBoosterDecision;

    /// <summary>The player would have bought one and could afford none.</summary>
    /// <param name="Shortfall">The price of the cheapest booster the player reaches for, and the coins missing for it.</param>
    public sealed record WantedUnpaid(Shortfall Shortfall) : StartBoosterDecision;

    /// <summary>The booster the attempt starts with; absent when it starts without one.</summary>
    public BoosterKind? Taken => this switch
    {
        UseOwned owned => owned.Booster,
        Buy bought => bought.Booster,
        None or WantedUnpaid => null,
        _ => throw new InvalidOperationException($"Unhandled start booster decision {GetType().Name}."),
    };
}

/// <summary>Why a session ended. Closed set.</summary>
public enum SessionEnd
{
    /// <summary>The player opened the game without a life and played nothing.</summary>
    NoLifeAtStart,

    /// <summary>The last life was lost during the session.</summary>
    LivesGone,

    /// <summary>The session had lasted its ceiling; the player had lives left.</summary>
    CeilingReached,

    /// <summary>The last measured level was won.</summary>
    NothingLeftToPlay,
}

/// <summary>Where coins come from. Closed set.</summary>
public enum CoinSource
{
    Finale,
    LevelWon,
    PaintingCompleted,
    Daily,

    /// <summary>What a new player is given before the first level: the settings' start section.</summary>
    WelcomeGift,

    /// <summary>
    /// What a booster used during play brings as it opens, on its level: the welcome gift's part for it
    /// (the settings' boosterUnlocks.gift). Boosters only, no coins.
    /// </summary>
    UnlockGift,
}

/// <summary>What coins are spent on in this version. Closed set.</summary>
public enum CoinSink
{
    ExtraMoves,
    StartBooster,
}

/// <summary>
/// Which of a seed's stored games a pass of the run may meet. The even and the odd games are two measurements of
/// the same levels that share no game: what differs between a pass on each is measurement noise, not a finding.
/// Closed set.
/// </summary>
public enum MeasuredHalf
{
    All,
    Even,
    Odd,
}

public static class MeasuredHalves
{
    /// <summary>The game of <paramref name="games"/> stored ones that <paramref name="luck"/> picks within the half.</summary>
    /// <param name="games">Even and at least 2, as every measurement stores them.</param>
    public static int GameAmong(this MeasuredHalf half, int games, ulong luck) => half switch
    {
        MeasuredHalf.All => (int)(luck % (ulong)games),
        MeasuredHalf.Even => 2 * (int)(luck % (ulong)(games / 2)),
        MeasuredHalf.Odd => (2 * (int)(luck % (ulong)(games / 2))) + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(half), half, "Unknown half."),
    };
}
