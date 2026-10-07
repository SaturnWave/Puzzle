#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What is hitting the board at one point of a turn, and when it reaches each cell. A match and a special piece
    /// set off by the player hit everything at once. An effect spreads: a rocket's stroke runs outwards along its
    /// row, a blast grows ring by ring, a colour bomb sends a ray to one cell after another, a seeker hits what is
    /// next to it and then what it flies to. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    internal sealed class Strike
    {
        private readonly EffectKind? _kind;
        private readonly int _radius;
        private readonly float _rayEvery;
        private readonly Dictionary<Position, float> _rayArrivals = new Dictionary<Position, float>();
        private readonly int _raysPlanned;
        private int _flights;
        private int _armed;
        private Position? _flightTarget;
        private float _flightLands;

        private Strike(EffectKind? kind, Position origin, int radius, float start, bool carriesASpecial, int cellsReached, float reachSeconds)
        {
            _kind = kind;
            _radius = radius;
            _raysPlanned = Math.Max(1, cellsReached);
            _rayEvery = Math.Min(Tempo.RayEvery, Tempo.RaysWithin / _raysPlanned);
            Origin = origin;
            Start = start;
            CarriesASpecial = carriesASpecial;
            Seconds = (SendsRays ? Tempo.RayLead + _raysPlanned * _rayEvery : reachSeconds) + Tempo.EffectLinger;
        }

        public Position Origin { get; }

        public float Start { get; }

        /// <summary>How long the effect is seen as a whole: until it has reached its last cell, and a little longer.</summary>
        public float Seconds { get; }

        /// <summary>How long a spreading effect takes to get one cell further; nothing for hits that do not spread.</summary>
        public float SecondsPerCell => _kind switch
        {
            EffectKind.ClearRow => Tempo.RocketPerCell,
            EffectKind.ClearColumn => Tempo.RocketPerCell,
            EffectKind.Cross => Tempo.RocketPerCell,
            EffectKind.Explode => Tempo.BlastPerRing,
            EffectKind.ClearBoard => Tempo.WavePerCell,
            _ => 0f,
        };

        /// <summary>True for a seeker that carries a rocket or a bomb: where it lands, that piece goes off as an effect of its own.</summary>
        public bool CarriesASpecial { get; }

        /// <summary>
        /// Until when the effect's own cell is taken: a colour bomb turns where it lay until its last ray has left,
        /// and nothing falls into its cell before that. Every other effect leaves its cell at once.
        /// </summary>
        public float OriginIsTakenUntil => SendsRays ? Start + Tempo.RayLead + _raysPlanned * _rayEvery : Start;

        /// <summary>True for a colour bomb that turns gems into special pieces.</summary>
        public bool MakesSpecials => _kind == EffectKind.ConvertColor;

        private bool SendsRays => _kind == EffectKind.ClearColor || _kind == EffectKind.ConvertColor;

        /// <summary>Hits that all come at one moment: a match, or the special piece the player set off.</summary>
        public static Strike AllAtOnce(float start) =>
            new Strike(kind: null, default, radius: 0, start, carriesASpecial: false, cellsReached: 0, reachSeconds: 0f);

        /// <param name="cellsReached">How many cells the effect reaches; only a colour bomb's rays are spaced by it.</param>
        public static Strike Of(GameEvent.EffectTriggered effect, float start, BoardShape board, bool carriesASpecial, int cellsReached)
        {
            Position origin = effect.Origin;
            int acrossTheRow = Math.Max(origin.X, board.Width - 1 - origin.X);
            int alongTheColumn = Math.Max(origin.Y, board.Height - 1 - origin.Y);
            float reachSeconds = effect.Kind switch
            {
                EffectKind.ClearRow => acrossTheRow * Tempo.RocketPerCell,
                EffectKind.ClearColumn => alongTheColumn * Tempo.RocketPerCell,
                EffectKind.Cross => Math.Max(acrossTheRow, alongTheColumn) * Tempo.RocketPerCell,
                EffectKind.Explode => effect.Radius * Tempo.BlastPerRing,
                EffectKind.ClearBoard => Math.Max(acrossTheRow, alongTheColumn) * Tempo.WavePerCell,
                EffectKind.Seeker => Tempo.SeekerRise,
                EffectKind.ClearColor => 0f,
                EffectKind.ConvertColor => 0f,
                _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "An effect without a timing."),
            };
            return new Strike(effect.Kind, origin, effect.Radius, start, carriesASpecial && effect.Kind == EffectKind.Seeker, cellsReached, reachSeconds);
        }

        /// <summary>When this strike reaches a cell.</summary>
        /// <param name="isANewRay">True when a colour bomb's ray was sent to the cell just now; later hits of the cell arrive with it.</param>
        public float TimeAt(Position cell, out bool isANewRay)
        {
            isANewRay = false;
            if (_flightTarget == cell)
            {
                return _flightLands;
            }

            if (SendsRays)
            {
                return RayArrivalAt(cell, out isANewRay);
            }

            return Start + CellsFromOrigin(cell) * SecondsPerCell;
        }

        /// <summary>The next flight of a seeker: when it leaves and when it lands. What it hits there is hit as it lands.</summary>
        /// <exception cref="InvalidOperationException">This strike is not a seeker's.</exception>
        public (float Departs, float Lands) Launch(Position from, Position target)
        {
            if (_kind != EffectKind.Seeker)
            {
                throw new InvalidOperationException($"The engine launched a seeker in the middle of {_kind?.ToString() ?? "a match"}.");
            }

            float departs = Start + Tempo.SeekerRise + _flights * Tempo.FlightStagger;
            double distance = Math.Sqrt(Math.Pow(target.X - from.X, 2) + Math.Pow(target.Y - from.Y, 2));
            _flights++;
            _flightTarget = target;
            _flightLands = departs + Tempo.FlightBase + (float)distance * Tempo.FlightPerCell;
            return (departs, _flightLands);
        }

        /// <summary>
        /// When the next special piece a colour bomb made goes off: after the last ray has arrived and the pieces
        /// have shown themselves, one after another.
        /// </summary>
        public float NextArmedTime()
        {
            float allMade = Start + Tempo.RayLead + _raysPlanned * _rayEvery + Tempo.AppearBeforeFalling;
            return allMade + _armed++ * Tempo.ArmedEvery;
        }

        private float RayArrivalAt(Position cell, out bool isANewRay)
        {
            isANewRay = !_rayArrivals.TryGetValue(cell, out float arrival);
            if (isANewRay)
            {
                arrival = Start + Tempo.RayLead + _rayArrivals.Count * _rayEvery;
                _rayArrivals[cell] = arrival;
            }

            return arrival;
        }

        /// <summary>How many cells the effect has to travel from its origin before it reaches a cell.</summary>
        private int CellsFromOrigin(Position cell)
        {
            int across = Math.Abs(cell.X - Origin.X);
            int down = Math.Abs(cell.Y - Origin.Y);
            switch (_kind)
            {
                case EffectKind.ClearRow:
                    return across;
                case EffectKind.ClearColumn:
                    return down;
                case EffectKind.Cross:
                    // The bands leave the origin as one block as wide as they are: a cell is reached along its own band.
                    return down <= _radius ? Math.Max(0, across - _radius) : Math.Max(0, down - _radius);
                case EffectKind.Explode:
                case EffectKind.ClearBoard:
                    return Math.Max(across, down);
                default:
                    return 0;
            }
        }
    }
}
