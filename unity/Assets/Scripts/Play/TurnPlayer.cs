#nullable enable
using System;
using Puzzle.Client.Boards;
using Puzzle.Client.Stage;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Play
{
    /// <summary>
    /// Plays a turn's script on the board: every cue becomes an act on the stage's timeline that moves, sizes, fades
    /// or changes what the cue names. When and in what order things happen was decided when the script was written
    /// (<see cref="TurnChoreographer"/>); here is only how each of them looks. The finale of a won game is a script
    /// of the same kind and is played the same way.
    /// </summary>
    /// <remarks>
    /// Pieces that come and go are settled with the board at once: a piece that will enter is added now and kept out
    /// of sight until its moment, a piece that will leave is taken from the board now and goes out of sight for
    /// good when its leaving is over. So from the first moment the board's own list of pieces is the list after the
    /// turn, and the acts only hold the sprites they move. Acts on one piece touch different things of it (its
    /// place, its size, its fading) or follow one another; where two meet in a step, the timeline lets the later
    /// one win. The sprite of a piece that has gone stays until the turn is over (BoardView.ClearAwayWhatWasTaken),
    /// so an act that still holds it poses a sprite nobody sees and never one that is no longer there.
    /// </remarks>
    public sealed class TurnPlayer
    {
        /// <summary>A landed piece is squeezed by this part of its height, grows by this part of its width, and stands up again.</summary>
        private const float LandingSqueeze = 0.14f;
        private const float LandingSpread = 0.1f;

        /// <summary>A collected item drops this many cells, out through the tray's edge.</summary>
        private const float DropOutCells = 1.3f;

        /// <summary>The gems of a reshuffle turn over one diagonal after another, this long apart.</summary>
        private const float ReshuffleSecondsPerDiagonal = 0.012f;

        private readonly BoardView _board;
        private readonly BoardEffects _effects;
        private readonly Timeline _timeline;
        private readonly Action _whenAMoveIsSpent;
        private readonly Action<int> _whenAGoalCounts;
        private readonly Action _whenAMoveIsThrown;

        /// <param name="whenAMoveIsSpent">Called at the moment of the turn at which the move is counted.</param>
        /// <param name="whenAGoalCounts">Called with a goal's place in the level's list whenever one more of it is done.</param>
        /// <param name="whenAMoveIsThrown">Called in a won game's finale whenever a move left over leaves the medallion for the board.</param>
        public TurnPlayer(BoardView board, BoardEffects effects, Timeline timeline, Action whenAMoveIsSpent, Action<int> whenAGoalCounts, Action whenAMoveIsThrown)
        {
            _board = board;
            _effects = effects;
            _timeline = timeline;
            _whenAMoveIsSpent = whenAMoveIsSpent;
            _whenAGoalCounts = whenAGoalCounts;
            _whenAMoveIsThrown = whenAMoveIsThrown;
        }

        /// <summary>Puts a whole turn, or a won game's finale, on the timeline, beginning now.</summary>
        /// <exception cref="InvalidOperationException">A cue names a piece the board does not show.</exception>
        public void Play(TurnScript script)
        {
            foreach (Cue cue in script.Cues)
            {
                Stage(cue);
            }
        }

        private void Stage(Cue cue)
        {
            switch (cue)
            {
                case Cue.MoveSpent _:
                    _timeline.At(cue.Start, _whenAMoveIsSpent);
                    break;
                case Cue.GoalTick tick:
                    _timeline.At(tick.Start, () => _whenAGoalCounts(tick.GoalIndex));
                    break;
                case Cue.Slide slide:
                    Slide(slide);
                    break;
                case Cue.Fall fall:
                    Fall(_board.Piece(fall.PieceId), fall, fall.From.Y, fall.To, fall.LandingSeconds);
                    break;
                case Cue.Enter enter:
                    Fall(Hidden(_board.AddPiece(enter.PieceId, enter.Tile, enter.To)), enter, enter.FromRow, enter.To, enter.LandingSeconds);
                    break;
                case Cue.Slip slip:
                    Slip(slip);
                    break;
                case Cue.Appear appear:
                    Appear(appear);
                    break;
                case Cue.Leave leave:
                    Leave(leave);
                    break;
                case Cue.Reface reface:
                    Reface(reface);
                    break;
                case Cue.TapeHit tape:
                    Peel(tape, () => _board.TakeTape(tape.At), () => _board.LayTape(tape.At, tape.HitsLeft), growsTo: 1.25f);
                    break;
                case Cue.SlabHit slab:
                    Peel(slab, () => _board.TakeSlab(slab.At), () => _board.LaySlab(slab.At, slab.LayersLeft), growsTo: 1.18f);
                    break;
                case Cue.Effect effect:
                    _effects.Play(effect);
                    break;
                case Cue.Flight flight:
                    _effects.Fly(flight);
                    break;
                case Cue.Zap zap:
                    _effects.Ray(zap);
                    break;
                case Cue.Reshuffle reshuffle:
                    Reshuffle(reshuffle);
                    break;
                case Cue.FinaleOpens opens:
                    _effects.LightTheMedallion(opens);
                    break;
                case Cue.MoveThrown thrown:
                    _timeline.At(thrown.Start, _whenAMoveIsThrown);
                    _effects.Throw(thrown);
                    break;
                default:
                    throw new InvalidOperationException($"The view cannot play a {cue.GetType().Name}.");
            }
        }

        private static PieceSprite Hidden(PieceSprite piece)
        {
            piece.IsSeen = false;
            return piece;
        }

        // ---------- travels ----------

        private void Slide(Cue.Slide slide)
        {
            PieceSprite piece = _board.Piece(slide.PieceId);
            Vector2 from = PieceSprite.MiddleOf(slide.From);
            Vector2 to = PieceSprite.MiddleOf(slide.To);
            bool isCarried = slide.Manner != SlideManner.Displaced;
            float swell = isCarried ? 0.1f : -0.06f;
            _timeline.Add(
                slide.Start,
                slide.Seconds,
                progress =>
                {
                    piece.Lift(isCarried);
                    piece.Place = Vector2.Lerp(from, to, Easing.InOut(progress));
                    float size = 1f + swell * Easing.Arch(progress);
                    piece.Size(size, size);
                },
                () =>
                {
                    // A special piece pushed onto another stays on top of it: both go off from there in the same moment.
                    piece.Lift(slide.Manner == SlideManner.Onto);
                    piece.ArriveIn(slide.To);
                    piece.Size(1f, 1f);
                });
        }

        /// <summary>A piece falls down its column from a row (which may lie above the board) into a cell, by the law of falling.</summary>
        private void Fall(PieceSprite piece, Cue travel, int fromRow, Position to, float landingSeconds)
        {
            Vector2 from = PieceSprite.MiddleOf(new Position(to.X, fromRow));
            float distance = to.Y - fromRow;
            _timeline.Add(
                travel.Start,
                travel.Seconds,
                progress =>
                {
                    piece.IsSeen = true;
                    piece.Place = from + new Vector2(0f, Mathf.Min(distance, FallLaw.CellsAfter(progress * travel.Seconds)));
                },
                () => piece.ArriveIn(to));
            if (landingSeconds > 0f)
            {
                Land(piece, to, travel.End, landingSeconds);
            }
        }

        private void Slip(Cue.Slip slip)
        {
            PieceSprite piece = _board.Piece(slip.PieceId);
            Vector2 from = PieceSprite.MiddleOf(slip.From);
            Vector2 to = PieceSprite.MiddleOf(slip.To);
            _timeline.Add(
                slip.Start,
                slip.Seconds,
                progress => piece.Place = Vector2.Lerp(from, to, Easing.InOut(progress)),
                () => piece.ArriveIn(slip.To));
            if (slip.Lands)
            {
                Land(piece, slip.To, slip.End, slip.LandingSeconds);
            }
        }

        /// <summary>
        /// The piece gives under its own weight where it lands and stands up again, its foot staying where it is.
        /// A landing of less than <see cref="Tempo.Land"/> is the beginning of a whole one: the piece falls on or is
        /// hit before it stands again, and whatever moves it on or takes it from the board goes on from there.
        /// </summary>
        private void Land(PieceSprite piece, Position cell, float startsIn, float seconds)
        {
            Vector2 middle = PieceSprite.MiddleOf(cell);
            _timeline.Add(
                startsIn,
                seconds,
                shown =>
                {
                    float progress = shown * seconds / Tempo.Land;
                    float give = Easing.Arch(progress) * (1f - 0.5f * progress);
                    float height = 1f - LandingSqueeze * give;
                    piece.Size(1f + LandingSpread * give, height);
                    piece.Place = middle + new Vector2(0f, BoardView.PieceScale * (1f - height) / 2f);
                },
                () =>
                {
                    piece.Size(1f, 1f);
                    piece.Place = middle;
                });
        }

        // ---------- coming and going ----------

        private void Appear(Cue.Appear appear)
        {
            PieceSprite piece = Hidden(_board.AddPiece(appear.PieceId, appear.Tile, appear.At));
            _effects.Flash(appear.Start, PieceSprite.MiddleOf(appear.At), 2.2f, BoardEffects.WarmLight, 0.9f);
            _timeline.Add(
                appear.Start,
                appear.Seconds,
                progress =>
                {
                    // Up from a fifth to a little over its size, then back to it: the web test page's "pop".
                    float size = progress < 0.65f
                        ? Mathf.Lerp(0.2f, 1.18f, Easing.Out(progress / 0.65f))
                        : Mathf.Lerp(1.18f, 1f, Easing.InOut(Easing.Within(progress, 0.65f, 1f)));
                    piece.IsSeen = true;
                    piece.Size(size, size);
                });
        }

        private void Leave(Cue.Leave leave)
        {
            PieceSprite piece = _board.TakePiece(leave.PieceId);
            switch (leave.Manner)
            {
                case LeaveManner.Pop:
                case LeaveManner.Break:
                    _effects.Burst(leave.Start, PieceSprite.MiddleOf(leave.At), leave.Tile);
                    _timeline.Add(leave.Start, leave.Seconds, progress => PoseBursting(piece, progress), piece.GoForGood);
                    break;
                case LeaveManner.Gather:
                    Gather(piece, leave);
                    break;
                case LeaveManner.Fire:
                    _timeline.Add(leave.Start, leave.Seconds, progress => PoseGoingOff(piece, progress), piece.GoForGood);
                    break;
                case LeaveManner.Collect:
                    DropOut(piece, leave);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(leave), leave.Manner, "A way of leaving the board without a picture.");
            }
        }

        /// <summary>Swells a little, then shrinks to nothing and fades: the web test page's "vanish".</summary>
        private static void PoseBursting(PieceSprite piece, float progress)
        {
            float shrinking = Easing.Within(progress, 0.35f, 1f);
            float size = progress < 0.35f ? Mathf.Lerp(1f, 1.12f, progress / 0.35f) : Mathf.Lerp(1.12f, 0.15f, shrinking);
            piece.Size(size, size);
            piece.Alpha = 1f - Easing.In(shrinking);
        }

        /// <summary>A special piece flares up as it goes off: larger and gone in a blink.</summary>
        private static void PoseGoingOff(PieceSprite piece, float progress)
        {
            float size = Mathf.Lerp(1f, 1.45f, Easing.Out(progress));
            piece.Size(size, size);
            piece.Alpha = 1f - Easing.In(progress);
        }

        private void Gather(PieceSprite piece, Cue.Leave leave)
        {
            Vector2 from = PieceSprite.MiddleOf(leave.At);
            Vector2 toward = PieceSprite.MiddleOf(leave.Toward);
            _timeline.Add(
                leave.Start,
                leave.Seconds,
                progress =>
                {
                    piece.Lift(true);
                    piece.Place = Vector2.Lerp(from, toward, Easing.In(progress));
                    float size = Mathf.Lerp(1f, 0.6f, progress);
                    piece.Size(size, size);
                    piece.Alpha = 1f - Easing.Within(progress, 0.7f, 1f);
                },
                piece.GoForGood);
        }

        private void DropOut(PieceSprite piece, Cue.Leave leave)
        {
            Vector2 from = PieceSprite.MiddleOf(leave.At);
            _timeline.Add(
                leave.Start,
                leave.Seconds,
                progress => piece.Place = from + new Vector2(0f, DropOutCells * Easing.In(progress)),
                piece.GoForGood);

            if (_board.ExitUnder(leave.At) is Transform exit)
            {
                Vector3 atRest = exit.localScale;
                _timeline.Add(leave.Start + leave.Seconds / 2f, Tempo.Bump, progress => exit.localScale = atRest * (1f + 0.3f * Easing.Arch(progress)), () => exit.localScale = atRest);
                _effects.Flash(leave.End, from + new Vector2(0f, 0.9f), 1.8f, BoardEffects.WarmLight, 0.9f);
            }
        }

        // ---------- hits that leave the piece ----------

        private void Reface(Cue.Reface reface)
        {
            PieceSprite piece = _board.Piece(reface.PieceId);
            bool isShown = false;
            _effects.Burst(reface.Start, PieceSprite.MiddleOf(reface.At), reface.Tile);
            _timeline.Add(
                reface.Start,
                reface.Seconds,
                progress =>
                {
                    if (!isShown)
                    {
                        piece.Show(reface.Tile);
                        isShown = true;
                    }

                    // Gives under the hit and comes back: the web test page's "bump".
                    float give = Easing.Arch(progress);
                    piece.Size(1f - 0.1f * give, 1f - 0.1f * give);
                    piece.Turn = -3f * give;
                },
                () =>
                {
                    piece.Size(1f, 1f);
                    piece.Turn = 0f;
                });
        }

        /// <summary>
        /// A tape or a slab takes a hit: what lay there grows a little and fades, and what is left of it lies under
        /// it from the first moment on.
        /// </summary>
        /// <param name="take">Takes the hit layer's sprite from the board, at the moment of the hit; nothing when the cell has none.</param>
        /// <param name="layWhatIsLeft">Lays what the hit leaves, at the same moment.</param>
        private void Peel(Cue hit, Func<SpriteRenderer?> take, Action layWhatIsLeft, float growsTo)
        {
            SpriteRenderer? peeled = null;
            Vector3 atRest = Vector3.one;
            bool isTaken = false;
            _timeline.Add(
                hit.Start,
                hit.Seconds,
                progress =>
                {
                    if (!isTaken)
                    {
                        isTaken = true;
                        peeled = take();
                        layWhatIsLeft();
                        atRest = peeled != null ? peeled.transform.localScale : atRest;
                    }

                    if (peeled != null)
                    {
                        peeled.transform.localScale = atRest * Mathf.Lerp(1f, growsTo, Easing.Out(progress));
                        peeled.color = new Color(1f, 1f, 1f, 1f - progress);
                    }
                },
                () =>
                {
                    if (peeled != null)
                    {
                        peeled.enabled = false; // out of sight; the board takes its sprite off the stage with the turn's other leavings
                    }
                });
        }

        /// <summary>Every dealt gem turns away to nothing and comes back as what it is now, in a wave across the board.</summary>
        private void Reshuffle(Cue.Reshuffle reshuffle)
        {
            foreach (PlacedPiece dealt in reshuffle.Dealt)
            {
                PieceSprite piece = _board.Piece(dealt.Id);
                Tile tile = dealt.Tile;
                float wait = (dealt.Cell.X + dealt.Cell.Y) * ReshuffleSecondsPerDiagonal;
                bool isDealt = false;
                _timeline.Add(
                    reshuffle.Start + wait,
                    reshuffle.Seconds - wait,
                    progress =>
                    {
                        if (progress >= 0.5f && !isDealt)
                        {
                            piece.Show(tile);
                            isDealt = true;
                        }

                        float size = Mathf.Abs(Mathf.Cos(progress * Mathf.PI));
                        piece.Size(size, size);
                        piece.Turn = 360f * Easing.InOut(progress);
                    },
                    () =>
                    {
                        piece.Size(1f, 1f);
                        piece.Turn = 0f;
                    });
            }
        }
    }
}
