#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Puzzle.Client.Sound
{
    /// <summary>What an active player on an Android phone plays, as far as Android tells another app. A closed set.</summary>
    public enum PlaybackSort
    {
        /// <summary>Something a person listens to: music, a film, a podcast (usage media; content music, movie or speech).</summary>
        Listening,

        /// <summary>Anything else: a game's mix whose content is not said, a click, a ringtone, a navigation voice.</summary>
        Other,
    }

    /// <summary>
    /// How the game reads Android's list of active players (AudioManager.getActivePlaybackConfigurations, API 26, the
    /// game's lowest) to tell whether another app's music plays. No Unity dependency, so the repository's tests
    /// compile it too; <c>AndroidOtherAudio</c> asks the phone.
    /// </summary>
    /// <remarks>
    /// AudioManager.isMusicActive cannot tell: the game's own output (Unity's mixer) is one of the phone's media
    /// players, started for as long as the game is in front, even in silence, so the phone's music stream is always
    /// active (seen on the Pixel 9 Pro Fold, Android 16: one AAudio player of the game's, started, usage media). The
    /// list cannot name the game's own player either (Android hides whose a player is from other apps, and from the
    /// game itself), so the game's player is told apart by what Android says it plays. Unity's AAudio output leaves
    /// its content unsaid (content unknown on that phone), and is then not <see cref="PlaybackSort.Listening"/>; a
    /// stream opened by type (OpenSL ES, older phones) is labelled music, and is. So while the game's output runs and
    /// no other player is among the active ones, one of the listening players is the game's own, and is not counted.
    /// A player of another app that plays without saying what (a game in a window, a stream muted in the background)
    /// is never taken for the player's music. One case stays wrong: on a phone that labels the game's output music,
    /// while another app's unlabelled player is active, the game's own player is counted and its music gives way.
    /// </remarks>
    public static class AndroidPlayback
    {
        /// <summary>android.media.AudioAttributes.USAGE_MEDIA.</summary>
        private const int UsageMedia = 1;

        /// <summary>android.media.AudioAttributes.CONTENT_TYPE_SPEECH, CONTENT_TYPE_MUSIC and CONTENT_TYPE_MOVIE.</summary>
        private const int ContentSpeech = 1;
        private const int ContentMusic = 2;
        private const int ContentMovie = 3;

        /// <summary>The sort of an active player from its AudioAttributes' usage and content type, as Android numbers them.</summary>
        public static PlaybackSort SortOf(int usage, int contentType)
        {
            bool isListenedTo = contentType == ContentMusic || contentType == ContentMovie || contentType == ContentSpeech;
            return usage == UsageMedia && isListenedTo ? PlaybackSort.Listening : PlaybackSort.Other;
        }

        /// <summary>Whether another app plays something a person listens to, from the sorts of the phone's active players.</summary>
        /// <param name="gameOutputRuns">Whether the game's own output runs (Unity's audio output thread), and so is one of the active players.</param>
        public static bool AnotherAppIsHeard(IReadOnlyCollection<PlaybackSort> activePlayers, bool gameOutputRuns)
        {
            if (activePlayers == null)
            {
                throw new ArgumentNullException(nameof(activePlayers));
            }

            int listening = activePlayers.Count(sort => sort == PlaybackSort.Listening);
            bool theGameIsAmongTheListening = gameOutputRuns && listening == activePlayers.Count;
            int listeningOfOtherApps = theGameIsAmongTheListening ? listening - 1 : listening;
            return listeningOfOtherApps > 0;
        }
    }
}
