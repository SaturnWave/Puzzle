#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Puzzle.Client.Purchases;

namespace Puzzle.Client.Cloud
{
    /// <summary>What asking the server to delete the player's data came to. A closed set.</summary>
    public enum DataDeletion
    {
        /// <summary>The save, the ledger and the player's name in the receipts' registry are gone (the anonymous 36-month trace stays).</summary>
        Deleted,

        /// <summary>The phone has no network: nothing was deleted.</summary>
        NoNetwork,

        /// <summary>The module could not be reached, is not deployed, or could not finish: nothing more was deleted; asking again finishes it.</summary>
        Unavailable,
    }

    /// <summary>
    /// The game's Cloud Code module (server/CloudCode, MuseumPuzzleServer) as the game calls it for the player signed in:
    /// ValidatePurchase and DeleteMyData (docs/research/server-v1.md §4.2). It is a seam: the game's adapter calls Cloud
    /// Code (Cloud/Ugs/UgsServerModule), the tests and the Editor's checks a server in memory
    /// (<see cref="FakePurchaseServer"/>). An adapter turns every failure of the service into an answer at this edge:
    /// a module not deployed, no network or a service down is <see cref="ServerVerdict.Unavailable"/> or
    /// <see cref="DataDeletion.Unavailable"/>, and nothing of the service's own errors goes further.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IServerModule
    {
        /// <summary>Asks the module to verify the payment with its store and enter it in the player's ledger.</summary>
        Task<ServerVerdict> ValidatePurchaseAsync(PaymentQuestion question, CancellationToken cancel);

        /// <summary>Asks the module to delete the player's data (the save, the ledger, the player's claims in the registry).</summary>
        Task<DataDeletion> DeleteMyDataAsync(CancellationToken cancel);
    }
}
