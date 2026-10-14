#nullable enable
using System.Globalization;

namespace Puzzle.Client.Flow
{
    /// <summary>What the plaques over the board say, in the player's language, in the capitals the design sets them in (BoardTR). No Unity dependency.</summary>
    public static class BoardWords
    {
        /// <summary>The label over the level's number, where the level gives no jigsaw piece.</summary>
        public const string Level = "BÖLÜM";

        /// <summary>The label on the moves' medallion.</summary>
        public const string Moves = "HAMLE";

        /// <summary>The label over the goals, where the level gives no jigsaw piece.</summary>
        public const string Goals = "HEDEF";

        /// <summary>The label over the goals where the level's number has no plaque of its own: "BÖLÜM 47".</summary>
        public static string LevelNumbered(int levelNumber) => $"{Level} {Number(levelNumber)}";

        /// <summary>The label over the painting whose piece the level gives: how many of its pieces the player has once it is won, "PARÇA 7 / 10".</summary>
        public static string PieceOf(PieceReward reward) => $"PARÇA {Number(reward.WonWithThem)} / {Number(reward.PieceCount)}";

        public static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What the two plaques beside the moves' medallion show, which the jigsaw piece the level gives decides. A
    /// closed set (private constructor). A level that gives a piece shows it as the design canvas draws the board
    /// (BoardTR): the level over its goals at the left, the painting with the piece lit at the right. A level that
    /// gives none (one won before, or beyond the museum's paintings) keeps the web test page's arrangement: the
    /// level's number at the left, the goals at the right. No Unity dependency.
    /// </summary>
    public abstract class BoardPlaques
    {
        private BoardPlaques(int levelNumber)
        {
            LevelNumber = levelNumber;
        }

        public int LevelNumber { get; }

        /// <summary>The label that stands over the goals, on whichever plaque they hang.</summary>
        public abstract string GoalsLabel { get; }

        /// <summary>The plaques of a level, by the piece winning it gives; nothing for none.</summary>
        public static BoardPlaques Of(int levelNumber, PieceReward? reward) => reward is PieceReward given
            ? new LevelOverGoalsBesideThePiece(levelNumber, given)
            : new LevelBesideGoals(levelNumber);

        /// <summary>The level gives no piece: "BÖLÜM" over its number at the left, "HEDEF" over the goals at the right.</summary>
        public sealed class LevelBesideGoals : BoardPlaques
        {
            public LevelBesideGoals(int levelNumber)
                : base(levelNumber)
            {
            }

            public override string GoalsLabel => BoardWords.Goals;

            public string LevelLabel => BoardWords.Level;

            public string LevelFigure => BoardWords.Number(LevelNumber);
        }

        /// <summary>The level gives a piece: "BÖLÜM 47" over the goals at the left, "PARÇA 7 / 10" over the painting at the right.</summary>
        public sealed class LevelOverGoalsBesideThePiece : BoardPlaques
        {
            public LevelOverGoalsBesideThePiece(int levelNumber, PieceReward reward)
                : base(levelNumber)
            {
                Reward = reward;
            }

            /// <summary>The pieces the level gives: the painting the right plaque shows, and the pieces lit on it.</summary>
            public PieceReward Reward { get; }

            public override string GoalsLabel => BoardWords.LevelNumbered(LevelNumber);

            public string PieceLabel => BoardWords.PieceOf(Reward);
        }
    }
}
