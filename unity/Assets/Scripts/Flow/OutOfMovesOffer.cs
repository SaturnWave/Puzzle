#nullable enable
using System;
using Puzzle.Core.Engine;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What a press on the card of a game out of moves led to. A closed set (private constructor): the engine gave
    /// the moves, and their payment is what the attempt's ledger is told; the engine refused them, and the loss is
    /// final with nothing paid; or the terms do not allow that way of paying, and nothing was asked of the engine.
    /// </summary>
    public abstract class OfferAnswer
    {
        private OfferAnswer()
        {
        }

        /// <summary>The engine gave the moves: the game goes on, and the payment is the ledger's to take.</summary>
        public sealed class MovesGiven : OfferAnswer
        {
            public MovesGiven(AttemptEvent.ExtraMovesTaken payment)
            {
                Payment = payment ?? throw new ArgumentNullException(nameof(payment));
            }

            public AttemptEvent.ExtraMovesTaken Payment { get; }
        }

        /// <summary>The engine gave no moves: the board has none that a shuffle could bring back. Nothing is paid, and the game is lost for good.</summary>
        public sealed class Refused : OfferAnswer
        {
            public Refused(AttemptEvent.LostForGood loss)
            {
                Loss = loss ?? throw new ArgumentNullException(nameof(loss));
            }

            public AttemptEvent.LostForGood Loss { get; }
        }

        /// <summary>The terms do not allow that way of paying: the coins fall short, or the day has no free ad left. The engine was not asked.</summary>
        public sealed class CannotPay : OfferAnswer
        {
        }
    }

    /// <summary>
    /// The offer of five more moves to a game that ran out of them (docs/GAME_DESIGN.md §19): to whom it is made, and
    /// what a press on its card leads to. It is made once in an attempt, the first time the moves run out with goals
    /// open: never after a win, never to a game that ended with no move possible, never once the moves were taken. It
    /// costs what the economy's rules say (Puzzle.Meta's ExtraMovesRules), always the same, coins or one of the day's
    /// free ads; no help is added after a payment. The engine is asked first (Game.TryTakeExtraMoves), and the payment
    /// is reported only when it gave the moves; the attempt's ledger then charges the coins or counts the ad. No Unity
    /// dependency, so the repository's tests compile it too.
    /// </summary>
    public static class OutOfMovesOffer
    {
        /// <summary>True when the game's moves ran out with goals open and it never took its extra moves.</summary>
        public static bool IsMadeIn(Game game) => game.Status == GameStatus.LostOutOfMoves && !game.ExtraMovesTaken;

        /// <summary>True when the terms allow paying for the moves that way: the coins cover the price, or the day has a free ad left.</summary>
        public static bool CanPay(ExtraMovesTerms terms, ExtraMovesPayment paidBy) => paidBy switch
        {
            ExtraMovesPayment.ByCoins => terms.CoinsCoverIt,
            ExtraMovesPayment.ByAd => terms.AnAdPaysToday,
            _ => throw new ArgumentOutOfRangeException(nameof(paidBy), paidBy, "Extra moves are paid by coins or by an ad."),
        };

        /// <summary>
        /// The player asks for the moves, paid one way: where the terms allow it the engine is asked, and gives them
        /// or refuses them. A refusal (the game's record keeps it, as the shuffles it tried drew from the shuffle
        /// stream) ends the game for good with nothing paid.
        /// </summary>
        /// <exception cref="InvalidOperationException">The offer is not made to the game.</exception>
        public static OfferAnswer Take(Game game, ExtraMovesPayment paidBy, ExtraMovesTerms terms)
        {
            RequireTheOfferIn(game);
            if (!CanPay(terms, paidBy))
            {
                return new OfferAnswer.CannotPay();
            }

            return game.TryTakeExtraMoves()
                ? new OfferAnswer.MovesGiven(new AttemptEvent.ExtraMovesTaken(paidBy))
                : new OfferAnswer.Refused(new AttemptEvent.LostForGood(LossReason.ExtraMovesRefused));
        }

        /// <summary>The player turns the offer down: the game is lost for good, which costs a life.</summary>
        /// <exception cref="InvalidOperationException">The offer is not made to the game.</exception>
        public static AttemptEvent.LostForGood GiveUp(Game game)
        {
            RequireTheOfferIn(game);
            return new AttemptEvent.LostForGood(LossReason.GaveUp);
        }

        private static void RequireTheOfferIn(Game game)
        {
            if (!IsMadeIn(game))
            {
                throw new InvalidOperationException($"Level {game.Level.Id.Value} is {game.Status}{(game.ExtraMovesTaken ? " and took its extra moves" : string.Empty)}; it is offered no extra moves.");
            }
        }
    }
}
