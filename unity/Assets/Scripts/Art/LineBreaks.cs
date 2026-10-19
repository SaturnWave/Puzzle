namespace Puzzle.Client.Art
{
    /// <summary>
    /// Where a line may break where no space parts two letters, as TextMesh Pro breaks the lines it wraps itself:
    /// Japanese and Chinese write no spaces between their words, and a line of them breaks between any two of their
    /// letters, or between one of them and a letter of another script, except before a mark that closes or trails
    /// ("」", "。", "ー", a small kana) and after one that opens ("「", "（"). Hangul is not among them: the game
    /// breaks Korean between its words, at its spaces, as TextMesh Pro's modern Hangul rule does (ProjectSetup
    /// sets it). The two lists are TextMesh Pro's own (its settings' line-breaking files), so a line the game breaks
    /// in two itself (<see cref="BalancedLines"/>) breaks where TextMesh Pro would. No Unity dependency.
    /// </summary>
    public static class LineBreaks
    {
        /// <summary>The marks no line ends on: TextMesh Pro's "leading characters", which lead what follows them.</summary>
        public const string NeverEndALine = "([｛〔〈《「『【〘〖〝‘“｟«$—…‥〳〴〵\\［（{£¥\"々〇＄￥￦ #";

        /// <summary>The marks and letters no line begins with: TextMesh Pro's "following characters", which follow what stands before them.</summary>
        public const string NeverBeginALine = ")]｝〕〉》」』】〙〗〟’”｠»ヽヾーァィゥェォッャュョヮヵヶぁぃぅぇぉっゃゅょゎゕゖㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ々〻‐゠–〜?!‼⁇⁈⁉・、%,.:;。！？］）：；＝}¢°\"†‡℃〆％，．";

        /// <summary>
        /// True where a line may break between two letters that stand side by side with no space between them: where
        /// either is a Chinese or Japanese letter, the first ends no line of itself, and the second begins none.
        /// TextMesh Pro lets a line end on an opening mark of plain Latin before such a letter; the game does not.
        /// </summary>
        public static bool MayBreakBetween(char before, char after) =>
            (IsChineseOrJapanese(before) || IsChineseOrJapanese(after))
            && !char.IsWhiteSpace(before) && !char.IsWhiteSpace(after)
            && NeverEndALine.IndexOf(before) < 0
            && NeverBeginALine.IndexOf(after) < 0;

        /// <summary>
        /// The letters TextMesh Pro breaks a line between (its IsCJK, of the letters a single char holds): Chinese
        /// characters and their compatibility forms, kana, Bopomofo, radicals and strokes, the punctuation of the
        /// three, the vertical and small forms, and half-width katakana.
        /// </summary>
        private static bool IsChineseOrJapanese(char letter) =>
            Within(letter, '一', '鿿') || Within(letter, '㐀', '䶿') || Within(letter, '豈', '﫿')
            || Within(letter, '぀', 'ヿ') || Within(letter, 'ㇰ', 'ㇿ')
            || Within(letter, '㄀', 'ㄯ') || Within(letter, 'ㆠ', 'ㆿ')
            || Within(letter, '⺀', '⿟') || Within(letter, '⿰', '⿿') || Within(letter, '㇀', '㇯')
            || Within(letter, '　', '〿') || Within(letter, '㆐', '㆟')
            || Within(letter, '︐', '︟') || Within(letter, '︰', '﹯') || Within(letter, '･', 'ﾟ');

        private static bool Within(char letter, char first, char last) => letter >= first && letter <= last;
    }
}
