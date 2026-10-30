#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>A rocket and the cell it lies in.</summary>
    internal readonly struct RocketPlace
    {
        /// <exception cref="InvalidOperationException">The piece is no rocket.</exception>
        public RocketPlace(Position cell, TileKind rocket)
        {
            Cell = cell;
            IsAlongTheRow = rocket switch
            {
                TileKind.RocketH => true,
                TileKind.RocketV => false,
                _ => throw new InvalidOperationException($"A {rocket} in {cell} is no rocket, and only rockets fire in a wave of the finale."),
            };
        }

        public Position Cell { get; }

        /// <summary>True for a rocket whose stroke runs along its row; false for one that runs along its column.</summary>
        public bool IsAlongTheRow { get; }
    }

    /// <summary>
    /// One wave of a won game's finale, in the view's own time. The engine works a wave out rocket by rocket and
    /// says which rocket took each hit; shown that way, a stroke would pass over a piece that only bursts later,
    /// with the stroke of a rocket the engine happened to fire first. So the wave is told as it is seen. Its moves
    /// leave the medallion one after another and land. Its rockets then go off in the order they were thrown, or
    /// sooner when another's stroke reaches them, and a rocket that already stood on the board goes off when a
    /// stroke reaches it. And every hit is shown when a stroke gets to its cell: the first stroke to arrive there
    /// shows the first hit the engine counted on that cell, the second the second. A stroke hits every cell of its
    /// line once whatever lies there, so the board is the same in the end. This file has no Unity dependency, so
    /// the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// What a stroke sets off on its way hits as well: a bomb's blast, a seeker, a colour bomb's rays. Those are
    /// timed as in any turn, with one difference: where a stroke of the wave gets to a cell before such a hit
    /// does, the stroke shows the hit (<see cref="NextStrokeReaches"/>). To the engine the stroke came later
    /// and found the piece gone; seen, it is the stroke that takes it.
    /// </remarks>
    internal sealed class Volley
    {
        private readonly float _start;
        private readonly int _moves;
        private readonly float _leaveEvery;

        /// <summary>Every rocket that goes off in the wave, thrown or found standing, in the order they go off.</summary>
        private readonly List<Stroke> _strokes = new List<Stroke>();

        /// <summary>The rockets the engine has fired so far.</summary>
        private readonly List<Stroke> _fired = new List<Stroke>();
        private int _thrown;

        /// <param name="start">When the first move leaves the medallion.</param>
        /// <param name="thrown">The rockets the wave's moves make, in the order the engine named them.</param>
        /// <param name="standing">The rockets that lay on the board before the wave, free to be hit.</param>
        /// <exception cref="ArgumentException">The wave has no move.</exception>
        public Volley(float start, IReadOnlyList<RocketPlace> thrown, IReadOnlyList<RocketPlace> standing)
        {
            if (thrown.Count == 0)
            {
                throw new ArgumentException("A wave of the finale has a move at least.", nameof(thrown));
            }

            _start = start;
            _moves = thrown.Count;
            _leaveEvery = Math.Min(Tempo.MovesLeaveEvery, Tempo.MovesLeaveWithin / _moves);
            float firstGoesOff = start + (_moves - 1) * _leaveEvery + Tempo.MoveFlies + Tempo.RocketsRest;
            float goOffEvery = Math.Min(Tempo.ArmedEvery, Tempo.RocketsFireWithin / _moves);
            var rockets = new List<Stroke>();
            for (int rocket = 0; rocket < thrown.Count; rocket++)
            {
                rockets.Add(new Stroke(thrown[rocket], firstGoesOff + rocket * goOffEvery));
            }

            LetStrokesSetOff(rockets, standing.ToList());
        }

        /// <summary>True once every move of the wave has left the medallion.</summary>
        public bool IsThrown => _thrown == _moves;

        /// <summary>When the next move of the wave leaves the medallion and when it lands on its gem.</summary>
        /// <exception cref="InvalidOperationException">Every move of the wave has left.</exception>
        public (float Leaves, float Lands) Next()
        {
            if (IsThrown)
            {
                throw new InvalidOperationException($"All {_moves} moves of this wave of the finale have been thrown.");
            }

            float leaves = _start + _thrown * _leaveEvery;
            _thrown++;
            return (leaves, leaves + Tempo.MoveFlies);
        }

        /// <summary>True when the rocket of a cell goes off in this wave: every rocket thrown does, a standing one when a stroke reaches it.</summary>
        public bool IsSetOff(Position cell) => StrokeFrom(cell, _strokes) != null;

        /// <summary>When the rocket of a cell flares up; its stroke starts a moment into that (<see cref="Tempo.FireLead"/>).</summary>
        /// <exception cref="InvalidOperationException">No rocket goes off there in this wave.</exception>
        public float GoesOffAt(Position cell) => StrokeFrom(cell, _strokes)?.GoesOff
            ?? throw new InvalidOperationException($"No rocket goes off in {cell} in this wave of the finale.");

        /// <summary>
        /// The engine fired a special piece. If it is a rocket of this wave, its stroke counts from now on among
        /// those that have hit, and true is returned.
        /// </summary>
        public bool Fires(Position origin)
        {
            if (!(StrokeFrom(origin, _strokes.Except(_fired)) is Stroke stroke))
            {
                return false;
            }

            _fired.Add(stroke);
            return true;
        }

        /// <summary>
        /// When the hit is shown that the stroke the engine fired last made on a cell: the engine has counted so
        /// many hits of this wave's strokes on the cell, and as many strokes have reached it at that moment.
        /// </summary>
        /// <exception cref="InvalidOperationException">No stroke the engine has fired runs through the cell.</exception>
        public float HitIsShownAt(Position cell)
        {
            int hitsCounted = FiredThrough(cell);
            if (hitsCounted == 0)
            {
                throw new InvalidOperationException($"The engine hit {cell} with a stroke of the finale that does not run through it.");
            }

            return WhenStrokesReach(cell)[hitsCounted - 1];
        }

        /// <summary>
        /// When the next stroke of the wave gets to a cell that the engine has not fired by now: the strokes it
        /// has fired have shown their hits there, each as one stroke arrived, and this is the arrival after
        /// those. Nothing when no stroke is left to come through the cell. A hit that something else of the wave
        /// makes on the cell (a blast, a seeker, a ray) is shown no later than this.
        /// </summary>
        public float? NextStrokeReaches(Position cell)
        {
            float[] reached = WhenStrokesReach(cell);
            int shown = FiredThrough(cell);
            return shown < reached.Length ? reached[shown] : (float?)null;
        }

        /// <summary>
        /// When the last stroke of the wave that runs through a cell gets there; nothing for a cell no stroke runs
        /// through. A stroke runs to both ends of its line, also over cells that an earlier one has emptied.
        /// </summary>
        public float? LastStrokeReaches(Position cell)
        {
            float[] reached = WhenStrokesReach(cell);
            return reached.Length > 0 ? reached[reached.Length - 1] : (float?)null;
        }

        /// <summary>How many of the strokes the engine has fired run through a cell.</summary>
        private int FiredThrough(Position cell) => _fired.Count(stroke => stroke.RunsThrough(cell));

        /// <summary>The moments at which the strokes of the wave get to a cell, the earliest first; none for a cell no stroke runs through.</summary>
        private float[] WhenStrokesReach(Position cell) => _strokes
            .Where(stroke => stroke.RunsThrough(cell))
            .Select(stroke => stroke.Reaches(cell))
            .OrderBy(reached => reached)
            .ToArray();

        /// <summary>
        /// Works out when each rocket goes off: a thrown one at its own moment, or when the stroke of a rocket
        /// that went off before reaches it; a standing one when the first stroke reaches it. The earliest one is
        /// settled first, so each is settled once. A standing rocket that no stroke reaches does not go off, and
        /// has no stroke.
        /// </summary>
        /// <param name="waiting">The rockets that are known to go off, each with the latest moment it does.</param>
        /// <param name="standing">The rockets that only go off if a stroke reaches them.</param>
        private void LetStrokesSetOff(List<Stroke> waiting, List<RocketPlace> standing)
        {
            while (waiting.Count > 0)
            {
                Stroke next = waiting.OrderBy(stroke => stroke.GoesOff).First();
                waiting.Remove(next);
                _strokes.Add(next);
                foreach (Stroke reached in waiting.Where(other => next.RunsThrough(other.Origin)))
                {
                    reached.GoOffNoLaterThan(next.Reaches(reached.Origin));
                }

                foreach (RocketPlace reached in standing.Where(rocket => next.RunsThrough(rocket.Cell)).ToArray())
                {
                    standing.Remove(reached);
                    waiting.Add(new Stroke(reached, next.Reaches(reached.Cell)));
                }
            }
        }

        private static Stroke? StrokeFrom(Position origin, IEnumerable<Stroke> strokes) =>
            strokes.FirstOrDefault(stroke => stroke.Origin == origin);

        /// <summary>The stroke of one rocket of the wave: where it starts, which way it runs, and when the rocket goes off.</summary>
        private sealed class Stroke
        {
            private readonly bool _isAlongTheRow;

            public Stroke(RocketPlace rocket, float goesOff)
            {
                Origin = rocket.Cell;
                _isAlongTheRow = rocket.IsAlongTheRow;
                GoesOff = goesOff;
            }

            public Position Origin { get; }

            /// <summary>When the rocket flares up.</summary>
            public float GoesOff { get; private set; }

            public void GoOffNoLaterThan(float moment) => GoesOff = Math.Min(GoesOff, moment);

            public bool RunsThrough(Position cell) => _isAlongTheRow ? cell.Y == Origin.Y : cell.X == Origin.X;

            /// <summary>
            /// When the stroke gets to a cell of its line: it starts a moment into the rocket's flare and takes as
            /// long for a cell as <see cref="Strike"/> gives a rocket's stroke, which is what its hits are timed by
            /// once the engine fires it.
            /// </summary>
            public float Reaches(Position cell)
            {
                int cells = _isAlongTheRow ? Math.Abs(cell.X - Origin.X) : Math.Abs(cell.Y - Origin.Y);
                return GoesOff + Tempo.FireLead + cells * Tempo.RocketPerCell;
            }
        }
    }
}
