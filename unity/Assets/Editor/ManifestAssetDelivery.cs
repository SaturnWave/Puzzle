#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Keeps the Google Play bundle to the test package's permissions. A bundle with an asset pack is given Play Asset
    /// Delivery's library by Unity (com.google.android.play:asset-delivery 2.3.0, in the Unity library's build.gradle;
    /// the test package, one APK, has none), and the library brings what it needs to fetch and unpack the packs a game
    /// downloads after it is installed (fast-follow and on-demand): a foreground service of type dataSync that unpacks
    /// them (<see cref="ForegroundService"/>), and WorkManager (androidx.work 2.9.1), which runs that unpacking. With them
    /// came four permissions the test package does not ask for: FOREGROUND_SERVICE and FOREGROUND_SERVICE_DATA_SYNC for
    /// the service (Play Console asks a declaration and a video of a dataSync foreground service), and WorkManager's
    /// WAKE_LOCK and RECEIVE_BOOT_COMPLETED (the bundle build saw them, 2026-10-28).
    /// <para>
    /// The game has one asset pack, Unity's data, and it is install-time: Play installs it with the game as a split APK,
    /// whose files Android's asset manager shows the game as its own, so nothing is downloaded or unpacked later. The
    /// game never asks for an asset pack itself (no AndroidAssetPacks call). The manifest step therefore marks the
    /// foreground service for removal (tools:node="remove") and the four permissions are withdrawn with the build's
    /// list (AndroidBuild.Permissions). The rest of the library stays as it is declared, so that Unity's player finds
    /// it whole if it asks the library about its pack: WorkManager starts with the game, as the library has it, and with
    /// no work given it needs neither a wake lock nor the boot; the extraction service Play binds to stays switched off
    /// until the library would switch it on for a download. The test package has none of it, and its merged manifest
    /// stays as it was.
    /// </para>
    /// The built package is read back too (<see cref="RequireWithdrawnFrom"/>): a service that came through stops the
    /// build. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestAssetDelivery
    {
        /// <summary>The foreground service of type dataSync that unpacks a downloaded pack.</summary>
        public const string ForegroundService = "com.google.android.play.core.assetpacks.ExtractionForegroundService";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>
        /// The manifest with the foreground service marked for removal under its application: a service named
        /// <see cref="ForegroundService"/> with tools:node="remove" and nothing else. A manifest that has it so already
        /// comes back unchanged.
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
                ?? throw new InvalidOperationException("The manifest has no <application> to withdraw Play Asset Delivery's foreground service under.");
            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
            }

            application.Elements("service").Where(element => (string?)element.Attribute(Android + "name") == ForegroundService).Remove();
            application.Add(new XElement("service", new XAttribute(Android + "name", ForegroundService), new XAttribute(Tools + "node", "remove")));

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Stops a package whose manifest still declares the foreground service: it would want the withdrawn permissions, and Play its declaration.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package declares the service.</exception>
        public static void RequireWithdrawnFrom(string dump)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }

            if (dump.Contains($"\"{ForegroundService}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest declares {ForegroundService}: Play Asset Delivery's dataSync foreground service, for packs downloaded after the "
                    + "installation, which the game's one install-time pack does not need. The manifest step is to mark it for removal in the Unity library's manifest.");
            }
        }
    }
}
