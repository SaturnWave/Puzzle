#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Layout
{
    /// <summary>The panels of the shop, from the top of its column down. A closed set.</summary>
    public enum ShopPanel
    {
        /// <summary>"ALTIN": the packs of coins.</summary>
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
    /// from the panel's top left corner (<see cref="InPanel"/>) and drawn at <see cref="Scale"/> layout points to
    /// one of them. On a phone the panels stand in one column, as wide as the page's plaque; where the height is
    /// short the gaps between the parts give way first, down to their least, and only then is the column drawn
    /// smaller. A screen wide enough for two columns at the column's least size (<see cref="SmallestShare"/>) has the
    /// packs and the sets side by side under the sign and the free strip under them. Nothing scrolls. No Unity
    /// dependency.
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

        /// <summary>The panels' heights; the sets' panel grows by a row for each set.</summary>
        public const float CoinsPanelHeight = 200f;
        public const float FreePanelHeight = 74f;

        /// <summary>Each panel's header: the middle of its words this far under the panel's top edge.</summary>
        public const float HeaderDown = 21f;

        /// <summary>
        /// The packs of coins, each in a column 104 wide, 11 apart, from 38 under the panel's top: a gilt frame of
        /// 100 by 78 at the column's top, the amount's pill (24 high) with its middle 79 under the column's top, and
        /// the button (100 by 44) with its middle 123 under it.
        /// </summary>
        public const float PackColumnWidth = 104f;
        public const float PackColumnsApart = 11f;
        public const float PacksDown = 38f;
        public const float PackFrameWidth = 100f;
        public const float PackFrameHeight = 78f;
        public const float PackPillDown = 79f;
        public const float PackButtonDown = 123f;

        /// <summary>
        /// A set's row: its top 37 under the panel's top and 82 under the row before; the wooden shelf (220 by 58) 16
        /// in from the panel's left, its discs (44 across) in equal columns along it, 27.5 under the row's top, each
        /// with its count's pill 58 under it; at the right, in a column of 100 that ends 16 in from the panel's right,
        /// the set's name 6.75 under the row's top and its button with its middle 40.5 under it.
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

        /// <summary>The small buttons are 100 by 44 with a lip of 5 under them: what a finger may hit.</summary>
        public const float ButtonWidth = 100f;
        public const float ButtonHeight = 44f;
        public const float ButtonLip = 5f;

        /// <summary>
        /// The canvas' gaps, in its points at full size: from the wallet row to the pin's top, from the sign to the
        /// first panel, between two panels, and from the last panel to the top of the bar's raised place. Where the
        /// height is short they give way down to their least, in layout points.
        /// </summary>
        private static readonly float[] DesignGaps = { 10.5f, 17f, 16f, 16f, 24f };
        private static readonly float[] LeastGaps = { 2f, 5f, 4f, 4f, 4f };

        /// <summary>With room to spare, a gap between two parts grows by at most this share of the canvas' gap.</summary>
        private const float MostGrowthBetweenParts = 0.5f;

        /// <summary>Two columns stand this far apart, in layout points.</summary>
        public const float ColumnsApart = 12f;

        private readonly Dictionary<ShopPanel, Box> _panels;

        private ShopLayout(PageLayout page, int packs, int sets)
        {
            Page = page;
            PackCount = packs;
            SetCount = sets;
            ScreenView view = page.View;
            float sideRoom = view.Width - 2f * PageLayout.PlaqueSideMargin;
            FullScale = Math.Min(PageLayout.WidestPlaque, sideRoom) / PanelWidth;
            float boostersHeight = BoostersPanelHeight(sets);
            float signBlock = PinOverTheSign + PinRadius + SignHeight;

            IsTwoColumns = (sideRoom - ColumnsApart) / (2f * PanelWidth) >= SmallestShare * FullScale;
            float[] blocks = IsTwoColumns
                ? new[] { signBlock, Math.Max(CoinsPanelHeight, boostersHeight), FreePanelHeight }
                : new[] { signBlock, CoinsPanelHeight, boostersHeight, FreePanelHeight };
            float[] designGaps = IsTwoColumns ? new[] { DesignGaps[0], DesignGaps[1], DesignGaps[3], DesignGaps[4] } : DesignGaps;
            float[] leastGaps = IsTwoColumns ? new[] { LeastGaps[0], LeastGaps[1], LeastGaps[3], LeastGaps[4] } : LeastGaps;

            float widest = IsTwoColumns ? (sideRoom - ColumnsApart) / (2f * PanelWidth) : FullScale;
            float room = page.WalletRow.Bottom - page.BarReach;
            float content = blocks.Sum();
            float least = leastGaps.Sum();
            Scale = Math.Min(widest, (room - least) / content);
            float[] gaps = GapsFor(room - Scale * content, designGaps.Select(gap => gap * Scale).ToArray(), leastGaps);

            float signTop = page.WalletRow.Bottom - gaps[0] - Scale * (PinOverTheSign + PinRadius);
            Pin = (0f, signTop + Scale * (PinOverTheSign + PinRadius));
            Sign = Box.Around(0f, signTop - Scale * (SignHeight / 2f), Scale * SignWidth, Scale * SignHeight);
            float panelsTop = Sign.Bottom - gaps[1];
            float width = Scale * PanelWidth;
            _panels = new Dictionary<ShopPanel, Box>();
            if (IsTwoColumns)
            {
                _panels[ShopPanel.Coins] = new Box(-ColumnsApart / 2f - width, panelsTop - Scale * CoinsPanelHeight, width, Scale * CoinsPanelHeight);
                _panels[ShopPanel.Boosters] = new Box(ColumnsApart / 2f, panelsTop - Scale * boostersHeight, width, Scale * boostersHeight);
                float freeTop = panelsTop - Scale * blocks[1] - gaps[2];
                _panels[ShopPanel.Free] = new Box(-width / 2f, freeTop - Scale * FreePanelHeight, width, Scale * FreePanelHeight);
            }
            else
            {
                _panels[ShopPanel.Coins] = new Box(-width / 2f, panelsTop - Scale * CoinsPanelHeight, width, Scale * CoinsPanelHeight);
                float boostersTop = _panels[ShopPanel.Coins].Bottom - gaps[2];
                _panels[ShopPanel.Boosters] = new Box(-width / 2f, boostersTop - Scale * boostersHeight, width, Scale * boostersHeight);
                float freeTop = _panels[ShopPanel.Boosters].Bottom - gaps[3];
                _panels[ShopPanel.Free] = new Box(-width / 2f, freeTop - Scale * FreePanelHeight, width, Scale * FreePanelHeight);
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

        /// <summary>The middle of the column of pack <paramref name="pack"/> of <paramref name="packs"/> across its panel, in the canvas' points; the packs are centred as a row.</summary>
        public static float PackMiddle(int pack, int packs)
        {
            float row = packs * PackColumnWidth + (packs - 1) * PackColumnsApart;
            return (PanelWidth - row) / 2f + pack * (PackColumnWidth + PackColumnsApart) + PackColumnWidth / 2f;
        }

        /// <summary>The top of set <paramref name="set"/>'s row under its panel's top, in the canvas' points.</summary>
        public static float SetRowTop(int set) => FirstSetRowDown + set * SetRowsApart;

        /// <summary>The middle of disc <paramref name="disc"/> of a shelf of <paramref name="discs"/> across its panel, in the canvas' points.</summary>
        public static float DiscMiddle(int disc, int discs) => ShelfLeft + ShelfInset + (disc + 0.5f) * (ShelfWidth - 2f * ShelfInset) / discs;

        /// <summary>What a finger may hit of a pack's button, in layout points: the pill and its lip.</summary>
        public Box PackButton(int pack) => HeldButton(ShopPanel.Coins, PackMiddle(pack, PackCount), PacksDown + PackButtonDown);

        /// <summary>What a finger may hit of a set's button.</summary>
        public Box SetButton(int set) => HeldButton(ShopPanel.Boosters, SetColumnMiddle, SetRowTop(set) + SetButtonDown);

        /// <summary>What a finger may hit of the free strip's button.</summary>
        public Box AdButton => HeldButton(ShopPanel.Free, SetColumnMiddle, FreeButtonDown);

        /// <summary>Every button of the shop, as a finger may hit it.</summary>
        public IEnumerable<Box> Buttons =>
            Enumerable.Range(0, PackCount).Select(PackButton).Concat(Enumerable.Range(0, SetCount).Select(SetButton)).Append(AdButton);

        /// <summary>The shop for a screen and for the goods the settings have: so many packs of coins and so many sets.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No pack or no set, or more packs than a panel holds side by side.</exception>
        public static ShopLayout Of(ScreenShape screen, int packs, int sets)
        {
            if (packs < 1 || packs > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(packs), packs, "The coins' panel holds one to three packs side by side.");
            }

            if (sets < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sets), sets, "The sets' panel holds a row for each set, and there is at least one.");
            }

            return new ShopLayout(PageLayout.Of(screen), packs, sets);
        }

        /// <summary>
        /// The gaps as the room left over by the parts gives them. Where there is more than the canvas' gaps, the gaps
        /// between the parts take an even share of it up to half as much again, so the parts stay together, and the
        /// rest goes above the sign and under the last panel alike: the shop stands in the middle of its room. Where
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
        private Box HeldButton(ShopPanel panel, float across, float down)
        {
            (float x, float y) = InPanel(panel, across, down);
            float top = y + ButtonHeight / 2f * Scale;
            float bottom = y - (ButtonHeight / 2f + ButtonLip) * Scale;
            return new Box(x - ButtonWidth / 2f * Scale, bottom, ButtonWidth * Scale, top - bottom);
        }
    }
}
