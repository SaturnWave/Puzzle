using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Puzzle.Core.Engine;

namespace Puzzle.Core.Levels
{
    /// <summary>Whether every attempt of a level starts from the same board. Closed set (private constructor).</summary>
    public abstract record StartBoard
    {
        private StartBoard()
        {
        }

        /// <summary>A new starting board each attempt (drawn from the attempt's seed).</summary>
        public sealed record Varied : StartBoard;

        /// <summary>The same starting board each attempt, drawn from <see cref="Seed"/>.</summary>
        public sealed record Fixed(uint Seed) : StartBoard;
    }

    /// <summary>
    /// How retries of a level differ: new tile patterns every attempt, but the same rules and — through the
    /// calibrated <see cref="SeedPool"/> — the same difficulty (docs/GAME_DESIGN.md §17).
    /// </summary>
    public sealed class AttemptPolicy
    {
        public const int MaxPoolSize = 256;

        private readonly StartBoard _board = new StartBoard.Varied();
        private readonly ReadOnlyCollection<uint> _seedPool = Array.AsReadOnly(Array.Empty<uint>());

        public static AttemptPolicy Default { get; } = new AttemptPolicy();

        public StartBoard Board
        {
            get => _board;
            init => _board = value ?? throw new ArgumentNullException(nameof(Board));
        }

        /// <summary>
        /// Play seeds whose measured difficulty is close to the level's typical difficulty. Attempt k uses
        /// entry k modulo the pool size. Empty means "any seed" (difficulty then varies between attempts).
        /// </summary>
        public IReadOnlyList<uint> SeedPool
        {
            get => _seedPool;
            init
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(SeedPool));
                }

                if (value.Count > MaxPoolSize || value.Distinct().Count() != value.Count)
                {
                    throw new ArgumentException($"A seed pool holds at most {MaxPoolSize} distinct seeds.", nameof(SeedPool));
                }

                _seedPool = Array.AsReadOnly(value.ToArray());
            }
        }

        /// <summary>
        /// The seeds of attempt <paramref name="attempt"/> (0-based). <paramref name="unpooledSeed"/> is used
        /// when the level has no calibrated pool.
        /// </summary>
        public AttemptSeeds SeedsFor(int attempt, uint unpooledSeed)
        {
            if (attempt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempts are counted from 0.");
            }

            uint play = _seedPool.Count > 0 ? _seedPool[attempt % _seedPool.Count] : unpooledSeed;
            return SeedsForPlaySeed(play);
        }

        /// <summary>The seeds for an explicit play seed, honoring the level's starting-board rule.</summary>
        public AttemptSeeds SeedsForPlaySeed(uint playSeed) => _board switch
        {
            StartBoard.Fixed board => AttemptSeeds.WithFixedBoard(board.Seed, playSeed),
            StartBoard.Varied _ => AttemptSeeds.From(playSeed),
            _ => throw new InvalidOperationException($"Unhandled start board {_board.GetType().Name}."),
        };
    }
}
