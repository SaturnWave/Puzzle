#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The world map itself on its sheet (MapWorldTR): the sea with its waves, the graticule, the light along the
    /// coasts, the land, its coastlines, the lakes and their shores, and the dark at the sheet's edges. The land and
    /// its lines are the shared meshes of <see cref="WorldMapMeshes"/>, moved and scaled as the camera frames them;
    /// the lines keep their width in points at every zoom in their shader, so a camera that moves sets a few numbers
    /// and builds nothing. Everything of the map that runs past the sheet is cut off in its shader.
    /// </summary>
    public sealed class WorldMapDrawing : IDisposable
    {
        private static readonly int CameraId = Shader.PropertyToID("_Camera");
        private static readonly int SheetId = Shader.PropertyToID("_Sheet");
        private static readonly int SheetHeightId = Shader.PropertyToID("_SheetHeight");
        private static readonly int ColourId = Shader.PropertyToID("_Colour");
        private static readonly int HalfWidthId = Shader.PropertyToID("_HalfWidth");
        private static readonly int DashId = Shader.PropertyToID("_Dash");

        /// <summary>The design's strokes (MapWorldTR): the coast 1.1 points in #94713C, the lakes' shores 0.8, the graticule 0.8 in #EAF7F3 at 17 %, dashed 3 and 3.</summary>
        private static readonly Line Coast = new Line(Tint.Of(0x94713C), 1.1f, dash: 0f);
        private static readonly Line Shore = new Line(Tint.Of(0x94713C), 0.8f, dash: 0f);
        private static readonly Line Graticule = new Line(Tint.Of(0xEAF7F3, 0.17f), 0.8f, dash: 6f);

        private readonly Transform _root;
        private readonly Transform _content;
        private readonly MeshRenderer _sea;
        private readonly MeshRenderer _vignette;
        private readonly List<MeshRenderer> _onTheMap = new List<MeshRenderer>();
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly Mesh _seaQuad;
        private readonly Mesh _vignetteQuad;
        private Box _sheet;

        public WorldMapDrawing(Transform parent, ArtCatalogue art)
        {
            _root = new GameObject("World map").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _seaQuad = new Mesh { name = "Sea" };
            _vignetteQuad = new Mesh { name = "Vignette" };
            _sea = Renderer("Sea", _root, art.MaterialOf(MapInk.Sea), DrawOrder.MapSea, _seaQuad);
            _vignette = Renderer("Vignette", _root, art.MaterialOf(MapInk.Vignette), DrawOrder.MapVignette, _vignetteQuad);

            _content = new GameObject("Map").transform;
            _content.SetParent(_root, worldPositionStays: false);
            OnTheMap("Graticule", art.MaterialOf(MapInk.Line), DrawOrder.MapGraticule, WorldMapMeshes.Graticule, Graticule);
            OnTheMap("Glow", art.MaterialOf(MapInk.Glow), DrawOrder.MapGlow, WorldMapMeshes.Coastlines, null);
            OnTheMap("Land", art.MaterialOf(MapInk.Fill), DrawOrder.MapLand, WorldMapMeshes.Land, null);
            OnTheMap("Coast", art.MaterialOf(MapInk.Line), DrawOrder.MapCoast, WorldMapMeshes.Coastlines, Coast);
            OnTheMap("Lakes", art.MaterialOf(MapInk.Fill), DrawOrder.MapLakes, WorldMapMeshes.Lakes, null);
            OnTheMap("Shores", art.MaterialOf(MapInk.Line), DrawOrder.MapLakeShore, WorldMapMeshes.Shores, Shore);
        }

        /// <summary>Lays the sea and the dark over a sheet, in the points of what the map stands on.</summary>
        public void Fit(Box sheet)
        {
            _sheet = sheet;
            SetQuad(_seaQuad, sheet, new Vector2(0f, 0f), new Vector2(sheet.Width, sheet.Height));
            SetQuad(_vignetteQuad, sheet, new Vector2(0f, 0f), new Vector2(1f, 1f));
            _properties.Clear();
            _properties.SetFloat(SheetHeightId, sheet.Height);
            _sea.SetPropertyBlock(_properties);
        }

        /// <summary>Draws the map as a frame of the camera has it on the sheet.</summary>
        public void Show(MapFrame frame)
        {
            _content.localPosition = new Vector3(_sheet.Left + frame.OffsetX, _sheet.Top - frame.OffsetY, 0f);
            _content.localScale = new Vector3(frame.Scale, frame.Scale, 1f);
            var camera = new Vector4(frame.OffsetX, frame.OffsetY, frame.Scale, 0f);
            var sheet = new Vector4(_sheet.Width, _sheet.Height, 0f, 0f);
            foreach (MeshRenderer renderer in _onTheMap)
            {
                renderer.GetPropertyBlock(_properties);
                _properties.SetVector(CameraId, camera);
                _properties.SetVector(SheetId, sheet);
                renderer.SetPropertyBlock(_properties);
            }
        }

        public void Dispose()
        {
            SceneObjects.Destroy(_seaQuad);
            SceneObjects.Destroy(_vignetteQuad);
            SceneObjects.DestroyObjectOf(_root);
        }

        private void OnTheMap(string name, Material material, DrawOrder order, Mesh mesh, Line? line)
        {
            MeshRenderer renderer = Renderer(name, _content, material, order, mesh);
            if (line is Line drawn)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetColor(ColourId, drawn.Colour);
                properties.SetFloat(HalfWidthId, drawn.Width / 2f);
                properties.SetFloat(DashId, drawn.Dash);
                renderer.SetPropertyBlock(properties);
            }

            _onTheMap.Add(renderer);
        }

        /// <summary>A renderer of a mesh it does not own (the map's meshes are shared, the quads this drawing's own).</summary>
        private static MeshRenderer Renderer(string name, Transform parent, Material material, DrawOrder order, Mesh mesh)
        {
            var shape = new GameObject(name);
            shape.transform.SetParent(parent, worldPositionStays: false);
            shape.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = shape.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = (int)order;
            return renderer;
        }

        /// <summary>A quad over the sheet; its uv run from <paramref name="topLeft"/> at the sheet's top left corner to <paramref name="footRight"/> at its foot right.</summary>
        private static void SetQuad(Mesh quad, Box sheet, Vector2 topLeft, Vector2 footRight)
        {
            quad.Clear();
            quad.SetVertices(new[]
            {
                new Vector3(sheet.Left, sheet.Top, 0f), new Vector3(sheet.Right, sheet.Top, 0f),
                new Vector3(sheet.Right, sheet.Bottom, 0f), new Vector3(sheet.Left, sheet.Bottom, 0f),
            });
            quad.SetUVs(0, new[]
            {
                new Vector2(topLeft.x, topLeft.y), new Vector2(footRight.x, topLeft.y),
                new Vector2(footRight.x, footRight.y), new Vector2(topLeft.x, footRight.y),
            });
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        }

        /// <summary>A line of the map: its colour, its width in points, and the period of its dash and gap (nothing for a solid line).</summary>
        private readonly struct Line
        {
            public Line(Color colour, float width, float dash)
            {
                Colour = colour;
                Width = width;
                Dash = dash;
            }

            public Color Colour { get; }

            public float Width { get; }

            public float Dash { get; }
        }
    }
}
