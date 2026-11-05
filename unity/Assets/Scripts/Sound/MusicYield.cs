#nullable enable
using System;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Whether the game's music gives way to another app's audio (the owner's decision of 2026-10-23): while the
    /// player's own music plays, the game's music stays silent and its effects go on; once that music has stopped,
    /// the game's music comes back. The music's player fades out and in as for its switch (<see cref="MusicFade"/>).
    /// No Unity dependency, so the repository's tests compile it too.
    /// </summary>
    /// <remarks>
    /// It asks at start and whenever the game comes to the front (<see cref="AskNow"/>), and follows that answer at
    /// once: the music is silent at those moments, so it either begins or stays silent, and nothing flaps. While the
    /// game runs it asks every <see cref="AskEverySeconds"/>, though never while the music's own fade is in motion;
    /// a question put off by a fade is asked as soon as the fade has ended. Another app heard, the music gives way at
    /// once; it comes back only when that app has gone unheard for <see cref="QuietBeforeReturnSeconds"/>, so a pause
    /// between two songs, or a moment of buffering, does not bring it in and out again.
    /// </remarks>
    public sealed class MusicYield
    {
        /// <summary>How often the device is asked while the game runs: the player's music is heard over the game's for this long at most.</summary>
        public const float AskEverySeconds = 2f;

        /// <summary>
        /// How long another app must go unheard before the game's music comes back: more than one asking interval, so
        /// a single quiet answer (a gap between songs) never brings it back; with <see cref="AskEverySeconds"/> of 2,
        /// it comes back at the second quiet answer in a row, two to four seconds after the other music stopped.
        /// </summary>
        public const float QuietBeforeReturnSeconds = 3f;

        private readonly IOtherAudio _otherAudio;

        /// <summary>How long ago the device was last asked.</summary>
        private float _sinceAsked;

        /// <summary>While the music gives way: how long ago another app was last heard, counted in the answers since.</summary>
        private float _sinceHeard;

        /// <summary>Asks at once: the music begins only if nothing else plays as the game starts.</summary>
        public MusicYield(IOtherAudio otherAudio)
        {
            _otherAudio = otherAudio ?? throw new ArgumentNullException(nameof(otherAudio));
            IsGivingWay = otherAudio.IsPlaying;
        }

        /// <summary>Whether the game's music gives way to another app's audio: it is then silent, whatever its switch says.</summary>
        public bool IsGivingWay { get; private set; }

        /// <summary>The game comes to the front again (from the background, or from behind a system card): asks at once, and follows the answer at once.</summary>
        public void AskNow()
        {
            IsGivingWay = _otherAudio.IsPlaying;
            _sinceAsked = 0f;
            _sinceHeard = 0f;
        }

        /// <summary>One frame of the game: asks the device when it is time to, unless the music's own fade is in motion.</summary>
        /// <param name="isFading">Whether the game's own music is fading in or out this frame: nothing is asked then.</param>
        /// <exception cref="ArgumentOutOfRangeException">The time is negative.</exception>
        public void Advance(float deltaSeconds, bool isFading)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Time does not run backwards.");
            }

            _sinceAsked += deltaSeconds;
            bool isTimeToAsk = !isFading && _sinceAsked >= AskEverySeconds;
            if (!isTimeToAsk)
            {
                return;
            }

            float sinceLastAnswer = _sinceAsked;
            _sinceAsked = 0f;
            if (_otherAudio.IsPlaying)
            {
                IsGivingWay = true;
                _sinceHeard = 0f;
                return;
            }

            if (!IsGivingWay)
            {
                return;
            }

            _sinceHeard += sinceLastAnswer;
            IsGivingWay = _sinceHeard < QuietBeforeReturnSeconds;
        }
    }
}
