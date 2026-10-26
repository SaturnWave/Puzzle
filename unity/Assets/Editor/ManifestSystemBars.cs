#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the Android build requires of the manifest Unity writes, for the system bars: that Unity hides them with
    /// Android's default behaviour, <c>unity.system-bars-behavior</c> = <see cref="Default"/>. The game is drawn over
    /// the whole screen with both bars hidden (the safe area leaves out the camera's cutout, Scripts/GameRoot.cs).
    /// <para>
    /// Unity's other behaviour, ShowTransientBarsBySwipe (what Unity 6 writes unless told), is Android's "immersive
    /// sticky": a swipe from an edge only shows the bars for a moment and does nothing else, so the first back gesture
    /// from the side of the screen was spent on the bars and a second one went back (T235, T412: the 23 and 26 October
    /// phone runs). With the default behaviour Android lets the system's gestures act whatever the bars do ("when the
    /// gesture navigation is enabled, the system gestures can be triggered regardless the visibility of system bars",
    /// WindowInsetsController.BEHAVIOR_DEFAULT): one back gesture goes back. What the game gives for it: a swipe that
    /// shows the bars shows them until the game is hidden and comes back (Unity hides them again when the game regains
    /// the focus), not for a moment; the bars then lie over the game's edges. Keeping the navigation bar shown was the
    /// other way: Unity pads the game's view by the bar's height then, and a phone loses that strip of the screen.
    /// </para>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestSystemBars
    {
        /// <summary>The value Unity writes for Android's default behaviour (AndroidSystemBarsBehavior.Default).</summary>
        public const string Default = "Default";

        /// <summary>The application's meta-data from which Unity's player reads how to hide the bars.</summary>
        private const string BehaviourMark = "unity.system-bars-behavior";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The manifest has not exactly one such meta-data, or it says another behaviour.</exception>
        public static void Require(string manifest)
        {
            XDocument document = XDocument.Parse(manifest);
            string?[] said = (document.Root?.Element("application")?.Elements("meta-data") ?? Enumerable.Empty<XElement>())
                .Where(data => (string?)data.Attribute(Android + "name") == BehaviourMark)
                .Select(data => (string?)data.Attribute(Android + "value"))
                .ToArray();
            if (said.Length != 1)
            {
                throw new InvalidOperationException($"The manifest's application has {said.Length} meta-data named {BehaviourMark}, not one, so it cannot be told how Unity hides the system bars.");
            }

            if (said[0] != Default)
            {
                throw new InvalidOperationException(
                    $"Unity hides the system bars as \"{said[0]}\", not \"{Default}\": a back gesture from the screen's edge would only show the bars, "
                    + "and a second one would go back. The player settings' system bars behaviour is to be Default.");
            }
        }
    }
}
