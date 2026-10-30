#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// An attempt's record as the one line of JSON the game appends to its log (<see cref="AttemptLogFile"/>), in the
    /// wire form the tools read (Puzzle.Tooling's AttemptRecordJson, schema 1): the same fields in the same order, and an
    /// optional field that is absent is left out, never written as a zero. The line is built by hand: the game has no
    /// System.Text.Json, and Unity's JSON writer writes every field a document has. A record of the game has no lab
    /// part; its move limit is the level's own, without what a start booster or the extra moves added. An attempt
    /// played in the tester mode says so (<c>"testerMode":true</c>); any other leaves the field out. An attempt in which
    /// the game showed hints by itself counts them (<c>"hintsShown":2</c>), one in which its tutorial showed moves
    /// by hand counts those (<c>"guidedMoves":1</c>), and one in which its tutorial had a booster used by hand, free,
    /// names it (<c>"guidedBoosters":["hit-cell"]</c>, also in <c>boostersUsed</c>); one without leaves the field out.
    /// This file has no Unity dependency, so the repository's tests compile it too and hold its lines against the
    /// tools' reader and writer.
    /// </summary>
    public static class AttemptLine
    {
        /// <summary>The record as one line of JSON, with no line break in it or at its end.</summary>
        /// <exception cref="ArgumentException">The record has a lab part, which only the web lab writes.</exception>
        public static string Of(AttemptRecord record)
        {
            if (record.Lab != null)
            {
                throw new ArgumentException("A record of the game has no lab part; only the web lab writes one.", nameof(record));
            }

            AttemptStart start = record.Start;
            AttemptOutcome outcome = record.Outcome;
            var line = new StringBuilder("{", capacity: 512);
            Number(line, "schema", AttemptRecord.SchemaVersion);
            Number(line, "level", start.Level.Value);
            if (!start.ContentVersion.IsFirst)
            {
                Number(line, "contentVersion", start.ContentVersion.Value);
            }

            if (start.Attempt is int attempt)
            {
                Number(line, "attempt", attempt);
            }

            Number(line, "boardSeed", start.Seeds.Board);
            Number(line, "playSeed", start.Seeds.Play);
            Number(line, "moveLimit", start.MoveLimit);
            Text(line, "result", ResultName(outcome.Result));
            Number(line, "movesUsed", outcome.MovesUsed);
            List(line, "goals", outcome.Goals.Select(Goal));
            Field(line, "extraMovesBought").Append(outcome.ExtraMovesBought ? "true" : "false");
            List(line, "boostersUsed", outcome.BoostersUsed.Select(Quoted));
            Text(line, "deviceId", start.Device.Value);
            Text(line, "client", start.Client);
            Text(line, "startedUtc", Time(start.StartedUtc));
            Text(line, "endedUtc", Time(outcome.EndedUtc));
            if (record.TesterMode)
            {
                Field(line, "testerMode").Append("true");
            }

            if (record.HintsShown > 0)
            {
                Number(line, "hintsShown", record.HintsShown);
            }

            if (record.GuidedMoves > 0)
            {
                Number(line, "guidedMoves", record.GuidedMoves);
            }

            if (record.GuidedBoosters.Count > 0)
            {
                List(line, "guidedBoosters", record.GuidedBoosters.Select(Quoted));
            }

            if (start.ShuffleRule != ShuffleBoosterRule.RandomOrders)
            {
                Number(line, "shuffleRule", (long)start.ShuffleRule);
            }

            if (record.Actions is IReadOnlyList<AttemptAction> actions)
            {
                List(line, "actions", actions.Select(Action));
            }

            return line.Append('}').ToString();
        }

        private static string ResultName(AttemptResult result)
        {
            switch (result)
            {
                case AttemptResult.Won:
                    return "won";
                case AttemptResult.OutOfMoves:
                    return "outOfMoves";
                case AttemptResult.NoMovesPossible:
                    return "noMovesPossible";
                case AttemptResult.Abandoned:
                    return "abandoned";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown attempt result.");
            }
        }

        /// <summary>A goal in the level files' words: <c>{"type":"CollectColor","color":"Red","target":40,"done":41}</c>, the colour only for a colour.</summary>
        private static string Goal(GoalOutcome goal)
        {
            GoalFile file = LevelFileMapper.ToGoalFile(goal.Goal);
            var text = new StringBuilder("{");
            Text(text, "type", file.type);
            if (file.color != null)
            {
                Text(text, "color", file.color);
            }

            Number(text, "target", file.count);
            Number(text, "done", goal.Done);
            return text.Append('}').ToString();
        }

        /// <summary>An action as <see cref="AttemptActionNames"/> spells it: <c>{"act":"move","at":[3,4,3,5]}</c>, with no <c>at</c> for one that takes no aim.</summary>
        private static string Action(AttemptAction action)
        {
            var text = new StringBuilder("{");
            Text(text, "act", AttemptActionNames.Of(action));
            if (AttemptActionNames.CellsOf(action) is int[] cells)
            {
                List(text, "at", cells.Select(cell => cell.ToString(CultureInfo.InvariantCulture)));
            }

            return text.Append('}').ToString();
        }

        /// <summary>
        /// An instant in UTC as the tools' writer has it: to the second, a fraction of a second only when there is one
        /// and without its trailing zeros, and the offset written out.
        /// </summary>
        private static string Time(DateTimeOffset instant)
        {
            DateTimeOffset utc = instant.ToUniversalTime();
            string seconds = utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss", CultureInfo.InvariantCulture);
            long fraction = utc.Ticks % TimeSpan.TicksPerSecond;
            string fractionDigits = fraction == 0 ? string.Empty : "." + fraction.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
            return seconds + fractionDigits + "+00:00";
        }

        /// <summary>The field's name, after a comma unless it is the first of its object.</summary>
        private static StringBuilder Field(StringBuilder json, string name)
        {
            if (json.Length > 1)
            {
                json.Append(',');
            }

            return json.Append('"').Append(name).Append("\":");
        }

        private static void Number(StringBuilder json, string name, long value) =>
            Field(json, name).Append(value.ToString(CultureInfo.InvariantCulture));

        private static void Number(StringBuilder json, string name, ulong value) =>
            Field(json, name).Append(value.ToString(CultureInfo.InvariantCulture));

        private static void Text(StringBuilder json, string name, string value) => Field(json, name).Append(Quoted(value));

        /// <summary>A list of values that are written already.</summary>
        private static void List(StringBuilder json, string name, IEnumerable<string> values) =>
            Field(json, name).Append('[').Append(string.Join(",", values)).Append(']');

        /// <summary>A JSON string: a quote, a backslash and a control character are escaped, everything else is written as it is.</summary>
        private static string Quoted(string value)
        {
            var text = new StringBuilder(value.Length + 2).Append('"');
            foreach (char c in value)
            {
                if (c == '"' || c == '\\')
                {
                    text.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.Append('"').ToString();
        }
    }
}
