#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Client.Boards
{
    /// <summary>A corner of the grid of cells: (0, 0) is the board's top left corner, x grows to the right and y downwards.</summary>
    public readonly struct GridCorner : IEquatable<GridCorner>
    {
        public GridCorner(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(GridCorner other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is GridCorner other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(GridCorner left, GridCorner right) => left.Equals(right);

        public static bool operator !=(GridCorner left, GridCorner right) => !left.Equals(right);
    }

    /// <summary>
    /// The outline of a board, worked out from its playable cells. The gilt frame, its row of beads, the shadow on
    /// the wall and the shade on the tray are all this one outline, moved out or in by a few points. Boards are often
    /// not rectangles and can have holes, so the outline is a set of closed loops. This file has no Unity
    /// dependency, so the repository's tests compile it too.
    /// </summary>
    public static class BoardOutline
    {
        /// <summary>
        /// The loops of corners around the playable cells. Seen on the screen, a loop runs clockwise around a piece of
        /// the board and the other way around a hole in it, so the cells always lie to the right of the walk and
        /// "outwards" is always to its left. Only true corners are listed: a straight run of cell edges is one side.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<GridCorner>> LoopsOf(BoardShape shape)
        {
            var walk = new EdgeWalk();
            for (int y = 0; y < shape.Height; y++)
            {
                for (int x = 0; x < shape.Width; x++)
                {
                    if (shape.IsPlayable(new Position(x, y)))
                    {
                        walk.AddOpenEdgesOf(x, y, shape);
                    }
                }
            }

            return walk.Loops();
        }

        /// <summary>One cell edge that has no cell on its other side, in the direction the outline is walked.</summary>
        private readonly struct Edge
        {
            public Edge(GridCorner from, GridCorner to)
            {
                From = from;
                To = to;
            }

            public GridCorner From { get; }

            public GridCorner To { get; }

            /// <summary>Positive when <paramref name="next"/> turns right off this edge, zero when it goes straight on.</summary>
            public int TurnTo(Edge next) =>
                (To.X - From.X) * (next.To.Y - next.From.Y) - (To.Y - From.Y) * (next.To.X - next.From.X);
        }

        /// <summary>The open edges of all cells, and the walk that strings them into loops.</summary>
        private sealed class EdgeWalk
        {
            private readonly List<Edge> _edges = new List<Edge>();
            private readonly List<bool> _walked = new List<bool>();
            private readonly Dictionary<GridCorner, List<int>> _edgesFrom = new Dictionary<GridCorner, List<int>>();

            /// <summary>Every loop has a top edge, and the first one met in reading order starts at a true corner: walks start there.</summary>
            private readonly List<int> _topEdges = new List<int>();

            public void AddOpenEdgesOf(int x, int y, BoardShape shape)
            {
                bool IsCell(int cellX, int cellY) => shape.IsPlayable(new Position(cellX, cellY));

                if (!IsCell(x, y - 1))
                {
                    _topEdges.Add(Add(new GridCorner(x, y), new GridCorner(x + 1, y)));
                }

                if (!IsCell(x + 1, y))
                {
                    Add(new GridCorner(x + 1, y), new GridCorner(x + 1, y + 1));
                }

                if (!IsCell(x, y + 1))
                {
                    Add(new GridCorner(x + 1, y + 1), new GridCorner(x, y + 1));
                }

                if (!IsCell(x - 1, y))
                {
                    Add(new GridCorner(x, y + 1), new GridCorner(x, y));
                }
            }

            public List<IReadOnlyList<GridCorner>> Loops()
            {
                var loops = new List<IReadOnlyList<GridCorner>>();
                foreach (int topEdge in _topEdges)
                {
                    // An earlier loop may have walked this edge already.
                    if (!_walked[topEdge])
                    {
                        loops.Add(WalkLoopFrom(topEdge));
                    }
                }

                return loops;
            }

            private int Add(GridCorner from, GridCorner to)
            {
                int index = _edges.Count;
                _edges.Add(new Edge(from, to));
                _walked.Add(false);
                if (!_edgesFrom.TryGetValue(from, out List<int>? leaving))
                {
                    leaving = new List<int>();
                    _edgesFrom[from] = leaving;
                }

                leaving.Add(index);
                return index;
            }

            private List<GridCorner> WalkLoopFrom(int firstEdge)
            {
                var corners = new List<GridCorner>();
                int? previous = null;
                for (int? edge = firstEdge; edge is int current; edge = UnwalkedEdgeAfter(current))
                {
                    _walked[current] = true;
                    bool goesStraightOn = previous is int before && _edges[before].TurnTo(_edges[current]) == 0;
                    if (!goesStraightOn)
                    {
                        corners.Add(_edges[current].From);
                    }

                    previous = current;
                }

                return corners;
            }

            /// <summary>
            /// The edge the walk goes on with, or none when the loop has closed. Where two cells touch only at a
            /// corner, two edges leave that corner: the sharpest right turn keeps each loop on its own cell.
            /// </summary>
            private int? UnwalkedEdgeAfter(int edge)
            {
                int? best = null;
                foreach (int candidate in _edgesFrom[_edges[edge].To])
                {
                    bool turnsMoreToTheRight = best is not int chosen || _edges[edge].TurnTo(_edges[candidate]) > _edges[edge].TurnTo(_edges[chosen]);
                    if (!_walked[candidate] && turnsMoreToTheRight)
                    {
                        best = candidate;
                    }
                }

                return best;
            }
        }
    }
}
