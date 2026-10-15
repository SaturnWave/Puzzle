#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Boards;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// What is thrown over the board while a turn is played back: the chips of a bursting piece, a rocket's stroke of
    /// ink, a bomb's blot, a colour bomb's rays, a seeker on its way, and in a won game's finale the moves left
    /// over that fly from their medallion to the board. Each is a sprite or a few that live for a moment on the
    /// turn's timeline and are then removed. Nothing here changes the board: the pieces themselves are moved by
    /// <see cref="TurnPlayer"/>. Places are in cells, rows growing downwards; the medallion hangs above the first
    /// row, so its row is less than nothing.
    /// </summary>
    public sealed class BoardEffects
    {
        /// <summary>The name of the object every effect's sprite hangs under, for the Editor's checks that look at what is drawn.</summary>
        public const string LayerName = "Effects";

        private const float BurstSeconds = 0.3f;
        private const float FlashSeconds = 0.2f;
        private const int ChipsPerBurst = 6;

        /// <summary>
        /// The ring of the ring's picture is this part of the picture across, so a ring drawn that much larger
        /// lies where it is meant to.
        /// </summary>
        private const float RingShareOfItsPicture = 0.78f;

        /// <summary>
        /// A thrown move, in cells: the gilt light around it, the pale light in that, the star of two rays in its
        /// middle and the star's core; the tail it draws behind it (as wide as this, and as long as the way it
        /// came in the last third of its flight); and how far to the side its arc bulges on a way that leads
        /// straight down.
        /// </summary>
        private const float MoteGlow = 2.6f;
        private const float MoteInnerGlow = 1.3f;
        private const float MoteStar = 1.15f;
        private const float MoteCore = 0.3f;
        private const float MoteTailWidth = 0.7f;
        private const float MoteTailOfTheFlight = 0.34f;
        private const float MoteLeastBulge = 0.5f;

        /// <summary>The star of a thrown move turns this many degrees on its way.</summary>
        private const float MoteStarTurns = 70f;

        /// <summary>
        /// A thrown move is not seen over the number it leaves, which must stay readable while it counts down: it
        /// comes into sight between these parts of the medallion's width from the medallion's middle.
        /// </summary>
        private const float MoteUnseenWithin = 0.3f;
        private const float MoteSeenFrom = 0.56f;

        /// <summary>The streak's picture is this wide where it is one cell long.</summary>
        private const float StreakWidthOfItsPicture = 0.25f;

        /// <summary>The medallion glows this long behind a move that leaves it.</summary>
        private const float HaloSeconds = 0.24f;

        /// <summary>A stroke and a blot may run this far over the tray's edge, under the frame's lip, before the mask cuts them.</summary>
        private const float OverTheEdge = 0.3f;

        /// <summary>A seeker beats its wings this often per second on its way.</summary>
        private const float WingBeatsPerSecond = 7f;

        /// <summary>The warm light of the gallery's lamps: whatever glows on the stage without a colour of its own glows in it.</summary>
        public static readonly Color WarmLight = Tint.Of(0xFFEDB0);

        private static readonly Color Light = Tint.Of(0xFFFFFF);

        /// <summary>The light of a thrown move: gilt, so it shows on the cream plaques it leaves as well as on the wall.</summary>
        private static readonly Color GiltLight = Tint.Of(0xFFC43D);

        /// <summary>The object every effect's sprite hangs under: a layer of the board's own, with the board's unit and origin.</summary>
        private readonly Transform _layer;

        /// <summary>
        /// Where an armed booster is aimed while the finger is down: a gilt ring under the cell's piece, and for a brush
        /// a stroke of pale light over its row or column. They are no effect of a turn: they hang beside the layer,
        /// which a turn's end clears, and come and go as the finger does.
        /// </summary>
        private readonly Transform _aim;
        private readonly SpriteRenderer _aimRing;
        private readonly SpriteRenderer[] _aimBand = new SpriteRenderer[3];
        private readonly BoardShape _shape;
        private readonly EffectArt _art;
        private readonly ArtCatalogue _catalogue;
        private readonly Timeline _timeline;

        /// <summary>Where the moves medallion hangs, as the board counts; nothing until the effects are placed on a screen.</summary>
        private MedallionPlace? _medallion;

        /// <summary>Until when, on the timeline's clock, the medallion glows too brightly behind a thrown move for the next one to add a glow of its own.</summary>
        private float _medallionGlowsBrightlyUntil;

        /// <param name="board">The board's own object: its unit is a cell, its origin the board's top left corner.</param>
        public BoardEffects(Transform board, BoardShape shape, EffectArt art, ArtCatalogue catalogue, Timeline timeline)
        {
            _layer = new GameObject(LayerName).transform;
            _layer.SetParent(board, worldPositionStays: false);
            _shape = shape;
            _art = art;
            _catalogue = catalogue;
            _timeline = timeline;

            _aim = new GameObject("Aim").transform;
            _aim.SetParent(board, worldPositionStays: false);
            _aimRing = SceneObjects.AddSprite("Aimed cell", _aim, art.Mark, catalogue.SpriteMaterial, DrawOrder.UnderPieces);
            Sprite[] band = { art.StrokeEnd, art.StrokeMiddle, art.StrokeEnd };
            for (int part = 0; part < band.Length; part++)
            {
                _aimBand[part] = SceneObjects.AddSprite("Aimed line", _aim, band[part], catalogue.SpriteMaterial, DrawOrder.EffectLight);
                _aimBand[part].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }

            ShowTheAim(null, null);
        }

        /// <summary>The stroke of light an aimed brush lays over its row or column shows this much of the light: the pieces under it stay plain to see.</summary>
        private const float AimedLineStrength = 0.4f;

        /// <summary>The stroke of an aimed brush is drawn this many cells across: its soft edges then lie on the cells' own.</summary>
        private const float AimedLineAcross = 1.35f;

        /// <summary>
        /// Shows where an armed booster is aimed: the cell the finger is over, ringed in gilt light, and for a brush
        /// a stroke of pale light over all of its row or column, where the brush will paint. Nothing for no booster or no cell.
        /// </summary>
        public void ShowTheAim(BoosterUse? aimedAt, float? clockSeconds)
        {
            Position? cell = aimedAt switch
            {
                BoosterUse.HitCell hitCell => hitCell.At,
                BoosterUse.HitRow row => row.At,
                BoosterUse.HitColumn column => column.At,
                _ => null,
            };
            _aim.gameObject.SetActive(cell != null);
            if (!(cell is Position at))
            {
                return;
            }

            // The ring breathes as the picked piece's does, so an aim looks alive under a still finger.
            float breath = clockSeconds is float seconds ? (Mathf.Sin(seconds * 2f * Mathf.PI / 1.1f) + 1f) / 2f : 1f;
            Put(_aimRing, PieceSprite.MiddleOf(at), 1f);
            _aimRing.color = Faded(WarmLight, Mathf.Lerp(0.85f, 1f, breath));

            bool isAlongTheRow = aimedAt is BoosterUse.HitRow;
            bool hasALine = aimedAt is BoosterUse.HitRow || aimedAt is BoosterUse.HitColumn;
            foreach (SpriteRenderer part in _aimBand)
            {
                part.enabled = hasALine;
                part.color = Faded(WarmLight, AimedLineStrength);
            }

            if (hasALine)
            {
                const float EndLength = 0.5f;
                float length = isAlongTheRow ? _shape.Width : _shape.Height;
                float line = isAlongTheRow ? at.Y + 0.5f : at.X + 0.5f;
                float turn = isAlongTheRow ? 0f : -90f;
                Vector2 PlaceAlong(float along) => isAlongTheRow ? new Vector2(along, line) : new Vector2(line, along);
                Put(_aimBand[0], PlaceAlong(EndLength), 1f, AimedLineAcross, turn);
                Put(_aimBand[1], PlaceAlong(length / 2f), Mathf.Max(0f, length - 2f * EndLength), AimedLineAcross, turn);
                Put(_aimBand[2], PlaceAlong(length - EndLength), 1f, AimedLineAcross, turn + 180f);
            }
        }

        /// <summary>
        /// The tool of a booster used during play at work (<see cref="Cue.ToolUsed"/>): the spatula comes down onto
        /// its cell, strikes it with a blink and a ring of light, and lifts away; the brush comes in over the board's
        /// edge and paints its row or column, a stroke of ink behind it. The hits come by cues of their own.
        /// </summary>
        public void ShowTheTool(Cue.ToolUsed tool)
        {
            switch (tool.Booster)
            {
                case BoosterUse.HitCell cell:
                    StrikeWithTheSpatula(tool, cell.At);
                    break;
                case BoosterUse.HitRow row:
                    PaintWithTheBrush(tool, row.At.Y, isAlongTheRow: true);
                    break;
                case BoosterUse.HitColumn column:
                    PaintWithTheBrush(tool, column.At.X, isAlongTheRow: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tool), tool.Booster, "A booster without a tool to show.");
            }
        }

        /// <summary>
        /// Where the moves medallion hangs on the screen the stage is laid out for; call again whenever the screen
        /// changes. What is on its way from the medallion then goes on from where the medallion now hangs.
        /// </summary>
        public void Place(ScreenLayout layout)
        {
            (float column, float row, float across) = layout.MovesMedallionOnTheBoard;
            _medallion = new MedallionPlace(new Vector2(column, row), across);
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

        /// <summary>A won game's finale opens: light gathers on the wall behind the moves medallion, and a ring of it widens from the medallion's rim.</summary>
        /// <exception cref="InvalidOperationException">The effects were not placed on a screen yet.</exception>
        public void LightTheMedallion(Cue.FinaleOpens opens)
        {
            RequireTheMedallionsPlace();
            GlowBehindTheMedallion(opens.Start, opens.Seconds, strength: 1f);
            Show(opens.Start, opens.Seconds, _art.Ring, DrawOrder.PlaqueRing, isCutAtTheEdge: false, (ring, progress) =>
            {
                MedallionPlace medallion = Medallion;
                Put(ring, medallion.Middle, medallion.Across * Mathf.Lerp(0.9f, 1.5f, Easing.Out(progress)) / RingShareOfItsPicture);
                ring.color = Faded(WarmLight, 0.9f * (1f - Easing.In(progress)));
            });
        }

        /// <summary>
        /// A move left over leaves the medallion for a cell: a star in a mote of gilt light, on an arc over the
        /// wall and the frame, drawing a tail behind it. It needs no light of its own where it lands: the gem that
        /// bursts there and the rocket that pops up are its landing.
        /// </summary>
        /// <exception cref="InvalidOperationException">The effects were not placed on a screen yet.</exception>
        public void Throw(Cue.MoveThrown thrown)
        {
            RequireTheMedallionsPlace();
            Vector2 to = PieceSprite.MiddleOf(thrown.To);
            float side = Scatter(to, 0) < 0.5f ? -1f : 1f;

            // Asked anew in every step: a screen that changes while the move flies moves the medallion.
            Vector2 PlaceAfter(float progress)
            {
                Vector2 from = Medallion.Middle;
                Vector2 way = to - from;

                // Thrown outwards first and coming down onto its gem: the arc turns where the way has gone most
                // of its width and little of its height. A way that leads straight down is given a bulge to a side.
                float outwards = Mathf.Abs(way.x) < MoteLeastBulge ? side * MoteLeastBulge : 0.8f * way.x;
                Vector2 turningPoint = from + new Vector2(outwards, 0.12f * way.y);
                float along = Mathf.Lerp(progress, Easing.In(progress), 0.6f);
                return (1f - along) * (1f - along) * from + 2f * (1f - along) * along * turningPoint + along * along * to;
            }

            float SeenAt(Vector2 place)
            {
                MedallionPlace medallion = Medallion;
                return Easing.Within((place - medallion.Middle).magnitude, MoteUnseenWithin * medallion.Across, MoteSeenFrom * medallion.Across);
            }

            // Every part lies at a place of its own in the draw order: the star over the lights, its core over the star.
            void ShowOnTheWay(ThrownMovePart part, Sprite picture, Color colour, Action<SpriteRenderer, Vector2, float> pose) =>
                Show(thrown.Start, thrown.Seconds, picture, DrawOrder.ThrownMove + (int)part, isCutAtTheEdge: false, (sprite, progress) =>
                {
                    Vector2 place = PlaceAfter(progress);
                    pose(sprite, place, progress);
                    sprite.color = Faded(colour, SeenAt(place));
                });

            LetTheMedallionGlowFor(thrown);
            ShowOnTheWay(ThrownMovePart.Tail, _art.Streak, GiltLight, (tail, head, progress) =>
            {
                Vector2 drawn = OutsideTheMedallion(PlaceAfter(Mathf.Max(0f, progress - MoteTailOfTheFlight)), head);
                float turn = Mathf.Atan2(-drawn.y, drawn.x) * Mathf.Rad2Deg;
                Put(tail, head - drawn / 2f, drawn.magnitude + MoteTailWidth, MoteTailWidth / StreakWidthOfItsPicture, turn);
            });
            ShowOnTheWay(ThrownMovePart.GiltLight, _art.Glow, GiltLight, (glow, place, progress) => Put(glow, place, MoteGlow));
            ShowOnTheWay(ThrownMovePart.PaleLight, _art.Glow, WarmLight, (glow, place, progress) => Put(glow, place, MoteInnerGlow));
            foreach (float ray in new[] { 0f, 90f })
            {
                ShowOnTheWay(ThrownMovePart.Star, _art.Streak, Light, (star, place, progress) => Put(star, place, MoteStar, 1f, ray + MoteStarTurns * progress));
            }

            ShowOnTheWay(ThrownMovePart.Core, _art.Dot, Light, (core, place, progress) => Put(core, place, MoteCore));
        }

        /// <summary>
        /// The medallion glows behind a move that leaves it, unless it still glows brightly behind the one before:
        /// many moves that leave in quick succession keep one glow up, where a glow each would pile up to a white
        /// blot on the wall.
        /// </summary>
        private void LetTheMedallionGlowFor(Cue.MoveThrown thrown)
        {
            float leaves = _timeline.Now + thrown.Start;
            if (leaves < _medallionGlowsBrightlyUntil)
            {
                return;
            }

            _medallionGlowsBrightlyUntil = leaves + HaloSeconds / 3f;
            GlowBehindTheMedallion(thrown.Start, HaloSeconds, strength: 0.7f);
        }

        /// <summary>
        /// The straight way from a place to a thrown move's head, less the part of it that lies over the medallion:
        /// the tail a move draws begins at the medallion's rim, and the number stays readable under any count of
        /// moves that leave together. Nothing is left of the way while the head itself is still over the medallion.
        /// </summary>
        private Vector2 OutsideTheMedallion(Vector2 from, Vector2 head)
        {
            MedallionPlace medallion = Medallion;
            float rim = MoteSeenFrom * medallion.Across;
            Vector2 way = head - from;
            Vector2 fromTheMiddle = from - medallion.Middle;
            if (fromTheMiddle.magnitude >= rim || way.sqrMagnitude <= 0f)
            {
                return way;
            }

            // Where the way crosses the rim, as the part of the way gone by then: the root of
            // |fromTheMiddle + part * way| = rim that lies ahead. The place it starts from is inside the rim, so there is one.
            float twiceAlong = 2f * Vector2.Dot(fromTheMiddle, way);
            float inside = fromTheMiddle.sqrMagnitude - rim * rim;
            float part = (-twiceAlong + Mathf.Sqrt(twiceAlong * twiceAlong - 4f * way.sqrMagnitude * inside)) / (2f * way.sqrMagnitude);
            return way * (1f - Mathf.Clamp01(part));
        }

        /// <summary>Light on the wall behind the moves medallion that swells and fades: the medallion glows as it gives a move away.</summary>
        private void GlowBehindTheMedallion(float startsIn, float seconds, float strength)
        {
            Show(startsIn, seconds, _art.Glow, DrawOrder.PlaqueHalo, isCutAtTheEdge: false, (halo, progress) =>
            {
                MedallionPlace medallion = Medallion;
                Put(halo, medallion.Middle, medallion.Across * Mathf.Lerp(2.2f, 2.9f, Easing.Out(progress)));
                halo.color = Faded(WarmLight, strength * (1f - Easing.In(progress)));
            });
        }

        /// <exception cref="InvalidOperationException">The effects were not placed on a screen yet.</exception>
        private MedallionPlace Medallion =>
            _medallion ?? throw new InvalidOperationException("The effects do not know where the moves medallion hangs; place them on a screen first.");

        /// <summary>
        /// Where the medallion hangs is asked anew in every step of what leaves it, since a screen that changes
        /// moves it. Whether that is known at all is asked here, when the effect is put on the timeline: whoever
        /// stages a finale can still do without its picture then. An effect that only found out in its first
        /// step would throw out of the stage's clock, one sprite a frame, where nobody is left to catch it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The effects were not placed on a screen yet.</exception>
        private void RequireTheMedallionsPlace() => _ = Medallion;

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

        /// <summary>
        /// The spatula, as large as one and a half cells, comes down from over the cell's upper right with its blade
        /// first, strikes, and lifts away the way it came, fading. Its blade is the lower left of its picture.
        /// </summary>
        private void StrikeWithTheSpatula(Cue.ToolUsed tool, Position cell)
        {
            const float Across = 1.5f;
            Vector2 at = PieceSprite.MiddleOf(cell);
            // The blade's tip lies this far down and to the left of the picture's middle: it strikes the cell's middle.
            Vector2 resting = at + new Vector2(0.44f, -0.46f);
            Vector2 high = resting + new Vector2(0.9f, -1.1f);
            Sprite spatula = _catalogue.Of(GalleryArt.IconSpatula);
            Show(tool.Start, tool.Seconds, spatula, DrawOrder.Flight, isCutAtTheEdge: false, (sprite, progress) =>
            {
                float elapsed = progress * tool.Seconds;
                float coming = Easing.In(Easing.Within(elapsed, 0f, tool.StrikesAt));
                float going = Easing.Out(Easing.Within(elapsed, tool.StrikesAt + 0.06f, tool.Seconds));
                Vector2 place = Vector2.Lerp(Vector2.Lerp(high, resting, coming), high, going);
                float squash = 1f - 0.12f * Easing.Arch(Easing.Within(elapsed, tool.StrikesAt - 0.02f, tool.StrikesAt + 0.08f));
                Put(sprite, place, Across * squash / 100f, Across / squash / 100f, 12f * (1f - coming) + 18f * going);
                sprite.color = Faded(Light, Easing.Within(elapsed, 0f, 0.06f) * (1f - going));
            });
            Flash(tool.StrikesAt, at, 1.7f, WarmLight, 0.85f);
            Wave(tool.StrikesAt, at, 2.2f);
        }

        /// <summary>
        /// The brush, lying for a row and standing for a column, comes in over the board's edge and travels to the
        /// other, a stroke of ink growing behind it that fades once it is through. It reaches the middle of the first
        /// cell when the strike begins and every next cell one step later, as the hits come.
        /// </summary>
        private void PaintWithTheBrush(Cue.ToolUsed tool, int line, bool isAlongTheRow)
        {
            const float EndLength = 0.5f;
            float length = isAlongTheRow ? _shape.Width : _shape.Height;
            float across = line + 0.5f;
            float turn = isAlongTheRow ? 0f : -90f;
            float throughAt = tool.StrikesAt + (length - 1f) * Tempo.BrushPerCell;
            Vector2 PlaceAlong(float along) => isAlongTheRow ? new Vector2(along, across) : new Vector2(across, along);
            float HeadAfter(float elapsed) => 0.5f + (elapsed - tool.StrikesAt) / Tempo.BrushPerCell;
            Color InkAfter(float elapsed) => Faded(Light, 1f - Easing.In(Easing.Within(elapsed, throughAt, tool.Seconds)));

            float tail = -OverTheEdge;

            // No ink before the brush has come over the edge far enough for a stroke with its two round ends.
            void ShowPart(Sprite part, Action<SpriteRenderer, float> pose) =>
                Show(tool.Start, tool.Seconds, part, DrawOrder.Effect, isCutAtTheEdge: true, (sprite, progress) =>
                {
                    float elapsed = progress * tool.Seconds;
                    float head = Mathf.Min(length + OverTheEdge, HeadAfter(elapsed));
                    pose(sprite, head);
                    sprite.color = head < tail + 2f * EndLength ? Faded(Light, 0f) : InkAfter(elapsed);
                });

            ShowPart(_art.StrokeEnd, (end, head) => Put(end, PlaceAlong(tail + EndLength), 1f, 1f, turn));
            ShowPart(_art.StrokeMiddle, (body, head) => Put(body, PlaceAlong((tail + head) / 2f), Mathf.Max(0f, head - tail - 2f * EndLength), 1f, turn));
            ShowPart(_art.StrokeEnd, (end, head) => Put(end, PlaceAlong(Mathf.Max(tail + 2f * EndLength, head) - EndLength), 1f, 1f, turn + 180f));

            Sprite brush = _catalogue.Of(isAlongTheRow ? PieceArt.RocketHorizontal : PieceArt.RocketVertical);
            Show(tool.Start, throughAt + 2f * Tempo.BrushPerCell - tool.Start, brush, DrawOrder.EffectPiece, isCutAtTheEdge: true, (head, progress) =>
            {
                float elapsed = progress * (throughAt + 2f * Tempo.BrushPerCell - tool.Start);
                Put(head, PlaceAlong(HeadAfter(elapsed)), BoardView.PieceScale);
            });
        }

        /// <summary>A bomb's blot of ink: it spreads fast, stands for a moment and fades.</summary>
        private void Splash(Cue.Effect effect)
        {
            Vector2 origin = PieceSprite.MiddleOf(effect.Origin);
            float across = 2f * effect.Radius + 2f;
            // The flash lies over the blot: at full strength it washed the ink out to a pale cloud for as long as it lasted.
            Flash(effect.Start, origin, across, Light, 0.5f);
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
        /// Each gets a sprite of its own, which is not kept for the next one: a sprite that is never used again
        /// carries nothing over, and the few dozen short-lived objects of a turn have not been measured to matter.
        /// A won game's finale is beyond that premise. A move thrown is six sprites and its landing eight, a
        /// rocket's stroke five and every piece it bursts seven, so thirty moves left make well over a thousand
        /// objects in about three seconds. What that costs on a phone has not been measured; if a finale drops
        /// frames there, this is where the sprites are to be kept and used again.
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

        /// <summary>The moves medallion as the board counts: the place of its middle in cells, and how many cells it is across.</summary>
        private readonly struct MedallionPlace
        {
            public MedallionPlace(Vector2 middle, float across)
            {
                Middle = middle;
                Across = across;
            }

            public Vector2 Middle { get; }

            public float Across { get; }
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

        /// <summary>
        /// A gem's chips have the main colour of its piece in the rich palette the pieces are exported in: each is
        /// what tools/web-theme/gem-palette.js makes of the colour the piece was first drawn with.
        /// </summary>
        private static Color ChipColourOf(GemColor colour) => colour switch
        {
            GemColor.Red => Tint.Of(0xEF0125),
            GemColor.Green => Tint.Of(0x15B60B),
            GemColor.Blue => Tint.Of(0x0384F8),
            GemColor.Yellow => Tint.Of(0xF8C707),
            GemColor.Purple => Tint.Of(0x984AF0),
            GemColor.Orange => Tint.Of(0xF4850E),
            GemColor.Cyan => Tint.Of(0x15C5E4),
            GemColor.Pink => Tint.Of(0xFD5BAB),
            _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A gem colour without a chip colour."),
        };
    }
}
