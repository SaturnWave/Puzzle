using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MuseumPuzzle.Server.Platform;

namespace MuseumPuzzle.Server.AppStore
{
    /// <summary>A text that should be Apple's signed transaction is not: its signature, its certificates or its layout. The message says what failed.</summary>
    public sealed class AppleSignatureException : Exception
    {
        public AppleSignatureException(string message)
            : base(message)
        {
        }

        public AppleSignatureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// What an App Store transaction says, from its signed form once the signature is Apple's (Apple's
    /// JWSTransactionDecodedPayload; only the fields the server judges by are read). Times are Apple's milliseconds.
    /// </summary>
    public sealed class AppleTransaction
    {
        [JsonPropertyName("transactionId")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("originalTransactionId")]
        public string? OriginalTransactionId { get; set; }

        [JsonPropertyName("bundleId")]
        public string? BundleId { get; set; }

        [JsonPropertyName("productId")]
        public string? ProductId { get; set; }

        /// <summary>The UUID the game attached to the payment (<c>Puzzle.Client.Purchases.StoreAccount</c>); nothing when it attached none.</summary>
        [JsonPropertyName("appAccountToken")]
        public string? AppAccountToken { get; set; }

        /// <summary>"Production" or "Sandbox" (also "Xcode" and "LocalTesting", whose signatures are not Apple's).</summary>
        [JsonPropertyName("environment")]
        public string? Environment { get; set; }

        [JsonPropertyName("signedDate")]
        public long? SignedDate { get; set; }

        /// <summary>When Apple refunded or revoked it; nothing while it stands.</summary>
        [JsonPropertyName("revocationDate")]
        public long? RevocationDate { get; set; }
    }

    /// <summary>
    /// Checks that a signed transaction (a JWS, RFC 7515, in its compact form) comes from Apple, the way Apple's own App
    /// Store Server Libraries do (docs/research/server-v1.md §4.4; Apple has none for .NET): the header's x5c chain of
    /// three certificates must lead to the trusted root (Apple Root CA - G3, embedded in the module), the
    /// intermediate must carry Apple's marker 1.2.840.113635.100.6.2.1 and the signing certificate
    /// 1.2.840.113635.100.6.11.1, each valid at the moment Apple signed, and the ES256 signature must be the signing
    /// certificate's. Revocation is not looked up online: every payment is also asked of Apple's server, which
    /// answers for the payment as it is now.
    /// </summary>
    public sealed class SignedTransactions
    {
        public const string IntermediateMarker = "1.2.840.113635.100.6.2.1";

        public const string SigningMarker = "1.2.840.113635.100.6.11.1";

        private const string EmbeddedRoot = "AppleRootCA-G3.cer";

        private readonly byte[] _trustedRoot;

        /// <param name="trustedRoot">The root certificate in DER: Apple's in the module, a root of their own in the tests.</param>
        public SignedTransactions(byte[] trustedRoot)
        {
            _trustedRoot = trustedRoot ?? throw new ArgumentNullException(nameof(trustedRoot));
        }

        /// <summary>The checker with Apple Root CA - G3, as apple.com/certificateauthority gives it (SHA-256 63343abf…3e9179).</summary>
        public static SignedTransactions WithAppleRoot()
        {
            using Stream root = typeof(SignedTransactions).Assembly.GetManifestResourceStream(EmbeddedRoot)
                ?? throw new InvalidOperationException($"The module carries no {EmbeddedRoot}: the build lost the root it embeds.");
            using var bytes = new MemoryStream();
            root.CopyTo(bytes);
            return new SignedTransactions(bytes.ToArray());
        }

        /// <summary>What the signed transaction says, once its signature is checked.</summary>
        /// <exception cref="AppleSignatureException">The text is not a transaction Apple signed.</exception>
        public AppleTransaction Verify(string jws)
        {
            string[] parts = (jws ?? string.Empty).Split('.');
            if (parts.Length != 3)
            {
                throw new AppleSignatureException("A signed transaction has three parts.");
            }

            Header header = Decoded<Header>(parts[0], "header");
            AppleTransaction transaction = Decoded<AppleTransaction>(parts[1], "payload");
            if (header.Alg != "ES256")
            {
                throw new AppleSignatureException($"The transaction is signed with '{header.Alg}', not ES256.");
            }

            byte[][] chain = (header.X5c ?? new List<string>()).Select(CertificateBytes).ToArray();
            DateTimeOffset signedAt = transaction.SignedDate is long millis ? DateTimeOffset.FromUnixTimeMilliseconds(millis) : throw new AppleSignatureException("The transaction has no signedDate.");
            using X509Certificate2 signer = VerifyChain(chain, signedAt);
            using ECDsa key = signer.GetECDsaPublicKey() ?? throw new AppleSignatureException("The signing certificate's key is not an elliptic curve key.");
            byte[] signature = Base64UrlBytes(parts[2], "signature");
            if (!key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                throw new AppleSignatureException("The signature is not the signing certificate's.");
            }

            return transaction;
        }

        /// <summary>
        /// The signing certificate of a chain (signing certificate, intermediate, root, in DER) that leads to the
        /// trusted root at the given moment, with Apple's markers on the intermediate and the signing certificate.
        /// </summary>
        /// <exception cref="AppleSignatureException">The chain is not Apple's.</exception>
        public X509Certificate2 VerifyChain(IReadOnlyList<byte[]> chain, DateTimeOffset at)
        {
            if (chain.Count != 3)
            {
                throw new AppleSignatureException($"An App Store chain has three certificates, not {chain.Count}.");
            }

            if (!chain[2].AsSpan().SequenceEqual(_trustedRoot))
            {
                throw new AppleSignatureException("The chain's root is not the trusted root.");
            }

            X509Certificate2 signer = Certificate(chain[0], "signing certificate");
            try
            {
                using X509Certificate2 intermediate = Certificate(chain[1], "intermediate");
                using X509Certificate2 root = Certificate(chain[2], "root");
                RequireMarker(intermediate, IntermediateMarker, "intermediate");
                RequireMarker(signer, SigningMarker, "signing certificate");
                using var verification = new X509Chain();
                verification.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                verification.ChainPolicy.CustomTrustStore.Add(root);
                verification.ChainPolicy.ExtraStore.Add(intermediate);
                verification.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                verification.ChainPolicy.DisableCertificateDownloads = true;
                verification.ChainPolicy.VerificationTime = at.UtcDateTime;
                if (!verification.Build(signer))
                {
                    string problems = string.Join("; ", verification.ChainStatus.Select(status => status.StatusInformation.Trim()));
                    throw new AppleSignatureException($"The chain does not lead to the trusted root at {at:O}: {problems}");
                }

                if (verification.ChainElements.Count != 3 || !verification.ChainElements[1].Certificate.RawData.AsSpan().SequenceEqual(chain[1]))
                {
                    throw new AppleSignatureException("The chain leads to the root by another way than through its own intermediate.");
                }

                return signer;
            }
            catch (AppleSignatureException)
            {
                signer.Dispose();
                throw;
            }
        }

        private static void RequireMarker(X509Certificate2 certificate, string marker, string which)
        {
            if (!certificate.Extensions.Cast<X509Extension>().Any(extension => extension.Oid?.Value == marker))
            {
                throw new AppleSignatureException($"The {which} does not carry Apple's marker {marker}.");
            }
        }

        private static X509Certificate2 Certificate(byte[] der, string which)
        {
            try
            {
                return X509CertificateLoader.LoadCertificate(der);
            }
            catch (CryptographicException unreadable)
            {
                throw new AppleSignatureException($"The {which} is not a certificate.", unreadable);
            }
        }

        private static byte[] CertificateBytes(string base64)
        {
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException unreadable)
            {
                throw new AppleSignatureException("A certificate of the chain is not base64.", unreadable);
            }
        }

        private static byte[] Base64UrlBytes(string part, string which)
        {
            try
            {
                return OutsideCalls.FromBase64Url(part);
            }
            catch (FormatException unreadable)
            {
                throw new AppleSignatureException($"The transaction's {which} is not base64url.", unreadable);
            }
        }

        private static T Decoded<T>(string part, string which)
            where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(Base64UrlBytes(part, which)) ?? throw new AppleSignatureException($"The transaction's {which} is empty.");
            }
            catch (JsonException unreadable)
            {
                throw new AppleSignatureException($"The transaction's {which} is not JSON.", unreadable);
            }
        }

        private sealed class Header
        {
            [JsonPropertyName("alg")]
            public string? Alg { get; set; }

            [JsonPropertyName("x5c")]
            public List<string>? X5c { get; set; }
        }
    }
}
