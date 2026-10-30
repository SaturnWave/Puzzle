using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using MuseumPuzzle.Server.AppStore;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.GooglePlay;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;
using MuseumPuzzle.Server.Purchases;

namespace MuseumPuzzle.Server.Endpoints
{
    /// <summary>
    /// What the module makes once and shares between calls (registered as a singleton in <see cref="ModuleSetup"/>):
    /// the catalogue, the one HTTP client, Apple's root, the clock and the two access-token slots. Everything else is
    /// made for each call, with the call's logger; nothing grows between calls (Cloud Code's workers have 512 MB).
    /// </summary>
    public sealed class ModuleParts
    {
        public ModuleParts(ProductCatalogue catalogue, HttpClient http, SignedTransactions signatures, IClock clock)
        {
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            Http = http ?? throw new ArgumentNullException(nameof(http));
            Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            GoogleTokens = new GoogleAccessTokens(http, clock);
            AppleTokens = new AppStoreTokens(clock);
        }

        public ProductCatalogue Catalogue { get; }

        public HttpClient Http { get; }

        public SignedTransactions Signatures { get; }

        public IClock Clock { get; }

        public GoogleAccessTokens GoogleTokens { get; }

        public AppStoreTokens AppleTokens { get; }

        /// <summary>The parts as Cloud Code runs them: the shipped catalogue, Apple Root CA - G3, real time.</summary>
        public static ModuleParts ForCloudCode() => new ModuleParts(ProductCatalogue.Shipped(), OutsideCalls.NewClient(), SignedTransactions.WithAppleRoot(), new SystemClock());

        public GooglePlayVerifier GooglePlay(ILogger log) => new GooglePlayVerifier(Http, GoogleTokens, log);

        public AppStoreVerifier AppStore(ILogger log) => new AppStoreVerifier(Http, AppleTokens, Signatures, log);

        public PurchaseValidation Validation(ILogger log) => new PurchaseValidation(Catalogue, new IStoreVerifier[] { GooglePlay(log), AppStore(log) }, Clock, log);

        public AppleRefundCheck Refunds(ILogger log) => new AppleRefundCheck(AppStore(log), Clock, log);

        public DataDeletion Deletion(ILogger log) => new DataDeletion(Clock, log);

        public GoogleVoidedPoll VoidedPoll(ILogger log) => new GoogleVoidedPoll(GooglePlay(log), Clock, log);

        public TraceRetention Retention(ILogger log) => new TraceRetention(Clock, log);
    }
}
