namespace Puzzle.Client.Sound
{
    /// <summary>
    /// How loud the game plays what it plays, as an AudioSource's volume (a linear share of the clip's own level). The
    /// effects and the music are set together here because the music's level is chosen against the effects'. No Unity
    /// dependency, so the repository's tests compile it too.
    /// </summary>
    public static class SoundLevels
    {
        /// <summary>The stand-in effects are interface sounds heard over and over: they are played below their full level, all alike (about -3 dB).</summary>
        public const float Effects = 0.7f;

        /// <summary>
        /// The music sits under the effects (docs/music/MUSIC_BRIEF.md §1): 0.4 is about 5 dB under the effects' 0.7.
        /// Its clips are cut to -20 dBFS RMS with peaks under -1 dBFS (art/sounds/music.json), while an effect's file
        /// peaks near -1 dBFS in a tenth of a second; so the music's body is heard about 20 dB under an effect's
        /// attack, and its loudest note still a few dB under it. Chosen by measure, not by ear: the owner's ear on the
        /// phone has the last word (a phone test), and this is the one number to change.
        /// </summary>
        public const float Music = 0.4f;
    }
}
