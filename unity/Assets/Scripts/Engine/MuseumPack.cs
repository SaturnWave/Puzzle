#nullable enable
using System;
using Puzzle.Museum.Catalogue;
using UnityEngine;

namespace Puzzle.Client.Engine
{
    /// <summary>
    /// The museum's catalogue inside a built game: museum/catalogue.json as tools/unity/sync-engine.js copies it, and
    /// where the pictures it names are found. The catalogue's file model is the museum library's own, which is laid
    /// out for Unity's JSON reader.
    /// </summary>
    public static class MuseumPack
    {
        /// <summary>Resources/Museum.json, named without its extension.</summary>
        private const string CatalogueResource = "Museum";

        /// <summary>The folder below a Resources folder that the catalogue's picture paths start from.</summary>
        private const string PicturesResourceFolder = "Paintings";

        /// <summary>The catalogue the game carries with it.</summary>
        /// <exception cref="CatalogueFormatException">The catalogue is missing or is not a valid catalogue.</exception>
        public static MuseumCatalogue LoadPacked()
        {
            TextAsset? packed = Resources.Load<TextAsset>(CatalogueResource);
            if (packed == null)
            {
                throw new CatalogueFormatException("The game carries no museum catalogue; run node tools/unity/sync-engine.js from the repository root.");
            }

            return Read(packed.text);
        }

        /// <exception cref="CatalogueFormatException">The text is not a valid catalogue.</exception>
        public static MuseumCatalogue Read(string json)
        {
            CatalogueFile? file;
            try
            {
                file = JsonUtility.FromJson<CatalogueFile>(json);
            }
            catch (ArgumentException notJson)
            {
                throw new CatalogueFormatException($"The museum catalogue is not JSON: {notJson.Message}", notJson);
            }

            if (file == null)
            {
                throw new CatalogueFormatException("The museum catalogue is empty.");
            }

            return CatalogueFileMapper.ToCatalogue(file);
        }

        /// <summary>
        /// The name <c>Resources.Load</c> finds a painting's picture by: its path below the pictures folder, without
        /// the extension. Pictures are loaded one at a time by this name, when a screen shows them, so that a
        /// hundred paintings never sit in memory together.
        /// </summary>
        public static string PictureResourceOf(Artwork artwork)
        {
            int extensionAt = artwork.ImageFile.LastIndexOf('.');
            string withoutExtension = extensionAt < 0 ? artwork.ImageFile : artwork.ImageFile.Substring(0, extensionAt);
            return $"{PicturesResourceFolder}/{withoutExtension}";
        }
    }
}
