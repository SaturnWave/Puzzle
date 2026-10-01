using System;

namespace Puzzle.Core.Model
{
    /// <summary>
    /// Mutable per-game cell state: the tile slot, the chain overlay and the jelly floor of every cell.
    /// Readers (view, bot) use the <see cref="Position"/> API; the engine mutates through internal index access.
    /// </summary>
    public sealed class Board
    {
        private readonly Tile[] _tiles;
        private readonly byte[] _chains;
        private readonly byte[] _jelly;

        internal Board(BoardShape shape)
        {
            Shape = shape;
            _tiles = new Tile[shape.CellCount];
            _chains = new byte[shape.CellCount];
            _jelly = new byte[shape.CellCount];
        }

        private Board(Board source)
        {
            Shape = source.Shape;
            _tiles = (Tile[])source._tiles.Clone();
            _chains = (byte[])source._chains.Clone();
            _jelly = (byte[])source._jelly.Clone();
        }

        public BoardShape Shape { get; }

        public int Width => Shape.Width;

        public int Height => Shape.Height;

        /// <summary>The tile at a position; <see cref="Tile.Empty"/> for gaps and out-of-board positions.</summary>
        public Tile TileAt(Position position) => Shape.IsPlayable(position) ? _tiles[Shape.IndexOf(position)] : Tile.Empty;

        /// <summary>Remaining chain hit points over the tile (0 = not chained).</summary>
        public int ChainAt(Position position) => Shape.IsPlayable(position) ? _chains[Shape.IndexOf(position)] : 0;

        /// <summary>Remaining jelly layers on the floor (0 = clean).</summary>
        public int JellyAt(Position position) => Shape.IsPlayable(position) ? _jelly[Shape.IndexOf(position)] : 0;

        /// <summary>Order-sensitive FNV-1a hash of the whole cell state; used to prove determinism.</summary>
        public ulong ComputeHash()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            unchecked
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    Tile tile = _tiles[i];
                    hash = (hash ^ (byte)tile.Kind) * prime;
                    hash = (hash ^ (tile.IsGem ? (byte)tile.Color : (byte)0)) * prime;
                    hash = (hash ^ (byte)tile.Hp) * prime;
                    hash = (hash ^ _chains[i]) * prime;
                    hash = (hash ^ _jelly[i]) * prime;
                }
            }

            return hash;
        }

        internal Tile Get(int index) => _tiles[index];

        internal void Set(int index, Tile tile) => _tiles[index] = tile;

        internal int Chain(int index) => _chains[index];

        internal void SetChain(int index, int hp) => _chains[index] = checked((byte)hp);

        internal int Jelly(int index) => _jelly[index];

        internal void SetJelly(int index, int layers) => _jelly[index] = checked((byte)layers);

        /// <summary>Movable and not held by a chain.</summary>
        internal bool CanFall(int index) => _chains[index] == 0 && _tiles[index].Kind.IsMovable();

        /// <summary>Swappable by the player and not held by a chain.</summary>
        internal bool CanSwap(int index) => _chains[index] == 0 && _tiles[index].Kind.IsSwappable();

        internal void Swap(int a, int b)
        {
            Tile tile = _tiles[a];
            _tiles[a] = _tiles[b];
            _tiles[b] = tile;
        }

        internal Board Clone() => new Board(this);
    }
}
