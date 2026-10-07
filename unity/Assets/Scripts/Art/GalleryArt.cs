namespace Puzzle.Client.Art
{
    /// <summary>
    /// The drawings of the gallery around the board: one per sprite in Assets/Art/Gallery, which
    /// tools/unity/export-gallery-art.js writes from the sources in art/unity. A closed set, like <see cref="PieceArt"/>:
    /// <see cref="ArtCatalogue"/> stores its sprites in this order, so after adding, removing or reordering a member,
    /// build the catalogue again (ProjectSetup.Run). Every sprite is sized so that one unit of the scene is one point of the design.
    /// </summary>
    public enum GalleryArt
    {
        /// <summary>A cream plaque in a gilt rim; its middle stretches sideways to any width. Its pivot is the middle of its body.</summary>
        Plaque,

        /// <summary>The round moves medallion, 98 points across. Its pivot is the middle of the ring.</summary>
        Medallion,

        /// <summary>The wooden shelf; its middle stretches sideways to any width. Its pivot is the middle of the plank.</summary>
        Shelf,

        /// <summary>A wall lamp in the pool of its light. Its pivot is the lamp's foot.</summary>
        Sconce,

        /// <summary>The green seal with a tick that marks a goal as met, 20 points across.</summary>
        GoalDone,

        /// <summary>The cream card a result is written on; its middle stretches both ways to any size. Its pivot is the middle of its body.</summary>
        Card,

        /// <summary>The brass nameplate on a card's top edge, 280 points wide, with the pins of the card's two wires. Its pivot is the middle of the plate.</summary>
        Nameplate,

        /// <summary>The green button of a card; its middle stretches sideways to any width. Its pivot is the middle of the pill.</summary>
        Button,

        /// <summary>A card's alcove: a piece of the wall with a shelf in a beaded gilt frame, 302 by 202 points. Its pivot is the middle of the frame.</summary>
        Alcove,

        /// <summary>A four-pointed sparkle, 24 points across.</summary>
        Sparkle,

        /// <summary>The teal badge with what is left of a goal; its middle stretches sideways for a long number. Its pivot is the middle of the badge.</summary>
        CountBadge,

        /// <summary>
        /// The gilt frame of a painting or of the map's sheet, with a rosette on each corner; the middle of each side
        /// repeats to any size, bead by bead. Its pivot is the middle of the picture it frames.
        /// </summary>
        PictureFrame,

        /// <summary>The brass picture light over a frame with the light it throws. Its pivot is the knob that sits on the frame's top bar.</summary>
        PictureLight,

        /// <summary>The brass plate that names a painting; its middle stretches sideways to any width. Its pivot is the middle of the plate.</summary>
        CaptionPlate,

        /// <summary>A cream pill in a gilt rim for a short line; its middle stretches sideways to any width. Its pivot is the middle of the pill.</summary>
        Pill,

        /// <summary>The round teal badge a room's number stands on, 34 points across in its rings. Its pivot is its middle.</summary>
        RoomBadge,

        /// <summary>The raised teal plate behind the navigation bar's chosen destination, 70 by 88 points. Its pivot is the middle of the plate.</summary>
        Tab,

        /// <summary>The museum's mark on the navigation bar, drawn in a box of 100 points. Its pivot is the middle of the box.</summary>
        IconMuseum,

        /// <summary>The map's mark on the navigation bar, drawn in a box of 100 points. Its pivot is the middle of the box.</summary>
        IconMap,

        /// <summary>The padlock of a room that is not open yet, drawn in a box of 100 points. Its pivot is the middle of the box.</summary>
        IconLock,

        /// <summary>The red pin of where the player stands on the map, 32 by 44 points. Its pivot is the pin's point.</summary>
        IconPin,

        /// <summary>The painter's workbench with a palette, brushes and a crate under it, on its shadow on the floor; the table is 198 points wide. Its pivot is where its feet stand, in the middle of its width.</summary>
        Workbench,

        /// <summary>The teal button of a card, the way back to the museum; stretches as <see cref="Button"/> does. Its pivot is the middle of the pill.</summary>
        TealButton,

        /// <summary>The blue button that leads to a painting's jigsaw; stretches as <see cref="Button"/> does. Its pivot is the middle of the pill.</summary>
        BlueButton,

        /// <summary>The round teal button with an arrow to the left, 44 points across in a gilt ring. Its pivot is the middle of the disc.</summary>
        BackButton,

        /// <summary>The collection's mark on the navigation bar, drawn in a box of 100 points. Its pivot is the middle of the box.</summary>
        IconCollection,

        /// <summary>An ivory jigsaw piece in a gilt edge, the mark of a painting's pieces, drawn in a box of 100 points. Its pivot is the middle of the box.</summary>
        IconPiece,

        /// <summary>The wooden tray with its velvet well; its middle stretches both ways to any size. Its pivot is the middle of the block.</summary>
        Tray,

        /// <summary>The dark teal bar the progress pips stand in, 22 points high; its middle stretches sideways to any width. Its pivot is the middle of the bar.</summary>
        PipBar,

        /// <summary>The pip of a piece in place, 13 by 9 points. Its pivot is the middle of the pip.</summary>
        PipOn,

        /// <summary>The pip of a piece not won yet. Its pivot is the middle of the pip.</summary>
        PipOff,

        /// <summary>The pip of a piece that waits to be placed. Its pivot is the middle of the pip.</summary>
        PipNext,

        /// <summary>The red badge of a count, 22 points across; its middle stretches sideways for a longer number. Its pivot is the middle of the badge.</summary>
        RedBadge,
    }

    /// <summary>
    /// How lettering is drawn. All of it is one typeface; a style is how heavy its strokes are drawn and whether it
    /// throws a shadow. The colour is the text's own.
    /// </summary>
    public enum LetteringStyle
    {
        /// <summary>The large numbers on the plaques: the strokes drawn heavier and rounder than the typeface has them.</summary>
        Figure,

        /// <summary>The small capitals over the numbers.</summary>
        Label,

        /// <summary>Light lettering on the dark wood of the shelf, with a dark edge under it.</summary>
        OnWood,

        /// <summary>White lettering on the green of a button: heavy strokes in a dark green outline, which is what makes it legible there.</summary>
        OnButton,

        /// <summary>White lettering on the gallery's teal (a room's number on its badge, a destination's name on the bar, the teal button): heavy strokes in a dark teal outline.</summary>
        OnTeal,

        /// <summary>White lettering on the blue of the buttons that lead to a jigsaw: heavy strokes in a dark blue outline.</summary>
        OnBlue,
    }
}
