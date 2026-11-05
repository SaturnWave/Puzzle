#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The key a Google Play bundle is signed with. Google Play App Signing holds the app's own signing key and signs
    /// what players install with it; what is uploaded is signed with the owner's upload key, which only proves to Play
    /// that the upload is the owner's. The upload key is a keystore the owner made with keytool
    /// (docs/PLAY_RELEASE.md), at <see cref="DefaultKeystorePath"/> or where <see cref="KeystoreVariable"/> says, with
    /// its key under the alias <see cref="Alias"/>; its two passwords are in the owner's environment variables
    /// <see cref="StorePasswordVariable"/> and <see cref="KeyPasswordVariable"/>, which no file of the repository holds.
    /// With all three there, the bundle is signed with the upload key; without one of them, with Unity's debug key, a
    /// bundle Play refuses, built only to be checked and tried on a phone. A closed set: <see cref="UploadKey"/> or
    /// <see cref="DebugKey"/>. Neither says a password in its text, so either can be logged.
    /// <para>
    /// Unity always builds with its debug key; the upload key's signature replaces the debug key's afterwards: the
    /// bundle's signature files (<see cref="IsSignatureFile"/>) are taken out and jarsigner signs it, after which they
    /// are <see cref="UploadSignatureFiles"/> and no others (unity/Assets/Editor/PlayBundle.cs). The passwords go to
    /// jarsigner alone, never to Unity, which wrote them in plain text into its build folder when it was given them.
    /// </para>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public abstract class BundleSigning
    {
        /// <summary>A bundle's signature files once jarsigner has signed it with the key of alias <see cref="Alias"/>, in ordinal order.</summary>
        public static readonly IReadOnlyList<string> UploadSignatureFiles = new[] { "META-INF/MANIFEST.MF", "META-INF/UPLOAD.RSA", "META-INF/UPLOAD.SF" };

        private static readonly string[] SignatureBlockEndings = { ".SF", ".RSA", ".DSA", ".EC" };

        /// <summary>Where the owner's upload keystore is, unless <see cref="KeystoreVariable"/> says otherwise: on drive D:, outside the repository.</summary>
        public const string DefaultKeystorePath = "D:/puzzle-cache/keys/museumpuzzle-upload.keystore";

        /// <summary>The alias of the upload key in the keystore.</summary>
        public const string Alias = "upload";

        /// <summary>The environment variable that may name another keystore.</summary>
        public const string KeystoreVariable = "PUZZLE_UPLOAD_KEYSTORE";

        /// <summary>The environment variable that holds the keystore's password.</summary>
        public const string StorePasswordVariable = "PUZZLE_UPLOAD_STORE_PASS";

        /// <summary>The environment variable that holds the upload key's own password.</summary>
        public const string KeyPasswordVariable = "PUZZLE_UPLOAD_KEY_PASS";

        private BundleSigning()
        {
        }

        /// <summary>The name of the bundle's file says which key signed it: "upload" or "debug".</summary>
        public abstract string FileWord { get; }

        /// <summary>
        /// Whether a file of the bundle (a zip entry's full name) belongs to a JAR signature, the way a bundle is signed:
        /// META-INF/MANIFEST.MF, and a signer's .SF file with its .RSA, .DSA or .EC block, directly under META-INF. Other
        /// files of META-INF (a library's version file, services) are not.
        /// </summary>
        public static bool IsSignatureFile(string entryName)
        {
            const string Folder = "META-INF/";
            if (entryName == null || !entryName.StartsWith(Folder, StringComparison.OrdinalIgnoreCase) || entryName.IndexOf('/', Folder.Length) >= 0)
            {
                return false;
            }

            string name = entryName.Substring(Folder.Length);
            return string.Equals(name, "MANIFEST.MF", StringComparison.OrdinalIgnoreCase)
                || SignatureBlockEndings.Any(ending => name.Length > ending.Length && name.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The key the bundle is signed with: the upload key when the keystore is there and both passwords are set (an
        /// empty one is not set), Unity's debug key otherwise, saying which of the three was missing.
        /// </summary>
        /// <param name="keystorePath">The keystore's path: <see cref="KeystoreVariable"/>'s value, or <see cref="DefaultKeystorePath"/>.</param>
        /// <param name="keystoreIsThere">Whether a file is at that path.</param>
        /// <param name="storePassword"><see cref="StorePasswordVariable"/>'s value; none when it is not set.</param>
        /// <param name="keyPassword"><see cref="KeyPasswordVariable"/>'s value; none when it is not set.</param>
        public static BundleSigning Choose(string keystorePath, bool keystoreIsThere, string? storePassword, string? keyPassword)
        {
            if (string.IsNullOrWhiteSpace(keystorePath))
            {
                throw new ArgumentException("A keystore has a path.", nameof(keystorePath));
            }

            var missing = new List<string>();
            if (!keystoreIsThere)
            {
                missing.Add($"no keystore at {keystorePath}");
            }

            if (string.IsNullOrEmpty(storePassword))
            {
                missing.Add($"{StorePasswordVariable} is not set");
            }

            if (string.IsNullOrEmpty(keyPassword))
            {
                missing.Add($"{KeyPasswordVariable} is not set");
            }

            return missing.Count == 0
                ? new UploadKey(keystorePath, storePassword!, keyPassword!)
                : new DebugKey(string.Join("; ", missing));
        }

        /// <summary>The owner's upload key: a bundle Google Play takes.</summary>
        public sealed class UploadKey : BundleSigning
        {
            internal UploadKey(string keystorePath, string storePassword, string keyPassword)
            {
                KeystorePath = keystorePath;
                StorePassword = storePassword;
                KeyPassword = keyPassword;
            }

            public string KeystorePath { get; }

            /// <summary>The keystore's password, handed to jarsigner in its environment alone; never written to a log or a file.</summary>
            public string StorePassword { get; }

            /// <summary>The key's password, handed to jarsigner in its environment alone; never written to a log or a file.</summary>
            public string KeyPassword { get; }

            public override string FileWord => "upload";

            public override string ToString() => $"the upload key (alias \"{Alias}\" in {KeystorePath})";
        }

        /// <summary>Unity's debug key: a bundle Google Play refuses, to be checked and tried on a phone only.</summary>
        public sealed class DebugKey : BundleSigning
        {
            internal DebugKey(string why)
            {
                Why = why;
            }

            /// <summary>Which of the keystore and the two passwords was missing.</summary>
            public string Why { get; }

            public override string FileWord => "debug";

            public override string ToString() => $"Unity's debug key, because {Why}";
        }
    }
}
