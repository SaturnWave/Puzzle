#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// What stands at the world map's corners (MapWorldTR): the legend at the top left (a gilt dot "Onarıldı", a red one
    /// "Buradasın", a cream one "İleride"), the two round buttons at the top right (the globe shows the whole world and
    /// is lit while it does; the target goes to where the player is and opens the card of the painting being
    /// restored), and the compass rose at the lower left, its letter in the language spoken.
    /// </summary>
    public sealed class MapControls : IDisposable
    {
        /// <summary>The design's places, in the sheet's points: the legend 8 in from the top left; the buttons' boxes of 44 at 6 from the right, 30 and 78 down; the compass of 50 at 8 from the left, 56 over the strip.</summary>
        private const float LegendIn = 8f;
        private const float LegendHeight = 20f;
        private const float LegendPadding = 8f;
        private const float LegendDotGap = 3f;
        private const float LegendItemGap = 8f;
        private const float LegendSize = 9.5f;
        private const float LegendDot = 7f;
        private const float LegendDrawnHeight = 20f;
        private const float LegendPictureMarginAcross = 16f;
        private const float LegendPictureHeight = 28f;

        private const float ButtonBox = 44f;
        private const float ButtonsFromTheRight = 6f;
        private const float GlobeDown = 30f;
        private const float TargetDown = 78f;
        private const float GlyphSize = 19f;
        private const float GlyphBox = 24f;

        private const float CompassSize = 50f;
        private const float CompassDrawn = 60f;
        private const float CompassIn = 8f;
        private const float CompassOverTheStrip = 56f;
        private const float CompassLetterSize = 7f;
        private const float CompassLetterUp = 17.5f;

        /// <summary>The design's dots are 7 points across; their pictures draw them so, with room for their lines.</summary>
        private const float DotDrawn = 7f;
        private const float HereDotDrawn = 9f;

        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly SpriteRenderer _legend;
        private readonly (SpriteRenderer Dot, TextMeshPro Words)[] _items;
        private readonly SpriteRenderer _globeFace;
        private readonly Transform _globe;
        private readonly Transform _target;
        private readonly Transform _compass;
        private readonly TextMeshPro _north;

        public MapControls(Transform parent, ArtCatalogue art)
        {
            _art = art;
            _root = new GameObject("Map controls").transform;
            _root.SetParent(parent, worldPositionStays: false);

            _legend = SceneObjects.AddSprite("Legend", _root, art.Of(GalleryArt.MapLegend), art.SpriteMaterial, DrawOrder.MapLegend);
            _legend.drawMode = SpriteDrawMode.Sliced;
            _items = new[]
            {
                LegendItem(GalleryArt.MapDotRestored, MapWords.LegendRestored),
                LegendItem(GalleryArt.MapDotHere, MapWords.LegendHere),
                LegendItem(GalleryArt.MapDotAhead, MapWords.LegendAhead),
            };

            (_globe, _globeFace) = RoundButton("Globe", GalleryArt.MapGlyphGlobe);
            (_target, _) = RoundButton("Target", GalleryArt.MapGlyphTarget);
            Globe = new MapButton(MapWords.ShowTheWorld, _globe);
            Target = new MapButton(MapWords.GoToYourPlace, _target);

            _compass = SceneObjects.AddSprite("Compass", _root, art.Of(GalleryArt.MapCompass), art.SpriteMaterial, DrawOrder.MapCompass).transform;
            _compass.localScale = Vector3.one * (CompassSize / CompassDrawn);
            _north = Lettering.Write("North", _root, art, LetteringStyle.Title, CompassLetterSize, Lettering.Ink, DrawOrder.MapCompass);
            Lettering.Say(_north, MapWords.CompassNorth);
        }

        /// <summary>The globe: the whole world.</summary>
        public MapButton Globe { get; }

        /// <summary>The target: where the player is, with the card of the painting being restored.</summary>
        public MapButton Target { get; }

        public void Fit(MapLayout layout)
        {
            // The legend: its dots and words in a row on its plate, as wide as they need, no wider than the map left of the buttons.
            float room = layout.MapWidth - 2f * LegendIn - ButtonBox - ButtonsFromTheRight - 2f * LegendPadding;
            float size = LegendSize;
            float RowWidth() => _items.Length * (LegendDot + LegendDotGap) + (_items.Length - 1) * LegendItemGap + SumOfWords(size);
            while (RowWidth() > room && size > LegendSize * 0.7f)
            {
                size -= 0.25f;
            }

            float plateWidth = RowWidth() + 2f * LegendPadding;
            (float left, float top) = layout.PageOf(LegendIn, LegendIn);
            float middleY = top - LegendHeight / 2f;
            float scale = LegendHeight / LegendDrawnHeight;
            _legend.size = new Vector2(plateWidth / scale + LegendPictureMarginAcross, LegendPictureHeight);
            _legend.transform.localScale = Vector3.one * scale;
            _legend.transform.localPosition = new Vector3(left + plateWidth / 2f, middleY, 0f);
            float x = left + LegendPadding;
            foreach ((SpriteRenderer dot, TextMeshPro words) in _items)
            {
                dot.transform.localPosition = new Vector3(x + LegendDot / 2f, middleY, 0f);
                x += LegendDot + LegendDotGap;
                words.fontSize = size;
                Lettering.SayFromLeft(words, words.text, x, middleY);
                x += Lettering.WidthOf(words, words.text) + LegendItemGap;
            }

            Place(Globe, _globe, layout, GlobeDown);
            Place(Target, _target, layout, TargetDown);

            (float compassX, float compassY) = layout.PageOf(CompassIn + CompassSize / 2f, layout.MapHeight - CompassOverTheStrip + CompassSize / 2f);
            _compass.localPosition = new Vector3(compassX, compassY, 0f);
            _north.transform.localPosition = new Vector3(compassX, compassY + CompassLetterUp, 0f);
        }

        /// <summary>The globe is lit while the whole world is shown.</summary>
        public void ShowTheWorld(bool isShown) => _globeFace.sprite = _art.Of(isShown ? GalleryArt.MapButtonLit : GalleryArt.MapButton);

        public void Dispose() => SceneObjects.DestroyObjectOf(_root);

        private float SumOfWords(float size)
        {
            float sum = 0f;
            foreach ((_, TextMeshPro words) in _items)
            {
                words.fontSize = size;
                sum += Lettering.WidthOf(words, words.text);
            }

            return sum;
        }

        private (SpriteRenderer Dot, TextMeshPro Words) LegendItem(GalleryArt dot, string words)
        {
            SpriteRenderer drawn = SceneObjects.AddSprite("Legend dot", _root, _art.Of(dot), _art.SpriteMaterial, DrawOrder.MapLegendArt);
            drawn.transform.localScale = Vector3.one * (LegendDot / (dot == GalleryArt.MapDotHere ? HereDotDrawn : DotDrawn));
            TextMeshPro said = Lettering.Write("Legend words", _root, _art, LetteringStyle.Soft, LegendSize, Lettering.Ink, DrawOrder.MapLegendLettering);
            Lettering.Say(said, words);
            return (drawn, said);
        }

        private (Transform Button, SpriteRenderer Face) RoundButton(string name, GalleryArt glyph)
        {
            Transform button = new GameObject(name).transform;
            button.SetParent(_root, worldPositionStays: false);
            SpriteRenderer face = SceneObjects.AddSprite("Face", button, _art.Of(GalleryArt.MapButton), _art.SpriteMaterial, DrawOrder.MapButton);
            SceneObjects.AddSprite("Glyph", button, _art.Of(glyph), _art.SpriteMaterial, DrawOrder.MapButtonGlyph).transform.localScale = Vector3.one * (GlyphSize / GlyphBox);
            return (button, face);
        }

        private static void Place(MapButton button, Transform drawing, MapLayout layout, float down)
        {
            (float x, float y) = layout.PageOf(layout.MapWidth - ButtonsFromTheRight - ButtonBox / 2f, down + ButtonBox / 2f);
            drawing.localPosition = new Vector3(x, y, 0f);
            drawing.localScale = Vector3.one;
            button.Aim(new Vector2(x, y), ButtonBox, ButtonBox);
        }
    }
}
