#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card that tells of the player's lives, over a museum page: the heart with the lives that are there on
    /// it, how many of how many those are, and either that every life is there or how long until the next one is
    /// back and until all are. A player without a life is told that a life must come first. It is opened by the
    /// heart's pill of the wallet row, and by the play button of a player without a life, and closed by its round
    /// button, by its "Tamam" and by the way back. No life is sold here: coins do not buy lives in this game.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the cards it does draw (LevelStartTR,
    /// SettingsTR): a cream card that hangs from two pins in the wall, a brass nameplate in the serif, a large mark
    /// with its figure, lines of the interface type, and a green button; it is the owner's to look at and to
    /// redraw. Its sizes are the canvas' points around the middle of its body, and it is drawn at the share of them
    /// the screen's card place gives it (<see cref="PageLayout.PlaceOfACard"/>).
    /// </remarks>
    public sealed class LivesCard : IDisposable
    {
        /// <summary>The body, and the nameplate as wide as the settings card's (SettingsTR) with its title at the level-start card's size.</summary>
        private static readonly Vector2 Body = new Vector2(300f, 330f);
        private const float NameplateWidth = 176f;
        private const float TitleSize = 22f;

        /// <summary>The heart as large as a card's exhibit, its middle this far under the body's top edge; the count on it, as on the wallet row's heart, at twice that size.</summary>
        private const float MarkBox = 100f;
        private const float HeartSize = 92f;
        private const float HeartDown = 80f;
        private const float HeartCountSize = 36f;
        private const float HeartCountOverMiddle = 12f;

        /// <summary>"3 / 5" under the heart, in the figures of a card (LevelStartTR's goals are 34).</summary>
        private const float OfMostSize = 30f;
        private const float OfMostDown = 150f;

        /// <summary>The lines under the figures stand this far apart, as a block in the middle of the wall between the figures and the button.</summary>
        private const float LinesFrom = 170f;
        private const float LinesTo = 246f;
        private const float LinesApart = 22f;
        private const float FirstLineSize = 15f;
        private const float OtherLineSize = 13f;
        private const float LinesInFromSides = 24f;

        /// <summary>The green button, as wide as fits the card with room at each side, its middle this far under the body's top edge.</summary>
        private const float ButtonWidth = 180f;
        private const float ButtonDown = 284f;
        private const float ButtonLabelSize = 28f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly TextMeshPro _heartCount;
        private readonly TextMeshPro _ofMost;
        private readonly List<TextMeshPro> _lines = new List<TextMeshPro>();
        private readonly PushButton _done;

        private LivesCard(Transform parent, ArtCatalogue art, WalletView wallet)
        {
            _card = new HangingCard<MuseumPress>(
                "Lives card", parent, art, Body, new CardNameplate(WalletWords.LivesTitle, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            Transform heart = SceneObjects.AddSprite("Heart", _card.Content, art.Of(GalleryArt.IconHeart), art.SpriteMaterial, DrawOrder.CardExhibit).transform;
            heart.localPosition = _card.At(HeartDown);
            heart.localScale = new Vector3(HeartSize / MarkBox, HeartSize / MarkBox, 1f);
            _heartCount = Lettering.Write("Lives", _card.Content, art, LetteringStyle.OnHeart, HeartCountSize, Color.white, DrawOrder.CardLettering);
            _heartCount.transform.localPosition = _card.At(HeartDown - HeartCountOverMiddle + Lettering.FigureDrop(LetteringStyle.OnHeart) * HeartCountSize);

            _ofMost = Lettering.Write("Lives of most", _card.Content, art, LetteringStyle.Figure, OfMostSize, Lettering.Ink, DrawOrder.CardLettering);
            _ofMost.transform.localPosition = _card.At(OfMostDown + Lettering.FigureDrop(LetteringStyle.Figure) * OfMostSize);

            _lines.Add(Lettering.Write("First line", _card.Content, art, LetteringStyle.Label, FirstLineSize, Lettering.Ink, DrawOrder.CardLettering));
            _lines.Add(Lettering.Write("Second line", _card.Content, art, LetteringStyle.Soft, OtherLineSize, Lettering.SoftInk, DrawOrder.CardLettering));
            _lines.Add(Lettering.Write("Third line", _card.Content, art, LetteringStyle.Soft, OtherLineSize, Lettering.Danger, DrawOrder.CardLettering));

            _done = new PushButton("Done", _card.Content, art, ButtonLook.Green, MuseumWords.Done, ButtonWidth, ButtonLabelSize, DrawOrder.CardArt, DrawOrder.CardLettering);
            _done.Place(_card.At(ButtonDown));
            _card.Listen(_done, new MuseumPress.CloseTheCard());
            Tell(wallet);
        }

        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The middle of the "Tamam" button, in the layout's points: where a finger presses it.</summary>
        public Vector2 DoneButtonMiddle => _card.InTheLayout(_done.Middle);

        /// <summary>Builds the card out of sight above the screen, telling of a wallet. Place it before the first picture.</summary>
        public static LivesCard Create(Transform parent, ArtCatalogue art, WalletView wallet) => new LivesCard(parent, art, wallet);

        /// <summary>Tells of the wallet as it is now: the lives, and the waits that count down while the card hangs.</summary>
        public void Tell(WalletView wallet)
        {
            Lettering.Say(_heartCount, WalletWords.OnTheHeart(wallet));
            Lettering.Say(_ofMost, WalletWords.LivesOfMost(wallet));
            Lettering.DrawIn(_ofMost, wallet.HasALife ? Lettering.Ink : Lettering.Danger);

            var said = new List<string>();
            if (wallet.UntilNextLife is TimeSpan next && wallet.UntilFull is TimeSpan full)
            {
                said.Add(WalletWords.NextLife(next));
                said.Add(WalletWords.FullAfter(full));
                if (!wallet.HasALife)
                {
                    said.Add(WalletWords.PlayWhenALifeComes);
                }
            }
            else
            {
                said.Add(WalletWords.AllLivesThere);
            }

            float firstDown = (LinesFrom + LinesTo) / 2f - (said.Count - 1) * LinesApart / 2f;
            for (int line = 0; line < _lines.Count; line++)
            {
                TextMeshPro written = _lines[line];
                bool isSaid = line < said.Count;
                written.gameObject.SetActive(isSaid);
                if (isSaid)
                {
                    float size = line == 0 ? FirstLineSize : OtherLineSize;
                    Lettering.SayWithin(written, said[line], size, Body.x - 2f * LinesInFromSides);
                    written.transform.localPosition = _card.At(firstDown + line * LinesApart);
                }
            }
        }

        public void Dispose() => _card.Dispose();
    }
}
