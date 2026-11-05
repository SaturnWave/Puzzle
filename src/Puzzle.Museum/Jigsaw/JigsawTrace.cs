using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// The cutter's fingerprint: every size cut both ways up with two seeds, one line each. The repository's tests
    /// pin the lines on the tools' runtime (tests/fixtures/jigsaw-cuts.txt) and the Unity project compares what it
    /// cuts inside the Editor with the same file, as the engine's trace does for the engine: a device that cut a
    /// painting differently would show a player pieces that do not fit the ones they placed elsewhere.
    /// </summary>
    public static class JigsawTrace
    {
        /// <summary>The proportions of the first room's widest painting and of its upright one.</summary>
        private static readonly (PaintingOrientation Orientation, PictureSize Picture)[] Pictures =
        {
            (PaintingOrientation.Landscape, new PictureSize(1920, 1138)),
            (PaintingOrientation.Portrait, new PictureSize(1280, 1589)),
        };

        private static readonly ulong[] Seeds = { 1, 0x9E3779B97F4A7C15UL };

        public static string[] Lines()
        {
            var lines = new List<string>();
            foreach (JigsawSize size in JigsawSize.All)
            {
                foreach ((PaintingOrientation orientation, PictureSize picture) in Pictures)
                {
                    foreach (ulong seed in Seeds)
                    {
                        string fingerprint = JigsawCut.Of(seed, size, orientation, picture).Fingerprint();
                        lines.Add(FormattableString.Invariant($"{size.Name} {orientation} {picture.Width}x{picture.Height} seed {seed}: {fingerprint}"));
                    }
                }
            }

            return lines.ToArray();
        }
    }
}
