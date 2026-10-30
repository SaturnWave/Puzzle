#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Keeps Google Play Games from starting before the game asks for it. Play Games Services v2
    /// (com.google.android.gms:play-services-games-v2 22.1.0, which the game's Play Games plugin needs) declares a content
    /// provider, <see cref="InitProvider"/>, that Android starts with the game's process: it initialises the SDK at once,
    /// and the SDK then signs the player in by itself when the game's activity comes up, Google's "welcome" banner and
    /// its own reports with it, whatever the game says. The game's own rule is that nothing of the cloud runs while the
    /// player's "Bulut kaydı" switch is off (the owner's decision 1, docs/research/server-v1.md §3.8), Play Games
    /// included.
    /// <para>
    /// The manifest step marks the provider for removal in the Unity library's manifest (tools:node="remove"), so
    /// Android's manifest merger takes it out of the SDK's declaration, as it takes out the logging transport's backend
    /// (<see cref="ManifestLoggingBackend"/>). The SDK is then initialised by the plugin itself, when the game activates
    /// it (PlayGamesPlatform.Activate, whose Android client calls PlayGamesSdk.initialize), which the game does only at
    /// its first sign-in, once Unity's services have started with the switch on (Cloud/Ugs/PlayGamesSignIn.cs). Without
    /// the provider the SDK's own code finds no APP_ID to read either (that read is guarded: its NumberFormatException is
    /// caught, bytecode of 22.1.0 read 2026-10-26), so a build that is not set up for Play Games starts nothing of it.
    /// </para>
    /// The built package is read back too (<see cref="RequireWithdrawnFrom"/>): a provider that came through stops the
    /// build. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestPlayGamesStart
    {
        /// <summary>The provider that initialises Play Games with the game's process.</summary>
        public const string InitProvider = "com.google.android.gms.games.provider.PlayGamesInitProvider";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>
        /// The manifest with the provider marked for removal: under the application, a provider named
        /// <see cref="InitProvider"/> with tools:node="remove" and nothing else. A manifest that has it so already comes
        /// back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string Withdraw(string manifest)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            XElement application = root.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application> to withdraw Play Games' start-up provider under.");
            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
            }

            application.Elements("provider").Where(element => (string?)element.Attribute(Android + "name") == InitProvider).Remove();
            application.Add(new XElement("provider", new XAttribute(Android + "name", InitProvider), new XAttribute(Tools + "node", "remove")));

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Stops a package whose manifest still declares the provider: Play Games would start with the game, switch off or not.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package declares the provider.</exception>
        public static void RequireWithdrawnFrom(string dump)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }

            if (dump.Contains($"\"{InitProvider}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest declares {InitProvider}: Play Games would start and sign the player in with the game, whatever the switch says. "
                    + "The manifest step is to mark it for removal in the Unity library's manifest.");
            }
        }
    }
}
