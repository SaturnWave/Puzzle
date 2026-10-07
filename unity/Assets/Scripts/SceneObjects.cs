using UnityEngine;

namespace Puzzle.Client
{
    /// <summary>
    /// What the game creates in code, it also removes in code. When a scene is closed, Unity destroys the scene's
    /// objects itself, in an order of its own, before or after the game's own clean-up runs: so whatever is asked to
    /// be destroyed here may be gone already, and that is not a mistake.
    /// </summary>
    public static class SceneObjects
    {
        /// <summary>
        /// Destroys an object, a mesh or a texture now, unless Unity has destroyed it already. Unity only destroys at
        /// the end of a frame while a game runs, and the Editor's screenshot command builds the same scene with no
        /// game running, where there is no frame.
        /// </summary>
        public static void Destroy(Object created)
        {
            if (created == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(created);
            }
            else
            {
                Object.DestroyImmediate(created);
            }
        }

        /// <summary>A new scene object under a parent that draws one sprite with a material, at its place in the drawing order.</summary>
        public static SpriteRenderer AddSprite(string name, Transform parent, Sprite picture, Material material, DrawOrder order)
        {
            var sprite = new GameObject(name).AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(parent, worldPositionStays: false);
            sprite.sprite = picture;
            sprite.sharedMaterial = material;
            sprite.sortingOrder = (int)order;
            return sprite;
        }

        /// <summary>Destroys the scene object a part sits on, with all that hangs under it, unless Unity has destroyed it already.</summary>
        public static void DestroyObjectOf(Component part)
        {
            if (part != null)
            {
                Destroy(part.gameObject);
            }
        }
    }
}
