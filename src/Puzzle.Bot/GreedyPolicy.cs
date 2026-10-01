using System;
using System.Collections.Generic;
using Puzzle.Core.Engine;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot
{
    /// <summary>
    /// One-step lookahead: tries every noticed move on a hidden-refill copy, scores the outcome, then picks by
    /// softmax over the scores. With <see cref="SkillProfile.Expert"/> it is a plain greedy maximizer.
    /// </summary>
    public sealed class GreedyPolicy : IBotPolicy
    {
        private readonly SkillProfile _skill;
        private readonly MoveEvaluator _evaluator;
        private readonly List<Move> _noticed = new List<Move>();
        private readonly List<double> _scores = new List<double>();

        public GreedyPolicy(SkillProfile skill, MoveEvaluator? evaluator = null)
        {
            _skill = skill;
            _evaluator = evaluator ?? MoveEvaluator.Default;
        }

        public string Name => "greedy-" + _skill.Name;

        public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng)
        {
            Notice(legalMoves, rng);
            MoveEvaluator.Snapshot before = _evaluator.Capture(game);
            _scores.Clear();
            foreach (Move move in _noticed)
            {
                Game lookahead = game.CreateLookahead(rng.NextULong());
                if (!lookahead.TryPlay(move))
                {
                    throw new InvalidOperationException($"Legal move {move} was rejected by its lookahead.");
                }

                _scores.Add(_evaluator.Score(game, before, lookahead));
            }

            return _noticed[Pick(rng)];
        }

        /// <summary>Keeps each move with the profile's notice chance; never leaves the player with nothing.</summary>
        private void Notice(IReadOnlyList<Move> legalMoves, Rng rng)
        {
            _noticed.Clear();
            foreach (Move move in legalMoves)
            {
                if (rng.RollPercent(_skill.NoticePercent))
                {
                    _noticed.Add(move);
                }
            }

            if (_noticed.Count == 0)
            {
                _noticed.Add(legalMoves[rng.NextInt(legalMoves.Count)]);
            }
        }

        private int Pick(Rng rng)
        {
            double best = double.MinValue;
            foreach (double score in _scores)
            {
                best = Math.Max(best, score);
            }

            if (best >= MoveEvaluator.WinScore || _skill.Temperature <= 0)
            {
                return PickAmongBest(best, rng);
            }

            double total = 0;
            for (int i = 0; i < _scores.Count; i++)
            {
                _scores[i] = Math.Exp((_scores[i] - best) / _skill.Temperature);
                total += _scores[i];
            }

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < _scores.Count; i++)
            {
                roll -= _scores[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            return _scores.Count - 1;
        }

        private int PickAmongBest(double best, Rng rng)
        {
            int ties = 0;
            int chosen = 0;
            for (int i = 0; i < _scores.Count; i++)
            {
                if (_scores[i] == best && rng.NextInt(++ties) == 0)
                {
                    chosen = i;
                }
            }

            return chosen;
        }
    }
}
