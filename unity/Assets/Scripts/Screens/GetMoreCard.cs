#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Meta;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card that offers one start booster the player holds none of, in front of the card before a level, where
    /// the plus on its disc was pressed: the booster's name on the nameplate, its disc with the plus, what it does to
    /// the level, that a booster is chosen only once it is held, and the green "Satın al" with the single price from
    /// the settings. When the coins do not cover the price the button says "Altın yetmiyor" and is dimmed. A press
    /// asks for one booster (<see cref="MuseumPress.BuyABooster"/>); whoever owns the wallet buys it by the economy's
    /// rules, and the card before the level then counts it. Closed by its round button and by the way back.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LevelStartTR,
    /// LoseTR's button with a price) and is the owner's to look at and to redraw. Its sizes are the canvas' points
    /// around the middle of its body, and it is drawn at the share of them the screen's card place gives it.
    /// </remarks>
    public sealed class GetMoreCard : IMuseumCard
    {
        /// <summary>The body, and the nameplate as wide as the card's before a level, with one line.</summary>
        private static readonly Vector2 Body = new Vector2(300f, 300f);
        private const float NameplateWidth = 212f;
        private const float TitleSize = 22f;

        /// <summary>The booster's disc, larger than on the shelf, its middle this far under the body's top edge.</summary>
        private const float DiscSize = 84f;
        private const float DiscDown = 92f;

        /// <summary>What the booster does, and under it that it is chosen once held, as a block between the disc and the button.</summary>
        private const float FirstLineDown = 172f;
        private const float SecondLineDown = 196f;
        private const float FirstLineSize = 15f;
        private const float SecondLineSize = 13f;
        private const float LinesInFromSides = 22f;

        /// <summary>The green button with the price, as LoseTR draws the one that sells: its label at 20.</summary>
        private const float ButtonWidth = 200f;
        private const float ButtonDown = 250f;
        private const float ButtonLabelSize = 20f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly LevelBrief _brief;
        private readonly BoosterKind _booster;
        private readonly PushButton _buy;

        /// <summary>Whether the coins covered the price when the card was last told; nothing before it was.</summary>
        private bool? _isAffordable;

        private GetMoreCard(Transform parent, ArtCatalogue art, BoosterKind booster, LevelBrief brief, WalletView wallet, int raisedBy)
        {
            _brief = brief;
            _booster = booster;
            string name = LevelStartWords.NameOf(booster, brief.ExtraMoves);
            _card = new HangingCard<MuseumPress>(
                "Get more card", parent, art, Body, new CardNameplate(name, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            var disc = new BoosterDisc(booster.Name(), _card.Content, art, StartBoosterPictures.Of(booster), DiscSize, DrawOrder.CardDisc + raisedBy, BoosterFace.Of(0, BoosterChoice.Waiting));
            disc.Place(_card.At(DiscDown));

            float room = Body.x - 2f * LinesInFromSides;
            TextMeshPro first = Lettering.Write("What it does", _card.Content, art, LetteringStyle.Label, FirstLineSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(first, LevelStartWords.WhatItDoes(booster, brief.ExtraMoves), FirstLineSize, room);
            first.transform.localPosition = _card.At(FirstLineDown);
            TextMeshPro second = Lettering.Write("Chosen once held", _card.Content, art, LetteringStyle.Soft, SecondLineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
            Lettering.SayWithin(second, LevelStartWords.BuyToChoose, SecondLineSize, room);
            second.transform.localPosition = _card.At(SecondLineDown);

            _buy = PushButton.WithAPrice(
                "Buy", _card.Content, art, ButtonLook.Green, LevelStartWords.Buy, WalletWords.Amount(brief.PriceOf(booster)), ButtonWidth, ButtonLabelSize,
                DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _buy.Place(_card.At(ButtonDown));
            _card.Listen(_buy, new MuseumPress.BuyABooster(booster));
            Tell(wallet);
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The booster the card offers.</summary>
        public BoosterKind Booster => _booster;

        /// <summary>The middle of the green button, in the layout's points: where a finger presses it. A dimmed button takes no finger.</summary>
        public Vector2 BuyButtonMiddle => _card.InTheLayout(_buy.Middle);

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/>, since it hangs in front of the card before a level.</param>
        public static GetMoreCard Create(Transform parent, ArtCatalogue art, BoosterKind booster, LevelBrief brief, WalletView wallet, int raisedBy) =>
            new GetMoreCard(parent, art, booster, brief, wallet, raisedBy);

        /// <summary>Shows whether the coins cover the price now: the button is lit and buys, or is dimmed and says that the coins are short.</summary>
        public void Tell(WalletView wallet)
        {
            bool isAffordable = _brief.CanBuy(_booster, wallet.Coins);
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
