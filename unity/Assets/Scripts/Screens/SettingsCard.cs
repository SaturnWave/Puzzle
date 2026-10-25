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
    /// <summary>The settings card and the four cards it opens in front of it, as the tools ask which of them is in front. A closed set.</summary>
    public enum SettingsCardKind
    {
        Settings,
        Privacy,
        Support,
        Language,
        Credits,
    }

    /// <summary>
    /// The settings card, as the design canvas draws it (SettingsTR), over a museum page: the brass nameplate
    /// "Ayarlar", a row with a switch for every setting the build honours (and the tester mode's while it holds, with
    /// the coin for its mark: the canvas draws no such row), a gilt rule under them, then the rows that open the
    /// language card (with the name of the language the game speaks, in its own words, before its chevron), the
    /// privacy card, the support card and the credits card (which the canvas does not draw), each with its teal disc
    /// and mark; at its foot the player's id and the game's version, and under them what kind of build this is. It is
    /// opened by the gear of the wallet row and closed by its round button and by the way back; the four cards it opens
    /// hang in front of it. Where everything stands is <see cref="SettingsLayout"/>'s, in the canvas' points.
    /// </summary>
    /// <remarks>
    /// A switch slides when its row is pressed, and its owner keeps the setting at once; the card shows the settings
    /// it is told (<see cref="Tell(DeviceView)"/>), the privacy row's smaller line too, which says what leaves the
    /// phone as the statistics' switch stands. A whole row is what a finger presses, as large as the canvas
    /// draws it, not only its switch or chevron. The rows are those of the phone as the card came down: the tester
    /// mode's row turned off stays, off, until the card is opened again.
    /// </remarks>
    public sealed class SettingsCard : IMuseumCard, IDeviceCard
    {
        /// <summary>The marks on the rows' teal discs and the chevron are drawn in a box of 100 points and at their own size.</summary>
        private const float MarkBox = 100f;

        /// <summary>The diamond in the rule under the switches: 8 across in a dark line of 0.9, as the section headers' (LevelStartTR).</summary>
        private const float DiamondAcross = 6.8f;
        private const float DiamondEdge = 1.25f;
        private const float DividerGapAtTheDiamond = 7f;

        /// <summary>A hairline keeps this far from each end of the rows.</summary>
        private const float HairlineInFromEnds = 2f;

        private readonly HangingCard<MuseumPress> _card;
        private readonly SettingsLayout _layout;
        private readonly PaintedShape _rules;
        private readonly TextMeshPro _foot;
        private readonly TextMeshPro _buildLine;
        private readonly Dictionary<DeviceSetting, Toggle> _switches = new Dictionary<DeviceSetting, Toggle>();

        /// <summary>The privacy row's smaller line and how far under the card's top edge it stands; nothing on a card built without the row.</summary>
        private (TextMeshPro Line, float Down)? _privacyLine;

        private SettingsCard(Transform parent, ArtCatalogue art, DeviceView device)
        {
            IReadOnlyList<DeviceSetting> switches = device.SettingRows;
            _layout = SettingsLayout.For(switches.Count);
            _card = new HangingCard<MuseumPress>(
                "Settings card", parent, art, new Vector2(SettingsLayout.BodyWidth, _layout.BodyHeight),
                new CardNameplate(SettingsWords.Title, SettingsLayout.NameplateWidth, SettingsLayout.TitleSize), CardHanging.FromPinsInTheWall);
            _card.AddCloseButton(new MuseumPress.CloseTheCard());

            foreach (SettingsRowPlace row in _layout.Rows)
            {
                AddRow(row, switches, device, art);
            }

            _rules = PaintedShape.Create("Rules", _card.Content, art.PaintMaterial, DrawOrder.CardArt);
            DrawTheRules();

            float room = SettingsLayout.RowsRight - SettingsLayout.RowsLeft;
            _foot = Lettering.Write("Foot", _card.Content, art, LetteringStyle.Soft, SettingsLayout.SmallSize, Lettering.SoftInk, DrawOrder.CardLettering);
            _foot.transform.localPosition = _card.At(_layout.FootMiddle);
            _buildLine = Lettering.Write("Build", _card.Content, art, LetteringStyle.Soft, SettingsLayout.SmallSize, Lettering.LabelInk, DrawOrder.CardLettering);
            Lettering.SayWithin(_buildLine, SettingsWords.BuildLine(device.Ads, device.RealMoney), SettingsLayout.SmallSize, room);
            _buildLine.transform.localPosition = _card.At(_layout.BuildLineMiddle);
            Lettering.SayWithin(_foot, SettingsWords.Foot(device.Device, device.GameVersion), SettingsLayout.SmallSize, room);
        }

        public HangingCard<MuseumPress> Card => _card;

        /// <summary>The middles of the rows that open the language card, the privacy card, the support card and the credits card, in the layout's points: where a finger presses them.</summary>
        public Vector2 LanguageRowMiddle => _card.InTheLayout(RowMiddle(_layout.RowOf(SettingsRowKind.Language)));

        public Vector2 PrivacyRowMiddle => _card.InTheLayout(RowMiddle(_layout.RowOf(SettingsRowKind.Privacy)));

        public Vector2 SupportRowMiddle => _card.InTheLayout(RowMiddle(_layout.RowOf(SettingsRowKind.Support)));

        public Vector2 CreditsRowMiddle => _card.InTheLayout(RowMiddle(_layout.RowOf(SettingsRowKind.Credits)));

        /// <summary>The settings whose switches the card shows, in its order.</summary>
        public IReadOnlyCollection<DeviceSetting> Switches => _switches.Keys;

        /// <summary>Builds the card out of sight above the screen, with a switch for each setting the phone's view lists. Place it before the first picture.</summary>
        public static SettingsCard Create(Transform parent, ArtCatalogue art, DeviceView device) => new SettingsCard(parent, art, device);

        /// <summary>The middle of a setting's switch, in the layout's points: where a finger presses it.</summary>
        /// <exception cref="ArgumentException">The card has no switch for that setting.</exception>
        public Vector2 SwitchMiddle(DeviceSetting setting) => _card.InTheLayout(SwitchOf(setting).Middle);

        /// <summary>Whether a setting's switch shows it on.</summary>
        /// <exception cref="ArgumentException">The card has no switch for that setting.</exception>
        public bool ShowsOn(DeviceSetting setting) => SwitchOf(setting).IsOn;

        /// <summary>A setting's switch was pressed: it goes to the other state, and its knob is slid by whoever times the slide.</summary>
        /// <returns>The state the setting is turned to.</returns>
        /// <exception cref="ArgumentException">The card has no switch for that setting.</exception>
        public bool Flip(DeviceSetting setting)
        {
            Toggle flipped = SwitchOf(setting);
            flipped.Flip();
            return flipped.IsOn;
        }

        /// <summary>Shows a setting's switch so far on its way to its new state, from 0 to 1.</summary>
        public void Slide(DeviceSetting setting, float progress) => SwitchOf(setting).Slide(progress);

        /// <summary>The card tells of nothing of the wallet.</summary>
        public void Tell(WalletView wallet)
        {
        }

        /// <summary>Shows the settings as they are kept; a switch whose state is that already does not move. The privacy row's smaller line follows them.</summary>
        public void Tell(DeviceView device)
        {
            foreach (KeyValuePair<DeviceSetting, Toggle> each in _switches)
            {
                bool isOn = device.Settings.IsOn(each.Key);
                if (each.Value.IsOn != isOn)
                {
                    each.Value.Set(isOn);
                }
            }

            if (_privacyLine is (TextMeshPro line, float down))
            {
                SayThePrivacyLine(line, down, device.Settings);
            }
        }

        /// <summary>What the privacy row's smaller line says now.</summary>
        public string? PrivacyLineSays => _privacyLine?.Line.text;

        public void Dispose()
        {
            _rules.Dispose();
            _card.Dispose();
        }

        private Toggle SwitchOf(DeviceSetting setting) =>
            _switches.TryGetValue(setting, out Toggle found) ? found : throw new ArgumentException($"The settings card has no switch for {setting}.", nameof(setting));

        private Vector3 RowMiddle(SettingsRowPlace row) => _card.At(row.Middle) + new Vector3((SettingsLayout.RowsLeft + SettingsLayout.RowsRight) / 2f, 0f, 0f);

        private void AddRow(SettingsRowPlace row, IReadOnlyList<DeviceSetting> switches, DeviceView device, ArtCatalogue art)
        {
            Vector3 middle = _card.At(row.Middle);
            switch (row.Kind)
            {
                case SettingsRowKind.Switch:
                    DeviceSetting setting = switches[row.SwitchIndex];
                    AddDiscAndName(row, MarkOf(setting), SettingsWords.NameOf(setting), art);
                    var toggle = new Toggle($"{setting} switch", _card.Content, art, device.Settings.IsOn(setting), DrawOrder.CardDisc);
                    toggle.Place(new Vector2(SettingsLayout.SwitchMiddle, middle.y));
                    _switches.Add(setting, toggle);
                    _card.Listen(new RowButton(row, toggle), new MuseumPress.FlipASwitch(setting));
                    break;
                case SettingsRowKind.Language:
                    AddDiscAndName(row, GalleryArt.IconGlobe, SettingsWords.Language, art);
                    TextMeshPro value = Lettering.Write("Value", _card.Content, art, LetteringStyle.Soft, SettingsLayout.ValueSize, Lettering.SoftInk, DrawOrder.CardLettering);
                    Lettering.SayUpToRight(value, device.SpeakingName, SettingsLayout.ValueRight(hasAChevron: true), middle.y);
                    _card.Listen(new RowButton(row, AddChevron(middle.y, art)), new MuseumPress.OpenTheLanguages());
                    break;
                case SettingsRowKind.Privacy:
                    AddPrivacyRow(row, device.Settings, art);
                    _card.Listen(new RowButton(row, AddChevron(middle.y, art)), new MuseumPress.OpenThePrivacy());
                    break;
                case SettingsRowKind.Support:
                    AddDiscAndName(row, GalleryArt.IconHelp, SettingsWords.Support, art);
                    _card.Listen(new RowButton(row, AddChevron(middle.y, art)), new MuseumPress.OpenTheSupport());
                    break;
                case SettingsRowKind.Credits:
                    AddDiscAndName(row, GalleryArt.IconCollection, CreditWords.Title, art);
                    _card.Listen(new RowButton(row, AddChevron(middle.y, art)), new MuseumPress.OpenTheCredits());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(row), row.Kind, "A row the settings card does not know.");
            }
        }

        /// <summary>The privacy row: its disc, its name, and the smaller line under the name, the two centred together on the row.</summary>
        private void AddPrivacyRow(SettingsRowPlace row, DeviceSettings settings, ArtCatalogue art)
        {
            float together = SettingsLayout.NameSize + SettingsLayout.SmallLineGap + SettingsLayout.SmallSize;
            float nameDown = row.Middle - together / 2f + SettingsLayout.NameSize / 2f;
            float lineDown = row.Middle + together / 2f - SettingsLayout.SmallSize / 2f;
            AddDisc(row, GalleryArt.IconShield, art);
            AddName(nameDown, PrivacyWords.Title, art);
            TextMeshPro line = Lettering.Write("Smaller line", _card.Content, art, LetteringStyle.Soft, SettingsLayout.SmallSize, Lettering.SoftInk, DrawOrder.CardLettering);
            _privacyLine = (line, lineDown);
            SayThePrivacyLine(line, lineDown, settings);
        }

        /// <summary>
        /// The privacy row's smaller line as the settings stand, from the name's left edge, set smaller where a language's
        /// words would reach the chevron.
        /// </summary>
        private void SayThePrivacyLine(TextMeshPro line, float down, DeviceSettings settings)
        {
            float room = SettingsLayout.ValueRight(hasAChevron: true) - SettingsLayout.NameLeft;
            Lettering.SayWithin(line, SettingsWords.PrivacyLine(settings), SettingsLayout.SmallSize, room);
            line.transform.localPosition = new Vector3(SettingsLayout.NameLeft + Lettering.WidthOf(line, line.text) / 2f, _card.At(down).y, 0f);
        }

        private void AddDiscAndName(SettingsRowPlace row, GalleryArt mark, string name, ArtCatalogue art)
        {
            AddDisc(row, mark, art);
            AddName(row.Middle, name, art);
        }

        private void AddDisc(SettingsRowPlace row, GalleryArt mark, ArtCatalogue art)
        {
            var middle = new Vector3(SettingsLayout.DiscMiddle, _card.At(row.Middle).y, 0f);
            Transform disc = SceneObjects.AddSprite("Disc", _card.Content, art.Of(GalleryArt.SmallTealDisc), art.SpriteMaterial, DrawOrder.CardDisc).transform;
            disc.localPosition = middle;
            Transform drawn = SceneObjects.AddSprite("Mark", _card.Content, art.Of(mark), art.SpriteMaterial, DrawOrder.CardDisc + (int)DiscPart.Picture).transform;
            drawn.localPosition = middle;
            drawn.localScale = new Vector3(SettingsLayout.MarkSize / MarkBox, SettingsLayout.MarkSize / MarkBox, 1f);
        }

        private void AddName(float down, string name, ArtCatalogue art)
        {
            TextMeshPro words = Lettering.Write("Name", _card.Content, art, LetteringStyle.Label, SettingsLayout.NameSize, Lettering.Ink, DrawOrder.CardLettering);
            Lettering.SayFromLeft(words, name, SettingsLayout.NameLeft, _card.At(down).y);
        }

        /// <summary>The small teal disc with the white chevron at a row's right end: the row leads somewhere.</summary>
        private Transform AddChevron(float height, ArtCatalogue art)
        {
            Transform chevron = new GameObject("Chevron").transform;
            chevron.SetParent(_card.Content, worldPositionStays: false);
            chevron.localPosition = new Vector3(SettingsLayout.ChevronMiddle, height, 0f);
            SceneObjects.AddSprite("Disc", chevron, art.Of(GalleryArt.TinyTealDisc), art.SpriteMaterial, DrawOrder.CardDisc);
            SceneObjects.AddSprite("Glyph", chevron, art.Of(GalleryArt.GlyphChevron), art.SpriteMaterial, DrawOrder.CardDisc + (int)DiscPart.Picture);
            return chevron;
        }

        /// <summary>The hairlines between the rows and over the foot, and the rule with its diamond under the switches.</summary>
        private void DrawTheRules()
        {
            var sketch = new MeshSketch();
            float left = SettingsLayout.RowsLeft + HairlineInFromEnds;
            float right = SettingsLayout.RowsRight - HairlineInFromEnds;
            foreach (float down in _layout.Hairlines)
            {
                GiltRule.AddHairline(sketch, left, right, _card.At(down).y);
            }

            if (_layout.DividerMiddle is float divider)
            {
                float height = _card.At(divider).y;
                float reach = SettingsLayout.DividerWidth / 2f;
                GiltRule.AddLine(sketch, -reach, -DividerGapAtTheDiamond, height, isShaded: true);
                GiltRule.AddLine(sketch, DividerGapAtTheDiamond, reach, height, isShaded: true);
                GiltRule.AddDiamond(sketch, new Vector2(0f, height), DiamondAcross, DiamondEdge);
            }

            _rules.Show(sketch.ToMesh("Settings card rules"));
        }

        private static GalleryArt MarkOf(DeviceSetting setting) => setting switch
        {
            DeviceSetting.Sound => GalleryArt.IconSound,
            DeviceSetting.Music => GalleryArt.IconMusic,
            DeviceSetting.Vibration => GalleryArt.IconBuzz,
            DeviceSetting.TesterMode => GalleryArt.IconCoin,
            DeviceSetting.Statistics => GalleryArt.IconStatistics,
            _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "A setting without a mark."),
        };

        /// <summary>
        /// A whole row as one button: a finger anywhere on it presses it. Held, what leads (the switch's knob, or the
        /// chevron's disc) goes down a little and loses a little of its light.
        /// </summary>
        private sealed class RowButton : IPressable
        {
            private const float ChevronPressedDown = 1.5f;

            private readonly SettingsRowPlace _row;
            private readonly Toggle? _switch;
            private readonly Transform? _chevron;
            private readonly float _chevronHeight;

            public RowButton(SettingsRowPlace row, Toggle toggle)
            {
                _row = row;
                _switch = toggle;
            }

            public RowButton(SettingsRowPlace row, Transform chevron)
            {
                _row = row;
                _chevron = chevron;
                _chevronHeight = chevron.localPosition.y;
            }

            /// <param name="point">A point of the card, in its own points around the middle of its body.</param>
            public bool IsUnder(Vector2 point)
            {
                float rowMiddleY = _switch != null ? _switch.Middle.y : _chevronHeight;
                return point.x >= SettingsLayout.RowsLeft && point.x <= SettingsLayout.RowsRight && Mathf.Abs(point.y - rowMiddleY) <= _row.Height / 2f;
            }

            public void ShowPressed(bool isPressed)
            {
                _switch?.ShowPressed(isPressed);
                if (_chevron != null)
                {
                    _chevron.localPosition = new Vector3(_chevron.localPosition.x, _chevronHeight - (isPressed ? ChevronPressedDown : 0f), 0f);
                    foreach (SpriteRenderer part in _chevron.GetComponentsInChildren<SpriteRenderer>())
                    {
                        part.color = Dimming.Of(isDimmed: false, isHeld: isPressed);
                    }
                }
            }
        }
    }
}
