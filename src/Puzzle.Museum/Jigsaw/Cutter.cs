using System;
using System.Collections.Generic;
using Puzzle.Core.Randomness;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// Cuts one picture. Every stretch of line between two pieces is drawn once and handed to both of them, each
    /// walking it its own way round; so a knob always meets a socket of exactly its shape and the pieces cover the
    /// picture without gap or overlap.
    /// </summary>
    /// <remarks>
    /// Everything left to chance (which piece gets the knob, where on the stretch it sits, how large it is, how it
    /// leans) is drawn as whole numbers from the engine's own generator and only then turned into lengths with
    /// plain arithmetic, no sines or square roots: so the same seed cuts the same pieces on every device.
    /// </remarks>
    internal sealed class Cutter
    {
        /// <summary>A knob's measure as a share of the shorter of the lengths it has to fit: the stretch it sits on, and the pieces' depth behind it.</summary>
        private const double KnobShare = 0.85;

        /// <summary>A chance value is one of the whole steps from -8 to 8, read as -1 to 1.</summary>
        private const int ChanceSteps = 8;

        /// <summary>How far a knob's middle may lie from the middle of its stretch, as a share of the stretch.</summary>
        private const double LargestShift = 0.06;

        /// <summary>How much larger or smaller than the rule a knob may be.</summary>
        private const double LargestSizeChange = 0.08;

        /// <summary>How far a knob's crown may slouch to one side, in the knob's own measure.</summary>
        private const double LargestLean = 0.03;

        private readonly Bands _bands;
        private readonly Rng _chance;
        private readonly PieceSides[] _sides;

        private Cutter(Bands bands, ulong seed)
        {
            _bands = bands;
            _chance = new Rng(seed);
            _sides = new PieceSides[bands.FirstPieceOf(bands.Count)];
            for (int piece = 0; piece < _sides.Length; piece++)
            {
                _sides[piece] = new PieceSides();
            }
        }

        public static IReadOnlyList<JigsawPiece> Cut(ulong seed, JigsawSize size, PaintingOrientation orientation, PictureSize picture)
        {
            var cutter = new Cutter(new Bands(size, orientation, picture), seed);
            for (int band = 0; band < cutter._bands.Count; band++)
            {
                cutter.CutInside(band);
                if (band + 1 < cutter._bands.Count)
                {
                    cutter.CutBetween(band, band + 1);
                }
            }

            return cutter.Pieces();
        }

        /// <summary>The lines that part a band's pieces from each other, each from the band's near edge to its far edge.</summary>
        private void CutInside(int band)
        {
            int unitsPerPiece = _bands.UnitsPerPieceOf(band);
            int firstPiece = _bands.FirstPieceOf(band);
            double pieceLength = unitsPerPiece * _bands.UnitLength;
            for (int place = 0; place + 1 < _bands.PieceCounts[band]; place++)
            {
                int units = (place + 1) * unitsPerPiece;
                Point2 start = _bands.Corner(units, band);
                Point2 end = _bands.Corner(units, band + 1);
                StretchFrame frame = StretchFrame.InsideABand(_bands, _bands.Along(units), _bands.Across(band));
                Stretch line = WithAKnob(start, end, frame, length: _bands.Thickness, depthBehind: pieceLength);

                int near = firstPiece + place;
                int far = near + 1;
                _sides[near].FarEnd.Add(line.EdgeOfFirst(neighbour: far));
                _sides[far].NearEnd.Add(line.EdgeOfSecond(neighbour: near));
            }
        }

        /// <summary>
        /// The line between two bands. Where the bands are divided alike, every piece meets one piece across it;
        /// where they are not (rows of three and four), the line breaks into stretches at every piece boundary of
        /// either band, and each stretch parts one piece above from one below.
        /// </summary>
        private void CutBetween(int upper, int lower)
        {
            int upperUnits = _bands.UnitsPerPieceOf(upper);
            int lowerUnits = _bands.UnitsPerPieceOf(lower);
            int from = 0;
            while (from < _bands.Units)
            {
                int to = Math.Min(NextBoundaryAfter(from, upperUnits), NextBoundaryAfter(from, lowerUnits));
                Point2 start = _bands.Corner(from, lower);
                Point2 end = _bands.Corner(to, lower);
                StretchFrame frame = StretchFrame.BetweenBands(_bands, _bands.Along(from), _bands.Across(lower));
                double length = (to - from) * _bands.UnitLength;
                Stretch line = CarriesAKnob(to - from, upperUnits, lowerUnits)
                    ? WithAKnob(start, end, frame, length, depthBehind: _bands.Thickness)
                    : Stretch.Straight(start, end);

                int above = _bands.FirstPieceOf(upper) + from / upperUnits;
                int below = _bands.FirstPieceOf(lower) + from / lowerUnits;
                _sides[above].FarEdge.Add(line.EdgeOfFirst(neighbour: below));
                _sides[below].NearEdge.Add(line.EdgeOfSecond(neighbour: above));
                from = to;
            }
        }

        private static int NextBoundaryAfter(int units, int unitsPerPiece) => (units / unitsPerPiece + 1) * unitsPerPiece;

        /// <summary>
        /// A stretch shorter than half the shorter of the two pieces' sides stays straight: the knob that fits it
        /// would be a pinhead. In rows of three and four that leaves the two shortest stretches straight, as on the
        /// design canvas.
        /// </summary>
        private static bool CarriesAKnob(int stretchUnits, int upperUnitsPerPiece, int lowerUnitsPerPiece) =>
            2 * stretchUnits >= Math.Min(upperUnitsPerPiece, lowerUnitsPerPiece);

        private Stretch WithAKnob(Point2 start, Point2 end, StretchFrame frame, double length, double depthBehind)
        {
            bool reachesIntoTheSecondPiece = _chance.NextInt(2) == 0;
            double middle = length / 2 + Chance() * LargestShift * length;
            double size = KnobShare * Math.Min(length, depthBehind) * (1 + Chance() * LargestSizeChange);
            var knob = new Knob(middle, size, reachesIntoTheSecondPiece, lean: Chance() * LargestLean);

            var steps = new List<PathStep>(knob.StepsIn(frame)) { new PathStep.Line(end) };
            return new Stretch(new OpenPath(start, steps), reachesIntoTheSecondPiece ? Joint.Tab : Joint.Blank);
        }

        /// <summary>A value from -1 to 1, in sixteen even steps.</summary>
        private double Chance() => (double)(_chance.NextInt(2 * ChanceSteps + 1) - ChanceSteps) / ChanceSteps;

        private List<JigsawPiece> Pieces()
        {
            var pieces = new List<JigsawPiece>(_sides.Length);
            for (int band = 0; band < _bands.Count; band++)
            {
                int unitsPerPiece = _bands.UnitsPerPieceOf(band);
                for (int place = 0; place < _bands.PieceCounts[band]; place++)
                {
                    int number = _bands.FirstPieceOf(band) + place;
                    Point2 nearStart = _bands.Corner(place * unitsPerPiece, band);
                    Point2 nearEnd = _bands.Corner((place + 1) * unitsPerPiece, band);
                    Point2 farEnd = _bands.Corner((place + 1) * unitsPerPiece, band + 1);
                    Point2 farStart = _bands.Corner(place * unitsPerPiece, band + 1);
                    List<PieceEdge> edges = _sides[number].WalkedRound(nearStart, nearEnd, farEnd, farStart, _bands.WalksClockwise);
                    pieces.Add(new JigsawPiece(number, new PictureRect(nearStart, farEnd), edges));
                }
            }

            return pieces;
        }

        /// <summary>One stretch of cut line between a first piece (above, or earlier in the band) and a second, drawn from the first piece's point of view.</summary>
        private sealed class Stretch
        {
            private readonly OpenPath _path;
            private readonly Joint _jointOfFirst;

            public Stretch(OpenPath path, Joint jointOfFirst)
            {
                _path = path;
                _jointOfFirst = jointOfFirst;
            }

            public static Stretch Straight(Point2 start, Point2 end) =>
                new Stretch(new OpenPath(start, new PathStep[] { new PathStep.Line(end) }), Joint.Flat);

            public PieceEdge EdgeOfFirst(int neighbour) => new PieceEdge(_jointOfFirst, neighbour, _path);

            public PieceEdge EdgeOfSecond(int neighbour) => new PieceEdge(Opposite(_jointOfFirst), neighbour, _path);

            private static Joint Opposite(Joint joint)
            {
                switch (joint)
                {
                    case Joint.Tab:
                        return Joint.Blank;
                    case Joint.Blank:
                        return Joint.Tab;
                    default:
                        return joint;
                }
            }
        }

        /// <summary>
        /// The cut lines round one piece as they were drawn: each in the direction the cutter drew it (along the
        /// bands, or from a band's near edge to its far edge), and the stretches of an edge in that order too.
        /// </summary>
        private sealed class PieceSides
        {
            /// <summary>The band's edge towards the first band, and the one towards the last.</summary>
            public List<PieceEdge> NearEdge { get; } = new List<PieceEdge>();

            public List<PieceEdge> FarEdge { get; } = new List<PieceEdge>();

            /// <summary>The line to the piece before this one in its band, and to the one after.</summary>
            public List<PieceEdge> NearEnd { get; } = new List<PieceEdge>();

            public List<PieceEdge> FarEnd { get; } = new List<PieceEdge>();

            /// <summary>
            /// The piece's edges in clockwise order from its cell's top left corner. A side no line was cut along is
            /// the picture's border. The walk "near edge, far end, far edge backwards, near end backwards" is
            /// clockwise when the bands are rows; turned for a portrait picture it is the same walk taken backwards.
            /// </summary>
            public List<PieceEdge> WalkedRound(Point2 nearStart, Point2 nearEnd, Point2 farEnd, Point2 farStart, bool walksClockwise)
            {
                var edges = new List<PieceEdge>();
                edges.AddRange(OrBorder(NearEdge, nearStart, nearEnd));
                edges.AddRange(OrBorder(FarEnd, nearEnd, farEnd));
                edges.AddRange(Backwards(OrBorder(FarEdge, farStart, farEnd)));
                edges.AddRange(Backwards(OrBorder(NearEnd, nearStart, farStart)));
                return walksClockwise ? edges : Backwards(edges);
            }

            private static List<PieceEdge> OrBorder(List<PieceEdge> cut, Point2 start, Point2 end) => cut.Count > 0
                ? cut
                : new List<PieceEdge> { new PieceEdge(Joint.Border, neighbour: null, new OpenPath(start, new PathStep[] { new PathStep.Line(end) })) };

            private static List<PieceEdge> Backwards(List<PieceEdge> edges)
            {
                var backwards = new List<PieceEdge>(edges.Count);
                for (int index = edges.Count - 1; index >= 0; index--)
                {
                    PieceEdge edge = edges[index];
                    backwards.Add(new PieceEdge(edge.Joint, edge.Neighbour, edge.Path.Reversed()));
                }

                return backwards;
            }
        }
    }
}
