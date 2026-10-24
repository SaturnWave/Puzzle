#nullable enable
using System;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the Android build requires of the manifest Unity writes, for the screen's turning: that the application is
    /// a game, <c>android:appCategory="game"</c>. Android 16 and later ignore every app's wish to stay upright on a screen
    /// of 600 dp and more (a tablet, an open foldable) unless the app is a game; the game's own rule (phones upright,
    /// tablets every way, Scripts/Layout/ScreenClass.cs) holds only while Android honours its wishes. Unity writes the
    /// category today; a build in which it would not stops here instead of turning in Android's way, not the game's. This
    /// file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestGameCategory
    {
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The manifest has no application, or one that is not a game.</exception>
        public static void Require(string manifest)
        {
            XDocument document = XDocument.Parse(manifest);
            XElement application = document.Root?.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application>, so Android cannot be told the app is a game.");
            string? category = (string?)application.Attribute(Android + "appCategory");
            if (category != "game")
            {
                throw new InvalidOperationException(
                    $"The manifest's application is {(category == null ? "of no category" : $"of the category \"{category}\"")}, not \"game\": "
                    + "Android 16 and later would then turn it with the device on every tablet and open foldable, whatever the game asks.");
            }
        }
    }
}
