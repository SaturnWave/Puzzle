namespace Puzzle.Core.Engine
{
    public enum GameStatus
    {
        Playing,
        Won,
        LostOutOfMoves,

        /// <summary>No legal move existed even after shuffling; a validated level never reaches this.</summary>
        LostNoMovesPossible,
    }

    public static class GameStatuses
    {
        public static bool IsOver(this GameStatus status) => status != GameStatus.Playing;

        public static bool IsLoss(this GameStatus status) =>
            status == GameStatus.LostOutOfMoves || status == GameStatus.LostNoMovesPossible;
    }
}
