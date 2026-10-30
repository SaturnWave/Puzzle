using System.Text.Json;
using Puzzle.Tooling.Players;

namespace Puzzle.Web.Accounts;

/// <summary>
/// The test settings file that lists the ready-made accounts of the public test (<c>public-test-players.json</c>
/// beside the app's settings, unless <c>Puzzle:ReadyMadePlayersPath</c> names another): the one place their usernames
/// and test passwords are written down. A public server makes each listed account that is missing when it starts.
/// </summary>
/// <remarks>
/// The file is plain JSON, without comments: the start script reads it too, with Windows PowerShell. What it says
/// about itself is in its <c>about</c> text, which the server does not read.
/// </remarks>
internal static class ReadyMadePlayersFile
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The accounts <paramref name="path"/> lists, in its order; none when there is no such file.</summary>
    /// <exception cref="InvalidOperationException">
    /// The file cannot be read, is not the JSON expected, or lists an account that cannot be made. The message names
    /// the file and the entry, and never spells a password.
    /// </exception>
    public static IReadOnlyList<ReadyMadePlayer> Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var players = new List<ReadyMadePlayer>();
        foreach (Entry? entry in Parse(path).Players ?? [])
        {
            ReadyMadePlayer player = ToPlayer(entry, number: players.Count + 1, path);
            if (players.Any(earlier => earlier.Username.Equals(player.Username)))
            {
                throw Unusable(path, $"'{player.Username}' is listed twice (names that differ only in case are one name)");
            }

            players.Add(player);
        }

        return players;
    }

    private static Listing Parse(string path)
    {
        try
        {
            using FileStream file = File.OpenRead(path);
            return JsonSerializer.Deserialize<Listing>(file, Json) ?? throw Unusable(path, "it holds no JSON object");
        }
        catch (JsonException ex)
        {
            // The parser's own message may quote what it read; its place in the file says enough.
            throw Unusable(path, $"it is not the JSON expected (line {(ex.LineNumber ?? 0) + 1}, at {ex.Path ?? "the start"})", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Unusable(path, "it could not be read", ex);
        }
    }

    /// <param name="number">The entry's place in the list, from 1, for the message.</param>
    private static ReadyMadePlayer ToPlayer(Entry? entry, int number, string path)
    {
        if (!Username.TryCreate(entry?.Username, out Username? username))
        {
            throw Unusable(path, $"account {number} has no usable username ({Username.MinLength}..{Username.MaxLength} letters, digits, '_' or '-')");
        }

        return ReadyMadePlayer.TryCreate(username, entry?.Password, out ReadyMadePlayer? player)
            ? player
            : throw Unusable(path, $"the password of '{username}' is missing or longer than {PasswordRules.MaxLength} characters");
    }

    private static InvalidOperationException Unusable(string path, string why, Exception? cause = null) =>
        new($"The ready-made accounts' file '{path}' cannot be used: {why}. Fix it, or set Puzzle:ReadyMadePlayersPath to an empty value to make no ready-made accounts.", cause);

    /// <summary>The file as it is written: <c>{ "about": "...", "players": [ { "username": "...", "password": "..." } ] }</c>.</summary>
    private sealed record Listing(IReadOnlyList<Entry?>? Players);

    private sealed record Entry(string? Username, string? Password);
}
