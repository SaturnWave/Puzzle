using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Levels;

namespace Puzzle.Museum
{
    /// <summary>
    /// How finely a painting is cut. The game has four sizes, by how far into the levels a painting lies
    /// (docs/GAME_DESIGN.md section 18): the further, the more pieces, and the more of them every won level gives,
    /// so that ten levels always complete one painting.
    /// </summary>
    public sealed class JigsawSize
    {
        /// <summary>
        /// Ten pieces in rows of three, four and three. A plain grid of ten can only be five by two, which gives long
        /// thin pieces on the usual picture shapes; in 3-4-3 they stay near square (the owner's choice in a prototype).
        /// </summary>
        public static readonly JigsawSize Ten = new JigsawSize("3-4-3", lastLevel: 100, piecesPerLevel: 1, 3, 4, 3);

        public static readonly JigsawSize Twenty = new JigsawSize("5x4", lastLevel: 300, piecesPerLevel: 2, 5, 5, 5, 5);

        public static readonly JigsawSize Thirty = new JigsawSize("6x5", lastLevel: 600, piecesPerLevel: 3, 6, 6, 6, 6, 6);

        public static readonly JigsawSize Forty = new JigsawSize("8x5", lastLevel: MuseumPlan.LevelCount, piecesPerLevel: 4, 8, 8, 8, 8, 8);

        /// <summary>The four sizes, in the order the levels reach them.</summary>
        public static readonly IReadOnlyList<JigsawSize> All = new[] { Ten, Twenty, Thirty, Forty };

        private JigsawSize(string name, int lastLevel, int piecesPerLevel, params int[] rowsOfALandscapePicture)
        {
            Name = name;
            LastLevel = new LevelId(lastLevel);
            PiecesPerLevel = piecesPerLevel;
            RowsOfALandscapePicture = rowsOfALandscapePicture;
            PieceCount = rowsOfALandscapePicture.Sum();
        }

        /// <summary>The layout as the design documents write it for a landscape picture: "3-4-3", "5x4" (columns by rows).</summary>
        public string Name { get; }

        /// <summary>The last level whose painting is cut to this size.</summary>
        public LevelId LastLevel { get; }

        public int PieceCount { get; }

        /// <summary>How many pieces of its painting every won level gives.</summary>
        public int PiecesPerLevel { get; }

        /// <summary>
        /// How many pieces each row of a landscape picture has, from the top row down. A portrait picture is cut
        /// with the same numbers turned by a quarter: they are then its columns, from the left.
        /// </summary>
        public IReadOnlyList<int> RowsOfALandscapePicture { get; }

        /// <summary>The size of the painting a level belongs to.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The level lies beyond the museum's last painting.</exception>
        public static JigsawSize OfLevel(LevelId level)
        {
            foreach (JigsawSize size in All)
            {
                if (level.Value <= size.LastLevel.Value)
                {
                    return size;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(level), level.Value, $"The museum's paintings cover the levels 1 to {MuseumPlan.LevelCount}.");
        }

        public override string ToString() => $"{PieceCount} pieces ({Name})";
    }
}
