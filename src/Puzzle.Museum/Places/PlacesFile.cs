using System;

namespace Puzzle.Museum.Places
{
    // Serialization contracts of museum/places.json and of a language's labels for it. Field names are camelCase on
    // purpose: Unity's JsonUtility maps public fields verbatim, and System.Text.Json reads them with IncludeFields.
    // Every value here is raw and unvalidated; PlacesFileMapper turns them into MuseumPlaces and PlaceLabelTable or
    // says what is wrong.
#pragma warning disable SA1307, IDE1006, CA1051 // public camelCase fields are the wire format

    /// <summary>museum/places.json: where each painting's pin stands.</summary>
    [Serializable]
    public sealed class PlacesFile
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion;

        public PaintingPlaceFile[] paintings = Array.Empty<PaintingPlaceFile>();
    }

    [Serializable]
    public sealed class PaintingPlaceFile
    {
        public string id = string.Empty;

        /// <summary>Degrees, WGS84.</summary>
        public double lat;
        public double lon;

        /// <summary>A key of the language files' "places".</summary>
        public string place = string.Empty;

        /// <summary>"exact", "city" or "region".</summary>
        public string precision = string.Empty;

        public bool verified;

        /// <summary>A key of the language files' "museums".</summary>
        public string museum = string.Empty;

        /// <summary>A key of the language files' "notes"; null in the file, or empty as Unity's reader gives a null, for none.</summary>
        public string? note;
    }

    /// <summary>
    /// A language's labels for the map, as the game carries them: museum/places/&lt;code&gt;.json writes each kind
    /// as an object of key and text, which tools/unity/sync-engine.js packs into lists of entries, because Unity's
    /// JSON reader has no dictionaries.
    /// </summary>
    [Serializable]
    public sealed class PlaceLabelsFile
    {
        public string language = string.Empty;

        /// <summary>"source", "machine" or "native" (who has read the table).</summary>
        public string review = string.Empty;

        public LabelEntryFile[] places = Array.Empty<LabelEntryFile>();
        public LabelEntryFile[] notes = Array.Empty<LabelEntryFile>();
        public LabelEntryFile[] museums = Array.Empty<LabelEntryFile>();
    }

    [Serializable]
    public sealed class LabelEntryFile
    {
        public string key = string.Empty;
        public string text = string.Empty;
    }
}
