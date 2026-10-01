using System;
using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Resolution
{
    internal enum Axis : byte
    {
        Horizontal,
        Vertical,
    }

    /// <summary>A straight line of 3+ same-colored gems.</summary>
    internal readonly struct Run
    {
        public Run(int start, int length, Axis axis)
        {
            Start = start;
            Length = length;
            Axis = axis;
        }

        public int Start { get; }

        public int Length { get; }

        public Axis Axis { get; }

        public int CellAt(int offset, int boardWidth) => Start + offset * (Axis == Axis.Horizontal ? 1 : boardWidth);
    }

    internal readonly struct SpecialSpawn
    {
        public SpecialSpawn(TileKind kind, int anchor)
        {
            Kind = kind;
            Anchor = anchor;
        }

        public TileKind Kind { get; }

        public int Anchor { get; }
    }

    /// <summary>Same-colored gems that clear together, and the special they leave behind (if any).</summary>
    internal sealed class MatchGroup
    {
        public MatchGroup(GemColor color)
        {
            Color = color;
        }

        public GemColor Color { get; }

        public List<int> Cells { get; } = new List<int>();

        public SpecialSpawn? Special { get; set; }

        // Pattern facts gathered while classifying the group.
        public Run? FiveRun { get; set; }

        public Run? FourRun { get; set; }

        public int CrossCell { get; set; } = -1;

        public int SquareCell { get; set; } = -1;
    }

    /// <summary>
    /// Finds match groups on a stable board. Lines of 3+ and 2x2 squares are the patterns; cells are grouped
    /// with union-find over pattern membership, so two patterns merge only when they share a cell.
    /// </summary>
    internal sealed class MatchFinder
    {
        private static readonly IReadOnlyList<MatchGroup> NoGroups = Array.Empty<MatchGroup>();

        private readonly Board _board;
        private readonly BoardShape _shape;
        private readonly int[] _hRun;
        private readonly int[] _vRun;
        private readonly bool[] _inMatch;
        private readonly int[] _parent;
        private readonly int[] _groupOfRoot;
        private readonly List<Run> _runs = new List<Run>();
        private readonly List<int> _squares = new List<int>();

        public MatchFinder(Board board)
        {
            _board = board;
            _shape = board.Shape;
            _hRun = new int[_shape.CellCount];
            _vRun = new int[_shape.CellCount];
            _inMatch = new bool[_shape.CellCount];
            _parent = new int[_shape.CellCount];
            _groupOfRoot = new int[_shape.CellCount];
        }

        /// <param name="preferredA">A cell the player moved (special spawns there if it is in the group), or -1.</param>
        /// <param name="preferredB">The other moved cell, or -1.</param>
        public IReadOnlyList<MatchGroup> FindGroups(int preferredA, int preferredB)
        {
            if (!CollectPatterns())
            {
                return NoGroups;
            }

            var groups = new List<MatchGroup>();
            BuildGroups(groups);
            foreach (MatchGroup group in groups)
            {
                Classify(group, preferredA, preferredB);
            }

            return groups;
        }

        /// <summary>Whether the gem at <paramref name="index"/> is part of a line of 3+ or a 2x2 square.</summary>
        public bool HasMatchAt(int index)
        {
            if (!IsMatchable(index))
            {
                return false;
            }

            if (1 + Extent(index, Direction.Left) + Extent(index, Direction.Right) >= 3
                || 1 + Extent(index, Direction.Up) + Extent(index, Direction.Down) >= 3)
            {
                return true;
            }

            return IsSquare(index, _shape.Left(index), _shape.Up(index), _shape.UpLeft(index))
                || IsSquare(index, _shape.Right(index), _shape.Up(index), _shape.UpRight(index))
                || IsSquare(index, _shape.Left(index), _shape.Down(index), _shape.DownLeft(index))
                || IsSquare(index, _shape.Right(index), _shape.Down(index), _shape.DownRight(index));
        }

        public bool AnyMatch()
        {
            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (HasMatchAt(i))
                {
                    return true;
                }
            }

            return false;
        }

        private enum Direction
        {
            Left,
            Right,
            Up,
            Down,
        }

        private bool IsMatchable(int index) => index >= 0 && _shape.IsPlayable(index) && _board.Get(index).IsGem;

        private bool SameGem(int a, int b) => b >= 0 && _board.Get(a).IsSameGemAs(_board.Get(b));

        private bool IsSquare(int index, int a, int b, int c) =>
            a >= 0 && b >= 0 && c >= 0 && SameGem(index, a) && SameGem(index, b) && SameGem(index, c);

        private int Step(int index, Direction direction) => direction switch
        {
            Direction.Left => _shape.Left(index),
            Direction.Right => _shape.Right(index),
            Direction.Up => _shape.Up(index),
            Direction.Down => _shape.Down(index),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
        };

        /// <summary>Count of same-colored gems beyond <paramref name="index"/> in one direction.</summary>
        private int Extent(int index, Direction direction)
        {
            int count = 0;
            int next = Step(index, direction);
            while (SameGem(index, next))
            {
                count++;
                next = Step(next, direction);
            }

            return count;
        }

        /// <summary>Fills run/square data for the whole board; false when nothing matches.</summary>
        private bool CollectPatterns()
        {
            Array.Clear(_hRun, 0, _hRun.Length);
            Array.Clear(_vRun, 0, _vRun.Length);
            Array.Clear(_inMatch, 0, _inMatch.Length);
            _runs.Clear();
            _squares.Clear();

            for (int y = 0; y < _shape.Height; y++)
            {
                CollectRuns(_shape.IndexOf(0, y), Direction.Right, Axis.Horizontal, _hRun);
            }

            for (int x = 0; x < _shape.Width; x++)
            {
                CollectRuns(_shape.IndexOf(x, 0), Direction.Down, Axis.Vertical, _vRun);
            }

            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (IsMatchable(i) && IsSquare(i, _shape.Right(i), _shape.Down(i), _shape.DownRight(i)))
                {
                    _squares.Add(i);
                    _inMatch[i] = _inMatch[_shape.Right(i)] = _inMatch[_shape.Down(i)] = _inMatch[_shape.DownRight(i)] = true;
                }
            }

            return _runs.Count > 0 || _squares.Count > 0;
        }

        /// <summary>Scans one row or column starting at its first cell, recording every run of 3+.</summary>
        private void CollectRuns(int lineStart, Direction direction, Axis axis, int[] runLength)
        {
            int stride = axis == Axis.Horizontal ? 1 : _shape.Width;
            int lineLength = axis == Axis.Horizontal ? _shape.Width : _shape.Height;
            int offset = 0;
            while (offset < lineLength)
            {
                int start = lineStart + offset * stride;
                if (!IsMatchable(start))
                {
                    offset++;
                    continue;
                }

                int length = 1 + Extent(start, direction);
                if (length >= 3)
                {
                    _runs.Add(new Run(start, length, axis));
                    for (int k = 0; k < length; k++)
                    {
                        int cell = start + k * stride;
                        runLength[cell] = length;
                        _inMatch[cell] = true;
                    }
                }

                offset += length;
            }
        }

        private void BuildGroups(List<MatchGroup> groups)
        {
            for (int i = 0; i < _parent.Length; i++)
            {
                _parent[i] = i;
                _groupOfRoot[i] = -1;
            }

            foreach (Run run in _runs)
            {
                for (int k = 1; k < run.Length; k++)
                {
                    Union(run.Start, run.CellAt(k, _shape.Width));
                }
            }

            foreach (int square in _squares)
            {
                Union(square, _shape.Right(square));
                Union(square, _shape.Down(square));
                Union(square, _shape.DownRight(square));
            }

            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (!_inMatch[i])
                {
                    continue;
                }

                int root = Find(i);
                if (_groupOfRoot[root] < 0)
                {
                    _groupOfRoot[root] = groups.Count;
                    groups.Add(new MatchGroup(_board.Get(i).Color));
                }

                groups[_groupOfRoot[root]].Cells.Add(i);
            }

            foreach (Run run in _runs)
            {
                MatchGroup group = groups[_groupOfRoot[Find(run.Start)]];
                if (run.Length >= 5 && group.FiveRun is null)
                {
                    group.FiveRun = run;
                }
                else if (run.Length == 4 && group.FourRun is null)
                {
                    group.FourRun = run;
                }
            }

            foreach (int square in _squares)
            {
                MatchGroup group = groups[_groupOfRoot[Find(square)]];
                if (group.SquareCell < 0)
                {
                    group.SquareCell = square;
                }
            }
        }

        /// <summary>Picks the special by pattern priority (5-line > L/T > 4-line > square) and where it spawns.</summary>
        private void Classify(MatchGroup group, int preferredA, int preferredB)
        {
            foreach (int cell in group.Cells)
            {
                if (_hRun[cell] >= 3 && _vRun[cell] >= 3)
                {
                    group.CrossCell = cell;
                    break;
                }
            }

            TileKind kind;
            int defaultAnchor;
            if (group.FiveRun is Run five)
            {
                kind = TileKind.ColorBomb;
                defaultAnchor = five.CellAt(five.Length / 2, _shape.Width);
            }
            else if (group.CrossCell >= 0)
            {
                kind = TileKind.Bomb;
                defaultAnchor = group.CrossCell;
            }
            else if (group.FourRun is Run four)
            {
                // The rocket fires across the line that made it.
                kind = four.Axis == Axis.Horizontal ? TileKind.RocketV : TileKind.RocketH;
                defaultAnchor = four.CellAt(1, _shape.Width);
            }
            else if (group.SquareCell >= 0)
            {
                kind = TileKind.Seeker;
                defaultAnchor = group.SquareCell;
            }
            else
            {
                return;
            }

            int anchor = ChooseAnchor(group, preferredA, preferredB, defaultAnchor);
            if (anchor >= 0)
            {
                group.Special = new SpecialSpawn(kind, anchor);
            }
        }

        private int ChooseAnchor(MatchGroup group, int preferredA, int preferredB, int defaultAnchor)
        {
            int groupRoot = Find(group.Cells[0]);
            if (IsFreeMemberOf(groupRoot, preferredA))
            {
                return preferredA;
            }

            if (IsFreeMemberOf(groupRoot, preferredB))
            {
                return preferredB;
            }

            if (_board.Chain(defaultAnchor) == 0)
            {
                return defaultAnchor;
            }

            foreach (int cell in group.Cells)
            {
                if (_board.Chain(cell) == 0)
                {
                    return cell;
                }
            }

            return -1;
        }

        private bool IsFreeMemberOf(int groupRoot, int cell) =>
            cell >= 0 && _inMatch[cell] && Find(cell) == groupRoot && _board.Chain(cell) == 0;

        private int Find(int cell)
        {
            while (_parent[cell] != cell)
            {
                _parent[cell] = _parent[_parent[cell]];
                cell = _parent[cell];
            }

            return cell;
        }

        private void Union(int a, int b)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if (rootA != rootB)
            {
                _parent[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
            }
        }
    }
}
