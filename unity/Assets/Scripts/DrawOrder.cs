namespace Puzzle.Client
{
    /// <summary>
    /// What is drawn over what on a screen of the game, from the wall up: the sorting order of every renderer.
    /// Everything is flat and drawn by one camera, so this one list decides all overlaps. The board screen and the
    /// museum's pages share the wall, the wires, the plaques and the dark; between those each has its own stretch. Pieces, tapes and exit marks add their
    /// row to their value: a lower row is drawn over the row above it, so the shadow under a piece never covers the
    /// head of the piece below. A piece that is carried or flies adds <see cref="PieceLift"/> as well. The gaps leave
    /// room for that, and for what later screens put between.
    /// </summary>
    public enum DrawOrder
    {
        WallPaint = -1000,
        WallDamask = -999,
        WallLight = -998,

        /// <summary>
        /// Light on the wall behind a plaque: the moves medallion glows as a won game's finale opens. It lies on
        /// the wall itself, over the lamps' light and under everything that hangs or stands in front of the wall,
        /// so on a screen where the plaques hang close over the board it falls behind the frame, not on it.
        /// </summary>
        PlaqueHalo = -997,

        /// <summary>The museum's hall: its floor, and the mouldings along the wall (cornice, picture rail, skirting).</summary>
        RoomFloor = -980,
        RoomTrim = -970,
        Sconce = -900,
        Wire = -800,
        FrameShadow = -700,

        /// <summary>
        /// What hangs in a picture frame on a page of the museum, from its ground up: a painting's canvas (the
        /// picture as a ghost under the linen, the linen, the marks of the places that are asked for, the pieces
        /// in place, the cut lines over them, and the light that runs over a painting just restored), or the map's
        /// sheet with its floors, walls, marks and lettering; small framed pictures on it with their marks, labels
        /// on it, and the pin over everything.
        /// </summary>
        CanvasGhost = -660,
        Canvas = -658,
        CanvasMark = -656,
        CanvasPicture = -654,
        CanvasLines = -652,
        CanvasShine = -650,
        SheetWalls = -647,
        SheetArt = -646,
        SheetLettering = -645,
        SheetPictureFrame = -644,
        SheetPicture = -643,
        SheetPictureMark = -642,
        SheetLabel = -641,
        SheetLabelLettering = -640,
        SheetPin = -639,

        /// <summary>The picture frame over what hangs in it, its light over the frame, the brass plate that names it, the workbench on the floor.</summary>
        PictureFrame = -620,
        PictureLight = -619,
        /// <summary>The cloth thrown over the frame of a painting that is not reached yet: over the frame, under its brass plate.</summary>
        DustSheet = -615,
        Caption = -610,
        CaptionLettering = -609,

        /// <summary>The bar of progress pips under a painting's plate, and the pips in it.</summary>
        PipBar = -608,
        Pip = -607,
        Bench = -605,

        /// <summary>What is pinned to a painting of the collection: the pill that says how far it is restored, and the badge of the pieces that wait for it.</summary>
        ExhibitLabel = -604,
        ExhibitLabelLettering = -603,
        ExhibitBadge = -602,
        ExhibitBadgeLettering = -601,

        /// <summary>
        /// The piece that waits to be placed, leaning on the hall's workbench (HomeTR): it takes three places from
        /// here up (its cardboard, its face, its rim) and its shadow the place below; the sparkles around it come over all of it.
        /// </summary>
        BenchPiece = -598,
        BenchSparkle = -595,
        Floor = 0,
        FloorWeave = 1,
        TrayShade = 2,
        Frame = 3,
        FrameBeads = 4,
        Slab = 100,
        Exit = 200,

        /// <summary>The ring under the piece the player picked, and light that lies on the tray under the pieces.</summary>
        UnderPieces = 250,
        Piece = 300,

        /// <summary>Added to a piece's order while it is carried over another piece.</summary>
        PieceLift = 40,
        Tape = 400,

        /// <summary>What a special piece throws over the board: strokes, splashes, light.</summary>
        Effect = 500,

        /// <summary>A piece that is drawn as part of an effect, over its ink: a rocket's heads, a colour bomb that turns.</summary>
        EffectPiece = 510,
        EffectLight = 520,

        /// <summary>What flies over the board and its frame: a seeker on its way.</summary>
        Flight = 600,
        Shelf = 1000,
        ShelfLettering = 1001,

        /// <summary>The round button on the shelf's left end that leads out of the level, and the mark on it.</summary>
        ShelfButton = 1002,
        ShelfButtonGlyph = 1003,

        /// <summary>
        /// A booster's round disc on the shelf. What stands on a disc takes the places above it, wherever the disc
        /// lies (<see cref="DiscPart"/>): its picture (two places, for a picture of two pieces), the ring of a
        /// chosen one, its badges and what is written on them. The shelf's last place before the jigsaw's tray is
        /// <see cref="ShelfDisc"/> plus <see cref="DiscPart.BadgeLettering"/>.
        /// </summary>
        ShelfDisc = 1004,

        /// <summary>
        /// The jigsaw's wooden tray, the pieces that wait in it (a piece takes three places from <see cref="TrayPiece"/>
        /// up: its cardboard, its face, its rim), and the labels that ride on the tray.
        /// </summary>
        Tray = 1010,
        TrayPiece = 1020,
        TrayLabel = 1030,
        TrayLabelLettering = 1031,

        Plaque = 1100,
        PlaqueArt = 1101,
        PlaqueLettering = 1102,

        /// <summary>A small framed picture on a plaque: its frame is plaque art, its picture and the mark on it come over that.</summary>
        PlaquePicture = 1103,
        PlaquePictureMark = 1104,

        /// <summary>The studs of a brass nameplate that rides on a plaque (the banner of a restored room), and the pins on its top edge: over the plate, which is plaque art.</summary>
        PlaqueNameplatePin = 1105,

        /// <summary>The ring of light that widens from a plaque's rim as a won game's finale opens: over the plaques, under what then leaves them.</summary>
        PlaqueRing = 1140,

        /// <summary>
        /// What leaves a plaque for the board: a move left over on its way to a gem, over the plaques, the wall and
        /// the frame. It is made of several pictures that lie on one another, and each takes a place of its own
        /// from here up (<see cref="ThrownMovePart"/>): pictures that share a place are drawn in whatever order
        /// the device happens to take them, and the same move then looks different from one game to the next.
        /// </summary>
        ThrownMove = 1150,

        /// <summary>
        /// A button that stands on a page by itself, and the navigation bar at a page's foot with the chosen
        /// destination's plate, the marks and their names. A button's badge takes the two places above its lettering.
        /// </summary>
        PageButton = 1200,
        PageButtonLettering = 1201,

        /// <summary>
        /// A jigsaw piece in the player's hand or on its way to its place, over everything of the page: its shadow
        /// takes the place below, the piece three places from here up. Over it, the sparkles of a painting just restored.
        /// </summary>
        Carried = 1310,
        Celebration = 1320,
        Bar = 1500,
        BarTab = 1501,
        BarIcon = 1502,
        BarLettering = 1503,

        /// <summary>
        /// The row of the player's coins and lives over a museum page's plaque, which the stage owns as it owns
        /// the bar, so pages slide under it: the cream pills, the coin and the heart that stand on their ends,
        /// what is written on them, and the round buttons of the row with their marks.
        /// </summary>
        WalletPill = 1510,
        WalletMark = 1511,
        WalletLettering = 1512,
        WalletButton = 1513,
        WalletButtonGlyph = 1514,

        /// <summary>The dark that falls over the whole room, plaques and shelf included, when a game is decided.</summary>
        Veil = 2000,

        /// <summary>The shelf's round button while a result's card hangs: it stays lit in the dark room, as the way out to the museum. Its mark takes the place above.</summary>
        ShelfButtonOverTheDark = 2050,

        /// <summary>A card in front of the dark, from its wires up to what stands and is written on it.</summary>
        CardWire = 2090,
        Card = 2100,

        /// <summary>
        /// What lies flat on a card: an alcove, a rule, a wooden plank, the face of a button. What is sunk into
        /// such a face (the pill of a price) takes the place above, and the mark in that pill the place after.
        /// </summary>
        CardArt = 2110,
        CardLight = 2111,

        /// <summary>A round disc that stands on a card or on a plank of it; what stands on the disc takes the places above it (<see cref="DiscPart"/>).</summary>
        CardDisc = 2112,

        /// <summary>What stands in the card's alcove. A jigsaw piece takes three places from here up: its cardboard, its face, its rim.</summary>
        CardExhibit = 2120,
        CardBadge = 2125,
        CardBadgeLettering = 2126,
        CardLettering = 2130,
        CardNameplate = 2140,
        CardNameplateLettering = 2141,

        /// <summary>The studs of a card's nameplate and the pins its wires end in.</summary>
        CardNameplatePin = 2142,

        /// <summary>The round button on a card's upper right corner that closes it, over the card and its nameplate, and the cross on it.</summary>
        CardCloseButton = 2145,
        CardCloseGlyph = 2146,

        /// <summary>
        /// The dark that falls over a card when a second card is hung in front of it, and what is added to the
        /// order of everything of that second card (<see cref="CardWire"/> to <see cref="CardCloseGlyph"/>): it
        /// keeps its order within itself and all of it comes over the second dark.
        /// </summary>
        SecondVeil = 2400,
        OverACard = 450,

        /// <summary>
        /// Added to the order of a painting that is hung in front of the dark to be looked at by itself: its canvas,
        /// wires, frame and plate keep their order among themselves and all come over the <see cref="Veil"/>.
        /// </summary>
        OverTheDark = 2900,
    }

    /// <summary>
    /// What stands on a round disc with a picture (a booster's), from the disc up. Each is drawn at the disc's own
    /// place in the drawing order plus its value, so a disc is the same on the shelf, on a card and on a page. A
    /// closed set.
    /// </summary>
    public enum DiscPart
    {
        /// <summary>The cream disc itself.</summary>
        Disc = 0,

        /// <summary>The picture on it; a picture of two pieces takes this place and the next, the first piece under the second.</summary>
        Picture = 1,

        /// <summary>The green ring of a disc that is chosen or armed, over the disc's gilt one.</summary>
        Ring = 3,

        /// <summary>The badges on its rim: how many are owned, the plus of "get more", the tick of a chosen one.</summary>
        Badge = 4,

        /// <summary>What a badge says.</summary>
        BadgeLettering = 5,
    }

    /// <summary>
    /// The pictures a move is made of that flies from a plaque to the board, from the one that lies lowest up.
    /// Each is drawn at <see cref="DrawOrder.ThrownMove"/> plus its own value, so of two moves that cross, every
    /// star lies over every light. A closed set.
    /// </summary>
    public enum ThrownMovePart
    {
        /// <summary>The tail the move draws behind it.</summary>
        Tail = 0,

        /// <summary>The gilt light around it.</summary>
        GiltLight = 1,

        /// <summary>The pale light inside the gilt.</summary>
        PaleLight = 2,

        /// <summary>The two rays of its star.</summary>
        Star = 3,

        /// <summary>The white core of the star.</summary>
        Core = 4,
    }
}
