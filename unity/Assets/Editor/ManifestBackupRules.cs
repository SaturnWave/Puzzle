#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What Android's own backup keeps of the game (2026-10-27, T566), on the owner's decision that it stays on (A29,
    /// 2026-10-16). Unity Authentication 3.8.1 keeps the anonymous account's session in Unity's PlayerPrefs, the
    /// shared-preferences file <c>&lt;package&gt;.v2.playerprefs.xml</c> (Cloud/Ugs/UgsAccounts.cs), so a phone restored
    /// from Google's backup, or the game installed again from Google Play on a phone whose backup holds it, signs in to
    /// the same account and finds its cloud save. Android backs up an application's whole data by default; these rules
    /// name what it keeps instead, and nothing else: that file, and the game's four files in its data folder
    /// (Application.persistentDataPath is getExternalFilesDir on Android, the backup's "external" domain): the progress,
    /// the cloud save's state, the phone's settings and id, and the attempt records. The progress file and cloud.json go
    /// together, so the merge's base and the phone's progress come back from the same moment (Cloud/CloudSync tells a
    /// state the installation's later writes outran).
    /// <para>
    /// Left out, so never restored: Unity's and the libraries' other files and databases (the logging transport's
    /// database of earlier builds, Remote Config's cache, the purchase library's), the files a damaged progress or state
    /// is set aside as, the files half written (the two-step writes' new files), and Android's caches, which it never
    /// backs up anyway. The same rules hold for Google's cloud backup and for a transfer from phone to phone: Android 12
    /// and later read <see cref="DataExtractionRules"/>, Android 11 and earlier <see cref="FullBackupContent"/>.
    /// </para>
    /// The manifest step marks the application with both (<see cref="Apply"/>) and writes the two files into the Unity
    /// library's resources; the built package is read back (<see cref="RequireIn"/>). Android backs up about once a day,
    /// on Wi-Fi, idle and with the player's backup on; a restore comes only with an installation from Google Play or a
    /// phone set up from a backup, never with <c>adb install</c>. This file has no Unity dependency, so the repository's
    /// tests compile it too.
    /// </summary>
    public static class ManifestBackupRules
    {
        /// <summary>The resource Android 11 and earlier read: res/xml/game_backup_rules.xml.</summary>
        public const string FullBackupContentResource = "game_backup_rules";

        /// <summary>The resource Android 12 and later read: res/xml/game_data_extraction_rules.xml.</summary>
        public const string DataExtractionRulesResource = "game_data_extraction_rules";

        /// <summary>The game's own files in its data folder that the backup keeps.</summary>
        public static readonly IReadOnlyList<string> GameFiles = new[] { "progress.json", "cloud.json", "device.json", "attempts.jsonl" };

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <summary>Unity's PlayerPrefs file of the package on Android, in the shared preferences' folder.</summary>
        public static string PlayerPrefsFile(string packageId) =>
            string.IsNullOrEmpty(packageId) ? throw new ArgumentException("A package has a name.", nameof(packageId)) : packageId + ".v2.playerprefs.xml";

        /// <summary>The rules of Android 11 and earlier: the PlayerPrefs file and the game's files, nothing else.</summary>
        public static string FullBackupContent(string packageId) =>
            Document(new XElement("full-backup-content", Includes(packageId)));

        /// <summary>The rules of Android 12 and later, the same for Google's cloud backup and for a transfer from phone to phone.</summary>
        public static string DataExtractionRules(string packageId) =>
            Document(new XElement("data-extraction-rules", new XElement("cloud-backup", Includes(packageId)), new XElement("device-transfer", Includes(packageId))));

        /// <summary>
        /// The manifest with its application backed up by these rules: backup on (Android's default, said so the rules
        /// have something to rule), and both rules' resources named. A manifest that names them so already comes back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application, or it turns the backup off.</exception>
        public static string Apply(string manifest)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement application = (document.Root is XElement root && root.Name.LocalName == "manifest" ? root.Element("application") : null)
                ?? throw new InvalidOperationException("The file is not an Android manifest with an <application> to give the backup rules to.");
            if ((string?)application.Attribute(Android + "allowBackup") == "false")
            {
                throw new InvalidOperationException("The manifest turns Android's backup off, which the owner keeps on (A29); the backup rules would rule nothing.");
            }

            application.SetAttributeValue(Android + "allowBackup", "true");
            application.SetAttributeValue(Android + "fullBackupContent", "@xml/" + FullBackupContentResource);
            application.SetAttributeValue(Android + "dataExtractionRules", "@xml/" + DataExtractionRulesResource);
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Stops a package whose manifest does not keep the backup on with both rules: a restore would bring back what Android chose.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package lacks the backup or one of the rules.</exception>
        public static void RequireIn(string dump)
        {
            if (dump == null || !dump.Contains("E: application"))
            {
                throw new InvalidOperationException("the manifest's dump names no application to read the backup rules of");
            }

            string[] application = dump.Split('\n').SkipWhile(line => !line.Contains("E: application")).Skip(1).TakeWhile(line => line.Contains("A: ")).ToArray();
            bool backsUp = application.Any(line => line.Contains("android:allowBackup(") && line.Contains("0xffffffff"));
            bool namesTheOldRules = application.Any(line => line.Contains("android:fullBackupContent(") && line.Contains("=@0x"));
            bool namesTheNewRules = application.Any(line => line.Contains("android:dataExtractionRules(") && line.Contains("=@0x"));
            if (!backsUp || !namesTheOldRules || !namesTheNewRules)
            {
                throw new InvalidOperationException(
                    $"the package's application {(backsUp ? "backs up" : "does not back up")}, {(namesTheOldRules ? "names" : "does not name")} its rules for Android 11 and earlier "
                    + $"and {(namesTheNewRules ? "names" : "does not name")} those for Android 12 and later");
            }
        }

        private static IEnumerable<XElement> Includes(string packageId) =>
            new[] { Include("sharedpref", PlayerPrefsFile(packageId)) }.Concat(GameFiles.Select(file => Include("external", file)));

        private static XElement Include(string domain, string path) => new XElement("include", new XAttribute("domain", domain), new XAttribute("path", path));

        private static string Document(XElement rules) =>
            new XDeclaration("1.0", "utf-8", null) + "\n"
            + "<!-- Written by the Android build's manifest step (unity/Assets/Editor/ManifestBackupRules.cs): what Android's backup keeps of the game. -->\n"
            + rules + "\n";
    }
}
