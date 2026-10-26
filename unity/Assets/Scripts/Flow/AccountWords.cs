#nullable enable
using System;
using System.Globalization;
using Puzzle.Client.Cloud;
using Puzzle.Client.Words;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>What a card about the player's account says: its nameplate and its two lines.</summary>
    public sealed class AccountCardWords
    {
        public AccountCardWords(string title, string firstLine, string secondLine)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            FirstLine = firstLine ?? throw new ArgumentNullException(nameof(firstLine));
            SecondLine = secondLine ?? throw new ArgumentNullException(nameof(secondLine));
        }

        public string Title { get; }

        public string FirstLine { get; }

        public string SecondLine { get; }

        public override string ToString() => $"{Title}: {FirstLine} / {SecondLine}";
    }

    /// <summary>
    /// What the cards about the player's account in the cloud say (docs/research/server-v1.md §5.3, §5.7), in the
    /// language the game speaks (<see cref="Speech"/>): the offer to link, made once after the first won level, and what
    /// came of the player's answer. The design canvas draws none of them; they are written in the manner of the cards it
    /// does draw. The platform is named by its product's name in every language. No Unity dependency.
    /// </summary>
    public static class AccountWords
    {
        /// <summary>The answers of the offer to link.</summary>
        public static string Link => Speech.Say(WordId.AccountLinkButton);

        public static string NotNow => Speech.Say(WordId.AccountNotNowButton);

        /// <summary>The answers of the question of the other save.</summary>
        public static string Switch => Speech.Say(WordId.AccountSwitchButton);

        public static string Stay => Speech.Say(WordId.AccountStayButton);

        /// <summary>The offer to link the account to the platform: the save is on this phone only, and the platform brings it to a new one.</summary>
        public static AccountCardWords Offer(SignInPlatform platform) => new AccountCardWords(
            Speech.Say(WordId.AccountOfferTitle), Speech.Say(WordId.AccountOfferLine), Speech.Say(WordId.AccountOfferPlatformLine, Named(platform)));

        /// <summary>The card of what the account has to tell.</summary>
        /// <param name="levelNumber">The level the player stands on, counted from 1: the card of a save that came back names it.</param>
        /// <param name="coins">The coins in the wallet: the card of a save that came back names them.</param>
        /// <exception cref="ArgumentOutOfRangeException">News the cards have no words for.</exception>
        public static AccountCardWords Of(AccountNews news, int levelNumber, Coins coins) => news switch
        {
            AccountNews.Linked linked => new AccountCardWords(
                Speech.Say(WordId.AccountLinkedTitle), Speech.Say(WordId.AccountLinkedLine, Named(linked.Platform)), Speech.Say(WordId.AccountLinkedNewPhoneLine)),
            AccountNews.NotLinked { Why: LinkFailure.NotOnThisPhone } missing => new AccountCardWords(
                Speech.Say(WordId.AccountNotLinkedTitle), Speech.Say(WordId.AccountPlatformMissingLine, Named(missing.Platform)), Speech.Say(WordId.AccountSafeLine)),
            AccountNews.NotLinked _ => new AccountCardWords(
                Speech.Say(WordId.AccountNotLinkedTitle), Speech.Say(WordId.AccountUnreachableLine), Speech.Say(WordId.AccountSafeLine)),
            AccountNews.OtherSaveFound other => new AccountCardWords(
                Speech.Say(WordId.AccountOtherSaveTitle), Speech.Say(WordId.AccountOtherSaveLine, Named(other.Platform)), Speech.Say(WordId.AccountOtherSaveMergeLine)),
            AccountNews.SaveCameBack _ => new AccountCardWords(
                Speech.Say(WordId.AccountSaveBackTitle),
                Speech.Say(WordId.AccountSaveBackLine, ("level", levelNumber.ToString(CultureInfo.InvariantCulture)), ("coins", NumberWords.Amount(coins.Amount))),
                Speech.Say(WordId.AccountSaveBackGoOnLine)),
            AccountNews.NotSwitched _ => new AccountCardWords(
                Speech.Say(WordId.AccountNotSwitchedTitle), Speech.Say(WordId.AccountUnreachableLine), Speech.Say(WordId.AccountSafeLine)),
            _ => throw new ArgumentOutOfRangeException(nameof(news), news, "The account's cards have no words for this news."),
        };

        private static (string Name, string Value) Named(SignInPlatform platform) => ("platform", SignInPlatforms.NameOf(platform));
    }
}
