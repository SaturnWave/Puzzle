#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The red badge with a count of what waits for the player, as the design canvas sets it on the buttons and
    /// paintings that lead to a jigsaw (HomeTR, CollectionTR): a red disc in a white ring, 22 points across, that
    /// widens for a number of two figures.
    /// </summary>
    public static class CountBadge
    {
        /// <summary>The badge's picture is 32 points across for a disc of 22; a number wider than this many points widens the badge.</summary>
        private const float PictureSize = 32f;
        private const float NumberRoom = 14f;
        private const float NumberSize = 14f;

        /// <summary>Adds a badge under a parent; its place is the middle of its disc.</summary>
        /// <param name="order">The badge's place in the drawing order; its number takes the place above.</param>
        public static Transform Add(string name, Transform parent, ArtCatalogue art, int count, DrawOrder order)
        {
            string number = MuseumWords.Count(count);
            SpriteRenderer badge = SceneObjects.AddSprite(name, parent, art.Of(GalleryArt.RedBadge), art.SpriteMaterial, order);
            badge.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches for a longer number; its round ends keep their shape

            TextMeshPro line = Lettering.Write("Count", badge.transform, art, LetteringStyle.Label, NumberSize, Color.white, order + 1);
            line.transform.localPosition = new Vector3(0f, -Hud.FigureDrop * NumberSize, 0f);
            Lettering.Say(line, number);
            badge.size = new Vector2(PictureSize + Mathf.Max(0f, Lettering.WidthOf(line, number) - NumberRoom), PictureSize);
            return badge.transform;
        }
    }
}
