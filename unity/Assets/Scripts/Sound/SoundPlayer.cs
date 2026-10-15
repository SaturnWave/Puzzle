#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using UnityEngine;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// The game's cues on the phone: a cue's sound from the art catalogue on one of a few audio sources, its tick
    /// through the vibrator (<see cref="Haptics"/>). The game's root makes it on itself, with the scene's one listener,
    /// and gives it to the session, which asks the player's settings before every cue. The Editor's tools never make
    /// one: their sessions are silent. A cue the catalogue has no sound for is not heard, and said once.
    /// </summary>
    /// <remarks>
    /// The sounds are short and come many at once in a cascade. <see cref="Voices"/> sources take them in turn, and a
    /// cue takes the source whose sound began longest ago, cutting what is left of it: no more than that many ring
    /// together however busy the board, and no cue waits.
    /// </remarks>
    public sealed class SoundPlayer : ICuePlayer, IDisposable
    {
        /// <summary>How many sounds ring together at most: a trade, its pops, a special piece made and one gone off, with room to spare.</summary>
        private const int Voices = 6;

        /// <summary>The stand-ins are interface sounds heard over and over: they are played below their full level, all alike.</summary>
        private const float Volume = 0.7f;

        private readonly ArtCatalogue _art;
        private readonly Haptics _haptics;
        private readonly AudioSource[] _sources = new AudioSource[Voices];

        /// <summary>The cues found without a sound, each said once: at most one of each cue.</summary>
        private readonly HashSet<SoundCue> _saidMissing = new HashSet<SoundCue>();
        private int _nextVoice;

        /// <summary>Puts the listener and the sources on a game object (the game's root), which keeps them as long as it lives.</summary>
        public SoundPlayer(GameObject host, ArtCatalogue art, Haptics haptics)
        {
            _art = art ?? throw new ArgumentNullException(nameof(art));
            _haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            host.AddComponent<AudioListener>();
            for (int voice = 0; voice < Voices; voice++)
            {
                AudioSource source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // heard as it is, wherever the stage's camera stands
                source.volume = Volume;
                _sources[voice] = source;
            }
        }

        public void Sound(SoundCue cue)
        {
            if (!(_art.SoundOf(cue) is AudioClip clip))
            {
                if (_saidMissing.Add(cue))
                {
                    Debug.LogWarning($"The art catalogue has no sound for {cue}, so it is not heard; run ProjectSetup.Run in the Editor's batch mode.");
                }

                return;
            }

            AudioSource source = _sources[_nextVoice];
            _nextVoice = (_nextVoice + 1) % Voices;
            source.Stop();
            source.clip = clip;
            source.Play();
        }

        public void Buzz(Tick tick) => _haptics.Buzz(tick);

        public void Dispose() => _haptics.Dispose();
    }
}
