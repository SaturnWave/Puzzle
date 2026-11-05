#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Layout;
using UnityEngine;

namespace Puzzle.Client.Avatar
{
    /// <summary>
    /// The painter standing on a page in one of his standing pictures (§22): beside the hall's workbench, cheering in
    /// a restored room's hall or in front of a whole painting's tray. His picture turns about his feet and is measured
    /// in metres (Editor/AvatarArtImport.cs), so the figure stands where its place puts his feet and is drawn at its
    /// place's points a metre; its soft shadow is in the picture. It holds its picture while it stands, and lets go of
    /// it when it is taken down.
    /// </summary>
    public sealed class PainterFigure : IDisposable
    {
        private readonly PictureShelf<PainterPicture, Sprite>.PictureHold _hold;
        private readonly SpriteRenderer _figure;

        /// <exception cref="ArgumentOutOfRangeException">A bust, which does not stand.</exception>
        public PainterFigure(string name, Transform parent, ArtCatalogue art, PainterGallery painter, PainterPicture pose, DrawOrder order)
        {
            if (!PainterPictures.IsStanding(pose))
            {
                throw new ArgumentOutOfRangeException(nameof(pose), pose, "A bust does not stand on a page: it is framed (PainterPortrait).");
            }

            Pose = pose;
            _hold = painter.Hold(pose);
            _figure = SceneObjects.AddSprite(name, parent, _hold.Picture, art.SpriteMaterial, order);
        }

        public PainterPicture Pose { get; }

        /// <summary>Stands the figure in a place: his feet on its point, drawn at its points a metre.</summary>
        public void Stand(FigurePlace place)
        {
            _figure.transform.localPosition = new Vector3(place.FeetX, place.FeetY, 0f);
            _figure.transform.localScale = new Vector3(place.PointsPerMetre, place.PointsPerMetre, 1f);
        }

        /// <summary>Draws the figure this strongly: 0 not at all, 1 fully.</summary>
        public void Fade(float strength)
        {
            _figure.enabled = strength > 0f;
            _figure.color = new Color(1f, 1f, 1f, Mathf.Clamp01(strength));
        }

        public void Dispose()
        {
            SceneObjects.DestroyObjectOf(_figure);
            _hold.Dispose();
        }
    }
}
