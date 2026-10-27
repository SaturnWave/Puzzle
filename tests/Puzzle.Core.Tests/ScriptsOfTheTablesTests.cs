using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// The tables of the languages that are not written in Latin letters are written in their own script: Russian in
/// Cyrillic, Japanese in kana and Chinese characters, Chinese in its characters, Simplified in the mainland's forms
/// and Traditional in Taiwan's. Latin letters stand only for what the game keeps as it is in every language (the
/// names Unity, Android, Aptabase, the stores and Apple's devices, "ID" and "IP", a Roman numeral), and a line that says anything says it in the language's script.
/// A painter's name and a year are not the language's: they are left out.
/// </summary>
public partial class ScriptsOfTheTablesTests
{
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    /// <summary>The Latin words a table of another script may say: names and marks that stay as they are, each with why.</summary>
    private static readonly HashSet<string> KeptAsTheyAre = new(StringComparer.Ordinal)
    {
        "ID",      // the player's id on the settings and support cards, as every language writes it
        "Unity",   // the engine the privacy text names
        "Android", // the system the privacy text names
        "SaturnWave", // the studio's name at the entry page's foot
        "Aptabase", // the service of the play statistics, which the privacy card and the question name
        "Google", "Play", "App", "Store", // the stores that take the payments, named as they name themselves
        "Apple", "iPhone", "iPad", // the maker and the devices the privacy card names for the app identifier
        "IP",      // the internet address the statistics do not keep, as every language writes it
        "Games", "Game", "Center", // Google Play Games and Game Center, which link the account (the privacy card, 2026-10-27)
        "Gaming", "Services", // Unity Gaming Services, which keeps the cloud save, as the privacy card names it
    };

    /// <summary>The words a language says in Latin letters on purpose: Japanese "OK" on the button that closes a card, as Japanese interfaces do.</summary>
    private static readonly HashSet<(Language, string)> SaidInLatinOnPurpose = new() { (Language.Japanese, "OK") };

    /// <summary>
    /// Characters whose Simplified and Traditional forms differ, as pairs: the forms the game's Chinese words use
    /// most (its buttons, rooms, counts and settings), so a line copied from the other table shows here.
    /// </summary>
    private static readonly (char Simplified, char Traditional)[] TwoForms =
    {
        ('简', '簡'), ('体', '體'), ('们', '們'), ('这', '這'), ('个', '個'), ('关', '關'), ('时', '時'), ('开', '開'), ('页', '頁'), ('设', '設'),
        ('门', '門'), ('币', '幣'), ('画', '畫'), ('说', '說'), ('话', '話'), ('钟', '鐘'), ('间', '間'), ('书', '書'), ('长', '長'), ('东', '東'),
        ('车', '車'), ('马', '馬'), ('见', '見'), ('红', '紅'), ('绿', '綠'), ('蓝', '藍'), ('发', '發'), ('术', '術'), ('现', '現'), ('图', '圖'),
        ('务', '務'), ('续', '續'), ('选', '選'), ('择', '擇'), ('级', '級'), ('馆', '館'), ('厅', '廳'), ('尝', '嘗'), ('试', '試'), ('购', '購'),
        ('买', '買'), ('礼', '禮'), ('记', '記'), ('录', '錄'), ('语', '語'), ('块', '塊'), ('笔', '筆'), ('盘', '盤'), ('数', '數'), ('让', '讓'),
    };

    public static TheoryData<Language> OtherScripts() => new() { Language.Russian, Language.Japanese, Language.ChineseSimplified, Language.ChineseTraditional };

    [Theory]
    [MemberData(nameof(OtherScripts))]
    public void A_table_of_another_script_says_in_Latin_letters_only_what_every_language_keeps_as_it_is(Language language)
    {
        Assert.All(LinesOf(language), line =>
        {
            foreach (Match word in LatinWord().Matches(Places().Replace(line.Words, " ")))
            {
                Assert.True(
                    KeptAsTheyAre.Contains(word.Value) || RomanNumeral().IsMatch(word.Value) || SaidInLatinOnPurpose.Contains((language, word.Value)),
                    $"{Languages.CodeOf(language)} {line.What} says \"{word.Value}\" in Latin letters: \"{line.Words}\"");
            }
        });
    }

    [Theory]
    [MemberData(nameof(OtherScripts))]
    public void Every_line_of_a_table_of_another_script_says_what_it_says_in_that_script(Language language)
    {
        Regex script = language == Language.Russian ? Cyrillic() : ChineseOrJapanese();

        Assert.All(LinesOf(language), line =>
        {
            string words = Places().Replace(line.Words, " ");
            bool saysAWord = words.Any(char.IsLetter);
            bool isSaidInLatinOnPurpose = SaidInLatinOnPurpose.Contains((language, words.Trim()));

            // A line that is only a name every language keeps as it is (the studio's) has no word of its own to say.
            bool isANameKeptAsItIs = KeptAsTheyAre.Contains(words.Trim());
            Assert.True(
                !saysAWord || isSaidInLatinOnPurpose || isANameKeptAsItIs || script.IsMatch(words),
                $"{Languages.CodeOf(language)} {line.What} says \"{line.Words}\" in no letter of its script");
        });
    }

    [Fact]
    public void Simplified_Chinese_writes_no_Traditional_form_and_Traditional_Chinese_no_Simplified_one()
    {
        string simplified = string.Concat(LinesOf(Language.ChineseSimplified).Select(line => line.Words));
        string traditional = string.Concat(LinesOf(Language.ChineseTraditional).Select(line => line.Words));

        Assert.Equal(string.Empty, string.Concat(TwoForms.Select(pair => pair.Traditional).Where(simplified.Contains)));
        Assert.Equal(string.Empty, string.Concat(TwoForms.Select(pair => pair.Simplified).Where(traditional.Contains)));
        Assert.Contains("简体中文", simplified);
        Assert.Contains("繁體中文", traditional);
    }

    /// <summary>Every line of a language's tables that is said in the language: its words (each form of a counted one), its rooms' names and its plates' titles.</summary>
    private static IEnumerable<(string What, string Words)> LinesOf(Language language)
    {
        foreach ((WordId id, string words) in TestWords.WordsOf(language).AllWords)
        {
            yield return (id.ToString(), words);
        }

        CaptionFile captions = JsonSerializer.Deserialize<CaptionFile>(File.ReadAllText(RepoFiles.CaptionTablePath(Languages.CodeOf(language))), AsUnityMapsIt)!;
        foreach (RoomCaption room in captions.rooms)
        {
            yield return ($"room {room.number}", room.name);
        }

        foreach (PaintingCaption plate in captions.paintings)
        {
            yield return ($"title of {plate.id}", plate.title);
        }
    }

    /// <summary>A king's number or a century, as Russian writes them in Latin capitals: "Эдуарда VI", "XV век".</summary>
    [GeneratedRegex("^[IVXLC]+$")]
    private static partial Regex RomanNumeral();

    [GeneratedRegex("[A-Za-z]+")]
    private static partial Regex LatinWord();

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Places();

    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex Cyrillic();

    [GeneratedRegex(@"[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}]")]
    private static partial Regex ChineseOrJapanese();
}
