#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>What a row of the settings card is. A closed set: the order of its members is the card's order.</summary>
    public enum SettingsRowKind
    {
        /// <summary>A setting with its switch; as many as the build honours.</summary>
        Switch,

        /// <summary>"Dil", with the language the game speaks as a value and a chevron: it opens the language card.</summary>
        Language,

        /// <summary>"Hesap ve bulut kaydı" with a chevron: it opens the account card (the v1 server, 2026-10-27).</summary>
        Account,

        /// <summary>"Gizlilik ve veri" with its smaller line and a chevron: it opens the privacy card.</summary>
        Privacy,

        /// <summary>"Destek" with a chevron: it opens the support card, whose "Künye" opens the credits card.</summary>
        Support,
    }

    /// <summary>One row of the settings card: what it is, and where it lies, down from the card's top edge.</summary>
    public readonly struct SettingsRowPlace
    {
        public SettingsRowPlace(SettingsRowKind kind, int switchIndex, float down, float height)
        {
            Kind = kind;
            SwitchIndex = switchIndex;
            Down = down;
            Height = height;
        }

        public SettingsRowKind Kind { get; }

        /// <summary>Which switch a switch's row holds, counted from 0 at the top; 0 for the other rows.</summary>
        public int SwitchIndex { get; }

        /// <summary>How far under the card's top edge the row begins.</summary>
        public float Down { get; }

        public float Height { get; }

        /// <summary>How far under the card's top edge the row's middle lies, where its disc, its name and its switch or chevron stand.</summary>
        public float Middle => Down + Height / 2f;

        /// <summary>How far under the card's top edge the row ends.</summary>
        public float Bottom => Down + Height;
    }

    /// <summary>
    /// The settings card, as the design canvas draws it (SettingsTR), in the canvas' own points: right of the middle
    /// of the card's body and down from its top edge. A body 362 wide with the brass nameplate "Ayarlar" on its top
    /// edge; under it the rows of the settings the build honours, each with its switch, then a gilt rule with a
    /// diamond, then the rows that lead somewhere or say something: the language, the account and its cloud save (which
    /// the canvas does not draw: the v1 server, 2026-10-27), privacy and data, and support. The credits (which the canvas
    /// does not draw either: they came with the painter's pictures, 2026-10-24) had a row of their own here until the
    /// account took its place, so that the card stays as high and a card in front of it as large on every screen; they
    /// open from the support card now. Rows are
    /// parted by gilt hairlines; under the last one, the foot: the player's id and the game's version, and under them
    /// the line that says what kind of build this is. A build that honours no setting draws neither the switches nor
    /// the rule, and the card is that much shorter. A screen too small for the card draws all of it smaller
    /// (<see cref="PageLayout.PlaceOfACard"/>). The cards it opens (privacy, support, credits) hang in front of it, as
    /// wide, and are measured here too. No Unity dependency.
    /// </summary>
    public sealed class SettingsLayout
    {
        public const float BodyWidth = 362f;

        /// <summary>The nameplate: 176 wide, the title in the serif at 23, one line; it rides half above the card, as high as the pins in the wall.</summary>
        public const float NameplateWidth = 176f;
        public const float TitleSize = 23f;
        public const float RidesAbove = 20f;

        /// <summary>The rows begin this far under the card's top edge, and reach from this far left of its middle to this far right.</summary>
        public const float RowsDown = 37f;
        public const float RowsLeft = -162f;
        public const float RowsRight = 164f;

        /// <summary>A row is 52 high; the privacy row, with its smaller line, 62. Two rows of a group are parted by a hairline 1.5 high.</summary>
        public const float RowHeight = 52f;
        public const float TallRowHeight = 62f;
        public const float Hairline = 1.5f;

        /// <summary>Between the switches and the other rows: a band of 17 with a gilt rule 148 wide and a diamond in its middle.</summary>
        public const float DividerBand = 17f;
        public const float DividerWidth = 148f;

        /// <summary>
        /// In a row, from its left end: the teal disc, 32 across, and its mark, 21; the name 11 after the disc. At its
        /// right end: a switch's button, 70 wide; or a chevron's disc, 26 across, 5 in from the end, with a value 11
        /// before it; a value with no chevron ends 5 in from the end.
        /// </summary>
        public const float DiscSize = 32f;
        public const float MarkSize = 21f;
        public const float NameAfterDisc = 11f;
        public const float SwitchWidth = 70f;
        public const float ChevronSize = 26f;
        public const float ChevronInFromEnd = 5f;
        public const float ValueBeforeChevron = 11f;

        /// <summary>The canvas' type: a row's name at 17, a value at 14.5, the privacy row's smaller line and the foot at 11.5.</summary>
        public const float NameSize = 17f;
        public const float ValueSize = 14.5f;
        public const float SmallSize = 11.5f;

        /// <summary>In the privacy row the name and its smaller line stand 6 apart and are centred together.</summary>
        public const float SmallLineGap = 6f;

        /// <summary>The foot: a band of 30 for the player's id and the version; a band of 17 more for the build's line; the card ends 16.5 under it.</summary>
        public const float FootBand = 30f;
        public const float BuildLineBand = 17f;
        public const float BottomMargin = 16.5f;

        /// <summary>
        /// The privacy card, the support card and the credits card are as wide as the settings card; the privacy card
        /// and the credits card at most this high (as their text needs), the support card this high. 470 is the most a
        /// card in front may be on every screen the tests know, over a settings card of three switches or more, as
        /// every build now draws (sound, music and the statistics at the least); it was 382 while the privacy text was
        /// four paragraphs.
        /// </summary>
        public const float PrivacyBodyHeight = 470f;
        public const float SupportBodyHeight = 372f;
        public const float CreditsBodyHeight = PrivacyBodyHeight;

        /// <summary>A card in front of another stands this far over the foot of the one behind, in that card's points (the museum's stage).</summary>
        public const float InFrontOverTheFoot = 16f;

        private SettingsLayout(IReadOnlyList<SettingsRowPlace> rows, IReadOnlyList<float> hairlines, float? divider, float footMiddle)
        {
            Rows = rows;
            Hairlines = hairlines;
            DividerMiddle = divider;
            FootMiddle = footMiddle;
            BuildLineMiddle = footMiddle + FootBand / 2f + BuildLineBand / 2f;
            BodyHeight = footMiddle + FootBand / 2f + BuildLineBand + BottomMargin;
        }

        /// <summary>The rows, from the top.</summary>
        public IReadOnlyList<SettingsRowPlace> Rows { get; }

        /// <summary>How far under the card's top edge each hairline's middle lies: between two rows of a group, and under the last row.</summary>
        public IReadOnlyList<float> Hairlines { get; }

        /// <summary>How far under the card's top edge the rule between the switches and the other rows lies; nothing on a card without switches.</summary>
        public float? DividerMiddle { get; }

        /// <summary>How far under the card's top edge the middles of the foot's line and of the build's line lie.</summary>
        public float FootMiddle { get; }

        public float BuildLineMiddle { get; }

        public float BodyHeight { get; }

        /// <summary>The room the card takes on a screen: its body with its nameplate.</summary>
        public float Height => BodyHeight + RidesAbove;

        /// <summary>The middle of the chevron's disc, right of the card's middle.</summary>
        public static float ChevronMiddle => RowsRight - ChevronInFromEnd - ChevronSize / 2f;

        /// <summary>The middle of a switch's button, right of the card's middle.</summary>
        public static float SwitchMiddle => RowsRight - SwitchWidth / 2f;

        /// <summary>The middle of a row's disc, and where its name begins, right of the card's middle.</summary>
        public static float DiscMiddle => RowsLeft + DiscSize / 2f;

        public static float NameLeft => RowsLeft + DiscSize + NameAfterDisc;

        /// <summary>Where a value ends: before the chevron of a row that leads somewhere, at the row's end otherwise.</summary>
        public static float ValueRight(bool hasAChevron) => hasAChevron ? ChevronMiddle - ChevronSize / 2f - ValueBeforeChevron : RowsRight - ChevronInFromEnd;

        /// <summary>The card with a row for each of so many switches.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Fewer switches than none.</exception>
        public static SettingsLayout For(int switches)
        {
            if (switches < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(switches), switches, "A card has no switch or some.");
            }

            var rows = new List<SettingsRowPlace>();
            var hairlines = new List<float>();
            float down = RowsDown;
            float? divider = null;
            for (int index = 0; index < switches; index++)
            {
                down = AddRow(rows, hairlines, SettingsRowKind.Switch, index, RowHeight, down, isFirstOfItsGroup: index == 0);
            }

            if (switches > 0)
            {
                divider = down + DividerBand / 2f;
                down += DividerBand;
            }

            down = AddRow(rows, hairlines, SettingsRowKind.Language, 0, RowHeight, down, isFirstOfItsGroup: true);
            down = AddRow(rows, hairlines, SettingsRowKind.Account, 0, RowHeight, down, isFirstOfItsGroup: false);
            down = AddRow(rows, hairlines, SettingsRowKind.Privacy, 0, TallRowHeight, down, isFirstOfItsGroup: false);
            down = AddRow(rows, hairlines, SettingsRowKind.Support, 0, RowHeight, down, isFirstOfItsGroup: false);
            hairlines.Add(down + Hairline / 2f);
            down += Hairline;
            return new SettingsLayout(rows, hairlines, divider, footMiddle: down + FootBand / 2f);
        }

        /// <summary>Where the card hangs on a page.</summary>
        public CardPlace PlaceOn(PageLayout page) => page.PlaceOfACard(BodyWidth, Height);

        /// <summary>The row of a kind; for the switches, the first.</summary>
        /// <exception cref="InvalidOperationException">The card has no such row.</exception>
        public SettingsRowPlace RowOf(SettingsRowKind kind)
        {
            foreach (SettingsRowPlace row in Rows)
            {
                if (row.Kind == kind)
                {
                    return row;
                }
            }

            throw new InvalidOperationException($"The settings card has no row of the kind {kind}.");
        }

        /// <summary>Adds a row where the last one ended, under a hairline unless it begins its group; returns where the next one begins.</summary>
        private static float AddRow(
            List<SettingsRowPlace> rows, List<float> hairlines, SettingsRowKind kind, int switchIndex, float height, float down, bool isFirstOfItsGroup)
        {
            if (!isFirstOfItsGroup)
            {
                hairlines.Add(down + Hairline / 2f);
                down += Hairline;
            }

            var row = new SettingsRowPlace(kind, switchIndex, down, height);
            rows.Add(row);
            return row.Bottom;
        }
    }
}
