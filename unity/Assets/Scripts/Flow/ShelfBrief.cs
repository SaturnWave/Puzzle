#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Screens;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the board's shelf of boosters used during play offers in one attempt: which of them are open on its level
    /// and which wait for a later one (the owner's decision of 2026-10-15), and what one more of each costs where none
    /// is held. The booster that opens on the level is used once by hand there, a step of the tutorial (#7,
    /// TutorialPlan). The boosters the player holds are told to the stage apart from this (Puzzle.Meta's Wallet): they
    /// change while the attempt is played. No Unity dependency.
    /// </summary>
    public sealed class ShelfBrief
    {
        private readonly BoosterUnlocks _unlocks;
        private readonly Shop _shop;

        /// <param name="levelNumber">The number the player knows the level by.</param>
        public ShelfBrief(int levelNumber, BoosterUnlocks unlocks, Shop shop)
        {
            if (levelNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), levelNumber, "Levels are numbered from 1.");
            }

            LevelNumber = levelNumber;
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        }

        /// <summary>The boosters the shelf shows, from its left end after the pause disc: the four used during play, in the engine's order.</summary>
        public static IReadOnlyList<BoosterKind> Boosters => BoosterKinds.InLevel;

        public int LevelNumber { get; }

        /// <summary>The shelf of a try at a level, by the economy's rules.</summary>
        public static ShelfBrief Of(int levelNumber, EconomyRules rules) => new ShelfBrief(levelNumber, rules.Unlocks, rules.Shop);

        /// <summary>True when the booster can be used on this level.</summary>
        public bool IsOpen(BoosterKind booster) => _unlocks.IsOpenOn(booster, LevelNumber);

        /// <summary>
        /// What the booster's disc shows: locked until its level, before it; otherwise how many are held and what the
        /// player is doing with it. A booster held none of is never armed: its disc offers more.
        /// </summary>
        /// <exception cref="ArgumentException">A booster held none of is said to be armed.</exception>
        public BoosterFace FaceOf(BoosterKind booster, BoosterShelf owned, BoosterChoice choice) => IsOpen(booster)
            ? BoosterFace.Of(owned.Count(booster), choice)
            : BoosterFace.LockedUntil(_unlocks.LevelOf(booster));

        /// <summary>One booster's single price, which the card that offers one asks.</summary>
        public Coins PriceOf(BoosterKind booster) => _shop.PriceOf(booster);

        /// <summary>True when the wallet pays for one booster of the kind: its coins cover the price, or have no end.</summary>
        public bool CanBuy(BoosterKind booster, WalletView wallet) => wallet.CanPay(PriceOf(booster));
    }
}
