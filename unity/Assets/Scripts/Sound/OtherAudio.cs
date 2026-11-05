#nullable enable

namespace Puzzle.Client.Sound
{
    /// <summary>
    /// Whether another app's audio plays now: the player's own music (Spotify, YouTube Music and so on), a film, a
    /// podcast. Never the game's own output, whatever the game plays or holds: an answer that heard the game itself
    /// would silence its music for good. Asked on the game's main thread, at start, when the game comes to the front
    /// and every few seconds while it runs (<see cref="MusicYield"/>), so an answer is cheap.
    /// </summary>
    public interface IOtherAudio
    {
        bool IsPlaying { get; }
    }

    /// <summary>
    /// Where the game cannot ask the device (the Editor, a desktop build, an iPhone until the iOS seam is filled):
    /// nothing else plays. The Editor's music check, and nothing else, sets it to play another app's music and to
    /// stop it again. No Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class OtherAudioStandIn : IOtherAudio
    {
        public bool IsPlaying { get; set; }
    }

    /// <summary>
    /// The device's answer to whether another app's audio plays: Android's list of active players
    /// (<c>AndroidOtherAudio</c>); on an iPhone, the audio session's hint, which the iOS branch adds through
    /// <c>AskTheIPhone</c>; anywhere else nothing else plays.
    /// </summary>
    public static partial class OtherAudio
    {
        public static IOtherAudio OfThisDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return AndroidOtherAudio.OfThisPhone();
#else
            IOtherAudio? iPhone = null;
            AskTheIPhone(ref iPhone);
            return iPhone ?? new OtherAudioStandIn();
#endif
        }

        /// <summary>
        /// The seam for iOS, empty on this branch (the call is then compiled away): the iOS branch adds a file that
        /// implements this partial method under <c>#if UNITY_IOS &amp;&amp; !UNITY_EDITOR</c> and sets
        /// <paramref name="iPhone"/> to its answer, AVAudioSession's secondaryAudioShouldBeSilencedHint read through a
        /// small Objective-C plugin. Nothing else changes for it.
        /// </summary>
        static partial void AskTheIPhone(ref IOtherAudio? iPhone);
    }
}
