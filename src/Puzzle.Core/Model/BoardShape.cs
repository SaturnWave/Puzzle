using System;
using System.Collections.Generic;

namespace Puzzle.Core.Model
{
    /// <summary>
    /// The static geometry of a level: which cells exist, where tiles spawn and where items exit.
    /// Immutable and shared by every copy of a board.
    /// </summary>
    /// <remarks>
    /// Gaps (non-playable cells) are fall-through: a tile above a gap drops past it into the next playable cell
    /// of the same column. Every column's topmost playable cell spawns new tiles (unless the column's spawner is
    /// disabled) and its bottommost playable cell is the exit that collects items.
    /// </remarks>
    public sealed class BoardShape
    {
        public const int MaxDimension = 16;

        private const int None = -1;

        private readonly bool[] _playable;
        private readonly bool[] _spawnerColumn;
        private readonly int[] _left;
        private readonly int[] _right;
        private readonly int[] _up;
        private readonly int[] _down;
        private readonly int[] _upLeft;
        private readonly int[] _upRight;
        private readonly int[] _downLeft;
        private readonly int[] _downRight;
        private readonly int[] _fallSource;
        private readonly int[] _fallTarget;
        private readonly int[][] _columnsBottomUp;

        /// <param name="width">Column count, 1..<see cref="MaxDimension"/>.</param>
        /// <param name="height">Row count, 1..<see cref="MaxDimension"/>.</param>
        /// <param name="playable">Row-major flags, <c>width * height</c> long.</param>
        /// <param name="spawnerColumns">Optional per-column spawner switch; every column spawns when omitted.</param>
        public BoardShape(int width, int height, IReadOnlyList<bool> playable, IReadOnlyList<bool>? spawnerColumns = null)
        {
            if (width < 1 || width > MaxDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Width must be 1..{MaxDimension}.");
            }

            if (height < 1 || height > MaxDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Height must be 1..{MaxDimension}.");
            }

            if (playable.Count != width * height)
            {
                throw new ArgumentException($"Expected {width * height} cell flags, got {playable.Count}.", nameof(playable));
            }

            if (spawnerColumns is not null && spawnerColumns.Count != width)
            {
                throw new ArgumentException($"Expected {width} spawner flags, got {spawnerColumns.Count}.", nameof(spawnerColumns));
            }

            Width = width;
            Height = height;
            CellCount = width * height;
            _playable = new bool[CellCount];
            for (int i = 0; i < CellCount; i++)
            {
                _playable[i] = playable[i];
                PlayableCount += playable[i] ? 1 : 0;
            }

            if (PlayableCount == 0)
            {
                throw new ArgumentException("A board needs at least one playable cell.", nameof(playable));
            }

            _spawnerColumn = new bool[width];
            for (int x = 0; x < width; x++)
            {
                _spawnerColumn[x] = spawnerColumns is null || spawnerColumns[x];
            }

            _left = BuildNeighbors(-1, 0);
            _right = BuildNeighbors(1, 0);
            _up = BuildNeighbors(0, -1);
            _down = BuildNeighbors(0, 1);
            _upLeft = BuildNeighbors(-1, -1);
            _upRight = BuildNeighbors(1, -1);
            _downLeft = BuildNeighbors(-1, 1);
            _downRight = BuildNeighbors(1, 1);
            _fallSource = BuildFallLinks(-1);
            _fallTarget = BuildFallLinks(1);
            _columnsBottomUp = BuildColumns();
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount { get; }

        public int PlayableCount { get; }

        public static BoardShape Rectangle(int width, int height)
        {
            var playable = new bool[width * height];
            Array.Fill(playable, true);
            return new BoardShape(width, height, playable);
        }

        public bool Contains(Position position) =>
            position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        public bool IsPlayable(Position position) => Contains(position) && _playable[IndexOf(position)];

        /// <summary>True for the topmost playable cell of a column whose spawner is enabled.</summary>
        public bool IsSpawner(Position position) =>
            IsPlayable(position) && _spawnerColumn[position.X] && _fallSource[IndexOf(position)] == None;

        /// <summary>True for the bottommost playable cell of a column, where items are collected.</summary>
        public bool IsExit(Position position) => IsPlayable(position) && _fallTarget[IndexOf(position)] == None;

        public bool HasSpawner(int column) => _spawnerColumn[column];

        internal int IndexOf(Position position) => position.Y * Width + position.X;

        internal int IndexOf(int x, int y) => y * Width + x;

        internal Position PositionOf(int index) => new Position(index % Width, index / Width);

        internal int XOf(int index) => index % Width;

        internal int YOf(int index) => index / Width;

        internal bool IsPlayable(int index) => _playable[index];

        internal bool IsExit(int index) => _playable[index] && _fallTarget[index] == None;

        internal int Left(int index) => _left[index];

        internal int Right(int index) => _right[index];

        internal int Up(int index) => _up[index];

        internal int Down(int index) => _down[index];

        internal int UpLeft(int index) => _upLeft[index];

        internal int UpRight(int index) => _upRight[index];

        internal int DownLeft(int index) => _downLeft[index];

        internal int DownRight(int index) => _downRight[index];

        /// <summary>The playable cell a tile falls from into <paramref name="index"/> (gaps skipped), or -1 at the top.</summary>
        internal int FallSource(int index) => _fallSource[index];

        /// <summary>The playable cell a tile at <paramref name="index"/> falls into (gaps skipped), or -1 at the bottom.</summary>
        internal int FallTarget(int index) => _fallTarget[index];

        /// <summary>Playable cell indices of a column ordered bottom to top.</summary>
        internal IReadOnlyList<int> ColumnBottomUp(int column) => _columnsBottomUp[column];

        private int[] BuildNeighbors(int dx, int dy)
        {
            var neighbors = new int[CellCount];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    bool exists = nx >= 0 && nx < Width && ny >= 0 && ny < Height && _playable[IndexOf(nx, ny)];
                    neighbors[IndexOf(x, y)] = exists ? IndexOf(nx, ny) : None;
                }
            }

            return neighbors;
        }

        private int[] BuildFallLinks(int dy)
        {
            var links = new int[CellCount];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int link = None;
                    for (int ny = y + dy; ny >= 0 && ny < Height; ny += dy)
                    {
                        if (_playable[IndexOf(x, ny)])
                        {
                            link = IndexOf(x, ny);
                            break;
                        }
                    }

                    links[IndexOf(x, y)] = link;
                }
            }

            return links;
        }

        private int[][] BuildColumns()
        {
            var columns = new int[Width][];
            var buffer = new List<int>(Height);
            for (int x = 0; x < Width; x++)
            {
                buffer.Clear();
                for (int y = Height - 1; y >= 0; y--)
                {
                    if (_playable[IndexOf(x, y)])
                    {
                        buffer.Add(IndexOf(x, y));
                    }
                }

                columns[x] = buffer.ToArray();
            }

            return columns;
        }
    }
}
