#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Gallery
{
    /// <summary>
    /// What stands on the card that is hung in front of the darkened room once a game is decided, as the web test
    /// page shows its result panel (result-panel.js, gallery.css): under a brass nameplate that says what happened,
    /// an alcove with what the game left behind, two lines about the level, and two buttons: the green one leads
    /// on with the levels; the other is the blue one that leads to the jigsaw while pieces the player has won wait
    /// to be placed, as on the design canvas (WinTR), and otherwise the teal one that leads back to the museum's
    /// hall. The card itself, its nameplate and its wires are the game's hanging card (<see cref="HangingCard{TPress}"/>).
    /// Sizes are in points around the middle of the card at its full size; a screen too small for that draws the
    /// whole card smaller.
    /// </summary>
    /// <remarks>
    /// The panel has no clock of its own. The stage tells it how far it has come down (<see cref="ComeDown"/>), how
    /// long its exhibits have been coming (<see cref="ShowExhibits"/>), what moment its sparkles are at
    /// (<see cref="Twinkle"/>), and where a finger is; the panel answers which button the finger pressed.
    /// </remarks>
    public sealed class ResultPanel : IDisposable
    {
        /// <summary>The card's body. Its height is what stands on it, from the top edge down, as the measures below place it.</summary>
        private const float CardWidth = 332f;
        private const float CardHeight = 429f;

        /// <summary>The nameplate is the design's (WinTR): 280 points wide, riding on the card's top edge with half of it above the card.</summary>
        private const float NameplateWidth = 280f;

        /// <summary>How far under the card's top edge the middle of each thing lies.</summary>
        private const float AlcoveDown = 131f;
        private const float HeadlineDown = 257.5f;
        private const float FootnoteDown = 281f;
        private const float RuleDown = 306.5f;
        private const float ButtonDown = 366f;

        /// <summary>The title on the nameplate, as the design sets it (WinTR, "Bölüm tamamlandı!": the serif at 20.5 points); it keeps clear of the plate's two studs.</summary>
        private const float TitleSize = 20.5f;

        /// <summary>
        /// The two lines under the alcove: the stronger one at the size of the design's words there (WinTR, "Yeni
        /// parça:", 17 points), the secondary one at the size of its secondary lines (WinTR, "Kalan 3 hamle", 12.5).
        /// </summary>
        private const float HeadlineSize = 17f;
        private const float FootnoteSize = 12.5f;

        /// <summary>The lines under the alcove keep this far from the card's sides.</summary>
        private const float LineRoom = CardWidth - 2f * 24f;

        /// <summary>The design's rule across the card: a gilt line to each side of a cut diamond.</summary>
        private const float RuleWidth = 180f;
        private const float RuleGapAroundDiamond = 10f;
        private const float DiamondAcross = 8.6f;
        private const float DiamondEdge = 1.2f;

        /// <summary>
        /// The card is the design's (WinTR, 362 points wide) at this share of its size, and so is its row of
        /// buttons: the measures of the row are the design's own, and the buttons are drawn at the card's share.
        /// </summary>
        private const float DesignCardWidth = 362f;
        private const float ShareOfTheDesign = CardWidth / DesignCardWidth;

        /// <summary>
        /// The two buttons stand side by side over the width the design's row of buttons has (WinTR: 328 points,
        /// a gap of 12): the green one, the wider, at the left, the teal one at the right. The green label is the
        /// design's "Devam" at 28 points; the teal button is not the design's, and is as narrow as "Müze" allows.
        /// </summary>
        private const float ButtonsWidth = 328f;
        private const float GapBetweenButtons = 12f;
        private const float MuseumButtonWidth = 118f;
        private const float GoOnButtonWidth = ButtonsWidth - GapBetweenButtons - MuseumButtonWidth;
        private const float GoOnLabelSize = 28f;
        private const float MuseumLabelSize = 21f;

        /// <summary>
        /// While pieces wait, the blue button takes the wider share of the row, for its mark and its two lines
        /// (WinTR: 166 points, "Parçayı / yerleştir" at 17.5), and the green one beside it has the 150 points the
        /// design gives it. A green label of two words stands on two lines there, at the size of the blue one's.
        /// </summary>
        private const float PlaceButtonWidth = 166f;
        private const float GoOnBesidePlaceWidth = ButtonsWidth - GapBetweenButtons - PlaceButtonWidth;
        private const float PlaceLabelSize = 17.5f;

        private readonly HangingCard<ResultChoice> _card;
        private readonly PaintedShape _rule;
        private readonly ResultAlcove _alcove;
        private readonly ArtCatalogue _art;
        private readonly LevelResult _result;

        /// <summary>The green button, and the button beside it with where it leads: to the jigsaw, or to the museum.</summary>
        private PushButton _goOn;
        private PushButton _other;
        private ResultChoice _otherLeadsTo;

        private ResultPanel(Transform parent, ArtCatalogue art, EffectArt effects, PaintingGallery paintings, LevelResult result, int piecesWaiting)
        {
            _art = art;
            _result = result;
            _card = new HangingCard<ResultChoice>(
                "Result", parent, art, new Vector2(CardWidth, CardHeight),
                new CardNameplate(ResultWords.Title(result), NameplateWidth, TitleSize), CardHanging.OnWiresFromAbove);

            _alcove = ResultAlcove.Create(_card.Content, art, effects, paintings, result);
            _alcove.Transform.localPosition = _card.At(AlcoveDown);

            WriteLine("Headline", ResultWords.Headline(result), art, LetteringStyle.Label, HeadlineSize, Lettering.Ink, HeadlineDown);
            WriteLine("Footnote", ResultWords.Footnote(result), art, LetteringStyle.Soft, FootnoteSize, Lettering.SoftInk, FootnoteDown);
            _rule = PaintedShape.Create("Rule", _card.Content, art.PaintMaterial, DrawOrder.CardArt);
            _rule.Transform.localPosition = _card.At(RuleDown);
            _rule.Show(Rule());

            (_goOn, _other, _otherLeadsTo) = BuildTheButtons(piecesWaiting);
        }

        /// <summary>The middle of the green button, in the layout's points: where a finger presses it to go on with the levels.</summary>
        public Vector2 GoOnButtonMiddle => _card.InTheLayout(_goOn.Middle);

        /// <summary>The middle of the teal button, in the layout's points: where a finger presses it to go back to the museum. Nothing while the blue button stands there.</summary>
        public Vector2? MuseumButtonMiddle => _otherLeadsTo == ResultChoice.ToTheMuseum ? _card.InTheLayout(_other.Middle) : (Vector2?)null;

        /// <summary>The middle of the blue button, in the layout's points: where a finger presses it to place the pieces that wait. Nothing while none waits.</summary>
        public Vector2? PlaceButtonMiddle => _otherLeadsTo == ResultChoice.PlaceThePieces ? _card.InTheLayout(_other.Middle) : (Vector2?)null;

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

        /// <summary>How long the alcove's exhibits take to pop up.</summary>
        public float ExhibitsSeconds => _alcove.PopSeconds;

        /// <summary>Shows the alcove's exhibits as they are a while after they began to pop up.</summary>
        public void ShowExhibits(float secondsIn) => _alcove.Pop(secondsIn);

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

        /// <summary>
        /// The row of two buttons, and what the finger can press: the green one at the left (on with the levels,
        /// or on with the game for a player who asked to leave it), and at the right the blue one while a won
        /// level's pieces wait, or else the teal one.
        /// </summary>
        private (PushButton GoOn, PushButton Other, ResultChoice OtherLeadsTo) BuildTheButtons(int piecesWaiting)
        {
            bool offersThePlacing = _result is LevelResult.Won && piecesWaiting > 0;
            ResultChoice greenLeadsTo = _result is LevelResult.Leaving ? ResultChoice.Stay : ResultChoice.GoOn;
            float goOnWidth = offersThePlacing ? GoOnBesidePlaceWidth : GoOnButtonWidth;
            float otherWidth = offersThePlacing ? PlaceButtonWidth : MuseumButtonWidth;
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
            Vector2 row = _card.At(ButtonDown);
            goOn.Place(row + new Vector2(ShareOfTheDesign * (-ButtonsWidth / 2f + goOnWidth / 2f), 0f), ShareOfTheDesign);
            other.Place(row + new Vector2(ShareOfTheDesign * (ButtonsWidth / 2f - otherWidth / 2f), 0f), ShareOfTheDesign);

            ResultChoice otherLeadsTo = offersThePlacing ? ResultChoice.PlaceThePieces : ResultChoice.ToTheMuseum;
            _card.ForgetTheControls();
            _card.Listen(goOn, greenLeadsTo);
            _card.Listen(other, otherLeadsTo);
            return (goOn, other, otherLeadsTo);
        }

        private void WriteLine(string name, string words, ArtCatalogue art, LetteringStyle style, float size, Color colour, float down)
        {
            TextMeshPro line = Lettering.Write(name, _card.Content, art, style, size, colour, DrawOrder.CardLettering);
            line.transform.localPosition = _card.At(down);
            Lettering.SayWithin(line, words, size, LineRoom);
        }

        /// <summary>The rule, around its own middle: a line to each side of a diamond, which is lit from its upper left.</summary>
        private static Mesh Rule()
        {
            var sketch = new MeshSketch();
            GiltRule.AddLine(sketch, -RuleWidth / 2f, -RuleGapAroundDiamond, height: 0f, isShaded: false);
            GiltRule.AddLine(sketch, RuleGapAroundDiamond, RuleWidth / 2f, height: 0f, isShaded: false);
            GiltRule.AddDiamond(sketch, Vector2.zero, DiamondAcross, DiamondEdge);
            return sketch.ToMesh("Rule");
        }
    }
}
