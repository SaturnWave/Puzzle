using Puzzle.Economy.Measuring;
using Puzzle.Economy.Settings;
using Puzzle.Economy.Units;

namespace Puzzle.Economy.Simulating;

/// <summary>What an attempt is played with, besides the stored game: the level's limit and what the player brings to it.</summary>
/// <param name="WithExtraMovesBooster">The attempt started with the booster that adds moves to the limit.</param>
/// <param name="InLevelBoosterAtHand">A held in-level booster that is assumed to have an effect; absent when the player holds none such.</param>
public sealed record AttemptTerms(
    MoveCount MoveLimit,
    bool WithExtraMovesBooster,
    BoosterKind? InLevelBoosterAtHand,
    int LossesOnLevel,
    Coins Balance,
    int FreeAdsLeftToday);

/// <summary>How an attempt went.</summary>
/// <param name="RanOutOfMoves">The moves ran out before the attempt was decided: the moment an offer can be made.</param>
/// <param name="GoalsDoneThen">What the player saw of the goals at that moment; absent when the moves never ran out.</param>
/// <param name="InLevelBoosterUsed">The in-level booster the player used then; absent when none was.</param>
/// <param name="MovesPlayed">The moves of the stored game the attempt covers; they are what its play time is counted from.</param>
public sealed record AttemptReading(
    AttemptEnding Ending,
    bool RanOutOfMoves,
    Percent? GoalsDoneThen,
    BoosterKind? InLevelBoosterUsed,
    ExtraMovesDecision ExtraMoves,
    MoveCount MovesPlayed);

/// <summary>
/// Reads an attempt from a stored game: a function of the game, the terms and the player rules, with no state and no
/// randomness. The game is read at the level's limit (three moves further with the extra-moves booster); if the
/// moves run out there and the player takes the offer, the same game is read on to five moves further, once.
/// </summary>
public sealed class AttemptReader(MeasurementSpec spec, InLevelBoosterHabit inLevelBoosters, ExtraMovesRule extraMovesRule)
{
    public AttemptReading Read(MeasuredGame game, AttemptTerms terms)
    {
        MoveCount budget = terms.MoveLimit + (terms.WithExtraMovesBooster ? spec.StartBoosterExtraMoves : MoveCount.None);
        if (game.WonWithin(budget) || game.StuckBefore(budget))
        {
            return Decided(game, budget);
        }

        Percent goalsDone = game.GoalsDoneWithin(budget);
        BoosterKind? used = null;
        if (terms.InLevelBoosterAtHand is BoosterKind booster && !game.StuckWithin(budget) && game.HasReached(inLevelBoosters.UseWhenGoalsDone, budget))
        {
            used = booster;
            budget += inLevelBoosters.AssumedMovesWorth[booster];
            if (game.WonWithin(budget))
            {
                // The booster's assumed work ended the game by itself: the player had no move left to be paid for.
                return new AttemptReading(new AttemptEnding.Won(MoveCount.None), true, goalsDone, used, new ExtraMovesDecision.NotOffered(), game.MovesPlayedWithin(budget));
            }
        }

        ExtraMovesDecision offer = OfferAt(game, budget, terms);
        if (offer.IsTaken)
        {
            budget += spec.ExtraMovesOfferMoves;
        }

        return new AttemptReading(EndingAt(game, budget), true, goalsDone, used, offer, game.MovesPlayedWithin(budget));
    }

    /// <summary>The offer is made to a game that is out of moves and whose board can go on; the engine sells no moves nobody could play.</summary>
    private ExtraMovesDecision OfferAt(MeasuredGame game, MoveCount budget, AttemptTerms terms) => game.StuckWithin(budget)
        ? new ExtraMovesDecision.NotOffered()
        : extraMovesRule.Decide(new OutOfMovesView(game.GoalsDoneWithin(budget), terms.LossesOnLevel, terms.Balance, terms.FreeAdsLeftToday));

    private static AttemptReading Decided(MeasuredGame game, MoveCount budget) =>
        new(EndingAt(game, budget), RanOutOfMoves: false, GoalsDoneThen: null, InLevelBoosterUsed: null, new ExtraMovesDecision.NotOffered(), game.MovesPlayedWithin(budget));

    /// <summary>
    /// The ending the engine gives the game with <paramref name="budget"/> moves: a board that runs out of legal moves on
    /// its very last move is out of moves to the engine, like any other board the budget ended.
    /// </summary>
    private static AttemptEnding EndingAt(MeasuredGame game, MoveCount budget)
    {
        if (game.WonWithin(budget))
        {
            return new AttemptEnding.Won(game.MovesLeftAt(budget));
        }

        return game.StuckBefore(budget) ? new AttemptEnding.LostNoMovePossible() : new AttemptEnding.LostOutOfMoves();
    }
}

