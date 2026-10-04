using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Players;

/// <summary>
/// The accounts of the public mode, with each player's progress, in one small JSON file. An open store is the file's
/// only writer: it holds a lock file beside it for as long as it lives, so the public server and the owner's
/// <c>puzzle players add</c>, <c>password</c> or <c>delete</c> can never overwrite each other's changes. Every change writes the whole file to a
/// temporary file first and then moves it over the real one, so a crash leaves either the old file or the new one,
/// never a torn one. One instance may be shared by every thread: changes are made one at a time, and readers see the
/// accounts as the last finished change left them.
/// </summary>
public sealed class PlayerStore : IDisposable
{
    /// <summary>Where the public server keeps its data unless told otherwise, relative to the repository root.</summary>
    public const string DefaultFolder = "data";

    private const string FileName = "players.json";
    private const string LockFileName = "players.lock";

    /// <summary>
    /// A virus scanner or an indexer may hold the file for a moment; the move waits for it this many times, doubling the
    /// pause from <see cref="FirstRetryPause"/> (under 0.4 s in all), before it gives up.
    /// </summary>
    private const int MoveTries = 5;

    private static readonly TimeSpan FirstRetryPause = TimeSpan.FromMilliseconds(25);

    private readonly object _changeGate = new();
    private readonly FileStream _writerLock;
    private readonly string _tempPath;
    private volatile Roster _roster;

    private PlayerStore(string path, FileStream writerLock, Roster roster)
    {
        Path = path;
        _tempPath = path + ".tmp";
        _writerLock = writerLock;
        _roster = roster;
    }

    /// <summary>The full path of the accounts file.</summary>
    public string Path { get; }

    /// <summary>Every account, in the order they were added, as the last finished change left them.</summary>
    public IReadOnlyList<PlayerAccount> Accounts => _roster.InOrder;

    /// <summary>
    /// The data folder in the repository root above <paramref name="start"/>, so the server and the CLI find the same
    /// store however they are started; absent outside a repository.
    /// </summary>
    public static string? FindDefaultFolder(string start) =>
        LevelLibrary.FindRepositoryRoot(start) is { } root ? System.IO.Path.GetFullPath(System.IO.Path.Combine(root, DefaultFolder)) : null;

    /// <summary>Opens the store in <paramref name="folder"/> as its only writer, creating the folder when needed; a store never written holds no accounts.</summary>
    /// <exception cref="PlayerStoreInUseException">Another process has the store open.</exception>
    /// <exception cref="PlayerStoreFormatException">The accounts file is not a valid store.</exception>
    /// <exception cref="IOException">The folder or the file could not be used.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there.</exception>
    public static PlayerStore Open(string folder)
    {
        string fullFolder = System.IO.Path.GetFullPath(folder);
        Directory.CreateDirectory(fullFolder);
        FileStream writerLock = TakeWriterLock(fullFolder);
        try
        {
            string path = System.IO.Path.Combine(fullFolder, FileName);
            return new PlayerStore(path, writerLock, Roster.Of(Load(path)));
        }
        catch
        {
            // Whatever went wrong, a store that never opened must not keep the next one out.
            writerLock.Dispose();
            throw;
        }
    }

    /// <summary>The accounts in <paramref name="folder"/> as they are on disk now, without becoming the store's writer.</summary>
    /// <exception cref="PlayerStoreFormatException">The accounts file is not a valid store.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not read the file.</exception>
    public static IReadOnlyList<PlayerAccount> ReadAccounts(string folder) =>
        Roster.Of(Load(System.IO.Path.Combine(System.IO.Path.GetFullPath(folder), FileName))).InOrder;

    public bool TryFind(Username username, [NotNullWhen(true)] out PlayerAccount? account) =>
        _roster.ByName.TryGetValue(username, out account);

    public bool TryFind(DeviceId playerId, [NotNullWhen(true)] out PlayerAccount? account) =>
        _roster.ById.TryGetValue(playerId, out account);

    /// <summary>Adds <paramref name="account"/>; false, with nothing changed, when another account has its username in whatever case.</summary>
    /// <exception cref="IOException">The change could not be written; the store is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there; the store is as it was.</exception>
    public bool TryAdd(PlayerAccount account)
    {
        lock (_changeGate)
        {
            Roster current = _roster;
            if (current.ByName.ContainsKey(account.Username))
            {
                return false;
            }

            Commit(Roster.Of(current.InOrder.Append(account)));
            return true;
        }
    }

    /// <summary>Removes the account named <paramref name="username"/> with its progress; false when there is none.</summary>
    /// <exception cref="IOException">The change could not be written; the store is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there; the store is as it was.</exception>
    public bool Remove(Username username)
    {
        lock (_changeGate)
        {
            Roster current = _roster;
            if (!current.ByName.ContainsKey(username))
            {
                return false;
            }

            Commit(Roster.Of(current.InOrder.Where(account => !account.Username.Equals(username))));
            return true;
        }
    }

    /// <summary>Replaces the progress of the player <paramref name="playerId"/> with what <paramref name="change"/> makes of it, and returns the account as it now is.</summary>
    /// <exception cref="ArgumentException">No account has that player id.</exception>
    /// <exception cref="IOException">The change could not be written; the store is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there; the store is as it was.</exception>
    public PlayerAccount UpdateProgress(DeviceId playerId, Func<PlayerProgress, PlayerProgress> change) =>
        Replace(playerId, account => account.WithProgress(change(account.Progress)));

    /// <summary>Stores a new hash of the same password, for when the hashing cost was raised since the account was made.</summary>
    /// <exception cref="ArgumentException">No account has that player id.</exception>
    /// <exception cref="IOException">The change could not be written; the store is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there; the store is as it was.</exception>
    public PlayerAccount ReplacePasswordHash(DeviceId playerId, string passwordHash) =>
        Replace(playerId, account => account.WithPasswordHash(passwordHash));

    /// <summary>
    /// Gives the player another password: stores its hash and a new sign-in stamp, so every sign-in made with the old
    /// password ends. The progress stays as it is.
    /// </summary>
    /// <exception cref="ArgumentException">No account has that player id.</exception>
    /// <exception cref="IOException">The change could not be written; the store is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The process may not write there; the store is as it was.</exception>
    public PlayerAccount ChangePassword(DeviceId playerId, string passwordHash) =>
        Replace(playerId, account => account.WithNewPassword(passwordHash));

    /// <summary>Gives the store up as writer; the file stays.</summary>
    public void Dispose() => _writerLock.Dispose();

    /// <exception cref="PlayerStoreInUseException">Another process holds the lock file.</exception>
    private static FileStream TakeWriterLock(string folder)
    {
        try
        {
            return new FileStream(System.IO.Path.Combine(folder, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex) when (IsHeldByAnother(ex))
        {
            throw new PlayerStoreInUseException(
                $"The player store in '{folder}' is in use by another process (the public server, or a 'puzzle players' command).",
                ex);
        }
    }

    /// <summary>
    /// A file another handle holds is reported with the plain <see cref="IOException"/> on Windows and Unix alike; its
    /// subclasses (file or folder not found, path too long) mean something else is wrong.
    /// </summary>
    private static bool IsHeldByAnother(IOException ex) => ex.GetType() == typeof(IOException);

    private static PlayerAccount[] Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        // The writer may move a new file over this one while it is read; the reader then finishes the old one.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            return PlayerStoreJson.Parse(file);
        }
        catch (PlayerStoreFormatException ex)
        {
            throw new PlayerStoreFormatException($"'{path}': {ex.Message}", ex);
        }
    }

    private static void MoveOver(string source, string target)
    {
        TimeSpan pause = FirstRetryPause;
        for (int tries = 1; ; tries++)
        {
            try
            {
                File.Move(source, target, overwrite: true);
                return;
            }
            catch (Exception ex) when (MayBeBriefLock(ex) && tries < MoveTries)
            {
                Thread.Sleep(pause);
                pause *= 2;
            }
        }
    }

    /// <summary>Windows refuses to replace a file another program has open, sometimes as a sharing violation and sometimes as access denied.</summary>
    private static bool MayBeBriefLock(Exception ex) => ex is UnauthorizedAccessException || (ex is IOException io && IsHeldByAnother(io));

    private PlayerAccount Replace(DeviceId playerId, Func<PlayerAccount, PlayerAccount> change)
    {
        lock (_changeGate)
        {
            Roster current = _roster;
            if (!current.ById.TryGetValue(playerId, out PlayerAccount? account))
            {
                throw new ArgumentException("No account has that player id.", nameof(playerId));
            }

            PlayerAccount changed = change(account);
            Commit(Roster.Of(current.InOrder.Select(each => ReferenceEquals(each, account) ? changed : each)));
            return changed;
        }
    }

    /// <summary>Writes <paramref name="next"/> to disk and only then lets readers see it. Hold the change gate.</summary>
    private void Commit(Roster next)
    {
        using (var temp = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            temp.Write(PlayerStoreJson.Serialize(next.InOrder));
            temp.Flush(flushToDisk: true);
        }

        MoveOver(_tempPath, Path);
        _roster = next;
    }

    /// <summary>The accounts at one moment; never changed once built.</summary>
    private sealed class Roster
    {
        private Roster(PlayerAccount[] inOrder)
        {
            InOrder = Array.AsReadOnly(inOrder);
            ByName = inOrder.ToDictionary(account => account.Username);
            ById = inOrder.ToDictionary(account => account.PlayerId);
        }

        public ReadOnlyCollection<PlayerAccount> InOrder { get; }

        public Dictionary<Username, PlayerAccount> ByName { get; }

        public Dictionary<DeviceId, PlayerAccount> ById { get; }

        /// <exception cref="PlayerStoreFormatException">Two accounts share a username or a player id.</exception>
        public static Roster Of(IEnumerable<PlayerAccount> accounts)
        {
            try
            {
                return new Roster(accounts.ToArray());
            }
            catch (ArgumentException ex)
            {
                throw new PlayerStoreFormatException("Two accounts share a username or a player id.", ex);
            }
        }
    }
}
