using System;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// Every drawing of the board and its pieces: one per PNG in Assets/Art/Pieces, which tools/unity/export-sprites.js writes from
    /// the web test page's sprite. A closed set, so a missing drawing is found when the catalogue is built and not when
    /// a level first needs it. <see cref="ArtCatalogue"/> stores its sprites in this order: after adding, removing
    /// or reordering a member, build the catalogue again (ProjectSetup.Run).
    /// </summary>
    public enum PieceArt
    {
        GemRed,
        GemGreen,
        GemBlue,
        GemYellow,
        GemPurple,
        GemOrange,
        GemCyan,
        GemPink,
        RocketHorizontal,
        RocketVertical,
        Bomb,
        BigBomb,
        Seeker,
        ColorBomb,
        CrateOneHit,
        CrateTwoHits,
        CrateThreeHits,
        Item,
        TapeOneHit,
        TapeTwoHits,
        ThinSlabA,
        ThinSlabB,
        ThinSlabC,
        ThickSlabA,
        ThickSlabB,
        ThickSlabC,
        Exit,
        GoalRed,
        GoalGreen,
        GoalBlue,
        GoalYellow,
        GoalPurple,
        GoalOrange,
        GoalCyan,
        GoalPink,
        GoalCrates,
        GoalSlabs,
        GoalTape,
        GoalItems,
        Star,
        Unknown,
    }

    /// <summary>Which drawing shows a thing of the engine's board.</summary>
    public static class PieceArtOf
    {
        /// <summary>The slabs come in this many looks, so a field of them does not read as one repeated stamp.</summary>
        private const int SlabVariants = 3;

        /// <summary>The drawing of what lies in a cell's tile slot; nothing for an empty slot.</summary>
        public static PieceArt? Tile(Tile tile) => tile.Kind switch
        {
            TileKind.Empty => null,
            TileKind.Gem => Gem(tile.Color),
            TileKind.RocketH => PieceArt.RocketHorizontal,
            TileKind.RocketV => PieceArt.RocketVertical,
            TileKind.Bomb => PieceArt.Bomb,
            TileKind.BigBomb => PieceArt.BigBomb,
            TileKind.Seeker => PieceArt.Seeker,
            TileKind.ColorBomb => PieceArt.ColorBomb,
            TileKind.Box => Crate(tile.Hp),
            TileKind.Item => PieceArt.Item,
            TileKind.Hidden => PieceArt.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(tile), tile.Kind, "A tile kind without a drawing."),
        };

        public static PieceArt Gem(GemColor color) => color switch
        {
            GemColor.Red => PieceArt.GemRed,
            GemColor.Green => PieceArt.GemGreen,
            GemColor.Blue => PieceArt.GemBlue,
            GemColor.Yellow => PieceArt.GemYellow,
            GemColor.Purple => PieceArt.GemPurple,
            GemColor.Orange => PieceArt.GemOrange,
            GemColor.Cyan => PieceArt.GemCyan,
            GemColor.Pink => PieceArt.GemPink,
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, "A gem colour without a drawing."),
        };

        /// <summary>The icon a goal is shown with on the goals plaque.</summary>
        public static PieceArt Goal(GoalSpec goal) => goal switch
        {
            GoalSpec.CollectColor collect => GoalGem(collect.Color),
            GoalSpec.BreakBoxes => PieceArt.GoalCrates,
            GoalSpec.ClearJelly => PieceArt.GoalSlabs,
            GoalSpec.BreakChains => PieceArt.GoalTape,
            GoalSpec.DropItems => PieceArt.GoalItems,
            _ => throw new ArgumentOutOfRangeException(nameof(goal), goal, "A goal without an icon."),
        };

        /// <summary>A crate shows the hits it still takes by its own look: cracked, whole, iron-bound.</summary>
        public static PieceArt Crate(int hitsLeft) => hitsLeft switch
        {
            1 => PieceArt.CrateOneHit,
            2 => PieceArt.CrateTwoHits,
            3 => PieceArt.CrateThreeHits,
            _ => throw new ArgumentOutOfRangeException(nameof(hitsLeft), hitsLeft, "A crate takes 1..3 hits."),
        };

        private static PieceArt GoalGem(GemColor color) => color switch
        {
            GemColor.Red => PieceArt.GoalRed,
            GemColor.Green => PieceArt.GoalGreen,
            GemColor.Blue => PieceArt.GoalBlue,
            GemColor.Yellow => PieceArt.GoalYellow,
            GemColor.Purple => PieceArt.GoalPurple,
            GemColor.Orange => PieceArt.GoalOrange,
            GemColor.Cyan => PieceArt.GoalCyan,
            GemColor.Pink => PieceArt.GoalPink,
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, "A gem colour without a goal icon."),
        };

        /// <summary>The tape over a chained tile: one strip for the last hit, crossed strips before that.</summary>
        public static PieceArt Tape(int hitsLeft) => hitsLeft switch
        {
            1 => PieceArt.TapeOneHit,
            2 => PieceArt.TapeTwoHits,
            _ => throw new ArgumentOutOfRangeException(nameof(hitsLeft), hitsLeft, "Tape takes 1..2 hits."),
        };

        /// <summary>
        /// The jelly on a cell's floor: a thin amber slab for the last layer, a thick dark one for two. The look is
        /// picked from the cell's diagonal, as the web test page does, so neighbours differ and a cell keeps its look.
        /// </summary>
        public static PieceArt Slab(int layers, Position cell)
        {
            int variant = (((cell.X - cell.Y) % SlabVariants) + SlabVariants) % SlabVariants;
            return (layers, variant) switch
            {
                (1, 0) => PieceArt.ThinSlabA,
                (1, 1) => PieceArt.ThinSlabB,
                (1, 2) => PieceArt.ThinSlabC,
                (2, 0) => PieceArt.ThickSlabA,
                (2, 1) => PieceArt.ThickSlabB,
                (2, 2) => PieceArt.ThickSlabC,
                _ => throw new ArgumentOutOfRangeException(nameof(layers), layers, "A slab has 1..2 layers."),
            };
        }
    }
}
