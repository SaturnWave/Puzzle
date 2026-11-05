#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>Where one goal of the card before a level stands: its icon and its number, each at a place of the card and a size.</summary>
    public readonly struct StartGoalPlace
    {
        public StartGoalPlace(float iconRight, float iconDown, float iconSize, float numberRight, float numberDown, float numberSize)
        {
            IconRight = iconRight;
            IconDown = iconDown;
            IconSize = iconSize;
            NumberRight = numberRight;
            NumberDown = numberDown;
            NumberSize = numberSize;
        }

        /// <summary>The middle of the icon: this far right of the card's middle and this far under its top edge.</summary>
        public float IconRight { get; }

        public float IconDown { get; }

        public float IconSize { get; }

        /// <summary>The middle of the number's line, and its type size.</summary>
        public float NumberRight { get; }

        public float NumberDown { get; }

        public float NumberSize { get; }

        /// <summary>The goal's left and right edges, for a number this wide at its own size.</summary>
        public float LeftEdge(float numberWidth) => Math.Min(IconRight - IconSize / 2f, NumberRight - numberWidth / 2f);

        public float RightEdge(float numberWidth) => Math.Max(IconRight + IconSize / 2f, NumberRight + numberWidth / 2f);

        /// <summary>How far under the card's top edge the goal reaches.</summary>
        public float Bottom => Math.Max(IconDown + IconSize / 2f, NumberDown + NumberSize / 2f);
    }

    /// <summary>
    /// The card before a level, as the design canvas draws it (LevelStartTR), in the canvas' own points: right of
    /// the middle of the card's body and down from its top edge. A body of 362 by 392 with the brass nameplate on
    /// its top edge; at its upper left the goals under their header, at its upper right the moves' medallion; under
    /// them the header of the boosters, the wooden shelf with the three discs and their names; and over its lower
    /// edge the green button into the level, which reaches under the card. A screen too small for the card draws
    /// all of it smaller (<see cref="PageLayout.PlaceOfACard"/>). No Unity dependency.
    /// </summary>
    public static class LevelStartLayout
    {
        public const float BodyWidth = 362f;
        public const float BodyHeight = 392f;

        /// <summary>The nameplate: 212 wide, the title in the serif at 22; 50 high with the reward's line under the title, 40 without (CardNameplate).</summary>
        public const float NameplateWidth = 212f;
        public const float TitleSize = 22f;
        private const float PlateWithTwoLines = 50f;
        private const float PlateWithOneLine = 40f;

        /// <summary>The pins in the wall stand this far over the card's top edge (HangingCard).</summary>
        private const float WallPinOverTheCard = 20f;

        /// <summary>The goals' column: 216 wide, its middle 68 left of the card's; its header's middle 60 under the top edge, the row of goals' 102.</summary>
        public const float GoalsRight = -68f;
        public const float GoalsRoom = 216f;
        public const float GoalsHeaderDown = 60f;
        public const float GoalsRowDown = 102f;

        /// <summary>Two goals or one stand beside their numbers: icons of 54, numbers of 34, 5 between an icon and its number, 14 between two goals.</summary>
        public const float GoalIconSize = 54f;
        public const float GoalNumberSize = 34f;
        private const float IconToNumber = 5f;
        private const float BetweenGoals = 14f;

        /// <summary>
        /// Three goals do not fit beside their numbers: each stands over its number, as on the board's plaque, from
        /// the top of the row of two (the header's foot and 10 more), 16 apart, the number tucked 4 under its icon,
        /// whose lowest part is only shadow.
        /// </summary>
        private const float StackedIconSize = 46f;
        private const float StackedNumberSize = 28f;
        private const float BetweenStackedGoals = 16f;
        private const float NumberTuckedUnderIcon = 4f;
        private const float GoalsTop = GoalsRowDown - GoalIconSize / 2f;

        /// <summary>The moves' medallion, 98 across, as the board's plaque row has it (BoardTR), with its middle here.</summary>
        public const float MedallionRight = 115f;
        public const float MedallionDown = 102f;
        public const float MedallionSize = 98f;

        /// <summary>The boosters' header, the shelf (a plank of 328 by 88) and its three discs of 66 in three columns 104 apart.</summary>
        public const float BoostersHeaderDown = 178f;
        public const float ShelfDown = 239f;
        public const float ShelfWidth = 328f;
        public const float ShelfHeight = 88f;
        public const float DiscDown = 237.5f;
        public const float DiscSize = 66f;
        public const float DiscsApart = 104f;

        /// <summary>The booster's name under its disc: Gabarito of 12.5 on lines 14.25 apart, the first line's middle here.</summary>
        public const float NameDown = 301.1f;
        public const float NameSize = 12.5f;
        public const float NameLinesApart = 14.25f;

        /// <summary>
        /// The green button into the level: 244 wide with a label of 34, its middle 5 points over the card's lower
        /// edge. Its pill is 72 high with a lip of 7 under it (PushButton.Height and Lip), so it reaches under the card.
        /// </summary>
        public const float PlayDown = 387f;
        public const float PlayWidth = 244f;
        public const float PlayLabelSize = 34f;
        private const float PlayHeight = 72f;
        private const float PlayLip = 7f;

        /// <summary>How far the green button reaches under the card's lower edge, its lip included.</summary>
        public static float RidesBelow => PlayDown + PlayHeight / 2f + PlayLip - BodyHeight;

        /// <summary>How far the nameplate, or the pins in the wall, stand over the card's top edge.</summary>
        public static float RidesAbove(bool hasRewardLine) =>
            Math.Max((hasRewardLine ? PlateWithTwoLines : PlateWithOneLine) / 2f, WallPinOverTheCard);

        /// <summary>The room the card takes on a screen, from the top of what rides over it to the lip of its button.</summary>
        public static float Height(bool hasRewardLine) => BodyHeight + RidesAbove(hasRewardLine) + RidesBelow;

        /// <summary>Where the card hangs on a page of the museum, and how large it is drawn there.</summary>
        public static CardPlace PlaceOn(PageLayout page, bool hasRewardLine) => page.PlaceOfACard(BodyWidth, Height(hasRewardLine));

        /// <summary>The middle of the disc in a column of the shelf, counted from 0 at the left, right of the card's middle.</summary>
        public static float DiscRight(int column) => (column - 1) * DiscsApart;

        /// <summary>
        /// Where the level's goals stand. One or two stand each beside its number, as drawn; three stand each over
        /// its number. A row too wide for the goals' column (long numbers) is drawn smaller, all of it alike.
        /// </summary>
        /// <param name="numberWidths">How wide each goal's number is, set at <see cref="GoalNumberSize"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">A level has one to three goals.</exception>
        public static IReadOnlyList<StartGoalPlace> Goals(IReadOnlyList<float> numberWidths)
        {
            return numberWidths.Count switch
            {
                1 or 2 => Beside(numberWidths),
                3 => Stacked(numberWidths),
                _ => throw new ArgumentOutOfRangeException(nameof(numberWidths), numberWidths.Count, "A level has one to three goals."),
            };
        }

        private static StartGoalPlace[] Beside(IReadOnlyList<float> numberWidths)
        {
            float rowWidth = BetweenGoals * (numberWidths.Count - 1);
            foreach (float width in numberWidths)
            {
                rowWidth += GoalIconSize + IconToNumber + width;
            }

            float scale = Math.Min(1f, GoalsRoom / rowWidth);
            float left = GoalsRight - scale * rowWidth / 2f;
            var places = new StartGoalPlace[numberWidths.Count];
            for (int goal = 0; goal < places.Length; goal++)
            {
                float width = numberWidths[goal];
                places[goal] = new StartGoalPlace(
                    left + scale * GoalIconSize / 2f, GoalsRowDown, scale * GoalIconSize,
                    left + scale * (GoalIconSize + IconToNumber + width / 2f), GoalsRowDown, scale * GoalNumberSize);
                left += scale * (GoalIconSize + IconToNumber + width + BetweenGoals);
            }

            return places;
        }

        private static StartGoalPlace[] Stacked(IReadOnlyList<float> numberWidths)
        {
            float numberShare = StackedNumberSize / GoalNumberSize;
            var columns = new float[numberWidths.Count];
            float rowWidth = BetweenStackedGoals * (columns.Length - 1);
            for (int goal = 0; goal < columns.Length; goal++)
            {
                columns[goal] = Math.Max(StackedIconSize, numberShare * numberWidths[goal]);
                rowWidth += columns[goal];
            }

            float scale = Math.Min(1f, GoalsRoom / rowWidth);
            float iconSize = scale * StackedIconSize;
            float iconDown = GoalsTop + iconSize / 2f;
            float numberSize = scale * StackedNumberSize;
            float numberDown = GoalsTop + iconSize - scale * NumberTuckedUnderIcon + numberSize / 2f;
            float left = GoalsRight - scale * rowWidth / 2f;
            var places = new StartGoalPlace[columns.Length];
            for (int goal = 0; goal < places.Length; goal++)
            {
                float middle = left + scale * columns[goal] / 2f;
                places[goal] = new StartGoalPlace(middle, iconDown, iconSize, middle, numberDown, numberSize);
                left += scale * (columns[goal] + BetweenStackedGoals);
            }

            return places;
        }
    }
}
