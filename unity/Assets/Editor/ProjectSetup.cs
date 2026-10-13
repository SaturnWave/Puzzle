using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Brings everything the Editor derives up to date, in the order the parts need each other: the render pipeline,
    /// the atlas of the sprites, the art catalogue from the drawings and the typeface, the scene that refers to the
    /// catalogue. Safe to run again at any time; run it after the drawings, the lists of drawings or the lettering
    /// styles change (after node tools/unity/export-sprites.js or export-gallery-art.js, for instance).
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public static void Run()
        {
            try
            {
                BringUpToDate();
                Debug.Log("Project setup: the render pipeline, the art catalogue and the main scene are up to date.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is IOException)
            {
                Debug.LogError($"Project setup failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// What <see cref="Run"/> does, for a tool that must not work with derived files older than their sources:
        /// the package build, which would otherwise carry a catalogue that lacks the drawings added since.
        /// </summary>
        /// <exception cref="InvalidOperationException">A drawing, a font file or a list of them does not fit the others; the message names it.</exception>
        /// <exception cref="IOException">A derived file could not be written.</exception>
        public static void BringUpToDate()
        {
            RenderPipelineSetup.Ensure();
            PieceAtlasBuilder.Ensure();
            ArtCatalogueBuilder.Build();
            PaintingSizesBuilder.Build();
            MainSceneBuilder.Build();
            AssetDatabase.SaveAssets();
        }
    }
}
