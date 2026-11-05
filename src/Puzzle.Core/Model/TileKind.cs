namespace Puzzle.Core.Model
{
    /// <summary>What occupies the tile slot of a cell.</summary>
    public enum TileKind : byte
    {
        /// <summary>Nothing; only exists transiently while a move resolves. Must stay 0 so <c>default(Tile)</c> is empty.</summary>
        Empty = 0,
        Gem = 1,

        /// <summary>Clears its whole row.</summary>
        RocketH = 2,

        /// <summary>Clears its whole column.</summary>
        RocketV = 3,

        /// <summary>3x3 blast; made by an L, T or plus of exactly five gems.</summary>
        Bomb = 4,

        /// <summary>5x5 blast; made by a crossing shape of six or more gems.</summary>
        BigBomb = 10,

        /// <summary>Hits a plus shape around itself, then flies to the most goal-relevant cell.</summary>
        Seeker = 5,
        ColorBomb = 6,

        /// <summary>Static blocker with 1..3 hit points; stops gravity.</summary>
        Box = 7,

        /// <summary>Falls and can be swapped, never matches, cannot be destroyed; collected at the bottom of its column.</summary>
        Item = 8,

        /// <summary>A refill the bot cannot see yet during lookahead. Never produced in a live game.</summary>
        Hidden = 9,
    }

    public static class TileKinds
    {
        public static bool IsSpecial(this TileKind kind) => kind switch
        {
            TileKind.RocketH => true,
            TileKind.RocketV => true,
            TileKind.Bomb => true,
            TileKind.BigBomb => true,
            TileKind.Seeker => true,
            TileKind.ColorBomb => true,
            _ => false,
        };

        public static bool IsRocket(this TileKind kind) => kind == TileKind.RocketH || kind == TileKind.RocketV;

        public static bool IsBomb(this TileKind kind) => kind == TileKind.Bomb || kind == TileKind.BigBomb;

        /// <summary>Affected by gravity (when not chained).</summary>
        public static bool IsMovable(this TileKind kind) => kind switch
        {
            TileKind.Gem => true,
            TileKind.Item => true,
            TileKind.Hidden => true,
            _ => kind.IsSpecial(),
        };

        /// <summary>Can take part in a player swap (when not chained).</summary>
        public static bool IsSwappable(this TileKind kind) => kind == TileKind.Gem || kind == TileKind.Item || kind.IsSpecial();
    }
}
