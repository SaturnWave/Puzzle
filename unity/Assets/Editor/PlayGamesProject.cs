#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The game's Google Play Games project as a build carries it (docs/research/server-v1.md §9 B2–B4, Stage 6): the
    /// project's id, the number Play Console's Play Games Services configuration shows (it is the Cloud project's number),
    /// and the id of its "Game server" OAuth client, which the game's sign-in asks Google for a one-time server code with
    /// (Cloud/Ugs/PlayGamesSignIn.cs). Neither is a secret; the web client's secret is entered in Unity's dashboard by the
    /// owner and never reaches the repository. Both come from the Play Games plugin's settings file
    /// (unity/Assets/Resources/PlayGamesSettings.asset). A closed set (private constructor): not set up (both empty), set
    /// up, or refused (one without the other, or not of the form Google gives).
    /// <para>
    /// Google Play's services read the project id from the installed game's manifest
    /// (<see cref="AppIdName"/>, a text they read with getString and turn into a number: bytecode of play-services-games-v2
    /// 22.1.0 read 2026-11-02, appshortcuts.zzt). The plugin's own Editor set-up writes it there; this project carries only
    /// the plugin's runtime, so the build's manifest step does (<see cref="Declare"/>), as "\u003" followed by the id: the
    /// plugin's way of making Android keep a number as text. The built package is read back for it
    /// (<see cref="RequireDeclaredIn"/>). This file has no Unity dependency, so the repository's tests compile it too.
    /// </para>
    /// </summary>
    public abstract class PlayGamesProject
    {
        /// <summary>The manifest's meta-data Google Play's services read the project id from.</summary>
        public const string AppIdName = "com.google.android.gms.games.APP_ID";

        private const string WebClientEnd = ".apps.googleusercontent.com";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

        /// <summary>A project id: the Cloud project's number, digits only.</summary>
        private static readonly Regex ProjectNumber = new Regex(@"^[1-9][0-9]{5,19}$", RegexOptions.CultureInvariant);

        /// <summary>A web client id as Google gives it: the project's number, a dash, the client's own part, Google's ending.</summary>
        private static readonly Regex WebClient = new Regex(@"^(?<project>[0-9]+)-[a-z0-9]+\.apps\.googleusercontent\.com$", RegexOptions.CultureInvariant);

        private PlayGamesProject()
        {
        }

        /// <summary>What the plugin's settings say: their project id and web client id, as the settings file holds them.</summary>
        public static PlayGamesProject Read(string? appId, string? webClientId)
        {
            string app = appId?.Trim() ?? string.Empty;
            string web = webClientId?.Trim() ?? string.Empty;
            if (app.Length == 0 && web.Length == 0)
            {
                return new NotSetUp();
            }

            if (!ProjectNumber.IsMatch(app))
            {
                return new Refused($"the project id \"{app}\" is not the number Play Console's Play Games Services configuration shows");
            }

            Match client = WebClient.Match(web);
            if (!client.Success)
            {
                return new Refused($"the web client id \"{web}\" is not an OAuth client id of the form <number>-<letters>{WebClientEnd}");
            }

            return client.Groups["project"].Value == app
                ? new SetUp(app, web)
                : (PlayGamesProject)new Refused($"the web client id belongs to the project {client.Groups["project"].Value}, not to the Play Games project {app}");
        }

        /// <summary>
        /// The manifest with the project id under its application: a meta-data named <see cref="AppIdName"/> whose value
        /// is "\u003" and the id, and no other of that name. A manifest that has it so already comes back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string Declare(string manifest, SetUp project)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            XElement application = root.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application> to give the Play Games project id under.");
            application.Elements("meta-data").Where(element => (string?)element.Attribute(Android + "name") == AppIdName).Remove();
            application.Add(new XElement("meta-data", new XAttribute(Android + "name", AppIdName), new XAttribute(Android + "value", @"\u003" + project.AppId)));

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Stops a package whose manifest does not hold the project id as text: Google Play's services would not know the game's Play Games project.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree … AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or it does not hold the id so.</exception>
        public static void RequireDeclaredIn(string dump, SetUp project)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }

            // aapt prints a meta-data's name and then its value on the attribute lines that follow the element.
            string[] lines = dump.Split('\n');
            int named = Array.FindIndex(lines, line => line.Contains($"=\"{AppIdName}\""));
            string? value = named < 0 ? null : lines.Skip(named + 1).TakeWhile(line => line.TrimStart().StartsWith("A: ", StringComparison.Ordinal))
                .FirstOrDefault(line => line.TrimStart().StartsWith("A: android:value(", StringComparison.Ordinal));
            if (value == null || !value.Contains($")=\"{project.AppId}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest does not hold {AppIdName} as the text \"{project.AppId}\" ({value?.Trim() ?? "no such meta-data"}): Google Play's services would not "
                    + "know the game's Play Games project, and the sign-in would fail. The manifest step is to write it.");
            }
        }

        /// <summary>The settings hold neither id: the build has no Play Games sign-in, and its account stays on the phone.</summary>
        public sealed class NotSetUp : PlayGamesProject
        {
            public override string ToString() => "no Play Games project";
        }

        /// <summary>The settings hold both ids, of the forms Google gives, of one project.</summary>
        public sealed class SetUp : PlayGamesProject
        {
            public SetUp(string appId, string webClientId)
            {
                AppId = appId;
                WebClientId = webClientId;
            }

            public string AppId { get; }

            public string WebClientId { get; }

            public override string ToString() => $"the Play Games project {AppId} with the web client {WebClientId}";
        }

        /// <summary>The settings hold ids the build does not take, and why.</summary>
        public sealed class Refused : PlayGamesProject
        {
            public Refused(string why)
            {
                Why = why ?? throw new ArgumentNullException(nameof(why));
            }

            public string Why { get; }

            public override string ToString() => $"a refused Play Games project: {Why}";
        }
    }
}
