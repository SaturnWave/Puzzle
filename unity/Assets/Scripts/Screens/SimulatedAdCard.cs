#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card a test build hangs in place of an ad (the settings' build.ads "simulated"): it says on its nameplate
    /// that it stands in for one and that this is the test build, shows what it pays for in a gilt alcove (the +5 of
    /// a game out of moves, or the shop's pile of coins with their number), says in words that no ad is shown and
    /// what it pays, and fills a bar as the stand-in's seconds run, the seconds left beside it. It has nothing to press
    /// and goes up by itself when the ad is over. Nothing is advertised, nothing is loaded.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LoseTR's card on
    /// wires from above, LevelStartTR's nameplate of two lines, LoseTR's alcove and +5, the jigsaw's dark teal bar) and
    /// is the owner's to look at and to redraw. It hangs on wires and not on pins in the wall: its long name needs a
    /// plate wider than the pins leave room for. Its sizes are the canvas' points around the middle of its body.
    /// </remarks>
    public sealed class SimulatedAdCard : IDisposable
    {
        /// <summary>The body, and the nameplate with the card's name over the test build's mark.</summary>
        private static readonly Vector2 Body = new Vector2(316f, 300f);
        private const float NameplateWidth = 272f;
        private const float TitleSize = 19f;

        /// <summary>The canvas' wide alcove, drawn smaller, its middle this far under the body's top edge, with the +5 in it.</summary>
        private const float AlcoveScale = 0.8f;
        private const float AlcoveDown = 112f;
        private const float ExtraMovesSize = 112f;
        private const float ExtraMovesUnderMiddle = 10f;
        private const float MarkBox = 100f;

        /// <summary>The shop's coins in the alcove: the pile, and their number beside it in the figures of a teal ground, the two centred together.</summary>
        private const float CoinPileSize = 84f;
        private const float CoinsNumberSize = 34f;
        private const float GapPileToNumber = 4f;

        /// <summary>The two lines under the alcove.</summary>
        private const float FirstLineDown = 214f;
        private const float SecondLineDown = 236f;
        private const float FirstLineSize = 14.5f;
        private const float SecondLineSize = 13f;
        private const float LinesInFromSides = 22f;

        /// <summary>
        /// The bar (the jigsaw's dark teal one, 22 high), the seconds left 10 points after it in Paytone of 15, the two
        /// centred together; the green that fills it is the badge of a chosen booster (22 high, its picture 10 points
        /// wider and 34 high) drawn at 0.7, and at the least as wide as it is high.
        /// </summary>
        private const float BarDown = 270f;
        private const float BarWidth = 200f;
        private const float BarPictureMargin = 12f;
        private const float BarPictureHeight = 32f;
        private const float FillScale = 0.7f;
        private const float FillBadgeHeight = 22f;
        private const float FillPictureMargin = 10f;
        private const float FillPictureHeight = 34f;
        private const float FillInset = 3f;
        private const float GapBarToSeconds = 10f;
        private const float SecondsSize = 15f;
        private const float SecondsRoom = 40f;

        private readonly HangingCard<NoPress> _card;
        private readonly SpriteRenderer _fill;
        private readonly TextMeshPro _seconds;
        private readonly float _barLeft;

        /// <param name="prize">What the ad pays for once it is over, which the card shows and says.</param>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> where it hangs in front of the card that offered the moves.</param>
        public SimulatedAdCard(Transform parent, ArtCatalogue art, AdPrize prize, int raisedBy)
        {
            _card = new HangingCard<NoPress>(
                "Simulated ad card", parent, art, Body, new CardNameplate(AdWords.Title, NameplateWidth, TitleSize, AdWords.TestBuild), CardHanging.OnWiresFromAbove, raisedBy: raisedBy);

            Transform alcove = SceneObjects.AddSprite("Alcove", _card.Content, art.Of(GalleryArt.WideAlcove), art.SpriteMaterial, DrawOrder.CardArt + raisedBy).transform;
            alcove.localPosition = _card.At(AlcoveDown);
            alcove.localScale = new Vector3(AlcoveScale, AlcoveScale, 1f);
            ShowThePrize(prize, art, raisedBy);

            float room = Body.x - 2f * LinesInFromSides;
            TextMeshPro first = Lettering.Write("Nothing is shown", _card.Content, art, LetteringStyle.Label, FirstLineSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(first, AdWords.NothingIsShown, FirstLineSize, room);
            first.transform.localPosition = _card.At(FirstLineDown);
            TextMeshPro second = Lettering.Write("What it pays for", _card.Content, art, LetteringStyle.Soft, SecondLineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(second, AdWords.ItPaysFor(prize), SecondLineSize, room);
            second.transform.localPosition = _card.At(SecondLineDown);

            float rowLeft = -(BarWidth + GapBarToSeconds + SecondsRoom) / 2f;
            _barLeft = rowLeft;
            float barHeight = _card.At(BarDown).y;
            SpriteRenderer bar = SceneObjects.AddSprite("Bar", _card.Content, art.Of(GalleryArt.PipBar), art.SpriteMaterial, DrawOrder.CardArt + raisedBy);
            bar.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            bar.size = new Vector2(BarWidth + BarPictureMargin, BarPictureHeight);
            bar.transform.localPosition = new Vector3(rowLeft + BarWidth / 2f, barHeight, 0f);

            _fill = SceneObjects.AddSprite("Filled", _card.Content, art.Of(GalleryArt.GreenBadge), art.SpriteMaterial, DrawOrder.CardExhibit + raisedBy);
            _fill.drawMode = SpriteDrawMode.Sliced;
            _fill.transform.localScale = new Vector3(FillScale, FillScale, 1f);

            _seconds = Lettering.Write("Seconds left", _card.Content, art, LetteringStyle.Figure, SecondsSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            _seconds.transform.localPosition = new Vector3(rowLeft + BarWidth + GapBarToSeconds + SecondsRoom / 2f, barHeight - Lettering.FigureDrop(LetteringStyle.Figure) * SecondsSize, 0f);
            Show(new SimulatedAd());
        }

        /// <summary>What a press on the card asks for: nothing, the card has nothing to press. A closed set with no member.</summary>
        private enum NoPress
        {
        }

        /// <summary>The room the card takes on a screen.</summary>
        public float Width => _card.Width;

        public float Height => _card.Height;

        /// <summary>Hangs the card at a place a layout has for it; call again whenever the screen changes.</summary>
        public void Place(CardPlace place, ScreenView view) => _card.Place(place, view);

        /// <summary>The card on its way down: 0 is out of sight above the screen, 1 is in its place.</summary>
        public void ComeDown(float progress) => _card.ComeDown(progress);

        /// <summary>The card on its way back up: 0 is in its place, 1 is out of sight.</summary>
        public void GoUp(float progress) => _card.GoUp(progress);

        /// <summary>Shows how far the ad has come: the bar filled so far, and the seconds it still takes.</summary>
        public void Show(IAdShowing ad)
        {
            // The green is as wide as it is high at the beginning, and fills the bar's well, a little in from its ends, at the end.
            float fillHeight = FillScale * FillBadgeHeight;
            float well = BarWidth - 2f * FillInset;
            float width = Mathf.Lerp(fillHeight, well, ad.Progress);
            _fill.size = new Vector2(width / FillScale + FillPictureMargin, FillPictureHeight);
            _fill.transform.localPosition = new Vector3(_barLeft + FillInset + width / 2f, _card.At(BarDown).y, 0f);
            Lettering.Say(_seconds, AdWords.SecondsLeft(ad.SecondsLeft));
        }

        public void Dispose() => _card.Dispose();

        /// <summary>What the ad pays for, in the alcove: the +5 of a game out of moves, or the shop's pile of coins and their number.</summary>
        private void ShowThePrize(AdPrize prize, ArtCatalogue art, int raisedBy)
        {
            switch (prize)
            {
                case AdPrize.ExtraMoves _:
                    Transform moves = SceneObjects.AddSprite("Five more moves", _card.Content, art.Of(GalleryArt.IconFiveMoves), art.SpriteMaterial, DrawOrder.CardExhibit + raisedBy).transform;
                    moves.localPosition = _card.At(AlcoveDown + ExtraMovesUnderMiddle);
                    moves.localScale = new Vector3(ExtraMovesSize / MarkBox, ExtraMovesSize / MarkBox, 1f);
                    break;
                case AdPrize.CoinsInTheShop coins:
                    string words = ShopWords.More(coins.Amount.Amount);
                    TextMeshPro number = Lettering.Write("Coins", _card.Content, art, LetteringStyle.OnTeal, CoinsNumberSize, Color.white, DrawOrder.CardLettering + raisedBy);
                    Lettering.Say(number, words);
                    float numberWidth = Lettering.WidthOf(number, words);
                    float left = -(CoinPileSize + GapPileToNumber + numberWidth) / 2f;
                    Vector3 middle = _card.At(AlcoveDown);
                    Transform pile = SceneObjects.AddSprite("Coins", _card.Content, art.Of(GalleryArt.CoinPile), art.SpriteMaterial, DrawOrder.CardExhibit + raisedBy).transform;
                    pile.localPosition = middle + new Vector3(left + CoinPileSize / 2f, 0f, 0f);
                    pile.localScale = new Vector3(CoinPileSize / MarkBox, CoinPileSize / MarkBox, 1f);
                    number.transform.localPosition = middle + new Vector3(
                        left + CoinPileSize + GapPileToNumber + numberWidth / 2f, -Lettering.FigureDrop(LetteringStyle.OnTeal) * CoinsNumberSize, 0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(prize), prize, "An ad's prize the card cannot show.");
            }
        }
    }
}
