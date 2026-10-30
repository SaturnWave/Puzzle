using System.Text;

namespace Puzzle.Client.Art
{
    /// <summary>
    /// Where a line may break where no space parts two letters, as TextMesh Pro breaks the lines it wraps itself:
    /// Japanese and Chinese write no spaces between their words, and a line of them breaks between any two of their
    /// letters, or between one of them and a letter of another script, except before a mark that closes or trails
    /// ("」", "。", "ー", a small kana) and after one that opens ("「", "（"). Hangul is not among them: the game
    /// breaks Korean between its words, at its spaces, as TextMesh Pro's modern Hangul rule does (ProjectSetup
    /// sets it). The two lists are TextMesh Pro's own (its settings' line-breaking files), so a line the game breaks
    /// in two itself (<see cref="BalancedLines"/>) breaks where TextMesh Pro would, and a paragraph TextMesh Pro
    /// wraps is made ready for it (<see cref="ReadyForWrapping"/>) where TextMesh Pro's own rule falls short. No Unity dependency.
    /// </summary>
    public static class LineBreaks
    {
        /// <summary>The marks no line ends on: TextMesh Pro's "leading characters", which lead what follows them.</summary>
        public const string NeverEndALine = "([｛〔〈《「『【〘〖〝‘“｟«$—…‥〳〴〵\\［（{£¥\"々〇＄￥￦ #";

        /// <summary>The marks and letters no line begins with: TextMesh Pro's "following characters", which follow what stands before them.</summary>
        public const string NeverBeginALine = ")]｝〕〉》」』】〙〗〟’”｠»ヽヾーァィゥェォッャュョヮヵヶぁぃぅぇぉっゃゅょゎゕゖㇰㇱㇲㇳㇴㇵㇶㇷㇸㇹㇺㇻㇼㇽㇾㇿ々〻‐゠–〜?!‼⁇⁈⁉・、%,.:;。！？］）：；＝}¢°\"†‡℃〆％，．";

        /// <summary>TextMesh Pro's rich-text tags that keep what lies between them on one line.</summary>
        private const string NoBreak = "<nobr>";
        private const string NoBreakEnds = "</nobr>";

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

        /// <summary>
        /// The words as TextMesh Pro is to wrap them, so that it breaks no line where <see cref="MayBreakBetween"/> does
        /// not. TextMesh Pro keeps its own lists when the letter before a break is one of the Chinese and Japanese letters
        /// it knows; when it is not, it lets a line end wherever such a letter follows, opening mark or not. Its letters
        /// leave out the full-width forms ("（", "［", "｛", "＄") and the plain marks ("(", "“", "‘", "«"), so a
        /// Japanese line could end on "（" and the next begin "支払い情報なし）" (the privacy card, 26 October). Every such
        /// mark is kept on its line with the letter after it, between TextMesh Pro's no-break tags; the line may still
        /// break before the mark. A closing mark, a comma, a full stop or a small kana never begins a line already:
        /// TextMesh Pro looks at the letter after a break in its lists whatever that letter is. Korean breaks at its
        /// spaces, where an opening mark stands with its word; words without such a pair, as all of a language
        /// written with spaces are, come back as they were.
        /// </summary>
        public static string ReadyForWrapping(string words)
        {
            if (words.Length < 2)
            {
                return words;
            }

            var ready = new StringBuilder(words.Length);
            int index = 0;
            while (index < words.Length)
            {
                int end = index;
                while (end + 1 < words.Length && WouldTextMeshProBreakAgainstTheRule(words[end], words[end + 1]))
                {
                    end++;
                }

                if (end == index)
                {
                    ready.Append(words[index]);
                }
                else
                {
                    ready.Append(NoBreak).Append(words, index, end - index + 1).Append(NoBreakEnds);
                }

                index = end + 1;
            }

            return ready.ToString();
        }

        /// <summary>
        /// True where TextMesh Pro would let a line end between two letters and the game's rule does not: after a
        /// letter outside its Chinese and Japanese ones that ends no line, before one of its letters that may begin one.
        /// </summary>
        private static bool WouldTextMeshProBreakAgainstTheRule(char before, char after) =>
            !IsChineseOrJapanese(before) && !char.IsWhiteSpace(before) && NeverEndALine.IndexOf(before) >= 0
            && IsChineseOrJapanese(after) && NeverBeginALine.IndexOf(after) < 0;

        private static bool Within(char letter, char first, char last) => letter >= first && letter <= last;
    }
}
