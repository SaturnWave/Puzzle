namespace Puzzle.Web.Api;

// Wire contract of the test lab. Engine types never cross the HTTP boundary directly: StateMapper translates
// them, so the engine can evolve without breaking the browser code (and vice versa).

public sealed record PositionDto(int X, int Y);

public sealed record TileDto(string Kind, string? Color, int Hp);

public sealed record GoalDto(string Type, string? Color, int Target, int Done);

/// <summary>Board and progress; per-cell arrays are row-major (index = y * width + x).</summary>
public sealed record StateDto(
    int Width,
    int Height,
    bool[] Playable,
    TileDto?[] Tiles,
    int[] Chains,
    int[] Jelly,
    GoalDto[] Goals,
    int MovesUsed,
    int MoveLimit,
    string Status,
    string[] Palette);

/// <summary>A level in the picker; <see cref="StartBoard"/> is "varied" or "fixed", <see cref="PoolSize"/> the calibrated retry seeds.</summary>
public sealed record LevelSummaryDto(string Key, int Id, string Difficulty, int Width, int Height, int Moves, int Colors, string[] Goals, string StartBoard, int PoolSize);

/// <summary>
/// One attempt. <see cref="Attempt"/> is the retry number when the seeds came from the level's policy (absent for
/// an explicit seed); <see cref="FromPool"/> tells whether they came from the calibrated equal-difficulty pool.
/// </summary>
public sealed record SessionDto(
    string SessionId,
    int? Attempt,
    bool FromPool,
    string StartBoard,
    ulong BoardSeed,
    ulong PlaySeed,
    int Assist,
    LevelSummaryDto Level,
    StateDto State,
    RecordingDto Recording);

/// <summary>
/// How the attempt counts in the recordings. <see cref="Standing"/> is "comparable" (compared with the bots), "assisted"
/// (help from the lab) or "chosenSeeds" (seeds outside the retry sequence); the last two are left out of the comparison.
/// <see cref="SequenceBreak"/> says why the attempt is outside its device's retries — "chosenByHand",
/// "startBoardOverride", "deviceNotKept" or "poolReplay" — and is absent when it is the device's next retry.
/// <see cref="Record"/> is how far its record has come: "open" (still being played), "waiting" (the log refused it; the
/// lab tries again), "written" or "lost".
/// </summary>
public sealed record RecordingDto(string Standing, string? SequenceBreak, string Record);

/// <summary>A move; <see cref="To"/> is absent for a tap.</summary>
public sealed record MoveDto(string Kind, PositionDto From, PositionDto? To);

public sealed record StatsDto(int GemsCleared, int SpecialsCreated, int SpecialsActivated, int Cascades, int Shuffles);

/// <summary>One animation step; which fields are present depends on <see cref="Type"/>.</summary>
public sealed record EventDto(string Type)
{
    public PositionDto? At { get; init; }

    public PositionDto? From { get; init; }

    public PositionDto? To { get; init; }

    public PositionDto[]? Cells { get; init; }

    public string? Kind { get; init; }

    public string? Color { get; init; }

    public TileDto? Tile { get; init; }

    public int? Hp { get; init; }

    public int? Radius { get; init; }

    public int? Round { get; init; }
}

public sealed record MoveResultDto(bool Accepted, MoveDto? Move, IReadOnlyList<EventDto> Events, StateDto State, StatsDto? Stats, RecordingDto Recording);

/// <summary>
/// Starts an attempt. Seeds, by priority: both <see cref="BoardSeed"/> and <see cref="PlaySeed"/> (exact repeat),
/// <see cref="PlaySeed"/> alone (board follows the start-board rule), retry number <see cref="Attempt"/> (a replay of that
/// retry, recorded as hand-chosen), otherwise the device's next retry on the level, numbered by the server.
/// <see cref="StartBoard"/> overrides the level's rule for experiments: "level", "varied" or "fixed".
/// <see cref="Replaces"/> is the session the player leaves for this one; an undecided attempt there is recorded as abandoned.
/// <see cref="TransientDeviceId"/> is true when the browser could keep the device id only for this tab or page load: the
/// device's retries cannot then be numbered across page loads, so its attempts stay out of the comparison.
/// </summary>
public sealed record NewSessionRequest(
    string LevelKey,
    int? Attempt,
    ulong? PlaySeed,
    ulong? BoardSeed,
    string? StartBoard,
    int? Assist,
    string? Replaces,
    bool? TransientDeviceId);

public sealed record BotRequest(string? Bot);

public sealed record SolveRequest(int? Beam);

public sealed record SolveDto(bool Solved, int? MovesNeeded, int MovesLeft, MoveDto[] Path, int PositionsExplored, long ElapsedMs);

public sealed record SimulateRequest(string LevelKey, string? Bot, int? Runs);

public sealed record CurvePointDto(int Moves, double WinRate);

public sealed record SimulationDto(
    string Bot,
    int Runs,
    int MoveLimit,
    double Completion,
    double WinAtLimit,
    double NearMissAtLimit,
    MovesNeededDto MovesNeeded,
    CurvePointDto[] Curve,
    double AverageShuffles,
    double SpecialsPerRun,
    double CascadesPerMove,
    long ElapsedMs);

/// <summary>Moves needed by 10/25/50/75/90 % of runs; absent when that share never finished.</summary>
public sealed record MovesNeededDto(int? P10, int? P25, int? P50, int? P75, int? P90);

/// <summary>
/// Comparable attempts as the bot comparison counts them: decided at the limit (finished) or abandoned before it.
/// <see cref="WinRate"/> covers the finished ones, <see cref="WinRateLowerBound"/> counts every abandoned one as a loss;
/// both are absent without attempts to share.
/// </summary>
public sealed record PlayDto(int Finished, int Abandoned, int Wins, int Devices, double? WinRate, double? WinRateLowerBound);

/// <summary>One level version at one move limit; <see cref="LevelKey"/> is absent for the released game's records.</summary>
public sealed record LevelAttemptsDto(
    int LevelId,
    string? LevelKey,
    int MoveLimit,
    PlayDto Play,
    double? MedianWinMoves,
    int Assisted,
    int ChosenSeeds,
    DateTimeOffset LastPlayedUtc);

/// <summary>
/// The recorded attempts of a scope ("device" or "all"). <see cref="Levels"/> lists the most recently played level
/// versions first, and at most a page of them; <see cref="LevelCount"/> counts them all. <see cref="Assisted"/> and
/// <see cref="ChosenSeeds"/> attempts are left out of <see cref="Overall"/> and of the comparison.
/// <see cref="Players"/> lists the public mode's players by username, the one who played last first; it is present only
/// on a public server, for the scope "all".
/// </summary>
public sealed record RecordingsDto(
    string Scope,
    int Records,
    int UnreadableLines,
    PlayDto Overall,
    int Assisted,
    int ChosenSeeds,
    int LevelCount,
    LevelAttemptsDto[] Levels,
    PlayerAttemptsDto[]? Players);

/// <summary>Compares a scope's attempts ("device", the default, or "all") with the bots; <see cref="Runs"/> games per bot and attempt.</summary>
public sealed record CompareRequest(string? Scope, int? Runs);

/// <summary>
/// One bot's win rates, each on the games its human rate covers: <see cref="WinRateOnFinished"/> on the finished
/// attempts' games, beside <see cref="PlayDto.WinRate"/> (absent when none finished), and <see cref="WinRateOnAll"/> on
/// every attempt's game, beside <see cref="PlayDto.WinRateLowerBound"/>.
/// </summary>
public sealed record BotRateDto(string Bot, double? WinRateOnFinished, double WinRateOnAll);

/// <summary>
/// One row of the comparison; the level fields are absent on the overall row. <see cref="ClosestBot"/> is matched on
/// the finished attempts' games, <see cref="ClosestBotAtLowerBound"/> on every attempt's game.
/// </summary>
public sealed record ComparisonRowDto(
    int? LevelId,
    string? LevelKey,
    int? MoveLimit,
    double? MedianWinMoves,
    PlayDto Play,
    BotRateDto[] Bots,
    string? ClosestBot,
    string? ClosestBotAtLowerBound,
    bool AbandonsDecideClosest);

/// <summary>What happened to each record of the comparison: compared, or left out and why.</summary>
public sealed record TallyDto(int Compared, int Assisted, int ChosenSeeds, int UnknownLevel);

/// <summary>
/// The people's attempts next to the bots on the same games. Of the scope's <see cref="Comparable"/> attempts, only the
/// latest <see cref="ComparedLimit"/> are compared; <see cref="Bots"/> lists the profiles weakest first.
/// </summary>
public sealed record ComparisonDto(
    string Scope,
    int BotRuns,
    int Comparable,
    int ComparedLimit,
    TallyDto Records,
    string[] Bots,
    ComparisonRowDto Overall,
    ComparisonRowDto[] Levels,
    long ElapsedMs);
