using System.Diagnostics;
using System.Globalization;
using Puzzle.Core.Recording;
using Puzzle.Tooling.Players;
using Puzzle.Tooling.Recording;

namespace Puzzle.Cli.Commands;

/// <summary>
/// <c>players add &lt;username&gt;</c>, <c>players password &lt;username&gt;</c>, <c>players list</c> and
/// <c>players delete &lt;username&gt;</c>: the owner makes the public mode's accounts here (the server registers
/// nobody), gives one a new password — which keeps its progress and signs out every browser signed in with the old
/// one — sees them, and deletes one when a player asks: their account, their progress and every attempt record that
/// carries their player id. All four find the store and the attempt log where the public server keeps them, in the
/// repository root, unless <c>--data</c> and <c>--records</c> say otherwise.
/// </summary>
internal static class PlayersCommand
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(CliArgs args)
    {
        string action = args.Required(0, "add|password|list|delete");
        string dataFolder = args.OptionalText("data") ?? DefaultDataFolder();
        try
        {
            return action switch
            {
                "add" => Add(dataFolder, UsernameFrom(args)),
                "password" => ChangePassword(dataFolder, UsernameFrom(args)),
                "list" => List(dataFolder, RecordsPathFrom(args)),
                "delete" => Delete(dataFolder, RecordsPathFrom(args), UsernameFrom(args)),
                _ => throw new UsageException($"Unknown players action '{action}' (use add, password, list or delete)."),
            };
        }
        catch (PlayerStoreFormatException ex)
        {
            Console.Error.WriteLine($"The player store is damaged: {ex.Message}");
            return 1;
        }
        catch (PlayerStoreInUseException)
        {
            // A running server keeps its own copy of the accounts and would write it over a change made here.
            Console.Error.WriteLine("The public server is running and holds the player store. Stop it, run this again, then start it.");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not finish: {ex.Message}");
            return 1;
        }
    }

    private static int Add(string dataFolder, Username username)
    {
        using PlayerStore store = PlayerStore.Open(dataFolder);
        if (store.TryFind(username, out _))
        {
            Console.Error.WriteLine($"A player is already called '{username}'.");
            return 1;
        }

        if (!TryAskForPassword("Password for the new player", out string password))
        {
            Console.Error.WriteLine("Nothing was added.");
            return 1;
        }

        switch (PlayerAddition.Add(store, new PasswordHasher(), username, password, DateTimeOffset.UtcNow))
        {
            case AddPlayerOutcome.Added added:
                Console.WriteLine($"added '{added.Account.Username}' (player id {added.Account.PlayerId.Value}) to {store.Path}");
                return 0;
            case AddPlayerOutcome.UsernameTaken:
                Console.Error.WriteLine($"A player is already called '{username}'.");
                return 1;
            case AddPlayerOutcome.PasswordRefused:
                Console.Error.WriteLine($"{PasswordRule()} Nothing was added.");
                return 1;
            default:
                throw new UnreachableException("Unhandled outcome of adding a player.");
        }
    }

    private static int ChangePassword(string dataFolder, Username username)
    {
        if (!Directory.Exists(dataFolder))
        {
            Console.Error.WriteLine($"No player store at '{dataFolder}'.");
            return 1;
        }

        using PlayerStore store = PlayerStore.Open(dataFolder);
        if (!store.TryFind(username, out _))
        {
            Console.Error.WriteLine($"No player is called '{username}'.");
            return 1;
        }

        if (!TryAskForPassword($"New password for '{username}'", out string password))
        {
            Console.Error.WriteLine("Nothing was changed.");
            return 1;
        }

        switch (PlayerPasswordChange.Change(store, new PasswordHasher(), username, password))
        {
            case ChangePasswordOutcome.Changed changed:
                Console.WriteLine($"changed the password of '{changed.Account.Username}': the progress is kept, and every browser signed in with the old password is signed out");
                return 0;
            case ChangePasswordOutcome.NoSuchPlayer:
                Console.Error.WriteLine($"No player is called '{username}'.");
                return 1;
            case ChangePasswordOutcome.PasswordRefused:
                Console.Error.WriteLine($"{PasswordRule()} Nothing was changed.");
                return 1;
            default:
                throw new UnreachableException("Unhandled outcome of changing a password.");
        }
    }

    /// <summary>
    /// A password for a player, asked for on the console after <paramref name="prompt"/> and never taken from the
    /// command line: typed twice alike at a keyboard, read once from redirected input. False, with the reason said,
    /// when there is none to use.
    /// </summary>
    private static bool TryAskForPassword(string prompt, out string password)
    {
        password = string.Empty;
        string? typed = ConsoleSecret.ReadLine($"{prompt} ({PasswordRules.MinLength}..{PasswordRules.MaxLength} characters): ");
        if (!PasswordRules.Allows(typed))
        {
            Console.Error.WriteLine(PasswordRule());
            return false;
        }

        if (!Console.IsInputRedirected && ConsoleSecret.ReadLine("The same password again: ") != typed)
        {
            Console.Error.WriteLine("The two passwords differ.");
            return false;
        }

        password = typed;
        return true;
    }

    private static string PasswordRule() => $"A password has {PasswordRules.MinLength}..{PasswordRules.MaxLength} characters.";

    private static int List(string dataFolder, string recordsPath)
    {
        IReadOnlyList<PlayerAccount> accounts = PlayerStore.ReadAccounts(dataFolder);
        ILookup<DeviceId, AttemptRecord> recordsByPlayer = new AttemptLog(recordsPath).ReadAll().Records.ToLookup(record => record.Start.Device);

        // tools/public/Start-PublicServer.ps1 reads this listing: after this line and the heading line, the username
        // that starts each line. It tells from them which ready-made accounts are still to be made, and whether
        // anybody could sign in at all.
        Console.WriteLine($"players in {Path.GetFullPath(dataFolder)}: {accounts.Count}");
        if (accounts.Count == 0)
        {
            return 0;
        }

        Console.WriteLine($"{"username",-20} {"player id",-32} {"added (UTC)",-16} {"levels won",10} {"records",7}");
        foreach (PlayerAccount account in accounts)
        {
            string added = account.CreatedUtc.ToString("yyyy-MM-dd HH:mm", Invariant);
            int records = recordsByPlayer[account.PlayerId].Count();
            Console.WriteLine($"{account.Username.Value,-20} {account.PlayerId.Value,-32} {added,-16} {account.Progress.LevelsWon,10} {records,7}");
        }

        return 0;
    }

    private static int Delete(string dataFolder, string recordsPath, Username username)
    {
        if (!Directory.Exists(dataFolder))
        {
            Console.Error.WriteLine($"No player store at '{dataFolder}'.");
            return 1;
        }

        using PlayerStore store = PlayerStore.Open(dataFolder);
        if (!PlayerDeletion.TryDelete(store, new AttemptLog(recordsPath), username, out DeletedPlayer? deleted))
        {
            Console.Error.WriteLine($"No player is called '{username}'.");
            return 1;
        }

        Console.WriteLine($"deleted '{deleted.Username}' (player id {deleted.PlayerId}): the account, its progress and {deleted.RecordsRemoved} attempt records");
        return 0;
    }

    /// <exception cref="UsageException">The username is missing or malformed.</exception>
    private static Username UsernameFrom(CliArgs args)
    {
        string name = args.Required(1, "username");
        return Username.TryCreate(name, out Username? username)
            ? username
            : throw new UsageException($"'{name}' is not a username ({Username.MinLength}..{Username.MaxLength} letters, digits, '_' or '-').");
    }

    private static string RecordsPathFrom(CliArgs args) => args.OptionalText("records") ?? DefaultRecordsPath();

    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string DefaultDataFolder() =>
        PlayerStore.FindDefaultFolder(Directory.GetCurrentDirectory())
            ?? throw new UsageException("Not inside the repository (no 'levels' folder above here); give the data folder with --data.");

    /// <exception cref="UsageException">The current folder is not inside a repository.</exception>
    private static string DefaultRecordsPath() =>
        AttemptLog.FindDefaultPath(Directory.GetCurrentDirectory())
            ?? throw new UsageException("Not inside the repository (no 'levels' folder above here); give the attempts file with --records.");
}
