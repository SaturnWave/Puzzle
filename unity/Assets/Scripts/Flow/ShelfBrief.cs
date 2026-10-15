#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Screens;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the board's shelf of boosters used during play offers in one attempt: which of them are open on its level
    /// and which wait for a later one (the owner's decision of 2026-10-15), the one that opens on this level and is
    /// shown for the first time, and what one more of each costs where none is held. The boosters the player holds
    /// are told to the stage apart from this (Puzzle.Meta's Wallet): they change while the attempt is played.
    /// No Unity dependency.
    /// </summary>
    public sealed class ShelfBrief
    {
        private readonly BoosterUnlocks _unlocks;
        private readonly Shop _shop;

        /// <param name="levelNumber">The number the player knows the level by.</param>
        /// <param name="isFirstAttempt">True for the first try at the level: a booster that opens on it is shown for the first time then.</param>
        public ShelfBrief(int levelNumber, bool isFirstAttempt, BoosterUnlocks unlocks, Shop shop)
        {
            if (levelNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), levelNumber, "Levels are numbered from 1.");
            }

            LevelNumber = levelNumber;
            _unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            Introduced = isFirstAttempt ? unlocks.OpeningOn(levelNumber).Cast<BoosterKind?>().FirstOrDefault() : null;
        }

        /// <summary>The boosters the shelf shows, from its left end after the pause disc: the four used during play, in the engine's order.</summary>
        public static IReadOnlyList<BoosterKind> Boosters => BoosterKinds.InLevel;

        public int LevelNumber { get; }

        /// <summary>
        /// The booster met for the first time: the one that opens on this level, on the level's first try, where it is
        /// ringed and a word says how it is used until it is first touched. Nothing on any other attempt.
        /// </summary>
        public BoosterKind? Introduced { get; }

        /// <summary>
        /// True when the word on a booster's first use is the very first of them, the spatula's on its level: the
        /// honest line goes with it, that every level can be won without boosters (they are calibrated without).
        /// </summary>
        public bool SaysEveryLevelWithout => Introduced is BoosterKind introduced && Boosters.All(other => _unlocks.LevelOf(other) >= _unlocks.LevelOf(introduced));

        /// <summary>The shelf of a try at a level, by the economy's rules.</summary>
        /// <param name="attemptsBefore">How many times the level was tried before: 0 for the first try.</param>
        public static ShelfBrief Of(int levelNumber, int attemptsBefore, EconomyRules rules) =>
            new ShelfBrief(levelNumber, attemptsBefore == 0, rules.Unlocks, rules.Shop);

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

        public bool CanBuy(BoosterKind booster, Coins coins) => coins.Covers(PriceOf(booster));
    }
}
