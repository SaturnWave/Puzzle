#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// Shows the game's hint on the board: the two pieces of the move glow brightly around them (a broad gold halo, a
    /// pale core close to the piece and a bright edge), a little larger, and every beat they nudge towards each
    /// other's cell and back, as if about to trade, a sheen passing over them as they go. A piece that the move taps
    /// in place glows and swells on the beat instead. The light fades in and then breathes. It is told at every step of
    /// the stage's clock which move to show and how long it has shown, and it puts the pieces it moved back at rest the
    /// moment it shows nothing, so that a touch finds them where they lie.
    /// </summary>
    public sealed class HintMark
    {
        /// <summary>The light comes up over this long when the hint appears.</summary>
        private const float FadeInSeconds = 0.35f;

        /// <summary>One nudge and the rest after it; the first nudge waits for the light to be up.</summary>
        private const float BeatSeconds = 1.5f;
        private const float NudgeSeconds = 0.5f;

        /// <summary>How far a piece goes towards its partner's cell, in cells.</summary>
        private const float NudgeReach = 0.12f;

        /// <summary>The pieces are drawn this much larger while they show the hint; a tapped piece swells by the second on the beat.</summary>
        private const float Grown = 1.06f;
        private const float TapSwell = 0.1f;

        /// <summary>The light under a piece: a broad halo and a core close to the piece, this many cells across; both breathe between this strength and full.</summary>
        private const float HaloAcross = 2.6f;
        private const float CoreAcross = 1.9f;
        private const float FaintestGlow = 0.75f;

        /// <summary>The bright edge around a piece: a ring this many cells across, drawn over the pieces.</summary>
        private const float EdgeAcross = 1.3f;

        /// <summary>The sheen over a piece, this many cells across, is this strong at the farthest of a nudge and gone at rest.</summary>
        private const float SheenAcross = 0.95f;
        private const float SheenStrength = 0.22f;

        /// <summary>The gilt of the frame for the halo, and the gallery lamps' warm white for the core, the edge and the sheen: they read on the dark tray and on every colour of gem.</summary>
        private static readonly Color Gilt = new Color(1f, 0.82f, 0.38f);
        private static readonly Color Light = new Color(1f, 0.96f, 0.8f);

        private readonly Lit _first;
        private readonly Lit _second;
        private Move? _shown;

        /// <param name="board">The board's own object: its unit is a cell, its origin the board's top left corner.</param>
        public HintMark(Transform board, EffectArt art, ArtCatalogue catalogue)
        {
            _first = new Lit(board, art, catalogue, "first");
            _second = new Lit(board, art, catalogue, "second");
        }

        /// <summary>The move the hint shows now; nothing while it shows none.</summary>
        public Move? Shown => _shown;

        /// <summary>Shows a move, as it looks <paramref name="sinceShown"/> seconds after it appeared.</summary>
        /// <param name="first">The piece in the move's first cell (the one a tap fires).</param>
        /// <param name="second">The piece in its second cell; nothing for a tap.</param>
        public void Show(Move move, PieceSprite first, PieceSprite? second, float sinceShown)
        {
            if (_shown != move)
            {
                Hide();
                _shown = move;
            }

            float lit = Easing.Out(Mathf.Clamp01(sinceShown / FadeInSeconds));
            float breath = (Mathf.Sin(sinceShown * 2f * Mathf.PI / BeatSeconds) + 1f) / 2f;
            float strength = lit * Mathf.Lerp(FaintestGlow, 1f, breath);
            float beat = NudgeAt(sinceShown);
            float sheen = lit * SheenStrength * beat;
            if (second == null)
            {
                _first.Show(first, move.From, move.From, 0f, Grown + TapSwell * beat, strength, sheen);
                _second.Hide();
            }
            else
            {
                _first.Show(first, move.From, move.To, NudgeReach * beat, Grown, strength, sheen);
                _second.Show(second, move.To, move.From, NudgeReach * beat, Grown, strength, sheen);
            }
        }

        /// <summary>Shows nothing: the light goes out at once and the pieces are put back at rest in their cells.</summary>
        public void Hide()
        {
            _first.Hide();
            _second.Hide();
            _shown = null;
        }

        /// <summary>How far into its nudge the beat is, 0 at rest to 1 at its farthest; the first beat waits for the light.</summary>
        private static float NudgeAt(float sinceShown)
        {
            float beatTime = sinceShown - FadeInSeconds;
            if (beatTime < 0f)
            {
                return 0f;
            }

            float intoBeat = beatTime % BeatSeconds;
            return intoBeat < NudgeSeconds ? Easing.Arch(intoBeat / NudgeSeconds) : 0f;
        }

        /// <summary>The light around one piece of the hint, and the piece it lights.</summary>
        private sealed class Lit
        {
            private readonly SpriteRenderer _halo;
            private readonly SpriteRenderer _core;
            private readonly SpriteRenderer _edge;
            private readonly SpriteRenderer _sheen;
            private PieceSprite? _piece;
            private Position _cell;

            public Lit(Transform board, EffectArt art, ArtCatalogue catalogue, string name)
            {
                _halo = Sprite(board, art.Glow, catalogue, $"Hint halo ({name})", DrawOrder.UnderPieces);
                _core = Sprite(board, art.Glow, catalogue, $"Hint core ({name})", DrawOrder.UnderPieces);
                _edge = Sprite(board, art.Ring, catalogue, $"Hint edge ({name})", DrawOrder.Effect);
                _sheen = Sprite(board, art.Glow, catalogue, $"Hint sheen ({name})", DrawOrder.Effect);
            }

            public void Show(PieceSprite piece, Position cell, Position toward, float reach, float size, float strength, float sheen)
            {
                if (_piece != piece)
                {
                    Hide();
                    _piece = piece;
                    _cell = cell;
                }

                Vector2 home = PieceSprite.MiddleOf(cell);
                Vector2 place = Vector2.Lerp(home, PieceSprite.MiddleOf(toward), reach);
                piece.Place = place;
                piece.Size(size, size);
                Place(_halo, place, HaloAcross * size, Lighted(Gilt, strength));
                Place(_core, place, CoreAcross * size, Lighted(Light, strength));
                Place(_edge, place, EdgeAcross * size, Lighted(Light, strength));
                Place(_sheen, place, SheenAcross * size, Lighted(Color.white, sheen));
            }

            /// <summary>The light goes out, and the piece it lit lies at rest in its cell again.</summary>
            public void Hide()
            {
                _halo.enabled = false;
                _core.enabled = false;
                _edge.enabled = false;
                _sheen.enabled = false;
                if (_piece != null)
                {
                    _piece.Place = PieceSprite.MiddleOf(_cell);
                    _piece.Size(1f, 1f);
                    _piece = null;
                }
            }

            private static Color Lighted(Color colour, float strength) => new Color(colour.r, colour.g, colour.b, strength);

            private static void Place(SpriteRenderer renderer, Vector2 place, float across, Color color)
            {
                renderer.enabled = true;
                renderer.transform.localPosition = new Vector3(place.x, -place.y, 0f);
                renderer.transform.localScale = new Vector3(across, across, 1f);
                renderer.color = color;
            }

            private static SpriteRenderer Sprite(Transform board, Sprite sprite, ArtCatalogue catalogue, string name, DrawOrder order)
            {
                SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(board, worldPositionStays: false);
                renderer.sprite = sprite;
                renderer.sharedMaterial = catalogue.SpriteMaterial;
                renderer.sortingOrder = (int)order;
                renderer.enabled = false;
                return renderer;
            }
        }
    }
}
