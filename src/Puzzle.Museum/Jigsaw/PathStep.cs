using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// One step of a path, from where the step before it ended to <see cref="End"/>: a straight line or a cubic
    /// Bézier curve. A closed set; the private constructor keeps every kind inside this class.
    /// </summary>
    public abstract class PathStep
    {
        private PathStep(Point2 end)
        {
            End = end;
        }

        public Point2 End { get; }

        /// <summary>The same stretch walked the other way: it starts at this step's end and ends where this step started.</summary>
        internal abstract PathStep ReversedTo(Point2 start);

        /// <summary>Adds the step's points to a polygon, without the point it starts from.</summary>
        internal abstract void FlattenInto(List<Point2> points, Point2 start, int pointsPerCurve);

        public sealed class Line : PathStep
        {
            public Line(Point2 end)
                : base(end)
            {
            }

            internal override PathStep ReversedTo(Point2 start) => new Line(start);

            internal override void FlattenInto(List<Point2> points, Point2 start, int pointsPerCurve) => points.Add(End);
        }

        /// <summary>A cubic Bézier curve: it leaves its start towards <see cref="Control1"/> and arrives at its end from <see cref="Control2"/>.</summary>
        public sealed class Curve : PathStep
        {
            public Curve(Point2 control1, Point2 control2, Point2 end)
                : base(end)
            {
                Control1 = control1;
                Control2 = control2;
            }

            public Point2 Control1 { get; }

            public Point2 Control2 { get; }

            /// <summary>The curve's point at a share of its way, 0 at the start and 1 at the end.</summary>
            public Point2 At(Point2 start, double share)
            {
                double rest = 1 - share;
                double a = rest * rest * rest;
                double b = 3 * rest * rest * share;
                double c = 3 * rest * share * share;
                double d = share * share * share;
                return new Point2(
                    a * start.X + b * Control1.X + c * Control2.X + d * End.X,
                    a * start.Y + b * Control1.Y + c * Control2.Y + d * End.Y);
            }

            internal override PathStep ReversedTo(Point2 start) => new Curve(Control2, Control1, start);

            internal override void FlattenInto(List<Point2> points, Point2 start, int pointsPerCurve)
            {
                for (int step = 1; step < pointsPerCurve; step++)
                {
                    points.Add(At(start, (double)step / pointsPerCurve));
                }

                points.Add(End);
            }
        }
    }

    /// <summary>A path from one point to another: a stretch of a cut line, or one side of a piece.</summary>
    public sealed class OpenPath
    {
        public OpenPath(Point2 start, IReadOnlyList<PathStep> steps)
        {
            if (steps.Count == 0)
            {
                throw new ArgumentException("A path has at least one step.", nameof(steps));
            }

            Start = start;
            Steps = steps;
        }

        public Point2 Start { get; }

        public IReadOnlyList<PathStep> Steps { get; }

        public Point2 End => Steps[Steps.Count - 1].End;

        /// <summary>
        /// The same line walked from its end to its start. Two neighbouring pieces share one stretch of the cut, each
        /// walking it its own way round, which is what makes a knob fit its socket exactly.
        /// </summary>
        public OpenPath Reversed()
        {
            var steps = new List<PathStep>(Steps.Count);
            for (int index = Steps.Count - 1; index >= 0; index--)
            {
                Point2 startOfStep = index == 0 ? Start : Steps[index - 1].End;
                steps.Add(Steps[index].ReversedTo(startOfStep));
            }

            return new OpenPath(End, steps);
        }
    }
}
