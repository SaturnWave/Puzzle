using System;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Core.Recording
{
    /// <summary>
    /// One thing the player did in an attempt that a replay has to do again, at the same place in the order, to come
    /// to the same game (docs/GAME_DESIGN.md §18): a move, a booster used with its aim, the "+5 moves" taken, and two
    /// requests the game refused that still drew from its shuffle stream (a shuffle that found no other arrangement,
    /// and the extra moves for a board without a move that no shuffle brought back). An illegal move and a strike with
    /// nothing to hit change nothing and draw nothing, so they are no actions. Closed set (private base constructor);
    /// the game keeps the list itself (<see cref="Game.Actions"/>, while <see cref="GameOptions.RecordActions"/> is on),
    /// so every client that records attempts records the same.
    /// </summary>
    public abstract record AttemptAction
    {
        private AttemptAction()
        {
        }

        /// <summary>A move the game accepted.</summary>
        public sealed record MovePlayed(Move Move) : AttemptAction;

        /// <summary>A booster the game accepted, with its aim.</summary>
        public sealed record BoosterUsed(BoosterUse Booster) : AttemptAction;

        /// <summary>The "+5 moves" taken by a game that had run out of moves.</summary>
        public sealed record ExtraMovesTaken : AttemptAction;

        /// <summary>A shuffle booster the game refused because it found no other arrangement, after drawing from the shuffle stream.</summary>
        public sealed record ShuffleRefused : AttemptAction;

        /// <summary>The "+5 moves" refused for a board without a move that no shuffle brought back; the shuffles it tried drew from the shuffle stream.</summary>
        public sealed record ExtraMovesRefused : AttemptAction;
    }

    /// <summary>
    /// How attempt records spell an action: a name, and the cells it was aimed at. A move is "move" at <c>[x, y]</c> for
    /// a tap or <c>[fromX, fromY, toX, toY]</c> for a swap, as the lab writes its moves; a booster goes by its record
    /// name (<see cref="BoosterNames"/>) at the cell it was aimed at, the shuffle at none; the rest are "extra-moves",
    /// "shuffle-refused" and "extra-moves-refused", at none. The names are written into files that outlive a build: a
    /// name is never changed or given to another action.
    /// </summary>
    public static class AttemptActionNames
    {
        private const string MoveName = "move";
        private const string ExtraMovesName = "extra-moves";
        private const string ShuffleRefusedName = "shuffle-refused";
        private const string ExtraMovesRefusedName = "extra-moves-refused";

        public static string Of(AttemptAction action) => action switch
        {
            AttemptAction.MovePlayed _ => MoveName,
            AttemptAction.BoosterUsed used => BoosterNames.Of(used.Booster),
            AttemptAction.ExtraMovesTaken _ => ExtraMovesName,
            AttemptAction.ShuffleRefused _ => ShuffleRefusedName,
            AttemptAction.ExtraMovesRefused _ => ExtraMovesRefusedName,
            _ => throw new InvalidOperationException($"Unhandled action {action.GetType().Name}."),
        };

        /// <summary>The cells the action was aimed at, as records write them; nothing for an action that takes no aim.</summary>
        public static int[]? CellsOf(AttemptAction action) => action switch
        {
            AttemptAction.MovePlayed { Move: { Kind: MoveKind.Tap } tap } => new[] { tap.From.X, tap.From.Y },
            AttemptAction.MovePlayed played => new[] { played.Move.From.X, played.Move.From.Y, played.Move.To.X, played.Move.To.Y },
            AttemptAction.BoosterUsed { Booster: BoosterUse.HitCell cell } => CellNumbers(cell.At),
            AttemptAction.BoosterUsed { Booster: BoosterUse.HitRow row } => CellNumbers(row.At),
            AttemptAction.BoosterUsed { Booster: BoosterUse.HitColumn column } => CellNumbers(column.At),
            _ => null,
        };

        /// <summary>The action a record spells; nothing for a name this build does not know, such as a booster of a later game.</summary>
        /// <param name="cells">The cells the record names the action aimed at; nothing for none.</param>
        /// <exception cref="ArgumentException">The name is known, and the cells do not fit it.</exception>
        public static AttemptAction? Named(string name, int[]? cells) => name switch
        {
            MoveName => new AttemptAction.MovePlayed(MoveAt(cells)),
            BoosterNames.HitCellName => new AttemptAction.BoosterUsed(new BoosterUse.HitCell(CellAt(name, cells))),
            BoosterNames.HitRowName => new AttemptAction.BoosterUsed(new BoosterUse.HitRow(CellAt(name, cells))),
            BoosterNames.HitColumnName => new AttemptAction.BoosterUsed(new BoosterUse.HitColumn(CellAt(name, cells))),
            BoosterNames.ShuffleName => Unaimed(name, cells, new AttemptAction.BoosterUsed(new BoosterUse.Shuffle())),
            ExtraMovesName => Unaimed(name, cells, new AttemptAction.ExtraMovesTaken()),
            ShuffleRefusedName => Unaimed(name, cells, new AttemptAction.ShuffleRefused()),
            ExtraMovesRefusedName => Unaimed(name, cells, new AttemptAction.ExtraMovesRefused()),
            _ => null,
        };

        private static int[] CellNumbers(Position at) => new[] { at.X, at.Y };

        /// <exception cref="ArgumentException">The numbers are neither a tap nor a swap of neighbouring cells.</exception>
        private static Move MoveAt(int[]? cells) => cells switch
        {
            { Length: 2 } => Move.Tap(new Position(cells[0], cells[1])),
            { Length: 4 } => Move.Swap(new Position(cells[0], cells[1]), new Position(cells[2], cells[3])),
            _ => throw new ArgumentException("A move is at [x, y] for a tap or at [fromX, fromY, toX, toY] for a swap."),
        };

        /// <exception cref="ArgumentException">The numbers are not one cell.</exception>
        private static Position CellAt(string name, int[]? cells) => cells is { Length: 2 }
            ? new Position(cells[0], cells[1])
            : throw new ArgumentException($"A {name} is aimed at one cell, [x, y].");

        /// <exception cref="ArgumentException">The action is aimed at cells, and takes no aim.</exception>
        private static AttemptAction Unaimed(string name, int[]? cells, AttemptAction action) => cells is null
            ? action
            : throw new ArgumentException($"A {name} takes no aim.");
    }
}
