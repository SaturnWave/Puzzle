using UnityEditor;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// How the music's two clips in Assets/Art/Music are imported, whatever their .meta files say and also when
    /// tools/music/cut-loop.py writes them again. Both are kept in the package as Vorbis at a quality of 0.6, stereo at
    /// the source's 48 kHz, which phones mix at without resampling: about 1.5 MB a minute, and transparent for soft
    /// chamber music under the effects. The loop, over a minute and a half, is streamed from the package while it
    /// plays (Streaming), so it costs a small read buffer rather than its whole length in memory. The intro, played
    /// once at the game's very start, is kept compressed in memory and loaded with the scene, so its first note
    /// never waits for a stream to open; it costs its compressed size, a few hundred kilobytes.
    /// </summary>
    public sealed class MusicImport : AssetPostprocessor
    {
        public const string MusicFolder = "Assets/Art/Music";

        /// <summary>Vorbis quality, 0 to 1: 0.6 is well above where soft acoustic music starts to smear, at less than half the size of 1.</summary>
        public const float VorbisQuality = 0.6f;

        /// <summary>The clip played once at the start: the one that is not streamed. Its name is the record's (art/sounds/music.json).</summary>
        public const string IntroFile = "music-intro.wav";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder + "/"))
            {
                return;
            }

            bool isTheIntro = assetPath == $"{MusicFolder}/{IntroFile}";
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = isTheIntro ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = VorbisQuality,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = isTheIntro,
            };
            importer.ClearSampleSettingOverride("Android");
        }
    }
}
