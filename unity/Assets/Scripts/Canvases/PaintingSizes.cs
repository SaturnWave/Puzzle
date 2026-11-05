#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// The true size in pixels of each painting's picture, by the path the catalogue names it with. The game needs
    /// it because the pictures are stored stretched to powers of two (only then can they carry mipmaps and stay
    /// compressed, see the Editor's PaintingImport), so a texture no longer tells the proportions of its painting.
    /// The list is Resources/Paintings/sizes.json, written by the Editor from the picture files (PaintingSizesBuilder).
    /// </summary>
    public sealed class PaintingSizes
    {
        /// <summary>Resources/Paintings/sizes.json, named without its extension.</summary>
        public const string Resource = "Paintings/sizes";

        private readonly Dictionary<string, float> _aspectByImage;

        private PaintingSizes(Dictionary<string, float> aspectByImage)
        {
            _aspectByImage = aspectByImage;
        }

        /// <summary>The list a built game carries; an empty one when the game carries none (every aspect is then read from its texture).</summary>
        public static PaintingSizes LoadPacked()
        {
            TextAsset? packed = Resources.Load<TextAsset>(Resource);
            return packed == null ? new PaintingSizes(new Dictionary<string, float>()) : Read(packed.text);
        }

        public static PaintingSizes Read(string json)
        {
            PaintingSizesFile file = JsonUtility.FromJson<PaintingSizesFile>(json) ?? new PaintingSizesFile();
            var aspects = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (PaintingSizeFile picture in file.pictures)
            {
                if (picture.width > 0 && picture.height > 0)
                {
                    aspects[picture.image] = (float)picture.width / picture.height;
                }
            }

            return new PaintingSizes(aspects);
        }

        /// <summary>Width over height of the picture the catalogue names <paramref name="imageFile"/>; nothing when the list does not know it.</summary>
        public float? AspectOf(string imageFile) =>
            _aspectByImage.TryGetValue(imageFile, out float aspect) ? aspect : (float?)null;
    }

    /// <summary>The list's file format; public fields because Unity's JSON reader maps fields.</summary>
    [Serializable]
    public sealed class PaintingSizesFile
    {
        public PaintingSizeFile[] pictures = Array.Empty<PaintingSizeFile>();
    }

    [Serializable]
    public sealed class PaintingSizeFile
    {
        /// <summary>The picture's path as the catalogue writes it, for example "room-01/water-lilies.jpg".</summary>
        public string image = string.Empty;
        public int width;
        public int height;
    }
}
