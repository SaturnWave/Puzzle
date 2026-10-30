#nullable enable
using System;
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

    /// <summary>
    /// The module with a deadline on the deletion (2026-10-27): "Verilerimi sil" waits at most <see cref="Deadline"/> for
    /// the module's answer, then counts the module out of reach (<see cref="DataDeletion.Unavailable"/>), and the account
    /// card says, calmly, that nothing could be deleted now. Unity's Cloud Code took 17 seconds on the phone run of
    /// 27 October to say that the module was not deployed, with "Siliniyor…" on the card all along. A deployed module
    /// deletes a player's few items in a second or two, and Cloud Code ends a call of the module's after 15 seconds on its
    /// side; the call that was let go is not cancelled (Cloud Code's call takes no cancellation) and its late answer is
    /// not heard: asking again finishes what it may have begun. The verification of payments has no deadline here: a
    /// payment waits for its verdict as the purchase's course says (Purchases/Checkout).
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class ModuleWithADeadline : IServerModule
    {
        /// <summary>How long the game's own module is given to delete the player's data.</summary>
        public static readonly TimeSpan GameDeadline = TimeSpan.FromSeconds(10);

        private readonly IServerModule _module;

        /// <param name="deadline">How long a deletion is waited for: <see cref="GameDeadline"/> in the game.</param>
        /// <exception cref="ArgumentOutOfRangeException">A deadline that is not a positive time.</exception>
        public ModuleWithADeadline(IServerModule module, TimeSpan deadline)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Deadline = deadline > TimeSpan.Zero ? deadline : throw new ArgumentOutOfRangeException(nameof(deadline), deadline, "A deadline is a positive time.");
        }

        public TimeSpan Deadline { get; }

        public Task<ServerVerdict> ValidatePurchaseAsync(PaymentQuestion question, CancellationToken cancel) => _module.ValidatePurchaseAsync(question, cancel);

        public async Task<DataDeletion> DeleteMyDataAsync(CancellationToken cancel)
        {
            Task<DataDeletion> deletion = _module.DeleteMyDataAsync(cancel);
            Task finished = await Task.WhenAny(deletion, Task.Delay(Deadline, cancel));
            return finished == deletion ? await deletion : DataDeletion.Unavailable;
        }
    }
}
