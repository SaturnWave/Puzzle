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

        /// <summary>
        /// A brass nameplate 280 points wide with its studs and the pins of a card's two wires drawn in. The game no
        /// longer draws it: every nameplate, a card's and the one on the banner of a restored room, is
        /// <see cref="BrassPlate"/> with <see cref="Stud"/> and <see cref="WirePin"/> on it, at any width (the
        /// game's BrassNameplate). The member stays until the catalogue is next built without it. Its pivot is the
        /// middle of the plate.
        /// </summary>
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

        /// <summary>The raised teal plate behind the navigation bar's chosen destination, 70 by 88 points; its middle stretches sideways under a longer name. Its pivot is the middle of the plate.</summary>
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

        /// <summary>
        /// The round teal button with an arrow to the left drawn in, 44 points across in a gilt ring. The game no
        /// longer draws it: a round button is <see cref="TealDisc"/> with its mark over it (<see cref="IconBack"/>
        /// for this one). Its pivot is the middle of the disc.
        /// </summary>
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

        // The marks below are the design canvas' own symbols, written out of it by tools/unity/extract-design-symbols.js.
        // Each is drawn in a box of 100 points with its pivot in the middle of the box; the game draws it at the size a place needs.

        /// <summary>A gold coin with a drop of paint struck on it: the player's coins, a price, what a level pays.</summary>
        IconCoin,

        /// <summary>The red heart of the player's lives.</summary>
        IconHeart,

        /// <summary>The ivory gear on the round button that opens the settings.</summary>
        IconGear,

        /// <summary>The shop's mark on the navigation bar: a market stall under a striped awning.</summary>
        IconShop,

        /// <summary>The picture in the small frame of the free gift: a parcel on velvet that fills its whole box. <see cref="SmallFrame"/> is drawn over it.</summary>
        GiftPicture,

        /// <summary>The ivory arrow to the left on the round button that leads back; turned around, it points to the right.</summary>
        IconBack,

        /// <summary>The two white bars on the shelf's round button that stops a level.</summary>
        IconPause,

        /// <summary>The painter's spatula: the booster that takes one piece off the board.</summary>
        IconSpatula,

        /// <summary>A palette between two turning arrows: the booster that shuffles the board.</summary>
        IconShuffle,

        /// <summary>The gilt token that says +3: the booster of three more moves, chosen before a level.</summary>
        IconThreeMoves,

        /// <summary>The gilt token that says +5: the offer of five more moves when a level's moves have run out.</summary>
        IconFiveMoves,

        /// <summary>A handful of coins: the shop's smallest pack, and the coins an advertisement gives.</summary>
        CoinPile,

        /// <summary>A red pouch of coins: the shop's middle pack.</summary>
        CoinPouch,

        /// <summary>An open chest of coins: the shop's largest pack.</summary>
        CoinChest,

        /// <summary>A loudspeaker: the settings' row for sound.</summary>
        IconSound,

        /// <summary>Two notes: the settings' row for music.</summary>
        IconMusic,

        /// <summary>A phone that shakes: the settings' row for vibration.</summary>
        IconBuzz,

        /// <summary>A globe: the settings' row for the language.</summary>
        IconGlobe,

        /// <summary>A shield with a tick: the settings' row for privacy and data.</summary>
        IconShield,

        /// <summary>A question mark: the settings' row for support.</summary>
        IconHelp,

        // The white strokes below stand on a round button or a badge, each in a picture as large as the canvas shows it.

        /// <summary>The white plus of "get more" on its dark green edge, 14 points across; it stands on <see cref="GreenDisc"/>. Its pivot is its middle.</summary>
        GlyphPlus,

        /// <summary>The white cross that closes a card, on its dark teal edge, 22 points across; it stands on <see cref="TealDisc"/>. Its pivot is its middle.</summary>
        GlyphClose,

        /// <summary>The white chevron to the right of a row that leads somewhere, on its dark teal edge, 14 points across; it stands on <see cref="TinyTealDisc"/>. Its pivot is its middle.</summary>
        GlyphChevron,

        /// <summary>The white tick of what is chosen, 18 points across; it stands on <see cref="GreenBadge"/>. Its pivot is its middle.</summary>
        GlyphTick,

        /// <summary>The white triangle of "watch" on its dark blue edge, 24 points across; it stands on the blue buttons that play an advertisement. Its pivot is its middle.</summary>
        GlyphPlay,

        /// <summary>The round cream disc a booster's picture stands on, 60 points across in a gilt ring. Its pivot is the middle of the disc.</summary>
        CreamDisc,

        /// <summary>The green ring of a chosen booster, drawn over <see cref="CreamDisc"/> at the same place and scale: it covers the gilt ring. Its pivot is the middle of the ring.</summary>
        ChosenRing,

        /// <summary>The round teal button with nothing on it, 44 points across in a gilt ring on a dark lip; a mark is drawn over it. Its pivot is the middle of the disc.</summary>
        TealDisc,

        /// <summary>The teal disc a row's mark stands on, 32 points across in a gilt ring. Its pivot is the middle of the disc.</summary>
        SmallTealDisc,

        /// <summary>The smallest teal disc, 26 points across in a dark line with no gilt; the chevron stands on it. Its pivot is the middle of the disc.</summary>
        TinyTealDisc,

        /// <summary>The round green button of "get more", 26 points across; <see cref="GlyphPlus"/> stands on it. Its pivot is the middle of the disc.</summary>
        GreenDisc,

        /// <summary>The teal badge in a white ring with how many of a booster the player owns, 22 points across; its middle stretches sideways for a longer number. Its pivot is the middle of the badge.</summary>
        TealBadge,

        /// <summary>The green badge in a white ring, 22 points across: with a plus where the player owns none of a booster, with <see cref="GlyphTick"/> where one is chosen. Stretches as <see cref="TealBadge"/> does. Its pivot is the middle of the badge.</summary>
        GreenBadge,

        /// <summary>The small green button, 44 points high; its middle stretches sideways to any width. Its pivot is the middle of the pill.</summary>
        SmallButton,

        /// <summary>The small blue button that plays an advertisement; stretches as <see cref="SmallButton"/> does. Its pivot is the middle of the pill.</summary>
        SmallBlueButton,

        /// <summary>The see-through dark green pill a price stands in, sunk into a green button, 21 points high; its middle stretches sideways to any width. Its pivot is the middle of the pill.</summary>
        PricePill,

        /// <summary>The track of a switch that is on, 56 by 23 points: green in a gilt ring with a white tick at its left end. Its pivot is the middle of the track.</summary>
        ToggleOn,

        /// <summary>The track of a switch that is off: a hollow of dull cream in a gilt ring, as large as <see cref="ToggleOn"/>. Its pivot is the middle of the track.</summary>
        ToggleOff,

        /// <summary>The cream knob of a switch, 33 points across; it stands 15 points right of an on track's middle and 15 left of an off track's. Its pivot is the middle of the knob.</summary>
        ToggleKnob,

        /// <summary>
        /// A small gilt frame around a hole, 60 by 60 points with a hole of 50 by 50; its middle stretches both ways
        /// to any size. It is drawn over the picture it frames and throws its shade on it. Its pivot is the middle
        /// of the hole.
        /// </summary>
        SmallFrame,

        /// <summary>The teal velvet behind what <see cref="SmallFrame"/> shows, a box of 100 points filled to its corners; drawn at the size of the frame's hole, also wider than high. Its pivot is the middle of the box.</summary>
        Velvet,

        /// <summary>
        /// The wooden plank a row of round discs stands on, 80 points high as drawn; its middle stretches both ways,
        /// to the board's booster shelf, the shelf before a level and the shop's shelves. Its pivot is the middle of the plank.
        /// </summary>
        Plank,

        /// <summary>The dark well the rooms' chips lie in on the collection's plaque, 36 points high in a gilt ring; its middle stretches sideways to any width. Its pivot is the middle of the well.</summary>
        ChipWell,

        /// <summary>The cream chip of the chosen room in <see cref="ChipWell"/>, 28 points high in a dark line; its middle stretches sideways to any width. Its pivot is the middle of the chip.</summary>
        Chip,

        /// <summary>A pip of a piece won, on a cream card: gilt, 19 by 13 points. The jigsaw's dark bar has pips of its own (<see cref="PipOn"/>). Its pivot is the middle of the pip.</summary>
        CardPipOn,

        /// <summary>The pip of the piece a level has just given, on a cream card: gilt in rings of light and red, as large inside as <see cref="CardPipOn"/>. Its pivot is the middle of the pip.</summary>
        CardPipNew,

        /// <summary>A pip of a piece not won yet, on a cream card: a shallow hollow, as large as <see cref="CardPipOn"/>. Its pivot is the middle of the pip.</summary>
        CardPipOff,

        /// <summary>
        /// A brass nameplate with neither studs nor pins, 40 by 40 points as drawn; its middle stretches both ways
        /// to any size, for a name in one line or two. <see cref="Stud"/> and <see cref="WirePin"/> are drawn on it.
        /// It is the nameplate of every card that hangs and of the banner of a restored room. Its pivot is the middle of the plate.
        /// </summary>
        BrassPlate,

        /// <summary>A stud of a brass nameplate, 6 points across; the canvas sets one 9 points in from each end of a plate. Its pivot is its middle.</summary>
        Stud,

        /// <summary>The gilt pin a picture wire ends in, 6 points across. Its pivot is its middle.</summary>
        WirePin,

        /// <summary>The cream pill an amount stands in, 36 points high in a gilt rim: the wallet's coins and lives, the coins a level pays; its middle stretches sideways to any width. Its pivot is the middle of the pill.</summary>
        WalletPill,

        /// <summary>
        /// The alcove of the design canvas' cards (WinTR, LoseTR): <see cref="Alcove"/> with a niche of 316 by 188,
        /// so its frame is 330 by 202 points. Its pivot is the middle of the frame.
        /// </summary>
        WideAlcove,

        // The world map (MapWorldTR, #4).
        /// <summary>The frame of a painting's pin on the world map, in a box of 46 by 52 points with room for its rings: gilt around an open square of 32 where the painting is drawn under it, and the pointer under it. Its pivot is the pointer's tip.</summary>
        MapPin,

        /// <summary><see cref="MapPin"/> in the red ring of the painting being restored.</summary>
        MapPinHere,

        /// <summary><see cref="MapPin"/> in the teal ring of the painting whose card is open.</summary>
        MapPinChosen,

        /// <summary>The green badge with a tick on a restored pin or era, 15 points across. Its pivot is its middle.</summary>
        MapTickBadge,

        /// <summary>The ivory badge with a padlock on an era still ahead, 13 points across. Its pivot is its middle.</summary>
        MapLockBadge,

        /// <summary>The red badge that counts the pieces won on the pin being restored; its middle stretches sideways. Its pivot is the middle of the pill.</summary>
        MapCountBadge,

        /// <summary>A restored painting of another era as a gilt dot, 7 points across. Its pivot is its middle.</summary>
        MapDotRestored,

        /// <summary>The painting being restored as a red dot in its halo, 9 points across. Its pivot is its middle.</summary>
        MapDotHere,

        /// <summary>A painting still ahead as a cream dot, 7 points across. Its pivot is its middle.</summary>
        MapDotAhead,

        /// <summary>The exact place of a pin: a dark dot in a cream ring, 12 points across. Its pivot is its middle.</summary>
        MapAnchor,

        /// <summary>The cream plate of the world map's legend, 20 points high; its middle stretches sideways. Its pivot is the middle of the plate.</summary>
        MapLegend,

        /// <summary>A round button of the world map: a cream face in a gilt ring, 38 points across in a box of 44. Its pivot is its middle.</summary>
        MapButton,

        /// <summary><see cref="MapButton"/> with the light teal face of the globe while the whole world is shown.</summary>
        MapButtonLit,

        /// <summary>The globe on the world map's round button, in a box of 24 points. Its pivot is its middle.</summary>
        MapGlyphGlobe,

        /// <summary>The target on the round button that leads to where the player is, in a box of 24 points. Its pivot is its middle.</summary>
        MapGlyphTarget,

        /// <summary>The world map's compass rose without its letter, in a box of 60 points. Its pivot is its middle.</summary>
        MapCompass,

        /// <summary>The gilt medallion of a restored era on the world map's strip, 34 points across in a box of 48. Its pivot is its middle.</summary>
        EraRestored,

        /// <summary>The teal medallion in its red ring of the era the player is in. Its pivot is its middle.</summary>
        EraHere,

        /// <summary>The ivory medallion of an era still ahead. Its pivot is its middle.</summary>
        EraAhead,

        /// <summary>The teal light under the era the map shows, 58 by 64 points. Its pivot is its middle.</summary>
        EraChosen,

        /// <summary>The card of a painting on the world map, cream in a gilt rim; its middle stretches both ways. Its pivot is the middle of the card.</summary>
        MapCard,

        /// <summary>The gilt frame of a card's picture on the world map, 92 points square, open where the picture is drawn under it. Its pivot is the middle of the frame.</summary>
        MapCardFrame,

        /// <summary>The green pill of a restored painting on its card; its middle stretches sideways. Its pivot is the middle of the pill.</summary>
        MapPillRestored,

        /// <summary>The red pill of the painting being restored on its card; its middle stretches sideways. Its pivot is the middle of the pill.</summary>
        MapPillHere,

        /// <summary>The cream pill of a painting still ahead on its card; its middle stretches sideways. Its pivot is the middle of the pill.</summary>
        MapPillAhead,

        /// <summary>The small cream round button of a card that steps to the era's painting before or after, 28 points across. Its pivot is its middle.</summary>
        MapStep,

        /// <summary>The dark chevron pointing up on a card's step button, in a box of 12 points; turned over it points down. Its pivot is its middle.</summary>
        MapGlyphChevron,

        // The entry page (EntryTR).
        /// <summary>The gilt bar that fills while the game starts, 22 points high around a dark well 16 high; its middle stretches sideways to any width. Its pivot is the middle of the bar.</summary>
        EntryBar,

        /// <summary>The green that runs in that well, 16 points high; its middle stretches sideways to how far the bar has filled. Its pivot is the middle of the green.</summary>
        EntryBarFill,
    }

    /// <summary>
    /// How lettering is drawn, as the design canvas sets its type: a style is the typeface of a line
    /// (<see cref="Typefaces.Of"/>) and what is drawn around and under its strokes. The colour is the text's own.
    /// A closed set: <see cref="ArtCatalogue"/> stores a typeface and a material per style in this order, so after
    /// adding a member, build the catalogue again (ProjectSetup.Run).
    /// </summary>
    public enum LetteringStyle
    {
        /// <summary>Every figure: the numbers on the plaques, counts on badges, the amounts of a wallet. Paytone One as its file has it.</summary>
        Figure,

        /// <summary>The design's interface type, Gabarito at its heaviest: labels in small capitals, names, hints, the words on pills.</summary>
        Label,

        /// <summary>Light lettering on the dark wood of the shelf, with a dark edge under it.</summary>
        OnWood,

        /// <summary>White lettering on the green of a button: Paytone One in a dark green outline, as wide under the letters as at their sides.</summary>
        OnButton,

        /// <summary>White lettering on the gallery's teal (a room's number on its badge, a destination's name on the bar, the teal button): in a dark teal outline.</summary>
        OnTeal,

        /// <summary>White lettering on the blue of the buttons that lead to a jigsaw: in a dark blue outline.</summary>
        OnBlue,

        /// <summary>The serif of titles and names, on brass and on cream: a nameplate's title, the name of a room or a page, a painting's title.</summary>
        Title,

        /// <summary>A secondary line under or beside a stronger one: Gabarito a weight lighter than <see cref="Label"/>.</summary>
        Soft,

        /// <summary>The white count on the red heart of the lives: in a dark red outline.</summary>
        OnHeart,

        /// <summary>The white amount on the dark green pill that states a price inside a button: no outline, the pill is dark enough.</summary>
        OnPricePill,

        /// <summary>Cream lettering on the gallery's dark teal where no plate lies under it: a place that is not chosen, a locked room's name.</summary>
        OnDarkTeal,

        /// <summary>
        /// The game's title on the entry page (EntryTR): the serif of titles, set large in gilt (a gradient of its own,
        /// the line's colours) in a dark brown outline with a hard shade under it, so it reads on the page's cream.
        /// </summary>
        GiltTitle,

        /// <summary>White lettering on the entry page's cream ("Başlamak için dokun"): Paytone One in a dark brown outline.</summary>
        OnCream,
    }
}
