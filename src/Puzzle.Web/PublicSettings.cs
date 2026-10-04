using Puzzle.Tooling.Players;
using Puzzle.Web.Accounts;
using Puzzle.Web.Security;

namespace Puzzle.Web;

/// <summary>What the server is: the owner's test lab, or the public mode that lets the players the owner added play.</summary>
public enum ServerMode
{
    /// <summary>The lab alone, open to whoever reaches the server directly; never served through a tunnel or a proxy. The default.</summary>
    Lab,

    /// <summary>The player page and its API for everyone; the lab only for the owner, behind a key.</summary>
    Public,
}

/// <summary>The public mode's configuration, read once at startup.</summary>
/// <param name="OwnerKey">Opens the lab to the owner; absent when no key is configured, and then the lab does not exist.</param>
/// <param name="LevelSet">The folder of the level library the players work through.</param>
/// <param name="DataFolder">Where the player store and the cookie keys are kept.</param>
/// <param name="ReadyMadePlayers">The accounts of the public test the server makes at its start when they are missing; none when no file lists any.</param>
internal sealed record PublicSettings(OwnerKey? OwnerKey, string LevelSet, string DataFolder, IReadOnlyList<ReadyMadePlayer> ReadyMadePlayers)
{
    private const string DefaultLevelSet = "generated";

    /// <summary>The mode <c>Puzzle:Mode</c> names; the lab when it names none.</summary>
    /// <exception cref="InvalidOperationException">The setting is neither "Lab" nor "Public".</exception>
    public static ServerMode ReadMode(IConfiguration configuration)
    {
        string? text = configuration["Puzzle:Mode"];
        if (string.IsNullOrWhiteSpace(text))
        {
            return ServerMode.Lab;
        }

        return Enum.TryParse(text, ignoreCase: true, out ServerMode mode) && Enum.IsDefined(mode)
            ? mode
            : throw new InvalidOperationException($"Puzzle:Mode is '{text}'; it must be Lab or Public.");
    }

    /// <summary>
    /// Reads the public mode's settings. The data folder defaults to <see cref="PlayerStore.DefaultFolder"/> in the
    /// repository root, like the recordings; a configured path is relative to the content root, an absolute one is
    /// used as it is. The same holds for the file <c>Puzzle:ReadyMadePlayersPath</c> names, which lists the ready-made
    /// accounts of the public test; an empty setting, or a file that is not there, lists none.
    /// </summary>
    /// <exception cref="InvalidOperationException">A setting is unusable; the message says which and why.</exception>
    public static PublicSettings Read(IConfiguration configuration, string contentRoot)
    {
        string adminKey = configuration["Puzzle:AdminKey"]?.Trim() ?? string.Empty;
        if (adminKey.Length is > 0 and < OwnerKey.MinLength)
        {
            throw new InvalidOperationException(
                $"Puzzle:AdminKey is shorter than {OwnerKey.MinLength} characters; use a long random string, or leave it empty to switch the lab off.");
        }

        string dataFolder = configuration["Puzzle:DataPath"] is { Length: > 0 } configured
            ? Path.Combine(contentRoot, configured)
            : PlayerStore.FindDefaultFolder(contentRoot)
                ?? throw new InvalidOperationException("No repository root (a folder with 'levels') found; set Puzzle:DataPath.");

        IReadOnlyList<ReadyMadePlayer> readyMadePlayers = configuration["Puzzle:ReadyMadePlayersPath"] is { Length: > 0 } listedIn
            ? ReadyMadePlayersFile.Read(Path.Combine(contentRoot, listedIn))
            : [];

        return new PublicSettings(
            adminKey.Length > 0 ? new OwnerKey(adminKey) : null,
            configuration["Puzzle:PublicLevels"] is { Length: > 0 } levelSet ? levelSet : DefaultLevelSet,
            dataFolder,
            readyMadePlayers);
    }
}
