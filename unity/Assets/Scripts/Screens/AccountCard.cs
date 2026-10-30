#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Cloud;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>What the account card asks or tells under its rows. A closed set.</summary>
    public enum AccountCardAsks
    {
        /// <summary>The line on what the cloud save keeps, "Verilerimi sil" and "Tamam".</summary>
        Nothing,

        /// <summary>Whether to delete the account and its save in the cloud: "Vazgeç" and "Sil".</summary>
        AboutTheCloud,

        /// <summary>The deletion runs: no answer.</summary>
        Deleting,

        /// <summary>The cloud's data is deleted (or there was none): whether to delete this phone's progress too, "Kalsın" and "Sil".</summary>
        AboutThePhone,

        /// <summary>What a deletion came to, and "Tamam".</summary>
        Tells,
    }

    /// <summary>
    /// The card the settings card's row "Hesap ve bulut kaydı" opens, in front of it (docs/research/server-v1.md §5.7):
    /// where the account stands ("Bu telefonun hesabı", "Google Play Games ile bağlı", or the green "Bağlan" where an
    /// account of this phone can be linked), the "Bulut kaydı" switch, the last sync, and the player code, which a touch
    /// copies; under them the line on what the cloud save keeps, the blue "Verilerimi sil" and the green "Tamam". Closed
    /// by "Tamam", by its round button and by the way back.
    /// <para>
    /// "Verilerimi sil" asks on the card itself, as the privacy card asks before it deletes the records: whether to
    /// delete the account and its save in the cloud ("Vazgeç", "Sil"), with the line on the anonymous trace kept for 36
    /// months. "Sil" deletes it in the background while the card says so; then the card asks whether to delete the
    /// progress on this phone too ("Kalsın", "Sil"), or tells why nothing was deleted. The card shows the account as the
    /// cloud tells it (<see cref="Tell(DeviceView)"/>), at every change.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The design canvas draws no such card. It is built as the support card is, in the language of the settings card it
    /// hangs in front of: as wide, its rows parted by the same hairlines, its words in the canvas' interface type, its
    /// switch the settings card's. Its sizes are the canvas' points around the middle of its body.
    /// </remarks>
    public sealed class AccountCard : IMuseumCard, IDeviceCard
    {
        /// <summary>The rows: 44 high from this far under the body's top edge, parted by hairlines; a label at the left, its value at the right.</summary>
        private const float RowsDown = 40f;
        private const float RowHeight = 44f;
        private const float LabelSize = 14.5f;
        private const float ValueSize = 15f;
        private const float CodeSize = 12.5f;
        private const float ValueAfterLabel = 12f;
        private const int Rows = 4;

        /// <summary>The line on the cloud save, in lines of small type from this far down.</summary>
        private const float LineTop = 232f;
        private const float LineSize = 12.5f;

        /// <summary>A question in the place of that line, the line above or under it, and the answers side by side.</summary>
        private const float HeadingDown = 238f;
        private const float QuestionDown = 262f;
        private const float QuestionAloneDown = 250f;
        private const float QuestionSize = 15f;
        private const float UnderTheQuestionDown = 285f;
        private const float AnswersDown = 326f;
        private const float AnswerWidth = 146f;
        private const float AnswersApart = 160f;
        private const float AnswerLabelSize = 16f;

        /// <summary>The green "Bağlan" in the account row, at its right end.</summary>
        private const float LinkWidth = 104f;
        private const float LinkLabelSize = 15f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly PaintedShape _hairlines;
        private readonly float[] _rowMiddles = new float[Rows];
        private readonly TextMeshPro[] _labels = new TextMeshPro[Rows];
        private readonly TextMeshPro _accountValue;
        private readonly SmallButton _link;
        private readonly Toggle _switch;
        private readonly TextMeshPro _switchMissing;
        private readonly TextMeshPro _lastSync;
        private readonly TextMeshPro _code;
        private readonly RowArea _codeRow;
        private readonly TextMeshPro _line;
        private readonly TextMeshPro _heading;
        private readonly TextMeshPro _question;
        private readonly TextMeshPro _underTheQuestion;
        private readonly SmallButton _leftAnswer;
        private readonly SmallButton _rightAnswer;
        private readonly SmallButton _done;
        private DeviceView _device;

        private AccountCard(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy)
        {
            _device = device;
            _card = new HangingCard<MuseumPress>(
                "Account card", parent, art, new Vector2(SettingsLayout.BodyWidth, SettingsLayout.SupportBodyHeight),
                new CardNameplate(AccountWords.CardTitle, SettingsLayout.NameplateWidth, SettingsLayout.TitleSize), CardHanging.FromPinsInTheWall, raisedBy: raisedBy);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            var sketch = new MeshSketch();
            string[] labels = { AccountWords.CardTitle, AccountWords.CloudSaveRow, AccountWords.LastSyncRow, AccountWords.PlayerCodeRow };
            float down = RowsDown;
            for (int row = 0; row < Rows; row++)
            {
                _rowMiddles[row] = _card.At(down + RowHeight / 2f).y;
                _labels[row] = Write("Label", art, LetteringStyle.Soft, LabelSize, Lettering.SoftInk, raisedBy);
                Lettering.SayFromLeft(_labels[row], labels[row], SettingsLayout.RowsLeft, _rowMiddles[row]);
                down += RowHeight;
                GiltRule.AddHairline(sketch, SettingsLayout.RowsLeft, SettingsLayout.RowsRight, _card.At(down + SettingsLayout.Hairline / 2f).y);
                down += SettingsLayout.Hairline;
            }

            _hairlines = PaintedShape.Create("Hairlines", _card.Content, art.PaintMaterial, DrawOrder.CardArt + raisedBy);
            _hairlines.Show(sketch.ToMesh("Account card hairlines"));

            _accountValue = Write("Account", art, LetteringStyle.Label, ValueSize, Lettering.Ink, raisedBy);
            _link = new SmallButton(
                "Link", _card.Content, art, SmallButtonColour.Green, AccountWords.Link, LinkWidth, LinkLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _link.Place(new Vector2(SettingsLayout.RowsRight - LinkWidth / 2f, _rowMiddles[0]));
            _switch = new Toggle("Cloud save switch", _card.Content, art, device.Account.IsSwitchedOn, DrawOrder.CardDisc + raisedBy);
            _switch.Place(new Vector2(SettingsLayout.SwitchMiddle, _rowMiddles[1]));
            _switchMissing = Write("No switch", art, LetteringStyle.Label, ValueSize, Lettering.Ink, raisedBy);
            _lastSync = Write("Last sync", art, LetteringStyle.Label, ValueSize, Lettering.Ink, raisedBy);
            _code = Write("Code", art, LetteringStyle.Label, CodeSize, Lettering.Ink, raisedBy);
            _codeRow = new RowArea(_rowMiddles[3]);

            _line = Write("Line", art, LetteringStyle.Soft, LineSize, Lettering.SoftInk, raisedBy);
            _heading = Write("Heading", art, LetteringStyle.Soft, LineSize, Lettering.SoftInk, raisedBy);
            _question = Write("Question", art, LetteringStyle.Label, QuestionSize, Lettering.Ink, raisedBy);
            _underTheQuestion = Write("Under the question", art, LetteringStyle.Soft, LineSize, Lettering.SoftInk, raisedBy);

            Vector3 answers = _card.At(AnswersDown);
            _leftAnswer = new SmallButton(
                "Left answer", _card.Content, art, SmallButtonColour.Blue, AccountWords.DeleteMyData, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _leftAnswer.Place(answers + new Vector3(-AnswersApart / 2f, 0f, 0f));
            _rightAnswer = new SmallButton(
                "Right answer", _card.Content, art, SmallButtonColour.Green, MuseumWords.Done, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _rightAnswer.Place(answers + new Vector3(AnswersApart / 2f, 0f, 0f));
            _done = new SmallButton(
                "Done", _card.Content, art, SmallButtonColour.Green, MuseumWords.Done, AnswerWidth, AnswerLabelSize, DrawOrder.CardArt + raisedBy, DrawOrder.CardLettering + raisedBy);
            _done.Place(answers);

            ShowTheAccount();
            Ask(AccountCardAsks.Nothing);
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>What the card asks or tells under its rows now.</summary>
        public AccountCardAsks Asks { get; private set; }

        /// <summary>What the card says under its rows: the line, the question, or what came of the deletion; for the tools.</summary>
        public string Says => Asks == AccountCardAsks.Nothing ? _line.text : $"{_heading.text} {_question.text} {_underTheQuestion.text}".Trim();

        /// <summary>What the account row, the last sync row and the code row show; for the tools.</summary>
        public (string Account, string LastSync, string Code) Shows => (IsLinkShown ? _link.Label : _accountValue.text, _lastSync.text, _code.text);

        /// <summary>Whether the switch shows the cloud save on.</summary>
        public bool ShowsTheSwitchOn => _switch.IsOn;

        /// <summary>The middles of the card's presses, in the layout's points; nothing while not in sight.</summary>
        public Vector2? SwitchMiddle => HasTheSwitch ? _card.InTheLayout(_switch.Middle) : (Vector2?)null;

        public Vector2? LinkMiddle => IsLinkShown ? _card.InTheLayout(_link.Middle) : (Vector2?)null;

        public Vector2? CodeRowMiddle => _device.Account.PlayerCode != null ? _card.InTheLayout(_codeRow.Middle) : (Vector2?)null;

        public Vector2? LeftAnswerMiddle => Asks == AccountCardAsks.Nothing || Asks == AccountCardAsks.AboutTheCloud || Asks == AccountCardAsks.AboutThePhone
            ? _card.InTheLayout(_leftAnswer.Middle) : (Vector2?)null;

        public Vector2? RightAnswerMiddle => Asks == AccountCardAsks.Nothing || Asks == AccountCardAsks.AboutTheCloud || Asks == AccountCardAsks.AboutThePhone
            ? _card.InTheLayout(_rightAnswer.Middle) : (Vector2?)null;

        public Vector2? DoneMiddle => Asks == AccountCardAsks.Tells ? _card.InTheLayout(_done.Middle) : (Vector2?)null;

        private bool HasTheSwitch => _device.Account.HasTheSwitch;

        private bool IsLinkShown => _device.Account.CanLinkTo != SignInPlatform.None;

        /// <summary>Builds the card out of sight above the screen. Place it before the first picture.</summary>
        /// <param name="raisedBy">Added to the card's places in the drawing order: <see cref="DrawOrder.OverACard"/> in front of the settings card.</param>
        public static AccountCard Create(Transform parent, ArtCatalogue art, DeviceView device, int raisedBy) => new AccountCard(parent, art, device, raisedBy);

        /// <summary>The switch was pressed: it goes to the other state, its knob slid by whoever times the slide.</summary>
        /// <returns>Whether the cloud save is turned on.</returns>
        public bool FlipTheSwitch()
        {
            _switch.Flip();
            return _switch.IsOn;
        }

        /// <summary>Shows the switch so far on its way to its new state, from 0 to 1.</summary>
        public void Slide(float progress) => _switch.Slide(progress);

        /// <summary>The code was copied: its row says so until <see cref="ShowTheCode"/>.</summary>
        public void ShowCopied() => SayValue(_code, AccountWords.CodeCopied, row: 3, CodeSize);

        public void ShowTheCode() => SayValue(_code, _device.Account.PlayerCode ?? AccountWords.NoCodeYet, row: 3, CodeSize);

        /// <summary>The card asks or tells this under its rows from now on.</summary>
        public void Ask(AccountCardAsks asks)
        {
            Asks = asks;
            ShowWhatIsAsked(heading: null, told: null);
        }

        /// <summary>What the deletion in the cloud came to: the question about this phone's progress, or why nothing was deleted.</summary>
        public void Hear(AccountNews news)
        {
            switch (news)
            {
                case AccountNews.CloudDataDeleted deleted:
                    Asks = AccountCardAsks.AboutThePhone;
                    ShowWhatIsAsked(AccountWords.CloudDeleted(deleted.ThereWereNone), told: null);
                    break;
                case AccountNews.CloudDataNotDeleted notDeleted:
                    Asks = AccountCardAsks.Tells;
                    ShowWhatIsAsked(heading: null, AccountWords.NotDeleted(notDeleted.Why));
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(news), news, "The account card hears only what a deletion came to.");
            }
        }

        /// <summary>The player kept this phone's progress after the cloud's was deleted: the card tells that the cloud save is off now.</summary>
        public void TellTheCloudSaveIsOff()
        {
            Asks = AccountCardAsks.Tells;
            ShowWhatIsAsked(heading: null, (AccountWords.CloudDeleted(thereWereNone: false), AccountWords.SwitchOffAfterDeletion));
        }

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        /// <summary>Shows the account as the cloud tells it now: where it stands, the switch, the last sync, the code.</summary>
        public void Tell(DeviceView device)
        {
            _device = device;
            ShowTheAccount();
            if (Asks == AccountCardAsks.Nothing)
            {
                ShowWhatIsAsked(heading: null, told: null);
            }
        }

        public void Dispose()
        {
            _hairlines.Dispose();
            _card.Dispose();
        }

        private void ShowTheAccount()
        {
            AccountView account = _device.Account;
            _accountValue.gameObject.SetActive(!IsLinkShown);
            _link.Show(IsLinkShown);
            SayValue(_accountValue, AccountWords.StateOf(account), row: 0, ValueSize);

            _switch.ShowInSight(HasTheSwitch);
            _switchMissing.gameObject.SetActive(!HasTheSwitch);
            if (HasTheSwitch && _switch.IsOn != account.IsSwitchedOn)
            {
                _switch.Set(account.IsSwitchedOn);
            }

            SayValue(_switchMissing, AccountWords.StateOf(account), row: 1, ValueSize);
            SayValue(_lastSync, AccountWords.LastSyncOf(account), row: 2, ValueSize);
            ShowTheCode();
        }

        /// <summary>A row's value, from the row's right end leftwards, set smaller where it would reach its label.</summary>
        private void SayValue(TextMeshPro value, string words, int row, float size)
        {
            float middle = _rowMiddles[row];
            float labelRight = SettingsLayout.RowsLeft + Lettering.WidthOf(_labels[row], _labels[row].text);
            Lettering.SayWithin(value, words, size, SettingsLayout.RowsRight - labelRight - ValueAfterLabel);
            value.transform.localPosition = new Vector3(SettingsLayout.RowsRight - Lettering.WidthOf(value, value.text) / 2f, middle, 0f);
        }

        /// <summary>What the card asks or tells under its rows, and the answers that go with it; what is not in sight is not pressed.</summary>
        private void ShowWhatIsAsked(string? heading, (string First, string Second)? told)
        {
            float room = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            bool tellsTheCloudSave = Asks == AccountCardAsks.Nothing;
            _line.gameObject.SetActive(tellsTheCloudSave);
            if (tellsTheCloudSave)
            {
                Lettering.SayInLines(_line, AccountWords.CardLine, room, SettingsLayout.RowsLeft, _card.At(LineTop).y);
            }

            (string? head, string? question, string? under) = Asks switch
            {
                AccountCardAsks.AboutTheCloud => ((string?)null, (string?)AccountWords.DeleteQuestion, (string?)AccountWords.DeleteTraceLine),
                AccountCardAsks.Deleting => (null, AccountWords.Deleting, null),
                AccountCardAsks.AboutThePhone => (heading, AccountWords.ForgetProgressQuestion, AccountWords.ForgetProgressLine),
                AccountCardAsks.Tells => (null, told?.First, told?.Second),
                _ => (null, null, null),
            };
            Show(_heading, head, HeadingDown, LineSize, room);
            Show(_question, question, head == null ? QuestionAloneDown : QuestionDown, QuestionSize, room);
            Show(_underTheQuestion, under, head == null ? QuestionAloneDown + (UnderTheQuestionDown - QuestionDown) : UnderTheQuestionDown, LineSize, room);

            (string? left, string? right) = Asks switch
            {
                AccountCardAsks.Nothing => ((string?)AccountWords.DeleteMyData, (string?)MuseumWords.Done),
                AccountCardAsks.AboutTheCloud => (PrivacyWords.KeepThem, PrivacyWords.Delete),
                AccountCardAsks.AboutThePhone => (AccountWords.KeepProgress, PrivacyWords.Delete),
                _ => (null, null),
            };
            _leftAnswer.Show(left != null);
            _rightAnswer.Show(right != null);
            if (left != null && right != null)
            {
                _leftAnswer.Say(left);
                _rightAnswer.Say(right);
            }

            _done.Show(Asks == AccountCardAsks.Tells);
            ListenToWhatIsInSight();
        }

        private void Show(TextMeshPro line, string? words, float down, float size, float room)
        {
            line.gameObject.SetActive(words != null);
            if (words != null)
            {
                Lettering.SayWithin(line, words, size, room);
                line.transform.localPosition = _card.At(down);
            }
        }

        private void ListenToWhatIsInSight()
        {
            _card.ForgetTheControls();
            if (IsLinkShown)
            {
                _card.Listen(_link, new MuseumPress.LinkFromTheAccountCard());
            }

            if (HasTheSwitch)
            {
                _card.Listen(_switch, new MuseumPress.FlipTheCloudSave());
            }

            if (_device.Account.PlayerCode != null)
            {
                _card.Listen(_codeRow, new MuseumPress.CopyThePlayerCode());
            }

            switch (Asks)
            {
                case AccountCardAsks.Nothing:
                    _card.Listen(_leftAnswer, new MuseumPress.AskToDeleteMyData());
                    _card.Listen(_rightAnswer, new MuseumPress.CloseTheCard());
                    break;
                case AccountCardAsks.AboutTheCloud:
                    _card.Listen(_leftAnswer, new MuseumPress.KeepMyData());
                    _card.Listen(_rightAnswer, new MuseumPress.DeleteMyData());
                    break;
                case AccountCardAsks.AboutThePhone:
                    _card.Listen(_leftAnswer, new MuseumPress.KeepMyData());
                    _card.Listen(_rightAnswer, new MuseumPress.ForgetThisPhone());
                    break;
                case AccountCardAsks.Tells:
                    _card.Listen(_done, new MuseumPress.KeepMyData());
                    break;
                case AccountCardAsks.Deleting:
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(Asks), Asks, "The account card asks something it has no answers for.");
            }
        }

        private TextMeshPro Write(string name, ArtCatalogue art, LetteringStyle style, float size, Color colour, int raisedBy) =>
            Lettering.Write(name, _card.Content, art, style, size, colour, DrawOrder.CardLettering + raisedBy);

        /// <summary>A row of the card as a place a finger presses: as wide as the rows, as high as one. It shows nothing while it is held.</summary>
        private sealed class RowArea : IPressable
        {
            private readonly float _middleY;

            public RowArea(float middleY)
            {
                _middleY = middleY;
            }

            public Vector2 Middle => new Vector2((SettingsLayout.RowsLeft + SettingsLayout.RowsRight) / 2f, _middleY);

            public bool IsUnder(Vector2 point) =>
                point.x >= SettingsLayout.RowsLeft && point.x <= SettingsLayout.RowsRight && Mathf.Abs(point.y - _middleY) <= RowHeight / 2f;

            public void ShowPressed(bool isPressed)
            {
            }
        }
    }
}
