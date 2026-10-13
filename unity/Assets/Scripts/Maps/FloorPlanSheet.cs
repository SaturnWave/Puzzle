#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Museum;
using TMPro;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The museum's floor plan on its sheet, as the design canvas draws it (MapTR): rooms as floors between walls,
    /// from the entrance at the foot of the sheet upwards. The room the player stands in is the one in gilt walls on
    /// a lit wooden floor, with its paintings and the pin; rooms that are open have their floor and their paintings;
    /// locked rooms are hatched, the next one with its padlock and the level that opens it; rooms far ahead fade
    /// into the paper. Which rooms are drawn by themselves and where is <see cref="MapLayout"/>'s; this draws them.
    /// The sheet is drawn anew for every layout it is shown in.
    /// </summary>
    public sealed class FloorPlanSheet : IDisposable
    {
        private static readonly Color Paper = Tint.Of(0xF6ECD2);
        private static readonly Color PaperRule = Tint.Of(0xB8985A, 0.55f);
        private const float PaperRuleInset = 5.5f;

        /// <summary>A locked room's floor: pale teal under a hatch of slanted lines.</summary>
        private static readonly Color LockedFloor = Tint.Of(0xC9DCD6, 0.5f);
        private static readonly Color Hatch = Tint.Of(0x8A6A3A, 0.3f);
        private const float HatchWidth = 1.2f;
        private const float HatchEvery = 8f;

        /// <summary>An open room's floor: boards in a warm light, brightest in the room the player stands in.</summary>
        private static readonly ColourRamp HereFloor = new ColourRamp((0f, Tint.Of(0xFFF6DC)), (0.55f, Tint.Of(0xF9E1AE)), (1f, Tint.Of(0xEDC788)));
        private static readonly Color OpenFloor = Tint.Of(0xF7E6BF);
        private static readonly Color BoardLine = Tint.Of(0x8A5524, 0.16f);
        private const float BoardsEvery = 11f;

        /// <summary>The shade a room's upper and left walls throw on its floor.</summary>
        private static readonly Color FloorShade = Tint.Of(0x2A1606, 0.36f);
        private const float FloorShadeFromTop = 9f;
        private const float FloorShadeFromLeft = 8f;

        /// <summary>The three kinds of wall: paper walls for rooms told by their number alone, wooden ones for a room drawn in full, gilt for the player's.</summary>
        private static readonly Color PaperWall = Tint.Of(0xEFE3C6);
        private static readonly Color PaperWallEdge = Tint.Of(0x8A6A3A);
        private static readonly Color WoodWall = Tint.Of(0xA9773F);
        private static readonly Color WallEdge = Tint.Of(0x5E3305);
        private static readonly Color GiltLit = Tint.Of(0xFFF1B4);
        private static readonly Color GiltWall = Tint.Of(0xF2B63C);
        private static readonly Color GiltShaded = Tint.Of(0xC98A1E);
        private static readonly Color GiltDark = Tint.Of(0x9C5C0B);
        private const float WallEdgeWidth = 1.2f;

        /// <summary>A doorway: a gap in a wall with a thin line for its threshold; shut, it is a dark bar with a light line.</summary>
        private const float DoorWidth = 30f;
        private static readonly Color Threshold = Tint.Of(0x8A6A3A);
        private const float ThresholdWidth = 1.3f;
        private static readonly Color ShutDoor = Tint.Of(0x5E3305);
        private static readonly Color ShutDoorLight = Tint.Of(0xC99255);

        /// <summary>The entrance in the plan's foot wall: two posts, the swing of its two door leaves, and its name beside it (MapTR, "GİRİŞ": 9 points).</summary>
        private const float EntranceWidth = 36f;
        private const float EntranceDepth = 18f;
        private static readonly Color EntrancePost = Tint.Of(0x5E3305);
        private static readonly Color EntrancePostLight = Tint.Of(0xF6B93C);
        private static readonly Color EntranceSwing = Tint.Of(0x8A6A3A);
        private const float EntranceLabelSize = 9f;
        private static readonly Color EntranceInk = Tint.Of(0x7A5A2E);

        /// <summary>The paper over the far rooms: none of it at the top of the next room, all of it at the sheet's top edge.</summary>
        private static readonly (float Up, float Strength)[] Fade = { (0f, 0f), (0.22f, 0.26f), (0.56f, 0.6f), (0.8f, 0.9f), (1f, 1f) };

        /// <summary>The numbers of rooms told by their number alone: nearer rows darker, a stretch of rooms palest.</summary>
        private static readonly Color NearNumber = Tint.Of(0x7A5A2E);
        private static readonly Color FarNumber = Tint.Of(0x93744A);
        private static readonly Color StretchNumber = Tint.Of(0x96784E);
        private const float LargestNumber = 34f;
        private const float LargestStretchNumber = 24f;

        /// <summary>
        /// A number is this share of its floor's height at the most, as the design has them (MapTR): a room's 34
        /// points on a floor of 69 and 28 on one of 59, a stretch of rooms' 24 on a floor of 45.
        /// </summary>
        private const float NumberOfFloorHeight = 0.48f;
        private const float StretchNumberOfFloorHeight = 0.54f;

        /// <summary>
        /// A room drawn in full: its badge or padlock at the left, two lines beside it, its paintings, a pill. The
        /// type sizes are the design's (MapTR): the number on the badge 18 points, the room's name in the serif 16.
        /// </summary>
        private const float BadgeIn = 28f;
        private const float BadgeSize = 34f;
        private const float BadgeNumberSize = 18f;
        private const float LinesBeginIn = 54f;
        private const float LabelOverBadgeMiddle = 7.5f;
        private const float NameUnderBadgeMiddle = 7f;
        private const float NameSize = 16f;
        private const float PicturesBeginIn = 14f;
        private const float CountAfterPictures = 13f;
        private static readonly Color SheetLabelInk = Tint.Of(0x5E3305);
        private const float PadlockIn = 39f;
        private const float PadlockSize = 46f;
        private const float PadlockBox = 100f;
        private const float LinesBesidePadlockIn = 75f;

        /// <summary>
        /// A pill: its picture is the pill and the room around it for its rim and drop, 16 points wider and 40 high
        /// for a pill of 24. Its line is the design's (MapTR, "Buradasın · Bölüm 47": 11.5 points).
        /// </summary>
        private const float PillHeight = 24f;
        private const float PillPictureMargin = 16f;
        private const float PillPictureHeight = 40f;
        private const float PillLineSize = 11.5f;
        private const float PillPadding = 12f;
        private const float SmallPillScale = 22f / 24f;

        /// <summary>The pin stands on the left end of the "you are here" pill, which leaves it this much room before its line.</summary>
        private const float PinRoomInPill = 33f;
        private const float PinPointIn = 16.5f;

        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly MuseumVisit _visit;

        /// <summary>What the sheet is made of as it is drawn now; all of it is taken down before the sheet is drawn anew.</summary>
        private readonly List<IDisposable> _drawn = new List<IDisposable>();
        private Transform? _content;

        private FloorPlanSheet(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit)
        {
            _art = art;
            _paintings = paintings;
            _visit = visit;
            _root = new GameObject("Floor plan").transform;
            _root.SetParent(parent, worldPositionStays: false);
        }

        public static FloorPlanSheet Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit) =>
            new FloorPlanSheet(parent, art, paintings, visit);

        /// <summary>Draws the plan as a layout has it.</summary>
        public void Show(MapLayout layout)
        {
            TakeDown();
            _content = new GameObject("Sheet").transform;
            _content.SetParent(_root, worldPositionStays: false);
            float softEdge = 1f / layout.Page.View.PixelsPerPoint;

            var ground = new MeshSketch();
            var floors = new MeshSketch();
            var paperWalls = new MeshSketch();
            var strongWalls = new MeshSketch();
            AddPaper(ground, layout.Sheet);
            foreach (PlanRow row in layout.Rows)
            {
                AddFloors(floors, row, layout, softEdge);
            }

            AddFade(strongWalls, layout);
            AddWalls(paperWalls, strongWalls, layout);
            AddEntrance(strongWalls, layout, softEdge);

            Paint("Paper", DrawOrder.Canvas, ground);
            Paint("Floors", DrawOrder.CanvasPicture, floors);
            Paint("Paper walls", DrawOrder.CanvasLines, paperWalls);
            Paint("Walls", DrawOrder.SheetWalls, strongWalls);
            foreach (PlanRow row in layout.Rows)
            {
                Furnish(row, layout);
            }
        }

        public void Dispose()
        {
            TakeDown();
            SceneObjects.DestroyObjectOf(_root);
        }

        private void TakeDown()
        {
            foreach (IDisposable drawn in _drawn)
            {
                drawn.Dispose();
            }

            _drawn.Clear();
            if (_content != null)
            {
                SceneObjects.DestroyObjectOf(_content);
                _content = null;
            }
        }

        private Transform Content => _content != null ? _content : throw new InvalidOperationException("The sheet is not being drawn.");

        private void Paint(string name, DrawOrder order, MeshSketch sketch)
        {
            PaintedShape shape = PaintedShape.Create(name, Content, _art.PaintMaterial, order);
            shape.Show(sketch.ToMesh(name));
            _drawn.Add(shape);
        }

        private RoomAccess AccessOf(RoomNumber room) => FloorPlan.AccessOf(room, _visit.Progress, _paintings.Catalogue);

        private bool IsClosed(RoomNumber room) => FloorPlan.IsClosed(AccessOf(room));

        // ---- the paper, the floors -------------------------------------------------------------------------------

        private static void AddPaper(MeshSketch sketch, Box sheet)
        {
            sketch.AddRectangle(RectOf(sheet), Paper);
            AddOutline(sketch, Shrunk(RectOf(sheet), PaperRuleInset), 1f, PaperRule);
        }

        private void AddFloors(MeshSketch sketch, PlanRow row, MapLayout layout, float softEdge)
        {
            FloorPlan plan = layout.Plan;
            switch (row.Kind)
            {
                case PlanRowKind.Here:
                    AddLitFloor(sketch, row.Floor);
                    break;
                case PlanRowKind.Next when plan.Next is RoomNumber next:
                    AddFloor(sketch, row.Floor, AccessOf(next), softEdge);
                    break;
                case PlanRowKind.Before when plan.Before is RoomNumber before:
                    AddFloor(sketch, row.Floor, AccessOf(before), softEdge);
                    break;
                case PlanRowKind.Ahead:
                    for (int room = row.RowAhead * FloorPlan.RoomsInARow; room < Math.Min(plan.Ahead.Count, (row.RowAhead + 1) * FloorPlan.RoomsInARow); room++)
                    {
                        AddFloor(sketch, layout.FloorOfRoomAhead(room), AccessOf(plan.Ahead[room]), softEdge);
                    }

                    break;
                case PlanRowKind.Beyond when plan.Beyond is RoomStretch beyond:
                    AddFloor(sketch, row.Floor, FloorPlan.AccessOf(beyond, _visit.Progress, _paintings.Catalogue), softEdge);
                    break;
                case PlanRowKind.Behind when plan.Behind is RoomStretch behind:
                    AddFloor(sketch, row.Floor, FloorPlan.AccessOf(behind, _visit.Progress, _paintings.Catalogue), softEdge);
                    break;
                default:
                    throw new InvalidOperationException($"The layout has a row for {row.Kind} that the plan has no room for.");
            }
        }

        private static void AddFloor(MeshSketch sketch, Box floor, RoomAccess access, float softEdge)
        {
            Rect area = RectOf(floor);
            if (FloorPlan.IsClosed(access))
            {
                sketch.AddRectangle(area, LockedFloor);
                AddHatch(sketch, area, softEdge);
            }
            else
            {
                sketch.AddRectangle(area, OpenFloor);
                AddBoards(sketch, area);
                AddFloorShade(sketch, area, strength: 0.6f);
            }
        }

        /// <summary>The floor of the room the player stands in: lit from its middle, darker towards its walls.</summary>
        private static void AddLitFloor(MeshSketch sketch, Box floor)
        {
            Rect area = RectOf(floor);
            float reach = 0.66f * area.width;
            sketch.AddNet(area, columns: 12, rows: 6, point => HereFloor.At(Vector2.Distance(point, area.center) / reach));
            AddBoards(sketch, area);
            AddFloorShade(sketch, area, strength: 1f);
        }

        /// <summary>Floorboards: thin lines across the room, and short ones across them where one board ends and the next begins.</summary>
        private static void AddBoards(MeshSketch sketch, Rect floor)
        {
            int board = 0;
            for (float y = floor.yMax - BoardsEvery; y > floor.yMin + 2f; y -= BoardsEvery, board++)
            {
                sketch.AddRectangle(new Rect(floor.xMin, y, floor.width, 1f), BoardLine);
                float firstEnd = floor.xMin + (board % 2 == 0 ? 0.17f : 0.42f) * floor.width;
                for (float x = firstEnd; x < floor.xMax - 4f; x += 0.5f * floor.width)
                {
                    sketch.AddRectangle(new Rect(x, y, 1f, BoardsEvery), BoardLine);
                }
            }
        }

        private static void AddFloorShade(MeshSketch sketch, Rect floor, float strength)
        {
            Color shade = FloorShade;
            shade.a *= strength;
            Color none = new Color(shade.r, shade.g, shade.b, 0f);
            sketch.AddRectangle(new Rect(floor.xMin, floor.yMax - FloorShadeFromTop, floor.width, FloorShadeFromTop), none, shade, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
            int nearTop = sketch.AddCorner(new Vector2(floor.xMin, floor.yMax), shade);
            int farTop = sketch.AddCorner(new Vector2(floor.xMin + FloorShadeFromLeft, floor.yMax), none);
            int farFoot = sketch.AddCorner(new Vector2(floor.xMin + FloorShadeFromLeft, floor.yMin), none);
            int nearFoot = sketch.AddCorner(new Vector2(floor.xMin, floor.yMin), shade);
            sketch.AddQuad(nearTop, farTop, farFoot, nearFoot);
        }

        /// <summary>Slanted lines over a floor, each cut where it leaves the floor.</summary>
        private static void AddHatch(MeshSketch sketch, Rect floor, float softEdge)
        {
            // A line of the hatch is x - y = c; c runs from the floor's upper left corner to its lower right one.
            float first = floor.xMin - floor.yMax;
            float last = floor.xMax - floor.yMin;
            float step = HatchEvery * Mathf.Sqrt(2f);
            for (float c = first + step / 2f; c < last; c += step)
            {
                float fromX = Mathf.Max(floor.xMin, c + floor.yMin);
                float toX = Mathf.Min(floor.xMax, c + floor.yMax);
                if (toX - fromX > 1f)
                {
                    var line = new List<Vector2> { new Vector2(fromX, fromX - c), new Vector2(toX, toX - c) };
                    Stroke.Add(sketch, line, isClosed: false, HatchWidth, softEdge, Hatch);
                }
            }
        }

        // ---- the walls -------------------------------------------------------------------------------------------

        /// <summary>What a wall is made of. The order is from the plainest up: where two rows share a wall, it is the stronger one's.</summary>
        private enum WallPaint
        {
            Paper,
            Wood,
            Gilt,
        }

        /// <summary>Which side of its room a wall is: gilt is lit from the upper left.</summary>
        private enum WallSide
        {
            Top,
            Foot,
            Left,
            Right,
        }

        private static WallPaint PaintOf(PlanRowKind kind) => kind switch
        {
            PlanRowKind.Here => WallPaint.Gilt,
            PlanRowKind.Next => WallPaint.Wood,
            PlanRowKind.Before => WallPaint.Wood,
            PlanRowKind.Ahead => WallPaint.Paper,
            PlanRowKind.Beyond => WallPaint.Paper,
            PlanRowKind.Behind => WallPaint.Paper,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A row of the plan without walls."),
        };

        /// <summary>
        /// Every wall of the plan: for each row the wall over it and the walls at its two sides, then the foot wall
        /// under the last row. The wall between two rows has a doorway; the plan's rooms are walked through them.
        /// </summary>
        private void AddWalls(MeshSketch paperWalls, MeshSketch strongWalls, MapLayout layout)
        {
            const float Thickness = MapLayout.WallThickness;
            IReadOnlyList<PlanRow> rows = layout.Rows;
            var walls = new List<(Rect Area, WallPaint Paint, WallSide Side)>();
            for (int index = 0; index < rows.Count; index++)
            {
                Rect floor = RectOf(rows[index].Floor);
                WallPaint own = PaintOf(rows[index].Kind);
                WallPaint above = index > 0 ? PaintOf(rows[index - 1].Kind) : own;
                bool isTheRoomsOwn = own >= above;
                var over = new Rect(floor.xMin - Thickness, floor.yMax, floor.width + 2f * Thickness, Thickness);
                WallPaint overPaint = isTheRoomsOwn ? own : above;
                WallSide overSide = isTheRoomsOwn ? WallSide.Top : WallSide.Foot;
                if (index == 0)
                {
                    walls.Add((over, overPaint, overSide));
                }
                else
                {
                    float doorLeft = floor.xMin + DoorAcross(index) * floor.width - DoorWidth / 2f;
                    walls.Add((new Rect(over.xMin, over.y, doorLeft - over.xMin, Thickness), overPaint, overSide));
                    walls.Add((new Rect(doorLeft + DoorWidth, over.y, over.xMax - doorLeft - DoorWidth, Thickness), overPaint, overSide));
                    AddDoor(strongWalls, new Rect(doorLeft, over.y, DoorWidth, Thickness), isShut: IsShutBetween(rows[index - 1], rows[index], layout.Plan));
                }

                walls.Add((new Rect(floor.xMin - Thickness, floor.yMin, Thickness, floor.height), own, WallSide.Left));
                walls.Add((new Rect(floor.xMax, floor.yMin, Thickness, floor.height), own, WallSide.Right));
                if (rows[index].Kind == PlanRowKind.Ahead && RoomsInRowAhead(rows[index], layout.Plan) == FloorPlan.RoomsInARow)
                {
                    AddWallBetweenTwoRooms(walls, strongWalls, floor);
                }
            }

            Rect lowest = RectOf(rows[rows.Count - 1].Floor);
            WallPaint footPaint = PaintOf(rows[rows.Count - 1].Kind);
            float entranceLeft = lowest.center.x - EntranceWidth / 2f;
            walls.Add((new Rect(lowest.xMin - Thickness, lowest.yMin - Thickness, entranceLeft - lowest.xMin + Thickness, Thickness), footPaint, WallSide.Foot));
            walls.Add((new Rect(entranceLeft + EntranceWidth, lowest.yMin - Thickness, lowest.xMax + Thickness - entranceLeft - EntranceWidth, Thickness), footPaint, WallSide.Foot));

            // Edges first and faces over them, the plain walls under the strong ones: where walls meet they run into one another.
            foreach (WallPaint paint in new[] { WallPaint.Paper, WallPaint.Wood, WallPaint.Gilt })
            {
                MeshSketch sketch = paint == WallPaint.Paper ? paperWalls : strongWalls;
                foreach ((Rect area, WallPaint wallPaint, _) in walls)
                {
                    if (wallPaint == paint)
                    {
                        sketch.AddRectangle(Shrunk(area, -WallEdgeWidth), paint == WallPaint.Paper ? PaperWallEdge : WallEdge);
                    }
                }

                foreach ((Rect area, WallPaint wallPaint, WallSide side) in walls)
                {
                    if (wallPaint == paint)
                    {
                        AddWallFace(sketch, area, paint, side);
                    }
                }
            }
        }

        private static void AddWallFace(MeshSketch sketch, Rect area, WallPaint paint, WallSide side)
        {
            switch (paint)
            {
                case WallPaint.Paper:
                    sketch.AddRectangle(area, PaperWall);
                    break;
                case WallPaint.Wood:
                    sketch.AddRectangle(area, WoodWall);
                    break;
                case WallPaint.Gilt:
                    (Color lower, Color upper) = side switch
                    {
                        WallSide.Top => (GiltWall, GiltLit),
                        WallSide.Left => (GiltWall, GiltLit),
                        WallSide.Right => (GiltShaded, GiltWall),
                        WallSide.Foot => (GiltDark, GiltShaded),
                        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "A wall has four sides."),
                    };
                    sketch.AddRectangle(area, lower, upper, new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(paint), paint, "A wall without a paint.");
            }
        }

        /// <summary>How far across its wall a doorway lies: the doorways alternate between the two sides, so the way through the rooms winds.</summary>
        private static float DoorAcross(int wallIndex) => wallIndex % 2 == 0 ? 0.2f : 0.8f;

        /// <summary>A door is shut between a room the player can enter and one that is locked.</summary>
        private bool IsShutBetween(PlanRow upper, PlanRow lower, FloorPlan plan) =>
            IsLocked(upper, plan) != IsLocked(lower, plan);

        private bool IsLocked(PlanRow row, FloorPlan plan) => row.Kind switch
        {
            PlanRowKind.Here => false,
            PlanRowKind.Next => plan.Next is RoomNumber next && IsClosed(next),
            PlanRowKind.Before => false,
            PlanRowKind.Behind => false,
            PlanRowKind.Ahead => IsClosed(plan.Ahead[row.RowAhead * FloorPlan.RoomsInARow]),
            PlanRowKind.Beyond => plan.Beyond is RoomStretch beyond && IsClosed(beyond.First),
            _ => throw new ArgumentOutOfRangeException(nameof(row), row.Kind, "A row of the plan that is neither open nor locked."),
        };

        private static int RoomsInRowAhead(PlanRow row, FloorPlan plan) =>
            Math.Min(FloorPlan.RoomsInARow, plan.Ahead.Count - row.RowAhead * FloorPlan.RoomsInARow);

        private static void AddDoor(MeshSketch sketch, Rect doorway, bool isShut)
        {
            float middle = doorway.center.y;
            if (isShut)
            {
                sketch.AddRectangle(new Rect(doorway.x, middle - 1.8f, doorway.width, 3.6f), ShutDoor);
                sketch.AddRectangle(new Rect(doorway.x + 1.2f, middle - 0.15f, doorway.width - 2.4f, 1.3f), ShutDoorLight);
            }
            else
            {
                sketch.AddRectangle(new Rect(doorway.x, middle - ThresholdWidth / 2f, doorway.width, ThresholdWidth), Threshold);
            }
        }

        /// <summary>The wall between the two rooms of a row, with a doorway in its middle.</summary>
        private static void AddWallBetweenTwoRooms(List<(Rect Area, WallPaint Paint, WallSide Side)> walls, MeshSketch thresholds, Rect floor)
        {
            const float Thickness = MapLayout.WallThickness;
            float left = floor.center.x - Thickness / 2f;
            float doorHeight = Mathf.Min(DoorWidth, 0.45f * floor.height);
            float doorFoot = floor.center.y - doorHeight / 2f;
            walls.Add((new Rect(left, doorFoot + doorHeight, Thickness, floor.yMax - doorFoot - doorHeight), WallPaint.Paper, WallSide.Left));
            walls.Add((new Rect(left, floor.yMin, Thickness, doorFoot - floor.yMin), WallPaint.Paper, WallSide.Left));
            thresholds.AddRectangle(new Rect(floor.center.x - ThresholdWidth / 2f, doorFoot, ThresholdWidth, doorHeight), Threshold);
        }

        /// <summary>The paper that lies over the rooms beyond the next one, more of it the further they are.</summary>
        private static void AddFade(MeshSketch sketch, MapLayout layout)
        {
            Box? nearest = layout.FloorOf(PlanRowKind.Next) ?? layout.FloorOf(PlanRowKind.Here);
            if (nearest is not Box clear || layout.Rows[0].Kind == PlanRowKind.Next || layout.Rows[0].Kind == PlanRowKind.Here)
            {
                return;
            }

            float from = clear.Top + MapLayout.WallThickness;
            float height = layout.Sheet.Top - from;
            for (int stop = 0; stop + 1 < Fade.Length; stop++)
            {
                var band = new Rect(layout.Sheet.Left, from + Fade[stop].Up * height, layout.Sheet.Width, (Fade[stop + 1].Up - Fade[stop].Up) * height);
                sketch.AddRectangle(band, WithStrength(Paper, Fade[stop].Strength), WithStrength(Paper, Fade[stop + 1].Strength), new Rect(MeshSketch.AnywhereInTheTexture, Vector2.zero));
            }
        }

        private void AddEntrance(MeshSketch sketch, MapLayout layout, float softEdge)
        {
            Rect lowest = RectOf(layout.Rows[layout.Rows.Count - 1].Floor);
            float wallFoot = lowest.yMin - MapLayout.WallThickness;
            float left = lowest.center.x - EntranceWidth / 2f;
            float right = left + EntranceWidth;

            // Each leaf swings from its post to the middle of the doorway: a quarter circle, drawn in dashes.
            foreach ((float hinge, float towards) in new[] { (left, 1f), (right, -1f) })
            {
                var swing = new List<Vector2>();
                for (int step = 0; step <= 10; step++)
                {
                    float angle = step / 10f * Mathf.PI / 2f;
                    swing.Add(new Vector2(hinge + towards * EntranceDepth * Mathf.Sin(angle), wallFoot - EntranceDepth * Mathf.Cos(angle)));
                }

                Stroke.AddDashed(sketch, swing, 0.8f, softEdge, EntranceSwing, dash: 2.4f, gap: 2.2f);
                sketch.AddRectangle(new Rect(hinge - 1.8f, wallFoot - EntranceDepth, 3.6f, EntranceDepth), EntrancePost);
                sketch.AddRectangle(new Rect(hinge - 1.15f, wallFoot - EntranceDepth + 1f, 1.3f, EntranceDepth - 2f), EntrancePostLight);
            }

            TextMeshPro label = Lettering.Write("Entrance", Content, _art, LetteringStyle.Label, EntranceLabelSize, EntranceInk, DrawOrder.SheetLettering);
            label.characterSpacing = 9f;
            Lettering.SayFromLeft(label, MuseumWords.Entrance, right + 9f, wallFoot - EntranceDepth / 2f);
        }

        // ---- what stands in the rooms ----------------------------------------------------------------------------

        private void Furnish(PlanRow row, MapLayout layout)
        {
            FloorPlan plan = layout.Plan;
            switch (row.Kind)
            {
                case PlanRowKind.Here:
                    FurnishThePlayersRoom(row.Floor, plan.Here);
                    break;
                case PlanRowKind.Next when plan.Next is RoomNumber next:
                    FurnishARoomInFull(row.Floor, next);
                    break;
                case PlanRowKind.Before when plan.Before is RoomNumber before:
                    FurnishARoomInFull(row.Floor, before);
                    break;
                case PlanRowKind.Ahead:
                    for (int room = row.RowAhead * FloorPlan.RoomsInARow; room < Math.Min(plan.Ahead.Count, (row.RowAhead + 1) * FloorPlan.RoomsInARow); room++)
                    {
                        Box floor = layout.FloorOfRoomAhead(room);
                        WriteNumber(MuseumWords.Rooms(plan.Ahead[room], plan.Ahead[room]), floor, LargestNumber, NumberOfFloorHeight, row.RowAhead == 0 ? NearNumber : FarNumber);
                    }

                    break;
                case PlanRowKind.Beyond when plan.Beyond is RoomStretch beyond:
                    WriteNumber(MuseumWords.Rooms(beyond.First, beyond.Last), row.Floor, LargestStretchNumber, StretchNumberOfFloorHeight, StretchNumber);
                    break;
                case PlanRowKind.Behind when plan.Behind is RoomStretch behind:
                    WriteNumber(MuseumWords.Rooms(behind.First, behind.Last), row.Floor, LargestStretchNumber, StretchNumberOfFloorHeight, NearNumber);
                    break;
                default:
                    throw new InvalidOperationException($"The layout has a row for {row.Kind} that the plan has no room for.");
            }
        }

        /// <summary>A room's number, or a stretch of rooms' numbers, in the middle of its floor, no larger than the floor's height allows.</summary>
        /// <param name="ofFloorHeight">The share of the floor's height the number may have.</param>
        private void WriteNumber(string number, Box floor, float largest, float ofFloorHeight, Color colour)
        {
            float size = Mathf.Min(largest, ofFloorHeight * floor.Height);
            TextMeshPro line = Lettering.Write($"Room {number}", Content, _art, LetteringStyle.Figure, size, colour, DrawOrder.SheetLettering);
            line.transform.localPosition = new Vector3(floor.MiddleX, floor.MiddleY - Lettering.FigureDrop(LetteringStyle.Figure) * size, 0f);
            Lettering.Say(line, number);
        }

        /// <summary>The player's room: its badge and name, its paintings with their count, and the pin on its pill.</summary>
        private void FurnishThePlayersRoom(Box floor, RoomNumber room)
        {
            RoomWall wall = RoomWall.Of(room, _visit.Progress, _paintings.Catalogue);

            // The pin stands on the pill and reaches up towards the paintings: of the room's height beyond what
            // its three rows need, the gap between the paintings and the pill gets most, so the pin stays clear.
            float spare = floor.Height - BadgeSize - PaintingThumbs.PictureHeight - PillHeight;
            float headerMiddle = floor.Top - 0.2f * spare - BadgeSize / 2f;
            float picturesMiddle = headerMiddle - BadgeSize / 2f - 0.2f * spare - PaintingThumbs.PictureHeight / 2f;
            float pillMiddle = floor.Bottom + 0.14f * spare + PillHeight / 2f;

            AddBadgeAndLines(floor, wall, headerMiddle);
            AddPictures(floor, wall, picturesMiddle);

            string line = MuseumWords.YouAreHere(_visit.LevelToPlay);
            TextMeshPro words = WritePillLine(line);
            float width = PinRoomInPill + Lettering.WidthOf(words, line) + PillPadding;
            var pill = new Vector2(floor.MiddleX, pillMiddle);
            AddPill(pill, width, scale: 1f);
            words.transform.localPosition = new Vector3(pill.x + (PinRoomInPill - PillPadding) / 2f, pill.y, 0f);
            Transform pin = SceneObjects.AddSprite("Pin", Content, _art.Of(GalleryArt.IconPin), _art.SpriteMaterial, DrawOrder.SheetPin).transform;
            pin.localPosition = new Vector3(pill.x - width / 2f + PinPointIn, pill.y + 1f, 0f);
        }

        /// <summary>
        /// A room that is not the player's, drawn in full: an open one with its badge, name and paintings; a locked
        /// one with its padlock, its name and the level that opens it.
        /// </summary>
        private void FurnishARoomInFull(Box floor, RoomNumber room)
        {
            RoomWall wall = RoomWall.Of(room, _visit.Progress, _paintings.Catalogue);
            RoomAccess access = AccessOf(room);
            if (!FloorPlan.IsClosed(access))
            {
                float gap = (floor.Height - BadgeSize - PaintingThumbs.PictureHeight) / 3f;
                float headerMiddle = floor.Top - gap - BadgeSize / 2f;
                AddBadgeAndLines(floor, wall, headerMiddle);
                AddPictures(floor, wall, headerMiddle - BadgeSize / 2f - gap - PaintingThumbs.PictureHeight / 2f);
                return;
            }

            Transform padlock = SceneObjects.AddSprite("Padlock", Content, _art.Of(GalleryArt.IconLock), _art.SpriteMaterial, DrawOrder.SheetArt).transform;
            padlock.localPosition = new Vector3(floor.Left + PadlockIn, floor.MiddleY, 0f);
            padlock.localScale = new Vector3(PadlockSize / PadlockBox, PadlockSize / PadlockBox, 1f);

            float left = floor.Left + LinesBesidePadlockIn;
            WriteLabel(MuseumWords.RoomOfRooms(room), left, floor.MiddleY + 25f);
            WriteName(wall.Name, left, floor.MiddleY + 8.5f, floor.Right - left - 8f);

            string opens = access == RoomAccess.ToCome ? MuseumWords.RoomToCome : MuseumWords.OpensAt(room);
            TextMeshPro words = WritePillLine(opens);
            float width = (Lettering.WidthOf(words, opens) + 2f * PillPadding) / SmallPillScale;
            var pill = new Vector2(left + width * SmallPillScale / 2f, floor.MiddleY - 18.5f);
            AddPill(pill, width, SmallPillScale);
            words.transform.localPosition = new Vector3(pill.x, pill.y, 0f);
        }

        private void AddBadgeAndLines(Box floor, RoomWall wall, float middle)
        {
            Transform badge = SceneObjects.AddSprite("Badge", Content, _art.Of(GalleryArt.RoomBadge), _art.SpriteMaterial, DrawOrder.SheetArt).transform;
            badge.localPosition = new Vector3(floor.Left + BadgeIn, middle, 0f);
            TextMeshPro number = Lettering.Write("Room number", badge, _art, LetteringStyle.OnTeal, BadgeNumberSize, Color.white, DrawOrder.SheetLettering);
            number.transform.localPosition = new Vector3(0f, -Lettering.FigureDrop(LetteringStyle.OnTeal) * BadgeNumberSize, 0f);
            Lettering.Say(number, wall.Number.Value.ToString(CultureInfo.InvariantCulture));

            float left = floor.Left + LinesBeginIn;
            WriteLabel(MuseumWords.RoomOfRooms(wall.Number), left, middle + LabelOverBadgeMiddle);
            WriteName(wall.Name, left, middle - NameUnderBadgeMiddle, floor.Right - left - 8f);
        }

        private void AddPictures(Box floor, RoomWall wall, float middle)
        {
            PaintingThumbs pictures = PaintingThumbs.Create(Content, _art, _paintings, wall, _visit.BeingRestored, ThumbOrders.OnTheSheet);
            float left = floor.Left + PicturesBeginIn;
            pictures.Transform.localPosition = new Vector3(left + PaintingThumbs.RowWidth / 2f, middle, 0f);
            _drawn.Add(pictures);
            WriteLabel(MuseumWords.PaintingsWhole(wall.PaintingsWhole), left + PaintingThumbs.RowWidth + CountAfterPictures, middle);
        }

        private void WriteLabel(string words, float left, float height)
        {
            TextMeshPro label = Lettering.Write("Label", Content, _art, LetteringStyle.Label, PagePlaque.LabelSize, SheetLabelInk, DrawOrder.SheetLettering);
            label.characterSpacing = 9f;
            Lettering.SayFromLeft(label, words, left, height);
        }

        private void WriteName(string name, float left, float height, float room)
        {
            TextMeshPro line = Lettering.Write("Room name", Content, _art, LetteringStyle.Title, NameSize, Lettering.Ink, DrawOrder.SheetLettering);
            line.fontSize = NameSize * Mathf.Min(1f, room / Lettering.WidthOf(line, name));
            Lettering.SayFromLeft(line, name, left, height);
        }

        private TextMeshPro WritePillLine(string words)
        {
            TextMeshPro line = Lettering.Write("Pill line", Content, _art, LetteringStyle.Label, PillLineSize, Lettering.Ink, DrawOrder.SheetLabelLettering);
            Lettering.Say(line, words);
            return line;
        }

        /// <param name="width">The pill's width before it is drawn at a share of its size.</param>
        private void AddPill(Vector2 middle, float width, float scale)
        {
            SpriteRenderer pill = SceneObjects.AddSprite("Pill", Content, _art.Of(GalleryArt.Pill), _art.SpriteMaterial, DrawOrder.SheetLabel);
            pill.drawMode = SpriteDrawMode.Sliced; // the picture's middle stretches; its round ends keep their shape
            pill.size = new Vector2(width + PillPictureMargin, PillPictureHeight);
            pill.transform.localPosition = new Vector3(middle.x, middle.y, 0f);
            pill.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ---- small things ----------------------------------------------------------------------------------------

        private static Rect RectOf(Box box) => new Rect(box.Left, box.Bottom, box.Width, box.Height);

        /// <summary>A rectangle with every side moved in by a length; out, by less than nothing.</summary>
        private static Rect Shrunk(Rect rect, float by) => new Rect(rect.x + by, rect.y + by, rect.width - 2f * by, rect.height - 2f * by);

        private static void AddOutline(MeshSketch sketch, Rect rect, float width, Color colour)
        {
            sketch.AddRectangle(new Rect(rect.xMin, rect.yMax - width, rect.width, width), colour);
            sketch.AddRectangle(new Rect(rect.xMin, rect.yMin, rect.width, width), colour);
            sketch.AddRectangle(new Rect(rect.xMin, rect.yMin + width, width, rect.height - 2f * width), colour);
            sketch.AddRectangle(new Rect(rect.xMax - width, rect.yMin + width, width, rect.height - 2f * width), colour);
        }

        private static Color WithStrength(Color colour, float strength) => new Color(colour.r, colour.g, colour.b, strength);
    }
}
