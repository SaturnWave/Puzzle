#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// The lit alcove of a result's card: a piece of the gallery wall in a beaded gilt frame, with a shelf on which
    /// stands what the game left behind. A level won leaves the jigsaw pieces it gave, in a warm light and among
    /// sparkles (or its star, where it gave none); a lost one leaves the goals it did not reach, each with what was
    /// left of it on a teal badge, and so does a level its player asks to leave: what is still to do. Sizes and places are the web
    /// test page's (gallery.css, .alcove), in points around the middle of the frame.
    /// </summary>
    public sealed class ResultAlcove : IDisposable
    {
        /// <summary>The frame, and the wall and shelf inside its band.</summary>
        public const float Width = 302f;
        public const float Height = 202f;
        private const float NicheWidth = 288f;
        private const float NicheHeight = 188f;

        /// <summary>The middle of the shelf's top, where things stand, lies this far under the frame's middle.</summary>
        private const float ShelfUnderMiddle = 76f;

        /// <summary>A piece stands on the ground shadow of its own drawing, which lies this part of its size above its foot.</summary>
        private const float ShadowInAPiece = 0.08f;

        private const float StarSize = 139f;

        /// <summary>
        /// A jigsaw piece that was won stands in a box this large, leaning a little, as the design canvas shows it
        /// (WinTR); several stand smaller, side by side, leaning one way and the other in turn.
        /// </summary>
        private static readonly Vector2 LonePieceBox = new Vector2(124f, 104f);
        private const float TallestPieceInARow = 74f;
        private const float PieceLeansBy = 7f;

        /// <summary>A piece's knobs reach past its cell: it stands this share of its cell's height above the shelf.</summary>
        private const float PieceOverShelf = 0.3f;

        /// <summary>The width of a pixel on a phone, in the card's points: a piece's rim fades out over it.</summary>
        private const float PieceSoftEdge = 0.33f;

        /// <summary>
        /// The light a won level adds to the alcove's own, behind the star: its size (it fades out before the
        /// niche's edges), how far above the star's middle it lies, its strength, and how far that breathes.
        /// </summary>
        private static readonly Vector2 WinLightSize = new Vector2(264f, 172f);
        private const float WinLightAboveStar = 10f;
        private const float WinLightStrength = 0.5f;
        private const float WinLightBreath = 0.08f;
        private const float WinLightBreathSeconds = 3.4f;
        private static readonly Color WinLight = Tint.Of(0xFFF1CC);

        /// <summary>A sparkle's picture is 24 points across at scale 1.</summary>
        private const float SparkleDrawnSize = 24f;

        /// <summary>A sparkle swells and shrinks between this part of its size and all of it, once in this many seconds.</summary>
        private const float SmallestTwinkle = 0.62f;
        private const float TwinkleSeconds = 3.4f;

        /// <summary>
        /// The sparkles around the star, as the web page places them: from the niche's left and top edge in parts of
        /// its width and height, their size in points, and how far into its swell each one begins.
        /// </summary>
        private static readonly (float Across, float Down, float Size, float Phase)[] Sparkles =
        {
            (0.13f, 0.32f, 24f, 0f),
            (0.88f, 0.27f, 18f, 0.53f),
            (0.85f, 0.66f, 13f, 0.24f),
            (0.16f, 0.66f, 10f, 0.76f),
            (0.78f, 0.10f, 8f, 0.12f),
        };

        /// <summary>One open goal alone stands this large; several stand smaller, side by side with this much room between them and beside the row.</summary>
        private const float LonePieceSize = 109f;
        private const float LargestPieceInARow = 76f;
        private const float GapBetweenPieces = 24f;
        private const float RoomBesideTheRow = 22f;

        /// <summary>A badge hangs over its piece's lower right corner: this far out to the right, this far above the piece's foot.</summary>
        private const float BadgeOutToTheRight = 9f;
        private const float BadgeAboveFoot = 3f;
        private const float BadgeHeight = 30f;
        private const float NarrowestBadge = 32f;
        private const float BadgeRoomBesideNumber = 8f;
        private const float BadgeNumberSize = 16f;

        /// <summary>The badge's picture is its body and the room around it for its edge and drop: 10 points wider, 40 high.</summary>
        private const float BadgePictureMargin = 10f;
        private const float BadgePictureHeight = 40f;

        private readonly Transform _root;

        /// <summary>What pops up on the shelf, in the order it does; each grows from nothing to its own size.</summary>
        private readonly List<Transform> _exhibits = new List<Transform>();
        private readonly List<(Transform Sparkle, float Size, float Phase)> _sparkles = new List<(Transform, float, float)>();
        private readonly List<LoosePiece> _pieces = new List<LoosePiece>();
        private SpriteRenderer? _winLight;

        private ResultAlcove(Transform card, ArtCatalogue art)
        {
            _root = new GameObject("Alcove").transform;
            _root.SetParent(card, worldPositionStays: false);
            SceneObjects.AddSprite("Niche", _root, art.Of(GalleryArt.Alcove), art.SpriteMaterial, DrawOrder.CardArt);
        }

        public Transform Transform => _root;

        /// <summary>How long the exhibits take to pop up, from the first one's beginning to the last one's end.</summary>
        public float PopSeconds => Tempo.ExhibitPop + Tempo.ExhibitEvery * Math.Max(0, _exhibits.Count - 1);

        /// <summary>Builds the alcove of a result with its exhibits, which are not seen until <see cref="Pop"/> shows them.</summary>
        /// <param name="paintings">Where the picture of a piece that was won comes from.</param>
        public static ResultAlcove Create(Transform card, ArtCatalogue art, EffectArt effects, PaintingGallery paintings, LevelResult result)
        {
            var alcove = new ResultAlcove(card, art);
            switch (result)
            {
                case LevelResult.Won { Reward: PieceReward reward }:
                    alcove.StandThePieces(reward, paintings.LookOf(reward.Painting), art, effects);
                    break;
                case LevelResult.Won _:
                    alcove.StandTheStar(art, effects);
                    break;
                case LevelResult.Lost lost:
                    alcove.StandWhatWasLeft(lost.OpenGoals, art);
                    break;
                case LevelResult.Leaving leaving:
                    alcove.StandWhatWasLeft(leaving.OpenGoals, art);
                    break;
                default:
                    throw new InvalidOperationException($"A result of the kind {result.GetType().Name} has nothing to show in the alcove.");
            }

            alcove.Pop(0f);
            return alcove;
        }

        /// <summary>
        /// Shows the exhibits as they are a while after they began to come: each pops up a moment after the one
        /// before it, a little past its size and back.
        /// </summary>
        public void Pop(float secondsIn)
        {
            for (int exhibit = 0; exhibit < _exhibits.Count; exhibit++)
            {
                float progress = Mathf.Clamp01((secondsIn - exhibit * Tempo.ExhibitEvery) / Tempo.ExhibitPop);
                float grown = progress > 0f ? Easing.Overshoot(progress) : 0f;
                _exhibits[exhibit].localScale = new Vector3(grown, grown, 1f);
            }
        }

        /// <summary>The sparkles and the light of a won level, as they are at a moment of the stage's clock: they never stand quite still.</summary>
        public void Twinkle(float clockSeconds)
        {
            foreach ((Transform sparkle, float size, float phase) in _sparkles)
            {
                float swell = (1f + Mathf.Sin(2f * Mathf.PI * (clockSeconds / TwinkleSeconds + phase))) / 2f;
                float drawn = size / SparkleDrawnSize * Mathf.Lerp(SmallestTwinkle, 1f, swell);
                sparkle.localScale = new Vector3(drawn, drawn, 1f);
            }

            if (_winLight != null)
            {
                float breath = Mathf.Sin(2f * Mathf.PI * clockSeconds / WinLightBreathSeconds);
                Color light = WinLight;
                light.a = WinLightStrength + WinLightBreath * breath;
                _winLight.color = light;
            }
        }

        public void Dispose()
        {
            foreach (LoosePiece piece in _pieces)
            {
                piece.Dispose();
            }
        }

        private void StandTheStar(ArtCatalogue art, EffectArt effects)
        {
            Transform stand = AddExhibit("Star", new Vector2(0f, StandingHeight(StarSize)));
            AddWinLight(stand, art, effects);
            SceneObjects.AddSprite("Star", stand, art.Of(PieceArt.Star), art.SpriteMaterial, DrawOrder.CardExhibit).transform.localScale = new Vector3(StarSize, StarSize, 1f);
            AddSparkles(art);
        }

        /// <summary>The jigsaw pieces a won level gave, side by side on the shelf, centred as a row, each as large as its share of the row allows.</summary>
        private void StandThePieces(PieceReward reward, PaintingLook look, ArtCatalogue art, EffectArt effects)
        {
            int count = reward.Pieces.Count;
            var box = count == 1
                ? LonePieceBox
                : new Vector2((NicheWidth - 2f * RoomBesideTheRow - GapBetweenPieces * (count - 1)) / count, TallestPieceInARow);
            float left = -(count * box.x + (count - 1) * GapBetweenPieces) / 2f;
            for (int index = 0; index < count; index++)
            {
                float paintingWidth = PaintingWidthForAPieceIn(box, look, reward.Pieces[index]);
                var middle = new Vector2(left + index * (box.x + GapBetweenPieces) + box.x / 2f, -ShelfUnderMiddle + (0.5f + PieceOverShelf) * box.y);
                Transform stand = AddExhibit("Piece won", middle);
                if (index == count / 2)
                {
                    AddWinLight(stand, art, effects);
                }

                LoosePiece piece = LoosePiece.Create(stand, art, look, reward.Pieces[index], paintingWidth, PieceSoftEdge, DrawOrder.CardExhibit);
                piece.Transform.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? PieceLeansBy : -PieceLeansBy);
                _pieces.Add(piece);
            }

            AddSparkles(art);
        }

        /// <summary>How wide the whole painting is when one of its pieces is drawn so that its cell just fits a box.</summary>
        private static float PaintingWidthForAPieceIn(Vector2 box, PaintingLook look, int pieceNumber)
        {
            // A cut is the same at every size: the piece's cell on a painting one point wide tells its share of any width.
            var cell = look.CutAt(new Vector2(1f, 1f / look.Aspect)).Pieces[pieceNumber].Cell;
            return Mathf.Min(box.x / (float)cell.Width, box.y / (float)cell.Height);
        }

        /// <summary>The warm light of a won level behind what stands on the shelf.</summary>
        private void AddWinLight(Transform stand, ArtCatalogue art, EffectArt effects)
        {
            _winLight = SceneObjects.AddSprite("Light", stand, effects.Glow, art.SpriteMaterial, DrawOrder.CardLight);
            _winLight.transform.localPosition = new Vector3(0f, WinLightAboveStar, 0f);
            _winLight.transform.localScale = new Vector3(WinLightSize.x, WinLightSize.y, 1f);
        }

        private void AddSparkles(ArtCatalogue art)
        {
            foreach ((float across, float down, float size, float phase) in Sparkles)
            {
                var place = new Vector2((across - 0.5f) * NicheWidth, (0.5f - down) * NicheHeight);
                Transform sparkle = SceneObjects.AddSprite("Sparkle", AddExhibit("Sparkle", place), art.Of(GalleryArt.Sparkle), art.SpriteMaterial, DrawOrder.CardExhibit).transform;
                _sparkles.Add((sparkle, size, phase));
            }

            Twinkle(0f);
        }

        /// <summary>The goals a lost game did not reach, side by side on the shelf, centred as a row.</summary>
        private void StandWhatWasLeft(IReadOnlyList<GoalFigure> openGoals, ArtCatalogue art)
        {
            int count = openGoals.Count;
            if (count == 0)
            {
                return; // a game is not lost with every goal met; an empty shelf is the honest picture of it all the same
            }

            float widestInARow = (NicheWidth - 2f * RoomBesideTheRow - GapBetweenPieces * (count - 1)) / count;
            float size = count == 1 ? LonePieceSize : Mathf.Min(LargestPieceInARow, widestInARow);
            float left = -(count * size + (count - 1) * GapBetweenPieces) / 2f;
            for (int goal = 0; goal < count; goal++)
            {
                var middle = new Vector2(left + goal * (size + GapBetweenPieces) + size / 2f, StandingHeight(size));
                Transform stand = AddExhibit("Open goal", middle);
                SceneObjects.AddSprite("Piece", stand, art.Of(openGoals[goal].Icon), art.SpriteMaterial, DrawOrder.CardExhibit).transform.localScale = new Vector3(size, size, 1f);
                AddBadge(stand, openGoals[goal].Remaining, size, art);
            }
        }

        private static void AddBadge(Transform stand, int remaining, float pieceSize, ArtCatalogue art)
        {
            TextMeshPro number = Lettering.Write("Left", stand, art, LetteringStyle.Figure, BadgeNumberSize, Color.white, DrawOrder.CardBadgeLettering);
            string left = remaining.ToString(CultureInfo.InvariantCulture);
            Lettering.Say(number, left);
            float width = Mathf.Max(NarrowestBadge, Lettering.WidthOf(number, left) + 2f * BadgeRoomBesideNumber);

            var middle = new Vector3(pieceSize / 2f + BadgeOutToTheRight - width / 2f, -pieceSize / 2f + BadgeAboveFoot + BadgeHeight / 2f, 0f);
            SpriteRenderer badge = SceneObjects.AddSprite("Badge", stand, art.Of(GalleryArt.CountBadge), art.SpriteMaterial, DrawOrder.CardBadge);
            badge.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            badge.size = new Vector2(width + BadgePictureMargin, BadgePictureHeight);
            badge.transform.localPosition = middle;
            number.transform.localPosition = middle + new Vector3(0f, -Hud.FigureDrop * BadgeNumberSize, 0f);
        }

        /// <summary>The height of the middle of a piece of a given size that stands on the shelf.</summary>
        private static float StandingHeight(float pieceSize) => -ShelfUnderMiddle - ShadowInAPiece * pieceSize + pieceSize / 2f;

        private Transform AddExhibit(string name, Vector2 middle)
        {
            Transform exhibit = new GameObject(name).transform;
            exhibit.SetParent(_root, worldPositionStays: false);
            exhibit.localPosition = new Vector3(middle.x, middle.y, 0f);
            _exhibits.Add(exhibit);
            return exhibit;
        }
    }
}
