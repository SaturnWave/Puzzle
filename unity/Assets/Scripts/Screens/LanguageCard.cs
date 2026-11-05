#nullable enable
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Words;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card the settings card's row "Dil" opens, in front of it (or, for more languages than stand there, the
    /// thirteen of the first release, by itself in the page's place for a card, over the settings card's dark): the
    /// languages the game offers, each named in its own words ("English", "Türkçe", "한국어", "日本語") whatever the
    /// game speaks, in two columns where they are more than seven, with a green tick at the right of the one it
    /// speaks; a press on a row chooses that language, and the game speaks it at once on every page and card, this one
    /// included. Under the rows the green "Tamam" closes the card, as its round button and the way back do. Where
    /// everything stands is <see cref="LanguageCardLayout"/>'s.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the settings card it hangs in front of,
    /// as the support card is: as wide, its rows parted by the same hairlines, its names in the canvas' interface type.
    /// </remarks>
    public sealed class LanguageCard : IMuseumCard, IDeviceCard
    {
        private readonly HangingCard<MuseumPress> _card;
        private readonly PaintedShape _hairlines;
        private readonly PushButton _done;
        private readonly Dictionary<Language, LanguageRow> _rows = new Dictionary<Language, LanguageRow>();

        private LanguageCard(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy)
        {
            LanguageCardLayout layout = LanguageCardLayout.For(device.Offered.Count);
            HangsByItself = !layout.StandsInFront;
            _card = new HangingCard<MuseumPress>(
                "Language card", parent, art, new Vector2(LanguageCardLayout.BodyWidth, layout.BodyHeight),
                new CardNameplate(SettingsWords.Language, SettingsLayout.NameplateWidth, SettingsLayout.TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            var sketch = new MeshSketch();
            for (int index = 0; index < device.Offered.Count; index++)
            {
                LanguageOffer offer = device.Offered[index];
                var row = new LanguageRow(layout.Rows[index], offer, _card, art, raisedBy);
                _rows.Add(offer.Language, row);
                _card.Listen(row, new MuseumPress.ChooseALanguage(offer.Language));
            }

            foreach (LanguageRowPlace row in layout.HairlinesUnder)
            {
                GiltRule.AddHairline(sketch, row.Left, row.Right, _card.At(row.Bottom + SettingsLayout.Hairline / 2f).y);
            }

            _hairlines = PaintedShape.Create("Hairlines", _card.Content, art.PaintMaterial, DrawOrder.CardArt + raisedBy);
            _hairlines.Show(sketch.ToMesh("Language card hairlines"));

            _done = new PushButton(
                "Done", _card.Content, art, ButtonLook.Green, MuseumWords.Done, LanguageCardLayout.ButtonWidth, LanguageCardLayout.ButtonLabelSize,
                DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _done.Place(_card.At(layout.ButtonMiddle));
            _card.Listen(_done, new MuseumPress.CloseTheCard());
            Tell(device);
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>True for a card of more languages than stand in front of the settings card: it hangs by itself in the page's place for a card, in front of the settings card's dark.</summary>
        public bool HangsByItself { get; }

        /// <summary>The language the card's tick marks: the one the game speaks.</summary>
        public Language Ticked { get; private set; }

        /// <summary>The middle of a language's row, in the layout's points: where a finger presses it. Nothing for a language the card does not offer.</summary>
        public Vector2? RowMiddle(Language language) => _rows.TryGetValue(language, out LanguageRow row) ? _card.InTheLayout(row.Middle) : (Vector2?)null;

        /// <summary>The middle of "Tamam", in the layout's points.</summary>
        public Vector2 DoneButtonMiddle => _card.InTheLayout(_done.Middle);

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static LanguageCard Create(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy) => new LanguageCard(parent, art, device, raisedBy);

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        /// <summary>Ticks the language the game speaks.</summary>
        public void Tell(DeviceView device)
        {
            Ticked = device.Speaking;
            foreach (KeyValuePair<Language, LanguageRow> row in _rows)
            {
                row.Value.ShowTicked(row.Key == device.Speaking);
            }
        }

        public void Dispose()
        {
            _hairlines.Dispose();
            _card.Dispose();
        }

        /// <summary>A language's row: its name at the left, the tick at the right while the game speaks it; the whole row is what a finger presses.</summary>
        private sealed class LanguageRow : IPressable
        {
            private readonly LanguageRowPlace _place;
            private readonly float _middleY;
            private readonly TextMeshPro _name;
            private readonly SpriteRenderer _badge;
            private readonly SpriteRenderer _tick;

            public LanguageRow(LanguageRowPlace place, LanguageOffer offer, HangingCard<MuseumPress> card, ArtCatalogue art, int raisedBy)
            {
                _place = place;
                _middleY = card.At(place.Middle).y;
                float tickMiddle = place.Right - LanguageCardLayout.TickInFromTheEnd;
                _name = Lettering.Write($"{Languages.CodeOf(offer.Language)} name", card.Content, art, LetteringStyle.Label, LanguageCardLayout.NameSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
                float room = tickMiddle - LanguageCardLayout.TickSize / 2f - place.Left - LanguageCardLayout.TickInFromTheEnd / 2f;
                Lettering.SayWithin(_name, offer.OwnName, LanguageCardLayout.NameSize, room);
                Lettering.SayFromLeft(_name, offer.OwnName, place.Left + SettingsLayout.ChevronInFromEnd, _middleY);

                _badge = SceneObjects.AddSprite("Tick badge", card.Content, art.Of(GalleryArt.GreenBadge), art.SpriteMaterial, DrawOrder.CardDisc + raisedBy);
                _badge.transform.localPosition = new Vector3(tickMiddle, _middleY, 0f);
                float badgeScale = LanguageCardLayout.TickSize / CountBadge.Size;
                _badge.transform.localScale = new Vector3(badgeScale, badgeScale, 1f);
                _tick = SceneObjects.AddSprite("Tick", _badge.transform, art.Of(GalleryArt.GlyphTick), art.SpriteMaterial, DrawOrder.CardDisc + raisedBy + (int)DiscPart.Picture);
            }

            /// <summary>The middle of the row, in the card's points around the middle of its body.</summary>
            public Vector2 Middle => new Vector2((_place.Left + _place.Right) / 2f, _middleY);

            public void ShowTicked(bool isTicked)
            {
                _badge.enabled = isTicked;
                _tick.enabled = isTicked;
            }

            /// <param name="point">A point of the card, in its own points around the middle of its body.</param>
            public bool IsUnder(Vector2 point) =>
                point.x >= _place.Left && point.x <= _place.Right && Mathf.Abs(point.y - _middleY) <= _place.Height / 2f;

            public void ShowPressed(bool isPressed) => Lettering.DrawIn(_name, isPressed ? Lettering.SoftInk : Lettering.Ink);
        }
    }
}
