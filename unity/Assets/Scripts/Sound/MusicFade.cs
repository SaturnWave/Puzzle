#nullable enable
using System;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// How much of its level the music is heard at, from silence (0) to all of it (1), as it follows the player's
    /// switch: turned off, it fades out over <see cref="Seconds"/>; turned on, it comes back over as long, from wherever
    /// it was, so a switch flipped twice in a moment never jumps. The music's player multiplies this with
    /// <see cref="SoundLevels.Music"/> and holds the music where it is once it is silent. No Unity dependency, so the
    /// repository's tests compile it too.
    /// </summary>
    public sealed class MusicFade
    {
        /// <summary>A fade lasts as long as a short breath: about twice the switch's knob takes to slide (Tempo.SwitchSlides, 0.16 s).</summary>
        public const float Seconds = 0.3f;

        /// <param name="isOn">Whether the music's switch is on as the game starts: the music is then heard at once, or not at all.</param>
        public MusicFade(bool isOn)
        {
            Share = isOn ? 1f : 0f;
        }

        /// <summary>How much of its level the music is heard at now, from 0 to 1.</summary>
        public float Share { get; private set; }

        /// <summary>Whether nothing of the music is heard: it may be held where it is.</summary>
        public bool IsSilent => Share <= 0f;

        /// <summary>Moves the share toward the switch's state by the time that passed: a whole fade per <see cref="Seconds"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The time is negative.</exception>
        public void Follow(bool isOn, float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Time does not run backwards.");
            }

            float step = deltaSeconds / Seconds;
            Share = isOn ? Math.Min(1f, Share + step) : Math.Max(0f, Share - step);
        }

        /// <summary>
        /// Silent at once, without a fade: the game was put away, and the music stopped with it. When the game comes
        /// back with the switch on, the music fades in from where it was held instead of starting mid-note at full level.
        /// </summary>
        public void Silence() => Share = 0f;
    }
}
