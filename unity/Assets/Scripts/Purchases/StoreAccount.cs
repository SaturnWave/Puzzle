#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace Puzzle.Client.Purchases
{
#pragma warning disable CA1850, CA5350 // Unity's .NET Standard 2.1 has no static HashData; SHA-1 is what RFC 4122 names for a version 5 UUID, which is a name, not a secret
    /// <summary>
    /// How the game names its player to a store at a payment, so that the game's server can tell whose payment a
    /// receipt is (docs/research/server-v1.md §4.1). Google Play takes an "obfuscated account id" with the payment (at
    /// most 64 characters, and no personal data), the App Store an "app account token" (a UUID); the store writes it
    /// into the receipt, where nobody but the store can change it. Both are drawn from the player's Unity player id
    /// and say nothing of it: Google's is the SHA-256 of the id in lowercase hexadecimal (64 characters), Apple's a
    /// name-based UUID (RFC 4122, version 5) of the id in a namespace of the game's own.
    /// The game attaches them when it opens a payment sheet; the server's Cloud Code module (server/CloudCode)
    /// compiles this same file and refuses a pack of coins whose receipt names another player. Changing either rule
    /// makes every earlier payment "another player's": they never change.
    /// This file has no Unity dependency, so the server and the repository's tests compile it too.
    /// </summary>
    public static class StoreAccount
    {
        /// <summary>
        /// The game's namespace for the players' app account tokens: a UUID drawn at random once, on 2026-10-26. A
        /// namespace keeps the game's tokens apart from every other name-based UUID drawn from the same player id.
        /// </summary>
        public const string PlayerNamespace = "8b86fd9e-d34b-4652-b2ed-bf65bfb98c76";

        /// <summary>The obfuscated account id Google Play keeps with this player's payments: the SHA-256 of the id, 64 lowercase hexadecimal digits.</summary>
        /// <exception cref="ArgumentException">No player id.</exception>
        public static string GooglePlayAccountOf(string playerId)
        {
            using SHA256 sha = SHA256.Create();
            return Hexadecimal(sha.ComputeHash(Encoding.UTF8.GetBytes(Required(playerId))));
        }

        /// <summary>The app account token the App Store keeps with this player's payments, as a lowercase UUID ("1b4e28ba-2fa1-51d2-…").</summary>
        /// <exception cref="ArgumentException">No player id.</exception>
        public static string AppStoreAccountTokenOf(string playerId) => NameBasedUuid(PlayerNamespace, Required(playerId));

        /// <summary>
        /// The version 5 UUID of a name in a namespace (RFC 4122 §4.3): the SHA-1 of the namespace's sixteen bytes in
        /// network order followed by the name in UTF-8, cut to sixteen bytes, with the version and the variant set.
        /// Written out from the bytes, not through <see cref="Guid"/>, whose byte order is not the network's.
        /// </summary>
        /// <exception cref="FormatException">The namespace is not a UUID.</exception>
        public static string NameBasedUuid(string namespaceUuid, string name)
        {
            byte[] space = NetworkOrderBytesOf(namespaceUuid);
            byte[] named = Encoding.UTF8.GetBytes(name ?? throw new ArgumentNullException(nameof(name)));
            var input = new byte[space.Length + named.Length];
            Buffer.BlockCopy(space, 0, input, 0, space.Length);
            Buffer.BlockCopy(named, 0, input, space.Length, named.Length);

            using SHA1 sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(input);
            var uuid = new byte[16];
            Buffer.BlockCopy(hash, 0, uuid, 0, uuid.Length);
            uuid[6] = (byte)((uuid[6] & 0x0F) | 0x50);
            uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);
            string digits = Hexadecimal(uuid);
            return $"{digits.Substring(0, 8)}-{digits.Substring(8, 4)}-{digits.Substring(12, 4)}-{digits.Substring(16, 4)}-{digits.Substring(20)}";
        }

        private static string Required(string playerId) =>
            string.IsNullOrEmpty(playerId) ? throw new ArgumentException("A store names a player by the player's id.", nameof(playerId)) : playerId;

        private static byte[] NetworkOrderBytesOf(string uuid)
        {
            string digits = (uuid ?? throw new ArgumentNullException(nameof(uuid))).Replace("-", string.Empty);
            if (digits.Length != 32)
            {
                throw new FormatException($"'{uuid}' is not a UUID: a UUID has 32 hexadecimal digits.");
            }

            var bytes = new byte[16];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(digits.Substring(index * 2, 2), 16);
            }

            return bytes;
        }

        private static string Hexadecimal(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }
    }
}
