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

public sealed record LevelSummaryDto(string Key, int Id, string Difficulty, int Width, int Height, int Moves, int Colors, string[] Goals);

public sealed record SessionDto(string SessionId, uint Seed, int Assist, LevelSummaryDto Level, StateDto State);

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

public sealed record MoveResultDto(bool Accepted, MoveDto? Move, IReadOnlyList<EventDto> Events, StateDto State, StatsDto? Stats);

public sealed record NewSessionRequest(string LevelKey, uint? Seed, int? Assist);

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
