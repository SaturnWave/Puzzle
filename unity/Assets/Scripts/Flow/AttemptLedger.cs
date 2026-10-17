#nullable enable
using System;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>How the player paid for the extra moves of a game that ran out of them. A closed set.</summary>
    public enum ExtraMovesPayment
    {
        ByCoins,
        ByAd,
    }

    /// <summary>
    /// Something that happened in an attempt and changes the player's progress, as the board's stage reports it at
    /// the moment the engine says so. A closed set (private constructor): the first move, a booster the engine
    /// accepted, extra moves the engine gave, a win, a loss for good, and leaving the level. A booster used during
    /// play that the engine accepts counts as a move (the owner's decision of 2026-10-15): when it comes first it is
    /// the attempt's first move, and leaving after it is leaving after a move.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public abstract class AttemptEvent
    {
        private AttemptEvent()
        {
        }

        /// <summary>
        /// Whether the attempt is over for good with this event: won, lost for good, or left after a move. Its record is
        /// written then (<see cref="IAttemptLog"/>), never while something could still bring the attempt back.
        /// </summary>
        public bool EndsTheAttempt => this is Won || this is LostForGood || (this is Left left && left.AfterAMove);

        /// <summary>
        /// The attempt's first move was played, or a booster used during play that the engine accepted before any move,
        /// which counts as one: the attempt counts, and the start boosters taken into it are spent.
        /// </summary>
        public sealed class FirstMove : AttemptEvent
        {
            public FirstMove(StartChoice startBoosters)
            {
                StartBoosters = startBoosters ?? throw new ArgumentNullException(nameof(startBoosters));
            }

            public StartChoice StartBoosters { get; }

            public override string ToString() => $"first move with {StartBoosters}";
        }

        /// <summary>The engine used a booster of the player's during play.</summary>
        public sealed class BoosterAccepted : AttemptEvent
        {
            public BoosterAccepted(BoosterKind booster)
            {
                Booster = booster;
            }

            public BoosterKind Booster { get; }

            public override string ToString() => $"{Booster.Name()} used";
        }

        /// <summary>The engine gave the game that ran out of moves its extra moves, paid for this way.</summary>
        public sealed class ExtraMovesTaken : AttemptEvent
        {
            public ExtraMovesTaken(ExtraMovesPayment paidBy)
            {
                PaidBy = paidBy;
            }

            public ExtraMovesPayment PaidBy { get; }

            public override string ToString() => $"extra moves paid {PaidBy}";
        }

        /// <summary>The move that won the level was played, with this many moves left.</summary>
        public sealed class Won : AttemptEvent
        {
            /// <exception cref="ArgumentOutOfRangeException">A negative number of moves.</exception>
            public Won(int movesLeft)
            {
                if (movesLeft < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(movesLeft), movesLeft, "A win leaves none or more moves.");
                }

                MovesLeft = movesLeft;
            }

            public int MovesLeft { get; }

            public override string ToString() => $"won with {MovesLeft} moves left";
        }

        /// <summary>The attempt is lost and nothing can bring it back.</summary>
        public sealed class LostForGood : AttemptEvent
        {
            public LostForGood(LossReason reason)
            {
                Reason = reason;
            }

            public LossReason Reason { get; }

            public override string ToString() => $"lost for good ({Reason})";
        }

        /// <summary>The player left the level while it was still being played.</summary>
        public sealed class Left : AttemptEvent
        {
            /// <param name="afterAMove">True when a move had been played in the attempt, or a booster used during play, which counts as one (<see cref="AttemptBegun"/>).</param>
            public Left(bool afterAMove)
            {
                AfterAMove = afterAMove;
            }

            public bool AfterAMove { get; }

            public override string ToString() => AfterAMove ? "left after a move" : "left before any move";
        }
    }

    /// <summary>
    /// Whether an attempt has begun: a move was played in it, or a booster used during play that the engine accepted,
    /// which counts as a move. The start boosters a board opens with do not begin it: a board nobody touched is left
    /// at no cost. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class AttemptBegun
    {
        public static bool In(Game game) => game.MovesUsed > 0 || game.BoostersUsed.Any(name => BoosterNames.StartBoosterNamed(name) == null);
    }

    /// <summary>
    /// What each event of an attempt does to the player's progress, by the rules of the economy: the first move (or
    /// a booster that counts as one) makes the attempt count and spends the start boosters taken into it; a booster
    /// the engine accepted leaves the shelf; extra moves are paid for; a win moves the player on and pays its coins in
    /// the same change, so one save holds both, with the gift of every booster used during play that opens on the
    /// level it leads to; a loss for good, and leaving a level after a move, cost a life. Leaving before the first
    /// move costs nothing. Closing the game in the middle of a level is no event and costs nothing either. In the
    /// tester mode (<see cref="WalletHandling.Unlimited"/>) no loss costs a life and the extra moves paid with coins
    /// leave the coins as they were; everything else is as the rules have it.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class AttemptLedger
    {
        private readonly EconomyRules _rules;
        private readonly int _levelCount;

        /// <param name="levelCount">How many levels the pack has: a win of its last level does not lead past it.</param>
        /// <exception cref="ArgumentOutOfRangeException">The pack has no level.</exception>
        public AttemptLedger(EconomyRules rules, int levelCount)
        {
            if (levelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "A pack without a level has no attempt to keep.");
            }

            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _levelCount = levelCount;
        }

        /// <summary>The progress after <paramref name="happened"/>, which happened at <paramref name="now"/> on the player's clock, for a wallet held as the rules have it.</summary>
        /// <exception cref="InvalidOperationException">
        /// The event spends what the player does not have (a booster not held, extra moves the coins do not cover or
        /// an ad the day has none of left): the screen that offered it should not have.
        /// </exception>
        public PlayerProgress Apply(PlayerProgress progress, AttemptEvent happened, DateTimeOffset now) => Apply(progress, happened, now, WalletHandling.Normal);

        /// <summary>The progress after <paramref name="happened"/>, which happened at <paramref name="now"/> on the player's clock, for a wallet held as the client holds it.</summary>
        /// <exception cref="InvalidOperationException">
        /// The event spends what the player does not have (a booster not held, extra moves the coins do not cover or
        /// an ad the day has none of left): the screen that offered it should not have.
        /// </exception>
        public PlayerProgress Apply(PlayerProgress progress, AttemptEvent happened, DateTimeOffset now, WalletHandling handling)
        {
            Wallet wallet = progress.Wallet;
            switch (happened)
            {
                case AttemptEvent.FirstMove first:
                    return progress.WithAttemptStarted().WithWallet(wallet.WithShelf(first.StartBoosters.TakenFrom(wallet.Shelf)));
                case AttemptEvent.BoosterAccepted used:
                    return progress.WithWallet(wallet.WithShelf(wallet.Shelf.WithOneFewer(used.Booster)));
                case AttemptEvent.ExtraMovesTaken taken:
                    return progress.WithWallet(PaidForTheExtraMoves(wallet, taken.PaidBy, now, handling));
                case AttemptEvent.Won won:
                    return WithTheOpeningGiftsDue(progress.WithLevelWon(_levelCount).WithWallet(wallet.WithCoins(wallet.Coins + _rules.Win.Of(won.MovesLeft))));
                case AttemptEvent.LostForGood _:
                case AttemptEvent.Left { AfterAMove: true }:
                    return handling == WalletHandling.Unlimited ? progress : progress.WithWallet(wallet.WithLives(OneLifeLess(wallet.Lives, now)));
                case AttemptEvent.Left _:
                    return progress;
                default:
                    throw new InvalidOperationException($"The ledger does not know the event {happened.GetType().Name}.");
            }
        }

        /// <summary>
        /// The progress with the gift of every booster used during play that is open on the level the player stands
        /// on and was not given yet (Puzzle.Meta's BoosterUnlocks); the very progress when none is due. A win gives
        /// what the next level opens; a session gives, as it opens, what a save from before the gifts is owed.
        /// </summary>
        public PlayerProgress WithTheOpeningGiftsDue(PlayerProgress progress)
        {
            int standsOn = progress.LevelIndexIn(_levelCount) + 1;
            Wallet given = _rules.Unlocks.GivenTo(progress.Wallet, standsOn);
            return given.Equals(progress.Wallet) ? progress : progress.WithWallet(given);
        }

        private Wallet PaidForTheExtraMoves(Wallet wallet, ExtraMovesPayment paidBy, DateTimeOffset now, WalletHandling handling)
        {
            switch (paidBy)
            {
                case ExtraMovesPayment.ByCoins:
                    return handling.Buy(_rules.ExtraMoves.Price, wallet, _rules.ExtraMoves.PaidWithCoins) is Purchase.Bought bought
                        ? bought.Wallet
                        : throw new InvalidOperationException($"Extra moves were given for {_rules.ExtraMoves.Price} coins, and the player has {wallet.Coins}.");
                case ExtraMovesPayment.ByAd:
                    return _rules.ExtraMoves.PaidWithAnAd(wallet, DayStamp.Of(now)) is AdOutcome.Rewarded rewarded
                        ? rewarded.Wallet
                        : throw new InvalidOperationException("Extra moves were given for an ad on a day that has no ad for them left.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(paidBy), paidBy, "Extra moves are paid by coins or by an ad.");
            }
        }

        /// <summary>
        /// The meter after a life was lost at <paramref name="now"/>. A meter that is empty even with the lives that
        /// came back by then gives nothing more: until the level's start refuses an attempt without a life, a player
        /// can play on an empty meter, and such a loss leaves it empty with its clock as it runs.
        /// </summary>
        private LifeMeter OneLifeLess(LifeMeter lives, DateTimeOffset now)
        {
            LifeMeter settled = lives.SettledAt(now, _rules.Lives);
            return settled.HasOneUnder(_rules.Lives) ? settled.WithOneLost(now, _rules.Lives) : settled;
        }
    }
}
