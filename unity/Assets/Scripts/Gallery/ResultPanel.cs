#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// What stands on the card that is hung in front of the darkened room once a game is decided, or while its
    /// player is asked whether to leave it: under a brass nameplate that says what happened, an alcove with what the
    /// game left behind, what it means, and two buttons. A won level's card is the design canvas' (WinTR): the
    /// jigsaw piece it gave in the wide alcove, "Yeni parça: 7 / 10" over the painting's pips with the new one
    /// ringed, and the coins the win paid counting up on their pill beside the moves that became brushes (a level
    /// that gave no piece shows its star, and its level and moves in place of the piece's line); the green button
    /// leads on with the levels, and the blue one beside it to the jigsaw while pieces the player has won wait to
    /// be placed, else the teal one to the museum's hall. A lost level's card and the question whether to leave
    /// are the web test page's (result-panel.js) until their own stage. The card itself, its nameplate and its
    /// wires are the game's hanging card (<see cref="HangingCard{TPress}"/>); its rows are
    /// <see cref="ResultCardLayout"/>'s, in the canvas' points; a screen too small for the card draws all of it smaller.
    /// </summary>
    /// <remarks>
    /// The panel has no clock of its own. The stage tells it how far it has come down (<see cref="ComeDown"/>), how
    /// long its exhibits have been coming (<see cref="ShowExhibits"/>: the alcove's, then the coins counting up),
    /// what moment its sparkles are at (<see cref="Twinkle"/>), and where a finger is; the panel answers which
    /// button the finger pressed.
    /// </remarks>
    public sealed class ResultPanel : IDisposable
    {
        /// <summary>
        /// The lines under the alcove (WinTR): the stronger one in Gabarito of 17 ("Yeni parça:"), the piece's
        /// figures beside it in Paytone of 23 ("7 / 10"), 7 apart and on one baseline with it, 21 under the top of
        /// their row; the secondary line of the other cards in Gabarito of 12.5.
        /// </summary>
        private const float HeadlineSize = 17f;
        private const float PieceCountSize = 23f;
        private const float GapBeforePieceCount = 7f;
        private const float BaselineUnderRowTop = 21f;
        private const float FootnoteSize = 12.5f;

        /// <summary>The coins on their pill (WinTR, "+45": Paytone of 19); the words beside the brush in Gabarito of 12.5, their lines 3 apart.</summary>
        private const float AmountSize = 19f;
        private const float MovesLeftOverSize = 12.5f;
        private const float GapBetweenLinesOfWords = 3f;

        /// <summary>The coin is a mark in a box of 100 points; the pill's picture is its body and 28 more points of width, 68 of height.</summary>
        private const float CoinDrawnAcross = 100f;
        private const float PillPictureMargin = 28f;
        private const float PillPictureHeight = 68f;

        /// <summary>The lines under the alcove keep this far from the card's sides.</summary>
        private const float RoomBesideLines = 24f;

        /// <summary>The rule's two lines stop this far from the middle of their diamond, which is lit from its upper left.</summary>
        private const float RuleGapAroundDiamond = 10f;
        private const float DiamondAcross = 8.6f;
        private const float DiamondEdge = 1.2f;

        /// <summary>The labels of the buttons: the green "Devam" at 28, the teal "Müze" at 21, and two lines at 17.5 (WinTR, "Parçayı / yerleştir").</summary>
        private const float GoOnLabelSize = 28f;
        private const float MuseumLabelSize = 21f;
        private const float PlaceLabelSize = 17.5f;

        private readonly HangingCard<ResultChoice> _card;
        private readonly ResultCardKind _kind;
        private readonly PaintedShape _rule;
        private readonly ResultAlcove _alcove;
        private readonly ArtCatalogue _art;
        private readonly LevelResult _result;

        /// <summary>The coins the win paid as they count up on their pill; nothing on a card of what is left.</summary>
        private readonly CountedCoins? _coins;

        /// <summary>The green button, and the button beside it with where it leads: to the jigsaw, or to the museum.</summary>
        private PushButton _goOn;
        private PushButton _other;
        private ResultChoice _otherLeadsTo;

        private ResultPanel(Transform parent, ArtCatalogue art, EffectArt effects, PaintingGallery paintings, LevelResult result, int piecesWaiting)
        {
            _art = art;
            _result = result;
            _kind = KindOf(result);
            _card = new HangingCard<ResultChoice>(
                "Result", parent, art, new Vector2(ResultCardLayout.BodyWidth, ResultCardLayout.BodyHeight),
                new CardNameplate(ResultWords.Title(result), ResultCardLayout.NameplateWidth, ResultCardLayout.TitleSize), CardHanging.OnWiresFromAbove);

            _alcove = ResultAlcove.Create(_card.Content, art, effects, paintings, result, ResultCardLayout.FrameOf(_kind));
            float alcoveScale = ResultCardLayout.AlcoveScale(_kind);
            _alcove.Transform.localPosition = At(0f, Row(CardRowContent.Alcove).Middle);
            _alcove.Transform.localScale = new Vector3(alcoveScale, alcoveScale, 1f);

            _rule = PaintedShape.Create("Rule", _card.Content, art.PaintMaterial, DrawOrder.CardArt);
            _rule.Transform.localPosition = At(0f, Row(CardRowContent.Rule).Middle);
            if (result is LevelResult.Won won)
            {
                WriteWhatWasWon(won, paintings);
                _coins = new CountedCoins(_card.Content, art, won, Row(CardRowContent.Coins).Middle);
                _rule.Show(Rule(ResultCardLayout.WonRuleWidth));
            }
            else
            {
                // The web test page's card, drawn at the canvas' share of itself: its lines and its rule are as they were.
                float share = ResultCardLayout.WebPageShare;
                WriteLine("Headline", ResultWords.Headline(result), LetteringStyle.Label, share * HeadlineSize, Lettering.Ink, Row(CardRowContent.Headline).Middle);
                WriteLine("Footnote", ResultWords.Footnote(result), LetteringStyle.Soft, share * FootnoteSize, Lettering.SoftInk, Row(CardRowContent.Footnote).Middle);
                _rule.Show(Rule(ResultCardLayout.WebRuleWidth / share));
                _rule.Transform.localScale = new Vector3(share, share, 1f);
            }

            (_goOn, _other, _otherLeadsTo) = BuildTheButtons(piecesWaiting);
        }

        /// <summary>The middle of the green button, in the layout's points: where a finger presses it to go on with the levels.</summary>
        public Vector2 GoOnButtonMiddle => _card.InTheLayout(_goOn.Middle);

        /// <summary>The middle of the teal button, in the layout's points: where a finger presses it to go back to the museum. Nothing while the blue button stands there.</summary>
        public Vector2? MuseumButtonMiddle => _otherLeadsTo == ResultChoice.ToTheMuseum ? _card.InTheLayout(_other.Middle) : (Vector2?)null;

        /// <summary>The middle of the blue button, in the layout's points: where a finger presses it to place the pieces that wait. Nothing while none waits.</summary>
        public Vector2? PlaceButtonMiddle => _otherLeadsTo == ResultChoice.PlaceThePieces ? _card.InTheLayout(_other.Middle) : (Vector2?)null;

        /// <summary>The coins the card shows on its pill now, as far as they have counted; nothing on a card of what is left.</summary>
        public long? CoinsShown => _coins?.Shown;

        /// <summary>Builds the card of a result, out of sight above the screen. Call <see cref="Place"/> before the first picture.</summary>
        /// <param name="paintings">Where the picture of a piece that was won comes from.</param>
        /// <param name="piecesWaiting">How many pieces of the painting the level belongs to wait to be placed; while any does, a won level's card leads to the jigsaw.</param>
        public static ResultPanel Create(Transform parent, ArtCatalogue art, EffectArt effects, PaintingGallery paintings, LevelResult result, int piecesWaiting) =>
            new ResultPanel(parent, art, effects, paintings, result, piecesWaiting);

        /// <summary>
        /// Tells the card anew how many pieces wait, when the player comes back from the jigsaw: once none does,
        /// the teal button that leads to the museum stands where the blue one stood.
        /// </summary>
        public void OfferThePlacing(int piecesWaiting)
        {
            _goOn.Remove();
            _other.Remove();
            (_goOn, _other, _otherLeadsTo) = BuildTheButtons(piecesWaiting);
        }

        /// <summary>Hangs the card where the layout has room for it; call again whenever the screen changes.</summary>
        public void Place(ScreenLayout layout) => _card.Place(layout.PlaceOfACard(_card.Width, _card.Height), layout.View);

        /// <summary>The card on its way down: 0 is out of sight above the screen, 1 is in its place, which it meets with a little give.</summary>
        public void ComeDown(float progress) => _card.ComeDown(progress);

        /// <summary>The card on its way back up: 0 is in its place, 1 is out of sight.</summary>
        public void GoUp(float progress) => _card.GoUp(progress);

        /// <summary>How long the exhibits take: the alcove's popping up, and after it the coins counting up.</summary>
        public float ExhibitsSeconds => _alcove.PopSeconds + (_coins?.HasCoinsToCount == true ? Tempo.CoinsCount : 0f);

        /// <summary>Shows the exhibits as they are a while after they began to come: the alcove's first, then the coins.</summary>
        public void ShowExhibits(float secondsIn)
        {
            _alcove.Pop(secondsIn);
            _coins?.CountTo(Mathf.Clamp01((secondsIn - _alcove.PopSeconds) / Tempo.CoinsCount));
        }

        /// <summary>What never stands still on the card, as it is at a moment of the stage's clock.</summary>
        public void Twinkle(float clockSeconds) => _alcove.Twinkle(clockSeconds);

        /// <summary>A finger came down at a point of the layout.</summary>
        public void FingerDown(Vector2 point) => _card.FingerDown(point);

        /// <summary>The finger moved while it was down: a button it has slid off is not pressed, one it came back to is.</summary>
        public void FingerMovedTo(Vector2 point) => _card.FingerMovedTo(point);

        /// <summary>The finger lifted.</summary>
        /// <returns>True when that pressed a button: the finger came down on it and lifted on it. <paramref name="choice"/> is then where that button leads.</returns>
        public bool FingerUp(out ResultChoice choice) => _card.FingerUp(out choice);

        /// <summary>The touch ended without a lift the game saw: nothing is pressed.</summary>
        public void FingerGone() => _card.FingerGone();

        public void Dispose()
        {
            _alcove.Dispose();
            _rule.Dispose();
            _card.Dispose();
        }

        /// <summary>Which of the layout's cards a result's is.</summary>
        private static ResultCardKind KindOf(LevelResult result) => result switch
        {
            LevelResult.Won { Reward: PieceReward _ } => ResultCardKind.WonWithAPiece,
            LevelResult.Won _ => ResultCardKind.WonWithoutAPiece,
            LevelResult.Lost _ => ResultCardKind.WhatIsLeft,
            LevelResult.Leaving _ => ResultCardKind.WhatIsLeft,
            _ => throw new InvalidOperationException($"A result of the kind {result.GetType().Name} has no card."),
        };

        private CardRow Row(CardRowContent content) => ResultCardLayout.RowOf(_kind, content);

        /// <summary>A place on the card: so far right of its middle, so far under its top edge.</summary>
        private Vector3 At(float right, float down) => _card.At(down) + new Vector3(right, 0f, 0f);

        /// <summary>
        /// Under a won level's alcove: the piece it gave as "Yeni parça:" with the figures beside it and the
        /// painting's pips under them, or, for a level that gave no piece, the level and the moves it took.
        /// </summary>
        private void WriteWhatWasWon(LevelResult.Won won, PaintingGallery paintings)
        {
            float rowMiddle = Row(CardRowContent.Headline).Middle;
            if (won.Reward is not PieceReward reward)
            {
                WriteLine("Headline", ResultWords.Headline(won), LetteringStyle.Label, HeadlineSize, Lettering.Ink, rowMiddle);
                return;
            }

            // One baseline for both typefaces, as the canvas aligns them; the game places a line by the middle of its capitals.
            float baseline = Row(CardRowContent.Headline).Top + BaselineUnderRowTop;
            TextMeshPro lead = Lettering.Write("Piece won", _card.Content, _art, LetteringStyle.Label, HeadlineSize, Lettering.Ink, DrawOrder.CardLettering);
            TextMeshPro count = Lettering.Write("Pieces won of all", _card.Content, _art, LetteringStyle.Figure, PieceCountSize, Lettering.Ink, DrawOrder.CardLettering);
            string leadWords = MuseumWords.EarnedLead(reward);
            string countWords = MuseumWords.EarnedCount(reward);
            float leadWidth = Lettering.WidthOf(lead, leadWords);
            float left = -(leadWidth + GapBeforePieceCount + Lettering.WidthOf(count, countWords)) / 2f;
            Lettering.SayFromLeft(lead, leadWords, left, At(0f, baseline - CapitalsMiddleOverBaseline(LetteringStyle.Label, HeadlineSize)).y);
            Lettering.SayFromLeft(count, countWords, left + leadWidth + GapBeforePieceCount, At(0f, baseline - CapitalsMiddleOverBaseline(LetteringStyle.Figure, PieceCountSize)).y);

            var pips = new PipRow("Pips", _card.Content, _art, PipRowPlan.Of(reward.PieceCount, reward.WonWithThem, reward.Pieces.Count), DrawOrder.CardArt);
            pips.Place(At(0f, Row(CardRowContent.Pips).Middle));
        }

        /// <summary>How far over its baseline the middle of a line's capitals stands: half the height of the typeface's capitals (Lettering.CapitalsHeight).</summary>
        private static float CapitalsMiddleOverBaseline(LetteringStyle style, float size) => Lettering.CapitalsHeight(style) * size / 2f;

        /// <summary>
        /// The row of two buttons, and what the finger can press: the green one at the left (on with the levels,
        /// or on with the game for a player who asked to leave it), and at the right the blue one while a won
        /// level's pieces wait, or else the teal one.
        /// </summary>
        private (PushButton GoOn, PushButton Other, ResultChoice OtherLeadsTo) BuildTheButtons(int piecesWaiting)
        {
            bool offersThePlacing = _result is LevelResult.Won && piecesWaiting > 0;
            ResultChoice greenLeadsTo = _result is LevelResult.Leaving ? ResultChoice.Stay : ResultChoice.GoOn;
            float goOnWidth = offersThePlacing ? ResultCardLayout.GoOnBesidePlaceWidth : ResultCardLayout.GoOnBesideMuseumWidth;
            float otherWidth = offersThePlacing ? ResultCardLayout.PlaceWidth : ResultCardLayout.MuseumWidth;
            string goOnLabel = offersThePlacing ? ResultWords.GoOnOnANarrowButton(_result) : ResultWords.GoOn(_result);
            bool isOnTwoLines = goOnLabel.Contains("\n");
            float goOnLabelSize = isOnTwoLines ? PlaceLabelSize : GoOnLabelSize;
            var goOn = new PushButton(
                "Go on", _card.Content, _art, ButtonLook.Green, goOnLabel, goOnWidth, goOnLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            PushButton other = offersThePlacing
                ? new PushButton(
                    "Place the pieces", _card.Content, _art, ButtonLook.Blue, MuseumWords.PlaceWhatWasWon(piecesWaiting), otherWidth, PlaceLabelSize,
                    DrawOrder.CardArt, DrawOrder.CardLettering, GalleryArt.IconPiece)
                : new PushButton(
                    "To the museum", _card.Content, _art, ButtonLook.Teal, MuseumWords.BackToTheMuseum, otherWidth, MuseumLabelSize,
                    DrawOrder.CardArt, DrawOrder.CardLettering);
            float row = Row(CardRowContent.Buttons).Top + ResultCardLayout.ButtonHeight / 2f;
            (float goOnRight, float otherRight) = ResultCardLayout.ButtonMiddles(goOnWidth);
            goOn.Place(At(goOnRight, row));
            other.Place(At(otherRight, row));

            ResultChoice otherLeadsTo = offersThePlacing ? ResultChoice.PlaceThePieces : ResultChoice.ToTheMuseum;
            _card.ForgetTheControls();
            _card.Listen(goOn, greenLeadsTo);
            _card.Listen(other, otherLeadsTo);
            return (goOn, other, otherLeadsTo);
        }

        private void WriteLine(string name, string words, LetteringStyle style, float size, Color colour, float down)
        {
            TextMeshPro line = Lettering.Write(name, _card.Content, _art, style, size, colour, DrawOrder.CardLettering);
            line.transform.localPosition = At(0f, down);
            Lettering.SayWithin(line, words, size, ResultCardLayout.BodyWidth - 2f * RoomBesideLines);
        }

        /// <summary>The rule, around its own middle: a line to each side of a diamond, which is lit from its upper left.</summary>
        private static Mesh Rule(float width)
        {
            var sketch = new MeshSketch();
            GiltRule.AddLine(sketch, -width / 2f, -RuleGapAroundDiamond, height: 0f, isShaded: false);
            GiltRule.AddLine(sketch, RuleGapAroundDiamond, width / 2f, height: 0f, isShaded: false);
            GiltRule.AddDiamond(sketch, Vector2.zero, DiamondAcross, DiamondEdge);
            return sketch.ToMesh("Rule");
        }

        /// <summary>
        /// A won card's row of coins (WinTR): the coins the win paid on their pill, which count up from nothing as
        /// the card shows its exhibits, and beside them the brush with what became of the moves left over, or, with
        /// no move left over, only the words that the level was won with its last move. A win that paid no coin (the
        /// economy pays by the moves left over) has no pill: its row is its words alone.
        /// </summary>
        private sealed class CountedCoins
        {
            /// <summary>The amount on the pill; nothing where the win paid no coin and there is no pill.</summary>
            private readonly TextMeshPro? _amount;
            private readonly long _paid;

            public CountedCoins(Transform card, ArtCatalogue art, LevelResult.Won won, float rowDown)
            {
                _paid = won.Coins.Amount;
                TextMeshPro[] lines = WriteTheWords(card, art, ResultWords.MovesLeftOver(won).Split('\n'));
                float wordsWidth = 0f;
                foreach (TextMeshPro line in lines)
                {
                    wordsWidth = Mathf.Max(wordsWidth, Lettering.WidthOf(line, line.text));
                }

                bool hasABrush = won.MovesLeft > 0;
                float rowY = OnTheCard(rowDown);
                if (won.Coins.IsNone)
                {
                    PlaceTheWords(lines, ResultCardLayout.CoinRow(pillWidth: null, wordsWidth, hasABrush), rowDown);
                    return;
                }

                _amount = Lettering.Write("Coins won", card, art, LetteringStyle.Figure, AmountSize, Lettering.Ink, DrawOrder.CardLettering);
                float pillWidth = ResultCardLayout.PillWidth(Lettering.WidthOf(_amount, ResultWords.CoinsWon(won.Coins)));
                CoinRowPlaces places = ResultCardLayout.CoinRow(pillWidth, wordsWidth, hasABrush);
                CoinsOnTheirPill coins = places.Coins ?? throw new InvalidOperationException("A row laid out for a pill has no place for its coins.");

                SpriteRenderer pill = SceneObjects.AddSprite("Coins' pill", card, art.Of(GalleryArt.WalletPill), art.SpriteMaterial, DrawOrder.CardArt);
                pill.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
                pill.size = new Vector2(pillWidth + PillPictureMargin, PillPictureHeight);
                pill.transform.localPosition = new Vector3(coins.Pill, rowY, 0f);

                Transform coin = SceneObjects.AddSprite("Coin", card, art.Of(GalleryArt.IconCoin), art.SpriteMaterial, DrawOrder.CardExhibit).transform;
                coin.localPosition = new Vector3(coins.Coin, rowY + ResultCardLayout.CoinOverRow, 0f);
                coin.localScale = Vector3.one * (ResultCardLayout.CoinSize / CoinDrawnAcross);

                // The amount is centred in the pill as a stylesheet centres a line, half a point lower; the game places it by its figures.
                float amountDown = rowDown + ResultCardLayout.AmountUnderMiddle + Lettering.FigureDrop(LetteringStyle.Figure) * AmountSize;
                _amount.transform.localPosition = new Vector3(coins.Amount, OnTheCard(amountDown), 0f);

                if (hasABrush)
                {
                    Transform brush = SceneObjects.AddSprite("Brush", card, art.Of(PieceArt.RocketHorizontal), art.SpriteMaterial, DrawOrder.CardExhibit).transform;
                    brush.localPosition = new Vector3(places.Brush, rowY, 0f);
                    brush.localScale = new Vector3(ResultCardLayout.BrushSize, ResultCardLayout.BrushSize, 1f);
                }

                PlaceTheWords(lines, places, rowDown);
                CountTo(0f);
            }

            /// <summary>The amount the pill shows now; none where there is no pill.</summary>
            public long Shown { get; private set; }

            /// <summary>True when there are coins to count up: the win paid some.</summary>
            public bool HasCoinsToCount => _amount != null;

            /// <summary>Shows the coins counted so far: 0 is none of them, 1 all, and between, the count slows as it ends.</summary>
            public void CountTo(float counted)
            {
                if (_amount == null)
                {
                    return; // no coin was paid: there is nothing to count
                }

                Shown = (long)Math.Round(_paid * (double)Easing.Out(counted));
                Lettering.Say(_amount, ResultWords.CoinsWon(new Coins(Shown)));
            }

            /// <summary>The words' lines from their left end, centred together on the row, as far apart as the canvas sets them.</summary>
            private static void PlaceTheWords(TextMeshPro[] lines, CoinRowPlaces places, float rowDown)
            {
                float linesApart = MovesLeftOverSize + GapBetweenLinesOfWords;
                float firstDown = rowDown - (lines.Length - 1) * linesApart / 2f;
                for (int line = 0; line < lines.Length; line++)
                {
                    Lettering.SayFromLeft(lines[line], lines[line].text, places.WordsLeft, OnTheCard(firstDown + line * linesApart));
                }
            }

            private static TextMeshPro[] WriteTheWords(Transform card, ArtCatalogue art, string[] lines)
            {
                var written = new TextMeshPro[lines.Length];
                for (int line = 0; line < lines.Length; line++)
                {
                    written[line] = Lettering.Write($"Moves left over {line + 1}", card, art, LetteringStyle.Soft, MovesLeftOverSize, Lettering.SoftInk, DrawOrder.CardLettering);
                    Lettering.Say(written[line], lines[line]);
                }

                return written;
            }

            /// <summary>The height on the card of a place so far under its top edge: the card's content is laid out around the middle of its body.</summary>
            private static float OnTheCard(float down) => ResultCardLayout.BodyHeight / 2f - down;
        }
    }
}
