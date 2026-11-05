using System;
using System.Collections.Generic;

namespace Puzzle.Client.Stage
{
    /// <summary>Curves that turn the run of a tween, 0..1, into how far its change has come.</summary>
    public static class Easing
    {
        /// <summary>Shoots a tenth past the end and settles back: a piece that pops into place.</summary>
        public static float Overshoot(float progress)
        {
            const float Tension = 1.70158f;
            float fromEnd = progress - 1f;
            return 1f + fromEnd * fromEnd * ((Tension + 1f) * fromEnd + Tension);
        }

        /// <summary>Comes in fast, gives a fortieth past the end and settles back: something heavy that is let down on its wires.</summary>
        public static float Settle(float progress)
        {
            const float Tension = 0.8f;
            float fromEnd = progress - 1f;
            return 1f + fromEnd * fromEnd * ((Tension + 1f) * fromEnd + Tension);
        }

        /// <summary>Starts slowly and ends slowly: a piece that is moved by hand from one cell to the next.</summary>
        public static float InOut(float progress) => progress * progress * (3f - 2f * progress);

        /// <summary>Starts slowly and keeps gaining: something that is drawn in or drops away.</summary>
        public static float In(float progress) => progress * progress;

        /// <summary>Starts fast and comes to rest: something that was thrown.</summary>
        public static float Out(float progress) => 1f - (1f - progress) * (1f - progress);

        /// <summary>From 0 up to 1 at the half and back to 0: a swell, a squeeze, a nudge that returns.</summary>
        public static float Arch(float progress) => (float)Math.Sin(Math.PI * progress);

        /// <summary>How far along a stretch of the run a moment is: 0 before the stretch, 1 after it.</summary>
        public static float Within(float progress, float from, float until) =>
            Math.Clamp((progress - from) / (until - from), 0f, 1f);
    }

    /// <summary>
    /// One change over stage time: it waits, then runs for a while, and tells its target how far it has come. Time
    /// only ever reaches it through <see cref="Advance"/>, so a tween is where the stage's clock says it is, whether
    /// frames or a screenshot command move that clock.
    /// </summary>
    public sealed class Tween
    {
        private readonly float _delaySeconds;
        private readonly float _durationSeconds;
        private readonly Func<float, float> _easing;
        private readonly Action<float> _apply;
        private float _elapsedSeconds;

        /// <param name="apply">Receives the eased progress; called once at once, so the target starts where the tween does.</param>
        /// <exception cref="ArgumentOutOfRangeException">The delay is negative or the duration is not positive.</exception>
        public Tween(float delaySeconds, float durationSeconds, Func<float, float> easing, Action<float> apply)
        {
            if (delaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "A tween cannot start in the past.");
            }

            if (durationSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "A tween needs time to run.");
            }

            _delaySeconds = delaySeconds;
            _durationSeconds = durationSeconds;
            _easing = easing;
            _apply = apply;
            _apply(_easing(0f));
        }

        public bool IsFinished => _elapsedSeconds >= _delaySeconds + _durationSeconds;

        public void Advance(float deltaSeconds)
        {
            _elapsedSeconds += deltaSeconds;
            float progress = Math.Clamp((_elapsedSeconds - _delaySeconds) / _durationSeconds, 0f, 1f);
            _apply(_easing(progress));
        }
    }

    /// <summary>The tweens that are running on a stage.</summary>
    public sealed class TweenSet
    {
        private readonly List<Tween> _running = new List<Tween>();

        public bool IsIdle => _running.Count == 0;

        public void Start(Tween tween) => _running.Add(tween);

        /// <exception cref="ArgumentOutOfRangeException">Time was asked to run backwards.</exception>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "The stage's clock only runs forwards.");
            }

            foreach (Tween tween in _running)
            {
                tween.Advance(deltaSeconds);
            }

            _running.RemoveAll(tween => tween.IsFinished);
        }
    }
}
