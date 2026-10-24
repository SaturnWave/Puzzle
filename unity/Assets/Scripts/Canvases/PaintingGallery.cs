#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Engine;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// A painting as a screen draws it: the work chosen for its place in the museum, if one is, with its picture,
    /// and the jigsaw it is cut into. A place no work is chosen for yet is a blank canvas of ordinary proportions
    /// with a cut of its own, so the room it hangs in can be shown all the same.
    /// </summary>
    /// <remarks>
    /// A look from <see cref="PaintingGallery.LookOf"/> holds its picture in memory: whoever asked for it owns it and
    /// disposes of it when it takes down what shows the picture; whatever it hands the look to (a canvas, a frame, a
    /// loose piece) only borrows it. Once the look is disposed of, its picture may be unloaded.
    /// </remarks>
    public sealed class PaintingLook : IDisposable
    {
        /// <summary>The proportions of a canvas whose painting is not chosen yet, or whose picture is missing: a common landscape format.</summary>
        private const float BlankCanvasAspect = 4f / 3f;

        /// <summary>Spreads the numbers of blank canvases over all seeds, so no two of them are cut alike.</summary>
        private const ulong BlankSeedSpread = 0x9E3779B97F4A7C15UL;

        /// <summary>
        /// The cuts that are kept: a screen draws a painting at one size (the canvas, its loose pieces and their
        /// glow all ask for the same cut, many times while pieces move), and a second screen at another. When a
        /// cut at one size more is asked for, the one asked for longest ago is let go.
        /// </summary>
        private const int CutsKept = 3;
        private readonly List<JigsawCut> _cutsKept = new List<JigsawCut>(CutsKept);

        private readonly float? _recordedAspect;

        /// <summary>What keeps the picture in memory while the look is shown; nothing for a picture nobody unloads.</summary>
        private readonly IDisposable? _hold;

        /// <param name="recordedAspect">
        /// The picture's true width over height when the game's size list knows it; nothing for a picture that comes
        /// from elsewhere, whose texture then has the picture's own proportions.
        /// </param>
        /// <param name="hold">What keeps the picture in memory until the look is disposed of; nothing for a picture nobody unloads.</param>
        public PaintingLook(PaintingNumber number, Artwork? artwork, Texture2D? picture, float? recordedAspect = null, IDisposable? hold = null)
        {
            Number = number;
            Artwork = artwork;
            Picture = picture;
            _recordedAspect = recordedAspect;
            _hold = hold;
        }

        public PaintingNumber Number { get; }

        /// <summary>The work that hangs at this place; nothing while none is chosen.</summary>
        public Artwork? Artwork { get; }

        /// <summary>The work's picture; nothing for a blank canvas, and for a work whose picture the game does not carry.</summary>
        public Texture2D? Picture { get; }

        /// <summary>Which way the canvas lies; a canvas whose painting is not chosen yet lies like a landscape.</summary>
        public PaintingOrientation Orientation => Artwork?.Orientation ?? PaintingOrientation.Landscape;

        /// <summary>
        /// The canvas' width over its height. The game's own pictures are stored stretched to powers of two, so their
        /// proportions come from the recorded list; a texture is asked only where no size is recorded.
        /// </summary>
        public float Aspect => Picture == null ? BlankCanvasAspect : _recordedAspect ?? (float)Picture.width / Picture.height;

        /// <summary>
        /// The painting's jigsaw at a size: the same pieces at every size, as a painting is cut once and for all.
        /// A cut is worked out once for a size and kept (the last few sizes asked for), so whoever asks again gets
        /// the very same pieces back.
        /// </summary>
        /// <param name="size">How large the painting is drawn; the pieces come back in the same unit.</param>
        public JigsawCut CutAt(Vector2 size)
        {
            for (int kept = 0; kept < _cutsKept.Count; kept++)
            {
                JigsawCut cut = _cutsKept[kept];
                if (cut.Picture.Width == size.x && cut.Picture.Height == size.y)
                {
                    // Asked for again: it moves to the end, where the cuts asked for last stand.
                    _cutsKept.RemoveAt(kept);
                    _cutsKept.Add(cut);
                    return cut;
                }
            }

            if (_cutsKept.Count == CutsKept)
            {
                _cutsKept.RemoveAt(0);
            }

            ulong seed = Artwork?.Id.CutSeed ?? unchecked((ulong)Number.Value * BlankSeedSpread);
            JigsawCut made = JigsawCut.Of(seed, Number.Size, Orientation, new PictureSize(size.x, size.y));
            _cutsKept.Add(made);
            return made;
        }

        /// <summary>Lets go of the picture: nothing shows it through this look any more. A second call does nothing.</summary>
        public void Dispose() => _hold?.Dispose();
    }

    /// <summary>
    /// The museum's paintings for the screens that draw them: the catalogue, and the pictures it names, each loaded
    /// when a screen first asks for it and held while a look shows it (<see cref="LookOf"/>). Of the pictures nothing
    /// shows any more the gallery keeps the last few loaded (<see cref="PicturesKeptUnheld"/>), so a room the player
    /// turns back to hangs at once, and unloads the others; whoever closes the screens unloads the kept ones too
    /// (<see cref="LetGoOfUnusedPictures"/>). However long a player browses, only the pictures on the screen and the
    /// few kept sit in memory, never the museum's hundred (A42).
    /// </summary>
    public sealed class PaintingGallery
    {
        /// <summary>
        /// How many pictures that nothing shows stay loaded: the two rooms the player left last, so turning back to
        /// them, or swiping to and fro between three rooms, loads nothing. About 2.5 MB each on a phone (ASTC 6x6,
        /// 2048 pixels, with mipmaps).
        /// </summary>
        public const int PicturesKeptUnheld = 2 * MuseumPlan.PaintingsPerRoom;

        private readonly PictureShelf<Artwork, Texture2D> _pictures;

        /// <summary>The true sizes of the game's own pictures; absent when the pictures come from elsewhere.</summary>
        private readonly PaintingSizes? _sizes;

        /// <param name="pictureOf">
        /// Where a work's picture comes from; nothing for the game's own resources. The Editor's picture commands
        /// give another source to show the screens with pictures the game does not carry yet; that source keeps its
        /// pictures, and the gallery never unloads them.
        /// </param>
        public PaintingGallery(MuseumCatalogue catalogue, Func<Artwork, Texture2D?>? pictureOf = null)
        {
            Catalogue = catalogue;
            _pictures = pictureOf == null
                ? new PictureShelf<Artwork, Texture2D>(PicturesKeptUnheld, artwork => Resources.Load<Texture2D>(MuseumPack.PictureResourceOf(artwork)), Resources.UnloadAsset)
                : new PictureShelf<Artwork, Texture2D>(PicturesKeptUnheld, pictureOf, _ => { });
            _sizes = pictureOf == null ? PaintingSizes.LoadPacked() : null;
        }

        public MuseumCatalogue Catalogue { get; }

        /// <summary>The gallery of the catalogue a built game carries.</summary>
        /// <exception cref="CatalogueFormatException">The catalogue is missing or is not a valid catalogue.</exception>
        public static PaintingGallery OfThePackedMuseum() => new PaintingGallery(MuseumPack.LoadPacked());

        /// <summary>
        /// A painting as a screen draws it, holding its picture in memory until the look is disposed of: the caller
        /// owns the look, and disposes of it when it takes down what shows it.
        /// </summary>
        public PaintingLook LookOf(PaintingNumber number)
        {
            Artwork? artwork = Catalogue.ArtworkAt(number);
            if (artwork == null)
            {
                return new PaintingLook(number, artwork: null, picture: null);
            }

            PictureShelf<Artwork, Texture2D>.PictureHold hold = _pictures.Hold(artwork);
            if (hold.Picture == null)
            {
                Debug.LogError($"The game carries no picture \"{artwork.ImageFile}\" for painting {number} ({artwork.Id}); its canvas is shown blank.");
            }

            return new PaintingLook(number, artwork, hold.Picture, _sizes?.AspectOf(artwork.ImageFile), hold);
        }

        /// <summary>
        /// Unloads the pictures that nothing on any screen shows any more, the few kept for a quick way back too, and
        /// lets Unity free whatever else the closed screens used: what a change of stage does, in the dark.
        /// </summary>
        public void LetGoOfUnusedPictures()
        {
            _pictures.LetGoOfUnheld();
            Resources.UnloadUnusedAssets();
        }
    }
}
