using System;

namespace Puzzle.Core.Model
{
    /// <summary>
    /// Immutable content of a cell's tile slot. A small value type because boards are copied constantly
    /// during bot lookahead.
    /// </summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        public const int MaxBoxHp = 3;

        private readonly GemColor _color;
        private readonly byte _hp;

        private Tile(TileKind kind, GemColor color, byte hp)
        {
            Kind = kind;
            _color = color;
            _hp = hp;
        }

        public static Tile Empty => default;

        public static Tile Item => new Tile(TileKind.Item, default, 0);

        public static Tile Hidden => new Tile(TileKind.Hidden, default, 0);

        public TileKind Kind { get; }

        /// <summary>Remaining hit points of a box; 0 for every other kind.</summary>
        public int Hp => _hp;

        /// <summary>The gem's color. Only gems have a color: specials are colorless.</summary>
        /// <exception cref="InvalidOperationException">The tile is not a gem.</exception>
        public GemColor Color => Kind == TileKind.Gem
            ? _color
            : throw new InvalidOperationException($"A {Kind} tile has no color.");

        public bool IsEmpty => Kind == TileKind.Empty;

        public bool IsGem => Kind == TileKind.Gem;

        public bool IsSpecial => Kind.IsSpecial();

        public static Tile Gem(GemColor color) => new Tile(TileKind.Gem, color, 0);

        public static Tile Special(TileKind kind) => kind.IsSpecial()
            ? new Tile(kind, default, 0)
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a special tile kind.");

        public static Tile Box(int hp) => hp >= 1 && hp <= MaxBoxHp
            ? new Tile(TileKind.Box, default, (byte)hp)
            : throw new ArgumentOutOfRangeException(nameof(hp), hp, $"Box hit points must be 1..{MaxBoxHp}.");

        public bool IsGemOf(GemColor color) => Kind == TileKind.Gem && _color == color;

        public bool IsSameGemAs(Tile other) => Kind == TileKind.Gem && other.Kind == TileKind.Gem && _color == other._color;

        public bool Equals(Tile other) => Kind == other.Kind && _color == other._color && _hp == other._hp;

        public override bool Equals(object? obj) => obj is Tile other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, _color, _hp);

        public override string ToString() => Kind switch
        {
            TileKind.Gem => _color.ToString(),
            TileKind.Box => $"Box{_hp}",
            _ => Kind.ToString(),
        };

        public static bool operator ==(Tile left, Tile right) => left.Equals(right);

        public static bool operator !=(Tile left, Tile right) => !left.Equals(right);
    }
}
