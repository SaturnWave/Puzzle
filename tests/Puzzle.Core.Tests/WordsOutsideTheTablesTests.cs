using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// Every word the player sees comes from a language's table; the game's code says none of its own. The rule that
/// tells a player's words from the code's: the code's own texts (the names of the scene's objects, the messages of
/// exceptions and of the log, the fields of the files the game writes) are in plain English, the code's language;
/// a player's words are in the player's language. So a text between quotation marks in the game's scripts
/// (unity/Assets/Scripts, comments left out) is taken for a player's word, and fails here, when it has a letter
/// outside plain Latin (a Turkish, Korean or any other language's letter), or when one of its words is a word of
/// the Turkish table that the English table does not use as well ("Oyna", "Tamam", "Devam": Turkish written in
/// plain letters). The one file whose texts are letters and not words is the game's alphabet, LetterSet.cs.
/// </summary>
public partial class WordsOutsideTheTablesTests
{
    /// <summary>The game's alphabet: the letters its typefaces are made with, written as letters, which is what it is for.</summary>
    private static readonly string[] Alphabet = { Path.Combine("Art", "LetterSet.cs") };

    /// <summary>A word shorter than this is no telling word: "de", "da" (a number's Turkish suffix) are in plain letters in any language's code.</summary>
    private const int ShortestTellingWord = 3;

    private static readonly Lazy<HashSet<string>> TurkishOnly = new(() =>
    {
        HashSet<string> turkish = WordsOf(TestWords.WordsOf(Language.Turkish));
        turkish.ExceptWith(WordsOf(TestWords.WordsOf(Language.English)));
        return turkish;
    });

    [Fact]
    public void No_script_of_the_game_says_a_players_word_outside_the_tables()
    {
        string scripts = Path.GetDirectoryName(RepoFiles.ClientScriptPath("GameRoot.cs"))!;
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string named = Path.GetRelativePath(scripts, file);
            if (Alphabet.Contains(named))
            {
                continue;
            }

            foreach ((int line, string text) in TextsBetweenQuotationMarks(File.ReadAllText(file)))
            {
                if (IsAPlayersWord(text))
                {
                    offenders.Add($"{named}:{line} \"{text}\"");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Words for the player belong in words/<code>.json, said through Speech:\n" + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData("Oyna", true)]
    [InlineData("Tamam", true)]
    [InlineData("Devam", true)]
    [InlineData("Bölüm {level}", true)]
    [InlineData("Sürükle", true)]
    [InlineData("한국어", true)]
    [InlineData("Español", true)]
    [InlineData("Shop", false)]
    [InlineData("Frame beads", false)]
    [InlineData("Piece {pieceNumber}", false)]
    [InlineData("The level pack has no level {number}.", false)]
    [InlineData("de", false)]
    [InlineData("Test", false)]
    public void The_rule_takes_Turkish_and_any_other_language_for_a_players_words_and_the_codes_English_for_its_own(string text, bool isAPlayersWord)
    {
        Assert.Equal(isAPlayersWord, IsAPlayersWord(text));
    }

    [Fact]
    public void A_text_in_a_comment_is_no_text_of_the_code_and_one_in_the_code_is_found_with_its_line()
    {
        const string Source = "// \"Oyna\" in a comment\n/* \"Tamam\" */ var a = \"Shop\";\nvar b = @\"two \"\"quoted\"\" words\"; char c = '\"';\nvar d = $\"Level {e}\";";

        Assert.Equal(new[] { (2, "Shop"), (3, "two \"\"quoted\"\" words"), (4, "Level {e}") }, TextsBetweenQuotationMarks(Source));
    }

    private static bool IsAPlayersWord(string text)
    {
        if (text.Any(letter => letter > '\u007F' && char.IsLetter(letter)))
        {
            return true;
        }

        return Words().Matches(text).Select(word => word.Value.ToLowerInvariant()).Any(word => word.Length >= ShortestTellingWord && TurkishOnly.Value.Contains(word));
    }

    /// <summary>The words of a table's every text, in small letters; a place's name in braces is no word of it.</summary>
    private static HashSet<string> WordsOf(WordTable table) => table.AllWords
        .SelectMany(entry => Words().Matches(Places().Replace(entry.Words, " ")))
        .Select(word => word.Value.ToLowerInvariant())
        .ToHashSet();

    /// <summary>
    /// The texts between quotation marks in a C# source and the line each begins on, comments and character
    /// literals left out: a plain text with its escapes as written, a verbatim one (@"..") with its doubled quotes,
    /// an interpolated one ($"..") up to the hole of a nested text, which counts as a text of its own.
    /// </summary>
    private static IReadOnlyList<(int Line, string Text)> TextsBetweenQuotationMarks(string source)
    {
        var texts = new List<(int, string)>();
        int line = 1;
        int at = 0;
        while (at < source.Length)
        {
            char c = source[at];
            char next = at + 1 < source.Length ? source[at + 1] : '\0';
            if (c == '\n')
            {
                line++;
                at++;
            }
            else if (c == '/' && next == '/')
            {
                while (at < source.Length && source[at] != '\n')
                {
                    at++;
                }
            }
            else if (c == '/' && next == '*')
            {
                at += 2;
                while (at < source.Length && !(source[at] == '*' && at + 1 < source.Length && source[at + 1] == '/'))
                {
                    line += source[at] == '\n' ? 1 : 0;
                    at++;
                }

                at += 2;
            }
            else if (c == '\'')
            {
                at++;
                while (at < source.Length && source[at] != '\'')
                {
                    at += source[at] == '\\' ? 2 : 1;
                }

                at++;
            }
            else if (c == '"' || ((c == '@' || c == '$') && (next == '"' || next == '@' || next == '$')))
            {
                bool isVerbatim = false;
                while (source[at] != '"')
                {
                    isVerbatim |= source[at] == '@';
                    at++;
                }

                at++;
                int startsOn = line;
                var text = new StringBuilder();
                while (at < source.Length && source[at] != '"' || (isVerbatim && at + 1 < source.Length && source[at] == '"' && source[at + 1] == '"'))
                {
                    if (!isVerbatim && source[at] == '\\')
                    {
                        text.Append(source, at, 2);
                        at += 2;
                        continue;
                    }

                    if (isVerbatim && source[at] == '"')
                    {
                        text.Append("\"\"");
                        at += 2;
                        continue;
                    }

                    line += source[at] == '\n' ? 1 : 0;
                    text.Append(source[at]);
                    at++;
                }

                at++;
                texts.Add((startsOn, text.ToString()));
            }
            else
            {
                at++;
            }
        }

        return texts;
    }

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Words();

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Places();
}
