using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Several pictures of one size side by side in one, each shrunk: the frames of a turn at a glance, in the order
    /// they were added. A picture is shrunk by a whole number, every pixel of the sheet being the mean of a square
    /// of the picture's pixels, so small things stay where they are and nothing shimmers.
    /// </summary>
    public sealed class ContactSheet
    {
        /// <summary>The sheet's own pixels between two pictures and around them.</summary>
        private const int Gap = 8;

        /// <summary>A sheet is no wider than this many pixels: half the largest texture every graphics card takes.</summary>
        private const int WidestSheet = 8192;

        private static readonly Color32 Paper = new Color32(0x12, 0x1A, 0x1C, 0xFF);

        private readonly int _shrinkBy;
        private readonly List<Color32[]> _shrunk = new List<Color32[]>();
        private int _width;
        private int _height;

        /// <exception cref="ArgumentOutOfRangeException">The pictures would not be shrunk but enlarged.</exception>
        public ContactSheet(int shrinkBy)
        {
            _shrinkBy = shrinkBy >= 1
                ? shrinkBy
                : throw new ArgumentOutOfRangeException(nameof(shrinkBy), shrinkBy, "A sheet shrinks its pictures by a whole number from 1 up.");
        }

        public bool IsEmpty => _shrunk.Count == 0;

        /// <exception cref="ArgumentException">The picture is not as large as the ones before it.</exception>
        public void Add(Texture2D picture)
        {
            int width = picture.width / _shrinkBy;
            int height = picture.height / _shrinkBy;
            if (!IsEmpty && (width != _width || height != _height))
            {
                throw new ArgumentException("The pictures of a sheet are all of one size.", nameof(picture));
            }

            _width = width;
            _height = height;
            _shrunk.Add(Shrink(picture.GetPixels32(), picture.width));
        }

        /// <summary>The whole sheet. The caller owns the picture and destroys it.</summary>
        public Texture2D ToPicture()
        {
            // Side by side while the sheet stays a picture a graphics card takes; a longer story goes on in a row below.
            int perRow = Math.Min(_shrunk.Count, Math.Max(1, (WidestSheet - Gap) / (_width + Gap)));
            int rows = (_shrunk.Count + perRow - 1) / perRow;
            int sheetWidth = Gap + perRow * (_width + Gap);
            int sheetHeight = Gap + rows * (_height + Gap);
            var pixels = new Color32[sheetWidth * sheetHeight];
            Array.Fill(pixels, Paper);
            for (int picture = 0; picture < _shrunk.Count; picture++)
            {
                int left = Gap + picture % perRow * (_width + Gap);

                // A texture's rows are counted from its foot: the first row of pictures is the highest.
                int foot = Gap + (rows - 1 - picture / perRow) * (_height + Gap);
                for (int row = 0; row < _height; row++)
                {
                    Array.Copy(_shrunk[picture], row * _width, pixels, (foot + row) * sheetWidth + left, _width);
                }
            }

            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGB24, mipChain: false);
            sheet.SetPixels32(pixels);
            sheet.Apply(updateMipmaps: false);
            return sheet;
        }

        private Color32[] Shrink(Color32[] pixels, int pictureWidth)
        {
            var shrunk = new Color32[_width * _height];
            int squarePixels = _shrinkBy * _shrinkBy;
            for (int row = 0; row < _height; row++)
            {
                for (int column = 0; column < _width; column++)
                {
                    int red = 0;
                    int green = 0;
                    int blue = 0;
                    for (int y = 0; y < _shrinkBy; y++)
                    {
                        int rowStart = (row * _shrinkBy + y) * pictureWidth + column * _shrinkBy;
                        for (int x = 0; x < _shrinkBy; x++)
                        {
                            Color32 pixel = pixels[rowStart + x];
                            red += pixel.r;
                            green += pixel.g;
                            blue += pixel.b;
                        }
                    }

                    shrunk[row * _width + column] = new Color32((byte)(red / squarePixels), (byte)(green / squarePixels), (byte)(blue / squarePixels), 0xFF);
                }
            }

            return shrunk;
        }
    }
}
