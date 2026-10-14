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

        /// <summary>A level lost, or the question whether to leave one: what is left of the goals, two lines, two buttons.</summary>
        WhatIsLeft,
    }

    /// <summary>Which beaded frame a card's alcove has. A closed set.</summary>
    public enum AlcoveFrame
    {
        /// <summary>The web test page's: a niche of 288 by 188 in a frame of 302 by 202.</summary>
        WebPage,

        /// <summary>The design canvas' (WinTR, LoseTR): a niche of 316 by 188 in a frame of 330 by 202.</summary>
        Wide,
    }

    /// <summary>What one row of a result card holds. A closed set.</summary>
    public enum CardRowContent
    {
        /// <summary>The alcove in its beaded frame, with what the game left on its shelf.</summary>
        Alcove,

        /// <summary>The stronger line under the alcove: the piece won, or the level with its moves or its attempt.</summary>
        Headline,

        /// <summary>The painting's pieces as pips, the one just won ringed.</summary>
        Pips,

        /// <summary>The smaller line under the headline.</summary>
        Footnote,

        /// <summary>The gilt rule with its diamond.</summary>
        Rule,

        /// <summary>The coins the win paid on their pill, and the moves that turned into brushes.</summary>
        Coins,

        /// <summary>The two buttons, with the lip under them.</summary>
        Buttons,
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
    /// The rows of the card that hangs in front of the board once a game is decided, or while its player is asked
    /// whether to leave it, in the canvas' own points: down from the top edge of a body of 362 by 468, with the brass
    /// nameplate riding on that edge. A won level's card is the design canvas' (WinTR): the alcove in the wide frame,
    /// the line of the piece won, the painting's pips, the rule, the coins and the moves that became brushes, and the
    /// two buttons. The cards of a level lost and of the question to leave are the web test page's at the canvas'
    /// share, until their own stage gives them the canvas' rows (LoseTR). A screen too small for a card draws all of
    /// it smaller (<see cref="ScreenLayout.PlaceOfACard"/>). No Unity dependency.
    /// </summary>
    public static class ResultCardLayout
    {
        public const float BodyWidth = 362f;
        public const float BodyHeight = 468f;

        /// <summary>The nameplate (WinTR): 280 wide, 40 high, its middle on the card's top edge, the title in the serif at 20.5.</summary>
        public const float NameplateWidth = 280f;
        public const float TitleSize = 20.5f;
        private const float NameplateHeight = 40f;

        /// <summary>The web test page's card was laid out 332 points wide: its measures are drawn at this share of themselves on the canvas' 362.</summary>
        public const float WebPageShare = BodyWidth / 332f;

        /// <summary>
        /// The won card's alcove (WinTR): a niche of 316 by 188, 38 under the card's top edge, in the wide frame,
        /// whose band of 7 reaches past the niche on every side. The other cards keep the web test page's narrower
        /// alcove, a frame of 302 by 202, drawn at <see cref="WebPageShare"/>.
        /// </summary>
        public const float WideFrameWidth = 330f;
        public const float WideFrameHeight = 202f;
        private const float WonAlcoveDown = 38f + 188f / 2f;
        private const float NarrowFrameHeight = 202f;

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

        /// <summary>The cards of what is left: the web test page's rows (ResultPanel before the canvas' card), at the canvas' share.</summary>
        private const float WebAlcoveDown = 131f * WebPageShare;
        private const float WebHeadlineDown = 257.5f * WebPageShare;
        private const float WebFootnoteDown = 281f * WebPageShare;
        private const float WebRuleDown = 306.5f * WebPageShare;
        private const float WebButtonsDown = 366f * WebPageShare;
        private const float HeadlineHeight = 17f;
        private const float FootnoteHeight = 12.5f;

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

        /// <summary>The rule (WinTR, a gilt line of 120 with a diamond in its middle); the other cards keep the web page's 180 at its share.</summary>
        public const float WonRuleWidth = 120f;
        public const float WebRuleWidth = 180f * WebPageShare;

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
                CardRow.Around(CardRowContent.Alcove, WonAlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, WonHeadlineDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.Pips, PipsDown, PipsHeight),
                CardRow.Around(CardRowContent.Rule, WonRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.Coins, CoinsDown, CoinsHeight),
                ButtonsAround(WonButtonsDown),
            },
            ResultCardKind.WonWithoutAPiece => new[]
            {
                CardRow.Around(CardRowContent.Alcove, WonAlcoveDown, WideFrameHeight),
                CardRow.Around(CardRowContent.Headline, HeadlineWithoutPipsDown, WonHeadlineHeight),
                CardRow.Around(CardRowContent.Rule, WonRuleDown, RuleHeight),
                CardRow.Around(CardRowContent.Coins, CoinsDown, CoinsHeight),
                ButtonsAround(WonButtonsDown),
            },
            ResultCardKind.WhatIsLeft => new[]
            {
                CardRow.Around(CardRowContent.Alcove, WebAlcoveDown, NarrowFrameHeight * WebPageShare),
                CardRow.Around(CardRowContent.Headline, WebHeadlineDown, HeadlineHeight),
                CardRow.Around(CardRowContent.Footnote, WebFootnoteDown, FootnoteHeight),
                CardRow.Around(CardRowContent.Rule, WebRuleDown, RuleHeight),
                ButtonsAround(WebButtonsDown),
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

        /// <summary>The alcove's frame: the canvas' wide one on a won card, the web page's on the others.</summary>
        public static AlcoveFrame FrameOf(ResultCardKind kind) => kind == ResultCardKind.WhatIsLeft ? AlcoveFrame.WebPage : AlcoveFrame.Wide;

        /// <summary>How large the alcove's frame is drawn: the canvas' at its size, the web page's at its share.</summary>
        public static float AlcoveScale(ResultCardKind kind) => FrameOf(kind) == AlcoveFrame.WebPage ? WebPageShare : 1f;

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

    /// <summary>Where the coin, its pill and the amount in it stand, in points right of the card's middle.</summary>
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
