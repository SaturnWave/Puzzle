#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card that offers one booster the player holds none of, wherever the plus on its disc was pressed: in front
    /// of the card before a level for a start booster (<see cref="GetMoreCard"/>), over the dimmed board for a booster
    /// used during play. It says the booster's name on its nameplate, shows its disc with the plus, says what it does
    /// and one line more, and has the green "Satın al" with the single price from the settings. When the coins do not
    /// cover the price the button says "Altın yetmiyor" and is dimmed. A press asks for one booster; whoever owns the
    /// wallet buys it by the economy's rules. Closed by its round button and by the way back.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LevelStartTR,
    /// LoseTR's button with a price) and is the owner's to look at and to redraw. Its sizes are the canvas' points
    /// around the middle of its body, and it is drawn at the share of them the screen's card place gives it.
    /// </remarks>
    /// <typeparam name="TPress">What a press on the card asks for.</typeparam>
    public sealed class OfferCard<TPress> : IDisposable
    {
        /// <summary>The body, and the nameplate as wide as the card's before a level, with one line.</summary>
        private static readonly Vector2 Body = new Vector2(300f, 300f);
        private const float NameplateWidth = 212f;
        private const float TitleSize = 22f;

        /// <summary>The booster's disc, larger than on the shelf, its middle this far under the body's top edge.</summary>
        private const float DiscSize = 84f;
        private const float DiscDown = 92f;

        /// <summary>What the booster does, and under it one line more, as a block between the disc and the button.</summary>
        private const float FirstLineDown = 172f;
        private const float SecondLineDown = 196f;
        private const float FirstLineSize = 15f;
        private const float SecondLineSize = 13f;
        private const float LinesInFromSides = 22f;

        /// <summary>The green button with the price, as LoseTR draws the one that sells: its label at 20.</summary>
        private const float ButtonWidth = 200f;
        private const float ButtonDown = 250f;
        private const float ButtonLabelSize = 20f;

        private readonly HangingCard<TPress> _card;
        private readonly PushButton _buy;

        /// <summary>Whether the coins covered the price when the card was last told; nothing before it was.</summary>
        private bool? _isAffordable;

        /// <param name="booster">What is offered: its name, its picture and its record name for the disc.</param>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> for a card in front of another card.</param>
        public OfferCard(
            Transform parent, ArtCatalogue art, BoosterKind booster, string name, BoosterPicture picture, string whatItDoes, string secondLine, Coins price,
            TPress close, TPress buy, int raisedBy)
        {
            _card = new HangingCard<TPress>(
                "Get more card", parent, art, Body, new CardNameplate(name, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(close);

            var disc = new BoosterDisc(booster.Name(), _card.Content, art, picture, DiscSize, DrawOrder.CardDisc + raisedBy, BoosterFace.Of(0, BoosterChoice.Waiting));
            disc.Place(_card.At(DiscDown));

            float room = Body.x - 2f * LinesInFromSides;
            TextMeshPro first = Lettering.Write("What it does", _card.Content, art, LetteringStyle.Label, FirstLineSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(first, whatItDoes, FirstLineSize, room);
            first.transform.localPosition = _card.At(FirstLineDown);
            TextMeshPro second = Lettering.Write("One line more", _card.Content, art, LetteringStyle.Soft, SecondLineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(second, secondLine, SecondLineSize, room);
            second.transform.localPosition = _card.At(SecondLineDown);

            _buy = PushButton.WithAPrice(
                "Buy", _card.Content, art, ButtonLook.Green, LevelStartWords.Buy, WalletWords.Amount(price), ButtonWidth, ButtonLabelSize,
                DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _buy.Place(_card.At(ButtonDown));
            _card.Listen(_buy, buy);
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<TPress> Card => _card;

        /// <summary>The middle of the green button, in the layout's points: where a finger presses it. A dimmed button takes no finger.</summary>
        public Vector2 BuyButtonMiddle => _card.InTheLayout(_buy.Middle);

        /// <summary>Shows whether the coins cover the price now: the button is lit and buys, or is dimmed and says that the coins are short.</summary>
        public void ShowAffordable(bool isAffordable)
        {
            if (_isAffordable == isAffordable)
            {
                return;
            }

            _isAffordable = isAffordable;
            _buy.Say(isAffordable ? LevelStartWords.Buy : LevelStartWords.CoinsShort);
            _buy.Dim(!isAffordable);
        }

        public void Dispose() => _card.Dispose();
    }
}
