#nullable enable
using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Puzzle.Client.Flow;
using UnityEngine;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Short ticks, as the phone's own keyboard ticks: through Android's vibrator on an Android phone, through the
    /// Taptic Engine on an iPhone (<see cref="Vibrator"/>). Anywhere else (the Editor, a desktop), and on a device
    /// without a vibrator (an iPad, a tablet), a tick is nothing. A vibrator that fails is said once and not asked
    /// again in the run.
    /// </summary>
    public sealed class Haptics : IDisposable
    {
        private Vibrator? _vibrator;

        private Haptics(Vibrator? vibrator, bool deviceHasNone)
        {
            _vibrator = vibrator;
            DeviceHasNone = deviceHasNone;
        }

        /// <summary>
        /// The vibrator of the device the game runs on; one that ticks nothing where there is none to reach. An iPad
        /// has no Taptic Engine, and iOS tells that only by the model's name.
        /// </summary>
        public static Haptics OfThisDevice()
        {
#if UNITY_IOS && !UNITY_EDITOR
            bool isAnIPad = SystemInfo.deviceModel.StartsWith("iPad", StringComparison.Ordinal);
            return isAnIPad ? new Haptics(null, deviceHasNone: true) : new Haptics(new Vibrator.TapticEngine(), deviceHasNone: false);
#else
            if (Application.platform != RuntimePlatform.Android)
            {
                return new Haptics(null, deviceHasNone: false);
            }

            Vibrator? vibrator = Vibrator.OfAndroid.OfThisPhone();
            return new Haptics(vibrator, deviceHasNone: vibrator == null);
#endif
        }

        /// <summary>
        /// Whether the device has nothing to tick from the start: an iPad, or an Android device without a vibrator or
        /// whose vibrator cannot be reached. The settings card then leaves the vibration switch out. False in the
        /// Editor and on a desktop, which are drawn as the phone, switch and all.
        /// </summary>
        public bool DeviceHasNone { get; }

        /// <summary>One short tick, lighter or firmer; nothing where there is no vibrator to reach.</summary>
        public void Buzz(Tick tick)
        {
            if (_vibrator != null && !_vibrator.TryTick(tick))
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            _vibrator?.Dispose();
            _vibrator = null;
        }

        /// <summary>What a tick goes through. A closed set: Android's vibrator, and the iPhone's Taptic Engine in an iOS build.</summary>
        private abstract class Vibrator : IDisposable
        {
            private Vibrator()
            {
            }

            /// <summary>One tick; false when the device refused it, which was said: the run asks no more of it.</summary>
            public abstract bool TryTick(Tick tick);

            public abstract void Dispose();

            /// <summary>
            /// Android's vibrator: one of Android's predefined clicks from Android 10 on, a pulse of a few milliseconds
            /// before that. The build asks for the permission to vibrate (AndroidBuild's manifest step).
            /// </summary>
            public sealed class OfAndroid : Vibrator
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

                private readonly AndroidJavaObject _vibrator;
                private readonly int _androidVersion;

                private OfAndroid(AndroidJavaObject vibrator, int androidVersion)
                {
                    _vibrator = vibrator;
                    _androidVersion = androidVersion;
                }

                /// <summary>The phone's vibrator; nothing on a phone without one, or where it cannot be reached, which is said.</summary>
                public static OfAndroid? OfThisPhone()
                {
                    try
                    {
                        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        AndroidJavaObject? vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                        {
                            vibrator?.Dispose();
                            return null;
                        }

                        return new OfAndroid(vibrator, version.GetStatic<int>("SDK_INT"));
                    }
                    catch (AndroidJavaException failure)
                    {
                        Debug.LogWarning($"The phone's vibrator cannot be reached, so play does not tick: {failure.Message}");
                        return null;
                    }
                }

                public override bool TryTick(Tick tick)
                {
                    try
                    {
                        using var effects = new AndroidJavaClass("android.os.VibrationEffect");
                        using AndroidJavaObject effect = _androidVersion >= FirstWithPredefinedEffects
                            ? effects.CallStatic<AndroidJavaObject>("createPredefined", tick == Tick.Firm ? EffectHeavyClick : EffectClick)
                            : effects.CallStatic<AndroidJavaObject>("createOneShot", tick == Tick.Firm ? FirmMilliseconds : LightMilliseconds, DefaultAmplitude);
                        _vibrator.Call("vibrate", effect);
                        return true;
                    }
                    catch (AndroidJavaException failure)
                    {
                        Debug.LogWarning($"The phone's vibrator refused a tick, so play ticks no more in this run: {failure.Message}");
                        return false;
                    }
                }

                public override void Dispose() => _vibrator.Dispose();
            }

#if UNITY_IOS && !UNITY_EDITOR
            /// <summary>
            /// The iPhone's Taptic Engine, through UIKit's impact feedback (Plugins/iOS/TapticEngine.mm): a light tap and
            /// a heavy one, for Android's click and heavy click. An iPad gets none (<see cref="OfThisDevice"/>); iOS itself
            /// leaves the taps out on an iPhone before the 7 and where the player turned the system's haptics off. A tap
            /// neither fails nor says so, and UIKit's generators live as long as the game.
            /// </summary>
            public sealed class TapticEngine : Vibrator
            {
                /// <summary>UIKit's UIImpactFeedbackStyleLight and UIImpactFeedbackStyleHeavy.</summary>
                private const int LightImpact = 0;
                private const int HeavyImpact = 2;

                public override bool TryTick(Tick tick)
                {
                    PuzzleTapticTap(tick == Tick.Firm ? HeavyImpact : LightImpact);
                    return true;
                }

                public override void Dispose()
                {
                    // Nothing of the game's to let go of: UIKit keeps the generators.
                }

                [DllImport("__Internal")]
                private static extern void PuzzleTapticTap(int style);
            }
#endif
        }
    }
}
