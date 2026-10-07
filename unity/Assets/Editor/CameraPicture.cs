using System.IO;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>What a camera sees, as a picture or a PNG file. The game draws everything with one camera, so this is a screenshot.</summary>
    public static class CameraPicture
    {
        /// <summary>Renders the camera once at the given size and writes the picture; the camera draws to the screen again afterwards.</summary>
        /// <exception cref="IOException">The file cannot be written.</exception>
        public static void Write(Camera camera, int pixelWidth, int pixelHeight, string path)
        {
            Texture2D picture = Take(camera, pixelWidth, pixelHeight);
            try
            {
                WritePng(picture, path);
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }

        /// <summary>Renders the camera once at the given size. The caller owns the picture and destroys it.</summary>
        public static Texture2D Take(Camera camera, int pixelWidth, int pixelHeight)
        {
            RenderTexture target = RenderTexture.GetTemporary(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                return Read(target);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Writes what was last rendered into a texture. The file has no transparency: a screen has none.</summary>
        /// <exception cref="IOException">The file cannot be written.</exception>
        public static void Save(RenderTexture rendered, string path)
        {
            Texture2D picture = Read(rendered);
            try
            {
                WritePng(picture, path);
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }

        /// <exception cref="IOException">The file cannot be written.</exception>
        public static void WritePng(Texture2D picture, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, picture.EncodeToPNG());
        }

        private static Texture2D Read(RenderTexture rendered)
        {
            var picture = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, mipChain: false);
            RenderTexture activeBefore = RenderTexture.active;
            try
            {
                RenderTexture.active = rendered;
                picture.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                picture.Apply(updateMipmaps: false);
                return picture;
            }
            finally
            {
                RenderTexture.active = activeBefore;
            }
        }
    }
}
