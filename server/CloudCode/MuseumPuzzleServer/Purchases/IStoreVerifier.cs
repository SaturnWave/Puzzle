using System.Threading;
using System.Threading.Tasks;
using MuseumPuzzle.Server.Catalogue;
using MuseumPuzzle.Server.Platform;
using MuseumPuzzle.Server.Players;

namespace MuseumPuzzle.Server.Purchases
{
    /// <summary>Asks one store whether a payment is real (GooglePlay/GooglePlayVerifier, AppStore/AppStoreVerifier).</summary>
    public interface IStoreVerifier
    {
        PurchaseStore Store { get; }

        /// <summary>
        /// What the store says of the payment the player claims for a product the game sells. Never throws for the
        /// store's or the network's faults, nor for missing or refused credentials: those are
        /// <see cref="StoreCheck.Unreachable"/>.
        /// </summary>
        /// <exception cref="SecretsUnavailableException">The Secret Manager could not be reached.</exception>
        Task<StoreCheck> CheckAsync(PaymentClaim claim, CatalogueProduct product, PlayerId player, ISecrets secrets, CancellationToken cancellation);
    }
}
