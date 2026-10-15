#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Meta;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card that offers one start booster the player holds none of, in front of the card before a level, where
    /// the plus on its disc was pressed (<see cref="OfferCard{TPress}"/>): the booster's name, its disc with the plus,
    /// what it does to the level, that a booster is chosen only once it is held, and the green "Satın al" with the
    /// single price from the settings. A press asks for one booster (<see cref="MuseumPress.BuyABooster"/>); whoever
    /// owns the wallet buys it by the economy's rules, and the card before the level then counts it.
    /// </summary>
    public sealed class GetMoreCard : IMuseumCard
    {
        private readonly OfferCard<MuseumPress> _offer;
        private readonly LevelBrief _brief;
        private readonly BoosterKind _booster;

        private GetMoreCard(Transform parent, ArtCatalogue art, BoosterKind booster, LevelBrief brief, WalletView wallet, int raisedBy)
        {
            _brief = brief;
            _booster = booster;
            _offer = new OfferCard<MuseumPress>(
                parent, art, booster, LevelStartWords.NameOf(booster, brief.ExtraMoves), StartBoosterPictures.Of(booster),
                LevelStartWords.WhatItDoes(booster, brief.ExtraMoves), LevelStartWords.BuyToChoose, brief.PriceOf(booster),
                new MuseumPress.CloseTheCard(), new MuseumPress.BuyABooster(booster), raisedBy);
            Tell(wallet);
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<MuseumPress> Card => _offer.Card;

        /// <summary>The booster the card offers.</summary>
        public BoosterKind Booster => _booster;

        /// <summary>The middle of the green button, in the layout's points: where a finger presses it. A dimmed button takes no finger.</summary>
        public Vector2 BuyButtonMiddle => _offer.BuyButtonMiddle;

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/>, since it hangs in front of the card before a level.</param>
        public static GetMoreCard Create(Transform parent, ArtCatalogue art, BoosterKind booster, LevelBrief brief, WalletView wallet, int raisedBy) =>
            new GetMoreCard(parent, art, booster, brief, wallet, raisedBy);

        /// <summary>Shows whether the coins cover the price now: the button is lit and buys, or is dimmed and says that the coins are short.</summary>
        public void Tell(WalletView wallet) => _offer.ShowAffordable(_brief.CanBuy(_booster, wallet.Coins));

        public void Dispose() => _offer.Dispose();
    }
}
