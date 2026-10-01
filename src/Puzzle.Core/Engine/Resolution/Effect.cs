using System;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>A queued special-tile effect waiting to be applied to the board.</summary>
    internal readonly struct Effect
    {
        private Effect(EffectKind kind, int origin, int radius, GemColor? color, TileKind? payload, int count, TileKind? requiredAtOrigin)
        {
            Kind = kind;
            Origin = origin;
            Radius = radius;
            Color = color;
            Payload = payload;
            Count = count;
            RequiredAtOrigin = requiredAtOrigin;
        }

        public EffectKind Kind { get; }

        public int Origin { get; }

        /// <summary>Blast radius of <see cref="EffectKind.Explode"/>.</summary>
        public int Radius { get; }

        /// <summary>Target color of <see cref="EffectKind.ClearColor"/>; absent means "most common color on the board".</summary>
        public GemColor? Color { get; }

        /// <summary>Special carried by a seeker, or produced by a color-bomb conversion.</summary>
        public TileKind? Payload { get; }

        /// <summary>Number of seeker flights.</summary>
        public int Count { get; }

        /// <summary>
        /// Set when the special still sits on the board while queued (color-bomb conversions). If another effect
        /// already fired it, the tile is gone and this entry is skipped instead of firing twice.
        /// </summary>
        public TileKind? RequiredAtOrigin { get; }

        public static Effect ForSpecial(TileKind special, int origin, RuleSet rules) => special switch
        {
            TileKind.RocketH => Simple(EffectKind.ClearRow, origin),
            TileKind.RocketV => Simple(EffectKind.ClearColumn, origin),
            TileKind.Bomb => new Effect(EffectKind.Explode, origin, rules.BombRadius, null, null, 0, null),
            TileKind.Seeker => new Effect(EffectKind.Seeker, origin, 0, null, null, 1, null),
            TileKind.ColorBomb => Simple(EffectKind.ClearColor, origin),
            _ => throw new ArgumentOutOfRangeException(nameof(special), special, "Not a special tile kind."),
        };

        public static Effect ClearColorOf(GemColor color, int origin) =>
            new Effect(EffectKind.ClearColor, origin, 0, color, null, 0, null);

        /// <summary>The effect of swapping two specials together; <paramref name="origin"/> is the swap destination.</summary>
        public static Effect ForCombo(TileKind first, TileKind second, int origin, RuleSet rules)
        {
            if (first == TileKind.ColorBomb || second == TileKind.ColorBomb)
            {
                TileKind other = first == TileKind.ColorBomb ? second : first;
                return other == TileKind.ColorBomb
                    ? Simple(EffectKind.ClearBoard, origin)
                    : new Effect(EffectKind.ConvertColor, origin, 0, null, other, 0, null);
            }

            if (first == TileKind.Seeker || second == TileKind.Seeker)
            {
                TileKind other = first == TileKind.Seeker ? second : first;
                return other == TileKind.Seeker
                    ? new Effect(EffectKind.Seeker, origin, 0, null, null, rules.SeekerComboTargets, null)
                    : new Effect(EffectKind.Seeker, origin, 0, null, other, 1, null);
            }

            bool firstIsRocket = first.IsRocket();
            bool secondIsRocket = second.IsRocket();
            if (firstIsRocket && secondIsRocket)
            {
                return Simple(EffectKind.Cross, origin);
            }

            if (firstIsRocket || secondIsRocket)
            {
                return Simple(EffectKind.WideCross, origin);
            }

            return new Effect(EffectKind.Explode, origin, rules.DoubleBombRadius, null, null, 0, null);
        }

        public Effect RequiringTileAtOrigin(TileKind kind) =>
            new Effect(Kind, Origin, Radius, Color, Payload, Count, kind);

        private static Effect Simple(EffectKind kind, int origin) => new Effect(kind, origin, 0, null, null, 0, null);
    }
}
