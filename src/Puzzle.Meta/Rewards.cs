using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Meta
{
    /// <summary>What a won level pays (the settings' coins).</summary>
    public sealed class WinReward
    {
        public WinReward(Coins perMoveLeft, Coins perLevelWon)
        {
            PerMoveLeft = perMoveLeft;
            PerLevelWon = perLevelWon;
        }

        /// <summary>Paid for every move left when a level is won (the finale turns each into a rocket).</summary>
        public Coins PerMoveLeft { get; }

        /// <summary>Paid for the win itself, whatever is left.</summary>
        public Coins PerLevelWon { get; }

        /// <summary>
        /// The coins of a win with <paramref name="movesLeft"/> moves left. Moves that were bought or came with a
        /// booster and are left over are paid like any other, and the amount is the same whether the finale is
        /// watched or skipped: it counts the moves, not the rockets that found a gem to fly at.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The number of moves is negative.</exception>
        public Coins Of(int movesLeft)
        {
            if (movesLeft < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(movesLeft), movesLeft, "A win leaves none or more moves.");
            }

            return (PerMoveLeft * movesLeft) + PerLevelWon;
        }
    }

    /// <summary>What one reward holds: coins and boosters, in the order the rotation names them.</summary>
    public sealed class Reward : IEquatable<Reward>
    {
        public Reward(Coins coins, IReadOnlyList<BoosterKind> boosters)
        {
            Coins = coins;
            Boosters = (boosters ?? throw new ArgumentNullException(nameof(boosters))).ToArray();
        }

        public Coins Coins { get; }

        public IReadOnlyList<BoosterKind> Boosters { get; }

        /// <summary>The wallet with the reward's coins and boosters in it. Counting the reward is the business of the rule that gives it.</summary>
        public Wallet AddedTo(Wallet wallet)
        {
            BoosterShelf shelf = wallet.Shelf;
            foreach (BoosterKind booster in Boosters)
            {
                shelf = shelf.WithOneMore(booster);
            }

            return wallet.WithCoins(wallet.Coins + Coins).WithShelf(shelf);
        }

        public bool Equals(Reward? other) => other is not null && Coins == other.Coins && Boosters.SequenceEqual(other.Boosters);

        public override bool Equals(object? obj) => Equals(obj as Reward);

        public override int GetHashCode() => HashCode.Combine(Coins, Boosters.Count);

        public override string ToString() => $"{Coins} coins and {(Boosters.Count == 0 ? "no booster" : string.Join(", ", Boosters.Select(BoosterKinds.Name)))}";
    }

    /// <summary>
    /// What a reward gives each time it is given. Nothing is random: reward number n holds the next boosters of the
    /// rotation, so with one booster each time reward n holds the n-th booster of the list, starting over at its end.
    /// </summary>
    public sealed class RewardRotation
    {
        private readonly BoosterKind[] _rotation;

        /// <exception cref="ArgumentOutOfRangeException">A negative number of boosters each time.</exception>
        /// <exception cref="ArgumentException">Boosters are to be given and the rotation names none.</exception>
        public RewardRotation(Coins coins, int boostersEachTime, IReadOnlyList<BoosterKind> rotation)
        {
            if (boostersEachTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boostersEachTime), boostersEachTime, "A reward gives none or more boosters.");
            }

            _rotation = (rotation ?? throw new ArgumentNullException(nameof(rotation))).ToArray();
            if (boostersEachTime > 0 && _rotation.Length == 0)
            {
                throw new ArgumentException("A reward that gives boosters needs a rotation to take them from.", nameof(rotation));
            }

            Coins = coins;
            BoostersEachTime = boostersEachTime;
        }

        /// <summary>Coins with every reward.</summary>
        public Coins Coins { get; }

        public int BoostersEachTime { get; }

        /// <summary>The boosters given, in turn.</summary>
        public IReadOnlyList<BoosterKind> Rotation => _rotation;

        /// <summary>What reward number <paramref name="rewardNumber"/> of its kind holds; the first has number 0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The number is negative.</exception>
        public Reward Of(int rewardNumber)
        {
            if (rewardNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewardNumber), rewardNumber, "Rewards are numbered from 0.");
            }

            var boosters = new BoosterKind[BoostersEachTime];
            for (int booster = 0; booster < boosters.Length; booster++)
            {
                long place = ((long)rewardNumber * BoostersEachTime) + booster;
                boosters[booster] = _rotation[(int)(place % _rotation.Length)];
            }

            return new Reward(Coins, boosters);
        }
    }

    /// <summary>
    /// The reward of a completed painting (the settings' rewards.paintingCompleted). When a painting counts as
    /// completed is the caller's to say; the rule knows only how many such rewards the player has had.
    /// </summary>
    public sealed class PaintingReward
    {
        public PaintingReward(RewardRotation rotation)
        {
            Rotation = rotation ?? throw new ArgumentNullException(nameof(rotation));
        }

        public RewardRotation Rotation { get; }

        /// <summary>What the next painting the player completes gives.</summary>
        public Reward NextFor(Wallet wallet) => Rotation.Of(wallet.Rewards.ForPaintings);

        /// <summary>The wallet after a painting was completed: its reward is in it and counted.</summary>
        public Wallet GivenTo(Wallet wallet) =>
            NextFor(wallet).AddedTo(wallet).WithRewards(wallet.Rewards.WithOneForAPainting());
    }

    /// <summary>How taking the daily reward ended. Closed set.</summary>
    public abstract record DailyGift
    {
        private DailyGift()
        {
        }

        /// <summary>Given and counted: the wallet after it and what it held.</summary>
        public sealed record Given(Wallet Wallet, Reward Reward) : DailyGift;

        /// <summary>The player has had the reward of this turn; nothing changed. The next is due on <paramref name="DueOn"/>.</summary>
        public sealed record NotDueYet(DayStamp DueOn) : DailyGift;
    }

    /// <summary>
    /// The daily reward (the settings' rewards.daily): one on the day a player first asks, then one every so many
    /// calendar days. A missed day is not made up for: the next reward is the next of the rotation, whenever it is taken.
    /// </summary>
    public sealed class DailyReward
    {
        /// <exception cref="ArgumentOutOfRangeException">A reward more often than daily.</exception>
        public DailyReward(RewardRotation rotation, int everyDays)
        {
            if (everyDays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(everyDays), everyDays, "The daily reward is given every day or every few days.");
            }

            Rotation = rotation ?? throw new ArgumentNullException(nameof(rotation));
            EveryDays = everyDays;
        }

        public RewardRotation Rotation { get; }

        /// <summary>A reward is due this many days after the one before it.</summary>
        public int EveryDays { get; }

        /// <summary>What the next daily reward the player takes gives.</summary>
        public Reward NextFor(Wallet wallet) => Rotation.Of(wallet.Rewards.Daily);

        /// <summary>The first day the player's next reward can be taken; absent for a player who never had one, who can take it any day.</summary>
        public DayStamp? DueOn(Wallet wallet) => wallet.Rewards.LastDailyOn?.Plus(EveryDays);

        /// <summary>True when a reward waits on <paramref name="today"/>. A day before the latest reward's (the date set back) has none.</summary>
        public bool IsDueOn(Wallet wallet, DayStamp today) => DueOn(wallet) is not DayStamp due || today >= due;

        public DailyGift TakenOn(Wallet wallet, DayStamp today)
        {
            if (DueOn(wallet) is DayStamp due && today < due)
            {
                return new DailyGift.NotDueYet(due);
            }

            Reward reward = NextFor(wallet);
            return new DailyGift.Given(reward.AddedTo(wallet).WithRewards(wallet.Rewards.WithTheDailyOf(today)), reward);
        }
    }
}
