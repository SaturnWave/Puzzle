using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Bot
{
    /// <summary>
    /// "Usta" (master): a fair search bot that plans two moves ahead over sampled futures. Every legal move first gets
    /// the expert's one-ply score (the prior); the best few are then compared by sequential halving, where one
    /// simulation plays the move on an imagined future (<see cref="Game.CreateImagined"/>: real spawn rules, refills
    /// drawn from the bot's own seed) and adds the best follow-up the player would see on the board it leaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fair: it reads only the visible game and the level's public rules. It never calls
    /// <see cref="Game.CreateReplica"/> and never reads the moves left, so the same board gives the same decision
    /// whatever the live game's refills will be, and whatever the move limit.
    /// </para>
    /// <para>
    /// Deterministic: every random choice comes from the <c>rng</c> argument, the budget is a count of simulations
    /// (never time), every ordering is total, and only scratch buffers outlive a decision. Not thread-safe: give each
    /// thread its own instance.
    /// </para>
    /// </remarks>
    public sealed class UstaPolicy : IBotPolicy
    {
        /// <summary>Salt of the follow-up lookahead seeds, which derive from a simulation's seed and draw nothing.</summary>
        private const ulong FollowUpSeedSalt = 16;

        /// <summary>With fewer candidates than this, every place goes to the prior's best moves.</summary>
        private const int MinArmsForSpecialSlots = 4;

        private readonly UstaSettings _settings;
        private readonly MoveEvaluator _evaluator;
        private readonly List<RootMove> _ranked = new List<RootMove>();
        private readonly List<Arm> _arms = new List<Arm>();
        private readonly List<Move> _followUps = new List<Move>();

        public UstaPolicy(UstaSettings? settings = null, MoveEvaluator? evaluator = null)
        {
            _settings = settings ?? UstaSettings.Default;
            _evaluator = evaluator ?? MoveEvaluator.Default;
            Name = NameFor(_settings);
        }

        /// <summary>
        /// "usta" at the default settings, "usta-&lt;simulations&gt;" when only the budget differs, "usta-custom"
        /// otherwise. Like the greedy bots' names, it does not describe the evaluator.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Diagnostic: how many copy-and-play operations (one lookahead or imagined copy, one move played on it) the
        /// most recent <see cref="ChooseMove"/> made. A decision's cost is about proportional to it.
        /// </summary>
        public int LastDecisionPlays { get; private set; }

        /// <summary>Diagnostic for tests: how many sampled futures the most recent <see cref="ChooseMove"/> simulated.</summary>
        internal int LastDecisionSimulations { get; private set; }

        public Move ChooseMove(Game game, IReadOnlyList<Move> legalMoves, Rng rng)
        {
            LastDecisionPlays = 0;
            LastDecisionSimulations = 0;
            if (legalMoves.Count == 1)
            {
                return legalMoves[0];
            }

            MoveEvaluator.Snapshot present = _evaluator.Capture(game);
            RankByPrior(game, present, legalMoves, rng);
            if (_ranked[0].Prior >= MoveEvaluator.WinScore)
            {
                return _ranked[0].Move; // a sure win; among several, the smallest tie key is a uniform pick
            }

            HalvingPlan plan = HalvingPlan.For(Math.Min(legalMoves.Count, _settings.RootWidth), _settings.Simulations);
            Shortlist(game.Board, plan.Arms);
            return SequentialHalving(game, present, plan, rng);
        }

        private static string NameFor(UstaSettings settings)
        {
            if (settings == UstaSettings.Default)
            {
                return BotPolicies.Usta;
            }

            bool onlyBudgetDiffers = settings with { Simulations = UstaSettings.Default.Simulations } == UstaSettings.Default;
            return onlyBudgetDiffers
                ? BotPolicies.Usta + "-" + settings.Simulations.ToString(CultureInfo.InvariantCulture)
                : BotPolicies.Usta + "-custom";
        }

        /// <summary>
        /// The prior: every legal move scored as the expert scores it, on a lookahead whose refills are hidden. Ranked
        /// best first; equal scores fall back to the lookahead seed (a random order that costs no extra draw), then to
        /// the position in <paramref name="legalMoves"/>.
        /// </summary>
        private void RankByPrior(Game game, MoveEvaluator.Snapshot present, IReadOnlyList<Move> legalMoves, Rng rng)
        {
            _ranked.Clear();
            for (int i = 0; i < legalMoves.Count; i++)
            {
                ulong seed = rng.NextULong();
                Game lookahead = game.CreateLookahead(seed);
                Play(lookahead, legalMoves[i]);
                _ranked.Add(new RootMove(legalMoves[i], _evaluator.Score(game, present, lookahead), seed, i));
            }

            _ranked.Sort(RootMove.BestFirst);
        }

        /// <summary>
        /// Picks the <paramref name="arms"/> moves to search: the prior's best, except that up to
        /// <see cref="UstaSettings.SpecialSlots"/> of the places go to the best special-firing moves outside that top.
        /// Reserved places no special-firing move claims go back to the next best by prior.
        /// </summary>
        private void Shortlist(Board board, int arms)
        {
            int reserved = arms >= MinArmsForSpecialSlots ? Math.Min(_settings.SpecialSlots, arms) : 0;
            int top = arms - reserved;
            _arms.Clear();
            for (int rank = 0; rank < top; rank++)
            {
                _arms.Add(new Arm(_ranked[rank].Move, rank));
            }

            AddBelowTop(board, top, arms, specialFiring: true);
            AddBelowTop(board, top, arms, specialFiring: false);
        }

        /// <summary>Adds moves ranked below the top, best first, whose special-firing matches, until there are <paramref name="arms"/>.</summary>
        private void AddBelowTop(Board board, int top, int arms, bool specialFiring)
        {
            for (int rank = top; rank < _ranked.Count && _arms.Count < arms; rank++)
            {
                if (FiresSpecial(board, _ranked[rank].Move) == specialFiring)
                {
                    _arms.Add(new Arm(_ranked[rank].Move, rank));
                }
            }
        }

        /// <summary>A tap, or a swap that moves a special (a tap's cells are the special itself).</summary>
        private static bool FiresSpecial(Board board, Move move) =>
            board.TileAt(move.From).IsSpecial || board.TileAt(move.To).IsSpecial;

        /// <summary>
        /// Spends the budget exactly as planned: after each round, as many arms go on as the plan gives the next round.
        /// In each round every arm still in plays the same freshly drawn futures (common random numbers), so all
        /// survivors have played the same futures and their totals compare like means.
        /// </summary>
        private Move SequentialHalving(Game game, MoveEvaluator.Snapshot present, HalvingPlan plan, Rng rng)
        {
            for (int round = 0; round < plan.Rounds.Count; round++)
            {
                for (int sample = 0; sample < plan.Rounds[round].SamplesPerArm; sample++)
                {
                    SimulateEveryArm(game, present, rng.NextULong());
                }

                KeepBest(plan.SurvivorsAfter(round));
            }

            return _arms[0].Move;
        }

        private void SimulateEveryArm(Game game, MoveEvaluator.Snapshot present, ulong future)
        {
            foreach (Arm arm in _arms)
            {
                arm.Add(Simulate(game, present, arm.Move, future));
            }
        }

        /// <summary>Keeps the best <paramref name="survivors"/> arms: higher total first, the better prior rank on a tie.</summary>
        private void KeepBest(int survivors)
        {
            _arms.Sort(Arm.HigherTotalFirst);
            _arms.RemoveRange(survivors, _arms.Count - survivors);
        }

        /// <summary>
        /// One sampled future of <paramref name="move"/>: its score on an imagined copy whose refills and effect choices
        /// come from <paramref name="future"/>, plus the value of the best follow-up on the board it leaves.
        /// </summary>
        private double Simulate(Game game, MoveEvaluator.Snapshot present, Move move, ulong future)
        {
            LastDecisionSimulations++;
            Game imagined = game.CreateImagined(future);
            Play(imagined, move);
            double now = _evaluator.Score(game, present, imagined);
            return imagined.Status switch
            {
                GameStatus.Playing => now + FollowUpValue(imagined, future),
                GameStatus.Won => MoveEvaluator.WinScore,
                GameStatus.LostNoMovesPossible => now - MoveEvaluator.WinScore, // the board died: the game is lost
                _ => throw new InvalidOperationException($"An imagined game cannot end {imagined.Status}: it has no move limit."),
            };
        }

        /// <summary>
        /// What the player can make of the board the first move left: the best one-ply score over every legal reply,
        /// each on a lookahead with hidden refills (the second move's own refills stay unknown). A win is worth
        /// <see cref="UstaSettings.FollowUpWinValue"/>; any other score is weighted by <see cref="UstaSettings.FollowUpWeight"/>.
        /// </summary>
        /// <remarks>
        /// The reply lookaheads keep the level's move limit, so they may end <see cref="GameStatus.LostOutOfMoves"/>.
        /// That cannot leak the moves left into the decision: only <see cref="GameStatus.Won"/> is read from them
        /// (in <see cref="MoveEvaluator.Score"/>), and no copy is played twice.
        /// </remarks>
        private double FollowUpValue(Game imagined, ulong future)
        {
            MoveEvaluator.Snapshot afterFirstMove = _evaluator.Capture(imagined);
            _followUps.Clear();
            imagined.CollectLegalMoves(_followUps); // never empty: a playing game has a move (the engine shuffles dead boards)
            double best = double.MinValue;
            for (int i = 0; i < _followUps.Count; i++)
            {
                Game reply = imagined.CreateLookahead(Rng.Derive(future, FollowUpSeedSalt + (ulong)i));
                Play(reply, _followUps[i]);
                best = Math.Max(best, _evaluator.Score(imagined, afterFirstMove, reply));
            }

            return best >= MoveEvaluator.WinScore ? _settings.FollowUpWinValue : _settings.FollowUpWeight * best;
        }

        /// <summary>Plays a legal move on a copy and counts the play.</summary>
        /// <exception cref="InvalidOperationException">The copy rejected the move; copies keep the live board, so this is an engine bug.</exception>
        private void Play(Game copy, Move move)
        {
            if (!copy.TryPlay(move))
            {
                throw new InvalidOperationException($"Legal move {move} was rejected by its copy.");
            }

            LastDecisionPlays++;
        }

        /// <summary>A legal move with its prior score and the keys that break ties between equal scores.</summary>
        private readonly struct RootMove
        {
            public static readonly Comparison<RootMove> BestFirst = Compare;

            public RootMove(Move move, double prior, ulong tieKey, int index)
            {
                Move = move;
                Prior = prior;
                TieKey = tieKey;
                Index = index;
            }

            public Move Move { get; }

            public double Prior { get; }

            public ulong TieKey { get; }

            /// <summary>Position in the legal-move list: makes the order total even if two tie keys collide.</summary>
            public int Index { get; }

            private static int Compare(RootMove a, RootMove b)
            {
                int byPrior = b.Prior.CompareTo(a.Prior);
                if (byPrior != 0)
                {
                    return byPrior;
                }

                int byTieKey = a.TieKey.CompareTo(b.TieKey);
                return byTieKey != 0 ? byTieKey : a.Index.CompareTo(b.Index);
            }
        }

        /// <summary>A searched move and the sum of its simulation values so far.</summary>
        private sealed class Arm
        {
            public static readonly Comparison<Arm> HigherTotalFirst = Compare;

            public Arm(Move move, int rank)
            {
                Move = move;
                Rank = rank;
            }

            public Move Move { get; }

            /// <summary>Position in the prior ranking; breaks ties between equal totals.</summary>
            public int Rank { get; }

            public double Total { get; private set; }

            public void Add(double value) => Total += value;

            private static int Compare(Arm a, Arm b)
            {
                int byTotal = b.Total.CompareTo(a.Total);
                return byTotal != 0 ? byTotal : a.Rank.CompareTo(b.Rank);
            }
        }
    }
}
