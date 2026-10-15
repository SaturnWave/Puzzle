using System.Text.Json;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;

namespace Puzzle.Tooling.Recording;

/// <summary>
/// The JSON form of an <see cref="AttemptRecord"/>: one object on one line, camelCase fields, the schema version
/// first. Goals use the level-file vocabulary ("CollectColor", "Red"); a move is <c>[x, y]</c> for a tap or
/// <c>[fromX, fromY, toX, toY]</c> for a swap; times are ISO 8601 in UTC. Unknown fields are ignored, so later
/// schema versions may add fields without breaking older readers. The optional <c>actions</c> come last, each one
/// <c>{"act":name,"at":cells}</c> as <see cref="AttemptActionNames"/> spells it (<c>at</c> left out for an action that
/// takes no aim); a line without them reads as a record that keeps none. Just before them, the optional
/// <c>shuffleRule</c> names the rule the shuffle booster followed by its number (<see cref="ShuffleBoosterRule"/>); it is
/// left out for the first rule, so a line without it, as every line written before it existed, reads as played under
/// that one. The Unity game writes the same lines by hand
/// (unity/Assets/Scripts/Flow/AttemptLine.cs).
/// </summary>
public static class AttemptRecordJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        RespectNullableAnnotations = true,
    };

    /// <summary>The record as a single line of JSON (no line break inside, none at the end).</summary>
    public static string Serialize(AttemptRecord record) => JsonSerializer.Serialize(ToLine(record), Options);

    /// <exception cref="AttemptRecordFormatException">The text is not JSON, misses a field, has an invalid value or a newer schema version.</exception>
    public static AttemptRecord Parse(string json)
    {
        int schema = Deserialize<SchemaProbe>(json).Schema;
        if (schema < 1 || schema > AttemptRecord.SchemaVersion)
        {
            throw new AttemptRecordFormatException($"Schema version {schema} is not supported; this build reads 1..{AttemptRecord.SchemaVersion}.");
        }

        RecordLine line = Deserialize<RecordLine>(json);
        try
        {
            return ToRecord(line);
        }
        catch (Exception ex) when (ex is ArgumentException or LevelFormatException)
        {
            throw new AttemptRecordFormatException($"Not a valid attempt record: {ex.Message}", ex);
        }
    }

    private static T Deserialize<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new AttemptRecordFormatException("The line holds null instead of a record.");
        }
        catch (JsonException ex)
        {
            throw new AttemptRecordFormatException($"Not a valid attempt record: {ex.Message}", ex);
        }
    }

    private static RecordLine ToLine(AttemptRecord record)
    {
        AttemptStart start = record.Start;
        AttemptOutcome outcome = record.Outcome;
        return new RecordLine
        {
            Schema = AttemptRecord.SchemaVersion,
            Level = start.Level.Value,
            ContentVersion = start.ContentVersion.IsFirst ? null : start.ContentVersion.Value,
            Attempt = start.Attempt,
            BoardSeed = start.Seeds.Board,
            PlaySeed = start.Seeds.Play,
            MoveLimit = start.MoveLimit,
            Result = ResultName(outcome.Result),
            MovesUsed = outcome.MovesUsed,
            Goals = outcome.Goals.Select(ToGoalLine).ToArray(),
            ExtraMovesBought = outcome.ExtraMovesBought,
            BoostersUsed = outcome.BoostersUsed.ToArray(),
            DeviceId = start.Device.Value,
            Client = start.Client,
            StartedUtc = start.StartedUtc,
            EndedUtc = outcome.EndedUtc,
            Lab = record.Lab is { } lab ? ToLabLine(lab) : null,
            ShuffleRule = start.ShuffleRule == ShuffleBoosterRule.RandomOrders ? null : (int)start.ShuffleRule,
            Actions = record.Actions?.Select(ToActionLine).ToArray(),
        };
    }

    private static AttemptRecord ToRecord(RecordLine line)
    {
        var start = new AttemptStart(
            level: new LevelId(line.Level),
            contentVersion: line.ContentVersion is int version ? new ContentVersion(version) : ContentVersion.First,
            moveLimit: line.MoveLimit,
            seeds: SeedsOf(line.BoardSeed, line.PlaySeed),
            attempt: line.Attempt,
            device: new DeviceId(line.DeviceId),
            client: line.Client,
            startedUtc: line.StartedUtc,
            shuffleRule: KnownRuleOf(line.ShuffleRule) ?? ShuffleBoosterRule.RandomOrders);
        var outcome = new AttemptOutcome(
            ParseResult(line.Result),
            line.MovesUsed,
            line.Goals.Select(ToGoalOutcome).ToArray(),
            line.ExtraMovesBought,
            line.BoostersUsed,
            line.EndedUtc);
        AttemptAction[]? actions = KnownRuleOf(line.ShuffleRule) is null ? null : ToActions(line.Actions);
        return new AttemptRecord(start, outcome, line.Lab is { } lab ? ToLabDetails(lab) : null, actions);
    }

    /// <summary>
    /// The shuffle booster rule a line names, the first one when it names none; nothing for a rule this build does not
    /// know, a rule of a later game. Such a line reads as one with an action this build does not know: it still counts,
    /// and keeps no actions, as they cannot be replayed here.
    /// </summary>
    /// <exception cref="ArgumentException">The number is no rule number at all.</exception>
    private static ShuffleBoosterRule? KnownRuleOf(int? number) => number switch
    {
        null => ShuffleBoosterRule.RandomOrders,
        < 1 or > byte.MaxValue => throw new ArgumentException($"Shuffle rule {number} is no rule number; rules are numbered from 1."),
        _ when Enum.IsDefined(typeof(ShuffleBoosterRule), (byte)number) => (ShuffleBoosterRule)number,
        _ => null,
    };

    /// <summary><see cref="AttemptSeeds.WithFixedBoard"/> is the general two-seed form; for equal seeds it equals <see cref="AttemptSeeds.From"/>.</summary>
    private static AttemptSeeds SeedsOf(ulong board, ulong play) => AttemptSeeds.WithFixedBoard(board, play);

    private static string ResultName(AttemptResult result) => result switch
    {
        AttemptResult.Won => "won",
        AttemptResult.OutOfMoves => "outOfMoves",
        AttemptResult.NoMovesPossible => "noMovesPossible",
        AttemptResult.Abandoned => "abandoned",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown attempt result."),
    };

    private static AttemptResult ParseResult(string name) => name switch
    {
        "won" => AttemptResult.Won,
        "outOfMoves" => AttemptResult.OutOfMoves,
        "noMovesPossible" => AttemptResult.NoMovesPossible,
        "abandoned" => AttemptResult.Abandoned,
        _ => throw new ArgumentException($"Unknown result '{name}' (use won, outOfMoves, noMovesPossible or abandoned)."),
    };

    private static GoalLine ToGoalLine(GoalOutcome goal)
    {
        GoalFile file = LevelFileMapper.ToGoalFile(goal.Goal);
        return new GoalLine { Type = file.type, Color = file.color, Target = file.count, Done = goal.Done };
    }

    /// <exception cref="LevelFormatException">Unknown goal type or color.</exception>
    private static GoalOutcome ToGoalOutcome(GoalLine? goal) => goal is null
        ? throw new ArgumentException("A goal cannot be null.")
        : new GoalOutcome(LevelFileMapper.ParseGoal(new GoalFile { type = goal.Type, color = goal.Color, count = goal.Target }), goal.Done);

    private static LabLine ToLabLine(LabDetails lab) => new()
    {
        LevelKey = lab.LevelKey,
        LevelFingerprint = lab.LevelFingerprint,
        AssistPercent = lab.AssistPercent,
        HintsShown = lab.HintsShown,
        BotMoves = lab.BotMoves,
        Moves = lab.Moves.Select(MoveNumbers.Of).ToArray(),
    };

    private static LabDetails ToLabDetails(LabLine lab) => new(
        levelKey: lab.LevelKey,
        levelFingerprint: lab.LevelFingerprint,
        assistPercent: lab.AssistPercent,
        hintsShown: lab.HintsShown,
        botMoves: lab.BotMoves,
        moves: lab.Moves.Select(MoveNumbers.ToMove).ToArray());

    private static ActionLine ToActionLine(AttemptAction action) => new()
    {
        Act = AttemptActionNames.Of(action),
        At = AttemptActionNames.CellsOf(action),
    };

    /// <summary>
    /// The actions a line keeps; none when it has no list, and none either when the list holds an action this build
    /// does not know (a booster of a later game): such a record still reads and counts, as its <c>boostersUsed</c> may
    /// name that booster too, and only cannot be replayed here.
    /// </summary>
    /// <exception cref="ArgumentException">An action of a known kind is aimed at cells that do not fit it.</exception>
    private static AttemptAction[]? ToActions(ActionLine[]? lines)
    {
        if (lines is null)
        {
            return null;
        }

        var actions = new AttemptAction[lines.Length];
        for (int index = 0; index < lines.Length; index++)
        {
            // The reader leaves the elements of a list as the line has them, null too.
            ActionLine line = lines[index] ?? throw new ArgumentException("An action cannot be null.");
            if (AttemptActionNames.Named(line.Act, line.At) is not { } action)
            {
                return null;
            }

            actions[index] = action;
        }

        return actions;
    }

    // Wire shapes. Declaration order is field order in the line; "required" makes a missing field a format error.
    private sealed class SchemaProbe
    {
        public required int Schema { get; init; }
    }

    private sealed class RecordLine
    {
        public required int Schema { get; init; }

        public required int Level { get; init; }

        /// <summary>Absent for the first version of the level's content, which is every record from before patches existed.</summary>
        public int? ContentVersion { get; init; }

        /// <summary>Absent for hand-chosen seeds.</summary>
        public int? Attempt { get; init; }

        public required ulong BoardSeed { get; init; }

        public required ulong PlaySeed { get; init; }

        public required int MoveLimit { get; init; }

        public required string Result { get; init; }

        public required int MovesUsed { get; init; }

        public required GoalLine[] Goals { get; init; }

        public required bool ExtraMovesBought { get; init; }

        public required string[] BoostersUsed { get; init; }

        public required string DeviceId { get; init; }

        public required string Client { get; init; }

        public required DateTimeOffset StartedUtc { get; init; }

        public required DateTimeOffset EndedUtc { get; init; }

        /// <summary>Web lab only.</summary>
        public LabLine? Lab { get; init; }

        /// <summary>The number of the shuffle booster's rule; absent for the first rule, which every record played under before it existed.</summary>
        public int? ShuffleRule { get; init; }

        /// <summary>What the player did, in order; absent in the lab's records and in records from before the list existed.</summary>
        public ActionLine[]? Actions { get; init; }
    }

    private sealed class ActionLine
    {
        /// <summary>The action's name: "move", a booster's name, "extra-moves", "shuffle-refused" or "extra-moves-refused".</summary>
        public required string Act { get; init; }

        /// <summary>The cells the action was aimed at; absent for an action that takes no aim.</summary>
        public int[]? At { get; init; }
    }

    private sealed class GoalLine
    {
        public required string Type { get; init; }

        /// <summary>Only for CollectColor goals.</summary>
        public string? Color { get; init; }

        public required int Target { get; init; }

        public required int Done { get; init; }
    }

    private sealed class LabLine
    {
        public required string LevelKey { get; init; }

        public required string LevelFingerprint { get; init; }

        public required int AssistPercent { get; init; }

        public required int HintsShown { get; init; }

        public required int BotMoves { get; init; }

        public required int[][] Moves { get; init; }
    }
}
