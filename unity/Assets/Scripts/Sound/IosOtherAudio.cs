#nullable enable
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Whether another app's music plays on the iPhone the game runs on: AVAudioSession's
    /// secondaryAudioShouldBeSilencedHint, read through Plugins/iOS/OtherAudio.mm. The hint is about other apps' primary
    /// (non-mixable) audio only, so the game's own output never counts.
    /// </summary>
    public sealed class IosOtherAudio : IOtherAudio
    {
        // An int, not a bool: a C bool is one byte, while the marshaller reads four.
        public bool IsPlaying => PuzzleOtherAudioShouldSilenceTheMusic() != 0;

        [DllImport("__Internal")]
        private static extern int PuzzleOtherAudioShouldSilenceTheMusic();
    }

    public static partial class OtherAudio
    {
        static partial void AskTheIPhone(ref IOtherAudio? iPhone) => iPhone = new IosOtherAudio();
    }
}
#endif
