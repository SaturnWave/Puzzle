using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Client.Engine
{
    /// <summary>How the game starts a level's attempts. No Unity dependency.</summary>
    public static class LevelStart
    {
        /// <summary>
        /// The level as a player first meets it: the first seed of its calibrated pool, so the first board is one whose
        /// difficulty was measured. A level without a pool is seeded with its own number, which keeps the board the
        /// same from run to run. The game records its events, because the view plays them.
        /// </summary>
        public static Game FirstAttempt(LevelDefinition level) => Attempt(level, attempt: 0);

        /// <summary>
        /// A later try at the level: the next seed of its pool (the level's attempt policy says how the pool is gone
        /// through), so a player who failed meets another measured board and not the same one again.
        /// </summary>
        /// <param name="attempt">How many times the level was tried before: 0 for the first try.</param>
        public static Game Attempt(LevelDefinition level, int attempt) => Game.Start(
            level,
            level.Attempts.SeedsFor(attempt, unpooledSeed: (uint)(level.Id.Value + attempt)),
            new GameOptions { RecordEvents = true });
    }
}
