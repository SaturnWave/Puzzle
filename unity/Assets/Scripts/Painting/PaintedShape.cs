#nullable enable
using System;
using UnityEngine;

namespace Puzzle.Client.Painting
{
    /// <summary>
    /// A shape built in code, on the stage: an object that draws one mesh with the paint material, at its place in the
    /// drawing order. It owns its mesh: showing a new one, or disposing of the shape, destroys the old one, so a
    /// shape that is rebuilt whenever the screen changes leaves nothing behind.
    /// </summary>
    public sealed class PaintedShape : IDisposable
    {
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int MainTextureTiling = Shader.PropertyToID("_MainTex_ST");
        private static readonly int WholeShapeTint = Shader.PropertyToID("_Tint");

        private readonly MeshFilter _filter;
        private readonly MeshRenderer _renderer;
        private readonly Texture? _texture;

        /// <summary>What this shape alone tells the paint material: its texture and how it repeats, and how strongly the shape is drawn.</summary>
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

        /// <summary>Held here too, not only by the filter: when a scene closes, the filter may be gone before the shape is disposed of.</summary>
        private Mesh? _mesh;

        private PaintedShape(string name, Transform parent, Material paint, DrawOrder order, Texture? texture)
        {
            var shape = new GameObject(name);
            shape.transform.SetParent(parent, worldPositionStays: false);
            _filter = shape.AddComponent<MeshFilter>();
            _renderer = shape.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = paint;
            _renderer.sortingOrder = (int)order;
            _texture = texture;
            _properties.SetColor(WholeShapeTint, Color.white);
            RepeatTexture(Vector2.one);
        }

        public Transform Transform => _filter.transform;

        /// <param name="texture">What the shape is painted with; none for plain colours. The shape does not own it.</param>
        public static PaintedShape Create(string name, Transform parent, Material paint, DrawOrder order, Texture? texture = null) =>
            new PaintedShape(name, parent, paint, order, texture);

        /// <summary>Moves the shape to another place in the drawing order.</summary>
        public void DrawAt(DrawOrder order) => _renderer.sortingOrder = (int)order;

        /// <summary>Takes the mesh over and draws it from now on.</summary>
        public void Show(Mesh mesh)
        {
            DestroyMesh();
            _mesh = mesh;
            _filter.sharedMesh = mesh;
        }

        /// <summary>
        /// How often the texture repeats over one unit of the mesh's texture corners: a pattern keeps its size on the
        /// screen while the shape under it is scaled.
        /// </summary>
        public void RepeatTexture(Vector2 timesPerUnit)
        {
            if (_texture != null)
            {
                _properties.SetTexture(MainTexture, _texture);
            }

            _properties.SetVector(MainTextureTiling, new Vector4(timesPerUnit.x, timesPerUnit.y, 0f, 0f));
            _renderer.SetPropertyBlock(_properties);
        }

        /// <summary>
        /// Draws the whole shape at a share of its strength, from 0 (not seen) to 1 (as its corners are painted),
        /// without touching its mesh: how something fades in or out frame by frame.
        /// </summary>
        public void Fade(float strength)
        {
            _properties.SetColor(WholeShapeTint, new Color(1f, 1f, 1f, Mathf.Clamp01(strength)));
            _renderer.SetPropertyBlock(_properties);
        }

        public void Dispose()
        {
            DestroyMesh();
            SceneObjects.DestroyObjectOf(_filter);
        }

        private void DestroyMesh()
        {
            if (_mesh != null)
            {
                SceneObjects.Destroy(_mesh);
                _mesh = null;
            }
        }
    }
}
