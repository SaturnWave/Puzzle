using System.IO;
using System.Text.Json;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the credits card says (the settings card's row "Künye"): the licence notices stay in English and say word for
/// word what the licence records say (art/avatar, art/fonts, art/paintings, art/sounds, unity/Packages), so a record
/// and the card cannot drift apart, nor the software's record and the packages the game pins; every notice is written
/// in the game's letters; and the card's title and headings are said in every language the game offers.
/// <c>CreditWords</c> is the Unity project's source file, compiled into these tests.
/// </summary>
public class CreditWordsTests
{
    [Fact]
    public void The_painters_credit_lines_and_his_bodys_licence_are_those_of_his_licence_record()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("art/avatar/licences.json")));
        JsonElement[] sources = record.RootElement.GetProperty("sources").EnumerateArray().ToArray();
        JsonElement rocketbox = sources.Single(source => source.GetProperty("licence").GetString() == "MIT License");

        Assert.Equal(CreditWords.EinarLine, sources[0].GetProperty("creditLine").GetString());
        Assert.Equal(CreditWords.RocketboxLine, rocketbox.GetProperty("creditLine").GetString());
        Assert.Equal(CreditWords.PolyHavenLine, sources[2].GetProperty("creditLine").GetString());
        Assert.Equal(rocketbox.GetProperty("licenceText").EnumerateArray().Select(line => line.GetString()!), CreditWords.RocketboxLicence);

        // CC BY asks for the credit and for a word that the work was changed.
        Assert.Contains("CC BY 4.0", CreditWords.EinarLine, StringComparison.Ordinal);
        Assert.Contains("modified", CreditWords.EinarLine, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2020 Microsoft", CreditWords.RocketboxLicence);
    }

    [Fact]
    public void Every_typeface_the_game_carries_is_named_with_its_copyright_line_and_its_licence()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("art/fonts/licences.json")));

        foreach (JsonElement font in record.RootElement.GetProperty("fonts").EnumerateArray())
        {
            string family = font.GetProperty("family").GetString()!;
            string copyright = font.GetProperty("copyright").GetString()!.Replace("©", "(c)", StringComparison.Ordinal);
            Assert.True(
                CreditWords.TypefaceLines.Any(line => line.Contains(family, StringComparison.Ordinal) && line.Contains(copyright, StringComparison.Ordinal)),
                $"no line names {family} with \"{copyright}\"");
            Assert.Equal("SIL Open Font License, Version 1.1", font.GetProperty("licence").GetString());
        }

        Assert.Contains("SIL Open Font License, Version 1.1", CreditWords.TypefaceLines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Every_museum_whose_photograph_the_game_carries_is_named()
    {
        string[] museums = Directory.GetDirectories(Path.GetDirectoryName(Path.GetDirectoryName(RepoFiles.RepositoryFile("art/paintings/room-01/licences.json")))!)
            .SelectMany(room => JsonDocument.Parse(File.ReadAllText(Path.Combine(room, "licences.json"))).RootElement.GetProperty("paintings").EnumerateArray())
            .Select(painting => painting.GetProperty("museum").GetString()!)
            .Distinct()
            .ToArray();

        Assert.True(museums.Length >= 10, $"only {museums.Length} museums were read");
        Assert.All(museums, museum => Assert.Contains(museum, CreditWords.PaintingsLine, StringComparison.Ordinal));
    }

    [Fact]
    public void The_sounds_are_named_with_their_author_and_licence()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.SoundLicencesPath));

        foreach (JsonElement sound in record.RootElement.GetProperty("sounds").EnumerateArray())
        {
            Assert.Contains(sound.GetProperty("author").GetString()!, CreditWords.SoundsLine, StringComparison.Ordinal);
            Assert.StartsWith("Creative Commons CC0 1.0 Universal", sound.GetProperty("licence").GetString()!, StringComparison.Ordinal);
        }

        Assert.Contains("Creative Commons CC0 1.0 Universal", CreditWords.SoundsLine, StringComparison.Ordinal);
    }

    [Fact]
    public void The_software_the_game_carries_is_named_with_its_licence_and_its_MIT_notice_is_shown_whole_as_its_record_has_it()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("unity/Packages/licences.json")));
        JsonElement[] packages = record.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        string[] mitCopyrights = packages.Where(package => package.GetProperty("licence").GetString() == "MIT License")
            .SelectMany(package => package.GetProperty("copyright").EnumerateArray().Select(line => line.GetString()!))
            .ToArray();
        string[] mitText = record.RootElement.GetProperty("mitLicenceText").EnumerateArray().Select(line => line.GetString()!).ToArray();

        Assert.Equal(new[] { "MIT License" }.Concat(mitCopyrights).Concat(mitText), CreditWords.SoftwareLicence);
        Assert.Contains("Copyright (c) 2023 Aptabase", CreditWords.SoftwareLicence);
        Assert.Contains("Copyright (c) 2007 James Newton-King", CreditWords.SoftwareLicence);
        Assert.Equal(CreditWords.RocketboxLicence.Skip(2), CreditWords.SoftwareLicence.Skip(1 + mitCopyrights.Length));
        foreach (JsonElement package in packages)
        {
            string version = package.GetProperty("version").GetString()!;
            Assert.True(
                CreditWords.SoftwareLines.Any(line => line.Contains(version, StringComparison.Ordinal)),
                $"no line of the software names {package.GetProperty("package").GetString()} {version}");
        }

        foreach (JsonElement notice in packages.Single(package => package.GetProperty("package").GetString() == "com.unity.purchasing").GetProperty("notices").EnumerateArray()
            .Where(notice => notice.GetProperty("licence").GetString() == "Apache License 2.0"))
        {
            Assert.Contains(notice.GetProperty("copyright").GetString()!, CreditWords.SoftwareLines[2], StringComparison.Ordinal);
        }

        Assert.Contains("Apache License 2.0", CreditWords.SoftwareLines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void The_software_record_names_the_versions_the_project_pins()
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("unity/Packages/licences.json")));
        using JsonDocument locked = JsonDocument.Parse(File.ReadAllText(RepoFiles.RepositoryFile("unity/Packages/packages-lock.json")));
        JsonElement dependencies = locked.RootElement.GetProperty("dependencies");

        foreach (JsonElement package in record.RootElement.GetProperty("packages").EnumerateArray())
        {
            string name = package.GetProperty("package").GetString()!;
            JsonElement pinned = dependencies.GetProperty(name);
            if (package.TryGetProperty("pinnedAt", out JsonElement commit))
            {
                Assert.Equal(commit.GetString(), pinned.GetProperty("hash").GetString());
            }
            else
            {
                Assert.Equal(package.GetProperty("version").GetString(), pinned.GetProperty("version").GetString());
            }
        }
    }

    [Fact]
    public void Every_credit_line_is_written_in_the_games_letters()
    {
        IEnumerable<string> lines = new[] { CreditWords.EinarLine, CreditWords.RocketboxLine, CreditWords.PolyHavenLine, CreditWords.PaintingsLine, CreditWords.SoundsLine }
            .Concat(CreditWords.RocketboxLicence)
            .Concat(CreditWords.TypefaceLines)
            .Concat(CreditWords.SoftwareLines)
            .Concat(CreditWords.SoftwareLicence);

        Assert.All(lines, line => Assert.Equal(string.Empty, LetterSet.LettersOutside(line)));
    }

    [Theory]
    [MemberData(nameof(WordTablesTests.EveryLanguage), MemberType = typeof(WordTablesTests))]
    public void The_credits_card_says_its_title_its_first_line_and_its_headings_in_every_language_and_its_notices_in_English(Language language)
    {
        using IDisposable speaking = TestWords.Speak(language);

        IReadOnlyList<CreditSection> sections = CreditWords.Sections;

        Assert.False(string.IsNullOrWhiteSpace(CreditWords.Title));
        Assert.False(string.IsNullOrWhiteSpace(CreditWords.InEnglish));
        Assert.Equal(7, sections.Count);
        Assert.Null(sections[1].Heading);
        Assert.Null(sections[6].Heading);
        Assert.All(sections.Where(section => section != sections[1] && section != sections[6]), section => Assert.False(string.IsNullOrWhiteSpace(section.Heading)));
        Assert.Equal(CreditWords.EinarLine, sections[0].Lines[0]);
        Assert.Equal(CreditWords.RocketboxLicence, sections[1].Lines);
        Assert.Equal(CreditWords.SoftwareLines, sections[5].Lines);
        Assert.Equal(CreditWords.SoftwareLicence, sections[6].Lines);
    }

    [Fact]
    public void A_part_of_the_credits_has_a_line_at_least()
    {
        Assert.Throws<ArgumentException>(() => new CreditSection("Ressam", Array.Empty<string>()));
    }
}
