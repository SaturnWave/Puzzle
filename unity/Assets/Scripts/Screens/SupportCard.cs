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
    /// where the attempt records lie on the phone; under them, a line that says to tell the id and the version, the
    /// blue "Künye", which opens the credits card in its place (it had a row of its own on the settings card until the
    /// account took that place, 2026-10-27), and the green "Tamam". Closed by "Tamam", by its round button and by the way back. It shows no address to write
    /// to: the owner has given none yet, and the card shows one only once there is one.
    /// <para>
    /// In a test build with the tester mode off (<see cref="TesterMode"/>), the version row is also a hidden door:
    /// seven taps on it in a row, with no other press between them, make the card ask, in the place of its line and
    /// its "Tamam", whether the tester mode is to be turned on: "Vazgeç" takes the question back, "Aç" turns it on.
    /// The row shows nothing while it is held, and a build without the tester mode, or with it on, does not listen to it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built in the language of the settings card it hangs in front of:
    /// as wide, its rows parted by the same hairlines, its words in the canvas' interface type. Its sizes are the
    /// canvas' points around the middle of its body. The question asks on the card itself, as the privacy card asks
    /// before it deletes, with the same small buttons.
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

        /// <summary>The row of the game's version, counted from 0 at the top: the door to the tester mode.</summary>
        private const int VersionRow = 1;

        /// <summary>The records' file: its label, and the path under it in lines of small type.</summary>
        private const float FileLabelDown = 196f;
        private const float PathTop = 208f;
        private const float PathSize = 11.5f;

        /// <summary>The line that says what to tell, and under it the blue "Künye" and the green "Tamam" side by side, as the question's answers.</summary>
        private const float WhatToTellDown = 260f;
        private const float WhatToTellSize = 12.5f;
        private const float ButtonDown = 318f;

        /// <summary>The question in the place of that line, the line under it, and its two answers side by side, as the privacy card's.</summary>
        private const float QuestionDown = 254f;
        private const float QuestionSize = 15f;
        private const float QuestionLineDown = 276f;
        private const float AnswerWidth = 136f;
        private const float AnswersApart = 152f;
        private const float AnswerLabelSize = 16f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly PaintedShape _hairlines;
        private readonly TextMeshPro _whatToTell;
        private readonly SmallButton _credits;
        private readonly SmallButton _done;
        private readonly TextMeshPro _question;
        private readonly TextMeshPro _questionLine;
        private readonly SmallButton _leaveOff;
        private readonly SmallButton _turnOn;

        /// <summary>The version row as a press, where the build lets it open the door; nothing otherwise.</summary>
        private readonly RowArea? _versionRow;

        /// <summary>The taps on the version row in a row since the card came, or since the question was taken back.</summary>
        private int _taps;

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
            for (int row = 0; row < rows.Length; row++)
            {
                float middle = _card.At(down + RowHeight / 2f).y;
                Lettering.SayFromLeft(Write("Label", art, LetteringStyle.Soft, LabelSize, Lettering.SoftInk, raisedBy), rows[row].Label, SettingsLayout.RowsLeft, middle);
                Lettering.SayUpToRight(Write("Value", art, LetteringStyle.Label, ValueSize, Lettering.Ink, raisedBy), rows[row].Value, SettingsLayout.RowsRight, middle);
                if (row == VersionRow && device.CanTurnOnTheTesterMode)
                {
                    _versionRow = new RowArea(middle);
                }

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

            _whatToTell = Write("What to tell", art, LetteringStyle.Soft, WhatToTellSize, Lettering.SoftInk, raisedBy);
            Lettering.SayWithin(_whatToTell, SettingsWords.WhatToTell, WhatToTellSize, room);
            _whatToTell.transform.localPosition = _card.At(WhatToTellDown);

            Vector3 buttons = _card.At(ButtonDown);
            _credits = new SmallButton(
                "Credits", _card.Content, art, SmallButtonColour.Blue, CreditWords.Title, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _credits.Place(buttons + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _done = new SmallButton(
                "Done", _card.Content, art, SmallButtonColour.Green, MuseumWords.Done, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _done.Place(buttons + new Vector3(AnswersApart / 2f, 0f, 0f));

            _question = Write("Question", art, LetteringStyle.Label, QuestionSize, Lettering.Ink, raisedBy);
            Lettering.SayWithin(_question, SettingsWords.TesterModeQuestion, QuestionSize, room);
            _question.transform.localPosition = _card.At(QuestionDown);
            _questionLine = Write("Question line", art, LetteringStyle.Soft, WhatToTellSize, Lettering.SoftInk, raisedBy);
            Lettering.SayWithin(_questionLine, SettingsWords.TesterModeTells, WhatToTellSize, room);
            _questionLine.transform.localPosition = _card.At(QuestionLineDown);

            Vector3 answers = _card.At(ButtonDown);
            _leaveOff = new SmallButton(
                "Leave off", _card.Content, art, SmallButtonColour.Blue, SettingsWords.LeaveTesterModeOff, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _leaveOff.Place(answers + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _turnOn = new SmallButton(
                "Turn on", _card.Content, art, SmallButtonColour.Green, SettingsWords.TurnTesterModeOn, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _turnOn.Place(answers + new Vector3(AnswersApart / 2f, 0f, 0f));
            ShowWhatIsAsked();
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>True while the card asks whether the tester mode is to be turned on.</summary>
        public bool AsksAboutTheTesterMode { get; private set; }

        /// <summary>The middle of "Tamam", in the layout's points: where a finger presses it. Nothing while the question is asked.</summary>
        public Vector2? DoneButtonMiddle => AsksAboutTheTesterMode ? (Vector2?)null : _card.InTheLayout(_done.Middle);

        /// <summary>The middle of "Künye", in the layout's points; nothing while the question is asked.</summary>
        public Vector2? CreditsButtonMiddle => AsksAboutTheTesterMode ? (Vector2?)null : _card.InTheLayout(_credits.Middle);

        /// <summary>The middle of the version row, in the layout's points; nothing where it opens no door, or while the question is asked.</summary>
        public Vector2? VersionRowMiddle => _versionRow != null && !AsksAboutTheTesterMode ? _card.InTheLayout(_versionRow.Middle) : (Vector2?)null;

        /// <summary>The middles of the question's "Vazgeç" and "Aç", in the layout's points; nothing while no question is asked.</summary>
        public Vector2? LeaveOffButtonMiddle => AsksAboutTheTesterMode ? _card.InTheLayout(_leaveOff.Middle) : (Vector2?)null;

        public Vector2? TurnOnButtonMiddle => AsksAboutTheTesterMode ? _card.InTheLayout(_turnOn.Middle) : (Vector2?)null;

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static SupportCard Create(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy) => new SupportCard(parent, art, device, raisedBy);

        /// <summary>The version row was tapped: the seventh tap in a row makes the card ask.</summary>
        public void CountATap()
        {
            _taps++;
            if (_taps >= TesterMode.TapsToAsk)
            {
                AsksAboutTheTesterMode = true;
                ShowWhatIsAsked();
            }
        }

        /// <summary>"Vazgeç" was pressed: the card is as it came, and the taps are counted from none again.</summary>
        public void TakeTheQuestionBack()
        {
            _taps = 0;
            AsksAboutTheTesterMode = false;
            ShowWhatIsAsked();
        }

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        public void Dispose()
        {
            _hairlines.Dispose();
            _card.Dispose();
        }

        /// <summary>The line and "Tamam", or the question and its answers; what is not in sight is not pressed.</summary>
        private void ShowWhatIsAsked()
        {
            bool asks = AsksAboutTheTesterMode;
            _whatToTell.gameObject.SetActive(!asks);
            _done.Show(!asks);
            _credits.Show(!asks);
            _question.gameObject.SetActive(asks);
            _questionLine.gameObject.SetActive(asks);
            _leaveOff.Show(asks);
            _turnOn.Show(asks);

            _card.ForgetTheControls();
            if (asks)
            {
                _card.Listen(_leaveOff, new MuseumPress.LeaveTheTesterModeOff());
                _card.Listen(_turnOn, new MuseumPress.TurnOnTheTesterMode());
                return;
            }

            _card.Listen(_done, new MuseumPress.CloseTheCard());
            _card.Listen(_credits, new MuseumPress.OpenTheCredits());
            if (_versionRow != null)
            {
                _card.Listen(_versionRow, new MuseumPress.TapTheVersion());
            }
        }

        private TextMeshPro Write(string name, ArtCatalogue art, LetteringStyle style, float size, Color colour, int raisedBy) =>
            Lettering.Write(name, _card.Content, art, style, size, colour, DrawOrder.CardLettering + raisedBy);

        /// <summary>A row of the card as a place a finger presses: as wide as the rows, as high as one. It shows nothing while it is held.</summary>
        private sealed class RowArea : IPressable
        {
            private readonly float _middleY;

            /// <param name="middleY">The height of the row's middle, in the card's points around the middle of its body.</param>
            public RowArea(float middleY)
            {
                _middleY = middleY;
            }

            /// <summary>The middle of the row, in the card's points.</summary>
            public Vector2 Middle => new Vector2((SettingsLayout.RowsLeft + SettingsLayout.RowsRight) / 2f, _middleY);

            /// <param name="point">A point of the card, in its own points around the middle of its body.</param>
            public bool IsUnder(Vector2 point) =>
                point.x >= SettingsLayout.RowsLeft && point.x <= SettingsLayout.RowsRight && Mathf.Abs(point.y - _middleY) <= RowHeight / 2f;

            public void ShowPressed(bool isPressed)
            {
            }
        }
    }
}
