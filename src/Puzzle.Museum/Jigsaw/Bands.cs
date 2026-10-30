using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// The layout of a cut before any knob is drawn: the picture as bands of equal thickness, each divided into its
    /// own number of equal pieces. In a landscape picture the bands are rows, from the top; a portrait picture is
    /// the same layout turned by a quarter, so its bands are columns, from the left. Everything about a cut is
    /// worked out along and across the bands and only then placed in the picture, which is why one piece of code
    /// cuts both ways.
    /// </summary>
    internal sealed class Bands
    {
        private readonly PaintingOrientation _orientation;

        /// <summary>The picture's measure along the bands, and across all of them.</summary>
        private readonly double _length;
        private readonly double _breadth;

        public Bands(JigsawSize size, PaintingOrientation orientation, PictureSize picture)
        {
            _orientation = orientation;
            PieceCounts = size.RowsOfALandscapePicture;
            bool bandsAreRows = orientation == PaintingOrientation.Landscape;
            _length = bandsAreRows ? picture.Width : picture.Height;
            _breadth = bandsAreRows ? picture.Height : picture.Width;
            Units = CommonMultipleOf(PieceCounts);
        }

        /// <summary>How many pieces each band has, band by band.</summary>
        public IReadOnlyList<int> PieceCounts { get; }

        public int Count => PieceCounts.Count;

        /// <summary>
        /// Into how many whole units a band's length is counted, chosen so that every piece of every band begins and
        /// ends on a unit (twelve for rows of three and four). Counting in units keeps a corner that two bands share
        /// the very same number in both.
        /// </summary>
        public int Units { get; }

        public double Thickness => _breadth / Count;

        public double UnitLength => _length / Units;

        public int UnitsPerPieceOf(int band) => Units / PieceCounts[band];

        /// <summary>The number of a band's first piece: pieces are numbered band by band.</summary>
        public int FirstPieceOf(int band)
        {
            int before = 0;
            for (int earlier = 0; earlier < band; earlier++)
            {
                before += PieceCounts[earlier];
            }

            return before;
        }

        /// <summary>
        /// How far along the bands a whole unit lies, and how far across them a band begins. Every line of the layout
        /// is placed with these two, so points that lie on one line carry the very same number, and the picture's
        /// far edges are hit exactly, not to within a rounding.
        /// </summary>
        public double Along(int units) => units == Units ? _length : _length * units / Units;

        public double Across(int bands) => bands == Count ? _breadth : _breadth * bands / Count;

        /// <summary>A crossing of the layout's lines, in the picture.</summary>
        public Point2 Corner(int units, int bands) => InPicture(Along(units), Across(bands));

        /// <summary>A point given by its way along the bands and across them, in the picture.</summary>
        public Point2 InPicture(double along, double across) =>
            _orientation == PaintingOrientation.Landscape ? new Point2(along, across) : new Point2(across, along);

        /// <summary>
        /// True when walking a cell's corners in the order "start of the band's near edge, along it, across, back"
        /// goes clockwise in the picture as it is seen. Turning the layout for a portrait picture mirrors it, so
        /// there the same walk goes the other way round.
        /// </summary>
        public bool WalksClockwise => _orientation == PaintingOrientation.Landscape;

        private static int CommonMultipleOf(IReadOnlyList<int> counts)
        {
            int multiple = 1;
            foreach (int count in counts)
            {
                multiple = multiple / GreatestCommonDivisor(multiple, count) * count;
            }

            return multiple;
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return a;
        }
    }
}
