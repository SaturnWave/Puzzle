#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using Puzzle.Client.Painting;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What is thrown over the board while a turn is played back: the chips of a bursting piece, a rocket's stroke of
    /// ink, a bomb's blot, a colour bomb's rays, a seeker on its way. Each is a sprite or a few that live for a
    /// moment on the turn's timeline and are then removed. Nothing here changes the board: the
    /// pieces themselves are moved by <see cref="TurnPlayer"/>. Places are in cells, rows growing downwards.
    /// </summary>
    public sealed class BoardEffects
    {
        private const float BurstSeconds = 0.3f;
        private const float FlashSeconds = 0.2f;
        private const int ChipsPerBurst = 6;

        /// <summary>A stroke and a blot may run this far over the tray's edge, under the frame's lip, before the mask cuts them.</summary>
        private const float OverTheEdge = 0.3f;

        /// <summary>A seeker beats its wings this often per second on its way.</summary>
        private const float WingBeatsPerSecond = 7f;

        private static readonly Color Light = Tint.Of(0xFFFFFF);
        private static readonly Color WarmLight = Tint.Of(0xFFEDB0);

        /// <summary>The object every effect's sprite hangs under: a layer of the board's own, with the board's unit and origin.</summary>
        private readonly Transform _layer;
        private readonly BoardShape _shape;
        private readonly EffectArt _art;
        private readonly ArtCatalogue _catalogue;
        private readonly Timeline _timeline;

        /// <param name="board">The board's own object: its unit is a cell, its origin the board's top left corner.</param>
        public BoardEffects(Transform board, BoardShape shape, EffectArt art, ArtCatalogue catalogue, Timeline timeline)
        {
            _layer = new GameObject("Effects").transform;
            _layer.SetParent(board, worldPositionStays: false);
            _shape = shape;
            _art = art;
            _catalogue = catalogue;
            _timeline = timeline;
        }

        /// <summary>
        /// Takes every effect's sprite off the stage. An effect removes its own sprite when its time is over, so
        /// after a turn there is nothing to take; this is for what an effect left behind that was cut off.
        /// </summary>
        public void ClearAway()
        {
            for (int index = _layer.childCount - 1; index >= 0; index--)
            {
                SceneObjects.Destroy(_layer.GetChild(index).gameObject);
            }
        }

        /// <summary>A piece bursts: a blink of light and chips of its colour that fly apart and drop.</summary>
        public void Burst(float startsIn, Vector2 place, Tile tile)
        {
            Flash(startsIn, place, 1.5f, Light, 0.7f);
            Color colour = ChipColourOf(tile);
            for (int chip = 0; chip < ChipsPerBurst; chip++)
            {
                float angle = (chip + Scatter(place, chip)) * 2f * Mathf.PI / ChipsPerBurst;
                var heading = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle));
                float reach = 0.42f + 0.25f * Scatter(place, chip + ChipsPerBurst);
                Show(startsIn, BurstSeconds, _art.Dot, DrawOrder.Effect, isCutAtTheEdge: true, (dot, progress) =>
                {
                    Vector2 drop = new Vector2(0f, 0.3f * progress * progress);
                    Put(dot, place + heading * (reach * Easing.Out(progress)) + drop, Mathf.Lerp(0.2f, 0.07f, progress));
                    dot.color = Faded(colour, 1f - Easing.Within(progress, 0.55f, 1f));
                });
            }
        }

        /// <summary>A blink of light at a place, so many cells across at its widest.</summary>
        public void Flash(float startsIn, Vector2 place, float size, Color colour, float strength)
        {
            Show(startsIn, FlashSeconds, _art.Glow, DrawOrder.EffectLight, isCutAtTheEdge: false, (glow, progress) =>
            {
                Put(glow, place, size * Mathf.Lerp(0.5f, 1f, Easing.Out(progress)));
                glow.color = Faded(colour, strength * (1f - progress));
            });
        }

        /// <summary>A ring that widens from a place and fades: something went off there.</summary>
        public void Wave(float startsIn, Vector2 place, float size)
        {
            Show(startsIn, FlashSeconds * 1.5f, _art.Ring, DrawOrder.EffectLight, isCutAtTheEdge: false, (ring, progress) =>
            {
                Put(ring, place, size * Mathf.Lerp(0.3f, 1f, Easing.Out(progress)));
                ring.color = Faded(Light, 0.8f * (1f - progress));
            });
        }

        /// <summary>The effect of a special piece as a whole; the pieces it hits burst by cues of their own.</summary>
        public void Play(Cue.Effect effect)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            switch (effect.Kind)
            {
                case EffectKind.ClearRow:
                    Stroke(effect, isAlongTheRow: true);
                    FlyRocketHeads(effect, isAlongTheRow: true);
                    break;
                case EffectKind.ClearColumn:
                    Stroke(effect, isAlongTheRow: false);
                    FlyRocketHeads(effect, isAlongTheRow: false);
                    break;
                case EffectKind.Cross:
                    Stroke(effect, isAlongTheRow: true);
                    Stroke(effect, isAlongTheRow: false);
                    Flash(effect.Start, origin, 2f * effect.Radius + 2.5f, Light, 0.9f);
                    break;
                case EffectKind.Explode:
                    Splash(effect);
                    break;
                case EffectKind.Seeker:
                    Flash(effect.Start, origin, 2.4f, WarmLight, 0.9f);
                    Wave(effect.Start, origin, 2.6f);
                    break;
                case EffectKind.ClearColor:
                case EffectKind.ConvertColor:
                    SpinColourBomb(effect);
                    break;
                case EffectKind.ClearBoard:
                    FloodWithLight(effect);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "An effect without a picture.");
            }
        }

        /// <summary>A seeker flies in an arc to its target, beating its wings, and is gone in a blink where it lands.</summary>
        public void Fly(Cue.Flight flight)
        {
            Vector2 from = PieceSprite.MiddleOf(flight.From);
            Vector2 to = PieceSprite.MiddleOf(flight.To);
            Vector2 way = to - from;

            // The arc bulges to the side of the way that lies higher on the screen; rows grow downwards.
            var aside = new Vector2(way.y, -way.x);
            aside = aside.y > 0f ? -aside : aside;
            Vector2 turningPoint = (from + to) / 2f + aside.normalized * Mathf.Min(1.4f, 0.35f * way.magnitude) + new Vector2(0f, -0.4f);
            float lean = -Mathf.Sign(way.x) * 14f;

            Show(flight.Start, flight.Seconds, _catalogue.Of(PieceArt.Seeker), DrawOrder.Flight, isCutAtTheEdge: false, (seeker, progress) =>
            {
                float along = Easing.InOut(progress);
                Vector2 place = (1f - along) * (1f - along) * from + 2f * (1f - along) * along * turningPoint + along * along * to;
                float size = BoardView.PieceScale * (1f + 0.25f * Easing.Arch(progress));
                float wings = 0.68f + 0.32f * Mathf.Abs(Mathf.Cos(progress * flight.Seconds * WingBeatsPerSecond * 2f * Mathf.PI));
                Put(seeker, place, size * wings, size, lean * Easing.Arch(progress));
                seeker.color = Faded(Light, 1f - Easing.Within(progress, 0.9f, 1f));
            });
            Flash(flight.End, to, 1.7f, Light, 0.9f);
        }

        /// <summary>A ray of a colour bomb's light shoots from the bomb to a cell and fades.</summary>
        public void Ray(Cue.Zap zap)
        {
            const float FadeSeconds = 0.12f;
            Vector2 from = PieceSprite.MiddleOf(zap.From);
            Vector2 way = PieceSprite.MiddleOf(zap.To) - from;
            float turn = Mathf.Atan2(-way.y, way.x) * Mathf.Rad2Deg;
            float seconds = zap.Seconds + FadeSeconds;
            Show(zap.Start, seconds, _art.Streak, DrawOrder.EffectLight, isCutAtTheEdge: false, (ray, progress) =>
            {
                float reached = Mathf.Max(0.05f, Easing.Out(Easing.Within(progress * seconds, 0f, zap.Seconds)));
                Put(ray, from + way * (reached / 2f), way.magnitude * reached, 1.3f, turn);
                ray.color = Faded(WarmLight, 1f - Easing.Within(progress * seconds, zap.Seconds, seconds));
            });
            Flash(zap.End, PieceSprite.MiddleOf(zap.To), 1.5f, WarmLight, 0.8f);
        }

        /// <summary>
        /// Brush strokes of ink that grow from the piece along its row or column, then dry away: one for a rocket, and
        /// for a rocket pushed onto a bomb one for every row or column of the band, side by side.
        /// </summary>
        private void Stroke(Cue.Effect effect, bool isAlongTheRow)
        {
            for (int aside = -effect.Radius; aside <= effect.Radius; aside++)
            {
                StrokeOneLine(effect, isAlongTheRow, aside);
            }
        }

        /// <summary>
        /// One stroke along a row or a column, in three parts that are posed alike in every step: a round end at
        /// each side, half a cell long, and a middle that is stretched between them.
        /// </summary>
        /// <param name="aside">How many rows (for a stroke along a row) or columns the line lies beside the piece.</param>
        private void StrokeOneLine(Cue.Effect effect, bool isAlongTheRow, int aside)
        {
            const float EndLength = 0.5f;
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            float middle = isAlongTheRow ? origin.x : origin.y;
            float line = (isAlongTheRow ? origin.y : origin.x) + aside;
            float boardLength = isAlongTheRow ? _shape.Width : _shape.Height;
            float reachSeconds = effect.Seconds - Tempo.EffectLinger;

            // A sprite's own x axis lies along the stroke: to the right along a row, down the screen along a column.
            float turn = isAlongTheRow ? 0f : -90f;
            Vector2 PlaceAlong(float along) => isAlongTheRow ? new Vector2(along, line) : new Vector2(line, along);

            // The lines of a band start as the block the band is wide, as the hits do (Strike).
            (float From, float Until) SpanAfter(float elapsed)
            {
                float reach = effect.Radius + 0.5f + elapsed / effect.SecondsPerCell;
                return (Mathf.Max(-OverTheEdge, middle - reach), Mathf.Min(boardLength + OverTheEdge, middle + reach));
            }

            Color InkAfter(float elapsed) => Faded(Light, 1f - Easing.In(Easing.Within(elapsed, reachSeconds, effect.Seconds)));

            void ShowPart(Sprite part, Action<SpriteRenderer, float, float> pose) =>
                Show(effect.Start, effect.Seconds, part, DrawOrder.Effect, isCutAtTheEdge: true, (sprite, progress) =>
                {
                    float elapsed = progress * effect.Seconds;
                    (float from, float until) = SpanAfter(elapsed);
                    pose(sprite, from, until);
                    sprite.color = InkAfter(elapsed);
                });

            ShowPart(_art.StrokeEnd, (end, from, until) => Put(end, PlaceAlong(from + EndLength), 1f, 1f, turn));
            ShowPart(_art.StrokeEnd, (end, from, until) => Put(end, PlaceAlong(until - EndLength), 1f, 1f, turn + 180f));
            ShowPart(_art.StrokeMiddle, (body, from, until) => Put(body, PlaceAlong((from + until) / 2f), Mathf.Max(0f, until - from - 2f * EndLength), 1f, turn));
        }

        /// <summary>The rocket itself, as two heads that fly apart at the tip of the stroke and off the board.</summary>
        private void FlyRocketHeads(Cue.Effect effect, bool isAlongTheRow)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            Sprite rocket = _catalogue.Of(isAlongTheRow ? PieceArt.RocketHorizontal : PieceArt.RocketVertical);
            float flightSeconds = effect.Seconds - Tempo.EffectLinger + effect.SecondsPerCell;
            foreach (int direction in new[] { -1, 1 })
            {
                Show(effect.Start, flightSeconds, rocket, DrawOrder.EffectPiece, isCutAtTheEdge: true, (head, progress) =>
                {
                    float flown = direction * (progress * flightSeconds / effect.SecondsPerCell);
                    Put(head, origin + (isAlongTheRow ? new Vector2(flown, 0f) : new Vector2(0f, flown)), BoardView.PieceScale);
                });
            }
        }

        /// <summary>A bomb's blot of ink: it spreads fast, stands for a moment and fades.</summary>
        private void Splash(Cue.Effect effect)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            float across = 2f * effect.Radius + 2f;
            Flash(effect.Start, origin, across, Light, 0.95f);
            Wave(effect.Start, origin, across + 1f);
            Show(effect.Start, effect.Seconds, _art.Splat, DrawOrder.Effect, isCutAtTheEdge: true, (blot, progress) =>
            {
                Put(blot, origin, across * Mathf.Lerp(0.4f, 1.1f, Easing.Out(progress)));
                blot.color = Faded(Light, 1f - Easing.Within(progress, 0.55f, 1f));
            });
        }

        /// <summary>A colour bomb turns where it lay, in a pool of light, for as long as its rays go out.</summary>
        private void SpinColourBomb(Cue.Effect effect)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            Show(effect.Start, effect.Seconds, _art.Glow, DrawOrder.UnderPieces, isCutAtTheEdge: false, (glow, progress) =>
            {
                Put(glow, origin, Mathf.Lerp(1.8f, 3.2f, Easing.Out(progress)));
                glow.color = Faded(WarmLight, 0.9f * Easing.Arch(Mathf.Sqrt(progress)));
            });
            Show(effect.Start, effect.Seconds, _catalogue.Of(PieceArt.ColorBomb), DrawOrder.EffectPiece, isCutAtTheEdge: true, (bomb, progress) =>
            {
                float size = BoardView.PieceScale * (1.15f + 0.08f * Mathf.Sin(progress * effect.Seconds * 14f));
                Put(bomb, origin, size, size, -540f * progress * effect.Seconds);
                bomb.color = Faded(Light, 1f - Easing.Within(progress, 0.8f, 1f));
            });
        }

        /// <summary>Two colour bombs: light that floods the tray from where they met, at the pace the pieces burst.</summary>
        private void FloodWithLight(Cue.Effect effect)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            float farthest = 2.6f * Mathf.Max(_shape.Width, _shape.Height);
            Show(effect.Start, effect.Seconds, _art.Glow, DrawOrder.EffectLight, isCutAtTheEdge: true, (glow, progress) =>
            {
                Put(glow, origin, Mathf.Lerp(1.5f, farthest, Easing.Out(progress)));
                glow.color = Faded(WarmLight, 0.9f * (1f - Easing.In(progress)));
            });
            Wave(effect.Start, origin, farthest / 2f);
        }

        /// <summary>
        /// A sprite that is on the stage for a while: made when its moment has come, posed in every step, and
        /// removed when the time is over.
        /// </summary>
        /// <remarks>
        /// Each gets a sprite of its own, which is not kept for the next one: a few dozen short-lived objects in a
        /// turn have not been measured to matter, and a sprite that is never used again carries nothing over.
        /// </remarks>
        private void Show(float startsIn, float seconds, Sprite sprite, DrawOrder order, bool isCutAtTheEdge, Action<SpriteRenderer, float> pose)
        {
            SpriteRenderer? shown = null;
            _timeline.Add(
                startsIn,
                seconds,
                progress =>
                {
                    shown ??= NewSprite(sprite, order, isCutAtTheEdge);
                    pose(shown, progress);
                },
                () =>
                {
                    if (shown != null)
                    {
                        SceneObjects.DestroyObjectOf(shown);
                    }
                });
        }

        private SpriteRenderer NewSprite(Sprite sprite, DrawOrder order, bool isCutAtTheEdge)
        {
            var renderer = new GameObject(sprite.name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_layer, worldPositionStays: false);
            renderer.sharedMaterial = _catalogue.SpriteMaterial;
            renderer.sprite = sprite;
            renderer.sortingOrder = (int)order;
            renderer.maskInteraction = isCutAtTheEdge ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            return renderer;
        }

        private static void Put(SpriteRenderer sprite, Vector2 place, float size) => Put(sprite, place, size, size, 0f);

        /// <summary>Puts a sprite's middle at a place in cells, so wide and so high, turned so many degrees counter-clockwise.</summary>
        private static void Put(SpriteRenderer sprite, Vector2 place, float width, float height, float turn)
        {
            Transform transform = sprite.transform;
            transform.localPosition = new Vector3(place.x, -place.y, 0f);
            transform.localScale = new Vector3(width, height, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, turn);
        }

        private static Color Faded(Color colour, float strength) => new Color(colour.r, colour.g, colour.b, colour.a * Mathf.Clamp01(strength));

        /// <summary>
        /// A number from 0 up to 1 that looks like chance but is decided by a place and a count: chips fly apart
        /// unevenly, and the same burst looks the same in every run, as a screenshot of a moment must.
        /// </summary>
        private static float Scatter(Vector2 place, int count)
        {
            unchecked
            {
                uint mixed = (uint)Mathf.RoundToInt(place.x * 8f) * 73856093u ^ (uint)Mathf.RoundToInt(place.y * 8f) * 19349663u ^ (uint)count * 83492791u;
                mixed ^= mixed >> 13;
                mixed *= 0x5BD1E995u;
                mixed ^= mixed >> 15;
                return (mixed & 0xFFFF) / 65536f;
            }
        }

        /// <summary>The colour of what flies off a piece: its own main colour.</summary>
        private static Color ChipColourOf(Tile tile)
        {
            switch (tile.Kind)
            {
                case TileKind.Gem:
                    return ChipColourOf(tile.Color);
                case TileKind.Box:
                    return Tint.Of(0xB98552);
                case TileKind.Item:
                    return Tint.Of(0xF6C445);
                default:
                    return Tint.Of(0x6C55C8);
            }
        }

        private static Color ChipColourOf(GemColor colour) => colour switch
        {
            GemColor.Red => Tint.Of(0xE2403F),
            GemColor.Green => Tint.Of(0x3DB54E),
            GemColor.Blue => Tint.Of(0x3489EA),
            GemColor.Yellow => Tint.Of(0xF8CB3A),
            GemColor.Purple => Tint.Of(0x9460DA),
            GemColor.Orange => Tint.Of(0xF28C30),
            GemColor.Cyan => Tint.Of(0x3CC8DA),
            GemColor.Pink => Tint.Of(0xF072AC),
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A gem colour without a chip colour."),
        };
    }
}
