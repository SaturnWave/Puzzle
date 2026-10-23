using Puzzle.Client.Sound;

namespace Puzzle.Core.Tests;

/// <summary>
/// How the game reads Android's list of active players to tell another app's music from its own output, which is
/// always one of them while the game is in front. <c>AndroidPlayback</c> is the Unity project's source file, compiled
/// into these tests; the numbers are android.media.AudioAttributes' own. The first cases are the lists seen on the
/// Pixel 9 Pro Fold (Android 16) on 2026-10-23: the game's output an AAudio player, usage media, content unknown.
/// </summary>
public class AndroidPlaybackTests
{
    private const int UsageMedia = 1;
    private const int UsageNotification = 5;
    private const int UsageGame = 14;

    private const int ContentUnknown = 0;
    private const int ContentSpeech = 1;
    private const int ContentMusic = 2;
    private const int ContentMovie = 3;
    private const int ContentSonification = 4;

    private static readonly PlaybackSort GamesOwnOutput = AndroidPlayback.SortOf(UsageMedia, ContentUnknown);
    private static readonly PlaybackSort Spotify = AndroidPlayback.SortOf(UsageMedia, ContentMusic);
    private static readonly PlaybackSort StreamInTheBackground = AndroidPlayback.SortOf(UsageMedia, ContentUnknown);

    [Theory]
    [InlineData(UsageMedia, ContentMusic)]
    [InlineData(UsageMedia, ContentMovie)]
    [InlineData(UsageMedia, ContentSpeech)]
    public void Music_a_film_or_a_podcast_is_something_a_person_listens_to(int usage, int content)
    {
        Assert.Equal(PlaybackSort.Listening, AndroidPlayback.SortOf(usage, content));
    }

    [Theory]
    [InlineData(UsageMedia, ContentUnknown)]
    [InlineData(UsageMedia, ContentSonification)]
    [InlineData(UsageGame, ContentMusic)]
    [InlineData(UsageNotification, ContentSonification)]
    public void A_mix_whose_content_is_not_said_a_click_a_game_or_a_ringtone_is_not(int usage, int content)
    {
        Assert.Equal(PlaybackSort.Other, AndroidPlayback.SortOf(usage, content));
    }

    [Fact]
    public void The_games_own_output_alone_is_not_another_apps_music()
    {
        Assert.False(AndroidPlayback.AnotherAppIsHeard(new[] { GamesOwnOutput }, gameOutputRuns: true));
    }

    [Fact]
    public void Music_beside_the_games_own_output_is_another_apps()
    {
        Assert.True(AndroidPlayback.AnotherAppIsHeard(new[] { GamesOwnOutput, Spotify }, gameOutputRuns: true));
    }

    [Fact]
    public void Another_apps_stream_that_does_not_say_what_it_plays_is_not_taken_for_music()
    {
        Assert.False(AndroidPlayback.AnotherAppIsHeard(new[] { GamesOwnOutput, StreamInTheBackground }, gameOutputRuns: true));
    }

    [Fact]
    public void Where_the_phone_labels_the_games_output_music_the_game_is_not_counted_and_another_app_still_is()
    {
        PlaybackSort gameLabelledMusic = AndroidPlayback.SortOf(UsageMedia, ContentMusic);

        Assert.False(AndroidPlayback.AnotherAppIsHeard(new[] { gameLabelledMusic }, gameOutputRuns: true));
        Assert.True(AndroidPlayback.AnotherAppIsHeard(new[] { gameLabelledMusic, Spotify }, gameOutputRuns: true));
    }

    [Fact]
    public void With_the_games_output_stopped_every_listening_player_is_another_apps()
    {
        Assert.True(AndroidPlayback.AnotherAppIsHeard(new[] { Spotify }, gameOutputRuns: false));
        Assert.False(AndroidPlayback.AnotherAppIsHeard(Array.Empty<PlaybackSort>(), gameOutputRuns: false));
        Assert.False(AndroidPlayback.AnotherAppIsHeard(Array.Empty<PlaybackSort>(), gameOutputRuns: true));
    }

    [Fact]
    public void A_missing_list_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => AndroidPlayback.AnotherAppIsHeard(null!, gameOutputRuns: true));
    }
}
