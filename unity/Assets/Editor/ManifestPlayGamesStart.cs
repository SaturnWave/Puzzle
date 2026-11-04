#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Starts Google Play Games with the game's process only while the player's "Bulut kaydı" switch is on. Play Games
    /// Services v2 (com.google.android.gms:play-services-games-v2 22.1.0, which the game's Play Games plugin needs)
    /// declares a content provider, <see cref="InitProvider"/>, that Android starts with the game's process: it
    /// initialises the SDK at once, and the SDK then signs the player in by itself when the game's activity comes up,
    /// Google's "welcome" banner and its own reports with it, whatever the game says. The game's own rule is that nothing
    /// of the cloud runs while the switch is off (the owner's decision 1, docs/research/server-v1.md §3.8), Play Games
    /// included. The manifest step marks that provider for removal in the Unity library's manifest (tools:node="remove"),
    /// so Android's manifest merger takes it out of the SDK's declaration, as it takes out the logging transport's backend
    /// (<see cref="ManifestLoggingBackend"/>).
    /// <para>
    /// The SDK's start cannot wait for the game's first sign-in, though: its own sign-in starts only as an activity is
    /// created (the initialisation registers activity callbacks, and the attempt starts in onActivityCreated alone;
    /// bytecode of 22.1.0, internal.games_v2.zzar). Initialised later, by the plugin (PlayGamesPlatform.Activate, whose
    /// Android client calls PlayGamesSdk.initialize), the SDK makes no attempt, and the game's quiet sign-in answers with
    /// error 10: a new phone did not bring the player's progress back (docs/LESSONS.md H7, T590 on 2026-11-04). In a build
    /// set up for Play Games the game's own provider, <see cref="GamesStart"/> (unity/Assets/Plugins/Android/PlayGamesStart.java),
    /// starts the SDK with the process instead, when the switch kept in cloud.json is on; the plugin's own initialisation
    /// at the first sign-in then finds it started. A build that is not set up has no such provider, and starts nothing of
    /// Play Games (the SDK's read of a missing APP_ID is guarded: its NumberFormatException is caught, bytecode of 22.1.0
    /// read 2026-10-26).
    /// </para>
    /// The built package is read back too (<see cref="RequireWithdrawnFrom"/>, <see cref="RequireTheGamesStartIn"/>):
    /// a package with the SDK's provider, or a set-up one without the game's provider and its class, stops the build. This
    /// file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestPlayGamesStart
    {
        /// <summary>The SDK's provider, which initialises Play Games with the game's process whatever the switch says.</summary>
        public const string InitProvider = "com.google.android.gms.games.provider.PlayGamesInitProvider";

        /// <summary>The game's provider, which initialises Play Games with the game's process while the switch is on.</summary>
        public const string GamesStart = "com.saturnwave.museumpuzzle.PlayGamesStart";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>The authority of the game's provider: Android asks every provider for one no other package has.</summary>
        public static string GamesStartAuthority(string packageId) => packageId + ".playgamesstart";

        /// <summary>
        /// The manifest with the SDK's provider marked for removal: under the application, a provider named
        /// <see cref="InitProvider"/> with tools:node="remove" and nothing else. A manifest that has it so already comes
        /// back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string Withdraw(string manifest)
        {
            return Rewritten(manifest, application =>
            {
                application.Elements("provider").Where(element => (string?)element.Attribute(Android + "name") == InitProvider).Remove();
                application.Add(new XElement("provider", new XAttribute(Android + "name", InitProvider), new XAttribute(Tools + "node", "remove")));
            });
        }

        /// <summary>
        /// The manifest with the game's provider under its application: <see cref="GamesStart"/>, not exported, with the
        /// package's own authority (<see cref="GamesStartAuthority"/>), and no other of that name. A manifest that has it
        /// so already comes back unchanged. Only a build set up for the game's Play Games project declares it.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string StartWithTheSwitch(string manifest, string packageId)
        {
            return Rewritten(manifest, application =>
            {
                application.Elements("provider").Where(element => (string?)element.Attribute(Android + "name") == GamesStart).Remove();
                application.Add(new XElement(
                    "provider",
                    new XAttribute(Android + "name", GamesStart),
                    new XAttribute(Android + "authorities", GamesStartAuthority(packageId)),
                    new XAttribute(Android + "exported", "false")));
            });
        }

        /// <summary>Stops a package whose manifest still declares the SDK's provider: Play Games would start with the game, switch off or not.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package declares the provider.</exception>
        public static void RequireWithdrawnFrom(string dump)
        {
            RequireAManifest(dump);
            if (dump.Contains($"\"{InitProvider}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest declares {InitProvider}: Play Games would start and sign the player in with the game, whatever the switch says. "
                    + "The manifest step is to mark it for removal in the Unity library's manifest.");
            }
        }

        /// <summary>
        /// Stops a package set up for Play Games that does not start it with the switch: its manifest must declare the
        /// game's provider, and one of its dex files must hold the provider's class, or Android would stop the game at
        /// every start for a class it cannot find.
        /// </summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <param name="dexFiles">The package's classes*.dex files.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, the provider is not declared, or no dex file holds its class.</exception>
        public static void RequireTheGamesStartIn(string dump, IEnumerable<byte[]> dexFiles)
        {
            RequireAManifest(dump);
            if (!dump.Contains($"\"{GamesStart}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest does not declare {GamesStart}: Play Games would start only after the game's activity, its quiet sign-in would fail, "
                    + "and a new phone would not get the player's progress back (docs/LESSONS.md H7). The manifest step is to declare it.");
            }

            // A dex file names each class it holds by its descriptor, in its strings.
            byte[] descriptor = Encoding.ASCII.GetBytes("L" + GamesStart.Replace('.', '/') + ";");
            if (!(dexFiles ?? throw new ArgumentNullException(nameof(dexFiles))).Any(dex => Holds(dex, descriptor)))
            {
                throw new InvalidOperationException(
                    $"No dex file of the package holds {GamesStart}, which its manifest declares: Android would stop the game at every start. "
                    + "Its source is unity/Assets/Plugins/Android/PlayGamesStart.java.");
            }
        }

        private static void RequireAManifest(string dump)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }
        }

        private static string Rewritten(string manifest, Action<XElement> change)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            XElement application = root.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application> to say how Play Games starts under.");
            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
            }

            change(application);

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        private static bool Holds(byte[] dex, byte[] descriptor) => dex.AsSpan().IndexOf(descriptor) >= 0;
    }
}
