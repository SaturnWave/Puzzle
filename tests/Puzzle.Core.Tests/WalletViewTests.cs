using Puzzle.Client.Flow;
using Puzzle.Meta;
using static Puzzle.Core.Tests.MetaSupport;

namespace Puzzle.Core.Tests;

/// <summary>
/// What the museum's wallet row and the lives card show of a wallet at an instant (<c>WalletView</c> and
/// <c>WalletWords</c>, the Unity project's source files, compiled into these tests): the coins as the canvas writes
/// them, the lives that are there by then, and the waits for the next life and for all of them.
/// </summary>
public class WalletViewTests
{
    private static readonly LifeRules FiveLives = new(max: 5, perLife: TimeSpan.FromMinutes(30));

    private static Wallet WalletWith(long coins, LifeMeter lives) => Wallet.Empty.WithCoins(new Coins(coins)).WithLives(lives);

    /// <summary>A meter with this many lives missing, the next back so many seconds after the evening.</summary>
    private static LifeMeter Missing(int lives, int nextInSeconds) => new(lives, Evening.AddSeconds(nextInSeconds));

    [Fact]
    public void A_full_meter_shows_every_life_and_says_full()
    {
        WalletView view = WalletView.Of(WalletWith(200, LifeMeter.Full), FiveLives, Evening);

        Assert.Equal((5, 5, true, true), (view.Lives, view.MostLives, view.IsFull, view.HasALife));
        Assert.Null(view.UntilNextLife);
        Assert.Null(view.UntilFull);
        Assert.Equal(("200", "5", "Dolu", "5 / 5"), (WalletWords.Amount(view.Coins), WalletWords.OnTheHeart(view), WalletWords.LivesPill(view), WalletWords.LivesOfMost(view)));
    }

    [Fact]
    public void A_meter_that_counts_shows_the_wait_for_the_next_life_and_for_all_of_them()
    {
        // Two lives missing, the next back 12 minutes 34 seconds from now: the last one thirty minutes after it.
        WalletView view = WalletView.Of(WalletWith(1250, Missing(2, 12 * 60 + 34)), FiveLives, Evening);

        Assert.Equal((3, false, true), (view.Lives, view.IsFull, view.HasALife));
        Assert.Equal(TimeSpan.FromSeconds(12 * 60 + 34), view.UntilNextLife);
        Assert.Equal(TimeSpan.FromSeconds(42 * 60 + 34), view.UntilFull);
        Assert.Equal(("1.250", "3", "12:34", "3 / 5"), (WalletWords.Amount(view.Coins), WalletWords.OnTheHeart(view), WalletWords.LivesPill(view), WalletWords.LivesOfMost(view)));
        Assert.Equal("Sonraki can 12:34", WalletWords.NextLife(view.UntilNextLife!.Value));
        Assert.Equal("Canların 43 dk sonra dolar", WalletWords.FullAfter(view.UntilFull!.Value));
    }

    [Fact]
    public void Lives_come_back_as_the_clock_moves_on_without_a_loss_to_settle_them()
    {
        Wallet wallet = WalletWith(0, Missing(5, 10 * 60));

        Assert.Equal(0, WalletView.Of(wallet, FiveLives, Evening).Lives);
        Assert.Equal(1, WalletView.Of(wallet, FiveLives, MinutesLater(10)).Lives);
        Assert.Equal(TimeSpan.FromMinutes(30), WalletView.Of(wallet, FiveLives, MinutesLater(10)).UntilNextLife);
        Assert.True(WalletView.Of(wallet, FiveLives, MinutesLater(10 + 4 * 30)).IsFull);
    }

    [Fact]
    public void A_player_without_a_life_is_told_how_long_until_the_next_and_may_not_play()
    {
        WalletView view = WalletView.Of(WalletWith(0, Missing(5, 7 * 60 + 30)), FiveLives, Evening);

        Assert.Equal((0, false, false), (view.Lives, view.IsFull, view.HasALife));
        Assert.Equal(("0", "0", "07:30"), (WalletWords.Amount(view.Coins), WalletWords.OnTheHeart(view), WalletWords.LivesPill(view)));
        Assert.Equal(TimeSpan.FromMinutes(7.5 + 4 * 30), view.UntilFull);
        Assert.Equal("Canların 2 sa 8 dk sonra dolar", WalletWords.FullAfter(view.UntilFull!.Value));
    }

    [Fact]
    public void A_clock_set_back_brings_no_life_and_shows_no_wait_longer_than_a_life_takes()
    {
        // The next life was due ten minutes after the evening; the phone's clock now says three hours earlier.
        Wallet wallet = WalletWith(10, Missing(1, 10 * 60));
        WalletView view = WalletView.Of(wallet, FiveLives, Evening.AddHours(-3));

        Assert.Equal(4, view.Lives);
        Assert.Equal(FiveLives.PerLife, view.UntilNextLife);
        Assert.Equal("30:00", WalletWords.LivesPill(view));
        Assert.Equal(view, WalletView.Of(wallet, FiveLives, Evening.AddHours(-1)));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(999L, "999")]
    [InlineData(12_500L, "12.500")]
    [InlineData(1_234_567L, "1.234.567")]
    [InlineData(3_000_000_000L, "3.000.000.000")]
    public void Coins_are_written_with_their_thousands_set_apart_however_many_there_are(long coins, string written)
    {
        WalletView view = WalletView.Of(WalletWith(coins, LifeMeter.Full), FiveLives, Evening);

        Assert.Equal(written, WalletWords.Amount(view.Coins));
    }

    [Fact]
    public void A_view_with_other_coins_keeps_its_lives_and_two_views_of_one_instant_are_equal()
    {
        WalletView view = WalletView.Of(WalletWith(1250, Missing(2, 12 * 60)), FiveLives, Evening);
        WalletView counting = view.WithCoins(new Coins(1000));

        Assert.Equal((1000L, view.Lives, view.UntilNextLife), (counting.Coins.Amount, counting.Lives, counting.UntilNextLife));
        Assert.NotEqual(view, counting);
        Assert.Equal(view, counting.WithCoins(view.Coins));
        Assert.Equal(view.GetHashCode(), counting.WithCoins(view.Coins).GetHashCode());
        Assert.Throws<ArgumentNullException>(() => WalletView.Of(null!, FiveLives, Evening));
    }
}
