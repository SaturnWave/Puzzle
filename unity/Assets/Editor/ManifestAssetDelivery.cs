#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Keeps the Google Play bundle to the test package's permissions without taking away what Play Asset Delivery's
    /// library needs to start. A bundle with an asset pack is given the library by Unity (com.google.android.play:
    /// asset-delivery 2.3.0; the test package, one APK, has none), and the library declares what it needs to unpack the
    /// packs a game downloads after it is installed (fast-follow and on-demand): the extraction service Play binds to
    /// (<see cref="ExtractionService"/>), a foreground service of type dataSync that does the unpacking
    /// (<see cref="ForegroundService"/>), and WorkManager (androidx.work 2.9.1). With them come four permissions the test
    /// package does not ask for: FOREGROUND_SERVICE and FOREGROUND_SERVICE_DATA_SYNC for the foreground service (Play
    /// Console asks a declaration and a video of a dataSync foreground service), and WorkManager's WAKE_LOCK and
    /// RECEIVE_BOOT_COMPLETED (the bundle build saw them, 2026-10-28).
    /// <para>
    /// The game has one asset pack, Unity's data, and it is install-time: Play installs it with the game as a split APK,
    /// whose files Android's asset manager shows the game as its own, so nothing is downloaded or unpacked later, and
    /// the four permissions are withdrawn with the build's list (AndroidBuild.Permissions). Both services must stay
    /// declared all the same: when Unity's player starts, it asks the library for its AssetPackManager, and the library
    /// first switches both services on (PackageManager.setComponentEnabledSetting; bytecode of 2.3.0 read 2026-10-29,
    /// s.a() and internal.af.a). Android refuses that for a service the manifest does not declare
    /// (IllegalArgumentException), and Unity's player stops on it: versionCode 1, whose manifest had the foreground
    /// service taken out (tools:node="remove", 2026-10-28), closed within two seconds of every start on the owner's
    /// phone (2026-10-29, "JNI DETECTED ERROR IN APPLICATION: java_object == null in call to GetObjectClass").
    /// </para>
    /// <para>
    /// The manifest step therefore keeps the foreground service and takes off its type alone
    /// (tools:remove="android:foregroundServiceType"), so the package declares no foreground service type and Play asks
    /// for no declaration. The library starts the service only when Play asks the extraction service to unpack a pack
    /// it has downloaded (its updateServiceState call), which a game without such packs is never asked. The mark is for
    /// the bundle's build alone: in the test package, which has no library to merge it with, it would declare a service
    /// whose class is not there. Both packages are read back (<see cref="RequireKeptWithoutItsTypeIn"/>,
    /// <see cref="RequireAbsentFrom"/>). This file has no Unity dependency, so the repository's tests compile it too.
    /// </para>
    /// </summary>
    public static class ManifestAssetDelivery
    {
        /// <summary>The foreground service of type dataSync that unpacks a downloaded pack.</summary>
        public const string ForegroundService = "com.google.android.play.core.assetpacks.ExtractionForegroundService";

        /// <summary>The service Play binds to when it has a downloaded pack for the game to unpack.</summary>
        public const string ExtractionService = "com.google.android.play.core.assetpacks.AssetPackExtractionService";

        /// <summary>What every class of the library is named under, its services and its receiver among them.</summary>
        private const string LibraryNamespace = "com.google.android.play.core.assetpacks.";

        private const string TypeAttribute = "android:foregroundServiceType";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>A service element as aapt prints it: <c>E: service (line=139)</c>.</summary>
        private static readonly Regex ServiceLine = new Regex(@"^\s*E: service\b", RegexOptions.CultureInvariant);

        /// <summary>A component's name as aapt prints it: <c>A: android:name(0x01010003)="…"</c>.</summary>
        private static readonly Regex NameLine = new Regex(@"^\s*A: android:name\(0x[0-9a-f]+\)=""([^""]+)""", RegexOptions.CultureInvariant);

        /// <summary>The services the library switches on when Unity's player asks it for its AssetPackManager.</summary>
        public static readonly IReadOnlyList<string> SwitchedOnAtStart = new[] { ExtractionService, ForegroundService };

        /// <summary>
        /// The manifest with the foreground service marked to keep, without its type, under its application: a service
        /// named <see cref="ForegroundService"/> with tools:remove="android:foregroundServiceType" and nothing else. A
        /// manifest that has it so already comes back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string KeepWithoutItsType(string manifest)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            XElement application = root.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application> to keep Play Asset Delivery's foreground service under.");
            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
            }

            application.Elements("service").Where(element => (string?)element.Attribute(Android + "name") == ForegroundService).Remove();
            application.Add(new XElement("service", new XAttribute(Android + "name", ForegroundService), new XAttribute(Tools + "remove", TypeAttribute)));

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Stops a bundle's package that lacks a service the library switches on at start-up, which would close the game
        /// on its first start, or that declares a foreground service type, for which Play wants a declaration.
        /// </summary>
        /// <param name="dump">What Android's own tool says of the base APK's manifest: <c>aapt dump xmltree base-master.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package is not as the bundle needs it.</exception>
        public static void RequireKeptWithoutItsTypeIn(string dump)
        {
            RequireADump(dump);
            HashSet<string> declared = ServicesDeclaredIn(dump);
            string[] missing = SwitchedOnAtStart.Where(service => !declared.Contains(service)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest does not declare {string.Join(" and ", missing)}: Play Asset Delivery's library switches it on when Unity's player "
                    + "starts, Android refuses that for a service it does not know, and the game closes at once (versionCode 1, 2026-10-29). The manifest step is to keep it.");
            }

            if (dump.Contains(TypeAttribute))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest gives a service a foreground service type ({TypeAttribute}): Play Console then asks a declaration and a video of it, "
                    + $"and the game runs none. The manifest step is to take the type off {ForegroundService}.");
            }
        }

        /// <summary>Stops a test package that declares anything of the library: one APK has no library, and a service declared there would have no class.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package declares a part of the library.</exception>
        public static void RequireAbsentFrom(string dump)
        {
            RequireADump(dump);
            if (dump.Contains($"\"{LibraryNamespace}"))
            {
                throw new InvalidOperationException(
                    $"The test package's merged manifest declares a part of Play Asset Delivery ({LibraryNamespace}…), which only the Google Play bundle has: "
                    + $"the manifest step is to keep {ForegroundService} in the bundle's build alone.");
            }
        }

        /// <exception cref="InvalidOperationException">The text is not what aapt says of a manifest.</exception>
        private static void RequireADump(string dump)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }
        }

        /// <summary>The names of the services a dump declares: the name line among the attribute lines that follow each service element.</summary>
        private static HashSet<string> ServicesDeclaredIn(string dump)
        {
            var services = new HashSet<string>(StringComparer.Ordinal);
            bool inService = false;
            foreach (string line in dump.Split('\n'))
            {
                if (line.TrimStart().StartsWith("E: ", StringComparison.Ordinal))
                {
                    inService = ServiceLine.IsMatch(line);
                    continue;
                }

                Match name = NameLine.Match(line);
                if (inService && name.Success)
                {
                    services.Add(name.Groups[1].Value);
                }
            }

            return services;
        }
    }
}
