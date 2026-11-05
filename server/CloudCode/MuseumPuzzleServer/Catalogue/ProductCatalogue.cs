using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Puzzle.Meta;
using Puzzle.Meta.Settings;

namespace MuseumPuzzle.Server.Catalogue
{
    /// <summary>A product the game sells, as the server needs it: the end of its id, its full id in both stores, and whether the store keeps it for good.</summary>
    public sealed class CatalogueProduct
    {
        public CatalogueProduct(string idSuffix, string storeId, StoreProductKind kind)
        {
            IdSuffix = idSuffix;
            StoreId = storeId;
            Kind = kind;
        }

        /// <summary>"coins.pouch": what the game and the ledger call it.</summary>
        public string IdSuffix { get; }

        /// <summary>"com.saturnwave.museumpuzzle.coins.pouch": what Google Play and the App Store call it.</summary>
        public string StoreId { get; }

        public StoreProductKind Kind { get; }

        /// <summary>
        /// A pack of coins, which the store forgets once the game has it: its receipt is good for one player once. The
        /// starter pack is the store account's for good, and comes back on every Unity account the same store account
        /// plays on (docs/research/server-v1.md §4.5), so its receipt is good once for each player.
        /// </summary>
        public bool IsGoodForOnePlayerOnly => Kind == StoreProductKind.CoinPack;

        public override string ToString() => StoreId;
    }

    /// <summary>
    /// What the game sells, read from the pack of the shipped economy settings that the game itself reads (embedded
    /// from tests/fixtures/economy-pack.json) through Puzzle.Meta's own mapper, so the server knows a product by the
    /// same rules as the game. A receipt for a product the game does not sell is never valid.
    /// </summary>
    public sealed class ProductCatalogue
    {
        /// <summary>The game's permanent application id (docs/GAME_DESIGN.md §19): Google Play's package name, Apple's bundle id, and the start of every product id.</summary>
        public const string ApplicationId = "com.saturnwave.museumpuzzle";

        private const string EmbeddedPack = "Economy.json";

        /// <summary>How the game's tools read the pack: Unity's reader maps public fields, so fields are read too.</summary>
        private static readonly JsonSerializerOptions AsUnityMapsIt = new JsonSerializerOptions { IncludeFields = true };

        private readonly IReadOnlyList<CatalogueProduct> _products;

        public ProductCatalogue(StoreCatalogue store)
        {
            _products = (store ?? throw new ArgumentNullException(nameof(store))).Products
                .Select(product => new CatalogueProduct(product.IdSuffix, $"{ApplicationId}.{product.IdSuffix}", product.Kind))
                .ToArray();
        }

        public IReadOnlyList<CatalogueProduct> Products => _products;

        /// <summary>The catalogue of the shipped settings, embedded in this module.</summary>
        /// <exception cref="EconomyFormatException">The embedded pack is not a set of settings the game plays by (a fault of the build, caught by the tests).</exception>
        public static ProductCatalogue Shipped()
        {
            using Stream pack = typeof(ProductCatalogue).Assembly.GetManifestResourceStream(EmbeddedPack)
                ?? throw new InvalidOperationException($"The module carries no {EmbeddedPack}: the build lost the economy pack it embeds.");
            EconomyFile file = JsonSerializer.Deserialize<EconomyFile>(pack, AsUnityMapsIt)
                ?? throw new EconomyFormatException($"The embedded {EmbeddedPack} holds no settings.");
            return new ProductCatalogue(EconomyFileMapper.ToRules(file).Shop.Store);
        }

        /// <summary>The product with this end of its id; nothing when the game sells no such product.</summary>
        public CatalogueProduct? FindBySuffix(string? idSuffix) => _products.FirstOrDefault(product => product.IdSuffix == idSuffix);

        /// <summary>The product with this id in the stores; nothing when the game sells no such product.</summary>
        public CatalogueProduct? FindByStoreId(string? storeId) => _products.FirstOrDefault(product => product.StoreId == storeId);
    }
}
