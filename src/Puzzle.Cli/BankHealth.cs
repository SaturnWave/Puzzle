using System.Globalization;
using Puzzle.Bank.Storage;

namespace Puzzle.Cli;

/// <summary>The bank is damaged and nobody said to go on with what is left of it; the message says what is gone.</summary>
internal sealed class BankDamagedException(string message) : Exception(message);

/// <summary>
/// What a command does about a damaged bank. Reading a damaged bank works (the sound records around the damage are
/// the bank), which is exactly why a command that chooses from it or adds to it must not do so unseen: the same
/// assembly from a bank with a gap picks other boards for a part of the levels.
/// </summary>
internal static class BankHealth
{
    public const string AcceptOption = "accept-damage";

    /// <summary>What a drawn candidate takes on disk with its ladder games (measured: 22.4 KB), to say roughly how much a gap held.</summary>
    private const int BytesPerCandidate = 22_000;

    /// <param name="consequence">What going on would mean for this command, as a sentence without its full stop.</param>
    /// <exception cref="BankDamagedException">The bank is damaged and --accept-damage was not given.</exception>
    public static void RequireSound(BankScan scan, BankStore store, CliArgs args, string consequence)
    {
        if (scan.IsSound)
        {
            return;
        }

        string damage = Describe(scan);
        if (!args.Flag(AcceptOption))
        {
            throw new BankDamagedException(
                $"The bank in '{store.Root}' is damaged: {damage}. {consequence}. Nothing was changed. 'bank verify' shows where the damage lies; "
                + $"put the shard back from a copy, or go on with what is left: --{AcceptOption}.");
        }

        Console.Error.WriteLine($"WARNING: going on with a damaged bank: {damage}.");
    }

    public static string Describe(BankScan scan)
    {
        ShardScan[] damaged = scan.Shards.Where(shard => !shard.IsSound).ToArray();
        long bytes = damaged.Sum(shard => shard.Unreadable.Sum(range => range.Length));
        var parts = new List<string>();
        if (damaged.Length > 0)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{damaged.Length} of {scan.Shards.Count} shards hold {bytes} unreadable bytes; the records that lay there are gone, about {Math.Max(1, bytes / BytesPerCandidate)} candidates with their games"));
        }

        if (scan.GamesWithoutCandidate > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{scan.GamesWithoutCandidate} batches of games have no candidate (its record lay in the damage, or a shard file is missing)"));
        }

        return string.Join("; ", parts);
    }
}
