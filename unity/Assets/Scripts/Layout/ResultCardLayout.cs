#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Layout
{
    /// <summary>What a card in front of the board shows, as far as its rows go. A closed set.</summary>
    public enum ResultCardKind
    {
        /// <summary>A level won that gave a jigsaw piece (WinTR): the piece, the painting's pips, the coins, two buttons.</summary>
        WonWithAPiece,

        /// <summary>A level won that gave none: its star, the level and its moves, the coins, two buttons.</summary>
        WonWithoutAPiece,

        /// <summary>A level lost for good, or the question whether to leave one: what is left of the goals, two lines, what it costs in lives, two buttons.</summary>
        WhatIsLeft,

        /// <summary>A level whose moves ran out (LoseTR): the +5 in the alcove, the goals that are left, the two ways to pay, and giving up.</summary>
        OutOfMoves,
    }

    /// <summary>What one row of a result card holds. A closed set.</summary>
    public enum CardRowContent
    {
        /// <summary>The alcove in its beaded frame, with what the game left on its shelf.</summary>
        Alcove,

        /// <summary>The stronger line under the alcove: the piece won, the level with its moves or its attempt, or how close the game was.</summary>
        Headline,

        /// <summary>The painting's pieces as pips, the one just won ringed.</summary>
        Pips,

        /// <summary>The goals that are left, each its picture and what is left of it (LoseTR).</summary>
        GoalsLeft,

        /// <summary>The smaller line under the headline.</summary>
        Footnote,

        /// <summary>The gilt rule with its diamond.</summary>
        Rule,

        /// <summary>The coins the win paid on their pill, and the moves that turned into brushes.</summary>
        Coins,

        /// <summary>The heart on its pill with the life the attempt costs, and the words beside it.</summary>
        Lives,

        /// <summary>The two buttons, with the lip under them.</summary>
        Buttons,

        /// <summary>"Vazgeç", underlined, and the heart with what giving up costs (LoseTR).</summary>
        GiveUp,
    }

    /// <summary>One row of a result card: what it holds, and how far under the card's top edge it begins and ends.</summary>
    public readonly struct CardRow
    {
        public CardRow(CardRowContent content, float top, float bottom)
        {
            if (bottom < top)
            {
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, $"A row that begins {top} points down cannot end above that.");
            }

            Content = content;
            Top = top;
            Bottom = bottom;
        }

        public CardRowContent Content { get; }

        public float Top { get; }

        public float Bottom { get; }

        public float Middle => (Top + Bottom) / 2f;

        public static CardRow Around(CardRowContent content, float middle, float height) => new CardRow(content, middle - height / 2f, middle + height / 2f);

        public override string ToString() => $"{Content} {Top:0.#}..{Bottom:0.#}";
    }

    /// <summary>
    /// The rows of the card that hangs in front of the board once a game is decided, while its moves have run out,
    /// or while its player is asked whether to leave it, in the canvas' own points: down from the top edge of a body
    /// of 362 by 468, with the brass nameplate riding on that edge, and the alcove in the canvas' wide frame. A won
    /// level's card is the design canvas' (WinTR): the alcove, the line of the piece won, the painting's pips, the
    /// rule, the coins and the moves that became brushes, and the two buttons. The card of a game out of moves is the
    /// canvas' too (LoseTR): the +5 in the alcove, how close the game was over the goals that are left, the two ways to
    /// pay, the rule, and giving up. The canvas draws no card of a level lost for good or of the question to leave:
    /// theirs is WinTR's rhythm, with what is left of the goals in the alcove, the level and its attempt, a line about
    /// the alcove, the rule, the heart on its pill where the coins stand on a won card, and the two buttons. A screen
    /// too small for a card draws all of it smaller (<see cref="ScreenLayout.PlaceOfACard"/>). No Unity dependency.
    /// </summary>
    public static class ResultCardLayout
    {
        public const float BodyWidth = 362f;
        public const float BodyHeight = 468f;

        /// <summary>The nameplate (WinTR): 280 wide, 40 high, its middle on the card's top edge, the title in the serif at 20.5.</summary>
        public const float NameplateWidth = 280f;
        public const float TitleSize = 20.5f;
        private const float NameplateHeight = 40f;

        /// <summary>
        /// Every card's alcove (WinTR, LoseTR): a niche of 316 by 188, 38 under the card's top edge, in the wide
        /// frame, whose band of 7 reaches past the niche on every side.
        /// </summary>
        public const float WideFrameWidth = 330f;
        public const float WideFrameHeight = 202f;
        private const float AlcoveDown = 38f + 188f / 2f;

        /// <summary>The won card's rows under the alcove (WinTR): the line of the piece, the pips, the rule, the coins, the buttons with their lip.</summary>
        private const float WonHeadlineDown = 255f;
        private const float WonHeadlineHeight = 24f;
        private const float PipsDown = 283f;
        private const float PipsHeight = 16f;
        private const float WonRuleDown = 304f;
        private const float RuleHeight = 10f;
        private const float CoinsDown = 333f;
        private const float CoinsHeight = 46f;
        private const float WonButtonsDown = 407f;

        /// <summary>A won level that gave no piece has no pips: its line stands midway between the alcove and the rule.</summary>
        private const float HeadlineWithoutPipsDown = 266f;

        /// <summary>The card of what is left: the level and its attempt where a won card has its piece's line, the line about the alcove where it has the pips, and the lives where it has the coins.</summary>
        private const float FootnoteDown = 280f;
        private const float FootnoteHeight = 12.5f;

        /// <summary>
        /// The card of a game out of moves (LoseTR): under the alcove, 15 down, the line of how close it was (24 high)
        /// and 2 under it the goals that are left (40 high); 13 under those the buttons; the rule 15 under their lip,
        /// and giving up in the band of 44 that begins 6 under the rule's top.
        /// </summary>
        private const float OfferHeadlineDown = 253f;
        private const float GoalsLeftDown = 287f;
        private const float GoalsLeftHeight = 40f;
        private const float OfferButtonsDown = 356f;
        private const float OfferRuleDown = 414f;
        private const float GiveUpDown = 437f;
        private const float GiveUpHeight = 24f;

        /// <summary>A button is 72 high with a lip of 7 under it (PushButton.Height and Lip).</summary>
        public const float ButtonHeight = 72f;
        private const float ButtonLip = 7f;

        /// <summary>
        /// The row of two buttons (WinTR: 328 wide, the green one at its left, 12 apart): while pieces wait, the
        /// green one is 150 wide and the blue one beside it 166; otherwise the green one takes the row but for the
        /// teal one's 118, which is as narrow as "Müze" allows.
        /// </summary>
        public const float ButtonsWidth = 328f;
        public const float GapBetweenButtons = 12f;
        public const float GoOnBesidePlaceWidth = 150f;
        public const float PlaceWidth = 166f;
        public const float MuseumWidth = 118f;
        public const float GoOnBesideMuseumWidth = ButtonsWidth - GapBetweenButtons - MuseumWidth;

        /// <summary>The offer's buttons (LoseTR): the green one with the price 152 wide at the left, the blue one of the ad 164 at the right.</summary>
        public const float TakeForCoinsWidth = 152f;
        public const float TakeForAnAdWidth = ButtonsWidth - GapBetweenButtons - TakeForCoinsWidth;

        /// <summary>The rule (WinTR, LoseTR): a gilt line of 120 with a diamond in its middle.</summary>
        public const float RuleWidth = 120f;

        /// <summary>The goals that are left (LoseTR): each a picture of 40 and 5 after it its number in Paytone of 30, the goals 26 apart, centred.</summary>
        public const float GoalLeftSize = 40f;
        public const float GapGoalToNumber = 5f;
        public const float GapBetweenGoalsLeft = 26f;

        /// <summary>Giving up (LoseTR): "Vazgeç", 12 after it the heart of 20, and 3 after that what it costs, centred.</summary>
        public const float GapGiveUpToHeart = 12f;
        public const float GiveUpHeartSize = 20f;
        public const float GapHeartToCost = 3f;

        /// <summary>
        /// The coins' row (WinTR): the coin, 46 across and a point over the row's middle, at the left end of its
        /// pill of 86 by 36, which begins 22 in and holds the amount between 27 and 13 of room, half a point under
        /// the pill's middle (a margin of 1 in a centred row); 14 to the right of the pill the brush, 34 across,
        /// and 5 after it the words. The row is centred on the card.
        /// </summary>
        public const float CoinSize = 46f;
        public const float CoinOverRow = 1f;
        public const float PillInFromCoin = 22f;
        public const float NarrowestPill = 86f;
        public const float PillHeight = 36f;
        public const float AmountRoomLeft = 27f;
        public const float AmountRoomRight = 13f;
        public const float AmountUnderMiddle = 0.5f;
        public const float GapAfterPill = 14f;
        public const float BrushSize = 34f;
        public const float GapAfterBrush = 5f;

        /// <summary>How far the card's nameplate stands over its top edge, and so how much more room than its body the card takes.</summary>
        public static float RidesAbove => NameplateHeight / 2f;

        /// <summary>The room the card takes on a screen, from the top of its nameplate to its foot.</summary>
        public static float Height => BodyHeight + RidesAbove;

        /// <summary>The rows of a card, from its top edge down.</summary>
        public static IReadOnlyList<CardRow> RowsOf(ResultCardKind kind) => kind switch
        {
            ResultCardKind.WonWithAPiece => new[]
            {
                CardRow.Around(CardRowContent.Alcove, AlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, WonHeadlineDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.Pips, PipsDown, PipsHeight),
                CardRow.Around(CardRowContent.Rule, WonRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.Coins, CoinsDown, CoinsHeight),
                ButtonsAround(WonButtonsDown),
            },
            ResultCardKind.WonWithoutAPiece => new[]
            {
                CardRow.Around(CardRowContent.Alcove, AlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, HeadlineWithoutPipsDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.Rule, WonRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.Coins, CoinsDown, CoinsHeight),
                ButtonsAround(WonButtonsDown),
            },
            ResultCardKind.WhatIsLeft => new[]
            {
                CardRow.Around(CardRowContent.Alcove, AlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, WonHeadlineDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.Footnote, FootnoteDown, FootnoteHeight),
                CardRow.Around(CardRowContent.Rule, WonRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.Lives, CoinsDown, CoinsHeight),
                ButtonsAround(WonButtonsDown),
            },
            ResultCardKind.OutOfMoves => new[]
            {
                CardRow.Around(CardRowContent.Alcove, AlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, OfferHeadlineDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.GoalsLeft, GoalsLeftDown, GoalsLeftHeight),
                ButtonsAround(OfferButtonsDown),
                CardRow.Around(CardRowContent.Rule, OfferRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.GiveUp, GiveUpDown, GiveUpHeight),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A card the layout does not know."),
        };

        /// <summary>The row of a card that holds something.</summary>
        /// <exception cref="ArgumentException">That card has no such row.</exception>
        public static CardRow RowOf(ResultCardKind kind, CardRowContent content)
        {
            foreach (CardRow row in RowsOf(kind))
            {
                if (row.Content == content)
                {
                    return row;
                }
            }

            throw new ArgumentException($"A card of the kind {kind} has no row of {content}.", nameof(content));
        }

        /// <summary>True when the card has a row that holds something.</summary>
        public static bool Has(ResultCardKind kind, CardRowContent content) => RowsOf(kind).Any(row => row.Content == content);

        /// <summary>
        /// Where the goals that are left stand (LoseTR), right of the card's middle, for the widths of their numbers:
        /// each goal's picture and the left end of its number, the row centred on the card.
        /// </summary>
        public static IReadOnlyList<(float Picture, float NumberLeft)> GoalsLeftRow(IReadOnlyList<float> numberWidths)
        {
            float width = numberWidths.Sum(number => GoalLeftSize + GapGoalToNumber + number) + GapBetweenGoalsLeft * Math.Max(0, numberWidths.Count - 1);
            var places = new List<(float Picture, float NumberLeft)>(numberWidths.Count);
            float left = -width / 2f;
            foreach (float number in numberWidths)
            {
                places.Add((left + GoalLeftSize / 2f, left + GoalLeftSize + GapGoalToNumber));
                left += GoalLeftSize + GapGoalToNumber + number + GapBetweenGoalsLeft;
            }

            return places;
        }

        /// <summary>
        /// Where giving up stands (LoseTR), right of the card's middle, for the widths of "Vazgeç" and of what it
        /// costs: the left end of the one, the heart's middle, and the left end of the other, the row centred.
        /// </summary>
        public static (float GiveUpLeft, float Heart, float CostLeft) GiveUpRow(float giveUpWidth, float costWidth)
        {
            float left = -(giveUpWidth + GapGiveUpToHeart + GiveUpHeartSize + GapHeartToCost + costWidth) / 2f;
            float heartLeft = left + giveUpWidth + GapGiveUpToHeart;
            return (left, heartLeft + GiveUpHeartSize / 2f, heartLeft + GiveUpHeartSize + GapHeartToCost);
        }

        /// <summary>
        /// The row of the lives on a card of what is left, laid out as a won card's coins are: the heart where the coin
        /// stands, on its pill with the life the attempt costs, and the words after it; the words alone, centred, where
        /// it costs none.
        /// </summary>
        /// <param name="pillWidth">The pill's width (<see cref="PillWidth"/>); nothing where the attempt costs no life.</param>
        public static CoinRowPlaces LivesRow(float? pillWidth, float wordsWidth) => CoinRow(pillWidth, wordsWidth, hasABrush: false);

        /// <summary>The middles of the row's two buttons, right of the card's middle, for the width of the green one.</summary>
        public static (float GoOn, float Other) ButtonMiddles(float goOnWidth) =>
            (-ButtonsWidth / 2f + goOnWidth / 2f, ButtonsWidth / 2f - (ButtonsWidth - GapBetweenButtons - goOnWidth) / 2f);

        /// <summary>The coins' pill: as wide as the drawing's, or wider for an amount that needs more room.</summary>
        public static float PillWidth(float amountWidth) => Math.Max(NarrowestPill, AmountRoomLeft + amountWidth + AmountRoomRight);

        /// <summary>
        /// Where the parts of the coins' row stand, right of the card's middle, for a pill of a width and words of a
        /// width; no brush where no move was left over, and neither coin nor pill where the win paid nothing (the
        /// economy pays a win by its moves left over, so one won with its last move may pay none).
        /// </summary>
        /// <param name="pillWidth">The pill's width (<see cref="PillWidth"/>); nothing for a win that paid no coin.</param>
        public static CoinRowPlaces CoinRow(float? pillWidth, float wordsWidth, bool hasABrush)
        {
            float coinsWidth = pillWidth is float pill ? PillInFromCoin + pill + GapAfterPill : 0f;
            float brushWidth = hasABrush ? BrushSize + GapAfterBrush : 0f;
            float left = -(coinsWidth + brushWidth + wordsWidth) / 2f;
            float brushLeft = left + coinsWidth;
            CoinsOnTheirPill? coins = pillWidth is float width ? CoinsOnTheirPill.From(left, width) : (CoinsOnTheirPill?)null;
            return new CoinRowPlaces(coins, brush: brushLeft + BrushSize / 2f, wordsLeft: brushLeft + brushWidth, wordsRight: brushLeft + brushWidth + wordsWidth);
        }

        private static CardRow ButtonsAround(float buttonsMiddle) =>
            new CardRow(CardRowContent.Buttons, buttonsMiddle - ButtonHeight / 2f, buttonsMiddle + ButtonHeight / 2f + ButtonLip);
    }

    /// <summary>Where the coin (or the heart), its pill and the amount in it stand, in points right of the card's middle.</summary>
    public readonly struct CoinsOnTheirPill
    {
        private CoinsOnTheirPill(float coin, float pill, float amount)
        {
            Coin = coin;
            Pill = pill;
            Amount = amount;
        }

        /// <summary>The middle of the coin, which overlaps the pill's left end.</summary>
        public float Coin { get; }

        public float Pill { get; }

        /// <summary>The middle of the amount's line in the pill, between the room the coin takes and the pill's round end.</summary>
        public float Amount { get; }

        /// <summary>The coin's left edge here, and the pill of a width beside it.</summary>
        public static CoinsOnTheirPill From(float left, float pillWidth)
        {
            float pillLeft = left + ResultCardLayout.PillInFromCoin;
            return new CoinsOnTheirPill(
                left + ResultCardLayout.CoinSize / 2f,
                pillLeft + pillWidth / 2f,
                pillLeft + ResultCardLayout.AmountRoomLeft + (pillWidth - ResultCardLayout.AmountRoomLeft - ResultCardLayout.AmountRoomRight) / 2f);
        }
    }

    /// <summary>Where the parts of a won card's coins' row stand, in points right of the card's middle.</summary>
    public readonly struct CoinRowPlaces
    {
        public CoinRowPlaces(CoinsOnTheirPill? coins, float brush, float wordsLeft, float wordsRight)
        {
            Coins = coins;
            Brush = brush;
            WordsLeft = wordsLeft;
            WordsRight = wordsRight;
        }

        /// <summary>The coins the win paid on their pill; nothing for a win that paid none, whose row is its words alone.</summary>
        public CoinsOnTheirPill? Coins { get; }

        /// <summary>The middle of the brush; where there is none, the words begin where it would.</summary>
        public float Brush { get; }

        /// <summary>The words' left end, which they stand from, and their right end.</summary>
        public float WordsLeft { get; }

        public float WordsRight { get; }
    }
}
