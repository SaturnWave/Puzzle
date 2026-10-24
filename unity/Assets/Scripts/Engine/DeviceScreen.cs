#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Layout;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The device's screen as the rule of turning sees it (<see cref="ScreenTurns"/>), and that rule put to work through
    /// Unity's screen settings. Android is asked for the largest window it can give the game: on Android 11 and later
    /// the window manager's maximum window, before that the display's real size, in density-independent points. An
    /// iOS device is a tablet when it is an iPad. The Editor and a desktop have no rule: their window is what it is.
    /// </summary>
    /// <remarks>
    /// The Android package's manifest keeps the game upright (AndroidBuild), so a phone never shows it sideways, not
    /// even for the first frame; a tablet held sideways shows the first frames upright and turns once the game's root
    /// has applied the rule. Android honours a game's own wish on a large screen (the manifest's appCategory="game",
    /// which AndroidBuild's manifest step requires); without it, Android 16 and later would turn any app on a large
    /// screen and keep none upright. With "auto-rotation: user" in the player settings the system's own rotation lock
    /// is honoured. On an iPad the Info.plist allows all four ways and the rule asks for the same; on an iPhone the
    /// Info.plist allows upright only.
    /// </remarks>
    public static class DeviceScreen
    {
        /// <summary>Android 11, API level 30: from it on the window manager tells the largest window it can give an app.</summary>
        private const int FirstWithMaximumWindowMetrics = 30;

        /// <summary>
        /// What kind of screen the game runs on; nothing in the Editor and on a desktop. An Android device that cannot
        /// be asked counts as a phone and stays upright, as every build before this rule did.
        /// </summary>
        public static ScreenClass? ClassOfThisDevice() => Application.platform switch
        {
            RuntimePlatform.Android => ClassOfThisAndroidDevice(),
            RuntimePlatform.IPhonePlayer => ScreenTurns.OfAnAppleDevice(SystemInfo.deviceModel),
            _ => null,
        };

        /// <summary>Lets the screen stand the ways the rule allows a screen of its class: upright alone, or all four with the device.</summary>
        public static void LetTurn(ScreenClass screen)
        {
            IReadOnlyList<ScreenTurn> allowed = ScreenTurns.AllowedFor(screen);
            Screen.autorotateToPortrait = allowed.Contains(ScreenTurn.Portrait);
            Screen.autorotateToPortraitUpsideDown = allowed.Contains(ScreenTurn.PortraitUpsideDown);
            Screen.autorotateToLandscapeLeft = allowed.Contains(ScreenTurn.LandscapeLeft);
            Screen.autorotateToLandscapeRight = allowed.Contains(ScreenTurn.LandscapeRight);
            Screen.orientation = allowed.Count == 1 ? OrientationOf(allowed[0]) : ScreenOrientation.AutoRotation;
        }

        private static ScreenOrientation OrientationOf(ScreenTurn turn) => turn switch
        {
            ScreenTurn.Portrait => ScreenOrientation.Portrait,
            ScreenTurn.PortraitUpsideDown => ScreenOrientation.PortraitUpsideDown,
            ScreenTurn.LandscapeLeft => ScreenOrientation.LandscapeLeft,
            ScreenTurn.LandscapeRight => ScreenOrientation.LandscapeRight,
            _ => throw new System.ArgumentOutOfRangeException(nameof(turn), turn, "A way of standing Unity has no name for."),
        };

        /// <summary>True once this run has said that the screen's size could not be read, which it says once however often it asks.</summary>
        private static bool _isTheUnreadSizeSaid;

        /// <summary>
        /// What an Android screen is, by its largest window. A size that cannot be read (Android refuses the call, Unity
        /// has no activity yet) or reads as no length (no density, an empty window) counts as a phone's, as every build
        /// before the rule did, and is said once a run: the rule is asked again whenever the screen changes.
        /// </summary>
        private static ScreenClass ClassOfThisAndroidDevice()
        {
            float? smallestSideDp;
            try
            {
                smallestSideDp = SmallestSideOfTheLargestWindowDp();
            }
            catch (AndroidJavaException failure)
            {
                return AsAPhoneForASizeUnread($"Android refused the call: {failure.Message}");
            }

            if (!(smallestSideDp is float side))
            {
                return AsAPhoneForASizeUnread("Unity has no activity to ask");
            }

            if (!ScreenTurns.IsALength(side))
            {
                return AsAPhoneForASizeUnread($"its shorter side reads as {side} dp");
            }

            ScreenClass screen = ScreenTurns.OfAnAndroidScreen(side);
            Debug.Log($"The screen's largest window is {side:0} dp on its shorter side: a {screen}.");
            return screen;
        }

        private static ScreenClass AsAPhoneForASizeUnread(string why)
        {
            if (!_isTheUnreadSizeSaid)
            {
                Debug.LogWarning($"The screen's size cannot be read, so the game stays upright as on a phone: {why}.");
                _isTheUnreadSizeSaid = true;
            }

            return ScreenClass.Phone;
        }

        /// <summary>
        /// The shorter side of the largest window the device can give the game now, in density-independent points;
        /// nothing while Unity has no activity. A device that reports no density or an empty window gives no length.
        /// </summary>
        /// <exception cref="AndroidJavaException">Android refused one of the calls.</exception>
        private static float? SmallestSideOfTheLargestWindowDp()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject? activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null)
            {
                return null;
            }

            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            using AndroidJavaObject windows = activity.Call<AndroidJavaObject>("getWindowManager");
            using AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources");
            using AndroidJavaObject metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics");
            float density = metrics.Get<float>("density");
            int width;
            int height;
            if (version.GetStatic<int>("SDK_INT") >= FirstWithMaximumWindowMetrics)
            {
                using AndroidJavaObject largest = windows.Call<AndroidJavaObject>("getMaximumWindowMetrics");
                using AndroidJavaObject bounds = largest.Call<AndroidJavaObject>("getBounds");
                width = bounds.Call<int>("width");
                height = bounds.Call<int>("height");
            }
            else
            {
                using AndroidJavaObject display = windows.Call<AndroidJavaObject>("getDefaultDisplay");
                using var size = new AndroidJavaObject("android.graphics.Point");
                display.Call("getRealSize", size);
                width = size.Get<int>("x");
                height = size.Get<int>("y");
            }

            return Mathf.Min(width, height) / density;
        }
    }
}
