#nullable enable
using System;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// How far the world map's strip of twenty eras is scrolled (MapWorldTR): a row of medallions wider than the
    /// sheet, which a finger drags sideways and flicks, and which the map scrolls by itself to bring an era into view.
    /// Dragged past either end it gives only a little way, and lets go back to the end; a flick runs on a while
    /// after the finger lifts. Places are in points; times in seconds of the page's clock. No Unity dependency.
    /// </summary>
    public sealed class EraStripScroll
    {
        /// <summary>The design's strip: 8 points in from each end, medallions 58 wide, 4 apart.</summary>
        public const float EndRoom = 8f;
        public const float MedalWidth = 58f;
        public const float MedalGap = 4f;
        public const float Pitch = MedalWidth + MedalGap;

        /// <summary>An era brought into view stands this far in from the strip's left edge, as the design scrolls it ((era - 1) × 62 - 120).</summary>
        private const float ShownFromTheLeft = 120f;

        /// <summary>Dragged past an end, the strip follows the finger at this share, never further than <see cref="MostStretch"/>.</summary>
        private const float Resistance = 0.35f;
        private const float MostStretch = 40f;

        /// <summary>A flick runs on as far as its speed would carry it in this long.</summary>
        private const float RunOnSeconds = 0.3f;

        /// <summary>The speed of a flick is read over the finger's last moves within this long.</summary>
        private const float SpeedWindow = 0.1f;

        private readonly int _eras;
        private float _viewWidth;
        private (float FingerX, float Offset)? _held;
        private (float X, float Time) _lastMove;
        private float _speed;
        private (float From, float To, float Begins, float Duration)? _settling;

        public EraStripScroll(int eras, float viewWidth)
        {
            if (eras < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(eras), eras, "A strip of eras has at least one.");
            }

            _eras = eras;
            _viewWidth = viewWidth;
        }

        /// <summary>How far the strip is scrolled at rest: its left edge lies this many points left of the view's.</summary>
        public float Offset { get; private set; }

        public float ContentWidth => 2f * EndRoom + _eras * MedalWidth + (_eras - 1) * MedalGap;

        public float Furthest => Math.Max(0f, ContentWidth - _viewWidth);

        /// <summary>True while a finger holds the strip.</summary>
        public bool IsHeld => _held.HasValue;

        /// <summary>The left edge of an era's medallion in the strip, from the strip's own left edge.</summary>
        public static float LeftOf(int era) => EndRoom + (era - 1) * Pitch;

        /// <summary>The view changed width: the strip stays where it was, within its new ends.</summary>
        public void Fit(float viewWidth)
        {
            _viewWidth = viewWidth;
            Offset = Clamp(Offset);
            _settling = null;
        }

        /// <summary>Where the strip shows an era: a little in from the left, as far as its ends allow.</summary>
        public float OffsetShowing(int era) => Clamp((era - 1) * Pitch - ShownFromTheLeft);

        /// <summary>The strip scrolls to show an era, from where it is now, over a time.</summary>
        public void ScrollTo(int era, float now, float duration)
        {
            float from = At(now);
            _held = null;
            _settling = (from, OffsetShowing(era), now, duration);
            Offset = OffsetShowing(era);
        }

        /// <summary>A finger takes the strip at a place: it stops where it is.</summary>
        public void Hold(float fingerX, float now)
        {
            float at = At(now);
            _settling = null;
            Offset = at;
            _held = (fingerX, at);
            _lastMove = (fingerX, now);
            _speed = 0f;
        }

        /// <summary>The finger that holds the strip moved: the strip follows it, giving only a little past its ends.</summary>
        public void DragTo(float fingerX, float now)
        {
            if (!_held.HasValue)
            {
                return;
            }

            (float FingerX, float Offset) held = _held.Value;
            float elapsed = now - _lastMove.Time;
            if (elapsed > 0f)
            {
                float speed = -(fingerX - _lastMove.X) / elapsed;
                float weight = Math.Min(1f, elapsed / SpeedWindow);
                _speed = _speed + (speed - _speed) * weight;
                _lastMove = (fingerX, now);
            }

            float wanted = held.Offset - (fingerX - held.FingerX);
            Offset = Stretched(wanted);
        }

        /// <summary>The finger let go: the strip runs on with the flick's speed, within its ends, or goes back to the end it was pulled past.</summary>
        public void LetGo(float now, float duration)
        {
            if (!_held.HasValue)
            {
                return;
            }

            _held = null;
            bool isStill = now - _lastMove.Time > SpeedWindow;
            float to = Clamp(Offset + (isStill ? 0f : _speed * RunOnSeconds));
            _settling = (Offset, to, now, duration);
            Offset = to;
        }

        /// <summary>The finger's touch ended without a lift the game saw: the strip stands within its ends at once.</summary>
        public void Forget()
        {
            _held = null;
            _settling = null;
            Offset = Clamp(Offset);
        }

        /// <summary>How far the strip is scrolled at a moment: on its way while it settles, eased out as it ends.</summary>
        public float At(float now)
        {
            if (!IsMovingAt(now) || !_settling.HasValue)
            {
                return Offset;
            }

            (float From, float To, float Begins, float Duration) settling = _settling.Value;
            float share = Math.Max(0f, now - settling.Begins) / settling.Duration;
            float eased = 1f - (1f - share) * (1f - share);
            return settling.From + (settling.To - settling.From) * eased;
        }

        /// <summary>True while the strip is still on its way somewhere.</summary>
        public bool IsMovingAt(float now) => _settling.HasValue && _settling.Value.Duration > 0f && now < _settling.Value.Begins + _settling.Value.Duration;

        private float Clamp(float offset) => Math.Min(Math.Max(offset, 0f), Furthest);

        private float Stretched(float wanted)
        {
            float within = Clamp(wanted);
            float past = wanted - within;
            return within + Math.Sign(past) * Math.Min(Math.Abs(past) * Resistance, MostStretch);
        }
    }
}
