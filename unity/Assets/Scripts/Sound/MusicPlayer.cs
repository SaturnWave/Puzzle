#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using UnityEngine;

namespace Puzzle.Client.Sound
{
    /// <summary>Which of the music's two clips a place lies in. A closed set.</summary>
    public enum MusicPart
    {
        /// <summary>The intro, heard once as the game starts.</summary>
        Intro,

        /// <summary>The loop, which follows the intro and repeats for as long as the game runs.</summary>
        Loop,
    }

    /// <summary>A place in the music: one of its clips, and a sample of that clip.</summary>
    public readonly struct MusicPlace : IEquatable<MusicPlace>
    {
        public MusicPlace(MusicPart part, int sample)
        {
            Part = part;
            Sample = sample;
        }

        /// <summary>The music's first note.</summary>
        public static MusicPlace Beginning => new MusicPlace(MusicPart.Intro, 0);

        public MusicPart Part { get; }

        public int Sample { get; }

        public bool Equals(MusicPlace other) => Part == other.Part && Sample == other.Sample;

        public override bool Equals(object? obj) => obj is MusicPlace other && Equals(other);

        public override int GetHashCode() => (int)Part ^ (Sample << 1);

        public override string ToString() => $"{Part.ToString().ToLowerInvariant()} sample {Sample}";
    }

    /// <summary>
    /// The game's one piece of music (#9) on the phone's speaker, from its first frame to its last, on every screen:
    /// the game's root makes it on itself beside the cues' player, and nothing the screens do reaches it, so a page
    /// turned or a level opened never starts it again. It plays the intro once, then the loop with no gap and over
    /// and over, both scheduled on the audio clock (<see cref="AudioSource.PlayScheduled"/>), so the join does not
    /// depend on when a frame comes. It is heard under the effects (<see cref="SoundLevels.Music"/>).
    /// </summary>
    /// <remarks>
    /// Every frame it follows the player's music switch as the settings store holds it, so a switch flipped on the
    /// settings card takes effect at once: off, the music fades out (<see cref="MusicFade"/>) and is then held where
    /// it is; on, it goes on from there and fades in. It gives way to another app's music in the same way
    /// (<see cref="MusicYield"/>): silent while the player's own music plays, whatever the switch says, and back in a
    /// fade once that music has stopped; the effects are not touched. Put away with the game, it is held at once, and
    /// it fades in from there when the game comes back, unless another app's music plays by then. Holding stops both
    /// clips and keeps the place; going on schedules them again from it, rather than pausing a clip whose start is
    /// still to come on the audio clock.
    /// </remarks>
    public sealed class MusicPlayer
    {
        /// <summary>A clip is scheduled this far ahead of the audio clock, so that the mixer, and a streamed clip's reader, have it ready on time.</summary>
        private const double ScheduleLeadSeconds = 0.1;

        /// <summary>The music is never the voice the mixer drops when too many sounds ring at once (0 is the highest priority, 128 a cue's).</summary>
        private const int HighestPriority = 0;

        private readonly ISettingsStore _settings;
        private readonly AudioSource _intro;
        private readonly AudioSource _loop;
        private readonly MusicFade _fade;
        private readonly MusicYield _yield;

        /// <summary>Where the music goes on from when it is played next: its beginning, before it was ever played.</summary>
        private MusicPlace _heldAt = MusicPlace.Beginning;

        /// <summary>Which clip the music went on in, the last time it was played: the intro is not played again after the loop.</summary>
        private MusicPart _wentOnIn;

        /// <summary>When the loop begins, or began, on the audio clock.</summary>
        private double _loopBeginsAt;

        /// <param name="host">The game object (the game's root) that keeps the music's two sources as long as it lives.</param>
        /// <param name="settings">The player's settings, as the session keeps them: the music follows its switch there.</param>
        /// <param name="otherAudio">Whether another app's music plays on the device: the music gives way to it. Asked at once.</param>
        public MusicPlayer(GameObject host, MusicClips clips, ISettingsStore settings, IOtherAudio otherAudio)
        {
            Clips = clips ?? throw new ArgumentNullException(nameof(clips));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _intro = AddSource(host, clips.Intro, loops: false);
            _loop = AddSource(host, clips.Loop, loops: true);
            _yield = new MusicYield(otherAudio);
            _fade = new MusicFade(IsWanted);
        }

        /// <summary>The clips the music is played from.</summary>
        public MusicClips Clips { get; }

        /// <summary>How many times the music began at its first note: once a run, however often the screens change.</summary>
        public int Beginnings { get; private set; }

        /// <summary>Whether a clip of the music plays, or is scheduled to; false while the music is held.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>The volume both clips are given now: the music's level times the fade.</summary>
        public float Volume => _loop.volume;

        /// <summary>Where the music is: the place the audio clock has reached, or the one it is held at.</summary>
        public MusicPlace Place => IsPlaying ? PlaceNow() : _heldAt;

        /// <summary>Whether the music gives way to another app's music: it is then silent, whatever its switch says.</summary>
        public bool IsGivingWay => _yield.IsGivingWay;

        /// <summary>The player's switch is on and no other app's music is to be given way to.</summary>
        private bool IsWanted => _settings.Settings.Music && !_yield.IsGivingWay;

        /// <summary>One frame of the game: the music follows the player's switch, and another app's music, by the time that passed.</summary>
        public void Advance(float deltaSeconds)
        {
            bool wasGivingWay = _yield.IsGivingWay;
            _yield.Advance(deltaSeconds, isFading: !_fade.IsAtRest);
            SayAChange(wasGivingWay);
            _fade.Follow(IsWanted, deltaSeconds);
            SetTheVolume();
            if (_fade.IsSilent)
            {
                Hold();
            }
            else if (!IsPlaying)
            {
                GoOnFrom(_heldAt);
            }
        }

        /// <summary>The game is put away: the music is held where it is, and fades in from there once the game comes back with the switch on.</summary>
        public void PutAway()
        {
            Hold();
            _fade.Silence();
            SetTheVolume();
        }

        /// <summary>
        /// The game is in front again (from the background, or from behind a system card): whether another app's music
        /// plays is asked at once, so the music held while the game was away fades in only where nothing else plays.
        /// </summary>
        public void ComeBack()
        {
            bool wasGivingWay = _yield.IsGivingWay;
            _yield.AskNow();
            SayAChange(wasGivingWay);
        }

        /// <summary>Says in the log when the music begins or ends giving way, for the phone tests.</summary>
        private void SayAChange(bool wasGivingWay)
        {
            if (_yield.IsGivingWay != wasGivingWay)
            {
                Debug.Log(_yield.IsGivingWay ? "The music gives way to another app's music." : "No other app's music plays: the music comes back.");
            }
        }

        private void SetTheVolume()
        {
            float volume = SoundLevels.Music * _fade.Share;
            _intro.volume = volume;
            _loop.volume = volume;
        }

        private static AudioSource AddSource(GameObject host, AudioClip clip, bool loops)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = loops;
            source.spatialBlend = 0f; // heard as it is, wherever the stage's camera stands
            source.priority = HighestPriority;
            source.volume = 0f;
            return source;
        }

        private void Hold()
        {
            if (!IsPlaying)
            {
                return;
            }

            _heldAt = PlaceNow();
            _intro.Stop();
            _loop.Stop();
            IsPlaying = false;
        }

        /// <summary>Schedules the clip the place lies in from the place, and the loop after the intro when the place lies in the intro.</summary>
        private void GoOnFrom(MusicPlace place)
        {
            double begins = AudioSettings.dspTime + ScheduleLeadSeconds;
            switch (place.Part)
            {
                case MusicPart.Intro:
                    if (place.Equals(MusicPlace.Beginning))
                    {
                        Beginnings++;
                    }

                    _intro.timeSamples = place.Sample;
                    _intro.PlayScheduled(begins);
                    _loopBeginsAt = begins + Clips.IntroSecondsFrom(place.Sample);
                    _loop.timeSamples = 0;
                    break;
                case MusicPart.Loop:
                    _loopBeginsAt = begins;
                    _loop.timeSamples = place.Sample;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(place), place.Part, "A part of the music the player does not know.");
            }

            _loop.PlayScheduled(_loopBeginsAt);
            _wentOnIn = place.Part;
            IsPlaying = true;
        }

        private MusicPlace PlaceNow()
        {
            bool isInTheLoop = _wentOnIn == MusicPart.Loop || AudioSettings.dspTime >= _loopBeginsAt;
            return isInTheLoop ? new MusicPlace(MusicPart.Loop, _loop.timeSamples) : new MusicPlace(MusicPart.Intro, _intro.timeSamples);
        }
    }
}
