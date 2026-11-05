using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Economy.Units;

/// <summary>
/// What a game's first board carries. A board with a special on it plays differently from the first move on, so each
/// start has measured games of its own. The extra-moves booster is no start of its own: it leaves the board alone and
/// is read from the plain game three moves further. Closed set.
/// </summary>
public enum BoardStart
{
    Plain,
    RocketAndBomb,
    ColorBomb,
}

public static class BoardStarts
{
    public static IReadOnlyList<BoardStart> All { get; } = Enum.GetValues<BoardStart>();

    /// <summary>The start boosters a game of this start is begun with.</summary>
    public static IReadOnlyList<StartBooster> Boosters(this BoardStart start) => start switch
    {
        BoardStart.Plain => [],
        BoardStart.RocketAndBomb => [StartBooster.RocketAndBomb],
        BoardStart.ColorBomb => [StartBooster.ColorBomb],
        _ => throw new ArgumentOutOfRangeException(nameof(start), start, "Unknown board start."),
    };

    /// <summary>The start's name in files: "plain", or the record name of its booster.</summary>
    public static string FileName(this BoardStart start) => start switch
    {
        BoardStart.Plain => "plain",
        BoardStart.RocketAndBomb => BoosterNames.Of(StartBooster.RocketAndBomb),
        BoardStart.ColorBomb => BoosterNames.Of(StartBooster.ColorBomb),
        _ => throw new ArgumentOutOfRangeException(nameof(start), start, "Unknown board start."),
    };

    /// <summary>The start a file names; absent for any other text.</summary>
    public static BoardStart? Find(string fileName)
    {
        foreach (BoardStart start in All)
        {
            if (start.FileName() == fileName)
            {
                return start;
            }
        }

        return null;
    }
}

/// <summary>The seven boosters of the game (docs/GAME_DESIGN.md section 18), in the engine's order. Closed set.</summary>
public enum BoosterKind
{
    StartRocketAndBomb,
    StartColorBomb,
    StartExtraMoves,
    HitCell,
    HitRow,
    HitColumn,
    Shuffle,
}

public static class BoosterKinds
{
    public static IReadOnlyList<BoosterKind> All { get; } = Enum.GetValues<BoosterKind>();

    /// <summary>The name the booster goes by in attempt records, and so in the settings and the reports.</summary>
    public static string Name(this BoosterKind kind) => kind switch
    {
        BoosterKind.StartRocketAndBomb => BoosterNames.Of(StartBooster.RocketAndBomb),
        BoosterKind.StartColorBomb => BoosterNames.Of(StartBooster.ColorBomb),
        BoosterKind.StartExtraMoves => BoosterNames.Of(StartBooster.ExtraMoves),
        BoosterKind.HitCell => BoosterNames.Of(new BoosterUse.HitCell(default(Position))),
        BoosterKind.HitRow => BoosterNames.Of(new BoosterUse.HitRow(default(Position))),
        BoosterKind.HitColumn => BoosterNames.Of(new BoosterUse.HitColumn(default(Position))),
        BoosterKind.Shuffle => BoosterNames.Of(new BoosterUse.Shuffle()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
    };

    /// <summary>Taken before the level starts.</summary>
    public static bool IsStart(this BoosterKind kind) => kind switch
    {
        BoosterKind.StartRocketAndBomb or BoosterKind.StartColorBomb or BoosterKind.StartExtraMoves => true,
        BoosterKind.HitCell or BoosterKind.HitRow or BoosterKind.HitColumn or BoosterKind.Shuffle => false,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
    };

    /// <summary>Used during play, at no cost of a move.</summary>
    public static bool IsInLevel(this BoosterKind kind) => !kind.IsStart();

    /// <summary>The board a start booster gives; <see cref="BoardStart.Plain"/> for every booster that leaves the first board alone.</summary>
    public static BoardStart BoardStart(this BoosterKind kind) => kind switch
    {
        BoosterKind.StartRocketAndBomb => Units.BoardStart.RocketAndBomb,
        BoosterKind.StartColorBomb => Units.BoardStart.ColorBomb,
        BoosterKind.StartExtraMoves or BoosterKind.HitCell or BoosterKind.HitRow or BoosterKind.HitColumn or BoosterKind.Shuffle => Units.BoardStart.Plain,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown booster."),
    };

    /// <summary>The booster a record name names; absent for any other text.</summary>
    public static BoosterKind? Find(string name)
    {
        foreach (BoosterKind kind in All)
        {
            if (kind.Name() == name)
            {
                return kind;
            }
        }

        return null;
    }
}
