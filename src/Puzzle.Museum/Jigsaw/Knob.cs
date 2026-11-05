using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// Where a stretch of cut line lies: it maps "so far along the stretch, so far to the side of it" into the
    /// picture. The side counted positive is the second piece's: the piece below a line between two bands, the next
    /// piece along the band for a line inside one.
    /// </summary>
    internal readonly struct StretchFrame
    {
        private readonly Bands _bands;
        private readonly double _alongAtStart;
        private readonly double _acrossAtStart;
        private readonly bool _runsAlongTheBands;

        private StretchFrame(Bands bands, double alongAtStart, double acrossAtStart, bool runsAlongTheBands)
        {
            _bands = bands;
            _alongAtStart = alongAtStart;
            _acrossAtStart = acrossAtStart;
            _runsAlongTheBands = runsAlongTheBands;
        }

        /// <summary>A stretch of the line between two bands, starting so far along them.</summary>
        public static StretchFrame BetweenBands(Bands bands, double alongAtStart, double across) =>
            new StretchFrame(bands, alongAtStart, across, runsAlongTheBands: true);

        /// <summary>The line between two pieces of one band, starting at the band's near edge.</summary>
        public static StretchFrame InsideABand(Bands bands, double along, double acrossAtStart) =>
            new StretchFrame(bands, along, acrossAtStart, runsAlongTheBands: false);

        public Point2 At(double way, double toTheSecondPiece) => _runsAlongTheBands
            ? _bands.InPicture(_alongAtStart + way, _acrossAtStart + toTheSecondPiece)
            : _bands.InPicture(_alongAtStart + toTheSecondPiece, _acrossAtStart + way);
    }

    /// <summary>
    /// The classic jigsaw knob: the line leaves its straight course in a soft shoulder, narrows to a neck, swells
    /// into a round head wider than the neck (which is what locks two pieces), and comes back the same way.
    /// </summary>
    internal sealed class Knob
    {
        // The outline in the knob's own measure, where 1 is the knob's size: pairs of "along the line from the
        // knob's middle" and "out from the line". Only the left half is written; the right half mirrors it.
        private const double ShoulderStart = 0.24;
        private const double ShoulderPull = 0.10;       // how long the line keeps its course before it turns up
        private const double NeckApproachAlong = 0.03;  // the shoulder arrives at the neck leaning outwards,
        private const double NeckApproachOut = 0.01;    // so the neck is narrowest just above the line
        private const double NeckAlong = 0.07;
        private const double NeckOut = 0.07;
        private const double CheekAlong = 0.14;         // the head leaves the neck in the direction the shoulder arrived
        private const double CheekOut = 0.18;
        private const double CrownAlong = 0.11;
        private const double Height = 0.27;

        private readonly double _middle;
        private readonly double _size;
        private readonly double _towardsSecond;
        private readonly double _lean;

        /// <param name="middle">How far along the stretch the knob's middle lies.</param>
        /// <param name="size">The knob's measure: its head is about a fifth of it wide and it stands a quarter of it high.</param>
        /// <param name="reachesIntoTheSecondPiece">Which of the two pieces gets the socket.</param>
        /// <param name="lean">How far the head's crown is pushed along the line, in the knob's own measure: a slight slouch.</param>
        public Knob(double middle, double size, bool reachesIntoTheSecondPiece, double lean)
        {
            _middle = middle;
            _size = size;
            _towardsSecond = reachesIntoTheSecondPiece ? 1 : -1;
            _lean = lean;
        }

        /// <summary>The knob from the point where the line leaves its course to the point where it is back on it.</summary>
        public IEnumerable<PathStep> StepsIn(StretchFrame frame)
        {
            Point2 P(double along, double outwards) => frame.At(_middle + along * _size, outwards * _size * _towardsSecond);

            yield return new PathStep.Line(P(-ShoulderStart, 0));
            yield return new PathStep.Curve(P(-ShoulderPull, 0), P(-NeckApproachAlong, NeckApproachOut), P(-NeckAlong, NeckOut));
            yield return new PathStep.Curve(P(-CheekAlong, CheekOut), P(_lean - CrownAlong, Height), P(_lean, Height));
            yield return new PathStep.Curve(P(_lean + CrownAlong, Height), P(CheekAlong, CheekOut), P(NeckAlong, NeckOut));
            yield return new PathStep.Curve(P(NeckApproachAlong, NeckApproachOut), P(ShoulderPull, 0), P(ShoulderStart, 0));
        }
    }
}
