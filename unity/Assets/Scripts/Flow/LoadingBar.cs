#nullable enable
using System;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// How full the entry page's bar is shown while the game starts. It follows the work done
    /// (<see cref="StartUpWork.Done"/>) but never runs ahead of it, never goes back, and never fills faster than
    /// <see cref="ShortestFillSeconds"/> takes for the whole bar: however fast a phone starts, the bar is seen to
    /// fill, never as a flash, and a step that holds a frame up does not make it jump. No Unity dependency.
    /// </summary>
    public sealed class LoadingBar
    {
        /// <summary>The whole bar takes at least this long to fill, as long as a player reads its line in.</summary>
        public const float ShortestFillSeconds = 1.2f;

        /// <summary>How full the bar is shown, from 0 to 1.</summary>
        public float Shown { get; private set; }

        /// <summary>True once the bar is shown full: the work is done and the bar has caught up with it.</summary>
        public bool IsFull => Shown >= 1f;

        /// <summary>The whole hundredths shown, as its line says them: 100 only once the bar is full.</summary>
        public int Percent => IsFull ? 100 : Math.Min(99, (int)Math.Floor(Shown * 100f + RoundingSlack));

        /// <summary>A share such as 0.45 is a hair under 45 hundredths in floating point; this much more is still the same whole hundredth.</summary>
        private const float RoundingSlack = 0.001f;

        /// <summary>Moves the bar on by a step of time towards the work done.</summary>
        /// <param name="workDone">How much of the work is done, from 0 to 1; more or less is taken as the nearest of the two.</param>
        /// <exception cref="ArgumentOutOfRangeException">The step of time is less than nothing, or either is no number.</exception>
        public void Advance(float deltaSeconds, float workDone)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), $"A step of {deltaSeconds} s is no step of time.");
            }

            if (float.IsNaN(workDone))
            {
                throw new ArgumentOutOfRangeException(nameof(workDone), "The work done is no number.");
            }

            float target = Math.Min(1f, Math.Max(0f, workDone));
            if (target <= Shown)
            {
                return; // the bar never goes back
            }

            Shown = Math.Min(target, Shown + deltaSeconds / ShortestFillSeconds);
        }
    }
}
