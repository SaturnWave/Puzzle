#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The card the settings card's row "Destek" opens, in front of it: what a tester tells when they report a
    /// problem. The player's id, the game's version and the version of its levels, each on a row of its own, and
    /// where the attempt records lie on the phone; under them, a line that says to tell the id and the version, and
    /// the green "Tamam". Closed by "Tamam", by its round button and by the way back. It shows no address to write
    /// to: the owner has given none yet, and the card shows one only once there is one.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the settings card it hangs in front of:
    /// as wide, its rows parted by the same hairlines, its words in the canvas' interface type. Its sizes are the
    /// canvas' points around the middle of its body.
    /// </remarks>
    public sealed class SupportCard : IMuseumCard
    {
        private const float NameplateWidth = SettingsLayout.NameplateWidth;
        private const float TitleSize = SettingsLayout.TitleSize;

        /// <summary>The rows: 44 high from this far under the body's top edge, parted by hairlines; a label at the left, its value at the right.</summary>
        private const float RowsDown = 40f;
        private const float RowHeight = 44f;
        private const float LabelSize = 14.5f;
        private const float ValueSize = 15f;

        /// <summary>The records' file: its label, and the path under it in lines of small type.</summary>
        private const float FileLabelDown = 196f;
        private const float PathTop = 208f;
        private const float PathSize = 11.5f;

        /// <summary>The line that says what to tell, and the green "Tamam", as the cards that tell something have it.</summary>
        private const float WhatToTellDown = 260f;
        private const float WhatToTellSize = 12.5f;
        private const float ButtonWidth = 180f;
        private const float ButtonDown = 318f;
        private const float ButtonLabelSize = 28f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly PaintedShape _hairlines;
        private readonly PushButton _done;

        private SupportCard(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy)
        {
            _card = new HangingCard<MuseumPress>(
                "Support card", parent, art, new Vector2(SettingsLayout.BodyWidth, SettingsLayout.SupportBodyHeight),
                new CardNameplate(SettingsWords.Support, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            var sketch = new MeshSketch();
            (string Label, string Value)[] rows =
            {
                (SettingsWords.PlayerIdLabel, SettingsWords.PlayerId(device.Device)),
                (SettingsWords.GameVersionLabel, device.GameVersion),
                (SettingsWords.ContentVersionLabel, SettingsWords.ContentVersionOf(device.Content)),
            };
            float down = RowsDown;
            foreach ((string label, string value) in rows)
            {
                float middle = _card.At(down + RowHeight / 2f).y;
                Lettering.SayFromLeft(Write("Label", art, LetteringStyle.Soft, LabelSize, Lettering.SoftInk, raisedBy), label, SettingsLayout.RowsLeft, middle);
                Lettering.SayUpToRight(Write("Value", art, LetteringStyle.Label, ValueSize, Lettering.Ink, raisedBy), value, SettingsLayout.RowsRight, middle);
                down += RowHeight;
                GiltRule.AddHairline(sketch, SettingsLayout.RowsLeft, SettingsLayout.RowsRight, _card.At(down + SettingsLayout.Hairline / 2f).y);
                down += SettingsLayout.Hairline;
            }

            _hairlines = PaintedShape.Create("Hairlines", _card.Content, art.PaintMaterial, DrawOrder.CardArt + raisedBy);
            _hairlines.Show(sketch.ToMesh("Support card hairlines"));

            float room = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            Lettering.SayFromLeft(
                Write("File label", art, LetteringStyle.Soft, LabelSize, Lettering.SoftInk, raisedBy), SettingsWords.RecordsFileLabel, SettingsLayout.RowsLeft, _card.At(FileLabelDown).y);
            Lettering.SayInLines(
                Write("Path", art, LetteringStyle.Soft, PathSize, Lettering.Ink, raisedBy), device.RecordsFile ?? SettingsWords.RecordsKeptInMemory, room, SettingsLayout.RowsLeft, _card.At(PathTop).y);

            TextMeshPro whatToTell = Write("What to tell", art, LetteringStyle.Soft, WhatToTellSize, Lettering.SoftInk, raisedBy);
            Lettering.SayWithin(whatToTell, SettingsWords.WhatToTell, WhatToTellSize, room);
            whatToTell.transform.localPosition = _card.At(WhatToTellDown);

            _done = new PushButton("Done", _card.Content, art, ButtonLook.Green, MuseumWords.Done, ButtonWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _done.Place(_card.At(ButtonDown));
            _card.Listen(_done, new MuseumPress.CloseTheCard());
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The middle of "Tamam", in the layout's points: where a finger presses it.</summary>
        public Vector2 DoneButtonMiddle => _card.InTheLayout(_done.Middle);

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static SupportCard Create(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy) => new SupportCard(parent, art, device, raisedBy);

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose()
        {
            _hairlines.Dispose();
            _card.Dispose();
        }

        private TextMeshPro Write(string name, ArtCatalogue art, LetteringStyle style, float size, Color colour, int raisedBy) =>
            Lettering.Write(name, _card.Content, art, style, size, colour, DrawOrder.CardLettering + raisedBy);
    }
}
