#nullable enable
using System;
using Puzzle.Client.Stage;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// What a switch of the settings card shows at a moment (SettingsTR): where its knob stands on the track, and how
    /// much of the lit green track with its tick shows over the dull cream one. At rest the knob stands at one end,
    /// right when the switch is on and left when it is off; a press slides it to the other end while the lit track
    /// fades in or out under it, so the switch is never seen in two states at once. Lengths are the canvas' points,
    /// from the middle of the track. No Unity dependency.
    /// </summary>
    public readonly struct ToggleLook
    {
        /// <summary>The knob's middle stands this far right of the track's middle when on, and as far left when off.</summary>
        public const float KnobReach = 15f;

        private ToggleLook(float knobOffset, float litTrack)
        {
            KnobOffset = knobOffset;
            LitTrack = litTrack;
        }

        /// <summary>How far right of the track's middle the knob's middle stands; less than nothing for left of it.</summary>
        public float KnobOffset { get; }

        /// <summary>How much the lit track shows over the dull one, from 0 (off) to 1 (on).</summary>
        public float LitTrack { get; }

        /// <summary>A switch at rest.</summary>
        public static ToggleLook AtRest(bool isOn) => new ToggleLook(isOn ? KnobReach : -KnobReach, isOn ? 1f : 0f);

        /// <summary>A switch on its way to a state, so far into its slide.</summary>
        /// <param name="isOn">The state the switch slides to.</param>
        /// <param name="progress">How far the slide has come, from 0 (just pressed) to 1 (at rest in its new state).</param>
        /// <exception cref="ArgumentOutOfRangeException">A progress outside 0..1.</exception>
        public static ToggleLook Sliding(bool isOn, float progress)
        {
            if (!(progress >= 0f && progress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "A slide's progress runs from 0 to 1.");
            }

            float come = Easing.InOut(progress);
            float towards = isOn ? 1f : -1f;
            return new ToggleLook(towards * KnobReach * (2f * come - 1f), isOn ? come : 1f - come);
        }
    }
}
