using System;

namespace Puzzle.Museum.Catalogue
{
    // Serialization contract of museum/catalogue.json. Field names are camelCase on purpose: Unity's JsonUtility
    // maps public fields verbatim, and System.Text.Json reads them with IncludeFields (the level files do the same).
    // Every value here is raw and unvalidated; CatalogueFileMapper turns it into a MuseumCatalogue or says what is wrong.
#pragma warning disable SA1307, IDE1006, CA1051 // public camelCase fields are the wire format

    [Serializable]
    public sealed class CatalogueFile
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion;

        /// <summary>All twenty rooms, in order; a room whose paintings are not chosen yet has an empty list.</summary>
        public RoomFile[] rooms = Array.Empty<RoomFile>();
    }

    [Serializable]
    public sealed class RoomFile
    {
        public int number;

        /// <summary>The art movement, in Turkish.</summary>
        public string movement = string.Empty;

        /// <summary>True while the movement is only pencilled in.</summary>
        public bool provisionalName;

        /// <summary>The paintings chosen so far, in the order they are restored: at most five.</summary>
        public PaintingFile[] paintings = Array.Empty<PaintingFile>();
    }

    [Serializable]
    public sealed class PaintingFile
    {
        public string id = string.Empty;

        /// <summary>The Turkish title.</summary>
        public string title = string.Empty;
        public string originalTitle = string.Empty;
        public string artist = string.Empty;
        public string year = string.Empty;

        /// <summary>"landscape" or "portrait".</summary>
        public string orientation = string.Empty;

        /// <summary>Path below the paintings folder, with forward slashes.</summary>
        public string image = string.Empty;
        public string source = string.Empty;
        public string licence = string.Empty;
    }
}
