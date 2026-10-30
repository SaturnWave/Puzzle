#nullable enable
using System;
using Puzzle.Client.Flow;
using UnityEngine;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Short ticks through Android's vibrator, as the phone's own keyboard ticks: one of Android's predefined clicks
    /// from Android 10 on, a pulse of a few milliseconds before that. The build asks for the permission to vibrate
    /// (AndroidBuild's manifest step). Anywhere but on an Android phone (the Editor, a desktop), and on a device without
    /// a vibrator (many tablets), a tick is nothing. A vibrator that fails is said once and not asked again in the run.
    /// </summary>
    public sealed class Haptics : IDisposable
    {
        /// <summary>Android's VibrationEffect.EFFECT_CLICK and EFFECT_HEAVY_CLICK.</summary>
        private const int EffectClick = 0;
        private const int EffectHeavyClick = 5;

        /// <summary>The first Android with predefined effects: 10, API level 29. The game runs from API level 26 on.</summary>
        private const int FirstWithPredefinedEffects = 29;

        /// <summary>VibrationEffect.DEFAULT_AMPLITUDE: the phone's own strength.</summary>
        private const int DefaultAmplitude = -1;

        /// <summary>How long a pulse lasts where no predefined click exists: a light tick and a firm one.</summary>
        private const long LightMilliseconds = 12;
        private const long FirmMilliseconds = 30;

        private readonly int _androidVersion;
        private AndroidJavaObject? _vibrator;

        private Haptics(AndroidJavaObject? vibrator, int androidVersion, bool deviceHasNone)
        {
            _vibrator = vibrator;
            _androidVersion = androidVersion;
            DeviceHasNone = deviceHasNone;
        }

        /// <summary>The vibrator of the device the game runs on; one that ticks nothing where there is none to reach.</summary>
        public static Haptics OfThisDevice()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                return new Haptics(null, androidVersion: 0, deviceHasNone: false);
            }

            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                AndroidJavaObject? vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    vibrator?.Dispose();
                    return new Haptics(null, androidVersion: 0, deviceHasNone: true);
                }

                return new Haptics(vibrator, version.GetStatic<int>("SDK_INT"), deviceHasNone: false);
            }
            catch (AndroidJavaException failure)
            {
                Debug.LogWarning($"The phone's vibrator cannot be reached, so play does not tick: {failure.Message}");
                return new Haptics(null, androidVersion: 0, deviceHasNone: true);
            }
        }

        /// <summary>
        /// Whether the device has nothing to tick from the start: an Android device without a vibrator (many tablets) or
        /// whose vibrator cannot be reached. The settings card then leaves the vibration switch out. False in the Editor
        /// and on a desktop, which are drawn as the phone, switch and all.
        /// </summary>
        public bool DeviceHasNone { get; }

        /// <summary>One short tick, lighter or firmer; nothing where there is no vibrator to reach.</summary>
        public void Buzz(Tick tick)
        {
            if (_vibrator == null)
            {
                return;
            }

            try
            {
                using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                using AndroidJavaObject effect = _androidVersion >= FirstWithPredefinedEffects
                    ? effects.CallStatic<AndroidJavaObject>("createPredefined", tick == Tick.Firm ? EffectHeavyClick : EffectClick)
                    : effects.CallStatic<AndroidJavaObject>("createOneShot", tick == Tick.Firm ? FirmMilliseconds : LightMilliseconds, DefaultAmplitude);
                _vibrator.Call("vibrate", effect);
            }
            catch (AndroidJavaException failure)
            {
                Debug.LogWarning($"The phone's vibrator refused a tick, so play ticks no more in this run: {failure.Message}");
                Dispose();
            }
        }

        public void Dispose()
        {
            _vibrator?.Dispose();
            _vibrator = null;
        }
    }
}
