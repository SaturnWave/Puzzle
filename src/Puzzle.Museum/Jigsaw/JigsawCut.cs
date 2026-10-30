using System;
using System.Collections.Generic;
using System.Globalization;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// A picture cut into jigsaw pieces with classic knobs and sockets. The picture's outer edges stay straight.
    /// The same seed, size, orientation and picture always give the same pieces: the game cuts a painting anew
    /// whenever it shows it and never has to store a piece's shape.
    /// </summary>
    public sealed class JigsawCut
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>
        /// The fingerprint counts lengths in about a millionth of the unit the picture is measured in. The grain is
        /// a power of two on purpose: the cut's lengths are products of short decimals and often end exactly on a
        /// half of a decimal grain, where the last bit of a sum would decide which way it is rounded.
        /// </summary>
        private const double FingerprintGrain = 1 << 20;

        private JigsawCut(ulong seed, JigsawSize size, PaintingOrientation orientation, PictureSize picture, IReadOnlyList<JigsawPiece> pieces)
        {
            Seed = seed;
            Size = size;
            Orientation = orientation;
            Picture = picture;
            Pieces = pieces;
        }

        public ulong Seed { get; }

        public JigsawSize Size { get; }

        public PaintingOrientation Orientation { get; }

        public PictureSize Picture { get; }

        /// <summary>The pieces by number: <c>Pieces[n].Number == n</c>.</summary>
        public IReadOnlyList<JigsawPiece> Pieces { get; }

        /// <summary>Cuts a picture.</summary>
        /// <param name="seed">What the knobs are drawn from; a painting's own seed is <c>ArtworkId.CutSeed</c>.</param>
        /// <param name="orientation">
        /// Which way the layout lies. It is the catalogue's word and not read off the picture's sides, so that a
        /// nearly square painting is not cut differently because its file is a pixel taller than wide.
        /// </param>
        /// <param name="picture">The picture's size in the caller's unit; the pieces come back in that unit.</param>
        public static JigsawCut Of(ulong seed, JigsawSize size, PaintingOrientation orientation, PictureSize picture) =>
            new JigsawCut(seed, size, orientation, picture, Cutter.Cut(seed, size, orientation, picture));

        /// <summary>
        /// Sixteen hex digits that change when any point of any outline moves by about a millionth of the picture's unit.
        /// The repository pins the fingerprints of a few cuts, and the Unity project compares its own with them:
        /// a difference between the tools' runtime and the game's shows up there.
        /// </summary>
        public string Fingerprint()
        {
            ulong sum = FnvOffset;
            foreach (JigsawPiece piece in Pieces)
            {
                sum = Mixed(sum, piece.Outline.Start);
                foreach (PathStep step in piece.Outline.Steps)
                {
                    if (step is PathStep.Curve curve)
                    {
                        sum = Mixed(Mixed(sum, curve.Control1), curve.Control2);
                    }

                    sum = Mixed(sum, step.End);
                }
            }

            return sum.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static ulong Mixed(ulong sum, Point2 point) => Mixed(Mixed(sum, point.X), point.Y);

        private static ulong Mixed(ulong sum, double length)
        {
            long grains = (long)Math.Round(length * FingerprintGrain);
            return unchecked((sum ^ (ulong)grains) * FnvPrime);
        }
    }
}
