#nullable enable
using System;
using UnityEngine;

namespace Puzzle.Client.Cloud
{
    /// <summary>The game's JSON of its documents: Unity's own reader and writer, which map a class's public fields, as the progress file has always been written.</summary>
    public sealed class UnityDocumentJson : IDocumentJson
    {
        public string Write(object document) => JsonUtility.ToJson(document ?? throw new ArgumentNullException(nameof(document)));

        public T? Read<T>(string text)
            where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(text ?? string.Empty);
            }
            catch (ArgumentException)
            {
                return null; // Unity's reader says so when the text is not JSON at all
            }
        }
    }
}
