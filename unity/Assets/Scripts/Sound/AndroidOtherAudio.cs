#nullable enable
#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Whether another app's music plays on the Android phone the game runs on, from Android's list of active players
    /// read through the phone's AudioManager and told apart from the game's own output (<see cref="AndroidPlayback"/>).
    /// An AudioManager that cannot be reached or that fails is said once, and nothing else plays from then on: the
    /// music is then played over the player's, as before this was asked.
    /// </summary>
    public sealed class AndroidOtherAudio : IOtherAudio, IDisposable
    {
        private AndroidJavaObject? _audioManager;

        /// <summary>The last answer, so that a change is said once in the phone's log (for the phone tests), and not every few seconds.</summary>
        private bool _wasPlaying;

        private AndroidOtherAudio(AndroidJavaObject? audioManager)
        {
            _audioManager = audioManager;
        }

        /// <summary>The phone's AudioManager; one where nothing else plays when it cannot be reached.</summary>
        public static AndroidOtherAudio OfThisPhone()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                return new AndroidOtherAudio(activity.Call<AndroidJavaObject>("getSystemService", "audio"));
            }
            catch (AndroidJavaException failure)
            {
                Debug.LogWarning($"The phone's audio manager cannot be reached, so the music never gives way to the player's: {failure.Message}");
                return new AndroidOtherAudio(null);
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (_audioManager == null)
                {
                    return false;
                }

                try
                {
                    IReadOnlyCollection<PlaybackSort> active = ActivePlayers(_audioManager);
                    bool gameOutputRuns = AudioSettings.Mobile.audioOutputStarted;
                    bool isPlaying = AndroidPlayback.AnotherAppIsHeard(active, gameOutputRuns);
                    SayAChange(isPlaying, active, gameOutputRuns);
                    return isPlaying;
                }
                catch (AndroidJavaException failure)
                {
                    Debug.LogWarning($"The phone's audio manager refused its list of players, so the music no longer gives way to the player's in this run: {failure.Message}");
                    Dispose();
                    return false;
                }
            }
        }

        public void Dispose()
        {
            _audioManager?.Dispose();
            _audioManager = null;
        }

        private static IReadOnlyCollection<PlaybackSort> ActivePlayers(AndroidJavaObject audioManager)
        {
            using AndroidJavaObject configurations = audioManager.Call<AndroidJavaObject>("getActivePlaybackConfigurations");
            int count = configurations.Call<int>("size");
            var sorts = new List<PlaybackSort>(count);
            for (int index = 0; index < count; index++)
            {
                using AndroidJavaObject configuration = configurations.Call<AndroidJavaObject>("get", index);
                using AndroidJavaObject attributes = configuration.Call<AndroidJavaObject>("getAudioAttributes");
                sorts.Add(AndroidPlayback.SortOf(attributes.Call<int>("getUsage"), attributes.Call<int>("getContentType")));
            }

            return sorts;
        }

        private void SayAChange(bool isPlaying, IReadOnlyCollection<PlaybackSort> active, bool gameOutputRuns)
        {
            if (isPlaying == _wasPlaying)
            {
                return;
            }

            _wasPlaying = isPlaying;
            int listening = active.Count(sort => sort == PlaybackSort.Listening);
            Debug.Log(
                $"Other audio: {(isPlaying ? "another app's music plays" : "no other app's music plays")} "
                + $"({listening} listening of {active.Count} active players; the game's output {(gameOutputRuns ? "runs" : "is stopped")}).");
        }
    }
}
#endif
