using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Client.Engine
{
    /// <summary>How the game starts a level's attempts. No Unity dependency.</summary>
    public static class LevelStart
    {
        /// <summary>The rules every attempt a player gets is played by: the engine's own, under which the levels were measured.</summary>
        public static RuleSet Rules => RuleSet.Default;

        /// <summary>
        /// The level as a player first meets it: the first seed of its calibrated pool, so the first board is one whose
        /// difficulty was measured. A level without a pool is seeded with its own number, which keeps the board the
        /// same from run to run. The game records its events, because the view plays them, and what the player does,
        /// because the attempt's record keeps it (Puzzle.Core's AttemptReplay plays the attempt again from it).
        /// </summary>
        public static Game FirstAttempt(LevelDefinition level) => Attempt(level, attempt: 0);

        /// <summary>
        /// A later try at the level: the next seed of its pool (the level's attempt policy says how the pool is gone
        /// through), so a player who failed meets another measured board and not the same one again.
        /// </summary>
        /// <param name="attempt">How many times the level was tried before: 0 for the first try.</param>
        public static Game Attempt(LevelDefinition level, int attempt) => Attempt(level, attempt, Array.Empty<StartBooster>());

        /// <summary>
        /// A try at the level with start boosters taken into it: their specials lie on the board as it opens, and
        /// their extra moves are on the plaque. The board and everything that falls later are the same as without
        /// them. A board with too few free gems for the specials opens without the boosters that put one there, and
        /// keeps the ones that need no room: the game's <see cref="Game.BoostersUsed"/> says which it took, and only
        /// those are the player's to spend.
        /// </summary>
        /// <param name="attempt">How many times the level was tried before: 0 for the first try.</param>
        /// <param name="startBoosters">The start boosters taken into the attempt, each at most once.</param>
        /// <exception cref="ArgumentException">A start booster is named twice.</exception>
        public static Game Attempt(LevelDefinition level, int attempt, IReadOnlyList<StartBooster> startBoosters)
        {
            AttemptSeeds seeds = SeedsOf(level, attempt);
            try
            {
                return Game.Start(level, seeds, Played(startBoosters));
            }
            catch (LevelInitializationException) when (startBoosters.Any(PutsASpecialOnTheBoard))
            {
                // The specials found no free gem to lie on: the level opens as it would without them.
                return Game.Start(level, seeds, Played(startBoosters.Where(booster => !PutsASpecialOnTheBoard(booster)).ToArray()));
            }
        }

        /// <summary>
        /// The same attempt with another count of moves than the level gives: the same board and the same pieces
        /// falling in, so the same moves play the same game, which only ends at another move. The Editor's studies
        /// use it to bring a game to a win with a chosen count of moves left over; a player never gets one.
        /// </summary>
        /// <param name="moves">The moves the attempt has; one at least.</param>
        public static Game AttemptWithMoves(LevelDefinition level, int attempt, int moves) =>
            Game.Start(level, SeedsOf(level, attempt), new GameOptions { RecordEvents = true, MoveLimitOverride = moves });

        /// <summary>The seeds of a try at the level, which <see cref="Attempt"/> starts its game from and the attempt's record keeps.</summary>
        /// <param name="attempt">How many times the level was tried before: 0 for the first try.</param>
        public static AttemptSeeds SeedsOf(LevelDefinition level, int attempt) =>
            level.Attempts.SeedsFor(attempt, unpooledSeed: (uint)(level.Id.Value + attempt));

        /// <summary>A player's game: it records the events the view plays and the actions the record keeps.</summary>
        private static GameOptions Played(IReadOnlyList<StartBooster> startBoosters) =>
            new GameOptions { Rules = Rules, RecordEvents = true, RecordActions = true, StartBoosters = startBoosters };

        /// <summary>True for a start booster that puts a special on the board, which needs a free gem to lie on; the extra moves need none.</summary>
        private static bool PutsASpecialOnTheBoard(StartBooster booster) => booster switch
        {
            StartBooster.RocketAndBomb or StartBooster.ColorBomb => true,
            StartBooster.ExtraMoves => false,
            _ => throw new ArgumentOutOfRangeException(nameof(booster), booster, "Unknown start booster."),
        };
    }
}
