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
    /// came of the player's answer; the settings' account row and the account card it opens (the account, the
    /// "Bulut kaydı" switch, the last sync, the player code, "Verilerimi sil" and its questions), and the cards after a
    /// deletion. The design canvas draws none of them; they are written in the manner of the cards it does draw. The
    /// platform is named by its product's name in every language. No Unity dependency.
    /// </summary>
    public static class AccountWords
    {
        /// <summary>The account card's nameplate, and its first row's label.</summary>
        public static string CardTitle => Speech.Say(WordId.AccountCardTitle);

        /// <summary>The account card's other rows' labels.</summary>
        public static string CloudSaveRow => Speech.Say(WordId.AccountRowCloudSave);

        public static string LastSyncRow => Speech.Say(WordId.AccountRowLastSync);

        public static string PlayerCodeRow => Speech.Say(WordId.AccountRowPlayerCode);

        /// <summary>The account card's line under the rows: what the cloud save keeps, and where.</summary>
        public static string CardLine => Speech.Say(WordId.AccountCardLine);

        /// <summary>The player code's value before the first sign-in: there is no code yet.</summary>
        public static string NoCodeYet => Speech.Say(WordId.AccountSyncNever);

        /// <summary>The player code's value for a moment after a touch copied it.</summary>
        public static string CodeCopied => Speech.Say(WordId.AccountCodeCopied);

        /// <summary>"Verilerimi sil", the button and the nameplate of the cards about the deletion.</summary>
        public static string DeleteMyData => Speech.Say(WordId.AccountDeleteMyData);

        /// <summary>The first question, and the line under it.</summary>
        public static string DeleteQuestion => Speech.Say(WordId.AccountDeleteQuestion);

        public static string DeleteTraceLine => Speech.Say(WordId.AccountDeleteTraceLine);

        /// <summary>The card while the deletion runs.</summary>
        public static string Deleting => Speech.Say(WordId.AccountDeleting);

        /// <summary>The second question, about this phone's progress, the line under it and its answer that keeps it.</summary>
        public static string ForgetProgressQuestion => Speech.Say(WordId.AccountForgetProgressQuestion);

        public static string ForgetProgressLine => Speech.Say(WordId.AccountForgetProgressLine);

        public static string KeepProgress => Speech.Say(WordId.AccountKeepProgress);

        /// <summary>What the cloud's deletion came to, above the second question: deleted, or nothing there.</summary>
        public static string CloudDeleted(bool thereWereNone) => Speech.Say(thereWereNone ? WordId.AccountNothingInTheCloud : WordId.AccountCloudDeleted);

        /// <summary>Why nothing was deleted, and that nothing was: the two lines of the card when the deletion did not come about.</summary>
        public static (string Why, string NothingDeleted) NotDeleted(DeletionFailure why) => (
            Speech.Say(why == DeletionFailure.NoNetwork ? WordId.AccountNeedsNetwork : WordId.AccountCannotDeleteNow), Speech.Say(WordId.AccountNothingDeleted));

        /// <summary>The line after a deletion: the cloud save is off now, and turned on in the settings.</summary>
        public static string SwitchOffAfterDeletion => Speech.Say(WordId.AccountSwitchOffAfterDeletion);

        /// <summary>
        /// Where the account stands, as the settings' row says it under its name and the account card as its value:
        /// off, no cloud in this build, connecting, out of reach, this phone's account, linked to the platform.
        /// </summary>
        public static string StateOf(AccountView view) => view.Account switch
        {
            Account.Off { Why: ServicesOff.NoCloudProject } => Speech.Say(WordId.AccountStateNoCloud),
            Account.Off _ => Speech.Say(WordId.AccountStateOff),
            Account.Starting _ => Speech.Say(WordId.AccountStateStarting),
            Account.Unreachable _ => Speech.Say(WordId.AccountStateUnreachable),
            Account.Anonymous _ => Speech.Say(WordId.AccountStateThisPhone),
            Account.Linked linked => Speech.Say(WordId.AccountStateLinked, Named(linked.Platform)),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view.Account, "An account the settings have no words for."),
        };

        /// <summary>
        /// When the save was last synced, as the account card says it, as of when the view was made: not yet, just now,
        /// so many minutes, hours or days before; out of reach while the last try failed.
        /// </summary>
        public static string LastSyncOf(AccountView view)
        {
            if (view.LastTryFailed && view.IsSwitchedOn)
            {
                return Speech.Say(WordId.AccountStateUnreachable);
            }

            if (!(view.LastSynced is DateTimeOffset synced))
            {
                return Speech.Say(WordId.AccountSyncNever);
            }

            TimeSpan since = view.SeenAt - synced;
            return since < TimeSpan.FromMinutes(1) ? Speech.Say(WordId.AccountSyncJustNow)
                : since < TimeSpan.FromHours(1) ? Counted(WordId.AccountSyncMinutesAgo, (int)since.TotalMinutes)
                : since < TimeSpan.FromDays(1) ? Counted(WordId.AccountSyncHoursAgo, (int)since.TotalHours)
                : Counted(WordId.AccountSyncDaysAgo, (int)Math.Min(since.TotalDays, 9999));
        }

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
            AccountNews.CloudDataDeleted deleted => new AccountCardWords(DeleteMyData, CloudDeleted(deleted.ThereWereNone), SwitchOffAfterDeletion),
            AccountNews.CloudDataNotDeleted notDeleted => new AccountCardWords(DeleteMyData, NotDeleted(notDeleted.Why).Why, NotDeleted(notDeleted.Why).NothingDeleted),
            AccountNews.StartedAnew _ => new AccountCardWords(
                Speech.Say(WordId.AccountEverythingDeletedTitle), Speech.Say(WordId.AccountEverythingDeletedLine), SwitchOffAfterDeletion),
            _ => throw new ArgumentOutOfRangeException(nameof(news), news, "The account's cards have no words for this news."),
        };

        private static (string Name, string Value) Named(SignInPlatform platform) => ("platform", SignInPlatforms.NameOf(platform));

        private static string Counted(WordId id, int count) => Speech.SayCounted(id, count, ("count", count.ToString(CultureInfo.InvariantCulture)));
    }
}
