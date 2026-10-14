#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Museum;

namespace Puzzle.Client.Layout
{
    /// <summary>What a row of the map's floor plan shows.</summary>
    public enum PlanRowKind
    {
        /// <summary>The rest of the museum past the rooms drawn ahead, as one stretch.</summary>
        Beyond,

        /// <summary>Two rooms ahead, side by side (or the last one alone).</summary>
        Ahead,

        /// <summary>The room after the player's.</summary>
        Next,

        /// <summary>The room the player stands in.</summary>
        Here,

        /// <summary>The room the player left last.</summary>
        Before,

        /// <summary>The rooms before that, as one stretch.</summary>
        Behind,
    }

    /// <summary>One row of the floor plan: what it shows, and its floor between the plan's side walls.</summary>
    public readonly struct PlanRow
    {
        public PlanRow(PlanRowKind kind, int rowAhead, Box floor)
        {
            Kind = kind;
            RowAhead = rowAhead;
            Floor = floor;
        }

        public PlanRowKind Kind { get; }

        /// <summary>For a row of rooms ahead: which one, counted from 0 at the row nearest the player. 0 for every other row.</summary>
        public int RowAhead { get; }

        public Box Floor { get; }
    }

    /// <summary>
    /// Where everything of the museum's map lies: under the page's plaque and rail hangs the floor plan, a sheet in a
    /// gilt frame, over the navigation bar. The plan is drawn from the entrance at its foot upwards, a row for each
    /// part of <see cref="FloorPlan"/> with a wall between rows. The room the player stands in and the next one are
    /// always drawn in full; how many more rooms are drawn by themselves is what the sheet's height allows, and all
    /// other rooms are in a stretch, so every screen shows the whole museum. No Unity dependency.
    /// </summary>
    /// <remarks>
    /// A short screen takes height from the map in this order: first the rows lose what they grow by on a tall
    /// screen; then the plan draws fewer rooms by themselves (the second row of rooms ahead, the first, and last
    /// the room left behind); then the wall over and under the frame narrows to its least; and last the rows that
    /// stand for a stretch of rooms are drawn lower, down to what still holds their numbers. The rooms drawn in
    /// full never lose what their furnishing needs.
    /// </remarks>
    public sealed class MapLayout
    {
        /// <summary>The frame is this wide on every side of the sheet (the frame's own drawing).</summary>
        public const float FrameWidth = HallLayout.FrameWidth;

        /// <summary>The widest sheet with its frame, and the wall it keeps at each side of a phone's screen.</summary>
        public const float WidestFrame = 384f;
        private const float WallBesideFrame = 10f;

        /// <summary>The plan's walls, and how far the outer walls stand in from the sheet's edges.</summary>
        public const float WallThickness = 6.5f;
        public const float SheetSideMargin = 12f;
        private const float SheetTopMargin = 8f;

        /// <summary>Under the plan's lowest wall the sheet has room for the entrance's door swing and its label.</summary>
        public const float SheetFootMargin = 22f;

        /// <summary>The wall over the frame and under it, on a tight screen and a roomy one, and the least either narrows to on a short screen.</summary>
        private const float TightWallOverTheFrame = 14f;
        private const float RoomyWallOverTheFrame = 22f;
        private const float TightWallUnderTheFrame = 12f;
        private const float RoomyWallUnderTheFrame = 20f;
        private const float LeastWallAroundTheFrame = 6f;

        /// <summary>A row that stands for a stretch of rooms holds only their numbers: on a short screen it is drawn as low as this.</summary>
        private const float LowestStretch = 24f;

        /// <summary>
        /// The depths a map may have, from the one that shows most to the one every screen has room for. The room
        /// left last tells more than a second row of rooms that are still locked, so it is given up later.
        /// </summary>
        private static readonly PlanDepth[] DepthsByPreference =
        {
            new PlanDepth(showsTheRoomBefore: true, rowsAhead: 2),
            new PlanDepth(showsTheRoomBefore: true, rowsAhead: 1),
            new PlanDepth(showsTheRoomBefore: true, rowsAhead: 0),
            new PlanDepth(showsTheRoomBefore: false, rowsAhead: 1),
            new PlanDepth(showsTheRoomBefore: false, rowsAhead: 0),
        };

        private MapLayout(PageLayout page, FloorPlan plan, Box sheet, IReadOnlyList<PlanRow> rows)
        {
            Page = page;
            Plan = plan;
            Sheet = sheet;
            Rows = rows;
        }

        public PageLayout Page { get; }

        /// <summary>The plan as this screen has room to draw it.</summary>
        public FloorPlan Plan { get; }

        /// <summary>The sheet the plan is drawn on, inside its frame.</summary>
        public Box Sheet { get; }

        /// <summary>The frame's outer edge.</summary>
        public Box Frame => new Box(Sheet.Left - FrameWidth, Sheet.Bottom - FrameWidth, Sheet.Width + 2 * FrameWidth, Sheet.Height + 2 * FrameWidth);

        /// <summary>The plan's rows from the top of the sheet down.</summary>
        public IReadOnlyList<PlanRow> Rows { get; }

        /// <summary>The floor of the row of a kind; nothing when this plan has no such row.</summary>
        public Box? FloorOf(PlanRowKind kind)
        {
            foreach (PlanRow row in Rows)
            {
                if (row.Kind == kind)
                {
                    return row.Floor;
                }
            }

            return null;
        }

        /// <summary>
        /// The floor of one of the rooms drawn ahead. The rooms run as a visitor would walk them: the first row
        /// from left to right, the row above it from right to left. A room alone in its row has the whole row.
        /// </summary>
        /// <param name="index">Which room of <see cref="FloorPlan.Ahead"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The plan draws no such room.</exception>
        public Box FloorOfRoomAhead(int index)
        {
            if (index < 0 || index >= Plan.Ahead.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"The plan draws {Plan.Ahead.Count} rooms ahead.");
            }

            int rowAhead = index / FloorPlan.RoomsInARow;
            Box row = RowAhead(rowAhead);
            bool isAloneInItsRow = index == Plan.Ahead.Count - 1 && index % FloorPlan.RoomsInARow == 0;
            if (isAloneInItsRow)
            {
                return row;
            }

            float roomWidth = (row.Width - WallThickness) / FloorPlan.RoomsInARow;
            bool isWalkedLeftToRight = rowAhead % 2 == 0;
            bool isFirstInItsRow = index % FloorPlan.RoomsInARow == 0;
            bool isAtTheLeft = isFirstInItsRow == isWalkedLeftToRight;
            return new Box(isAtTheLeft ? row.Left : row.Right - roomWidth, row.Bottom, roomWidth, row.Height);
        }

        /// <param name="here">The room the player stands in.</param>
        public static MapLayout Of(ScreenShape screen, RoomNumber here)
        {
            PageLayout page = PageLayout.Of(screen);
            float frameWidth = Math.Min(WidestFrame, page.View.Width - 2 * WallBesideFrame);
            float sheetWidth = page.View.OnWholePixels(frameWidth - 2 * FrameWidth);
            float wallOver = page.Between(TightWallOverTheFrame, RoomyWallOverTheFrame);
            float wallUnder = page.Between(TightWallUnderTheFrame, RoomyWallUnderTheFrame);
            float TallestSheetBetween(float over, float under) => SheetTopUnder(page, over) - (page.BarReach + under + FrameWidth);

            FloorPlan plan = DeepestPlanThatFits(here, TallestSheetBetween(wallOver, wallUnder)) ?? FloorPlan.Of(here, DepthsByPreference[DepthsByPreference.Length - 1]);
            List<(PlanRowKind Kind, int RowAhead)> parts = PartsOf(plan);

            // Where not even the shallowest plan fits, the wall around the frame narrows as far as it must.
            float missing = SheetHeightOf(parts, RowMeasure.Least) - TallestSheetBetween(wallOver, wallUnder);
            float canNarrow = wallOver + wallUnder - 2 * LeastWallAroundTheFrame;
            if (missing > 0f && canNarrow > 0f)
            {
                float narrowed = Math.Min(1f, missing / canNarrow);
                wallOver -= narrowed * (wallOver - LeastWallAroundTheFrame);
                wallUnder -= narrowed * (wallUnder - LeastWallAroundTheFrame);
            }

            float sheetTop = SheetTopUnder(page, wallOver);
            float tallestSheet = TallestSheetBetween(wallOver, wallUnder);
            float[] heights = HeightsOf(parts, tallestSheet);

            float floorLeft = -sheetWidth / 2 + SheetSideMargin + WallThickness;
            float floorWidth = sheetWidth - 2 * (SheetSideMargin + WallThickness);
            var rows = new List<PlanRow>();
            float y = sheetTop - SheetTopMargin;
            for (int part = 0; part < parts.Count; part++)
            {
                y -= WallThickness;
                rows.Add(new PlanRow(parts[part].Kind, parts[part].RowAhead, new Box(floorLeft, y - heights[part], floorWidth, heights[part])));
                y -= heights[part];
            }

            float sheetBottom = y - WallThickness - SheetFootMargin;
            return new MapLayout(page, plan, new Box(-sheetWidth / 2, sheetBottom, sheetWidth, sheetTop - sheetBottom), rows);
        }

        private Box RowAhead(int rowAhead)
        {
            foreach (PlanRow row in Rows)
            {
                if (row.Kind == PlanRowKind.Ahead && row.RowAhead == rowAhead)
                {
                    return row.Floor;
                }
            }

            throw new InvalidOperationException($"The plan has no row {rowAhead} of rooms ahead.");
        }

        /// <summary>The top of the sheet, inside the frame that hangs this far under the picture rail.</summary>
        private static float SheetTopUnder(PageLayout page, float wallOverTheFrame) =>
            page.View.OnWholePixels(page.RailBottom - wallOverTheFrame - FrameWidth);

        /// <summary>The plan that draws the most rooms by themselves and fits the sheet at its rows' least heights; nothing where not even the shallowest fits.</summary>
        private static FloorPlan? DeepestPlanThatFits(RoomNumber here, float tallestSheet)
        {
            foreach (PlanDepth depth in DepthsByPreference)
            {
                FloorPlan plan = FloorPlan.Of(here, depth);
                if (SheetHeightOf(PartsOf(plan), RowMeasure.Least) <= tallestSheet)
                {
                    return plan;
                }
            }

            return null;
        }

        /// <summary>The rows a plan has, from the top of the sheet down.</summary>
        private static List<(PlanRowKind Kind, int RowAhead)> PartsOf(FloorPlan plan)
        {
            var parts = new List<(PlanRowKind, int)>();
            if (plan.Beyond != null)
            {
                parts.Add((PlanRowKind.Beyond, 0));
            }

            for (int rowAhead = plan.RowsAhead - 1; rowAhead >= 0; rowAhead--)
            {
                parts.Add((PlanRowKind.Ahead, rowAhead));
            }

            if (plan.Next != null)
            {
                parts.Add((PlanRowKind.Next, 0));
            }

            parts.Add((PlanRowKind.Here, 0));
            if (plan.Before != null)
            {
                parts.Add((PlanRowKind.Before, 0));
            }

            if (plan.Behind != null)
            {
                parts.Add((PlanRowKind.Behind, 0));
            }

            return parts;
        }

        private static float SheetHeightOf(List<(PlanRowKind Kind, int RowAhead)> parts, RowMeasure measure)
        {
            float height = SheetTopMargin + SheetFootMargin + (parts.Count + 1) * WallThickness;
            foreach ((PlanRowKind kind, _) in parts)
            {
                height += measure == RowMeasure.Least ? LeastHeightOf(kind) : LeastHeightOf(kind) + RoomToGrowOf(kind);
            }

            return height;
        }

        /// <summary>
        /// Each row's height: its least, and of what the sheet has left over, a share by how much the row may grow.
        /// A sheet lower than the rows' least takes what is missing from the rows of stretches, alike, as far as
        /// they can give it; no screen the game supports is shorter than that, and the plan's foot would then run
        /// under the bar.
        /// </summary>
        private static float[] HeightsOf(List<(PlanRowKind Kind, int RowAhead)> parts, float tallestSheet)
        {
            float least = SheetHeightOf(parts, RowMeasure.Least);
            float most = SheetHeightOf(parts, RowMeasure.Most);
            float grown = most > least ? Math.Clamp((tallestSheet - least) / (most - least), 0f, 1f) : 0f;
            int stretches = parts.Count(part => IsAStretch(part.Kind));
            float lowered = stretches > 0 ? Math.Max(0f, least - tallestSheet) / stretches : 0f;
            var heights = new float[parts.Count];
            for (int part = 0; part < parts.Count; part++)
            {
                PlanRowKind kind = parts[part].Kind;
                heights[part] = IsAStretch(kind)
                    ? Math.Max(LowestStretch, LeastHeightOf(kind) - lowered) + grown * RoomToGrowOf(kind)
                    : LeastHeightOf(kind) + grown * RoomToGrowOf(kind);
            }

            return heights;
        }

        private static bool IsAStretch(PlanRowKind kind) => kind == PlanRowKind.Beyond || kind == PlanRowKind.Behind;

        /// <summary>The least height of a row's floor: what stands in it needs that much.</summary>
        private static float LeastHeightOf(PlanRowKind kind) => kind switch
        {
            PlanRowKind.Here => 120f,
            PlanRowKind.Next => 76f,
            PlanRowKind.Before => 70f,
            PlanRowKind.Ahead => 50f,
            PlanRowKind.Beyond => 32f,
            PlanRowKind.Behind => 32f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A row of the plan without a height."),
        };

        /// <summary>How much higher than its least a row is drawn where the sheet has the room.</summary>
        private static float RoomToGrowOf(PlanRowKind kind) => kind switch
        {
            PlanRowKind.Here => 30f,
            PlanRowKind.Next => 24f,
            PlanRowKind.Before => 20f,
            PlanRowKind.Ahead => 26f,
            PlanRowKind.Beyond => 16f,
            PlanRowKind.Behind => 16f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A row of the plan without a height."),
        };

        /// <summary>Which of a row's two heights is meant.</summary>
        private enum RowMeasure
        {
            Least,
            Most,
        }
    }
}
