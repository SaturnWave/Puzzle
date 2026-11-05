using System.IO;
using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The record of Google Play uploads and the versionCode of the next bundle (2026-10-28): the first bundle is
/// versionCode 1, every later one is one more than the last upload's, and a record that does not rise line by line, or
/// a line that is not "versionCode versionName day track", stops the build. <c>PlayUploads</c> is the Unity project's
/// Editor source file, compiled into these tests; the bundle build reads release/google-play-uploads.txt with it
/// (unity/Assets/Editor/PlayBundle.cs).
/// </summary>
public class PlayUploadsTests
{
    private const string Header = "# Every bundle uploaded to Google Play, one line each.\n#   versionCode versionName day track\n\n";

    [Fact]
    public void The_first_bundle_is_version_code_one()
    {
        Assert.Empty(PlayUploads.Read(Header));
        Assert.Equal(1, PlayUploads.NextVersionCode(PlayUploads.Read(Header)));
        Assert.Equal(1, PlayUploads.NextVersionCode(PlayUploads.Read(string.Empty)));
    }

    [Fact]
    public void A_bundle_after_uploads_is_one_more_than_the_last()
    {
        IReadOnlyList<PlayUpload> uploads = PlayUploads.Read(Header + "1 1.0 2026-10-28 closed\r\n2 1.0 2026-11-02 Closed\n\n7 1.1 2026-12-01 production\n");

        Assert.Equal(new[] { 1, 2, 7 }, uploads.Select(upload => upload.VersionCode));
        Assert.Equal(new[] { PlayTrack.Closed, PlayTrack.Closed, PlayTrack.Production }, uploads.Select(upload => upload.Track));
        Assert.Equal(new DateTime(2026, 11, 2), uploads[1].Day);
        Assert.Equal("1.1", uploads[2].VersionName);
        Assert.Equal(8, PlayUploads.NextVersionCode(uploads));
    }

    [Theory]
    [InlineData("1 1.0 2026-10-28 closed\n1 1.0 2026-10-29 closed\n")]
    [InlineData("2 1.0 2026-10-28 closed\n1 1.0 2026-10-29 closed\n")]
    public void A_record_whose_codes_do_not_rise_stops_the_build(string record)
    {
        Assert.Contains("does not rise", Assert.Throws<FormatException>(() => PlayUploads.Read(record)).Message);
    }

    [Theory]
    [InlineData("1 1.0 2026-10-28")]
    [InlineData("1 1.0 2026-10-28 closed extra")]
    [InlineData("0 1.0 2026-10-28 closed")]
    [InlineData("-1 1.0 2026-10-28 closed")]
    [InlineData("one 1.0 2026-10-28 closed")]
    [InlineData("2100000001 1.0 2026-10-28 closed")]
    [InlineData("1 1.0 28.10.2026 closed")]
    [InlineData("1 1.0 2026-10-28 beta")]
    public void A_line_that_is_not_an_upload_stops_the_build(string line)
    {
        Assert.Contains("Line 4", Assert.Throws<FormatException>(() => PlayUploads.Read(Header + line + "\n")).Message);
    }

    [Fact]
    public void After_the_highest_code_no_bundle_follows()
    {
        IReadOnlyList<PlayUpload> uploads = PlayUploads.Read($"{PlayUpload.HighestVersionCode} 9.9 2030-01-01 production\n");

        Assert.Throws<InvalidOperationException>(() => PlayUploads.NextVersionCode(uploads));
    }

    [Fact]
    public void The_line_to_add_reads_back_as_the_same_upload()
    {
        var upload = new PlayUpload(3, "1.0", new DateTime(2026, 11, 9, 15, 30, 0), PlayTrack.Closed);

        string line = PlayUploads.LineOf(upload);

        Assert.Equal("3 1.0 2026-11-09 closed", line);
        PlayUpload read = Assert.Single(PlayUploads.Read(line));
        Assert.Equal((3, "1.0", new DateTime(2026, 11, 9), PlayTrack.Closed), (read.VersionCode, read.VersionName, read.Day, read.Track));
    }

    [Fact]
    public void The_repositorys_record_reads_and_names_the_next_code()
    {
        IReadOnlyList<PlayUpload> uploads = PlayUploads.Read(File.ReadAllText(RepoFiles.RepositoryFile("release/google-play-uploads.txt")));

        Assert.True(PlayUploads.NextVersionCode(uploads) >= 1);
    }
}
