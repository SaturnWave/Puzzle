using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Puzzle.Bank.Storage;
using Puzzle.Bot;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Bank.Measuring;

/// <summary>
/// The running build does not play like the games in the bank. Nothing was written; the message says what differs
/// and how to go on.
/// </summary>
public sealed class EpochMismatchException(string message) : Exception(message);

/// <summary>Which epoch a tool that writes to the bank was told to work in. Closed set (private constructor).</summary>
public abstract record EpochChoice
{
    private EpochChoice()
    {
    }

    /// <summary>The epoch of the running build; a first one in an empty bank.</summary>
    public sealed record UseCurrent : EpochChoice;

    /// <summary>A new epoch. Every game the bank holds stops counting for this build, so where the build already has an epoch, that epoch must be named.</summary>
    /// <param name="Leaving">The id of the epoch that is left, as the confirmation; absent when none was named.</param>
    public sealed record OpenAnother(string? Leaving) : EpochChoice;
}

/// <summary>
/// Keeps games of different rules apart. Every batch of games carries the epoch it was played in; a tool works in
/// the epoch whose fingerprint its own build has, and refuses to add to a bank that has none for it until it is
/// told to open one.
/// </summary>
public static class Epochs
{
    private const int ReplayedCandidates = 6;
    private const int ReplayedSeedsPerBatch = 4;

    /// <summary>
    /// The epoch of the running build in this bank; absent when the bank has epochs but none of this build's
    /// fingerprint (its games were played with other rules or another bot).
    /// </summary>
    public static EpochId? Current(BankCatalog catalog) =>
        catalog.EpochOf(MeasurementFingerprint.OfThisBuild) is EpochRecord epoch ? EpochId.Parse(epoch.Epoch) : null;

    /// <summary>
    /// The epoch to write in: the build's own when the bank has it, a first one in an empty bank, a new one when
    /// <paramref name="choice"/> asks for it.
    /// </summary>
    /// <param name="note">Why a new epoch is opened, for whoever reads the bank later.</param>
    /// <exception cref="EpochMismatchException">
    /// The bank's epochs are all of another fingerprint and no new one was asked for; or a new one was asked for
    /// while this build has one, without naming the epoch that is left.
    /// </exception>
    /// <exception cref="BankWriteException">A new epoch could not be written.</exception>
    public static EpochId ForWriting(BankCatalog catalog, BankWriter writer, EpochChoice choice, string note)
    {
        string fingerprint = MeasurementFingerprint.OfThisBuild;
        EpochId? current = Current(catalog);
        switch (choice)
        {
            case EpochChoice.UseCurrent when current is EpochId own:
                return own;
            case EpochChoice.UseCurrent when catalog.Epochs.Count > 0:
                throw new EpochMismatchException(
                    $"The bank's games were played with other rules or another bot than this build has (its fingerprint {fingerprint[..16]} matches none of the bank's {catalog.Epochs.Count} epochs). "
                    + "Nothing was changed. If the engine or the bot changed on purpose, open a new epoch (bank remeasure --new-epoch): the boards stay, "
                    + "and none of them counts for this build until that command has played its ladder games again.");
            case EpochChoice.OpenAnother opening when current is EpochId left && opening.Leaving != left.ToString():
                throw new EpochMismatchException(LeavingMessage(catalog, left));
            case EpochChoice.UseCurrent or EpochChoice.OpenAnother:
                return Open(catalog, writer, fingerprint, note);
            default:
                throw new InvalidOperationException($"Unhandled epoch choice {choice.GetType().Name}.");
        }
    }

    /// <summary>How many candidates have ladder games of <paramref name="epoch"/>: what counts for the build that works in it.</summary>
    public static int MeasuredIn(BankCatalog catalog, EpochId epoch) =>
        catalog.Candidates.Count(candidate => candidate.ReadingIn(epoch).Games > 0);

    private static string LeavingMessage(BankCatalog catalog, EpochId left)
    {
        long games = catalog.Candidates.Sum(candidate => candidate.Games.Where(batch => batch.Header.Epoch == left).Sum(batch => (long)batch.Finishes.Length));
        return $"This build already works in epoch {left} of the bank: {MeasuredIn(catalog, left)} candidates with {games} games. "
            + "Opening another epoch makes none of them count for this build until bank remeasure has played them again, and it cannot be taken back. "
            + $"Nothing was changed. To do it all the same, name the epoch that is left: --new-epoch {left}.";
    }

    private static EpochId Open(BankCatalog catalog, BankWriter writer, string fingerprint, string note)
    {
        int generation = catalog.Epochs.Count(epoch => epoch.Fingerprint == fingerprint);
        var opened = new EpochRecord(
            MeasurementFingerprint.EpochOf(fingerprint, generation).ToString(),
            fingerprint,
            generation,
            DateTime.UtcNow,
            RuntimeInformation.FrameworkDescription,
            HashOfAssemblyOf(typeof(Game)),
            HashOfAssemblyOf(typeof(GreedyPolicy)),
            note);
        writer.Open(opened);
        return EpochId.Parse(opened.Epoch);
    }

    /// <summary>
    /// Replays a sample of the epoch's stored games and requires the very same bytes. The fingerprint is a probe of
    /// hand-made levels; this is the check on the bank's own content, made before anything is added to it.
    /// </summary>
    /// <exception cref="EpochMismatchException">Stored games do not replay; the message has the numbers.</exception>
    public static ReplayReport RequireSameGames(BankStore store, BankCatalog catalog, EpochId epoch, Rng dice)
    {
        ReplayReport report = SelfCheck.Replay(store, SelfCheck.Sample(catalog, epoch, ReplayedCandidates, dice), ReplayedSeedsPerBatch);
        return report.Agrees ? report : throw new EpochMismatchException(Describe(report, epoch));
    }

    public static string Describe(ReplayReport report, EpochId epoch) => report.Agrees
        ? $"{report.Games} stored games of epoch {epoch} replayed to the same bytes"
        : $"{report.DifferentGames} of {report.Games} replayed games of epoch {epoch} differ from what is stored (first in candidate {report.FirstDifferent}); "
            + $"mean finishing move stored {report.MeanFinishStored:0.00}, replayed {report.MeanFinishReplayed:0.00}. "
            + $"The build's fingerprint matches, so the probe levels do not see this change: nothing was written. Open a new epoch (bank remeasure --new-epoch {epoch}), and add a probe level for what changed.";

    /// <summary>The first 16 hex digits of a SHA-256 over the assembly file: which build opened an epoch, for a person to compare.</summary>
    private static string HashOfAssemblyOf(Type type)
    {
        string path = type.Assembly.Location;
        return File.Exists(path) ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))[..16] : "unknown";
    }
}
