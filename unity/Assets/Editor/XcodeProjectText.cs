#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What an exported Xcode project's project.pbxproj (the old plist text Xcode and Unity write) says of which
    /// targets link a framework, the thing the App Store's purchases rest on. Only what that needs is read: the native
    /// targets with their names and build phases, the frameworks phases with their files, the build files with the
    /// file each stands for, and the file references with their paths. Read as text, without Unity's iOS module
    /// (UnityEditor.iOS.Xcode), so that the repository's tests compile this file too, as they compile
    /// <see cref="InfoPlist"/>.
    /// </summary>
    public sealed class XcodeProjectText
    {
        /// <summary>
        /// One object of a section, on a line of its own or over several: its id, the comment Xcode writes after it, and
        /// its body up to the "};" that ends a line (a body's inner lists end in ");", its inner settings mid-line).
        /// </summary>
        private static readonly Regex ObjectPattern = new Regex(
            @"^[ \t]*(?<id>[0-9A-Za-z]+)(?: /\* (?<comment>[^\n]*?) \*/)? = \{(?<body>.*?)\};[ \t\r]*$",
            RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>An id in a list, with or without its comment, up to its comma.</summary>
        private static readonly Regex ListedIdPattern = new Regex(@"(?<id>[0-9A-Za-z]+)\s*(?:/\*[^\n]*?\*/)?\s*,", RegexOptions.CultureInvariant);

        private readonly string _text;
        private readonly IReadOnlyList<Target> _targets;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _frameworksPhases;
        private readonly IReadOnlyDictionary<string, string> _buildFiles;
        private readonly IReadOnlyDictionary<string, string> _fileReferences;

        private XcodeProjectText(string text)
        {
            _text = text;
            _targets = ObjectsOf("PBXNativeTarget")
                .Select(target => new Target(Unquoted(Setting(target.Body, "name") ?? target.Comment), ListedIds(List(target.Body, "buildPhases"))))
                .ToArray();
            if (_targets.Count == 0)
            {
                throw new InvalidOperationException("The text is not an Xcode project: it has no native target.");
            }

            _frameworksPhases = ObjectsOf("PBXFrameworksBuildPhase").ToDictionary(phase => phase.Id, phase => ListedIds(List(phase.Body, "files")), StringComparer.Ordinal);
            _buildFiles = ObjectsOf("PBXBuildFile")
                .Where(file => Setting(file.Body, "fileRef") != null)
                .ToDictionary(file => file.Id, file => FirstWord(Setting(file.Body, "fileRef")!), StringComparer.Ordinal);
            _fileReferences = ObjectsOf("PBXFileReference")
                .Where(file => Setting(file.Body, "path") != null)
                .ToDictionary(file => file.Id, file => Unquoted(Setting(file.Body, "path")!), StringComparer.Ordinal);
        }

        /// <summary>The names of the project's native targets, in the order they are written.</summary>
        public IReadOnlyList<string> TargetNames => _targets.Select(target => target.Name).ToArray();

        /// <exception cref="InvalidOperationException">The text has no native target: it is not an Xcode project.</exception>
        public static XcodeProjectText Read(string pbxproj) => new XcodeProjectText(pbxproj);

        /// <summary>
        /// The targets whose frameworks phase links the framework, by the file's name ("StoreKit.framework"), in the
        /// order they are written: the path of the file a build file stands for ends in that name.
        /// </summary>
        public IReadOnlyList<string> TargetsLinking(string framework) =>
            _targets.Where(target => target.Phases.Any(phase => _frameworksPhases.TryGetValue(phase, out IReadOnlyList<string>? files) && files.Any(file => IsFramework(file, framework))))
                .Select(target => target.Name)
                .ToArray();

        private bool IsFramework(string buildFile, string framework) =>
            _buildFiles.TryGetValue(buildFile, out string? reference)
            && _fileReferences.TryGetValue(reference, out string? path)
            && (path == framework || path.EndsWith("/" + framework, StringComparison.Ordinal));

        /// <summary>The objects of a section, nothing where the project has no such section.</summary>
        private IEnumerable<(string Id, string Comment, string Body)> ObjectsOf(string isa)
        {
            string begin = $"/* Begin {isa} section */";
            int start = _text.IndexOf(begin, StringComparison.Ordinal);
            int end = start < 0 ? -1 : _text.IndexOf($"/* End {isa} section */", start, StringComparison.Ordinal);
            if (end < 0)
            {
                return Array.Empty<(string, string, string)>();
            }

            string section = _text.Substring(start + begin.Length, end - start - begin.Length);
            return ObjectPattern.Matches(section)
                .Select(match => (match.Groups["id"].Value, match.Groups["comment"].Value, match.Groups["body"].Value))
                .ToArray();
        }

        /// <summary>A setting's value in an object's body ("name = Unity-iPhone;"); nothing where it has none.</summary>
        private static string? Setting(string body, string key)
        {
            Match match = Regex.Match(body, @"(?:^|[\s;{])" + Regex.Escape(key) + @" = (?<value>""(?:[^""\\]|\\.)*""|[^;]+);", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value.Trim() : null;
        }

        /// <summary>A list's items in an object's body ("files = ( ... );"); empty where it has none.</summary>
        private static string List(string body, string key)
        {
            Match match = Regex.Match(body, Regex.Escape(key) + @" = \((?<items>.*?)\);", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["items"].Value : string.Empty;
        }

        private static IReadOnlyList<string> ListedIds(string items) => ListedIdPattern.Matches(items).Select(match => match.Groups["id"].Value).ToArray();

        /// <summary>A value without the comment Xcode writes after it ("1D60 /* Unity-iPhone */" is "1D60").</summary>
        private static string FirstWord(string value) => value.Split(new[] { ' ' }, 2)[0];

        private static string Unquoted(string value) =>
            value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"' ? value.Substring(1, value.Length - 2) : value;

        private sealed class Target
        {
            public Target(string name, IReadOnlyList<string> phases)
            {
                Name = name;
                Phases = phases;
            }

            public string Name { get; }

            public IReadOnlyList<string> Phases { get; }
        }
    }

    /// <summary>
    /// What an exported Xcode project must hold for the App Store's purchases, as Unity In-App Purchasing 5.4.4 writes
    /// it into the project after Unity (AppleCapabilities in com.unity.purchasing): StoreKit linked into the app and
    /// into UnityFramework, where IAP's Swift and Objective-C sources are built. In Unity 6.6 that link is the whole of
    /// the In-App Purchase capability in the project: its Xcode library writes no capability list of the old kind, and
    /// In-App Purchase needs no entitlement (the App ID has it from the start). IAP compiles its step only while the
    /// Editor's active target is iOS: an export made from an Editor set to another target comes out without StoreKit,
    /// and the game would sell nothing (IosBuild's StoreKit step).
    /// </summary>
    public static class StoreKitInTheProject
    {
        public const string Framework = "StoreKit.framework";

        /// <summary>The app's target, as Unity names it in every export.</summary>
        public const string AppTarget = "Unity-iPhone";

        /// <summary>The target that builds the engine and the plugins' native sources, as Unity names it.</summary>
        public const string FrameworkTarget = "UnityFramework";

        /// <summary>What of the purchases' needs the project lacks, one sentence each; empty when it has them all.</summary>
        public static IReadOnlyList<string> Missing(XcodeProjectText project)
        {
            IReadOnlyList<string> linking = project.TargetsLinking(Framework);
            var missing = new List<string>();
            foreach (string target in new[] { AppTarget, FrameworkTarget })
            {
                if (!project.TargetNames.Contains(target))
                {
                    missing.Add($"the project has no target {target}");
                }
                else if (!linking.Contains(target))
                {
                    missing.Add($"{target} does not link {Framework}");
                }
            }

            return missing;
        }
    }
}
