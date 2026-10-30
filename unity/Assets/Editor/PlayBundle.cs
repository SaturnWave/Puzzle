#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Puzzle.Client.Engine;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using Unity.Android.Types;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Builds the Android App Bundle that is uploaded to Google Play, from the command line.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.PlayBundle.Run -logFile out/unity/play-bundle.log
    /// <para>
    /// The bundle is the test package's game (<see cref="AndroidBuild"/>: the same settings, the same manifest step, the
    /// same checks), built as Google Play takes it. The store build: it sells through Google Play (the settings'
    /// build.realMoneyPurchases is "store"; one that sells nothing stops the build), so it has no tester mode, and its
    /// ads are what the settings say (the stand-in card until real ads come). It lands at
    /// D:/puzzle-cache/play/museumpuzzle-vc&lt;versionCode&gt;-&lt;upload|debug&gt;.aab, outside the repository. Google Play
    /// takes a bundle whose base module downloads in at most 200 MB, and the game's data is larger, so Unity's "Split
    /// Application Binary" is turned on for this build: Unity puts its data, the paintings among them, into the
    /// install-time asset pack <see cref="AssetPack"/>, which Play installs together with the base. The native
    /// libraries' symbol tables go into the bundle too (BUNDLE-METADATA, which no phone downloads), so that Play
    /// Console names the functions of a native crash (versionCode 1 had none, 2026-10-28).
    /// </para>
    /// <para>
    /// The versionCode is the next one after the record of uploads (<see cref="PlayUploads"/>); the versionName is
    /// Unity's bundleVersion as it is ("1.0", what the entry page, the cloud save and the attempt records say). Unity
    /// builds the bundle with its debug key, as it builds the test package, and the bundle is then signed again with the
    /// owner's upload key when the keystore and both passwords are there (<see cref="BundleSigning"/>): the debug key's
    /// signature is taken out and Java's jarsigner, the tool Android's documentation names for a bundle, signs it, its
    /// two passwords handed over in its own environment alone. Without the key the bundle keeps the debug key's
    /// signature, which the log and the result line say loudly: Play refuses that bundle. The passwords never reach
    /// Unity: handed to Unity's keystore settings, they were written in plain text into eight files of its build
    /// folder, the Gradle project's build.gradle among them (the first build saw it, 2026-10-28). The settings the
    /// build changes are put back afterwards (<see cref="BundleSettings"/>), so the test package stays one APK.
    /// </para>
    /// <para>
    /// The bundle is then read back. bundletool (<see cref="BundletoolJar"/>, run with Unity's own Java) makes the APKs
    /// Play would make of it, next to it (museumpuzzle-vc&lt;versionCode&gt;-&lt;key&gt;.apks, signed with the debug key, for
    /// a phone: <c>bundletool install-apks</c>); its base APK must carry the package's name and versionCode and pass
    /// the package checks with Android's own tool as a bundle's base, Play Asset Delivery's services declared
    /// (<see cref="AndroidBuild.CheckThePackage"/>, <see cref="ManifestAssetDelivery"/>); every native
    /// library in the bundle must load on 16 KB pages (<see cref="NativeLibraryAlignment"/>); and the base module's
    /// download must be within Play's limit (<see cref="BundleDownloadSize"/>). A bundle that fails is set aside under
    /// another name, out of the way of an upload.
    /// </para>
    /// </summary>
    public static class PlayBundle
    {
        /// <summary>The install-time asset pack Unity makes of its data when a bundle is built with the binary split.</summary>
        internal const string AssetPack = "UnityDataAssetPack";

        /// <summary>bundletool, Google's official build, pinned (github.com/google/bundletool/releases/tag/1.18.3); on drive D:, never in the repository.</summary>
        internal const string BundletoolJar = "D:/puzzle-cache/tools/bundletool-all-1.18.3.jar";

        /// <summary>Where the bundles land, outside the repository.</summary>
        private const string BundleFolder = "D:/puzzle-cache/play";

        /// <summary>Where the base APK is taken out of the APK set to be read; Unity's Temp folder, gone when the Editor closes.</summary>
        private const string WorkFolder = "Temp/PlayBundle";

        /// <summary>Where a bundle keeps its native libraries' symbol tables for Play Console, one folder an ABI.</summary>
        private const string SymbolFolder = "BUNDLE-METADATA/com.android.tools.build.debugsymbols/";

        /// <summary>A native library in the bundle: lib/&lt;abi&gt;/&lt;name&gt;.so under one of its modules.</summary>
        private static readonly Regex NativeLibrary = new Regex(@"^[^/]+/lib/[^/]+/[^/]+\.so$", RegexOptions.CultureInvariant);

        public static void Run()
        {
            try
            {
                ProjectSetup.BringUpToDate();
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException)
            {
                Stop($"not started, because the project's setup failed: {failure.Message}");
                return;
            }

            BundlePlan plan;
            try
            {
                plan = Plan();
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is FormatException || failure is IOException
                || failure is UnauthorizedAccessException || failure is EconomyFormatException)
            {
                Stop($"not started: {failure.Message}");
                return;
            }

            if (plan.Signing is BundleSigning.DebugKey)
            {
                Debug.LogWarning($"Play bundle: signed with {plan.Signing}. THIS BUNDLE IS NOT UPLOADABLE to Google Play; it is built to be checked and tried on a phone only.");
            }

            AndroidBuild.ApplyPlayerSettings();
            AndroidBuild.TakeAwayTheKeptManifest();
            Directory.CreateDirectory(BundleFolder);
            BuildSummary summary;
            using (BundleSettings.For(plan.VersionCode))
            {
                summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MainSceneBuilder.ScenePath },
                    locationPathName = plan.BundlePath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                }).summary;
            }

            if (summary.result != BuildResult.Succeeded)
            {
                Stop($"{summary.result} with {summary.totalErrors} errors.");
                return;
            }

            ReadBack readBack;
            try
            {
                if (plan.Signing is BundleSigning.UploadKey key)
                {
                    SignWithTheUploadKey(plan.BundlePath, key);
                }

                readBack = ReadTheBundleBack(plan);
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException || failure is InvalidDataException
                || failure is FormatException || failure is UnauthorizedAccessException || failure is System.ComponentModel.Win32Exception)
            {
                Stop($"the bundle at {plan.BundlePath} is not to be uploaded{SetAside(plan.BundlePath)}: {failure.Message}");
                return;
            }

            string built = $"versionCode {plan.VersionCode} ({plan.VersionName}), {new FileInfo(plan.BundlePath).Length / (1024 * 1024)} MB at {plan.BundlePath} in {summary.totalTime.TotalMinutes:F1} min; "
                + $"base module {readBack.Base} of Play's {BundleDownloadSize.PlaysBaseLimitBytes / 1000 / 1000} MB, with {AssetPack} {readBack.Installation} at installation; "
                + $"{readBack.Libraries} native libraries load on 16 KB pages; APKs for a phone at {plan.ApkSetPath}";
            switch (plan.Signing)
            {
                case BundleSigning.UploadKey key:
                    Debug.Log($"Play bundle: succeeded, signed with {key}: {built}.");
                    Debug.Log($"Play bundle: once Play Console has taken it, add this line to release/google-play-uploads.txt and commit it: "
                        + PlayUploads.LineOf(new PlayUpload(plan.VersionCode, plan.VersionName, DateTime.Today, PlayTrack.Closed)));
                    break;
                case BundleSigning.DebugKey key:
                    Debug.LogWarning($"Play bundle: succeeded, but signed with Unity's DEBUG key, so it is NOT UPLOADABLE to Google Play ({key.Why}): {built}.");
                    break;
            }
        }

        /// <summary>What this build is to be: its versionCode and versionName, its key and its files.</summary>
        /// <exception cref="InvalidOperationException">The game's settings do not sell through the store.</exception>
        /// <exception cref="FormatException">The record of uploads is not well formed.</exception>
        /// <exception cref="IOException">The record of uploads could not be read.</exception>
        /// <exception cref="EconomyFormatException">The game's settings could not be read.</exception>
        private static BundlePlan Plan()
        {
            EconomyRules rules = EconomyPack.LoadPacked();
            if (rules.Shop.RealMoney != RealMoneySales.ThroughTheStore)
            {
                throw new InvalidOperationException(
                    $"the game's settings sell {rules.Shop.RealMoney} (economy/default.json, build.realMoneyPurchases): the bundle for Google Play is the store build, "
                    + "which sells through the store and has no tester mode");
            }

            Debug.Log($"Play bundle: the store build: it sells through Google Play, its ads are {rules.Ads}, it has no tester mode.");
            int versionCode = PlayUploads.NextVersionCode(PlayUploads.Read(File.ReadAllText(PlayUploads.RecordPath)));
            string keystore = Variable(BundleSigning.KeystoreVariable) ?? BundleSigning.DefaultKeystorePath;
            string? storePassword = Variable(BundleSigning.StorePasswordVariable);
            string? keyPassword = Variable(BundleSigning.KeyPasswordVariable);
            BundleSigning signing = BundleSigning.Choose(keystore, File.Exists(keystore), storePassword, keyPassword);
            Debug.Log($"Play bundle: versionCode {versionCode}, the next after {PlayUploads.RecordPath}; the keystore {(File.Exists(keystore) ? "is" : "is not")} at {keystore}, "
                + $"{BundleSigning.StorePasswordVariable} is {(storePassword == null ? "not set" : "set")}, {BundleSigning.KeyPasswordVariable} is {(keyPassword == null ? "not set" : "set")}; "
                + $"signing with {signing}.");
            string bundle = $"{BundleFolder}/museumpuzzle-vc{versionCode}-{signing.FileWord}.aab";
            return new BundlePlan(versionCode, PlayerSettings.bundleVersion, signing, bundle);
        }

        /// <summary>
        /// An environment variable's value: the process's, or, where the process has none, the user's, which Windows
        /// keeps for processes started later (a variable the owner set after the Editor started). None when neither
        /// has a value; the value itself is never logged.
        /// </summary>
        private static string? Variable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Signs the bundle Unity built with its debug key again, with the upload key alone: the debug key's signature
        /// files are taken out of META-INF, jarsigner signs (SHA-256 with RSA), its own check verifies the bundle, and
        /// META-INF must then hold the upload key's signature and no other. The passwords go to jarsigner in its
        /// environment (<c>-storepass:env</c>), never on its command line.
        /// </summary>
        /// <exception cref="InvalidOperationException">jarsigner failed, or the bundle is not signed by the upload key alone.</exception>
        /// <exception cref="IOException">The bundle could not be rewritten.</exception>
        private static void SignWithTheUploadKey(string bundle, BundleSigning.UploadKey key)
        {
            int debugSignature;
            using (ZipArchive archive = ZipFile.Open(bundle, ZipArchiveMode.Update))
            {
                ZipArchiveEntry[] signature = archive.Entries.Where(entry => BundleSigning.IsSignatureFile(entry.FullName)).ToArray();
                foreach (ZipArchiveEntry entry in signature)
                {
                    entry.Delete();
                }

                debugSignature = signature.Length;
            }

            string jarsigner = JdkTool("jarsigner");
            string full = Path.GetFullPath(bundle);
            WhatToolSays(
                jarsigner,
                $"-keystore \"{key.KeystorePath}\" -storepass:env {BundleSigning.StorePasswordVariable} -keypass:env {BundleSigning.KeyPasswordVariable} "
                + $"-sigalg SHA256withRSA -digestalg SHA-256 \"{full}\" {BundleSigning.Alias}",
                new Dictionary<string, string> { [BundleSigning.StorePasswordVariable] = key.StorePassword, [BundleSigning.KeyPasswordVariable] = key.KeyPassword });
            string verified = WhatToolSays(jarsigner, $"-verify \"{full}\"");
            if (!verified.Contains("jar verified."))
            {
                throw new InvalidOperationException($"jarsigner does not verify the signed bundle: {verified.Trim()}");
            }

            string[] signatureNow;
            using (ZipArchive archive = ZipFile.OpenRead(bundle))
            {
                signatureNow = archive.Entries.Select(entry => entry.FullName).Where(BundleSigning.IsSignatureFile).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }

            if (!signatureNow.SequenceEqual(BundleSigning.UploadSignatureFiles))
            {
                throw new InvalidOperationException(
                    $"the signed bundle's META-INF holds {string.Join(", ", signatureNow)}, where the upload key's signature alone is {string.Join(", ", BundleSigning.UploadSignatureFiles)}");
            }

            Debug.Log($"Play bundle: signed with {key} by jarsigner, which verifies it; the debug key's {debugSignature} signature files were taken out first, "
                + $"and META-INF holds {string.Join(", ", signatureNow)}.");
        }

        /// <summary>
        /// The bundle read back: the APKs Play would make of it, their base APK's identity and package checks, its
        /// native libraries' alignment and its download sizes.
        /// </summary>
        /// <exception cref="InvalidOperationException">bundletool or aapt failed, or the bundle is not as it is meant to be.</exception>
        /// <exception cref="InvalidDataException">A native library is not a library.</exception>
        /// <exception cref="FormatException">bundletool's size is not what it says.</exception>
        private static ReadBack ReadTheBundleBack(BundlePlan plan)
        {
            Bundletool($"build-apks --bundle=\"{Path.GetFullPath(plan.BundlePath)}\" --output=\"{Path.GetFullPath(plan.ApkSetPath)}\" --overwrite");
            Debug.Log($"Play bundle: bundletool made the APKs of every kind of phone at {plan.ApkSetPath}.");

            string baseApk = TakeOutTheBaseApk(plan.ApkSetPath);
            string identity = AndroidBuild.WhatAaptSays(baseApk, "badging").Split('\n')[0].Trim();
            if (!identity.Contains($"name='{AndroidBuild.PackageId}'") || !identity.Contains($"versionCode='{plan.VersionCode}'") || !identity.Contains($"versionName='{plan.VersionName}'"))
            {
                throw new InvalidOperationException($"the base APK says \"{identity}\", where the bundle is {AndroidBuild.PackageId}, versionCode {plan.VersionCode}, versionName {plan.VersionName}");
            }

            Debug.Log($"Play bundle: the base APK says \"{identity}\".");
            AndroidBuild.CheckThePackage(baseApk, BuiltPackage.BundleBase);

            IReadOnlyDictionary<string, long> alignments = NativeLibraryAlignments(plan.BundlePath);
            NativeLibraryAlignment.Require(alignments);
            Debug.Log($"Play bundle: every native library loads on 16 KB pages: {string.Join(", ", alignments.Select(library => $"{library.Key} {library.Value / 1024} KB"))}.");
            string[] symbols = SymbolFiles(plan.BundlePath);
            if (symbols.Length == 0)
            {
                Debug.LogWarning($"Play bundle: the bundle carries no symbol tables under {SymbolFolder}: Play Console will name no function of a native crash.");
            }
            else
            {
                Debug.Log($"Play bundle: the bundle carries the symbol tables of {string.Join(", ", symbols)}, for Play Console's crash reports.");
            }

            BundleDownloadSize baseModule = BundleDownloadSize.Parse(Bundletool($"get-size total --apks=\"{Path.GetFullPath(plan.ApkSetPath)}\" --modules=base"));
            if (!baseModule.IsWithinPlaysBaseLimit)
            {
                throw new InvalidOperationException(
                    $"the base module downloads in up to {baseModule}, beyond Google Play's {BundleDownloadSize.PlaysBaseLimitBytes / 1000 / 1000} MB: what is big belongs in {AssetPack}");
            }

            // The asset pack is not reckoned alone: bundletool counts the base with it, as Play installs them together.
            BundleDownloadSize installation = BundleDownloadSize.Parse(Bundletool($"get-size total --apks=\"{Path.GetFullPath(plan.ApkSetPath)}\""));
            return new ReadBack(baseModule, installation, alignments.Count);
        }

        /// <summary>The APK set's base APK (splits/base-master.apk), whose manifest is the app's whole merged manifest, taken out into <see cref="WorkFolder"/>.</summary>
        /// <exception cref="InvalidOperationException">The set has no base APK.</exception>
        private static string TakeOutTheBaseApk(string apkSet)
        {
            const string BaseApk = "splits/base-master.apk";
            Directory.CreateDirectory(WorkFolder);
            string taken = Path.Combine(WorkFolder, "base-master.apk");
            using ZipArchive set = ZipFile.OpenRead(apkSet);
            ZipArchiveEntry entry = set.GetEntry(BaseApk) ?? throw new InvalidOperationException($"bundletool's APK set {apkSet} has no {BaseApk}");
            entry.ExtractToFile(taken, overwrite: true);
            return taken;
        }

        /// <summary>The files under <see cref="SymbolFolder"/>, by their names there: one symbol table a native library.</summary>
        private static string[] SymbolFiles(string bundle)
        {
            using ZipArchive archive = ZipFile.OpenRead(bundle);
            return archive.Entries.Where(entry => entry.FullName.StartsWith(SymbolFolder, StringComparison.Ordinal) && entry.Length > 0)
                .Select(entry => entry.FullName.Substring(SymbolFolder.Length)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Every native library in the bundle, by its path there, with the smallest alignment of its loadable segments.</summary>
        /// <exception cref="InvalidDataException">A library is not a 64-bit ELF library.</exception>
        private static IReadOnlyDictionary<string, long> NativeLibraryAlignments(string bundle)
        {
            var alignments = new SortedDictionary<string, long>(StringComparer.Ordinal);
            using ZipArchive archive = ZipFile.OpenRead(bundle);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => NativeLibrary.IsMatch(entry.FullName)))
            {
                using Stream library = entry.Open();
                try
                {
                    alignments.Add(entry.FullName, NativeLibraryAlignment.SmallestLoadAlignment(library));
                }
                catch (InvalidDataException failure)
                {
                    throw new InvalidDataException($"{entry.FullName}: {failure.Message}", failure);
                }
            }

            return alignments;
        }

        /// <summary>What bundletool says, run with Unity's own Java.</summary>
        /// <exception cref="InvalidOperationException">bundletool is missing, did not start, or ended with an error.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">Java could not be started.</exception>
        private static string Bundletool(string arguments)
        {
            if (!File.Exists(BundletoolJar))
            {
                throw new InvalidOperationException(
                    $"no bundletool at {BundletoolJar}: put Google's bundletool-all-1.18.3.jar there, from github.com/google/bundletool/releases/tag/1.18.3 (docs/PLAY_RELEASE.md)");
            }

            return WhatToolSays(JdkTool("java"), $"-jar \"{BundletoolJar}\" {arguments}");
        }

        /// <summary>A tool of Unity's own Java: java, jarsigner.</summary>
        private static string JdkTool(string name) =>
            Path.Combine(AndroidExternalToolsSettings.jdkRootPath, "bin", Application.platform == RuntimePlatform.WindowsEditor ? name + ".exe" : name);

        /// <summary>
        /// What a tool says on its standard output. Its complaints are read while it runs, so a long one cannot stop it;
        /// a tool that ends with an error stops the read-back with what it said. Variables given are set in the tool's
        /// environment alone, and are never written into what this says.
        /// </summary>
        /// <exception cref="InvalidOperationException">The tool did not start, or ended with an error.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">The tool could not be started.</exception>
        private static string WhatToolSays(string tool, string arguments, IReadOnlyDictionary<string, string>? environment = null)
        {
            var running = new ProcessStartInfo(tool, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (KeyValuePair<string, string> variable in environment ?? new Dictionary<string, string>())
            {
                running.EnvironmentVariables[variable.Key] = variable.Value;
            }

            using Process process = Process.Start(running) ?? throw new InvalidOperationException($"{tool} did not start");
            Task<string> complaint = process.StandardError.ReadToEndAsync();
            string said = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(tool)} ended with exit {process.ExitCode}: {(complaint.Result + " " + said).Trim()}");
            }

            return said;
        }

        /// <summary>
        /// Moves a bundle that failed its signing or its read-back out of the way of an upload: to the same name with
        /// ".rejected" after it, which Play Console's file picker does not offer, kept for a look at what failed. Says
        /// where it went.
        /// </summary>
        private static string SetAside(string bundle)
        {
            string rejected = bundle + ".rejected";
            try
            {
                File.Delete(rejected);
                File.Move(bundle, rejected);
                return $" (set aside as {rejected})";
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            {
                return $" (it could not be set aside, {failure.Message}: delete it by hand)";
            }
        }

        private static void Stop(string why)
        {
            Debug.LogError($"Play bundle: {why}");
            EditorApplication.Exit(1);
        }

        /// <summary>What one build of the bundle is: its versionCode and versionName, its key, and where its files go.</summary>
        private sealed class BundlePlan
        {
            public BundlePlan(int versionCode, string versionName, BundleSigning signing, string bundlePath)
            {
                VersionCode = versionCode;
                VersionName = versionName;
                Signing = signing;
                BundlePath = bundlePath;
            }

            public int VersionCode { get; }

            public string VersionName { get; }

            public BundleSigning Signing { get; }

            /// <summary>The bundle: D:/puzzle-cache/play/museumpuzzle-vc1-upload.aab for one.</summary>
            public string BundlePath { get; }

            /// <summary>The APKs bundletool makes of the bundle, beside it.</summary>
            public string ApkSetPath => Path.ChangeExtension(BundlePath, ".apks");
        }

        /// <summary>What the read-back found: the base module's download, the whole installation's (the base with the asset pack), and how many native libraries were read.</summary>
        private sealed class ReadBack
        {
            public ReadBack(BundleDownloadSize baseModule, BundleDownloadSize installation, int libraries)
            {
                Base = baseModule;
                Installation = installation;
                Libraries = libraries;
            }

            public BundleDownloadSize Base { get; }

            public BundleDownloadSize Installation { get; }

            public int Libraries { get; }
        }

        /// <summary>
        /// The settings the bundle's build changes, for that build alone, and put back as they were when it ends,
        /// whichever way it ends: the bundle and the binary split on, the versionCode, Unity's debug key (no keystore
        /// of the project's), and the symbol tables in the bundle. ProjectSettings.asset keeps the test package's: one
        /// APK, versionCode 1, Unity's debug key.
        /// </summary>
        private sealed class BundleSettings : IDisposable
        {
            private readonly bool _appBundle = EditorUserBuildSettings.buildAppBundle;
            private readonly bool _split = PlayerSettings.Android.splitApplicationBinary;
            private readonly int _versionCode = PlayerSettings.Android.bundleVersionCode;
            private readonly bool _customKeystore = PlayerSettings.Android.useCustomKeystore;
            private readonly DebugSymbolLevel _symbols = UnityEditor.Android.UserBuildSettings.DebugSymbols.level;
            private readonly DebugSymbolFormat _symbolsFormat = UnityEditor.Android.UserBuildSettings.DebugSymbols.format;

            private BundleSettings()
            {
            }

            /// <summary>The settings for a bundle of this versionCode, to be put back as the project had them when this is disposed.</summary>
            public static BundleSettings For(int versionCode)
            {
                var kept = new BundleSettings();
                EditorUserBuildSettings.buildAppBundle = true;
                PlayerSettings.Android.splitApplicationBinary = true;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.Android.useCustomKeystore = false;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.level = DebugSymbolLevel.SymbolTable;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.format = DebugSymbolFormat.IncludeInBundle;
                return kept;
            }

            public void Dispose()
            {
                UnityEditor.Android.UserBuildSettings.DebugSymbols.format = _symbolsFormat;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.level = _symbols;
                PlayerSettings.Android.useCustomKeystore = _customKeystore;
                PlayerSettings.Android.bundleVersionCode = _versionCode;
                PlayerSettings.Android.splitApplicationBinary = _split;
                EditorUserBuildSettings.buildAppBundle = _appBundle;
            }
        }
    }
}
