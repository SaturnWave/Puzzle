#nullable enable
using System;
using Puzzle.Client.Flow;

namespace Puzzle.Client.Avatar
{
    /// <summary>
    /// The pictures of the painter the game carries (art/avatar, docs/GAME_DESIGN.md §22): the old master painter in
    /// a felt beret, rendered in Blender. Three of him standing, all at one scale so their feet stand on one line, and
    /// three busts, one for each look he gives the player. A closed set.
    /// </summary>
    public enum PainterPicture
    {
        /// <summary>Standing, the palette in his left hand and a brush raised in his right: the hall's painter by the workbench.</summary>
        StandingWithPalette,

        /// <summary>Standing in the same place, waving with the hand that holds the brush: the hall of a new player.</summary>
        StandingWaving,

        /// <summary>Standing with both arms raised, brush and palette in the air: a painting made whole, a room restored.</summary>
        StandingCheering,

        /// <summary>Head and shoulders, brows raised and a closed smile: "look, this is how it is done".</summary>
        BustGuiding,

        /// <summary>Head and shoulders, the brush's handle against his chin and his eyes turned up and aside: "hmm".</summary>
        BustThinking,

        /// <summary>Head and shoulders, a broad smile and creased eyes.</summary>
        BustJoyful,
    }

    /// <summary>How the painter looks at the player from a bust. A closed set: each has a picture of its own (<see cref="PainterPictures.BustOf"/>).</summary>
    public enum PainterMood
    {
        Guiding,
        Thinking,
        Joyful,
    }

    /// <summary>
    /// How much room a standing picture of the painter takes, in metres around his feet: how high its top stands over
    /// them, how far it reaches to each side, and how far it reaches to each side below the height where raised arms
    /// begin (<see cref="PainterPictures.ArmsRaisedFrom"/>): under it he is no wider than his apron and his lowered hands.
    /// The figures were measured on the pictures' opaque pixels (alpha over 40 of 255) on 2026-10-24.
    /// </summary>
    public readonly struct StandingMeasure
    {
        public StandingMeasure(float top, float left, float right, float lowLeft, float lowRight)
        {
            Top = top;
            Left = left;
            Right = right;
            LowLeft = lowLeft;
            LowRight = lowRight;
        }

        /// <summary>How high the picture's top (his beret, or a raised hand) stands over his feet.</summary>
        public float Top { get; }

        /// <summary>How far the figure reaches to the left and to the right of his feet, raised hands, brush and palette included.</summary>
        public float Left { get; }

        public float Right { get; }

        /// <summary>How far the figure reaches to each side below <see cref="PainterPictures.ArmsRaisedFrom"/>.</summary>
        public float LowLeft { get; }

        public float LowRight { get; }
    }

    /// <summary>
    /// What the game knows of the painter's pictures without loading them: the name each is loaded by, where a
    /// standing one's feet are, how much room it takes, and which bust gives which look. No Unity dependency.
    /// </summary>
    public static class PainterPictures
    {
        /// <summary>The folder under a Resources folder the pictures are loaded from (unity/Assets/Art/Avatar/Resources/Painter).</summary>
        public const string ResourceFolder = "Painter";

        /// <summary>
        /// The standing pictures are rendered at one scale: 1100 of their pixels to a metre (art/avatar/ressam_ayakta-*.json),
        /// so a figure drawn at so many points a metre is the same man in every pose, and the three feet stand on one line.
        /// </summary>
        public const float PixelsPerMetre = 1100f;

        /// <summary>A bust is a square picture; it is drawn in a box of this many points, as the gallery's marks are.</summary>
        public const float BustBox = 100f;

        /// <summary>Under this height over his feet, in metres, his arms hang or hold the palette: above it they may be raised.</summary>
        public const float ArmsRaisedFrom = 1.4f;

        /// <summary>Every picture, in the order of the set.</summary>
        public static PainterPicture[] All => (PainterPicture[])Enum.GetValues(typeof(PainterPicture));

        /// <summary>
        /// The picture's file name without its ending, as the game carries it. Its source in art/avatar keeps the
        /// render's own name; art/avatar/licences.json pairs the two ("inGame").
        /// </summary>
        public static string FileNameOf(PainterPicture picture) => picture switch
        {
            PainterPicture.StandingWithPalette => "painter-with-palette",
            PainterPicture.StandingWaving => "painter-waving",
            PainterPicture.StandingCheering => "painter-cheering",
            PainterPicture.BustGuiding => "painter-bust-guiding",
            PainterPicture.BustThinking => "painter-bust-thinking",
            PainterPicture.BustJoyful => "painter-bust-joyful",
            _ => throw new ArgumentOutOfRangeException(nameof(picture), picture, "A picture of the painter the game does not carry."),
        };

        /// <summary>The name a picture is loaded by.</summary>
        public static string ResourceOf(PainterPicture picture) => $"{ResourceFolder}/{FileNameOf(picture)}";

        /// <summary>True for the three standing pictures, false for the busts.</summary>
        public static bool IsStanding(PainterPicture picture) =>
            picture == PainterPicture.StandingWithPalette || picture == PainterPicture.StandingWaving || picture == PainterPicture.StandingCheering;

        /// <summary>
        /// Where a standing picture's feet are, as a share of its width from the left and of its height from the foot:
        /// the sprite's pivot, so a figure is placed by where it stands. The render placed his feet 657 pixels from the
        /// left of every picture, and 96 pixels over its foot, where the soft shadow ends.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A bust, which stands on no feet.</exception>
        public static (float FromLeft, float FromFoot) FeetOf(PainterPicture standing) => standing switch
        {
            PainterPicture.StandingWithPalette => (657f / 1301f, 96f / 2186f),
            PainterPicture.StandingWaving => (657f / 1301f, 96f / 2186f),
            PainterPicture.StandingCheering => (657f / 1577f, 96f / 2402f),
            _ => throw new ArgumentOutOfRangeException(nameof(standing), standing, "A bust stands on no feet."),
        };

        /// <summary>How much room a standing picture takes around his feet, in metres.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A bust.</exception>
        public static StandingMeasure MeasureOf(PainterPicture standing) => standing switch
        {
            PainterPicture.StandingWithPalette => new StandingMeasure(top: 1.864f, left: 0.381f, right: 0.525f, lowLeft: 0.361f, lowRight: 0.525f),
            PainterPicture.StandingWaving => new StandingMeasure(top: 1.864f, left: 0.519f, right: 0.525f, lowLeft: 0.492f, lowRight: 0.525f),
            PainterPicture.StandingCheering => new StandingMeasure(top: 2.060f, left: 0.495f, right: 0.799f, lowLeft: 0.263f, lowRight: 0.352f),
            _ => throw new ArgumentOutOfRangeException(nameof(standing), standing, "A bust is measured by its box, not around feet."),
        };

        /// <summary>The bust that gives a look.</summary>
        public static PainterPicture BustOf(PainterMood mood) => mood switch
        {
            PainterMood.Guiding => PainterPicture.BustGuiding,
            PainterMood.Thinking => PainterPicture.BustThinking,
            PainterMood.Joyful => PainterPicture.BustJoyful,
            _ => throw new ArgumentOutOfRangeException(nameof(mood), mood, "A look the painter has no bust for."),
        };

        /// <summary>
        /// How the painter looks beside the words of a step of the tutorial or of the booster intro: he guides where
        /// the step shows what to do or what a thing is for; he thinks where it shows something new on the board to
        /// work out (crates, varnish, tape, a lost frame corner) or how to learn more of a booster (the long press);
        /// he is glad at the first painting made whole.
        /// </summary>
        public static PainterMood MoodOf(TutorialStep step) => step switch
        {
            TutorialStep.Goal => PainterMood.Guiding,
            TutorialStep.Match => PainterMood.Guiding,
            TutorialStep.Moves => PainterMood.Guiding,
            TutorialStep.FourInALine => PainterMood.Guiding,
            TutorialStep.FiveInALine => PainterMood.Guiding,
            TutorialStep.Box => PainterMood.Thinking,
            TutorialStep.Jelly => PainterMood.Thinking,
            TutorialStep.Chain => PainterMood.Thinking,
            TutorialStep.Item => PainterMood.Thinking,
            TutorialStep.FirstPiece => PainterMood.Guiding,
            TutorialStep.FirstPainting => PainterMood.Joyful,
            TutorialStep.BoosterHitCell => PainterMood.Guiding,
            TutorialStep.BoosterHitRow => PainterMood.Guiding,
            TutorialStep.BoosterHitColumn => PainterMood.Guiding,
            TutorialStep.BoosterShuffle => PainterMood.Guiding,
            TutorialStep.StartBoosters => PainterMood.Guiding,
            TutorialStep.LongPress => PainterMood.Thinking,
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "A step of the tutorial the painter has no look for."),
        };
    }
}
