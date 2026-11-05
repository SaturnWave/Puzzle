using System.Collections.Generic;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// A band that runs around a loop of the board's outline: the frame's rails, its shadow, the shade on the tray.
    /// Across, the band is a row of stops, each a distance from the outline; along, it is the loop's spokes. Every
    /// spoke gets a corner at every stop, coloured by the caller, and the faces between them make the band.
    /// </summary>
    public static class OutlineBand
    {
        /// <summary>The colour of the band at one stop of one spoke; <paramref name="pagePoint"/> is where that is, in the board's page.</summary>
        public delegate Color32 Paint(Spoke spoke, int stop, Vector2 pagePoint);

        /// <summary>Adds the band around one loop to a sketch. The sketch is in scene units: the page with y turned upwards.</summary>
        /// <param name="distances">The stops, from one edge of the band to the other; negative is inside the outline.</param>
        public static void Add(MeshSketch sketch, IReadOnlyList<Spoke> loop, IReadOnlyList<float> distances, Paint paint)
        {
            var firstCornerOf = new int[loop.Count];
            for (int spoke = 0; spoke < loop.Count; spoke++)
            {
                for (int stop = 0; stop < distances.Count; stop++)
                {
                    Vector2 pagePoint = loop[spoke].At(distances[stop]);
                    int corner = sketch.AddCorner(BoardPage.ToScene(pagePoint), paint(loop[spoke], stop, pagePoint));
                    if (stop == 0)
                    {
                        firstCornerOf[spoke] = corner;
                    }
                }
            }

            for (int spoke = 0; spoke < loop.Count; spoke++)
            {
                int here = firstCornerOf[spoke];
                int next = firstCornerOf[(spoke + 1) % loop.Count];
                for (int stop = 0; stop + 1 < distances.Count; stop++)
                {
                    sketch.AddQuad(here + stop, here + stop + 1, next + stop + 1, next + stop);
                }
            }
        }
    }

    /// <summary>
    /// The board's page: points, the origin at the top left corner of the cells, y growing downwards as rows do. The
    /// outline and everything drawn along it are worked out there; the scene has y growing upwards.
    /// </summary>
    public static class BoardPage
    {
        public static Vector2 ToScene(Vector2 pagePoint) => new Vector2(pagePoint.x, -pagePoint.y);
    }
}
