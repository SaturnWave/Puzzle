using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>One simulated player: a rung of the skill ladder for life, and a seed for their own luck.</summary>
/// <param name="Index">The player's place among the simulated players, from 0; the same in every run of the same players block.</param>
public sealed record SimulatedPlayer(int Index, SkillRung Rung, PlayerSeed Seed);

/// <summary>Where a player stands at a moment: the levels behind them, their coins and the boosters they hold.</summary>
public sealed record Standing(int LevelsWon, Coins Balance, BoosterShelf Held);

/// <summary>
/// Which stored game an attempt meets: the seed of the level's pool that the engine gives that attempt, and of that
/// seed's stored games the one the player's own luck picks. The luck is derived from the player's seed, the level and
/// the attempt's number and from nothing else: what a player owns, has spent or did before cannot change a game, and
/// two settings files give the same player the same games.
/// </summary>
/// <param name="GameOfSeed">The game's place among the seed's stored games, from 0.</param>
public readonly record struct GameMet(SeedPosition Seed, int GameOfSeed)
{
    public static GameMet By(PlayerSeed player, LevelId level, AttemptNumber attempt, MeasuredLine line, MeasuredHalf half)
    {
        SeedPosition seed = attempt.PositionIn(line.Pool.Count);
        ulong luck = Rng.Derive(Rng.Derive(player.Value, (ulong)level.Value), (ulong)attempt.Value);
        return new GameMet(seed, half.GameAmong(line.Seeds[seed.Value].Count, luck));
    }

    public MeasuredGame In(MeasuredLine line) => line.Seeds[Seed.Value][GameOfSeed];
}

/// <summary>One attempt of a level, from the decision about a start booster to its ending.</summary>
/// <param name="DecidedAt">When the attempt was won or lost: after its moves and an ad, if one was watched.</param>
/// <param name="PlayTime">The time the moves took.</param>
/// <param name="Duration">The whole attempt: the moves, an ad if one was watched, and the pause before the next attempt.</param>
/// <param name="FinaleCoins">What the moves left paid; none unless the attempt was won.</param>
/// <param name="LevelCoins">The flat reward for the win; none unless the attempt was won.</param>
/// <param name="LivesLeft">The lives the player has once the attempt is settled.</param>
/// <param name="After">The player's standing once the attempt's coins and boosters are settled.</param>
public sealed record AttemptPlayed(
    LevelId Level,
    AttemptNumber Attempt,
    GameMet Met,
    PlayerTime StartedAt,
    PlayerTime DecidedAt,
    TimeSpan PlayTime,
    TimeSpan Duration,
    StartBoosterDecision StartBooster,
    AttemptReading Reading,
    Coins FinaleCoins,
    Coins LevelCoins,
    LifeCount LivesLeft,
    Standing After)
{
    public bool IsWon => Reading.Ending is AttemptEnding.Won;
}

/// <summary>A session that is over.</summary>
/// <param name="Length">From its start to the moment no further attempt starts: the moves, the ads and the pauses between attempts.</param>
/// <param name="PlayDenied">The play the session lost because the lives ran out: its ceiling less its length; nothing for a session that ended otherwise.</param>
/// <param name="Lives">What the life timer did for the session.</param>
public sealed record SessionEnded(PlayerTime StartedAt, TimeSpan Length, int Attempts, SessionEnd End, TimeSpan PlayDenied, SessionLives Lives, Standing After);

/// <summary>What the life timer did for one session: without these three, a report of lives says nothing about the timer.</summary>
/// <param name="FullAtStart">The meter was full when the session began: the timer had given back all that earlier sessions lost.</param>
/// <param name="BackDuringIt">Lives the timer gave back while the session ran.</param>
/// <param name="UntilNextLife">For a session that ended, or could not start, for lack of a life: how long until the next one comes back; absent for a session that ended otherwise.</param>
public readonly record struct SessionLives(bool FullAtStart, int BackDuringIt, TimeSpan? UntilNextLife);

/// <summary>A reward that was given: for a completed painting or for the day.</summary>
public sealed record RewardGiven(CoinSource Source, PlayerTime At, Coins Coins, IReadOnlyList<BoosterKind> Boosters, Standing After);

/// <summary>
/// Something bought for real money: a coin pack or the starter pack. One bought because the coins fell short of the
/// extra moves is told at the end of its attempt, before the attempt itself, and stands as bought before the moves
/// were paid for.
/// </summary>
/// <param name="After">The player's standing with the product's coins and boosters on hand.</param>
public sealed record PurchaseMade(StoreProduct Product, PurchaseOccasion Occasion, PlayerTime At, Standing After);

/// <summary>A day of a player's journey that is over.</summary>
public sealed record DayEnded(DayNumber Day, Standing Standing);

/// <summary>
/// Told what happens to one player, in the order it happens. The simulator tallies nothing itself: the reports'
/// tallies listen here, and so does a test that wants the whole journey as a list.
/// </summary>
public interface IJourneyLog
{
    void AttemptPlayed(AttemptPlayed attempt);

    void SessionEnded(SessionEnded session);

    void RewardGiven(RewardGiven reward);

    void PurchaseMade(PurchaseMade purchase);

    void DayEnded(DayEnded day);
}
