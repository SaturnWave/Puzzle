using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Bot
{
    /// <summary>
    /// Scores the outcome of one move, as seen through a lookahead (refills hidden). Goal progress dominates;
    /// specials are valued as an inventory so that spending one has to pay for itself.
    /// </summary>
    public sealed class MoveEvaluator
    {
        public const double WinScore = 10_000;

        public static MoveEvaluator Default { get; } = new MoveEvaluator();

        /// <summary>Score for completing one whole goal's worth of progress.</summary>
        public double GoalProgressWeight { get; init; } = 100;

        public double GoalCompletedBonus { get; init; } = 40;

        public double RocketValue { get; init; } = 8;

        public double BombValue { get; init; } = 8;

        public double BigBombValue { get; init; } = 14;

        public double SeekerValue { get; init; } = 9;

        public double ColorBombValue { get; init; } = 18;

        /// <summary>
        /// Fraction of a special's value lost when it is used. Measured over all 1000 generated levels (2026-10-02):
        /// 0.25 gives the best win rate for both the expert and the average profile; 0.5 made the bots hoard specials
        /// on levels with large collect targets, and 0 made them fire specials too early on item-drop levels.
        /// </summary>
        public double SpendFactor { get; init; } = 0.25;

        /// <summary>Per box hit point, jelly layer or chain hit point removed while that goal is open.</summary>
        public double PartialDamageWeight { get; init; } = 3;

        /// <summary>Per row an item descends while the drop goal is open.</summary>
        public double ItemDescentWeight { get; init; } = 4;

        public double GemClearedWeight { get; init; } = 0.3;

        /// <summary>Features of the position before the move, computed once and shared by every candidate.</summary>
        public Snapshot Capture(Game game) => new Snapshot(SpecialInventory(game.Board), ItemDepth(game));

        /// <summary>
        /// How promising a position is on its own: goal progress, finished goals, specials in stock and item depth.
        /// Used by search-based players that compare positions rather than single moves.
        /// </summary>
        public double ValueOf(Game game)
        {
            double value = SpecialInventory(game.Board);
            for (int i = 0; i < game.Goals.Count; i++)
            {
                GoalProgress progress = game.Goals[i];
                value += GoalProgressWeight * progress.Fraction + (progress.IsComplete ? GoalCompletedBonus : 0);
            }

            return Wants<GoalSpec.DropItems>(game.Goals) ? value + ItemDescentWeight * ItemDepth(game) : value;
        }

        public double Score(Game before, Snapshot beforeFeatures, Game after)
        {
            if (after.Status == GameStatus.Won)
            {
                return WinScore;
            }

            PlayStats stats = after.LastMove;
            double score = GoalScore(before.Goals, after.Goals);

            double created = stats.RocketsCreated * RocketValue + stats.BombsCreated * BombValue + stats.BigBombsCreated * BigBombValue
                + stats.SeekersCreated * SeekerValue + stats.ColorBombsCreated * ColorBombValue;
            double consumed = created - (SpecialInventory(after.Board) - beforeFeatures.SpecialInventory);
            score += created - SpendFactor * consumed;

            int partialDamage = (Wants<GoalSpec.BreakBoxes>(before.Goals) ? stats.BoxHpRemoved : 0)
                + (Wants<GoalSpec.ClearJelly>(before.Goals) ? stats.JellyLayersRemoved : 0)
                + (Wants<GoalSpec.BreakChains>(before.Goals) ? stats.ChainHpRemoved : 0);
            score += PartialDamageWeight * partialDamage;

            if (Wants<GoalSpec.DropItems>(before.Goals))
            {
                score += ItemDescentWeight * (ItemDepth(after) - beforeFeatures.ItemDepth);
            }

            return score + GemClearedWeight * stats.GemsCleared;
        }

        private double GoalScore(GoalTracker before, GoalTracker after)
        {
            double score = 0;
            for (int i = 0; i < before.Count; i++)
            {
                GoalProgress was = before[i];
                GoalProgress now = after[i];
                score += GoalProgressWeight * (now.Fraction - was.Fraction);
                if (now.IsComplete && !was.IsComplete)
                {
                    score += GoalCompletedBonus;
                }
            }

            return score;
        }

        private static bool Wants<TGoal>(GoalTracker goals)
            where TGoal : GoalSpec
        {
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals[i].Goal is TGoal && !goals[i].IsComplete)
                {
                    return true;
                }
            }

            return false;
        }

        private double SpecialInventory(Board board)
        {
            double value = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    value += board.TileAt(new Position(x, y)).Kind switch
                    {
                        TileKind.RocketH => RocketValue,
                        TileKind.RocketV => RocketValue,
                        TileKind.Bomb => BombValue,
                        TileKind.BigBomb => BigBombValue,
                        TileKind.Seeker => SeekerValue,
                        TileKind.ColorBomb => ColorBombValue,
                        _ => 0,
                    };
                }
            }

            return value;
        }

        /// <summary>Sum of item rows, a collected item counting as one row below the board, so every descent scores.</summary>
        private static int ItemDepth(Game game)
        {
            Board board = game.Board;
            int depth = ItemsCollected(game.Goals) * board.Height;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.TileAt(new Position(x, y)).Kind == TileKind.Item)
                    {
                        depth += y;
                    }
                }
            }

            return depth;
        }

        private static int ItemsCollected(GoalTracker goals)
        {
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals[i].Goal is GoalSpec.DropItems)
                {
                    return goals[i].Done;
                }
            }

            return 0;
        }

        public readonly struct Snapshot
        {
            internal Snapshot(double specialInventory, int itemDepth)
            {
                SpecialInventory = specialInventory;
                ItemDepth = itemDepth;
            }

            public double SpecialInventory { get; }

            public int ItemDepth { get; }
        }
    }
}
