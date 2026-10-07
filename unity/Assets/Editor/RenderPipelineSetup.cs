using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Puts the project on the Universal Render Pipeline with its 2D renderer: the game is flat, and the built-in
    /// pipeline the project was created with is deprecated. The two assets are made once and then only assigned again,
    /// so running this twice changes nothing.
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/Renderer2D.asset";
        private const string PipelinePath = SettingsFolder + "/UniversalRenderPipeline.asset";

        public static void Ensure()
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = CreatePipeline();
            }

            DrawEverySpriteWithItsOwnPicture(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // A quality level may name a pipeline of its own; none is to fall back to another one.
            QualitySettings.ForEach(() => QualitySettings.renderPipeline = pipeline);
        }

        /// <summary>
        /// Turns the pipeline's SRP Batcher off. With it on, sprites that had just come onto the stage were drawn
        /// with the picture of other sprites drawn in the same go: a bomb's blot showed a piece of the pieces' atlas,
        /// a brush stroke the white of a spark (seen in the storyboard's pictures of turns with many effects).
        /// </summary>
        private static void DrawEverySpriteWithItsOwnPicture(UniversalRenderPipelineAsset pipeline)
        {
            if (pipeline.useSRPBatcher)
            {
                pipeline.useSRPBatcher = false;
                EditorUtility.SetDirty(pipeline);
            }
        }

        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            Directory.CreateDirectory(SettingsFolder);
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            // The renderer's own shaders and materials are fields that Unity fills from the package, as its menu does.
            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
            // Flat painted sprites: no high dynamic range, and their edges are soft in the drawings themselves.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }
    }
}
