using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the sounds of Assets/Art/Sounds are imported, whatever their .meta files say and also when
    /// tools/unity/fetch-sounds.js writes them again: short clips that must sound the moment they are asked for, so
    /// each is loaded with the scene and decompressed then (a few hundred kilobytes in all, which also lets the
    /// catalogue's builder read their samples to measure them), and kept in the package as Vorbis at its best quality.
    /// </summary>
    public sealed class SoundImport : AssetPostprocessor
    {
        public const string SoundsFolder = "Assets/Art/Sounds";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SoundsFolder + "/"))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 1f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = true,
            };
            importer.ClearSampleSettingOverride("Android");
        }
    }
}
