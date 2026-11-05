#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Layout
{
    /// <summary>The panels of the shop, from the top of its column down. A closed set.</summary>
    public enum ShopPanel
    {
        /// <summary>"ALTIN": the packs of coins, the starter pack in the first one's place while it is offered, and on the App Store the row that asks for the purchases back.</summary>
        Coins,

        /// <summary>"GÜÇLENDİRİCİLER": the sets of boosters, a row each.</summary>
        Boosters,

        /// <summary>"ÜCRETSİZ": the strip of the ad for coins.</summary>
        Free,
    }

    /// <summary>
    /// Where everything of the shop lies, as the design canvas draws it (ShopTR): under the wallet row the brass sign
    /// on its pin between two lamps, and under it three cream panels: the packs of coins, the sets of boosters, and
    /// the strip of the free ad, over the navigation bar. Everything of a panel is placed in the canvas' own points
    /// from the panel's top left corner (<see cref="InPanel"/>) and drawn at <see cref="Scale"/> layout points to one
    /// of them. On a phone the panels stand in one column, as wide as the page's plaque; where the height is short the
    /// gaps between the parts give way first, down to their least, and only then is the column drawn smaller. A screen
    /// wide enough for two columns at the column's least size (<see cref="SmallestShare"/>) has the packs and the sets
    /// side by side under the sign and the free strip under them. Nothing scrolls. The canvas draws three packs of
    /// coins; four stand in the same row, each column drawn at three quarters (<see cref="PackScale"/>) with its button
    /// as high as ever. The starter pack, while it is offered, stands in the first pack's column, so it costs the shop
    /// no height: the shortest phone the game is laid out for has no room for a panel more with buttons a finger can
    /// hit. On the App Store the row that asks for the purchases back lies at the foot of the coins' panel, which
    /// grows by <see cref="RestoreRowRoom"/> for it. No Unity dependency.
    /// </summary>
    public sealed class ShopLayout
    {
        /// <summary>A panel is as wide as the canvas' plaque, and the column as wide as the page's plaque at full size.</summary>
        public const float PanelWidth = 362f;

        /// <summary>The column is drawn at no less than this share of its full size on the screens the game is laid out for.</summary>
        public const float SmallestShare = 0.86f;

        /// <summary>The sign: a brass plate of 176 by 40 with the pin it hangs from 14.5 over its top edge, the pin's top 3 higher.</summary>
        public const float SignWidth = 176f;
        public const float SignHeight = 40f;
        public const float PinOverTheSign = 14.5f;
        private const float PinRadius = 3f;

        /// <summary>The lamps hang 141 points to each side of the sign's middle, their feet 46 under its top edge.</summary>
        public const float LampsFromTheMiddle = 141f;
        public const float LampFootUnderTheSignTop = 46f;

        /// <summary>The free strip's height; the coins' panel's follows its packs, and the sets' panel grows by a row for each set.</summary>
        public const float FreePanelHeight = 74f;

        /// <summary>Each panel's header: the middle of its words this far under the panel's top edge.</summary>
        public const float HeaderDown = 21f;

        /// <summary>
        /// The packs of coins, each in a column 104 wide, 11 apart, from 38 under the panel's top: a gilt frame of
        /// 100 by 78 at the column's top, the amount's pill (24 high) with its middle 79 under the column's top, and
        /// the button (100 by 44) with its middle 44 under the pill's. Four packs draw their columns, frames and the
        /// gaps at <see cref="PackScale"/>, the pill and the button's height as they are.
        /// </summary>
        public const float PackColumnWidth = 104f;
        public const float PackColumnsApart = 11f;
        public const float PacksDown = 38f;
        public const float PackFrameWidth = 100f;
        public const float PackFrameHeight = 78f;
        public const float PackPillDown = 79f;
        public const float PackButtonUnderThePill = 44f;

        /// <summary>The most packs of coins the panel holds side by side.</summary>
        public const int MostPacks = 4;

        /// <summary>Under the packs' buttons the panel goes on this far.</summary>
        private const float UnderThePackButtons = 12f;

        /// <summary>
        /// A set's row: its top 37 under the panel's top and 82 under the row before; the wooden shelf (220 by 58) 16
        /// in from the panel's left, its discs (44 across) in equal columns along it, 27.5 under the row's top, each
        /// with its count's pill 58 under it; at the right, in a column of 100 that ends 16 in from the panel's right,
        /// the set's name 6.75 under the row's top and its button with its middle 40.5 under it. The starter pack's
        /// panel is a panel of one such row.
        /// </summary>
        public const float FirstSetRowDown = 37f;
        public const float SetRowsApart = 82f;
        private const float SetRowHeight = 67.5f;
        private const float UnderTheLastSetRow = 17.5f;
        public const float ShelfLeft = 16f;
        public const float ShelfWidth = 220f;
        public const float ShelfHeight = 58f;
        public const float DiscSize = 44f;
        private const float ShelfInset = 1f;
        public const float DiscDown = 27.5f;
        public const float CountPillDown = 58f;
        public const float SetColumnMiddle = 296f;
        public const float SetNameDown = 6.75f;
        public const float SetButtonDown = 40.5f;

        /// <summary>
        /// The free strip: the pile of coins (46 across) with its middle 35 from the left and 35 under the top, the
        /// two lines from 64 in, 174 wide, their middles 27 and 44.5 under the top, and the blue button at the right
        /// in the sets' column, its middle 37 under the top.
        /// </summary>
        public const float FreeCoinsMiddle = 35f;
        public const float FreeCoinsSize = 46f;
        public const float FreeLinesLeft = 64f;
        public const float FreeLinesRoom = 174f;
        public const float FreeLabelDown = 27f;
        public const float FreeLineDown = 44.5f;
        public const float FreeButtonDown = 37f;

        /// <summary>
        /// The row that asks the App Store for the purchases back, at the foot of the coins' panel: an underlined line
        /// whose middle is 23 under the packs' buttons (half a finger's band and a point), the panel 18 deeper under it.
        /// </summary>
        public const float RestoreRowUnderTheButtons = 23f;
        public const float RestoreRowRoom = RestoreRowUnderTheButtons + 18f - UnderThePackButtons;
        private const float RestoreRowHeight = 28f;

        /// <summary>The small buttons are 100 by 44 with a lip of 5 under them: what a finger may hit.</summary>
        public const float ButtonWidth = 100f;
        public const float ButtonHeight = 44f;
        public const float ButtonLip = 5f;

        /// <summary>
        /// The canvas' gaps, in its points at full size: from the wallet row to the pin's top, from the sign to the
        /// first panel, between two parts, and from the last part to the top of the bar's raised place. Where the
        /// height is short they give way down to their least, in layout points.
        /// </summary>
        private const float GapOverThePin = 10.5f;
        private const float GapUnderTheSign = 17f;
        private const float GapBetweenParts = 16f;
        private const float GapOverTheBar = 24f;
        private const float LeastGapOverThePin = 2f;
        private const float LeastGapUnderTheSign = 5f;
        private const float LeastGapBetweenParts = 4f;
        private const float LeastGapOverTheBar = 4f;

        /// <summary>With room to spare, a gap between two parts grows by at most this share of the canvas' gap.</summary>
        private const float MostGrowthBetweenParts = 0.5f;

        /// <summary>Two columns stand this far apart, in layout points.</summary>
        public const float ColumnsApart = 12f;

        private readonly Dictionary<ShopPanel, Box> _panels = new Dictionary<ShopPanel, Box>();

        private ShopLayout(PageLayout page, int packs, int sets, bool hasRestore)
        {
            Page = page;
            PackCount = packs;
            SetCount = sets;
            ScreenView view = page.View;
            float sideRoom = view.Width - 2f * PageLayout.PlaqueSideMargin;
            FullScale = Math.Min(PageLayout.WidestPlaque, sideRoom) / PanelWidth;
            float coinsHeight = CoinsPanelHeight(packs) + (hasRestore ? RestoreRowRoom : 0f);
            float boostersHeight = BoostersPanelHeight(sets);
            float signBlock = PinOverTheSign + PinRadius + SignHeight;

            // The parts under the sign, from the top down, each a row of panels (two side by side on a wide screen).
            IsTwoColumns = (sideRoom - ColumnsApart) / (2f * PanelWidth) >= SmallestShare * FullScale;
            var rows = new List<ShopPanel[]>();
            if (IsTwoColumns)
            {
                rows.Add(new[] { ShopPanel.Coins, ShopPanel.Boosters });
            }
            else
            {
                rows.Add(new[] { ShopPanel.Coins });
                rows.Add(new[] { ShopPanel.Boosters });
            }

            rows.Add(new[] { ShopPanel.Free });
            float HeightOf(ShopPanel panel) => panel switch
            {
                ShopPanel.Coins => coinsHeight,
                ShopPanel.Boosters => boostersHeight,
                ShopPanel.Free => FreePanelHeight,
                _ => throw new ArgumentOutOfRangeException(nameof(panel), panel, "The shop has no such panel."),
            };
            float[] rowHeights = rows.Select(row => row.Max(HeightOf)).ToArray();
            float[] blocks = new[] { signBlock }.Concat(rowHeights).ToArray();
            int between = blocks.Length - 2;
            float[] designGaps = new[] { GapOverThePin, GapUnderTheSign }.Concat(Enumerable.Repeat(GapBetweenParts, between)).Append(GapOverTheBar).ToArray();
            float[] leastGaps = new[] { LeastGapOverThePin, LeastGapUnderTheSign }.Concat(Enumerable.Repeat(LeastGapBetweenParts, between)).Append(LeastGapOverTheBar).ToArray();

            float widest = IsTwoColumns ? (sideRoom - ColumnsApart) / (2f * PanelWidth) : FullScale;
            float room = page.WalletRow.Bottom - page.BarReach;
            Scale = Math.Min(widest, (room - leastGaps.Sum()) / blocks.Sum());
            float[] gaps = GapsFor(room - Scale * blocks.Sum(), designGaps.Select(gap => gap * Scale).ToArray(), leastGaps);

            float signTop = page.WalletRow.Bottom - gaps[0] - Scale * (PinOverTheSign + PinRadius);
            Pin = (0f, signTop + Scale * (PinOverTheSign + PinRadius));
            Sign = Box.Around(0f, signTop - Scale * (SignHeight / 2f), Scale * SignWidth, Scale * SignHeight);
            float width = Scale * PanelWidth;
            float top = Sign.Bottom - gaps[1];
            for (int row = 0; row < rows.Count; row++)
            {
                ShopPanel[] panels = rows[row];
                for (int place = 0; place < panels.Length; place++)
                {
                    float left = panels.Length == 1 ? -width / 2f : place == 0 ? -ColumnsApart / 2f - width : ColumnsApart / 2f;
                    float height = Scale * HeightOf(panels[place]);
                    _panels[panels[place]] = new Box(left, top - height, width, height);
                }

                top -= Scale * rowHeights[row] + gaps[2 + row];
            }

            Rows = rows.Select(row => (IReadOnlyList<ShopPanel>)row).ToArray();
            if (hasRestore)
            {
                (float x, float y) = InPanel(ShopPanel.Coins, PanelWidth / 2f, PacksDown + PackButtonDown(packs) + ButtonHeight / 2f + ButtonLip + RestoreRowUnderTheButtons);
                RestoreRow = Box.Around(x, y, width, Scale * RestoreRowHeight);
            }
        }

        public PageLayout Page { get; }

        /// <summary>Layout points to one of the canvas' points at the column's full size: the page's plaque over the canvas' panel.</summary>
        public float FullScale { get; }

        /// <summary>Layout points to one of the canvas' points as the shop is drawn on this screen.</summary>
        public float Scale { get; }

        /// <summary>The share of its full size the column is drawn at.</summary>
        public float Share => Scale / FullScale;

        /// <summary>True on a screen wide enough for the packs and the sets side by side.</summary>
        public bool IsTwoColumns { get; }

        public int PackCount { get; }

        public int SetCount { get; }

        /// <summary>The sign's brass plate, centred on the screen.</summary>
        public Box Sign { get; }

        /// <summary>The top of the pin the sign hangs from.</summary>
        public (float X, float Top) Pin { get; }

        /// <summary>The middle of the pin the sign hangs from, where its wire begins.</summary>
        public (float X, float Y) PinMiddle => (Pin.X, Pin.Top - PinRadius * Scale);

        /// <summary>The middle of the foot of each lamp beside the sign; the right one stands as far to the right.</summary>
        public (float X, float Foot) LeftLamp => (-LampsFromTheMiddle * Scale, Sign.Top - LampFootUnderTheSignTop * Scale);

        /// <summary>The panels under the sign, a row at a time from the top: one panel in a row, or two side by side.</summary>
        public IReadOnlyList<IReadOnlyList<ShopPanel>> Rows { get; }

        /// <summary>The panels shown, in the order they are told.</summary>
        public IEnumerable<ShopPanel> PanelsShown => Rows.SelectMany(row => row);

        /// <summary>The row at the foot of the coins' panel that asks for the purchases back, as wide as the panel; nothing where the store gives them back by itself.</summary>
        public Box? RestoreRow { get; }

        /// <summary>A panel's box, its rim included.</summary>
        public Box Panel(ShopPanel panel) => _panels[panel];

        /// <summary>The panels in the order they are told, from the packs to the free strip.</summary>
        public static IReadOnlyList<ShopPanel> Panels { get; } = new[] { ShopPanel.Coins, ShopPanel.Boosters, ShopPanel.Free };

        /// <summary>A point of a panel, given in the canvas' points across from its left edge and down from its top, in layout points.</summary>
        public (float X, float Y) InPanel(ShopPanel panel, float across, float down)
        {
            Box box = _panels[panel];
            return (box.Left + across * Scale, box.Top - down * Scale);
        }

        /// <summary>The sets' panel with so many rows.</summary>
        public static float BoostersPanelHeight(int rows) => FirstSetRowDown + (rows - 1) * SetRowsApart + SetRowHeight + UnderTheLastSetRow;

        /// <summary>The coins' panel with so many packs, without the restore row: 200 for up to three, as the canvas draws it.</summary>
        public static float CoinsPanelHeight(int packs) => PacksDown + PackButtonDown(packs) + ButtonHeight / 2f + ButtonLip + UnderThePackButtons;

        /// <summary>How large a pack's column, frame and pictures are drawn for so many packs: whole for up to three, three quarters for four.</summary>
        public static float PackScale(int packs) => packs <= 3 ? 1f : 0.75f;

        /// <summary>A pack's column, and the button in it, for so many packs.</summary>
        public static float PackColumnWidthFor(int packs) => PackColumnWidth * PackScale(packs);

        public static float PackButtonWidth(int packs) => Math.Min(ButtonWidth, PackColumnWidthFor(packs));

        /// <summary>The middle of a pack's amount pill under the column's top: on the frame's foot.</summary>
        public static float PackPillDownFor(int packs) => PackPillDown * PackScale(packs);

        /// <summary>The middle of a pack's button under the column's top.</summary>
        public static float PackButtonDown(int packs) => PackPillDownFor(packs) + PackButtonUnderThePill;

        /// <summary>The middle of the column of pack <paramref name="pack"/> of <paramref name="packs"/> across its panel, in the canvas' points; the packs are centred as a row.</summary>
        public static float PackMiddle(int pack, int packs)
        {
            float column = PackColumnWidthFor(packs);
            float apart = PackColumnsApart * PackScale(packs);
            float row = packs * column + (packs - 1) * apart;
            return (PanelWidth - row) / 2f + pack * (column + apart) + column / 2f;
        }

        /// <summary>The top of set <paramref name="set"/>'s row under its panel's top, in the canvas' points.</summary>
        public static float SetRowTop(int set) => FirstSetRowDown + set * SetRowsApart;

        /// <summary>The middle of disc <paramref name="disc"/> of a shelf of <paramref name="discs"/> across its panel, in the canvas' points.</summary>
        public static float DiscMiddle(int disc, int discs) => ShelfLeft + ShelfInset + (disc + 0.5f) * (ShelfWidth - 2f * ShelfInset) / discs;

        /// <summary>What a finger may hit of a pack's button, in layout points: the pill and its lip.</summary>
        public Box PackButton(int pack) => HeldButton(ShopPanel.Coins, PackMiddle(pack, PackCount), PacksDown + PackButtonDown(PackCount), PackButtonWidth(PackCount));

        /// <summary>What a finger may hit of a set's button.</summary>
        public Box SetButton(int set) => HeldButton(ShopPanel.Boosters, SetColumnMiddle, SetRowTop(set) + SetButtonDown, ButtonWidth);

        /// <summary>What a finger may hit of the free strip's button.</summary>
        public Box AdButton => HeldButton(ShopPanel.Free, SetColumnMiddle, FreeButtonDown, ButtonWidth);

        /// <summary>Every button of the shop's panels, as a finger may hit it.</summary>
        public IEnumerable<Box> Buttons =>
            Enumerable.Range(0, PackCount).Select(PackButton).Concat(Enumerable.Range(0, SetCount).Select(SetButton)).Append(AdButton);

        /// <summary>The shop for a screen and for the goods the settings have, without the restore row.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No pack or no set, or more packs than a panel holds side by side.</exception>
        public static ShopLayout Of(ScreenShape screen, int packs, int sets) => Of(screen, packs, sets, hasRestore: false);

        /// <summary>The shop for a screen and for the goods the settings have: so many packs of coins and so many sets, and the restore row on the App Store.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No pack or no set, or more packs than a panel holds side by side.</exception>
        public static ShopLayout Of(ScreenShape screen, int packs, int sets, bool hasRestore)
        {
            if (packs < 1 || packs > MostPacks)
            {
                throw new ArgumentOutOfRangeException(nameof(packs), packs, $"The coins' panel holds one to {MostPacks} packs side by side.");
            }

            if (sets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sets), sets, "The sets' panel holds a row for each set, and there is at least one.");
            }

            return new ShopLayout(PageLayout.Of(screen), packs, sets, hasRestore);
        }

        /// <summary>
        /// The gaps as the room left over by the parts gives them. Where there is more than the canvas' gaps, the gaps
        /// between the parts take an even share of it up to half as much again, so the parts stay together, and the
        /// rest goes above the sign and under the last part alike: the shop stands in the middle of its room. Where
        /// the room is short the gaps lie between their least and the canvas', and at their least where it is shorter still.
        /// </summary>
        private static float[] GapsFor(float room, float[] design, float[] least)
        {
            float designSum = design.Sum();
            float leastSum = least.Sum();
            if (room >= designSum)
            {
                float more = room - designSum;
                float[] gaps = design.ToArray();
                int last = gaps.Length - 1;
                for (int gap = 1; gap < last; gap++)
                {
                    float grows = Math.Min(more / gaps.Length, MostGrowthBetweenParts * design[gap]);
                    gaps[gap] += grows;
                }

                float outside = (room - gaps.Sum()) / 2f;
                gaps[0] += outside;
                gaps[last] += outside;
                return gaps;
            }

            float between = designSum > leastSum ? Math.Clamp((room - leastSum) / (designSum - leastSum), 0f, 1f) : 0f;
            return design.Select((gap, index) => least[index] + between * Math.Max(0f, gap - least[index])).ToArray();
        }

        /// <summary>A small button's pill and lip, its pill's middle at a point of a panel given in the canvas' points.</summary>
        private Box HeldButton(ShopPanel panel, float across, float down, float width)
        {
            (float x, float y) = InPanel(panel, across, down);
            float top = y + ButtonHeight / 2f * Scale;
            float bottom = y - (ButtonHeight / 2f + ButtonLip) * Scale;
            return new Box(x - width / 2f * Scale, bottom, width * Scale, top - bottom);
        }
    }
}
