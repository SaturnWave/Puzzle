using System.Text.Json;
using Puzzle.Core.Engine;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Tooling;

namespace Puzzle.Web.Api;

/// <summary>Translates engine state and events into the browser contract.</summary>
internal static class StateMapper
{
    public static StateDto ToState(Game game)
    {
        Board board = game.Board;
        int cells = board.Width * board.Height;
        var playable = new bool[cells];
        var tiles = new TileDto?[cells];
        var chains = new int[cells];
        var jelly = new int[cells];
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                int i = (y * board.Width) + x;
                var p = new Position(x, y);
                playable[i] = board.Shape.IsPlayable(p);
                tiles[i] = playable[i] ? ToTile(board.TileAt(p)) : null;
                chains[i] = board.ChainAt(p);
                jelly[i] = board.JellyAt(p);
            }
        }

        var goals = new GoalDto[game.Goals.Count];
        for (int g = 0; g < goals.Length; g++)
        {
            GoalProgress progress = game.Goals[g];
            goals[g] = new GoalDto(GoalType(progress.Goal), (progress.Goal as GoalSpec.CollectColor)?.Color.ToString(), progress.Goal.Target, progress.Done);
        }

        return new StateDto(
            board.Width,
            board.Height,
            playable,
            tiles,
            chains,
            jelly,
            goals,
            game.MovesUsed,
            game.MoveLimit,
            game.Status.ToString(),
            game.Level.Spawn.Palette.Select(c => c.ToString()).ToArray());
    }

    public static LevelSummaryDto ToSummary(LibraryLevel entry)
    {
        LevelDefinition level = entry.Level;
        return new LevelSummaryDto(
            entry.Key,
            level.Id.Value,
            level.Difficulty.ToString(),
            level.Layout.Shape.Width,
            level.Layout.Shape.Height,
            level.MoveLimit,
            level.Spawn.Colors,
            level.Goals.Select(DescribeGoal).ToArray(),
            StartBoardName(level.Attempts.Board),
            level.Attempts.SeedPool.Count);
    }

    public static string StartBoardName(StartBoard board) => board switch
    {
        StartBoard.Varied _ => "varied",
        StartBoard.Fixed _ => "fixed",
        _ => throw new InvalidOperationException($"Unhandled start board {board.GetType().Name}."),
    };

    public static MoveDto ToMove(Move move) => move.Kind == MoveKind.Tap
        ? new MoveDto("tap", ToPosition(move.From), null)
        : new MoveDto("swap", ToPosition(move.From), ToPosition(move.To));

    /// <exception cref="ArgumentException">The DTO does not describe a valid move shape.</exception>
    public static Move ToMove(MoveDto move) => move.Kind switch
    {
        "tap" => Move.Tap(new Position(move.From.X, move.From.Y)),
        "swap" when move.To is not null => Move.Swap(new Position(move.From.X, move.From.Y), new Position(move.To.X, move.To.Y)),
        _ => throw new ArgumentException($"Unknown move kind '{move.Kind}' or missing target.", nameof(move)),
    };

    public static StatsDto ToStats(PlayStats stats) =>
        new StatsDto(stats.GemsCleared, stats.SpecialsCreated, stats.SpecialsActivated, stats.Cascades, stats.Shuffles);

    public static EventDto ToEvent(GameEvent gameEvent) => gameEvent switch
    {
        GameEvent.Swapped e => new EventDto("swapped") { From = ToPosition(e.From), To = ToPosition(e.To) },
        GameEvent.BoosterUsed e => new EventDto("boosterUsed") { Kind = BoosterNames.Of(e.Booster), At = AimOf(e.Booster) },
        GameEvent.RoundStarted e => new EventDto("roundStarted") { Round = e.Round },
        GameEvent.MatchCleared e => new EventDto("matchCleared") { Color = e.Color.ToString(), Cells = e.Cells.Select(ToPosition).ToArray() },
        GameEvent.SpecialCreated e => new EventDto("specialCreated") { At = ToPosition(e.At), Tile = ToTile(Tile.Special(e.Kind)) },
        GameEvent.EffectTriggered e => new EventDto("effectTriggered") { At = ToPosition(e.Origin), Kind = Camel(e.Kind.ToString()), Radius = e.Radius },
        GameEvent.SeekerLaunched e => new EventDto("seekerLaunched") { From = ToPosition(e.From), To = ToPosition(e.Target) },
        GameEvent.TileDestroyed e => new EventDto("tileDestroyed") { At = ToPosition(e.At), Tile = ToTile(e.Tile) },
        GameEvent.BoxDamaged e => new EventDto("boxDamaged") { At = ToPosition(e.At), Hp = e.HpLeft },
        GameEvent.ChainDamaged e => new EventDto("chainDamaged") { At = ToPosition(e.At), Hp = e.HpLeft },
        GameEvent.JellyDamaged e => new EventDto("jellyDamaged") { At = ToPosition(e.At), Hp = e.LayersLeft },
        GameEvent.TileMoved e => new EventDto("tileMoved") { From = ToPosition(e.From), To = ToPosition(e.To) },
        GameEvent.TileSpawned e => new EventDto("tileSpawned") { At = ToPosition(e.At), Tile = ToTile(e.Tile) },
        GameEvent.ItemCollected e => new EventDto("itemCollected") { At = ToPosition(e.At) },
        GameEvent.Shuffled _ => new EventDto("shuffled"),
        _ => throw new InvalidOperationException($"Unmapped event {gameEvent.GetType().Name}."),
    };

    /// <summary>The cell a booster was aimed at; absent for one that takes no aim.</summary>
    private static PositionDto? AimOf(BoosterUse booster) => booster switch
    {
        BoosterUse.HitCell cell => ToPosition(cell.At),
        BoosterUse.HitRow row => ToPosition(row.At),
        BoosterUse.HitColumn column => ToPosition(column.At),
        BoosterUse.Shuffle _ => null,
        _ => throw new InvalidOperationException($"Unmapped booster {booster.GetType().Name}."),
    };

    private static TileDto? ToTile(Tile tile) => tile.Kind switch
    {
        TileKind.Empty => null,
        TileKind.Gem => new TileDto("gem", tile.Color.ToString(), 0),
        _ => new TileDto(Camel(tile.Kind.ToString()), null, tile.Hp),
    };

    private static PositionDto ToPosition(Position p) => new PositionDto(p.X, p.Y);

    private static string Camel(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

    private static string GoalType(GoalSpec goal) => goal switch
    {
        GoalSpec.CollectColor _ => "collectColor",
        GoalSpec.BreakBoxes _ => "breakBoxes",
        GoalSpec.ClearJelly _ => "clearJelly",
        GoalSpec.BreakChains _ => "breakChains",
        GoalSpec.DropItems _ => "dropItems",
        _ => throw new InvalidOperationException($"Unhandled goal {goal.GetType().Name}."),
    };

    private static string DescribeGoal(GoalSpec goal) => goal is GoalSpec.CollectColor collect
        ? $"{collect.Target} {collect.Color}"
        : $"{goal.Target} {GoalType(goal)}";
}
