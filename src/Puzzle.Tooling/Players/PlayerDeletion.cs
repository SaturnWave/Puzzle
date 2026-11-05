using System.Diagnostics.CodeAnalysis;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Recording;

namespace Puzzle.Tooling.Players;

/// <summary>What a deletion took away.</summary>
/// <param name="PlayerId">The id the removed attempt records carried.</param>
/// <param name="RecordsRemoved">Lines removed from the attempt log.</param>
public sealed record DeletedPlayer(Username Username, DeviceId PlayerId, int RecordsRemoved);

/// <summary>Deletes a player on request: everything the public mode keeps about them.</summary>
public static class PlayerDeletion
{
    /// <summary>
    /// Deletes the attempt records of the player named <paramref name="username"/>, then the account with its progress;
    /// false when there is no such player. The records go first: should that fail, the account is still there, and it is
    /// the only thing that knows which records are the player's, so the deletion can be run again.
    /// </summary>
    /// <exception cref="IOException">The attempt log or the store could not be rewritten.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there.</exception>
    public static bool TryDelete(PlayerStore store, AttemptLog log, Username username, [NotNullWhen(true)] out DeletedPlayer? deleted)
    {
        if (!store.TryFind(username, out PlayerAccount? account))
        {
            deleted = null;
            return false;
        }

        int recordsRemoved = log.RemoveRecordsOf(account.PlayerId);
        store.Remove(account.Username);
        deleted = new DeletedPlayer(account.Username, account.PlayerId, recordsRemoved);
        return true;
    }
}
