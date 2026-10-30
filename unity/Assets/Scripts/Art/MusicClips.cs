#nullable enable
using System;
using UnityEngine;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The game's one piece of music as the two clips tools/music/cut-loop.py cuts it into (art/sounds/music.json): the
    /// intro, played once as the game starts, and the loop that follows it with no gap and repeats. The loop's last
    /// sample leads into its first as the intro's last sample does, so neither join can be heard.
    /// </summary>
    public sealed class MusicClips
    {
        public MusicClips(AudioClip intro, AudioClip loop)
        {
            Intro = intro != null ? intro : throw new ArgumentNullException(nameof(intro));
            Loop = loop != null ? loop : throw new ArgumentNullException(nameof(loop));
        }

        public AudioClip Intro { get; }

        public AudioClip Loop { get; }

        /// <summary>How long the intro plays from a sample of it to its end, in seconds of the audio clock: the loop begins that long after.</summary>
        public double IntroSecondsFrom(int sample) => (Intro.samples - sample) / (double)Intro.frequency;
    }
}
