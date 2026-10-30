using System;
using System.IO;
using Puzzle.Client.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Writes the game's only scene, Assets/Scenes/Main.unity: one object with the game root on it, which knows the
    /// art catalogue. The camera, the board and all else are built in code when the game starts, so the scene never
    /// needs the Editor's window, and what a build shows is what the screenshot command shows.
    /// </summary>
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        /// <exception cref="InvalidOperationException">The art catalogue is not built yet, or the scene cannot be written.</exception>
        public static void Build()
        {
            ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
            if (art == null)
            {
                throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; build the art catalogue first.");
            }

            // Writing the same scene again would only give its object new ids, and the file a difference without a change.
            if (!HoldsOnlyTheGameRoot(art))
            {
                Write(art);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, enabled: true) };
        }

        private static bool HoldsOnlyTheGameRoot(ArtCatalogue art)
        {
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            GameObject[] roots = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single).GetRootGameObjects();
            return roots.Length == 1
                && roots[0].transform.childCount == 0
                && roots[0].TryGetComponent(out GameRoot root)
                && new SerializedObject(root).FindProperty(GameRoot.ArtField).objectReferenceValue == art;
        }

        private static void Write(ArtCatalogue art)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; // the camera clears to the wall's colour; no sky is ever drawn
            var root = new GameObject("Game").AddComponent<GameRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty(GameRoot.ArtField).objectReferenceValue = art;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"{ScenePath} could not be written.");
            }
        }
    }
}
