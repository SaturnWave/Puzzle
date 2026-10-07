namespace Puzzle.Client
{
    /// <summary>
    /// What is drawn over what on the board screen, from the wall up: the sorting order of every renderer. Everything
    /// is flat and drawn by one camera, so this one list decides all overlaps. Pieces, tapes and exit marks add their
    /// row to their value: a lower row is drawn over the row above it, so the shadow under a piece never covers the
    /// head of the piece below. A piece that is carried or flies adds <see cref="PieceLift"/> as well. The gaps leave
    /// room for that, and for what later screens put between.
    /// </summary>
    public enum DrawOrder
    {
        WallPaint = -1000,
        WallDamask = -999,
        WallLight = -998,
        Sconce = -900,
        Wire = -800,
        FrameShadow = -700,
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
        Plaque = 1100,
        PlaqueArt = 1101,
        PlaqueLettering = 1102,

        /// <summary>The dark that falls over the whole room, plaques and shelf included, when a game is decided.</summary>
        Veil = 2000,

        /// <summary>The result's card in front of the dark, from its wires up to what stands and is written on it.</summary>
        CardWire = 2090,
        Card = 2100,
        CardArt = 2110,
        CardLight = 2111,
        CardExhibit = 2120,
        CardBadge = 2121,
        CardBadgeLettering = 2122,
        CardLettering = 2130,
        CardNameplate = 2140,
        CardNameplateLettering = 2141,
    }
}
