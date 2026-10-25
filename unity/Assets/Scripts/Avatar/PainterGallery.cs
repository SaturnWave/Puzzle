#nullable enable
using Puzzle.Client.Canvases;
using UnityEngine;

namespace Puzzle.Client.Avatar
{
    /// <summary>
    /// The painter's pictures for the screens that show him (§22): each is loaded when a screen first shows it, held
    /// while something shows it (<see cref="Hold"/>), and unloaded as soon as nothing does, by the shelf's own rules
    /// (<see cref="PictureShelf{TKey, TPicture}"/>, A42). None is kept once let go of: a standing picture is the
    /// largest of them, a third of a megabyte or so on a phone, and loads again in a few milliseconds when the hall
    /// comes back, so a screen that shows him holds him and no screen that does not.
    /// </summary>
    public sealed class PainterGallery
    {
        private readonly PictureShelf<PainterPicture, Sprite> _pictures = new PictureShelf<PainterPicture, Sprite>(
            keptUnheld: 0,
            picture => Resources.Load<Sprite>(PainterPictures.ResourceOf(picture)),
            sprite => Resources.UnloadAsset(sprite.texture));

        /// <summary>
        /// Holds a picture of the painter, loading it if no screen shows it yet: whoever shows it disposes of the hold
        /// when it takes the picture down. A picture the game does not carry gives a hold of nothing, and says so.
        /// </summary>
        public PictureShelf<PainterPicture, Sprite>.PictureHold Hold(PainterPicture picture)
        {
            PictureShelf<PainterPicture, Sprite>.PictureHold hold = _pictures.Hold(picture);
            if (hold.Picture == null)
            {
                Debug.LogError($"The game carries no picture \"{PainterPictures.ResourceOf(picture)}\" of the painter; he is not shown there.");
            }

            return hold;
        }
    }
}
