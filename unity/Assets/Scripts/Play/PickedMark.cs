#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// Shows which piece the player has picked with a tap: a gilt ring full of light under it, and the piece itself
    /// a little larger, both breathing slowly. The ring lies on the edge of the piece's cell, so it shows around
    /// every piece, the square gem that nearly fills its cell included. It is told in every step of the stage's
    /// clock which piece is picked, if any, and puts the piece it marked before back to rest when that changes.
    /// </summary>
    public sealed class PickedMark
    {
        /// <summary>The picked piece is drawn this much larger, and breathes by this much around that.</summary>
        private const float Grown = 1.08f;
        private const float Breath = 0.02f;
        private const float BreathSeconds = 1.1f;

        /// <summary>
        /// The ring breathes between this strength and full strength. Ring and glow are one picture, so this is as
        /// faint as the gilt line itself gets: it has to stay plain to see at every moment, so the breath is slight.
        /// </summary>
        private const float FaintestRing = 0.9f;

        private readonly SpriteRenderer _ring;
        private PieceSprite? _marked;

        /// <param name="board">The board's own object: its unit is a cell, its origin the board's top left corner.</param>
        public PickedMark(Transform board, EffectArt art, ArtCatalogue catalogue)
        {
            _ring = new GameObject("Picked mark").AddComponent<SpriteRenderer>();
            _ring.transform.SetParent(board, worldPositionStays: false);
            _ring.sprite = art.Mark;
            _ring.sharedMaterial = catalogue.SpriteMaterial;
            _ring.sortingOrder = (int)DrawOrder.UnderPieces;
            _ring.enabled = false;
        }

        /// <summary>Marks a piece, or none, as it looks at a moment of the stage's clock.</summary>
        public void Show(PieceSprite? picked, float clockSeconds)
        {
            if (_marked != picked)
            {
                _marked?.Size(1f, 1f);
                _marked = picked;
            }

            _ring.enabled = picked != null;
            if (picked == null)
            {
                return;
            }

            float breath = Mathf.Sin(clockSeconds * 2f * Mathf.PI / BreathSeconds);
            float size = Grown + Breath * breath;
            picked.Size(size, size);
            _ring.transform.localPosition = new Vector3(picked.Place.x, -picked.Place.y, 0f);
            _ring.color = new Color(1f, 1f, 1f, Mathf.Lerp(FaintestRing, 1f, (breath + 1f) / 2f));
        }
    }
}
