using Puzzle.Core.Engine;
using Puzzle.Core.Model;

namespace Puzzle.Tooling.Recording;

/// <summary>
/// A move as this project's JSON files spell it: <c>[x, y]</c> for a tap, <c>[fromX, fromY, toX, toY]</c> for a swap.
/// The attempt records and the player store's saved attempts share the form.
/// </summary>
internal static class MoveNumbers
{
    public static int[] Of(Move move) => move.Kind == MoveKind.Tap
        ? [move.From.X, move.From.Y]
        : [move.From.X, move.From.Y, move.To.X, move.To.Y];

    /// <exception cref="ArgumentException">The numbers are neither a tap nor a swap of neighbouring cells.</exception>
    public static Move ToMove(int[]? numbers) => numbers switch
    {
        [int x, int y] => Move.Tap(new Position(x, y)),
        [int fromX, int fromY, int toX, int toY] => Move.Swap(new Position(fromX, fromY), new Position(toX, toY)),
        _ => throw new ArgumentException("A move is [x, y] for a tap or [fromX, fromY, toX, toY] for a swap."),
    };
}
