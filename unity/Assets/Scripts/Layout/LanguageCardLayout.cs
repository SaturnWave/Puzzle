#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>One language's row on the language card: where it lies, left and right of the card's middle and down from its top edge.</summary>
    public readonly struct LanguageRowPlace
    {
        public LanguageRowPlace(int column, float left, float right, float down, float height)
        {
            Column = column;
            Left = left;
            Right = right;
            Down = down;
            Height = height;
        }

        /// <summary>The column the row stands in, counted from 0 at the left.</summary>
        public int Column { get; }

        public float Left { get; }

        public float Right { get; }

        /// <summary>How far under the card's top edge the row begins.</summary>
        public float Down { get; }

        public float Height { get; }

        /// <summary>How far under the card's top edge the row's middle lies, where its name and its tick stand.</summary>
        public float Middle => Down + Height / 2f;

        public float Bottom => Down + Height;
    }

    /// <summary>
    /// The language card, which the settings card's language row opens in front of it, in the design canvas' points
    /// around the middle of its body and down from its top edge. The canvas draws no such card; it is built in the
    /// language of the settings card, as the support card is: as wide, the nameplate "Dil" on its top edge, a row for
    /// each language it offers, named in its own words, with a tick at the right of the one the game speaks, the
    /// rows parted by gilt hairlines, and under them the green "Tamam". Up to seven languages stand in one column;
    /// more stand in two, filled down the left column first, so the thirteen of the first release take seven rows.
    /// A card of up to <see cref="MostRowsInFront"/> rows stands in front of the settings card on every screen the game
    /// is made for; a taller one runs over the room above that card's foot on the open fold, and hangs by itself, in the
    /// page's place for a card (<see cref="PlaceOn"/>), once the game offers that many languages. No Unity dependency.
    /// </summary>
    public sealed class LanguageCardLayout
    {
        public const float BodyWidth = SettingsLayout.BodyWidth;

        /// <summary>The rows begin this far under the card's top edge, as the support card's; a row is 48 high, two rows are parted by a hairline.</summary>
        public const float RowsDown = 40f;
        public const float RowHeight = 48f;

        /// <summary>A language's name, in the canvas' interface type at the size of a settings row's name.</summary>
        public const float NameSize = SettingsLayout.NameSize;

        /// <summary>The tick of the language the game speaks: a green badge this large, its middle this far in from the row's right end.</summary>
        public const float TickSize = 28f;
        public const float TickInFromTheEnd = 18f;

        /// <summary>The most languages one column holds; the two columns of more stand this far apart.</summary>
        public const int MostRowsInOneColumn = 7;

        /// <summary>The most rows a card has that stands in front of the settings card on every screen the game is made for.</summary>
        public const int MostRowsInFront = 5;
        public const float ColumnsApart = 18f;

        /// <summary>The green "Tamam", as the support card has it: its middle this far under the last row, the card's foot this far under its middle.</summary>
        public const float ButtonWidth = 180f;
        public const float ButtonLabelSize = 28f;
        public const float ButtonUnderTheRows = 50f;
        public const float FootUnderTheButton = 54f;

        private LanguageCardLayout(IReadOnlyList<LanguageRowPlace> rows, IReadOnlyList<LanguageRowPlace> hairlinesUnder, float buttonMiddle)
        {
            Rows = rows;
            HairlinesUnder = hairlinesUnder;
            ButtonMiddle = buttonMiddle;
            BodyHeight = buttonMiddle + FootUnderTheButton;
        }

        /// <summary>The rows, in the order of the languages: down the left column, then down the right one.</summary>
        public IReadOnlyList<LanguageRowPlace> Rows { get; }

        /// <summary>The rows a gilt hairline runs under: every row but the last of its column.</summary>
        public IReadOnlyList<LanguageRowPlace> HairlinesUnder { get; }

        /// <summary>How far under the card's top edge the middle of "Tamam" lies.</summary>
        public float ButtonMiddle { get; }

        public float BodyHeight { get; }

        /// <summary>The room the card takes on a screen: its body with its nameplate.</summary>
        public float Height => BodyHeight + SettingsLayout.RidesAbove;

        /// <summary>True for a card short enough to stand in front of the settings card on every screen; a taller one hangs by itself (<see cref="PlaceOn"/>).</summary>
        public bool StandsInFront => Rows.Count <= MostRowsInFront;

        /// <summary>The card for so many languages.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No language at all: the game always speaks one.</exception>
        public static LanguageCardLayout For(int languages)
        {
            if (languages < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(languages), languages, "The language card offers at least the language the game speaks.");
            }

            int columns = languages > MostRowsInOneColumn ? 2 : 1;
            int perColumn = (languages + columns - 1) / columns;
            float columnWidth = (SettingsLayout.RowsRight - SettingsLayout.RowsLeft - (columns - 1) * ColumnsApart) / columns;
            var rows = new List<LanguageRowPlace>();
            var hairlines = new List<LanguageRowPlace>();
            for (int index = 0; index < languages; index++)
            {
                int column = index / perColumn;
                int inColumn = index % perColumn;
                float left = SettingsLayout.RowsLeft + column * (columnWidth + ColumnsApart);
                var row = new LanguageRowPlace(column, left, left + columnWidth, RowsDown + inColumn * (RowHeight + SettingsLayout.Hairline), RowHeight);
                rows.Add(row);
                bool isLastOfItsColumn = inColumn == perColumn - 1 || index == languages - 1;
                if (!isLastOfItsColumn)
                {
                    hairlines.Add(row);
                }
            }

            float rowsBottom = RowsDown + perColumn * RowHeight + (perColumn - 1) * SettingsLayout.Hairline;
            return new LanguageCardLayout(rows, hairlines, rowsBottom + ButtonUnderTheRows);
        }

        /// <summary>Where the card hangs on a page by itself: the place of a card of more than <see cref="MostRowsInFront"/> rows, which is too tall to stand in front of the settings card.</summary>
        public CardPlace PlaceOn(PageLayout page) => page.PlaceOfACard(BodyWidth, Height);
    }
}
