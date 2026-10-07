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
    public sealed class PaintingLook
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

        public PaintingLook(PaintingNumber number, Artwork? artwork, Texture2D? picture)
        {
            Number = number;
            Artwork = artwork;
            Picture = picture;
        }

        public PaintingNumber Number { get; }

        /// <summary>The work that hangs at this place; nothing while none is chosen.</summary>
        public Artwork? Artwork { get; }

        /// <summary>The work's picture; nothing for a blank canvas, and for a work whose picture the game does not carry.</summary>
        public Texture2D? Picture { get; }

        /// <summary>Which way the canvas lies; a canvas whose painting is not chosen yet lies like a landscape.</summary>
        public PaintingOrientation Orientation => Artwork?.Orientation ?? PaintingOrientation.Landscape;

        /// <summary>The canvas' width over its height.</summary>
        public float Aspect => Picture != null ? (float)Picture.width / Picture.height : BlankCanvasAspect;

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
    }

    /// <summary>
    /// The museum's paintings for the screens that draw them: the catalogue, and the pictures it names, each loaded
    /// when a screen first asks for it. The pictures are the game's own resources; Unity keeps one copy of each
    /// however often it is asked for, and whoever closes the screens that showed them tells Unity to let go of the
    /// ones no longer used (<see cref="LetGoOfUnusedPictures"/>), so a hundred paintings never sit in memory together.
    /// </summary>
    public sealed class PaintingGallery
    {
        private readonly Func<Artwork, Texture2D?> _pictureOf;

        /// <param name="pictureOf">
        /// Where a work's picture comes from; nothing for the game's own resources. The Editor's picture commands
        /// give another source to show the screens with pictures the game does not carry yet.
        /// </param>
        public PaintingGallery(MuseumCatalogue catalogue, Func<Artwork, Texture2D?>? pictureOf = null)
        {
            Catalogue = catalogue;
            _pictureOf = pictureOf ?? (artwork => Resources.Load<Texture2D>(MuseumPack.PictureResourceOf(artwork)));
        }

        public MuseumCatalogue Catalogue { get; }

        /// <summary>The gallery of the catalogue a built game carries.</summary>
        /// <exception cref="CatalogueFormatException">The catalogue is missing or is not a valid catalogue.</exception>
        public static PaintingGallery OfThePackedMuseum() => new PaintingGallery(MuseumPack.LoadPacked());

        public PaintingLook LookOf(PaintingNumber number)
        {
            Artwork? artwork = Catalogue.ArtworkAt(number);
            Texture2D? picture = artwork == null ? null : _pictureOf(artwork);
            if (artwork != null && picture == null)
            {
                Debug.LogError($"The game carries no picture \"{artwork.ImageFile}\" for painting {number} ({artwork.Id}); its canvas is shown blank.");
            }

            return new PaintingLook(number, artwork, picture);
        }

        /// <summary>Frees the pictures that nothing on any screen shows any more.</summary>
        public static void LetGoOfUnusedPictures() => Resources.UnloadUnusedAssets();
    }
}
