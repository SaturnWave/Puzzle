using System.Globalization;
using Puzzle.Bank.Candidates;
using Puzzle.Bot;
using Puzzle.Bot.Simulation;
using Puzzle.Core.Engine;

namespace Puzzle.Bank.Measuring;

/// <summary>
/// The rules and the bot code a game was played with, as an identity (<see cref="MeasurementFingerprint"/>). Games of
/// two epochs are never read together.
/// </summary>
public readonly record struct EpochId(ulong Value)
{
    public static EpochId Parse(string hex) =>
        hex.Length == 16 && ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value)
            ? new EpochId(value)
            : throw new FormatException($"'{hex}' is not an epoch id: 16 hex digits are expected.");

    public override string ToString() => Value.ToString("x16", CultureInfo.InvariantCulture);
}

/// <summary>
/// The player whose games a record holds, with the settings it played with. The settings are stored beside the name,
/// so games of a rung whose settings changed since are known to be another player's without replaying anyone else.
/// </summary>
public sealed record PlayerModel(string Name, int NoticePercent, double Temperature)
{
    public static PlayerModel Of(SkillProfile rung) => new(rung.Name, rung.NoticePercent, rung.Temperature);

    public SkillProfile ToProfile() => new() { Name = Name, NoticePercent = NoticePercent, Temperature = Temperature };

    /// <summary>The skill ladder still has a rung of this name that plays with these settings.</summary>
    public bool IsOnLadder => SkillLadder.TryFind(Name, out SkillProfile? rung) && rung.NoticePercent == NoticePercent && rung.Temperature == Temperature;
}

/// <summary>
/// What a batch of stored games is for. Each purpose plays its own range of bot repeats on a seed
/// (<see cref="FirstRepeat"/>), so games that check a choice are never games that made it. Closed set.
/// </summary>
public enum GamePurpose : byte
{
    /// <summary>Every rung on every seed of a candidate: what drafts, status tables and the choice of candidates read.</summary>
    Breadth = 1,

    /// <summary>The policy's player on a level's shortlisted seeds: these set the limit and choose the pool.</summary>
    Confirm = 2,

    /// <summary>The policy's player on the final pool at the final limit, on games no choice has read: what a report states.</summary>
    Audit = 3,

    /// <summary>Every rung on the seeds people really play, so a recorded attempt can be placed on the ladder.</summary>
    Profile = 4,
}

public static class GamePurposes
{
    /// <summary>Repeats one purpose may use on a seed; the next purpose's range starts where this one ends.</summary>
    public const int RepeatsPerPurpose = 1000;

    public static int FirstRepeat(this GamePurpose purpose) => ((int)purpose - 1) * RepeatsPerPurpose;
}

/// <summary>What identifies a batch of stored games: who played which candidate, on which of its seeds, with which bot repeats.</summary>
public sealed record GamesHeader
{
    public required CandidateId Candidate { get; init; }

    public required EpochId Epoch { get; init; }

    public required PlayerModel Player { get; init; }

    public required GamePurpose Purpose { get; init; }

    /// <summary>Positions in the candidate's seed list, in the order the games are stored.</summary>
    public required IReadOnlyList<int> SeedIndices { get; init; }

    /// <summary>The first bot repeat; repeat j of a seed plays with the bot seed <c>Rng.Derive(seed, j)</c>.</summary>
    public required int RepeatFrom { get; init; }

    public required int RepeatCount { get; init; }

    /// <summary>Every game was followed this far and no further.</summary>
    public required int MoveCap { get; init; }

    /// <summary>Shares of the goals, in percent, whose first move is kept for every game (near misses are read from them).</summary>
    public required IReadOnlyList<int> ProgressMarks { get; init; }

    public int GameCount => SeedIndices.Count * RepeatCount;
}

/// <summary>
/// A batch of stored games in full: for each game the move it finished on, the moves on which the goals' progress
/// first reached each mark, and four totals of how it played. Games are stored seed by seed, a seed's repeats together.
/// </summary>
public sealed class RungGames
{
    /// <summary>The finishing move of a game that was still running at the move cap.</summary>
    public const byte StillPlaying = 0;

    /// <summary>The finishing move of a game that ran out of legal moves even after shuffling.</summary>
    public const byte Stuck = 255;

    /// <summary>A progress mark the game never reached.</summary>
    public const byte NeverReached = 255;

    /// <summary>Finishing moves and marks are one byte each, with the top values taken by the codes above.</summary>
    public const int LargestMoveCap = 250;

    private const int TotalsPerGame = 4;

    private readonly byte[] _games;

    public RungGames(GamesHeader header, byte[] games)
    {
        if (games.Length != header.GameCount * BytesPerGame(header.ProgressMarks.Count))
        {
            throw new ArgumentException($"{games.Length} bytes do not hold {header.GameCount} games with {header.ProgressMarks.Count} marks.", nameof(games));
        }

        Header = header;
        _games = games;
    }

    public GamesHeader Header { get; }

    /// <summary>The games as stored: <see cref="BytesPerGame"/> bytes each.</summary>
    public ReadOnlySpan<byte> Bytes => _games;

    public static int BytesPerGame(int marks) => 1 + marks + TotalsPerGame;

    /// <summary>The finishing move of every game, in stored order: 1..cap, <see cref="StillPlaying"/> or <see cref="Stuck"/>.</summary>
    public byte[] Finishes()
    {
        int stride = BytesPerGame(Header.ProgressMarks.Count);
        var finishes = new byte[Header.GameCount];
        for (int game = 0; game < finishes.Length; game++)
        {
            finishes[game] = _games[game * stride];
        }

        return finishes;
    }

    /// <summary>The move on which game <paramref name="game"/> first had <paramref name="mark"/> (a position in the header's marks) of its goals done, or <see cref="NeverReached"/>.</summary>
    public byte ReachedAt(int game, int mark) => _games[(game * BytesPerGame(Header.ProgressMarks.Count)) + 1 + mark];

    /// <summary>Writes one finished bot game into its <see cref="BytesPerGame"/> bytes.</summary>
    internal static void Record(RunResult run, IReadOnlyList<int> progressMarks, Span<byte> game)
    {
        game[0] = run.FinalStatus == GameStatus.LostNoMovesPossible
            ? Stuck
            : run.CompletedAtMove is int finished ? (byte)finished : StillPlaying;
        for (int mark = 0; mark < progressMarks.Count; mark++)
        {
            game[1 + mark] = FirstMoveAt(run.ProgressAfterMove, progressMarks[mark] / 100f);
        }

        Span<byte> totals = game[(1 + progressMarks.Count)..];
        totals[0] = Saturated(run.Totals.Shuffles);
        totals[1] = Saturated(run.Totals.SpecialsCreated);
        totals[2] = Saturated(run.Totals.SpecialsActivated);
        totals[3] = Saturated(run.Totals.Cascades);
    }

    private static byte FirstMoveAt(IReadOnlyList<float> progressAfterMove, float share)
    {
        for (int move = 0; move < progressAfterMove.Count; move++)
        {
            if (progressAfterMove[move] >= share)
            {
                return (byte)move;
            }
        }

        return NeverReached;
    }

    private static byte Saturated(int count) => (byte)Math.Min(count, byte.MaxValue);
}
