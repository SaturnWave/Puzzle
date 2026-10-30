using System;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// A point of a picture. x runs to the right and y DOWN from the picture's top left corner, as the pixels of an
    /// image file are counted; a renderer whose y runs up turns it over once, where it maps the picture.
    /// </summary>
    public readonly struct Point2 : IEquatable<Point2>
    {
        public Point2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public bool Equals(Point2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) => obj is Point2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => FormattableString.Invariant($"({X:0.####}, {Y:0.####})");

        public static bool operator ==(Point2 left, Point2 right) => left.Equals(right);

        public static bool operator !=(Point2 left, Point2 right) => !left.Equals(right);
    }

    /// <summary>
    /// How large the picture is that is cut, in whatever unit the caller draws in (pixels of the image, points of a
    /// screen): the pieces come back in the same unit. Only the proportions shape the cut.
    /// </summary>
    public readonly struct PictureSize
    {
        /// <exception cref="ArgumentOutOfRangeException">A side is not a positive, finite length.</exception>
        public PictureSize(double width, double height)
        {
            Width = IsALength(width) ? width : throw new ArgumentOutOfRangeException(nameof(width), width, "A picture has a positive width.");
            Height = IsALength(height) ? height : throw new ArgumentOutOfRangeException(nameof(height), height, "A picture has a positive height.");
        }

        public double Width { get; }

        public double Height { get; }

        private static bool IsALength(double value) => value > 0 && !double.IsInfinity(value);
    }

    /// <summary>A rectangle of the picture with its sides along the picture's.</summary>
    public readonly struct PictureRect
    {
        public PictureRect(Point2 topLeft, Point2 bottomRight)
        {
            TopLeft = topLeft;
            BottomRight = bottomRight;
        }

        public Point2 TopLeft { get; }

        public Point2 BottomRight { get; }

        public double Width => BottomRight.X - TopLeft.X;

        public double Height => BottomRight.Y - TopLeft.Y;

        public Point2 Centre => new Point2((TopLeft.X + BottomRight.X) / 2, (TopLeft.Y + BottomRight.Y) / 2);
    }
}
