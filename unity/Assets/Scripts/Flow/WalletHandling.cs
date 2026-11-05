#nullable enable
using System;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// How the client holds the player's coins and lives to the rules of the economy, which stay as they are. A closed
    /// set: as the rules have it, or the tester mode's (<see cref="TesterMode"/>).
    /// </summary>
    public enum WalletHandling
    {
        /// <summary>As the rules have it: a price leaves the coins, a loss takes a life, and no level is played without one.</summary>
        Normal,

        /// <summary>
        /// The tester mode's: every price the coins are asked for is paid and the coins stay as they were; no loss
        /// takes a life, and no level waits for one. Whatever the rules give (a win's coins, an ad's, the daily gift,
        /// a painting's reward) still arrives, and every allowance the rules count (the ads of a day, the gift of a
        /// day) is counted as ever.
        /// </summary>
        Unlimited,
    }

    /// <summary>
    /// What a <see cref="WalletHandling"/> does where a price is asked of the coins: the rules are asked as ever, of a
    /// wallet that can pay under the tester mode. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class WalletHandlingRules
    {
        /// <summary>
        /// The wallet the rules are shown when a price is asked of it: the player's own; under
        /// <see cref="WalletHandling.Unlimited"/>, the player's with the coins to cover the price where theirs do not.
        /// </summary>
        public static Wallet ToPay(this WalletHandling handling, Coins price, Wallet wallet) => handling switch
        {
            WalletHandling.Normal => wallet,
            WalletHandling.Unlimited => wallet.Coins.Covers(price) ? wallet : wallet.WithCoins(price),
            _ => throw new ArgumentOutOfRangeException(nameof(handling), handling, "A way of holding the wallet the client does not know."),
        };

        /// <summary>
        /// What buying something for a price comes to: the rules' own answer for the wallet; under
        /// <see cref="WalletHandling.Unlimited"/>, their answer for a wallet that can pay (<see cref="ToPay"/>), with
        /// the coins the player had, so the price never leaves them.
        /// </summary>
        /// <param name="buy">The rules' purchase of the thing, for a wallet.</param>
        public static Purchase Buy(this WalletHandling handling, Coins price, Wallet wallet, Func<Wallet, Purchase> buy)
        {
            Purchase purchase = buy(handling.ToPay(price, wallet));
            return handling == WalletHandling.Unlimited && purchase is Purchase.Bought bought
                ? new Purchase.Bought(bought.Wallet.WithCoins(wallet.Coins))
                : purchase;
        }
    }
}
