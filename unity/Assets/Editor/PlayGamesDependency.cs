#nullable enable
using System;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The Android library Google's Play Games plugin for Unity 2.3.0 needs: Play Games Services v2 at the version the
    /// plugin's own dependency file names (GooglePlayGamesPluginDependencies.xml: play-services-games-v2 22.1.0, from
    /// Google's Maven repository, which Unity's Gradle project lists already). The plugin asks Google's External Dependency
    /// Manager (EDM4U) to fetch it; the game leaves EDM4U and the plugin's Editor folder out
    /// (unity/Packages/com.google.play.games keeps the plugin's runtime only) and declares the library the way Unity In-App
    /// Purchasing declares its own: a marked section at the end of the Unity library's build.gradle, written anew at every
    /// build (<see cref="Into"/>). The plugin's second library, play-services-nearby, is for Nearby Connections, which the
    /// game does not use, and is not declared.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class PlayGamesDependency
    {
        /// <summary>The library, as Gradle names it.</summary>
        public const string Library = "com.google.android.gms:play-services-games-v2:22.1.0";

        private const string SectionStart = "// Dependencies for \"com.google.play.games\" (the game's AndroidBuild). This section is written at every build.";
        private const string SectionEnd = "// End of dependencies for \"com.google.play.games\".";

        /// <summary>
        /// The build.gradle with the section that declares the library: in the place of the section it had, or at its end.
        /// A build.gradle that has the section so already comes back unchanged.
        /// </summary>
        /// <exception cref="InvalidOperationException">The file has the section's start without its end: it was cut, and is not touched.</exception>
        public static string Into(string buildGradle)
        {
            string section = SectionStart + "\n"
                + "afterEvaluate {\n"
                + "    dependencies {\n"
                + $"        implementation '{Library}'\n"
                + "    }\n"
                + "}\n"
                + SectionEnd;
            int start = buildGradle.IndexOf(SectionStart, StringComparison.Ordinal);
            if (start < 0)
            {
                return buildGradle + "\n" + section + "\n";
            }

            int end = buildGradle.IndexOf(SectionEnd, start, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new InvalidOperationException("The Unity library's build.gradle has the start of the Play Games section and not its end.");
            }

            return buildGradle.Substring(0, start) + section + buildGradle.Substring(end + SectionEnd.Length);
        }
    }
}
