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
    }
}
