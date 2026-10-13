using System;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// The font files the game's lettering is set in: the three families of the design canvas, Gabarito in the two
    /// weights the canvas uses. A closed set; tools/unity/fetch-fonts.js fetches a file for each member and the
    /// Editor's LetteringBuilder makes each ready to draw. No Unity dependency.
    /// </summary>
    public enum Typeface
    {
        /// <summary>Gabarito at weight 700: the design's secondary lines.</summary>
        GabaritoBold,

        /// <summary>Gabarito at weight 800: the design's interface type.</summary>
        GabaritoExtraBold,

        /// <summary>Paytone One: every figure and every button's label.</summary>
        PaytoneOne,

        /// <summary>Young Serif: titles and names.</summary>
        YoungSerif,
    }

    /// <summary>Which typeface each lettering style is set in: the one place that says so.</summary>
    public static class Typefaces
    {
        /// <exception cref="ArgumentOutOfRangeException">A style this list does not know.</exception>
        public static Typeface Of(LetteringStyle style) => style switch
        {
            LetteringStyle.Figure => Typeface.PaytoneOne,
            LetteringStyle.Label => Typeface.GabaritoExtraBold,
            LetteringStyle.OnWood => Typeface.GabaritoBold,
            LetteringStyle.OnButton => Typeface.PaytoneOne,
            LetteringStyle.OnTeal => Typeface.PaytoneOne,
            LetteringStyle.OnBlue => Typeface.PaytoneOne,
            LetteringStyle.Title => Typeface.YoungSerif,
            LetteringStyle.Soft => Typeface.GabaritoBold,
            LetteringStyle.OnHeart => Typeface.PaytoneOne,
            LetteringStyle.OnPricePill => Typeface.PaytoneOne,
            LetteringStyle.OnDarkTeal => Typeface.GabaritoExtraBold,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "A lettering style no typeface is chosen for."),
        };
    }

    /// <summary>
    /// The line of the ground's dark that a style draws around its letters, as the design draws white lettering on
    /// colour: with a stroke under the letters, half of which shows outside them. As parts of the type size:
    /// "Bölüm 47" at 29 px has a stroke of 5 px, 0.086 outside the letters (HomeTR); "Tablo" at 19 px one of 4,
    /// 0.105; the lives at 18 px one of 3.4, 0.094. On teal the design goes from 0.089 (a room's number at 18 px) to
    /// 0.124 (a destination's name at 14.5 px), and one width serves both.
    /// </summary>
    public static class LetteringOutlines
    {
        /// <summary>The outline's width outside the letters, as a part of the type size; 0 for a style that draws none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A style this list does not know.</exception>
        public static float WidthOf(LetteringStyle style) => style switch
        {
            LetteringStyle.OnButton => 0.086f,
            LetteringStyle.OnTeal => 0.105f,
            LetteringStyle.OnBlue => 0.105f,
            LetteringStyle.OnHeart => 0.094f,
            LetteringStyle.Figure => 0f,
            LetteringStyle.Label => 0f,
            LetteringStyle.OnWood => 0f,
            LetteringStyle.Title => 0f,
            LetteringStyle.Soft => 0f,
            LetteringStyle.OnPricePill => 0f,
            LetteringStyle.OnDarkTeal => 0f,
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "A lettering style whose outline is not listed."),
        };
    }
}
