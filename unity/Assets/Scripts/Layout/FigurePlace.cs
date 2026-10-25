#nullable enable
using System;
using Puzzle.Client.Avatar;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where a standing picture of the painter stands on a page: the point under his feet, in the layout's points,
    /// and how many points a metre of him is drawn. His three standing pictures share one scale
    /// (<see cref="PainterPictures.PixelsPerMetre"/>), so one place holds any of them, each as tall as it is.
    /// No Unity dependency.
    /// </summary>
    public readonly struct FigurePlace
    {
        /// <exception cref="ArgumentOutOfRangeException">A figure drawn at no size.</exception>
        public FigurePlace(float feetX, float feetY, float pointsPerMetre)
        {
            if (!(pointsPerMetre > 0f) || float.IsInfinity(pointsPerMetre))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerMetre), pointsPerMetre, "A figure is drawn at some size.");
            }

            FeetX = feetX;
            FeetY = feetY;
            PointsPerMetre = pointsPerMetre;
        }

        public float FeetX { get; }

        public float FeetY { get; }

        public float PointsPerMetre { get; }

        /// <summary>The room a standing picture takes in this place: from his feet to its top, from its leftmost reach to its rightmost.</summary>
        public Box Bounds(StandingMeasure measure) =>
            new Box(FeetX - measure.Left * PointsPerMetre, FeetY, (measure.Left + measure.Right) * PointsPerMetre, measure.Top * PointsPerMetre);

        /// <summary>The room it takes below the height where raised arms begin (<see cref="PainterPictures.ArmsRaisedFrom"/>).</summary>
        public Box LowBounds(StandingMeasure measure) =>
            new Box(
                FeetX - measure.LowLeft * PointsPerMetre,
                FeetY,
                (measure.LowLeft + measure.LowRight) * PointsPerMetre,
                Math.Min(measure.Top, PainterPictures.ArmsRaisedFrom) * PointsPerMetre);
    }
}
