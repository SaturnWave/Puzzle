#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the Android build requires of the manifest Unity writes, for the screen's turning: that Unity's activity
    /// opens upright, <c>android:screenOrientation="portrait"</c>. The package stands as a phone does, the one way the
    /// rule lets a phone stand (Scripts/Layout/ScreenClass.cs), so a phone never shows the game sideways, not even for
    /// its first frame; a tablet or an open foldable turns with the device once the game runs and the rule lets it
    /// (Scripts/Engine/DeviceScreen.cs). Unity writes the activity's orientation from the player settings' default,
    /// which the project keeps upright and AndroidBuild sets so again; a build in which another default slipped in
    /// (the iOS build's own, say) stops here instead of opening every phone in whatever way it is held. The activity is
    /// the one Unity marks as its own, <c>unityplayer.UnityActivity</c>. This file has no Unity dependency, so the
    /// repository's tests compile it too.
    /// </summary>
    public static class ManifestOrientation
    {
        /// <summary>Android's name for upright with the device's foot down: the way a phone stands, and the package opens.</summary>
        public const string Upright = "portrait";

        /// <summary>The meta-data by which Unity marks the activity it plays the game in.</summary>
        private const string UnityActivityMark = "unityplayer.UnityActivity";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The manifest has not exactly one activity of Unity's, or that one does not open upright.</exception>
        public static void Require(string manifest)
        {
            XDocument document = XDocument.Parse(manifest);
            XElement[] unitys = (document.Root?.Element("application")?.Elements("activity") ?? Enumerable.Empty<XElement>())
                .Where(IsUnitys)
                .ToArray();
            if (unitys.Length != 1)
            {
                throw new InvalidOperationException(
                    $"The manifest has {unitys.Length} activities marked {UnityActivityMark}, not one, so it cannot be told which opens the game upright.");
            }

            XElement activity = unitys[0];
            string? orientation = (string?)activity.Attribute(Android + "screenOrientation");
            if (orientation != Upright)
            {
                throw new InvalidOperationException(
                    $"The activity {(string?)activity.Attribute(Android + "name")} opens {(orientation == null ? "in no orientation of its own" : $"as \"{orientation}\"")}, not \"{Upright}\": "
                    + "a phone would show the game sideways before its rule keeps it upright. The player settings' default orientation is to be Portrait.");
            }
        }

        private static bool IsUnitys(XElement activity) =>
            activity.Elements("meta-data").Any(data =>
                (string?)data.Attribute(Android + "name") == UnityActivityMark && (string?)data.Attribute(Android + "value") == "true");
    }
}
