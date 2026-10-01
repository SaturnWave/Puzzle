using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Events
{
    /// <summary>The shape of a special's effect, for the view to pick an animation.</summary>
    public enum EffectKind : byte
    {
        ClearRow,
        ClearColumn,
        Explode,

        /// <summary>
        /// Rows and columns through the origin, Radius cells to each side: rocket + rocket is one row and one
        /// column (radius 0), rocket + bomb three of each (radius 1), rocket + big bomb five (radius 2).
        /// </summary>
        Cross,
        Seeker,
        ClearColor,

        /// <summary>Color bomb + special: every gem of one color turns into that special and fires.</summary>
        ConvertColor,
        ClearBoard,
    }

    /// <summary>
    /// Something the view should animate, in resolution order. Closed set (private base constructor).
    /// Recorded only when <see cref="GameOptions.RecordEvents"/> is on.
    /// </summary>
    public abstract record GameEvent
    {
        private GameEvent()
        {
        }

        public sealed record Swapped(Position From, Position To) : GameEvent;

        /// <summary>Marks the start of a match round; round 0 follows the player's move, later rounds are cascades.</summary>
        public sealed record RoundStarted(int Round) : GameEvent;

        public sealed record MatchCleared(GemColor Color, IReadOnlyList<Position> Cells) : GameEvent;

        /// <summary>A special appeared, from a match pattern or a color-bomb conversion.</summary>
        public sealed record SpecialCreated(Position At, TileKind Kind) : GameEvent;

        /// <summary>A special fired. <paramref name="Radius"/> is the blast radius or cross half-width (0 when unused).</summary>
        public sealed record EffectTriggered(Position Origin, EffectKind Kind, int Radius) : GameEvent;

        public sealed record SeekerLaunched(Position From, Position Target) : GameEvent;

        public sealed record TileDestroyed(Position At, Tile Tile) : GameEvent;

        public sealed record BoxDamaged(Position At, int HpLeft) : GameEvent;

        public sealed record ChainDamaged(Position At, int HpLeft) : GameEvent;

        public sealed record JellyDamaged(Position At, int LayersLeft) : GameEvent;

        public sealed record TileMoved(Position From, Position To) : GameEvent;

        public sealed record TileSpawned(Position At, Tile Tile) : GameEvent;

        public sealed record ItemCollected(Position At) : GameEvent;

        public sealed record Shuffled : GameEvent;
    }
}
