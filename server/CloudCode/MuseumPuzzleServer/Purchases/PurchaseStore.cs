namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>The stores that take the game's payments. A closed set: each has a verifier of its own.</summary>
    public enum PurchaseStore
    {
        /// <summary>Google Play: a payment is known by its purchase token, verified with the Play Developer API.</summary>
        GooglePlay,

        /// <summary>The App Store: a payment is known by its transaction id, verified by Apple's signature and the App Store Server API.</summary>
        AppleAppStore,
    }

    /// <summary>The stores' names on the wire: the names Unity In-App Purchasing gives them, which the game sends and the ledger keeps.</summary>
    public static class PurchaseStoreWords
    {
        public const string GooglePlay = "GooglePlay";

        public const string AppleAppStore = "AppleAppStore";

        /// <summary>The store with this name; nothing for a name no store of the game has.</summary>
        public static PurchaseStore? StoreNamed(string? word) => word switch
        {
            GooglePlay => PurchaseStore.GooglePlay,
            AppleAppStore => PurchaseStore.AppleAppStore,
            _ => null,
        };

        public static string WordOf(PurchaseStore store) => store switch
        {
            PurchaseStore.GooglePlay => GooglePlay,
            PurchaseStore.AppleAppStore => AppleAppStore,
            _ => throw new System.ArgumentOutOfRangeException(nameof(store), store, "A store the game does not sell through."),
        };
    }
}
