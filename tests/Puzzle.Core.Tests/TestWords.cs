using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Puzzle.Client.Words;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's words as the tests hear them: Turkish, the source, unless a test speaks another language for its own
/// flow of work (<see cref="Speak"/>). The tables are read from the repository, as tools/unity/sync-engine.js
/// packs them for the game.
/// </summary>
internal static class TestWords
{
    /// <summary>The tables' files name their fields as Unity's JSON reader maps them: by the fields themselves.</summary>
    private static readonly JsonSerializerOptions AsUnityMapsIt = new() { IncludeFields = true };

    private static readonly ConcurrentDictionary<Language, Wording> Loaded = new();

    /// <summary>
    /// Before any test runs, the tests' words are left to load: the game's own screens are compiled into this
    /// assembly, and they say everything through <see cref="Speech"/>.
    /// </summary>
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "A test assembly's own words, set before its first test.")]
    internal static void SpeakTheSource() => Speech.SpeakWhenAsked(() => Of(Language.Turkish));

    /// <summary>A language's words as the repository's tables have them: the table of words, and the museum's words (the catalogue's own for Turkish).</summary>
    public static Wording Of(Language language) => Loaded.GetOrAdd(language, Load);

    /// <summary>Speaks a language within the calling test's flow of work until the returned scope is disposed.</summary>
    public static IDisposable Speak(Language language) => Speech.SpeakWithin(Of(language));

    /// <summary>A language's table of words as its file has it.</summary>
    public static WordTable WordsOf(Language language) =>
        WordTable.Of(JsonSerializer.Deserialize<WordFile>(File.ReadAllText(RepoFiles.WordTablePath(Languages.CodeOf(language))), AsUnityMapsIt)!);

    /// <summary>A language's words for the museum: the catalogue's own for Turkish, its file's for the others.</summary>
    public static CaptionTable CaptionsOf(Language language) => language == CaptionTable.CatalogueLanguage
        ? CaptionTable.OfTheCatalogue
        : CaptionTable.Of(JsonSerializer.Deserialize<CaptionFile>(File.ReadAllText(RepoFiles.CaptionTablePath(Languages.CodeOf(language))), AsUnityMapsIt)!);

    private static Wording Load(Language language) => new(WordsOf(language), CaptionsOf(language));
}
