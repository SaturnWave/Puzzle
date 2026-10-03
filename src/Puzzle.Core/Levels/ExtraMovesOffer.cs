namespace Puzzle.Core.Levels
{
    /// <summary>
    /// The "+5 moves" offer (docs/GAME_DESIGN.md §19): when a level runs out of moves, the player may buy this many
    /// more with coins or an ad. Move limits are calibrated without it; reports show the win rate it would give.
    /// </summary>
    public static class ExtraMovesOffer
    {
        public const int Moves = 5;
    }
}
