#nullable enable
using System;
using System.Collections.Generic;
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
    /// The card the settings card's row "Gizlilik ve veri" opens, in front of it: what the game keeps on the phone and
    /// that this build sends none of it anywhere (<see cref="PrivacyWords.Text"/>, the owner's draft), and under a
    /// hairline how many attempt records the phone keeps, the file they are in, and "Kayıtları sil". That button asks
    /// first, on the card itself: the count's place says how many records would go and what stays, and the buttons
    /// are "Vazgeç" and "Sil". Closed by its round button and by the way back, which also take a question back.
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the settings card it hangs in front of:
    /// as wide, its text in the canvas' interface type, its buttons the small ones of the shop. Its sizes are the
    /// canvas' points around the middle of its body. The text may be longer or shorter once the owner has read it:
    /// the card is as high as its text needs, up to the highest card that may hang in front of the settings card
    /// (<see cref="SettingsLayout.PrivacyBodyHeight"/>), and a text longer than that is set smaller, never cut off.
    /// </remarks>
    public sealed class PrivacyCard : IMuseumCard, IDeviceCard
    {
        private const float NameplateWidth = 236f;
        private const float TitleSize = 22f;

        /// <summary>The text begins this far under the body's top edge; its paragraphs stand this far apart.</summary>
        private const float TextTop = 42f;
        private const float ParagraphGap = 7f;
        private const float TextSize = 13f;

        /// <summary>A text too long for the highest card is set smaller by this step, down to this size at the least.</summary>
        private const float SmallerStep = 0.94f;
        private const float SmallestTextSize = 10.5f;

        /// <summary>Under the text, as far under its end: the hairline over the records, the count and the line under it, the buttons' middles, and the card's lower edge.</summary>
        private const float HairlineUnderText = 12f;
        private const float CountUnderText = 34f;
        private const float CountSize = 15f;
        private const float LineUnderText = 55f;
        private const float LineSize = 11.5f;
        private const float ButtonsUnderText = 98f;
        private const float BodyUnderText = 142f;

        /// <summary>The most the text may take: the card is then as high as the settings layout lets a card in front be.</summary>
        private const float MostTextHeight = SettingsLayout.PrivacyBodyHeight - TextTop - BodyUnderText;

        /// <summary>"Kayıtları sil" alone in the middle; the question's two buttons side by side.</summary>
        private const float DeleteWidth = 170f;
        private const float AnswerWidth = 136f;
        private const float AnswersApart = 152f;
        private const float ButtonLabelSize = 16f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly PaintedShape _hairline;
        private readonly TextMeshPro _count;
        private readonly TextMeshPro _line;
        private readonly SmallButton _delete;
        private readonly SmallButton _keep;
        private readonly SmallButton _confirm;
        private DeviceView _device;

        /// <summary>True once the records were deleted while the card hangs: the line under the count says so.</summary>
        private bool _hasDeleted;

        private PrivacyCard(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy)
        {
            _device = device;

            // The text is set first, beside the card, to know how high the card must be to hold it.
            IReadOnlyList<TextMeshPro> paragraphs = WriteTheText(parent, art, raisedBy, out float textHeight);
            float textEnd = TextTop + textHeight;
            _card = new HangingCard<MuseumPress>(
                "Privacy card", parent, art, new Vector2(SettingsLayout.BodyWidth, textEnd + BodyUnderText),
                new CardNameplate(PrivacyWords.Title, NameplateWidth, TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());
            foreach (TextMeshPro paragraph in paragraphs)
            {
                paragraph.transform.SetParent(_card.Content, worldPositionStays: false);
            }

            Lay(paragraphs, down => _card.At(down).y);

            _hairline = PaintedShape.Create("Hairline", _card.Content, art.PaintMaterial, DrawOrder.CardArt + raisedBy);
            var sketch = new MeshSketch();
            GiltRule.AddHairline(sketch, SettingsLayout.RowsLeft, SettingsLayout.RowsRight, _card.At(textEnd + HairlineUnderText).y);
            _hairline.Show(sketch.ToMesh("Privacy card hairline"));

            _count = Lettering.Write("Count", _card.Content, art, LetteringStyle.Label, CountSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy);
            _count.transform.localPosition = _card.At(textEnd + CountUnderText);
            _line = Lettering.Write("Line", _card.Content, art, LetteringStyle.Soft, LineSize, Lettering.SoftInk, DrawOrder.CardLettering + raisedBy);
            _line.transform.localPosition = _card.At(textEnd + LineUnderText);

            Vector3 buttons = _card.At(textEnd + ButtonsUnderText);
            _delete = new SmallButton("Delete", _card.Content, art, SmallButtonColour.Blue, PrivacyWords.DeleteTheRecords, DeleteWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _delete.Place(buttons);
            _keep = new SmallButton("Keep", _card.Content, art, SmallButtonColour.Blue, PrivacyWords.KeepThem, AnswerWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _keep.Place(buttons + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _confirm = new SmallButton("Confirm", _card.Content, art, SmallButtonColour.Green, PrivacyWords.Delete, AnswerWidth, ButtonLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _confirm.Place(buttons + new Vector3(AnswersApart / 2f, 0f, 0f));
            ShowTheRecords();
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>True while the card asks whether the records are to be deleted.</summary>
        public bool AsksTheQuestion { get; private set; }

        /// <summary>What the count says now.</summary>
        public string CountSays => _count.text;

        /// <summary>The middle of "Kayıtları sil", in the layout's points; nothing while the question is asked, or while there is no record to delete.</summary>
        public Vector2? DeleteButtonMiddle => !AsksTheQuestion && _device.HasRecords ? _card.InTheLayout(_delete.Middle) : (Vector2?)null;

        /// <summary>The middles of the question's "Vazgeç" and "Sil", in the layout's points; nothing while no question is asked.</summary>
        public Vector2? KeepButtonMiddle => AsksTheQuestion ? _card.InTheLayout(_keep.Middle) : (Vector2?)null;

        public Vector2? ConfirmButtonMiddle => AsksTheQuestion ? _card.InTheLayout(_confirm.Middle) : (Vector2?)null;

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static PrivacyCard Create(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy) => new PrivacyCard(parent, art, device, raisedBy);

        /// <summary>"Kayıtları sil" was pressed: the card asks whether every record is to go.</summary>
        public void Ask()
        {
            AsksTheQuestion = true;
            ShowTheRecords();
        }

        /// <summary>"Vazgeç" was pressed, or the records were deleted: the card is as it was before the question.</summary>
        public void TakeTheQuestionBack()
        {
            AsksTheQuestion = false;
            ShowTheRecords();
        }

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        /// <summary>Shows the records as the phone keeps them now: after a deletion, none, and no question.</summary>
        public void Tell(DeviceView device)
        {
            bool countChanged = device.RecordsKept != _device.RecordsKept;
            _device = device;
            if (countChanged)
            {
                _hasDeleted = device.RecordsKept == 0;
                AsksTheQuestion = false;
                ShowTheRecords();
            }
        }

        public void Dispose()
        {
            _hairline.Dispose();
            _card.Dispose();
        }

        /// <summary>The count and the line under it, and the buttons of the state the card is in; what is not in sight is not pressed.</summary>
        private void ShowTheRecords()
        {
            float room = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            int? kept = _device.RecordsKept;
            if (AsksTheQuestion && kept is int count)
            {
                Lettering.SayWithin(_count, PrivacyWords.Question(count), CountSize, room);
                Lettering.SayWithin(_line, PrivacyWords.WhatStays, LineSize, room);
            }
            else
            {
                Lettering.SayWithin(_count, kept is int counted ? PrivacyWords.RecordsKept(counted) : PrivacyWords.RecordsUncounted, CountSize, room);
                string line = kept == null ? PrivacyWords.CannotCount : _hasDeleted ? PrivacyWords.Deleted : PrivacyWords.FileLine(AttemptLogFile.FileName);
                Lettering.SayWithin(_line, line, LineSize, room);
            }

            _delete.Show(!AsksTheQuestion);
            _delete.Dim(!_device.HasRecords);
            _keep.Show(AsksTheQuestion);
            _confirm.Show(AsksTheQuestion);

            _card.ForgetTheControls();
            if (AsksTheQuestion)
            {
                _card.Listen(_keep, new MuseumPress.KeepTheRecords());
                _card.Listen(_confirm, new MuseumPress.DeleteTheRecords());
            }
            else
            {
                _card.Listen(_delete, new MuseumPress.AskToDeleteTheRecords());
            }
        }

        /// <summary>
        /// The draft text, a paragraph at a time, broken to the width of the rows' room: set beside the card, under
        /// what the card will hang under, and smaller where it would make the card higher than a card in front may be.
        /// </summary>
        /// <param name="textHeight">How high the text is, from the top of its first line to the foot of its last.</param>
        private static IReadOnlyList<TextMeshPro> WriteTheText(Transform beside, ArtCatalogue art, int raisedBy, out float textHeight)
        {
            var paragraphs = new List<TextMeshPro>();
            for (int index = 0; index < PrivacyWords.Text.Count; index++)
            {
                paragraphs.Add(Lettering.Write($"Paragraph {index + 1}", beside, art, LetteringStyle.Soft, TextSize, Lettering.Ink, DrawOrder.CardLettering + raisedBy));
            }

            float size = TextSize;
            foreach (TextMeshPro paragraph in paragraphs)
            {
                paragraph.fontSize = size;
            }

            textHeight = Lay(paragraphs, down => -down);
            while (textHeight > MostTextHeight && size * SmallerStep >= SmallestTextSize)
            {
                size *= SmallerStep;
                foreach (TextMeshPro paragraph in paragraphs)
                {
                    paragraph.fontSize = size;
                }

                textHeight = Lay(paragraphs, down => -down);
            }

            return paragraphs;
        }

        /// <summary>Lays the paragraphs one under the other from the text's top, at their type size.</summary>
        /// <param name="heightOf">Where a point that far under the body's top edge stands on what the paragraphs stand on.</param>
        /// <returns>How high the text is.</returns>
        private static float Lay(IReadOnlyList<TextMeshPro> paragraphs, Func<float, float> heightOf)
        {
            float width = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            float down = TextTop;
            for (int index = 0; index < paragraphs.Count; index++)
            {
                float height = Lettering.SayInLines(paragraphs[index], PrivacyWords.Text[index], width, SettingsLayout.RowsLeft, heightOf(down));
                down += height + ParagraphGap;
            }

            return down - ParagraphGap - TextTop;
        }
    }
}
