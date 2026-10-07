using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.Painting
{
    /// <summary>
    /// A flat shape while it is being put together in code, corner by corner: the wall, the board's floor, the gilt
    /// frame, the picture wires. Each corner has a place, a colour and a place in the shape's texture; the paint
    /// material draws the texture times the colour, so gradients come from the corners' colours.
    /// </summary>
    public sealed class MeshSketch
    {
        /// <summary>For shapes of plain colour: the paint material's own texture is white all over.</summary>
        public static readonly Vector2 AnywhereInTheTexture = new Vector2(0.5f, 0.5f);

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colours = new List<Color32>();
        private readonly List<Vector2> _textureCorners = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        /// <returns>The corner's number, for <see cref="AddQuad"/>.</returns>
        public int AddCorner(Vector2 place, Color32 colour, Vector2 inTexture)
        {
            _vertices.Add(new Vector3(place.x, place.y, 0f));
            _colours.Add(colour);
            _textureCorners.Add(inTexture);
            return _vertices.Count - 1;
        }

        public int AddCorner(Vector2 place, Color32 colour) => AddCorner(place, colour, AnywhereInTheTexture);

        /// <summary>The four-sided face between four corners, named in order around it.</summary>
        public void AddQuad(int first, int second, int third, int fourth)
        {
            _triangles.Add(first);
            _triangles.Add(second);
            _triangles.Add(third);
            _triangles.Add(first);
            _triangles.Add(third);
            _triangles.Add(fourth);
        }

        /// <summary>An upright rectangle whose colour runs from its lower edge to its upper edge.</summary>
        public void AddRectangle(Rect area, Color32 lowerColour, Color32 upperColour, Rect inTexture)
        {
            int lowerLeft = AddCorner(new Vector2(area.xMin, area.yMin), lowerColour, new Vector2(inTexture.xMin, inTexture.yMin));
            int upperLeft = AddCorner(new Vector2(area.xMin, area.yMax), upperColour, new Vector2(inTexture.xMin, inTexture.yMax));
            int upperRight = AddCorner(new Vector2(area.xMax, area.yMax), upperColour, new Vector2(inTexture.xMax, inTexture.yMax));
            int lowerRight = AddCorner(new Vector2(area.xMax, area.yMin), lowerColour, new Vector2(inTexture.xMax, inTexture.yMin));
            AddQuad(lowerLeft, upperLeft, upperRight, lowerRight);
        }

        public void AddRectangle(Rect area, Color32 colour) =>
            AddRectangle(area, colour, colour, new Rect(AnywhereInTheTexture, Vector2.zero));

        /// <summary>
        /// A net of faces over an area whose every knot takes its colour from where it lies: how a light, a shade or
        /// any colour that changes slowly over the area is painted without a texture.
        /// </summary>
        public void AddNet(Rect area, int columns, int rows, Func<Vector2, Color> colourAt)
        {
            int firstKnot = _vertices.Count;
            for (int row = 0; row <= rows; row++)
            {
                for (int column = 0; column <= columns; column++)
                {
                    var knot = new Vector2(area.xMin + area.width * column / columns, area.yMin + area.height * row / rows);
                    AddCorner(knot, colourAt(knot));
                }
            }

            int knotsInARow = columns + 1;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int lowerLeft = firstKnot + row * knotsInARow + column;
                    AddQuad(lowerLeft, lowerLeft + knotsInARow, lowerLeft + knotsInARow + 1, lowerLeft + 1);
                }
            }
        }

        /// <summary>The caller owns the mesh and destroys it.</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colours);
            mesh.SetUVs(0, _textureCorners);
            mesh.SetTriangles(_triangles, 0);
            return mesh;
        }
    }
}
