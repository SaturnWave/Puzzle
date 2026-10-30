#nullable enable
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// Puts Android's status and navigation bars away again after a swipe brought them out. The package hides them
    /// with Android's default behaviour (Editor/ManifestSystemBars.cs), under which a single back gesture from the
    /// screen's edge goes back (T235). Under that behaviour a swipe that shows the bars shows them for good: Android
    /// asks the game to show them, and they stay until the game hides them again; Unity hides them when the game starts,
    /// and on Android 11 and later never again. So the game hides them once more <see cref="SecondsShown"/> after
    /// a way back, a change of the screen's shape (the bars coming out change the safe area) or the focus coming back
    /// (from the notification shade, say): about as long as Android's own passing bars stay. Android 11 (API level 30)
    /// and later only, where Unity hides the bars through the window's insets controller as this does; before it,
    /// Unity's own way of hiding them is left alone. Nothing happens in the Editor or on another platform.
    /// </summary>
    public static class SystemBars
    {
        /// <summary>How long bars that a swipe brought out stay before the game hides them again.</summary>
        public const float SecondsShown = 2f;

        /// <summary>Android 11, API level 30: the first with a window insets controller.</summary>
        private const int FirstWithAnInsetsController = 30;

        /// <summary>Asks Android, on its own thread, to hide the status and the navigation bar; bars that are hidden already stay so.</summary>
        public static void HideAgain()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                if (version.GetStatic<int>("SDK_INT") < FirstWithAnInsetsController)
                {
                    return;
                }

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject? activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity?.Call("runOnUiThread", new AndroidJavaRunnable(HideOnTheUiThread));
            }
            catch (AndroidJavaException failure)
            {
                Debug.LogWarning($"The system bars could not be put away again: Android refused the call: {failure.Message}");
            }
        }

        /// <summary>The hiding itself, on Android's thread for the window, where its insets controller is to be called.</summary>
        private static void HideOnTheUiThread()
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject? activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject? window = activity?.Call<AndroidJavaObject>("getWindow");
                using AndroidJavaObject? controller = window?.Call<AndroidJavaObject>("getInsetsController");
                if (controller == null)
                {
                    return; // no window yet, or one not attached: Unity hides the bars itself once it is
                }

                using var types = new AndroidJavaClass("android.view.WindowInsets$Type");
                controller.Call("hide", types.CallStatic<int>("statusBars") | types.CallStatic<int>("navigationBars"));
            }
            catch (AndroidJavaException failure)
            {
                Debug.LogWarning($"The system bars could not be put away again: Android refused the call: {failure.Message}");
            }
        }
    }
}
