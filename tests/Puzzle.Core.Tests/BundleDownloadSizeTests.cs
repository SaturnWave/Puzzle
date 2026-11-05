using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The download sizes of the Google Play bundle as bundletool says them (2026-10-28): "MIN,MAX" and a line of two
/// sizes in bytes; a base module whose largest download is beyond Play's 200 MB, read as 200 million bytes, stops the
/// build. <c>BundleDownloadSize</c> is the Unity project's Editor source file, compiled into these tests; the bundle
/// build reads <c>bundletool get-size total</c> with it (unity/Assets/Editor/PlayBundle.cs).
/// </summary>
public class BundleDownloadSizeTests
{
    [Fact]
    public void Bundletools_sizes_are_read()
    {
        BundleDownloadSize size = BundleDownloadSize.Parse("MIN,MAX\r\n38212345,41000000\r\n");

        Assert.Equal((38212345L, 41000000L), (size.SmallestBytes, size.LargestBytes));
        Assert.Equal("38.2–41.0 MB", size.ToString());
        Assert.Equal("41.0 MB", BundleDownloadSize.Parse("MIN,MAX\n41000000,41000000").ToString());
    }

    [Fact]
    public void A_base_beyond_two_hundred_million_bytes_is_beyond_plays_limit()
    {
        Assert.True(BundleDownloadSize.Parse("MIN,MAX\n1,200000000").IsWithinPlaysBaseLimit);
        Assert.False(BundleDownloadSize.Parse("MIN,MAX\n1,200000001").IsWithinPlaysBaseLimit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MIN,MAX")]
    [InlineData("MIN,MAX\n12")]
    [InlineData("MIN,MAX\n12,ab")]
    [InlineData("MIN,MAX\n20,10")]
    [InlineData("SDK,MIN,MAX\n26,10,20")]
    [InlineData("MIN,MAX\n-1,20")]
    public void What_is_not_two_sizes_is_not_read(string said)
    {
        Assert.Throws<FormatException>(() => BundleDownloadSize.Parse(said));
    }
}
