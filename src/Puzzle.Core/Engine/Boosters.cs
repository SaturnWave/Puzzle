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
