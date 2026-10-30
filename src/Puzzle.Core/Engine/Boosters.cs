using System;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// A booster taken before the level starts (docs/GAME_DESIGN.md §18). The engine keeps its own names; what the
    /// player sees them called is the theme's business. The numbers are the order in which a game takes its boosters,
    /// and so the order of its random draws: a new booster gets the next number, never one in between.
    /// </summary>
    public enum StartBooster : byte
    {
        /// <summary>The board starts with one rocket and one bomb on it.</summary>
        RocketAndBomb = 0,

        /// <summary>The board starts with one colour bomb on it.</summary>
        ColorBomb = 1,

        /// <summary>The move limit is <see cref="RuleSet.StartBoosterExtraMoves"/> longer.</summary>
        ExtraMoves = 2,
    }

    /// <summary>
    /// A booster used during play: an action of the player that costs no move. Closed set (private base constructor).
    /// Bots never use one, so move limits are measured without them and a booster stays a real advantage.
    /// </summary>
    public abstract record BoosterUse
    {
        private BoosterUse()
        {
        }

        /// <summary>Hits one cell, as a match on it would.</summary>
        public sealed record HitCell(Position At) : BoosterUse;

        /// <summary>Hits every cell of the row through <paramref name="At"/>.</summary>
        public sealed record HitRow(Position At) : BoosterUse;

        /// <summary>Hits every cell of the column through <paramref name="At"/>.</summary>
        public sealed record HitColumn(Position At) : BoosterUse;

        /// <summary>Rearranges the free gems, as the game does by itself when no move is left.</summary>
        public sealed record Shuffle : BoosterUse;
    }

    /// <summary>
    /// How the shuffle booster looks for its new arrangement (<see cref="GameOptions.ShuffleBoosterRule"/>). The game's
    /// own shuffle of a board without a move follows neither: it is the same under both. An attempt's record names the
    /// rule its game was played under (<see cref="Recording.AttemptStart.ShuffleRule"/>), so that it replays to the same
    /// end after the rule changed. The numbers are written into records that outlive a build: a number is never changed
    /// or given to another rule, and a new rule gets the next one.
    /// </summary>
    public enum ShuffleBoosterRule : byte
    {
        /// <summary>
        /// The first rule, until 2026-10-15, and the rule of every record that names none: up to
        /// <see cref="RuleSet.ShuffleAttempts"/> random orders of the free gems, refused when none of them leaves the
        /// board without a ready match, with a move and other than before. On large open boards it was refused often.
        /// </summary>
        RandomOrders = 1,

        /// <summary>
        /// Since 2026-10-15: the free gems are laid one by one in random order so that none completes a match, the
        /// rule the shuffle booster always follows. Refused only when the free gems are all one kind, so no order can
        /// change the board, or when no laying of them leaves a match-free board with a move (a tiny board).
        /// </summary>
        LaidWithoutAMatch = 2,
    }

    /// <summary>
    /// The names boosters go by in attempt records (<see cref="Recording.AttemptOutcome.BoostersUsed"/>). They are
    /// written into files that outlive a build: a name is never changed or given to another booster.
    /// </summary>
    public static class BoosterNames
    {
        internal const string HitCellName = "hit-cell";
        internal const string HitRowName = "hit-row";
        internal const string HitColumnName = "hit-column";
        internal const string ShuffleName = "shuffle";

        private const string RocketAndBombName = "start-rocket-and-bomb";
        private const string ColorBombName = "start-color-bomb";
        private const string ExtraMovesName = "start-extra-moves";

        public static string Of(StartBooster booster) => booster switch
        {
            StartBooster.RocketAndBomb => RocketAndBombName,
            StartBooster.ColorBomb => ColorBombName,
            StartBooster.ExtraMoves => ExtraMovesName,
            _ => throw new ArgumentOutOfRangeException(nameof(booster), booster, "Unknown start booster."),
        };

        public static string Of(BoosterUse booster) => booster switch
        {
            BoosterUse.HitCell _ => HitCellName,
            BoosterUse.HitRow _ => HitRowName,
            BoosterUse.HitColumn _ => HitColumnName,
            BoosterUse.Shuffle _ => ShuffleName,
            _ => throw new InvalidOperationException($"Unhandled booster {booster.GetType().Name}."),
        };

        /// <summary>The start booster a record names; nothing for any other name: a booster of play, or one of a later game.</summary>
        public static StartBooster? StartBoosterNamed(string name) => name switch
        {
            RocketAndBombName => StartBooster.RocketAndBomb,
            ColorBombName => StartBooster.ColorBomb,
            ExtraMovesName => StartBooster.ExtraMoves,
            _ => null,
        };
    }
}
