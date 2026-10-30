#nullable enable

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The JSON of the game's documents: the classes with public fields named as a document names them
    /// (Flow/ProgressDocument, Cloud/CloudEnvelope, Cloud/CloudStateFile). It is a seam, so that the cloud's code
    /// has no Unity dependency: the game writes and reads with Unity's own reader (<c>UnityDocumentJson</c>, which
    /// maps public fields), the repository's tests with System.Text.Json set to map fields as Unity does.
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public interface IDocumentJson
    {
        /// <summary>The document as one line of JSON, every public field written.</summary>
        string Write(object document);

        /// <summary>The document the text holds; nothing when the text is no JSON of that shape (not JSON at all, or not an object).</summary>
        T? Read<T>(string text)
            where T : class;
    }
}
